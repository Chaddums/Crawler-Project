using Godot;
using System;
using System.IO;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Quick screenshot snip + annotate + bug report overlay.
    /// Ctrl+B from any editor tab: snip a region, add a description, submit.
    /// CanvasLayer 120 draws above the editor (layer 100).
    /// </summary>
    public partial class QuickBugTool : CanvasLayer
    {
        private enum Phase { Inactive, Snip, Describe }

        private Phase _phase = Phase.Inactive;

        // Snip overlay
        private Control _overlay;
        private TextureRect _screenshotDisplay;
        private SnipCanvas _snipCanvas;
        private Label _instructionLabel;

        // Describe panel
        private PanelContainer _describePanel;
        private LineEdit _descriptionEdit;
        private int _severity = 1; // 0=Low, 1=Medium, 2=High
        private Button[] _severityButtons;

        // Snip rectangle
        private Vector2 _dragStart;
        private Vector2 _dragEnd;
        private bool _dragging;

        // Captured image
        private Image _capturedImage;

        // Saved flash
        private Label _flashLabel;
        private float _flashTimer;

        public override void _Ready()
        {
            Layer = 120;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
            BuildUI();
        }

        /// <summary>Activate the snip tool — captures current viewport and enters snip mode.</summary>
        public void Activate()
        {
            if (_phase != Phase.Inactive) return;

            _capturedImage = GetViewport().GetTexture().GetImage();
            var tex = ImageTexture.CreateFromImage(_capturedImage);
            _screenshotDisplay.Texture = tex;

            _dragging = false;
            _snipCanvas.DragRect = new Rect2();
            _snipCanvas.Locked = false;
            _snipCanvas.QueueRedraw();

            _describePanel.Visible = false;
            _instructionLabel.Text = "Click and drag to highlight area (ESC to cancel)";
            _instructionLabel.Visible = true;

            Visible = true;
            _overlay.Visible = true;
            _phase = Phase.Snip;
        }

        public override void _Process(double delta)
        {
            if (_flashTimer > 0)
            {
                _flashTimer -= (float)delta;
                if (_flashTimer <= 0)
                    _flashLabel.Visible = false;
            }
        }

        private void BuildUI()
        {
            _overlay = new Control();
            _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _overlay.MouseFilter = Control.MouseFilterEnum.Stop;

            // Full-screen screenshot display
            _screenshotDisplay = new TextureRect();
            _screenshotDisplay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _screenshotDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _screenshotDisplay.StretchMode = TextureRect.StretchModeEnum.Scale;
            _screenshotDisplay.MouseFilter = Control.MouseFilterEnum.Ignore;
            _overlay.AddChild(_screenshotDisplay);

            // Dark tint overlay
            var tint = new ColorRect();
            tint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            tint.Color = new Color(0, 0, 0, 0.35f);
            tint.MouseFilter = Control.MouseFilterEnum.Ignore;
            _overlay.AddChild(tint);

            // Snip canvas for drawing the rectangle
            _snipCanvas = new SnipCanvas();
            _snipCanvas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _snipCanvas.MouseFilter = Control.MouseFilterEnum.Ignore;
            _overlay.AddChild(_snipCanvas);

            // Instruction label at top center
            _instructionLabel = EditorStyles.MakeLabel(
                "Click and drag to highlight area (ESC to cancel)",
                EditorStyles.FontHeader, EditorStyles.TextPrimary);
            _instructionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _instructionLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
            _instructionLabel.OffsetTop = 40;
            _instructionLabel.OffsetLeft = -300;
            _instructionLabel.OffsetRight = 300;
            _overlay.AddChild(_instructionLabel);

            // Describe panel (hidden until snip is complete)
            BuildDescribePanel();
            _overlay.AddChild(_describePanel);

            // Flash "Saved!" label
            _flashLabel = EditorStyles.MakeLabel("Bug saved!", EditorStyles.FontHeader, EditorStyles.StatusSaved);
            _flashLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _flashLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
            _flashLabel.OffsetTop = 40;
            _flashLabel.OffsetLeft = -100;
            _flashLabel.OffsetRight = 100;
            _flashLabel.Visible = false;
            _overlay.AddChild(_flashLabel);

            _overlay.GuiInput += OnOverlayInput;
            _overlay.Visible = false;
            AddChild(_overlay);
        }

        private void BuildDescribePanel()
        {
            _describePanel = new PanelContainer();
            _describePanel.AddThemeStyleboxOverride("panel", EditorStyles.MakePanel(
                bg: EditorStyles.BgDark, border: EditorStyles.BorderAccent, borderWidth: 2, cornerRadius: 6, margin: 12));
            _describePanel.CustomMinimumSize = new Vector2(380, 0);
            _describePanel.Visible = false;

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);

            // Title
            vbox.AddChild(EditorStyles.MakeLabel("QUICK BUG REPORT", EditorStyles.FontHeader, EditorStyles.TextAccent));

            // Description input
            _descriptionEdit = EditorStyles.MakeLineEdit("What's wrong here?");
            _descriptionEdit.AddThemeStyleboxOverride("normal", EditorStyles.MakeFlat(EditorStyles.BgField));
            _descriptionEdit.CustomMinimumSize = new Vector2(350, 32);
            vbox.AddChild(_descriptionEdit);

            // Severity row
            var sevRow = new HBoxContainer();
            sevRow.AddThemeConstantOverride("separation", 6);
            sevRow.AddChild(EditorStyles.MakeLabel("Severity:", EditorStyles.FontSmall, EditorStyles.TextSecondary));

            string[] sevLabels = { "Low", "Med", "High" };
            Color[] sevColors = {
                EditorStyles.StatusSaved,
                EditorStyles.StatusDirty,
                EditorStyles.StatusError
            };
            _severityButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = EditorStyles.MakeButton(sevLabels[i], EditorStyles.FontSmall, sevColors[i]);
                btn.CustomMinimumSize = new Vector2(50, 26);
                btn.Pressed += () => SetSeverity(idx);
                _severityButtons[i] = btn;
                sevRow.AddChild(btn);
            }
            vbox.AddChild(sevRow);

            // Button row
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 8);
            btnRow.Alignment = BoxContainer.AlignmentMode.End;

            var cancelBtn = EditorStyles.MakeButton("Cancel", EditorStyles.FontBody, EditorStyles.StatusError);
            cancelBtn.CustomMinimumSize = new Vector2(80, 30);
            cancelBtn.Pressed += Cancel;
            btnRow.AddChild(cancelBtn);

            var submitBtn = EditorStyles.MakeButton("Submit", EditorStyles.FontBody, EditorStyles.StatusSaved);
            submitBtn.CustomMinimumSize = new Vector2(80, 30);
            submitBtn.Pressed += Submit;
            btnRow.AddChild(submitBtn);

            vbox.AddChild(btnRow);
            _describePanel.AddChild(vbox);

            // Submit on Enter key
            _descriptionEdit.TextSubmitted += (_) => Submit();
        }

        private void SetSeverity(int idx)
        {
            _severity = idx;
            UpdateSeverityButtons();
        }

        private void UpdateSeverityButtons()
        {
            for (int i = 0; i < 3; i++)
            {
                if (i == _severity)
                    _severityButtons[i].AddThemeStyleboxOverride("normal", EditorStyles.MakePanel(
                        bg: EditorStyles.BgSelected, borderWidth: 1, cornerRadius: 3, margin: 4));
                else
                    _severityButtons[i].RemoveThemeStyleboxOverride("normal");
            }
        }

        private void OnOverlayInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            {
                Cancel();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_phase == Phase.Snip)
                HandleSnipInput(@event);
        }

        private void HandleSnipInput(InputEvent @event)
        {
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

                    // Require minimum size
                    if (rect.Size.X < 10 || rect.Size.Y < 10)
                        return;

                    _snipCanvas.DragRect = rect;
                    _snipCanvas.Locked = true;
                    _snipCanvas.QueueRedraw();
                    EnterDescribeMode(rect);
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

        private void EnterDescribeMode(Rect2 rect)
        {
            _phase = Phase.Describe;
            _instructionLabel.Visible = false;

            // Position the describe panel near the rectangle
            var screenSize = GetViewport().GetVisibleRect().Size;
            float panelX = Mathf.Clamp(rect.Position.X, 0, screenSize.X - 400);
            float panelY = rect.End.Y + 12;

            // If too close to bottom, place above
            if (panelY + 160 > screenSize.Y)
                panelY = Mathf.Max(0, rect.Position.Y - 170);

            _describePanel.Position = new Vector2(panelX, panelY);
            _describePanel.Visible = true;

            _descriptionEdit.Text = "";
            _severity = 1;
            UpdateSeverityButtons();

            // Focus the description input next frame
            _descriptionEdit.CallDeferred("grab_focus");
        }

        private void Submit()
        {
            if (_phase != Phase.Describe) return;

            string description = _descriptionEdit.Text.Trim();
            if (string.IsNullOrEmpty(description))
                description = "Quick bug (no description)";

            string[] sevNames = { "low", "medium", "high" };
            string severity = sevNames[_severity];

            // Get active tab name
            string tabName = EditorManager.Instance?.ActiveTabName ?? "Unknown";

            // Burn rectangle onto image
            var screenSize = GetViewport().GetVisibleRect().Size;
            var rect = _snipCanvas.DragRect;
            BurnRectOnImage(_capturedImage, rect, screenSize);

            // Save report
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string repoRoot = GetRepoRoot();
            string bugDir = Path.Combine(repoRoot, "test-reports", "bugs", $"quick-{timestamp}");
            Directory.CreateDirectory(bugDir);

            // Save screenshot
            string screenshotPath = Path.Combine(bugDir, "screenshot.png");
            _capturedImage.SavePng(screenshotPath);

            // Build report
            string report = $"""
                # Quick Bug: {description}
                **Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                **Severity:** {severity}
                **Editor Tab:** {tabName}
                **Region:** [{(int)rect.Position.X}, {(int)rect.Position.Y}, {(int)rect.Size.X}, {(int)rect.Size.Y}]

                ![screenshot](screenshot.png)
                """;

            File.WriteAllText(Path.Combine(bugDir, "report.md"), report);

            // Close overlay and flash
            CloseOverlay();
            ShowFlash();
            GD.Print($"[QuickBugTool] Bug saved to {bugDir}");
        }

        private void Cancel()
        {
            CloseOverlay();
        }

        private void CloseOverlay()
        {
            _phase = Phase.Inactive;
            _overlay.Visible = false;
            _describePanel.Visible = false;
            _instructionLabel.Visible = false;
            Visible = false;
        }

        private void ShowFlash()
        {
            // Show flash on the editor layer instead of our hidden overlay
            _flashLabel.Visible = true;
            _flashTimer = 1.5f;
            // Reparent flash above overlay visibility
            Visible = true;
            _overlay.Visible = false;
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

            var color = new Color(1.0f, 0.9f, 0.0f); // bright yellow
            int thickness = 3;

            for (int t = 0; t < thickness; t++)
            {
                // Top and bottom edges
                for (int x = Mathf.Max(0, x1 - t); x <= Mathf.Min(imgW - 1, x2 + t); x++)
                {
                    int yt = Mathf.Clamp(y1 - t, 0, imgH - 1);
                    int yb = Mathf.Clamp(y2 + t, 0, imgH - 1);
                    image.SetPixel(x, yt, color);
                    image.SetPixel(x, yb, color);
                }
                // Left and right edges
                for (int y = Mathf.Max(0, y1 - t); y <= Mathf.Min(imgH - 1, y2 + t); y++)
                {
                    int xl = Mathf.Clamp(x1 - t, 0, imgW - 1);
                    int xr = Mathf.Clamp(x2 + t, 0, imgW - 1);
                    image.SetPixel(xl, y, color);
                    image.SetPixel(xr, y, color);
                }
            }
        }

        private static Rect2 MakeRect(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.X, b.X);
            float y = Mathf.Min(a.Y, b.Y);
            float w = Mathf.Abs(a.X - b.X);
            float h = Mathf.Abs(a.Y - b.Y);
            return new Rect2(x, y, w, h);
        }

        private static string GetRepoRoot()
        {
            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
            return Path.GetDirectoryName(godotDir);
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
                    // Solid yellow rect with translucent fill
                    DrawRect(DragRect, new Color(1.0f, 0.9f, 0.0f, 0.15f), true);
                    DrawRect(DragRect, new Color(1.0f, 0.9f, 0.0f, 1.0f), false, 2.0f);
                }
                else
                {
                    // Dashed-style: bright yellow outline
                    DrawRect(DragRect, new Color(1.0f, 0.9f, 0.0f, 0.8f), false, 2.0f);
                }
            }
        }
    }
}
