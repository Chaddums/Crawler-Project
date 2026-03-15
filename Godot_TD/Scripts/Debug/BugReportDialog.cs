using Godot;
using System;
using System.IO;

namespace JunkyardTD
{
    /// <summary>
    /// Bug report tool. Ctrl+Shift+B from anywhere.
    /// Phase 1: Screenshot + optional snip highlight
    /// Phase 2: Description + severity + auto-captured metrics
    /// Saves report.md + screenshot.png to test-reports/bugs/
    /// </summary>
    public partial class BugReportDialog : CanvasLayer
    {
        private enum Phase { Snip, Describe }

        private Phase _phase;
        private bool _wasPaused;

        // Screenshot / snip
        private Image _capturedImage;
        private Control _snipOverlay;
        private TextureRect _screenshotDisplay;
        private SnipCanvas _snipCanvas;
        private Label _snipInstruction;
        private Vector2 _dragStart;
        private Vector2 _dragEnd;
        private bool _dragging;
        private Rect2 _snipRect;
        private bool _hasSnip;

        // Describe panel
        private PanelContainer _panel;
        private TextEdit _descriptionEdit;
        private OptionButton _severityPicker;
        private Label _statusLabel;
        private string _capturedMetrics;

        private static readonly Color BugAccent = new(1f, 0.4f, 0.3f);
        private static readonly Color BgDark = new(0.06f, 0.06f, 0.10f, 0.97f);
        private static readonly Color TextDim = new(0.6f, 0.6f, 0.65f);
        private static readonly Color TextGreen = new(0.5f, 0.8f, 0.5f);
        private static readonly Color YellowSnip = new(1.0f, 0.9f, 0.0f);

        public static void Show(SceneTree tree)
        {
            var dialog = new BugReportDialog();
            tree.Root.AddChild(dialog);
        }

        public override void _Ready()
        {
            Layer = 121;
            ProcessMode = ProcessModeEnum.Always;

            _wasPaused = GetTree().Paused;
            GetTree().Paused = true;

            _capturedImage = GetViewport().GetTexture().GetImage();
            _capturedMetrics = CaptureMetrics();

            BuildSnipUI();
            _phase = Phase.Snip;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    Close();
                    GetViewport().SetInputAsHandled();
                }
                else if (_phase == Phase.Snip &&
                    (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space))
                {
                    _hasSnip = false;
                    EnterDescribePhase();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        // ===== PHASE 1: SNIP =====

        private void BuildSnipUI()
        {
            _snipOverlay = new Control();
            _snipOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _snipOverlay.MouseFilter = Control.MouseFilterEnum.Stop;

            _screenshotDisplay = new TextureRect();
            _screenshotDisplay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _screenshotDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _screenshotDisplay.StretchMode = TextureRect.StretchModeEnum.Scale;
            _screenshotDisplay.Texture = ImageTexture.CreateFromImage(_capturedImage);
            _screenshotDisplay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _snipOverlay.AddChild(_screenshotDisplay);

            var tint = new ColorRect();
            tint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            tint.Color = new Color(0, 0, 0, 0.35f);
            tint.MouseFilter = Control.MouseFilterEnum.Ignore;
            _snipOverlay.AddChild(tint);

            _snipCanvas = new SnipCanvas();
            _snipCanvas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _snipCanvas.MouseFilter = Control.MouseFilterEnum.Ignore;
            _snipOverlay.AddChild(_snipCanvas);

            _snipInstruction = new Label();
            _snipInstruction.Text = "[BUG] Drag to highlight area, or press ENTER to skip  (ESC to cancel)";
            _snipInstruction.HorizontalAlignment = HorizontalAlignment.Center;
            _snipInstruction.AddThemeFontSizeOverride("font_size", 16);
            _snipInstruction.AddThemeColorOverride("font_color", BugAccent);
            _snipInstruction.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
            _snipInstruction.OffsetTop = 40;
            _snipInstruction.OffsetLeft = -400;
            _snipInstruction.OffsetRight = 400;
            _snipOverlay.AddChild(_snipInstruction);

            _snipOverlay.GuiInput += OnSnipInput;
            AddChild(_snipOverlay);
        }

        private void OnSnipInput(InputEvent @event)
        {
            if (_phase != Phase.Snip) return;

            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _dragging = true;
                    _dragStart = mb.Position;
                    _dragEnd = mb.Position;
                    _snipCanvas.DragRect = new Rect2(_dragStart, Vector2.Zero);
                    _snipCanvas.Locked = false;
                    _snipCanvas.QueueRedraw();
                }
                else if (_dragging)
                {
                    _dragging = false;
                    _dragEnd = mb.Position;
                    var rect = MakeRect(_dragStart, _dragEnd);

                    if (rect.Size.X < 10 || rect.Size.Y < 10) return;

                    _snipRect = rect;
                    _hasSnip = true;
                    _snipCanvas.DragRect = rect;
                    _snipCanvas.Locked = true;
                    _snipCanvas.QueueRedraw();
                    EnterDescribePhase();
                }
                GetViewport().SetInputAsHandled();
            }
            else if (@event is InputEventMouseMotion mm && _dragging)
            {
                _dragEnd = mm.Position;
                _snipCanvas.DragRect = MakeRect(_dragStart, _dragEnd);
                _snipCanvas.QueueRedraw();
                GetViewport().SetInputAsHandled();
            }
        }

        // ===== PHASE 2: DESCRIBE =====

        private void EnterDescribePhase()
        {
            _phase = Phase.Describe;
            _snipInstruction.Visible = false;
            _snipOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            BuildDescribeUI();
        }

        private void BuildDescribeUI()
        {
            _panel = new PanelContainer();
            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            _panel.GrowHorizontal = Control.GrowDirection.Both;
            _panel.GrowVertical = Control.GrowDirection.Both;
            _panel.OffsetLeft = -350;
            _panel.OffsetRight = 350;
            _panel.OffsetTop = -250;
            _panel.OffsetBottom = 250;
            _panel.MouseFilter = Control.MouseFilterEnum.Stop;

            var style = new StyleBoxFlat();
            style.BgColor = BgDark;
            style.BorderColor = BugAccent;
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(6);
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 12;
            style.ContentMarginBottom = 12;
            _panel.AddThemeStyleboxOverride("panel", style);
            AddChild(_panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "BUG REPORT" + (_hasSnip ? "  (area highlighted)" : "  (full screenshot)");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", BugAccent);
            vbox.AddChild(title);
            vbox.AddChild(new HSeparator());

            // Description
            var descLabel = new Label();
            descLabel.Text = "What happened?";
            descLabel.AddThemeFontSizeOverride("font_size", 13);
            descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            vbox.AddChild(descLabel);

            _descriptionEdit = new TextEdit();
            _descriptionEdit.PlaceholderText = "Describe the bug...";
            _descriptionEdit.CustomMinimumSize = new Vector2(0, 80);
            _descriptionEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _descriptionEdit.AddThemeFontSizeOverride("font_size", 13);
            _descriptionEdit.WrapMode = TextEdit.LineWrappingMode.Boundary;
            vbox.AddChild(_descriptionEdit);

            // Severity
            var sevRow = new HBoxContainer();
            sevRow.AddThemeConstantOverride("separation", 8);
            var sevLabel = new Label();
            sevLabel.Text = "Severity:";
            sevLabel.AddThemeFontSizeOverride("font_size", 12);
            sevLabel.AddThemeColorOverride("font_color", TextDim);
            sevRow.AddChild(sevLabel);

            _severityPicker = new OptionButton();
            _severityPicker.AddThemeFontSizeOverride("font_size", 12);
            _severityPicker.AddItem("Low - Visual/cosmetic");
            _severityPicker.AddItem("Medium - Gameplay issue");
            _severityPicker.AddItem("High - Crash/blocker");
            _severityPicker.Selected = 1;
            sevRow.AddChild(_severityPicker);
            vbox.AddChild(sevRow);

            // Metrics (auto-captured, read-only)
            var ctxLabel = new Label();
            ctxLabel.Text = "Auto-captured metrics:";
            ctxLabel.AddThemeFontSizeOverride("font_size", 11);
            ctxLabel.AddThemeColorOverride("font_color", TextDim);
            vbox.AddChild(ctxLabel);

            var contextDisplay = new TextEdit();
            contextDisplay.Text = _capturedMetrics;
            contextDisplay.Editable = false;
            contextDisplay.CustomMinimumSize = new Vector2(0, 100);
            contextDisplay.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            contextDisplay.AddThemeFontSizeOverride("font_size", 10);
            contextDisplay.AddThemeColorOverride("font_color", TextGreen);
            vbox.AddChild(contextDisplay);

            // Buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 8);

            var submitBtn = new Button();
            submitBtn.Text = "Submit Bug";
            submitBtn.CustomMinimumSize = new Vector2(120, 32);
            submitBtn.AddThemeFontSizeOverride("font_size", 14);
            submitBtn.AddThemeColorOverride("font_color", BugAccent);
            submitBtn.Pressed += OnSubmit;
            btnRow.AddChild(submitBtn);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btnRow.AddChild(spacer);

            var cancelBtn = new Button();
            cancelBtn.Text = "Cancel (Esc)";
            cancelBtn.CustomMinimumSize = new Vector2(100, 32);
            cancelBtn.AddThemeFontSizeOverride("font_size", 12);
            cancelBtn.AddThemeColorOverride("font_color", TextDim);
            cancelBtn.Pressed += Close;
            btnRow.AddChild(cancelBtn);

            vbox.AddChild(btnRow);

            _statusLabel = new Label();
            _statusLabel.Text = "";
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(_statusLabel);

            _descriptionEdit.CallDeferred("grab_focus");
        }

        // ===== SUBMIT =====

        private void OnSubmit()
        {
            string description = _descriptionEdit.Text.Trim();
            if (string.IsNullOrEmpty(description))
            {
                _statusLabel.Text = "Please enter a description!";
                _statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
                return;
            }

            string[] sevLabels = { "low", "medium", "high" };
            string severity = sevLabels[_severityPicker.Selected];
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string folderName = $"bug-{timestamp}";

            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
            string reportDir = Path.Combine(godotDir, "bugs", folderName);

            try
            {
                Directory.CreateDirectory(reportDir);

                if (_hasSnip)
                    BurnRectOnImage(_capturedImage, _snipRect, GetViewport().GetVisibleRect().Size);

                _capturedImage.SavePng(Path.Combine(reportDir, "screenshot.png"));

                string report = $@"# Bug Report: {(description.Length > 60 ? description[..57] + "..." : description)}
**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}
**Severity:** {severity}
{(_hasSnip ? $"**Highlighted Region:** [{(int)_snipRect.Position.X}, {(int)_snipRect.Position.Y}, {(int)_snipRect.Size.X}, {(int)_snipRect.Size.Y}]\n" : "")}
## Description
{description}

## Screenshot
![screenshot](screenshot.png)

## Metrics
{_capturedMetrics}
";
                File.WriteAllText(Path.Combine(reportDir, "report.md"), report);

                _statusLabel.Text = $"Saved to bugs/{folderName}/";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 0.4f));
                GD.Print($"[BugReport] Saved to {reportDir}");

                GetTree().CreateTimer(1.5).Timeout += Close;
            }
            catch (Exception e)
            {
                _statusLabel.Text = $"Save failed: {e.Message}";
                _statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
                GD.PrintErr($"[BugReport] Save failed: {e}");
            }
        }

        // ===== METRICS =====

        private string CaptureMetrics()
        {
            var gm = GameManager.Instance;
            double fps = Engine.GetFramesPerSecond();
            string phase = gm?.CurrentPhase.ToString() ?? "Unknown";
            int wave = gm?.CurrentWave ?? 0;
            int gold = gm?.CurrentScrap ?? 0;
            int lives = gm?.CoreLives ?? 0;

            int enemyCount = 0;
            int nodeCount = 0;
            try
            {
                enemyCount = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY).Count;
                nodeCount = GetTree().GetNodesInGroup(Constants.GROUP_VINE_NODE).Count;
            }
            catch { }

            return $@"- FPS: {fps}
- Phase: {phase}
- Wave: {wave} / {VineWaveRegistry.WaveCount}
- Gold: {gold}
- Lives: {lives}
- Enemies alive: {enemyCount}
- Nodes placed: {nodeCount}
- Mode: Vine Logic TD";
        }

        // ===== HELPERS =====

        private void Close()
        {
            GetTree().Paused = _wasPaused;
            QueueFree();
        }

        private static Rect2 MakeRect(Vector2 a, Vector2 b)
        {
            return new Rect2(
                Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y),
                Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        }

        private static void BurnRectOnImage(Image image, Rect2 screenRect, Vector2 screenSize)
        {
            int imgW = image.GetWidth();
            int imgH = image.GetHeight();
            float scaleX = imgW / screenSize.X;
            float scaleY = imgH / screenSize.Y;

            int x1 = Mathf.Clamp((int)(screenRect.Position.X * scaleX), 0, imgW - 1);
            int y1 = Mathf.Clamp((int)(screenRect.Position.Y * scaleY), 0, imgH - 1);
            int x2 = Mathf.Clamp((int)(screenRect.End.X * scaleX), 0, imgW - 1);
            int y2 = Mathf.Clamp((int)(screenRect.End.Y * scaleY), 0, imgH - 1);

            int thickness = 3;
            for (int t = 0; t < thickness; t++)
            {
                for (int x = Mathf.Max(0, x1 - t); x <= Mathf.Min(imgW - 1, x2 + t); x++)
                {
                    image.SetPixel(x, Mathf.Clamp(y1 - t, 0, imgH - 1), YellowSnip);
                    image.SetPixel(x, Mathf.Clamp(y2 + t, 0, imgH - 1), YellowSnip);
                }
                for (int y = Mathf.Max(0, y1 - t); y <= Mathf.Min(imgH - 1, y2 + t); y++)
                {
                    image.SetPixel(Mathf.Clamp(x1 - t, 0, imgW - 1), y, YellowSnip);
                    image.SetPixel(Mathf.Clamp(x2 + t, 0, imgW - 1), y, YellowSnip);
                }
            }
        }

        private partial class SnipCanvas : Control
        {
            public Rect2 DragRect;
            public bool Locked;

            public override void _Draw()
            {
                if (DragRect.Size.X < 2 && DragRect.Size.Y < 2) return;

                if (Locked)
                {
                    DrawRect(DragRect, new Color(1.0f, 0.9f, 0.0f, 0.15f), true);
                    DrawRect(DragRect, new Color(1.0f, 0.9f, 0.0f, 1.0f), false, 2.0f);
                }
                else
                {
                    DrawRect(DragRect, new Color(1.0f, 0.9f, 0.0f, 0.8f), false, 2.0f);
                }
            }
        }
    }
}
