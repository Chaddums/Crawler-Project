using Godot;
using System;
using System.IO;

namespace JunkbotArena
{
    /// <summary>
    /// Unified bug report tool. Ctrl+B from anywhere.
    /// Phase 1: Screenshot capture + optional snip annotation
    /// Phase 2: Description dialog with severity, scene context, live metrics
    /// Saves report.md + screenshot.png to test-reports/bugs/ or test-reports/features/
    /// </summary>
    public partial class BugReportDialog : CanvasLayer
    {
        private enum Phase { Snip, Describe }

        private Phase _phase;
        private bool _isFeatureRequest;
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
        private TextEdit _contextDisplay;
        private string _capturedContext;
        private string _capturedMetrics;

        // Colors
        private static readonly Color BugAccent = new(1f, 0.4f, 0.3f);
        private static readonly Color FeatureAccent = new(0.3f, 0.8f, 0.9f);
        private static readonly Color YellowSnip = new(1.0f, 0.9f, 0.0f);
        private static readonly Color BgDark = new(0.06f, 0.06f, 0.10f, 0.97f);
        private static readonly Color TextDim = new(0.6f, 0.6f, 0.65f);
        private static readonly Color TextGreen = new(0.5f, 0.8f, 0.5f);

        public static void Show(SceneTree tree, bool featureRequest = false)
        {
            var dialog = new BugReportDialog();
            dialog._isFeatureRequest = featureRequest;
            tree.Root.AddChild(dialog);
        }

        public override void _Ready()
        {
            Layer = 121; // Above editor (100) and QuickBugTool (120)
            ProcessMode = ProcessModeEnum.Always;

            _wasPaused = GetTree().Paused;
            GetTree().Paused = true;

            // Capture screenshot and context immediately
            _capturedImage = GetViewport().GetTexture().GetImage();
            _capturedContext = SceneContext.Capture(GetTree());
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
                    if (_phase == Phase.Describe)
                    {
                        Close();
                        GetViewport().SetInputAsHandled();
                    }
                    else
                    {
                        Close();
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (_phase == Phase.Snip && (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space))
                {
                    // Skip snip, go straight to describe
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

            // Show the captured screenshot
            _screenshotDisplay = new TextureRect();
            _screenshotDisplay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _screenshotDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _screenshotDisplay.StretchMode = TextureRect.StretchModeEnum.Scale;
            _screenshotDisplay.Texture = ImageTexture.CreateFromImage(_capturedImage);
            _screenshotDisplay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _snipOverlay.AddChild(_screenshotDisplay);

            // Dark tint
            var tint = new ColorRect();
            tint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            tint.Color = new Color(0, 0, 0, 0.35f);
            tint.MouseFilter = Control.MouseFilterEnum.Ignore;
            _snipOverlay.AddChild(tint);

            // Snip draw canvas
            _snipCanvas = new SnipCanvas();
            _snipCanvas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _snipCanvas.MouseFilter = Control.MouseFilterEnum.Ignore;
            _snipOverlay.AddChild(_snipCanvas);

            // Instruction label
            string typeHint = _isFeatureRequest ? "FEATURE" : "BUG";
            _snipInstruction = new Label();
            _snipInstruction.Text = $"[{typeHint}] Drag to highlight an area, or press ENTER to skip  (ESC to cancel)";
            _snipInstruction.HorizontalAlignment = HorizontalAlignment.Center;
            _snipInstruction.AddThemeFontSizeOverride("font_size", 16);
            _snipInstruction.AddThemeColorOverride("font_color", _isFeatureRequest ? FeatureAccent : BugAccent);
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

            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            {
                Close();
                GetViewport().SetInputAsHandled();
                return;
            }

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

                    if (rect.Size.X < 10 || rect.Size.Y < 10)
                        return;

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

            // Dim the snip overlay input but keep it visible as background
            _snipOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;

            BuildDescribeUI();
        }

        private void BuildDescribeUI()
        {
            Color accent = _isFeatureRequest ? FeatureAccent : BugAccent;

            _panel = new PanelContainer();
            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            _panel.GrowHorizontal = Control.GrowDirection.Both;
            _panel.GrowVertical = Control.GrowDirection.Both;
            _panel.OffsetLeft = -380;
            _panel.OffsetRight = 380;
            _panel.OffsetTop = -300;
            _panel.OffsetBottom = 300;
            _panel.MouseFilter = Control.MouseFilterEnum.Stop;

            var style = new StyleBoxFlat();
            style.BgColor = BgDark;
            style.BorderColor = accent;
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
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
            string titleText = _isFeatureRequest ? "FEATURE REQUEST" : "BUG REPORT";
            var title = new Label();
            title.Text = titleText + (_hasSnip ? "  (screenshot captured)" : "  (full screenshot)");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", accent);
            vbox.AddChild(title);

            var sep1 = new HSeparator();
            vbox.AddChild(sep1);

            // Description input
            var descLabel = new Label();
            descLabel.Text = _isFeatureRequest ? "Describe the feature:" : "What happened? (describe the issue)";
            descLabel.AddThemeFontSizeOverride("font_size", 13);
            descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            vbox.AddChild(descLabel);

            _descriptionEdit = new TextEdit();
            _descriptionEdit.PlaceholderText = _isFeatureRequest
                ? "Describe the feature you'd like..."
                : "What went wrong? What did you expect to happen?";
            _descriptionEdit.CustomMinimumSize = new Vector2(0, 80);
            _descriptionEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _descriptionEdit.AddThemeFontSizeOverride("font_size", 13);
            _descriptionEdit.WrapMode = TextEdit.LineWrappingMode.Boundary;
            vbox.AddChild(_descriptionEdit);

            if (!_isFeatureRequest)
            {
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
            }

            // Scene context (collapsed, read-only)
            var ctxLabel = new Label();
            ctxLabel.Text = "Scene Context + Metrics (auto-captured):";
            ctxLabel.AddThemeFontSizeOverride("font_size", 11);
            ctxLabel.AddThemeColorOverride("font_color", TextDim);
            vbox.AddChild(ctxLabel);

            _contextDisplay = new TextEdit();
            _contextDisplay.Text = _capturedMetrics + "\n" + _capturedContext;
            _contextDisplay.Editable = false;
            _contextDisplay.CustomMinimumSize = new Vector2(0, 140);
            _contextDisplay.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _contextDisplay.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _contextDisplay.AddThemeFontSizeOverride("font_size", 10);
            _contextDisplay.AddThemeColorOverride("font_color", TextGreen);
            vbox.AddChild(_contextDisplay);

            // Buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 8);

            var submitBtn = new Button();
            submitBtn.Text = _isFeatureRequest ? "Submit Feature" : "Submit Bug";
            submitBtn.CustomMinimumSize = new Vector2(120, 32);
            submitBtn.AddThemeFontSizeOverride("font_size", 14);
            submitBtn.AddThemeColorOverride("font_color", accent);
            submitBtn.Pressed += OnSubmit;
            btnRow.AddChild(submitBtn);

            var copyBtn = new Button();
            copyBtn.Text = "Copy to Clipboard";
            copyBtn.CustomMinimumSize = new Vector2(120, 32);
            copyBtn.AddThemeFontSizeOverride("font_size", 12);
            copyBtn.Pressed += OnCopyToClipboard;
            btnRow.AddChild(copyBtn);

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

            // Status
            _statusLabel = new Label();
            _statusLabel.Text = "";
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(_statusLabel);

            // Focus the description field
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

            string type = _isFeatureRequest ? "feature" : "bug";
            string severity = "";
            if (!_isFeatureRequest && _severityPicker != null)
            {
                string[] sevLabels = { "low", "medium", "high" };
                severity = sevLabels[_severityPicker.Selected];
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string folderName = $"{type}-{timestamp}";

            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
            string repoRoot = Path.GetDirectoryName(godotDir);
            string subDir = _isFeatureRequest ? "features" : "bugs";
            string reportDir = Path.Combine(repoRoot, "test-reports", subDir, folderName);

            try
            {
                Directory.CreateDirectory(reportDir);

                // Burn snip annotation onto screenshot if applicable
                if (_hasSnip)
                    BurnRectOnImage(_capturedImage, _snipRect, GetViewport().GetVisibleRect().Size);

                // Save screenshot
                string screenshotPath = Path.Combine(reportDir, "screenshot.png");
                _capturedImage.SavePng(screenshotPath);

                // Build and save report
                string report = BuildReport(type, severity, description);
                File.WriteAllText(Path.Combine(reportDir, "report.md"), report);

                _statusLabel.Text = $"Saved to test-reports/{subDir}/{folderName}/";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 0.4f));
                GD.Print($"[BugReport] Saved {type} report to {reportDir}");

                var timer = GetTree().CreateTimer(1.5);
                timer.Timeout += Close;
            }
            catch (Exception e)
            {
                _statusLabel.Text = $"Save failed: {e.Message}";
                _statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
                GD.PrintErr($"[BugReport] Save failed: {e}");
            }
        }

        private void OnCopyToClipboard()
        {
            string description = _descriptionEdit.Text.Trim();
            string type = _isFeatureRequest ? "Feature" : "Bug";
            string report = $"[{type}] {description}\n\n{_capturedMetrics}\n\n{_capturedContext}";
            DisplayServer.ClipboardSet(report);

            _statusLabel.Text = "Copied to clipboard!";
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 0.4f));
        }

        private string BuildReport(string type, string severity, string description)
        {
            string typeTitle = _isFeatureRequest ? "Feature Request" : "Bug Report";
            string sevSection = !_isFeatureRequest ? $"**Severity:** {severity}\n" : "";

            var gm = GameManager.Instance;
            int sector = gm?.CurrentSector ?? 0;
            int area = gm?.CurrentArea ?? 0;

            string snipSection = _hasSnip
                ? $"**Highlighted Region:** [{(int)_snipRect.Position.X}, {(int)_snipRect.Position.Y}, {(int)_snipRect.Size.X}, {(int)_snipRect.Size.Y}]\n"
                : "";

            return $@"# {typeTitle}: {(description.Length > 60 ? description[..57] + "..." : description)}
**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}
**Type:** {type}
{sevSection}{snipSection}
## Description
{description}

## Screenshot
![screenshot](screenshot.png)

## Live Metrics
{_capturedMetrics}

## Scene Context
```
{_capturedContext}
```

## How to Reproduce
1. Open the game
2. Navigate to Sector {sector}, Area {area}
3. [Fill in steps]

## Expected Behavior
[What should have happened]

## Actual Behavior
[What actually happened]
";
        }

        // ===== METRICS =====

        private string CaptureMetrics()
        {
            var gm = GameManager.Instance;
            var player = PlayerManager.P1;

            double fps = Engine.GetFramesPerSecond();
            string state = gm?.CurrentState.ToString() ?? "Unknown";
            int sector = gm?.CurrentSector ?? 0;
            int area = gm?.CurrentArea ?? 0;
            float hp = player?.Health?.CurrentHealth ?? 0;
            float maxHp = player?.Health?.MaxHealth ?? 0;
            int level = player?.Stats?.Level ?? 0;

            int enemyCount = 0;
            try { enemyCount = GetTree().GetNodesInGroup("Enemy").Count; } catch { }

            int kills = gm?.RunKills ?? 0;
            string godMode = DebugMenu.GodMode ? "ON" : "OFF";
            string instantKill = DebugMenu.InstantKill ? "ON" : "OFF";
            float dmgMult = DebugMenu.DamageMultiplier;
            int threat = MetaSaveManager.ThreatLevel;
            int ascension = MetaSaveManager.Data.AscensionRank;

            return $@"- FPS: {fps}
- State: {state}
- Sector: {sector}, Area: {area}
- Player HP: {hp:F0}/{maxHp:F0}, Level: {level}
- Enemies alive: {enemyCount}
- Run kills: {kills}
- GodMode: {godMode}, InstantKill: {instantKill}, DmgMult: {dmgMult:F1}x
- Threat Level: {threat}, Ascension: {ascension}";
        }

        // ===== HELPERS =====

        private void Close()
        {
            GetTree().Paused = _wasPaused;
            QueueFree();
        }

        private static Rect2 MakeRect(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.X, b.X);
            float y = Mathf.Min(a.Y, b.Y);
            float w = Mathf.Abs(a.X - b.X);
            float h = Mathf.Abs(a.Y - b.Y);
            return new Rect2(x, y, w, h);
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

            var color = YellowSnip;
            int thickness = 3;

            for (int t = 0; t < thickness; t++)
            {
                for (int x = Mathf.Max(0, x1 - t); x <= Mathf.Min(imgW - 1, x2 + t); x++)
                {
                    int yt = Mathf.Clamp(y1 - t, 0, imgH - 1);
                    int yb = Mathf.Clamp(y2 + t, 0, imgH - 1);
                    image.SetPixel(x, yt, color);
                    image.SetPixel(x, yb, color);
                }
                for (int y = Mathf.Max(0, y1 - t); y <= Mathf.Min(imgH - 1, y2 + t); y++)
                {
                    int xl = Mathf.Clamp(x1 - t, 0, imgW - 1);
                    int xr = Mathf.Clamp(x2 + t, 0, imgW - 1);
                    image.SetPixel(xl, y, color);
                    image.SetPixel(xr, y, color);
                }
            }
        }

        /// <summary>
        /// Inner control that draws the snip rectangle overlay.
        /// </summary>
        private partial class SnipCanvas : Control
        {
            public Rect2 DragRect;
            public bool Locked;

            public override void _Draw()
            {
                if (DragRect.Size.X < 2 && DragRect.Size.Y < 2)
                    return;

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
