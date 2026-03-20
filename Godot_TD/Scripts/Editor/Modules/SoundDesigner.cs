using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace JunkyardTD
{
    /// <summary>
    /// Sound Designer — browse, audition, and assign audio files to game sound slots.
    /// Left: audio slots from audio.json grouped by category with play/path/volume/bus.
    /// Right top: audio file browser (UI pack + Sonniss library).
    /// Right bottom: floor ambience controls.
    /// Saves assignments to audio.json.
    /// </summary>
    public partial class SoundDesigner : EditorModule
    {
        public override string ModuleName => "Sound";
        public override Color AccentColor => EditorStyles.AccentSound;

        private const string AUDIO_JSON = "res://Data/audio.json";

        private static readonly string[] AudioCategories = { "sfx", "abilities", "ambient", "ui" };
        private static readonly string[] BusNames = { "SFX", "Music", "Voice" };

        // Audio file library folders to browse
        private static readonly string[] LibraryFolders =
        {
            "res://Assets/Audio/Edited",
            "res://Assets/Audio/UI",
            "res://Assets/Audio/Sonniss/BigMechanical",
            "res://Assets/Audio/Sonniss/FuturisticWeapons",
            "res://Assets/Audio/Sonniss/HeavyMechanical",
            "res://Assets/Audio/Sonniss/Mechanical",
            "res://Assets/Audio/Sonniss/Mechanics2",
            "res://Assets/Audio/Sonniss/SciFiBlasters",
            "res://Assets/Audio/Sonniss/SciFiWeapons",
            "res://Assets/Audio/Sonniss/SciFiWeapons2",
            "res://Assets/Audio/Sonniss/SciFiWeapons3",
            "res://Assets/Audio/Sonniss/SteampunkMachines"
        };

        // Data
        private Dictionary<string, object> _audioRoot;
        private string _activeCategory = "sfx";
        private string _selectedSlot;

        // UI — left panel (slots)
        private Label _nowPlaying;
        private Label _statusLabel;
        private VBoxContainer _slotList;
        private readonly List<Button> _catButtons = new();

        // UI — right panel (browser)
        private OptionButton _folderPicker;
        private VBoxContainer _fileList;
        private ScrollContainer _fileScroll;
        private VBoxContainer _browserPanel;

        // UI — clip editor
        private AudioClipEditor _clipEditor;

        // UI — ambience
        private HSlider _floorSlider;
        private Label _floorLabel;

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            BuildUI();
        }

        public override void OnActivated()
        {
            LoadData();
        }

        private void BuildUI()
        {
            _nowPlaying = EditorStyles.MakeLabel("Click a sound to preview", EditorStyles.FontBody, EditorStyles.TextSecondary);
            AddChild(_nowPlaying);
            AddChild(EditorStyles.MakeSeparator());

            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 12);

            // ── LEFT: Audio Slots ──
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(420, 0);

            // Category tabs
            var catRow = new HBoxContainer();
            catRow.AddThemeConstantOverride("separation", 4);
            foreach (var cat in AudioCategories)
            {
                var btn = EditorStyles.MakeButton(cat, EditorStyles.FontSmall,
                    cat == _activeCategory ? AccentColor : EditorStyles.TextSecondary);
                btn.CustomMinimumSize = new Vector2(55, 24);
                var captured = cat;
                btn.Pressed += () => SwitchCategory(captured);
                catRow.AddChild(btn);
                _catButtons.Add(btn);
            }
            leftPanel.AddChild(catRow);

            // Slot list
            var slotScroll = new ScrollContainer();
            slotScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            slotScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _slotList = new VBoxContainer();
            _slotList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _slotList.AddThemeConstantOverride("separation", 2);
            slotScroll.AddChild(_slotList);
            leftPanel.AddChild(slotScroll);

            // Save button
            var saveRow = new HBoxContainer();
            saveRow.AddThemeConstantOverride("separation", 4);
            var saveBtn = EditorStyles.MakeButton("Save audio.json", EditorStyles.FontSmall, EditorStyles.StatusSaved);
            saveBtn.CustomMinimumSize = new Vector2(0, 28);
            saveBtn.Pressed += SaveData;
            saveRow.AddChild(saveBtn);
            _statusLabel = EditorStyles.MakeLabel("", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            saveRow.AddChild(_statusLabel);
            leftPanel.AddChild(saveRow);

            split.AddChild(leftPanel);

            // ── RIGHT: Browser + Ambience + Clip Editor ──
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(350, 0);

            // Browser panel
            _browserPanel = new VBoxContainer();
            _browserPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _browserPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            _browserPanel.AddThemeConstantOverride("separation", 4);

            _browserPanel.AddChild(EditorStyles.MakeLabel("Audio Library", EditorStyles.FontHeader, AccentColor));

            _folderPicker = new OptionButton();
            _folderPicker.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            foreach (var folder in LibraryFolders)
            {
                string label = folder.GetFile();
                if (label == "UI") label = "Boom UI Pack";
                else if (label == "Edited") label = "My Edited Clips";
                _folderPicker.AddItem(label);
            }
            _folderPicker.ItemSelected += _ => PopulateFileList();
            _browserPanel.AddChild(_folderPicker);

            _fileScroll = new ScrollContainer();
            _fileScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _fileScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _fileList = new VBoxContainer();
            _fileList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _fileList.AddThemeConstantOverride("separation", 1);
            _fileScroll.AddChild(_fileList);
            _browserPanel.AddChild(_fileScroll);

            _browserPanel.AddChild(EditorStyles.MakeSeparator());

            // Floor ambience section
            _browserPanel.AddChild(EditorStyles.MakeLabel("Floor Ambience", EditorStyles.FontHeader, AccentColor));

            var floorRow = new HBoxContainer();
            floorRow.AddThemeConstantOverride("separation", 8);
            floorRow.AddChild(EditorStyles.MakeLabel("Floor:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _floorLabel = EditorStyles.MakeLabel("1", EditorStyles.FontSmall, AccentColor);
            _floorLabel.CustomMinimumSize = new Vector2(20, 0);
            _floorSlider = new HSlider { MinValue = 1, MaxValue = 6, Step = 1, Value = 1 };
            _floorSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _floorSlider.ValueChanged += v => _floorLabel.Text = ((int)v).ToString();
            floorRow.AddChild(_floorSlider);
            floorRow.AddChild(_floorLabel);
            _browserPanel.AddChild(floorRow);

            var ambBtnRow = new HBoxContainer();
            ambBtnRow.AddThemeConstantOverride("separation", 4);
            var playAmb = EditorStyles.MakeButton("Play Ambience", EditorStyles.FontSmall, AccentColor);
            playAmb.CustomMinimumSize = new Vector2(0, 28);
            playAmb.Pressed += PlayAmbience;
            ambBtnRow.AddChild(playAmb);
            var stopAmb = EditorStyles.MakeButton("Stop", EditorStyles.FontSmall, EditorStyles.StatusError);
            stopAmb.CustomMinimumSize = new Vector2(0, 28);
            stopAmb.Pressed += () => { GetAudioManager()?.StopMusic(); _nowPlaying.Text = "Stopped"; };
            ambBtnRow.AddChild(stopAmb);
            _browserPanel.AddChild(ambBtnRow);

            rightPanel.AddChild(_browserPanel);

            // Clip Editor (hidden by default)
            _clipEditor = new AudioClipEditor();
            _clipEditor.Visible = false;
            _clipEditor.OnClosed += () =>
            {
                _clipEditor.Visible = false;
                _browserPanel.Visible = true;
            };
            _clipEditor.OnSaved += savedPath =>
            {
                if (!string.IsNullOrEmpty(_selectedSlot))
                {
                    SetSlotValue(_selectedSlot, "path", savedPath);
                    _nowPlaying.Text = $"Saved & assigned {savedPath.GetFile()} -> {_activeCategory}/{_selectedSlot}";
                    _nowPlaying.AddThemeColorOverride("font_color", EditorStyles.StatusSaved);
                    PopulateSlotList();
                }
            };
            rightPanel.AddChild(_clipEditor);

            split.AddChild(rightPanel);
            AddChild(split);
        }

        // ── Category / Slot List ──

        private void SwitchCategory(string cat)
        {
            _activeCategory = cat;
            _selectedSlot = null;

            for (int i = 0; i < _catButtons.Count && i < AudioCategories.Length; i++)
                _catButtons[i].AddThemeColorOverride("font_color",
                    AudioCategories[i] == cat ? AccentColor : EditorStyles.TextSecondary);

            PopulateSlotList();
        }

        private void PopulateSlotList()
        {
            if (_slotList == null) return;

            foreach (var child in _slotList.GetChildren())
                if (child is Node n) n.QueueFree();

            if (_audioRoot == null) return;
            if (!_audioRoot.TryGetValue(_activeCategory, out var catObj)) return;
            if (catObj is not Dictionary<string, object> entries) return;

            foreach (var kvp in entries)
            {
                string slotName = kvp.Key;
                string path = "";
                float volumeDb = 0;
                string bus = "SFX";

                if (kvp.Value is Dictionary<string, object> entry)
                {
                    if (entry.TryGetValue("path", out var p)) path = p?.ToString() ?? "";
                    if (entry.TryGetValue("volume_db", out var v) && v is double d) volumeDb = (float)d;
                    if (entry.TryGetValue("bus", out var b)) bus = b?.ToString() ?? "SFX";
                }

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);

                // Play button
                var playBtn = EditorStyles.MakeButton("Play", EditorStyles.FontTiny, AccentColor);
                playBtn.CustomMinimumSize = new Vector2(38, 22);
                var capturedSlot = slotName;
                playBtn.Pressed += () => PlaySlot(capturedSlot);
                row.AddChild(playBtn);

                // Name (clickable to select)
                var nameBtn = EditorStyles.MakeButton(slotName, EditorStyles.FontSmall);
                nameBtn.Alignment = HorizontalAlignment.Left;
                nameBtn.CustomMinimumSize = new Vector2(110, 22);
                nameBtn.Pressed += () => SelectSlot(capturedSlot);
                row.AddChild(nameBtn);

                // Edit button (for assigned WAV files)
                if (!string.IsNullOrEmpty(path) && path.ToLower().EndsWith(".wav"))
                {
                    var editSlotBtn = EditorStyles.MakeButton("Edit", EditorStyles.FontTiny, EditorStyles.TextAccent);
                    editSlotBtn.CustomMinimumSize = new Vector2(36, 22);
                    var capturedPath = path;
                    editSlotBtn.Pressed += () => OpenClipEditor(capturedPath);
                    row.AddChild(editSlotBtn);
                }

                // Path label (truncated)
                string displayPath = path.Length > 30 ? "..." + path.Substring(path.Length - 27) : path;
                var pathLabel = EditorStyles.MakeLabel(displayPath, EditorStyles.FontTiny, EditorStyles.TextMuted);
                pathLabel.TooltipText = path;
                pathLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                pathLabel.ClipText = true;
                row.AddChild(pathLabel);

                // Volume slider
                var volSlider = new HSlider { MinValue = -20, MaxValue = 6, Step = 1, Value = volumeDb };
                volSlider.CustomMinimumSize = new Vector2(60, 0);
                var capturedSlot2 = slotName;
                var volLabel = EditorStyles.MakeLabel($"{volumeDb:0}dB", EditorStyles.FontTiny, EditorStyles.TextMuted);
                volLabel.CustomMinimumSize = new Vector2(32, 0);
                volSlider.ValueChanged += val =>
                {
                    volLabel.Text = $"{val:0}dB";
                    SetSlotValue(capturedSlot2, "volume_db", val);
                };
                row.AddChild(volSlider);
                row.AddChild(volLabel);

                // Bus button (cycles SFX→Music→Voice)
                var busBtn = EditorStyles.MakeButton(bus, EditorStyles.FontTiny, EditorStyles.TextSecondary);
                busBtn.CustomMinimumSize = new Vector2(45, 22);
                var capturedSlot3 = slotName;
                busBtn.Pressed += () =>
                {
                    string curBus = GetSlotValue(capturedSlot3, "bus")?.ToString() ?? "SFX";
                    int idx = Array.IndexOf(BusNames, curBus);
                    string nextBus = BusNames[(idx + 1) % BusNames.Length];
                    SetSlotValue(capturedSlot3, "bus", nextBus);
                    busBtn.Text = nextBus;
                };
                row.AddChild(busBtn);

                _slotList.AddChild(row);
            }
        }

        private void SelectSlot(string slotName)
        {
            _selectedSlot = slotName;
            _nowPlaying.Text = $"Selected: {_activeCategory}/{slotName} — pick a file from the browser to assign";
            _nowPlaying.AddThemeColorOverride("font_color", AccentColor);
        }

        private void PlaySlot(string slotName)
        {
            _nowPlaying.Text = $"Playing: {_activeCategory}/{slotName}";
            _nowPlaying.AddThemeColorOverride("font_color", AccentColor);

            string path = GetSlotValue(slotName, "path")?.ToString();
            if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path))
            {
                var stream = GD.Load<AudioStream>(path);
                if (stream != null)
                {
                    float vol = 0;
                    var volObj = GetSlotValue(slotName, "volume_db");
                    if (volObj is double d) vol = (float)d;
                    GetAudioManager()?.PlaySFX(stream, vol);
                    return;
                }
            }

            GetAudioManager()?.PlaySFXByName(slotName);
        }

        // ── File Browser ──

        private void PopulateFileList()
        {
            if (_fileList == null) return;

            foreach (var child in _fileList.GetChildren())
                if (child is Node n) n.QueueFree();

            int folderIdx = _folderPicker?.Selected ?? 0;
            if (folderIdx < 0 || folderIdx >= LibraryFolders.Length) return;

            string folder = LibraryFolders[folderIdx];
            using var dir = DirAccess.Open(folder);
            if (dir == null)
            {
                _fileList.AddChild(EditorStyles.MakeLabel($"Cannot open {folder}", EditorStyles.FontSmall, EditorStyles.StatusError));
                return;
            }

            var files = new List<string>();
            dir.ListDirBegin();
            string file;
            while ((file = dir.GetNext()) != "")
            {
                string lower = file.ToLower();
                if (lower.EndsWith(".wav") || lower.EndsWith(".ogg") || lower.EndsWith(".mp3"))
                    files.Add(file);
            }
            dir.ListDirEnd();
            files.Sort();

            foreach (var f in files)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);

                string fullPath = $"{folder}/{f}";

                var playBtn = EditorStyles.MakeButton("Play", EditorStyles.FontTiny, AccentColor);
                playBtn.CustomMinimumSize = new Vector2(38, 20);
                var capturedPath = fullPath;
                playBtn.Pressed += () => PlayFile(capturedPath);
                row.AddChild(playBtn);

                var assignBtn = EditorStyles.MakeButton("Assign", EditorStyles.FontTiny, EditorStyles.StatusSaved);
                assignBtn.CustomMinimumSize = new Vector2(48, 20);
                assignBtn.Pressed += () => AssignFile(capturedPath);
                row.AddChild(assignBtn);

                if (capturedPath.ToLower().EndsWith(".wav"))
                {
                    var editBtn = EditorStyles.MakeButton("Edit", EditorStyles.FontTiny, EditorStyles.TextAccent);
                    editBtn.CustomMinimumSize = new Vector2(36, 20);
                    editBtn.Pressed += () => OpenClipEditor(capturedPath);
                    row.AddChild(editBtn);
                }

                var label = EditorStyles.MakeLabel(f, EditorStyles.FontTiny);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                label.ClipText = true;
                label.TooltipText = fullPath;
                row.AddChild(label);

                _fileList.AddChild(row);
            }
        }

        private void PlayFile(string path)
        {
            _nowPlaying.Text = $"Preview: {path.GetFile()}";
            _nowPlaying.AddThemeColorOverride("font_color", AccentColor);

            if (ResourceLoader.Exists(path))
            {
                var stream = GD.Load<AudioStream>(path);
                GetAudioManager()?.PlaySFX(stream);
            }
        }

        private void AssignFile(string path)
        {
            if (string.IsNullOrEmpty(_selectedSlot))
            {
                _nowPlaying.Text = "Select a slot first (click a name in the left panel)";
                _nowPlaying.AddThemeColorOverride("font_color", EditorStyles.StatusError);
                return;
            }

            SetSlotValue(_selectedSlot, "path", path);

            _nowPlaying.Text = $"Assigned {path.GetFile()} -> {_activeCategory}/{_selectedSlot}";
            _nowPlaying.AddThemeColorOverride("font_color", EditorStyles.StatusSaved);

            PopulateSlotList();
        }

        // ── Clip Editor ──

        private void OpenClipEditor(string path)
        {
            if (_clipEditor == null) return;

            _browserPanel.Visible = false;
            _clipEditor.Visible = true;
            _clipEditor.LoadFile(path);

            _nowPlaying.Text = $"Editing: {path.GetFile()}";
            _nowPlaying.AddThemeColorOverride("font_color", EditorStyles.TextAccent);
        }

        // ── Data Helpers ──

        private object GetSlotValue(string slotName, string key)
        {
            if (_audioRoot == null) return null;
            if (!_audioRoot.TryGetValue(_activeCategory, out var catObj)) return null;
            if (catObj is not Dictionary<string, object> cat) return null;
            if (!cat.TryGetValue(slotName, out var slotObj)) return null;
            if (slotObj is not Dictionary<string, object> slot) return null;
            slot.TryGetValue(key, out var val);
            return val;
        }

        private void SetSlotValue(string slotName, string key, object value)
        {
            if (_audioRoot == null) return;
            if (!_audioRoot.TryGetValue(_activeCategory, out var catObj)) return;
            if (catObj is not Dictionary<string, object> cat) return;
            if (!cat.TryGetValue(slotName, out var slotObj)) return;
            if (slotObj is not Dictionary<string, object> slot) return;

            slot[key] = value;
        }

        // ── Ambience ──

        private static AudioManager GetAudioManager()
        {
            return ServiceLocator.TryGet<AudioManager>(out var am) ? am : null;
        }

        private void PlayAmbience()
        {
            int floor = (int)_floorSlider.Value;
            _nowPlaying.Text = $"Ambience: Floor {floor}";
            _nowPlaying.AddThemeColorOverride("font_color", AccentColor);
            GetAudioManager()?.PlayBattleAmbience(floor);
        }

        // ── Load / Save ──

        private void LoadData()
        {
            _audioRoot = LoadJsonFile(AUDIO_JSON);
            if (_audioRoot == null)
                _audioRoot = new Dictionary<string, object>();

            PopulateSlotList();
            PopulateFileList();

            int totalSlots = 0;
            foreach (var cat in AudioCategories)
            {
                if (_audioRoot.TryGetValue(cat, out var obj) && obj is Dictionary<string, object> entries)
                    totalSlots += entries.Count;
            }
            _statusLabel.Text = $"Loaded {totalSlots} audio slots";
            _statusLabel.AddThemeColorOverride("font_color", EditorStyles.StatusSaved);
        }

        private void SaveData()
        {
            if (_audioRoot == null) return;

            if (SaveJsonFile(AUDIO_JSON, _audioRoot))
            {
                AudioLoader.Reload();
                _statusLabel.Text = "Saved audio.json (hot-reloaded)";
                _statusLabel.AddThemeColorOverride("font_color", EditorStyles.StatusSaved);
                GD.Print("[SoundDesigner] Saved audio.json and reloaded AudioLoader");
            }
            else
            {
                _statusLabel.Text = "Save failed!";
                _statusLabel.AddThemeColorOverride("font_color", EditorStyles.StatusError);
            }
        }

        private static Dictionary<string, object> LoadJsonFile(string resPath)
        {
            if (!FileAccess.FileExists(resPath)) return null;

            using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
            if (file == null) return null;

            string json = file.GetAsText();
            try
            {
                using var doc = JsonDocument.Parse(json);
                return ElementToDict(doc.RootElement);
            }
            catch (JsonException ex)
            {
                GD.PushError($"[SoundDesigner] JSON parse error: {ex.Message}");
                return null;
            }
        }

        private static bool SaveJsonFile(string resPath, Dictionary<string, object> data)
        {
            using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"[SoundDesigner] Cannot write: {resPath}");
                return false;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(ConvertToSerializable(data), options);
            file.StoreString(json + "\n");
            return true;
        }

        /// <summary>
        /// Convert Dictionary&lt;string, object&gt; tree to a structure JsonSerializer can handle.
        /// </summary>
        private static object ConvertToSerializable(object obj)
        {
            if (obj is Dictionary<string, object> dict)
            {
                var result = new Dictionary<string, object>();
                foreach (var kvp in dict)
                    result[kvp.Key] = ConvertToSerializable(kvp.Value);
                return result;
            }
            return obj;
        }

        private static Dictionary<string, object> ElementToDict(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;

            var dict = new Dictionary<string, object>();
            foreach (var prop in element.EnumerateObject())
                dict[prop.Name] = ElementToObject(prop.Value);
            return dict;
        }

        private static object ElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ElementToDict(element),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
    }
}
