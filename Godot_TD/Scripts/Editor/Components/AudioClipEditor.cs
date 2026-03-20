using Godot;
using System;

namespace JunkyardTD
{
    /// <summary>
    /// In-editor audio clip editor: waveform display, trim, gain, fade in/out, normalize.
    /// Reads raw WAV files, applies non-destructive edits, saves to Edited/ folder.
    /// </summary>
    public partial class AudioClipEditor : VBoxContainer
    {
        // WAV data
        private byte[] _rawPcm;
        private short[] _samples; // mono-mixed for display + editing
        private int _sampleRate;
        private int _bitsPerSample;
        private int _channels;
        private string _sourcePath;
        private string _fileName;

        // Edit state
        private float _trimStart; // 0.0–1.0 ratio
        private float _trimEnd = 1f;
        private float _gainDb;
        private float _fadeInMs;
        private float _fadeOutMs;

        // UI
        private WaveformView _waveform;
        private HSlider _trimStartSlider, _trimEndSlider;
        private HSlider _gainSlider;
        private HSlider _fadeInSlider, _fadeOutSlider;
        private Label _infoLabel;
        private Label _trimStartLabel, _trimEndLabel;
        private Label _gainLabel, _fadeInLabel, _fadeOutLabel;

        // Events
        public event Action<string> OnSaved;
        public event Action OnClosed;

        private static readonly Color Accent = EditorStyles.AccentSound;

        public AudioClipEditor()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);
        }

        public override void _Ready()
        {
            BuildUI();
        }

        public void LoadFile(string resPath)
        {
            _sourcePath = resPath;
            _fileName = resPath.GetFile();
            _trimStart = 0;
            _trimEnd = 1;
            _gainDb = 0;
            _fadeInMs = 0;
            _fadeOutMs = 0;

            if (!ParseWavFile(resPath))
            {
                _infoLabel.Text = $"Failed to load: {_fileName}";
                _infoLabel.AddThemeColorOverride("font_color", EditorStyles.StatusError);
                _samples = null;
                _waveform.Samples = null;
                _waveform.QueueRedraw();
                return;
            }

            float duration = (float)_samples.Length / _sampleRate;
            _infoLabel.Text = $"{_fileName}  |  {duration:F2}s  |  {_sampleRate}Hz  {_bitsPerSample}bit  {(_channels > 1 ? "stereo" : "mono")}";
            _infoLabel.AddThemeColorOverride("font_color", Accent);

            // Reset sliders
            float maxTrimMs = duration * 1000f;
            _trimStartSlider.MaxValue = maxTrimMs;
            _trimStartSlider.Value = 0;
            _trimEndSlider.MaxValue = maxTrimMs;
            _trimEndSlider.Value = maxTrimMs;
            _gainSlider.Value = 0;
            _fadeInSlider.MaxValue = Mathf.Min(2000, maxTrimMs / 2);
            _fadeInSlider.Value = 0;
            _fadeOutSlider.MaxValue = Mathf.Min(2000, maxTrimMs / 2);
            _fadeOutSlider.Value = 0;

            UpdateTrimLabels();
            SyncWaveform();
        }

        // ── UI ──

        private void BuildUI()
        {
            // Header
            var headerRow = new HBoxContainer();
            headerRow.AddThemeConstantOverride("separation", 8);
            headerRow.AddChild(EditorStyles.MakeLabel("Clip Editor", EditorStyles.FontHeader, Accent));
            headerRow.AddChild(MakeSpacer());
            var closeBtn = EditorStyles.MakeButton("X Close", EditorStyles.FontSmall, EditorStyles.StatusError);
            closeBtn.Pressed += () => OnClosed?.Invoke();
            headerRow.AddChild(closeBtn);
            AddChild(headerRow);

            // Info
            _infoLabel = EditorStyles.MakeLabel("No file loaded", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            AddChild(_infoLabel);

            // Waveform
            _waveform = new WaveformView();
            _waveform.CustomMinimumSize = new Vector2(0, 120);
            _waveform.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _waveform.OnTrimChanged += (start, end) =>
            {
                _trimStart = start;
                _trimEnd = end;
                float dur = (float)(_samples?.Length ?? 0) / Mathf.Max(_sampleRate, 1);
                _trimStartSlider.SetValueNoSignal(start * dur * 1000f);
                _trimEndSlider.SetValueNoSignal(end * dur * 1000f);
                UpdateTrimLabels();
            };
            AddChild(_waveform);

            // Trim controls
            AddChild(MakeSliderRow("Trim Start:", 0, 1000, 1, 0,
                out _trimStartSlider, out _trimStartLabel, "ms", v =>
                {
                    float dur = (float)(_samples?.Length ?? 0) / Mathf.Max(_sampleRate, 1);
                    _trimStart = dur > 0 ? (float)(v / 1000.0 / dur) : 0;
                    if (_trimStart >= _trimEnd)
                    {
                        _trimStart = _trimEnd - 0.001f;
                        _trimStartSlider.SetValueNoSignal(_trimStart * dur * 1000f);
                    }
                    UpdateTrimLabels();
                    SyncWaveform();
                }));

            AddChild(MakeSliderRow("Trim End:", 0, 1000, 1, 1000,
                out _trimEndSlider, out _trimEndLabel, "ms", v =>
                {
                    float dur = (float)(_samples?.Length ?? 0) / Mathf.Max(_sampleRate, 1);
                    _trimEnd = dur > 0 ? (float)(v / 1000.0 / dur) : 1;
                    if (_trimEnd <= _trimStart)
                    {
                        _trimEnd = _trimStart + 0.001f;
                        _trimEndSlider.SetValueNoSignal(_trimEnd * dur * 1000f);
                    }
                    UpdateTrimLabels();
                    SyncWaveform();
                }));

            // Gain
            AddChild(MakeSliderRow("Gain:", -20, 12, 0.5, 0,
                out _gainSlider, out _gainLabel, "dB", v =>
                {
                    _gainDb = (float)v;
                    _gainLabel.Text = $"{v:+0.0;-0.0;0}dB";
                }));

            // Fade In
            AddChild(MakeSliderRow("Fade In:", 0, 2000, 10, 0,
                out _fadeInSlider, out _fadeInLabel, "ms", v =>
                {
                    _fadeInMs = (float)v;
                    _fadeInLabel.Text = $"{v:0}ms";
                }));

            // Fade Out
            AddChild(MakeSliderRow("Fade Out:", 0, 2000, 10, 0,
                out _fadeOutSlider, out _fadeOutLabel, "ms", v =>
                {
                    _fadeOutMs = (float)v;
                    _fadeOutLabel.Text = $"{v:0}ms";
                }));

            // Buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 6);

            var previewBtn = EditorStyles.MakeButton("Preview", EditorStyles.FontSmall, Accent);
            previewBtn.CustomMinimumSize = new Vector2(0, 28);
            previewBtn.Pressed += Preview;
            btnRow.AddChild(previewBtn);

            var stopBtn = EditorStyles.MakeButton("Stop", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            stopBtn.CustomMinimumSize = new Vector2(0, 28);
            stopBtn.Pressed += StopPreview;
            btnRow.AddChild(stopBtn);

            var normBtn = EditorStyles.MakeButton("Normalize", EditorStyles.FontSmall, EditorStyles.TextAccent);
            normBtn.CustomMinimumSize = new Vector2(0, 28);
            normBtn.Pressed += Normalize;
            btnRow.AddChild(normBtn);

            btnRow.AddChild(MakeSpacer());

            var saveBtn = EditorStyles.MakeButton("Save Edited", EditorStyles.FontSmall, EditorStyles.StatusSaved);
            saveBtn.CustomMinimumSize = new Vector2(0, 28);
            saveBtn.Pressed += SaveEdited;
            btnRow.AddChild(saveBtn);

            var overwriteBtn = EditorStyles.MakeButton("Overwrite Original", EditorStyles.FontSmall, EditorStyles.StatusError);
            overwriteBtn.CustomMinimumSize = new Vector2(0, 28);
            overwriteBtn.Pressed += SaveOverwrite;
            btnRow.AddChild(overwriteBtn);

            AddChild(btnRow);
        }

        private HBoxContainer MakeSliderRow(string label, double min, double max, double step,
            double initial, out HSlider slider, out Label valueLabel, string suffix, Action<double> onChange)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var lbl = EditorStyles.MakeLabel(label, EditorStyles.FontSmall, EditorStyles.TextSecondary);
            lbl.CustomMinimumSize = new Vector2(70, 0);
            row.AddChild(lbl);

            slider = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = initial };
            slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(slider);

            valueLabel = EditorStyles.MakeLabel($"{initial}{suffix}", EditorStyles.FontTiny, EditorStyles.TextMuted);
            valueLabel.CustomMinimumSize = new Vector2(55, 0);
            valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(valueLabel);

            var capturedLabel = valueLabel;
            slider.ValueChanged += v =>
            {
                capturedLabel.Text = suffix == "dB" ? $"{v:+0.0;-0.0;0}{suffix}" : $"{v:0}{suffix}";
                onChange(v);
            };

            return row;
        }

        private static Control MakeSpacer()
        {
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            return spacer;
        }

        private void UpdateTrimLabels()
        {
            float dur = (float)(_samples?.Length ?? 0) / Mathf.Max(_sampleRate, 1);
            float startSec = _trimStart * dur;
            float endSec = _trimEnd * dur;
            float trimDur = endSec - startSec;
            _trimStartLabel.Text = $"{startSec:F2}s";
            _trimEndLabel.Text = $"{endSec:F2}s ({trimDur:F2}s)";
        }

        private void SyncWaveform()
        {
            if (_waveform == null) return;
            _waveform.Samples = _samples;
            _waveform.TrimStart = _trimStart;
            _waveform.TrimEnd = _trimEnd;
            _waveform.QueueRedraw();
        }

        // ── WAV Parsing ──

        private bool ParseWavFile(string resPath)
        {
            using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"[AudioClipEditor] Cannot open: {resPath} — {FileAccess.GetOpenError()}");
                return false;
            }

            var bytes = file.GetBuffer((long)file.GetLength());
            if (bytes.Length < 44) return false;

            // Validate RIFF/WAVE
            if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F') return false;
            if (bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E') return false;

            _rawPcm = null;
            _sampleRate = 0;
            _bitsPerSample = 16;
            _channels = 1;

            // Walk chunks
            int pos = 12;
            while (pos < bytes.Length - 8)
            {
                string chunkId = $"{(char)bytes[pos]}{(char)bytes[pos + 1]}{(char)bytes[pos + 2]}{(char)bytes[pos + 3]}";
                int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                if (chunkSize < 0 || pos + 8 + chunkSize > bytes.Length) break;

                if (chunkId == "fmt ")
                {
                    _channels = BitConverter.ToInt16(bytes, pos + 10);
                    _sampleRate = BitConverter.ToInt32(bytes, pos + 12);
                    _bitsPerSample = BitConverter.ToInt16(bytes, pos + 22);
                }
                else if (chunkId == "data")
                {
                    _rawPcm = new byte[chunkSize];
                    Array.Copy(bytes, pos + 8, _rawPcm, 0, chunkSize);
                }

                pos += 8 + chunkSize;
                if (chunkSize % 2 != 0) pos++; // pad byte
            }

            if (_rawPcm == null || _sampleRate == 0) return false;

            ConvertToSamples();
            return true;
        }

        private void ConvertToSamples()
        {
            if (_bitsPerSample == 16)
            {
                int totalSamples = _rawPcm.Length / 2;
                int framesCount = totalSamples / _channels;
                _samples = new short[framesCount];

                for (int f = 0; f < framesCount; f++)
                {
                    if (_channels == 1)
                    {
                        _samples[f] = BitConverter.ToInt16(_rawPcm, f * 2);
                    }
                    else
                    {
                        int sum = 0;
                        for (int c = 0; c < _channels; c++)
                            sum += BitConverter.ToInt16(_rawPcm, (f * _channels + c) * 2);
                        _samples[f] = (short)(sum / _channels);
                    }
                }
            }
            else if (_bitsPerSample == 8)
            {
                int framesCount = _rawPcm.Length / _channels;
                _samples = new short[framesCount];

                for (int f = 0; f < framesCount; f++)
                {
                    if (_channels == 1)
                    {
                        _samples[f] = (short)(((_rawPcm[f] - 128) / 128f) * 32767);
                    }
                    else
                    {
                        int sum = 0;
                        for (int c = 0; c < _channels; c++)
                            sum += _rawPcm[f * _channels + c] - 128;
                        _samples[f] = (short)((sum / (float)_channels / 128f) * 32767);
                    }
                }
            }
            else
            {
                _samples = new short[_rawPcm.Length / (_bitsPerSample / 8) / _channels];
                int bytesPerSample = _bitsPerSample / 8;
                for (int f = 0; f < _samples.Length; f++)
                {
                    int bytePos = f * _channels * bytesPerSample;
                    if (bytePos + 1 < _rawPcm.Length)
                    {
                        _samples[f] = BitConverter.ToInt16(_rawPcm, bytePos + bytesPerSample - 2);
                    }
                }
            }
        }

        // ── Edit Operations ──

        private short[] ApplyEdits()
        {
            if (_samples == null) return null;

            int startSample = (int)(_trimStart * _samples.Length);
            int endSample = (int)(_trimEnd * _samples.Length);
            startSample = Mathf.Clamp(startSample, 0, _samples.Length);
            endSample = Mathf.Clamp(endSample, startSample, _samples.Length);
            int length = endSample - startSample;
            if (length <= 0) return null;

            var result = new short[length];
            Array.Copy(_samples, startSample, result, 0, length);

            // Apply gain
            if (Mathf.Abs(_gainDb) > 0.01f)
            {
                float gainLinear = Mathf.Pow(10f, _gainDb / 20f);
                for (int i = 0; i < result.Length; i++)
                    result[i] = ClampSample(result[i] * gainLinear);
            }

            // Apply fade in
            if (_fadeInMs > 0)
            {
                int fadeSamples = (int)(_fadeInMs / 1000f * _sampleRate);
                fadeSamples = Mathf.Min(fadeSamples, result.Length);
                for (int i = 0; i < fadeSamples; i++)
                {
                    float t = (float)i / fadeSamples;
                    result[i] = ClampSample(result[i] * t);
                }
            }

            // Apply fade out
            if (_fadeOutMs > 0)
            {
                int fadeSamples = (int)(_fadeOutMs / 1000f * _sampleRate);
                fadeSamples = Mathf.Min(fadeSamples, result.Length);
                int fadeStart = result.Length - fadeSamples;
                for (int i = fadeStart; i < result.Length; i++)
                {
                    float t = 1f - (float)(i - fadeStart) / fadeSamples;
                    result[i] = ClampSample(result[i] * t);
                }
            }

            return result;
        }

        private static short ClampSample(float value)
        {
            return (short)Mathf.Clamp(Mathf.RoundToInt(value), short.MinValue, short.MaxValue);
        }

        // ── Preview ──

        private AudioStreamPlayer _previewPlayer;

        private void Preview()
        {
            var edited = ApplyEdits();
            if (edited == null) return;

            var stream = SamplesToStream(edited);

            if (_previewPlayer == null)
            {
                _previewPlayer = new AudioStreamPlayer();
                _previewPlayer.Bus = "SFX";
                AddChild(_previewPlayer);
            }

            _previewPlayer.Stream = stream;
            _previewPlayer.Play();
            _infoLabel.Text = $"Previewing: {edited.Length / (float)_sampleRate:F2}s";
            _infoLabel.AddThemeColorOverride("font_color", Accent);
        }

        private void StopPreview()
        {
            _previewPlayer?.Stop();
        }

        private AudioStreamWav SamplesToStream(short[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

            var stream = new AudioStreamWav();
            stream.Format = AudioStreamWav.FormatEnum.Format16Bits;
            stream.MixRate = _sampleRate;
            stream.Stereo = false;
            stream.Data = bytes;
            return stream;
        }

        // ── Normalize ──

        private void Normalize()
        {
            if (_samples == null) return;

            int startSample = (int)(_trimStart * _samples.Length);
            int endSample = (int)(_trimEnd * _samples.Length);

            short peak = 0;
            for (int i = startSample; i < endSample; i++)
            {
                short abs = (short)Mathf.Abs(_samples[i]);
                if (abs > peak) peak = abs;
            }

            if (peak == 0) return;

            float targetGain = 32767f / peak;
            float gainDb = 20f * Mathf.Log(targetGain) / Mathf.Log(10f);

            _gainDb = gainDb;
            _gainSlider.Value = Mathf.Clamp(gainDb, -20, 12);
            _gainLabel.Text = $"{gainDb:+0.0;-0.0;0}dB";

            _infoLabel.Text = $"Normalized: peak was {peak}/32767, gain set to {gainDb:+0.0}dB";
            _infoLabel.AddThemeColorOverride("font_color", EditorStyles.StatusSaved);
        }

        // ── Save ──

        private void SaveEdited()
        {
            DoSave(false);
        }

        private void SaveOverwrite()
        {
            DoSave(true);
        }

        private void DoSave(bool overwrite)
        {
            var edited = ApplyEdits();
            if (edited == null)
            {
                _infoLabel.Text = "Nothing to save";
                _infoLabel.AddThemeColorOverride("font_color", EditorStyles.StatusError);
                return;
            }

            string savePath;
            if (overwrite)
            {
                savePath = _sourcePath;
            }
            else
            {
                string dir = "res://Assets/Audio/Edited";
                if (!DirAccess.DirExistsAbsolute(dir))
                    DirAccess.MakeDirRecursiveAbsolute(dir);

                string baseName = _fileName.GetBaseName();
                string ext = _fileName.GetExtension();
                savePath = $"{dir}/{baseName}_edited.{ext}";
            }

            if (WriteWav(savePath, edited, _sampleRate))
            {
                _infoLabel.Text = $"Saved: {savePath.GetFile()}";
                _infoLabel.AddThemeColorOverride("font_color", EditorStyles.StatusSaved);
                OnSaved?.Invoke(savePath);
                GD.Print($"[AudioClipEditor] Saved edited clip: {savePath}");
            }
            else
            {
                _infoLabel.Text = "Save failed!";
                _infoLabel.AddThemeColorOverride("font_color", EditorStyles.StatusError);
            }
        }

        private static bool WriteWav(string path, short[] samples, int sampleRate)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"[AudioClipEditor] Cannot write: {path} — {FileAccess.GetOpenError()}");
                return false;
            }

            int dataSize = samples.Length * 2;
            int fileSize = 36 + dataSize;

            // RIFF header
            file.Store8((byte)'R'); file.Store8((byte)'I'); file.Store8((byte)'F'); file.Store8((byte)'F');
            file.Store32((uint)fileSize);
            file.Store8((byte)'W'); file.Store8((byte)'A'); file.Store8((byte)'V'); file.Store8((byte)'E');

            // fmt chunk
            file.Store8((byte)'f'); file.Store8((byte)'m'); file.Store8((byte)'t'); file.Store8((byte)' ');
            file.Store32(16);
            file.Store16(1);
            file.Store16(1);
            file.Store32((uint)sampleRate);
            file.Store32((uint)(sampleRate * 2));
            file.Store16(2);
            file.Store16(16);

            // data chunk
            file.Store8((byte)'d'); file.Store8((byte)'a'); file.Store8((byte)'t'); file.Store8((byte)'a');
            file.Store32((uint)dataSize);

            var bytes = new byte[dataSize];
            Buffer.BlockCopy(samples, 0, bytes, 0, dataSize);
            file.StoreBuffer(bytes);

            return true;
        }

        // ── Waveform View ──

        private partial class WaveformView : Control
        {
            public short[] Samples;
            public float TrimStart;
            public float TrimEnd = 1f;
            public event Action<float, float> OnTrimChanged;

            private bool _dragging;
            private float _dragStartX;

            private static readonly Color BgColor = EditorStyles.BgField;
            private static readonly Color WaveColor = EditorStyles.AccentSound;
            private static readonly Color DimColor = new(0.30f, 0.30f, 0.35f);
            private static readonly Color CenterLine = new(0.25f, 0.25f, 0.30f);
            private static readonly Color HandleColor = new(1f, 0.9f, 0.3f, 0.9f);

            public WaveformView()
            {
                MouseFilter = MouseFilterEnum.Stop;
            }

            public override void _Draw()
            {
                var size = Size;
                float w = size.X;
                float h = size.Y;
                float midY = h / 2f;

                DrawRect(new Rect2(Vector2.Zero, size), BgColor);
                DrawLine(new Vector2(0, midY), new Vector2(w, midY), CenterLine);

                if (Samples == null || Samples.Length == 0 || w < 2) return;

                int pixelW = (int)w;

                for (int x = 0; x < pixelW; x++)
                {
                    int startIdx = x * Samples.Length / pixelW;
                    int endIdx = (x + 1) * Samples.Length / pixelW;
                    endIdx = Math.Min(endIdx, Samples.Length);
                    if (startIdx >= endIdx) continue;

                    short sMin = Samples[startIdx], sMax = Samples[startIdx];
                    for (int i = startIdx + 1; i < endIdx; i++)
                    {
                        if (Samples[i] < sMin) sMin = Samples[i];
                        if (Samples[i] > sMax) sMax = Samples[i];
                    }

                    float ratio = (float)x / w;
                    bool inTrim = ratio >= TrimStart && ratio <= TrimEnd;

                    float yTop = midY - (sMax / 32768f) * midY;
                    float yBot = midY - (sMin / 32768f) * midY;

                    if (yBot - yTop < 1) { yTop = midY - 0.5f; yBot = midY + 0.5f; }

                    DrawLine(new Vector2(x, yTop), new Vector2(x, yBot), inTrim ? WaveColor : DimColor);
                }

                float trimStartX = TrimStart * w;
                float trimEndX = TrimEnd * w;

                if (trimStartX > 0)
                    DrawRect(new Rect2(0, 0, trimStartX, h), new Color(0, 0, 0, 0.4f));
                if (trimEndX < w)
                    DrawRect(new Rect2(trimEndX, 0, w - trimEndX, h), new Color(0, 0, 0, 0.4f));

                DrawLine(new Vector2(trimStartX, 0), new Vector2(trimStartX, h), HandleColor, 2);
                DrawLine(new Vector2(trimEndX, 0), new Vector2(trimEndX, h), HandleColor, 2);

                DrawTriangle(new Vector2(trimStartX, 0), 6, true);
                DrawTriangle(new Vector2(trimEndX, 0), 6, false);
            }

            private void DrawTriangle(Vector2 top, float size, bool pointRight)
            {
                var pts = new Vector2[3];
                pts[0] = top;
                if (pointRight)
                {
                    pts[1] = new Vector2(top.X, top.Y + size);
                    pts[2] = new Vector2(top.X + size, top.Y + size / 2f);
                }
                else
                {
                    pts[1] = new Vector2(top.X, top.Y + size);
                    pts[2] = new Vector2(top.X - size, top.Y + size / 2f);
                }
                DrawPolygon(pts, new Color[] { HandleColor, HandleColor, HandleColor });
            }

            public override void _GuiInput(InputEvent @event)
            {
                if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        _dragging = true;
                        _dragStartX = mb.Position.X;
                        float pos = Mathf.Clamp(mb.Position.X / Size.X, 0, 1);
                        TrimStart = pos;
                        TrimEnd = pos;
                        QueueRedraw();
                    }
                    else
                    {
                        _dragging = false;
                        if (TrimEnd - TrimStart < 0.005f)
                        {
                            TrimStart = 0;
                            TrimEnd = 1;
                        }
                        OnTrimChanged?.Invoke(TrimStart, TrimEnd);
                        QueueRedraw();
                    }
                }
                else if (@event is InputEventMouseMotion mm && _dragging)
                {
                    float pos = Mathf.Clamp(mm.Position.X / Size.X, 0, 1);
                    float startRatio = _dragStartX / Size.X;
                    TrimStart = Mathf.Clamp(Mathf.Min(startRatio, pos), 0, 1);
                    TrimEnd = Mathf.Clamp(Mathf.Max(startRatio, pos), 0, 1);
                    QueueRedraw();
                }
            }
        }
    }
}
