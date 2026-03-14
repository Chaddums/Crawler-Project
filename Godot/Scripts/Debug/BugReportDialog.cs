using Godot;
using System;
using System.IO;

namespace JunkbotArena
{
    /// <summary>
    /// In-game bug report dialog. Auto-fills with scene context (player position,
    /// room info, nearby nodes with IDs/types/collision shapes).
    /// Writes reports to test-reports/bugs/ with full context.
    /// </summary>
    public partial class BugReportDialog : CanvasLayer
    {
        private PanelContainer _panel;
        private TextEdit _contextDisplay;
        private TextEdit _descriptionEdit;
        private OptionButton _severityPicker;
        private Label _statusLabel;
        private string _capturedContext;
        private bool _isFeatureRequest;
        private bool _wasPaused;

        public static void Show(SceneTree tree, bool featureRequest = false)
        {
            var dialog = new BugReportDialog();
            dialog._isFeatureRequest = featureRequest;
            tree.Root.AddChild(dialog);
        }

        public override void _Ready()
        {
            Layer = 101; // Above editor (100)
            ProcessMode = ProcessModeEnum.Always;

            // Pause the game while bug report is open
            _wasPaused = GetTree().Paused;
            GetTree().Paused = true;

            // Capture context immediately
            _capturedContext = SceneContext.Capture(GetTree());

            BuildUI();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
            {
                Close();
                GetViewport().SetInputAsHandled();
            }
        }

        private void BuildUI()
        {
            _panel = new PanelContainer();
            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            _panel.GrowHorizontal = Control.GrowDirection.Both;
            _panel.GrowVertical = Control.GrowDirection.Both;
            _panel.OffsetLeft = -350;
            _panel.OffsetRight = 350;
            _panel.OffsetTop = -300;
            _panel.OffsetBottom = 300;

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.06f, 0.10f, 0.97f);
            style.BorderColor = _isFeatureRequest
                ? new Color(0.3f, 0.8f, 0.9f)
                : new Color(1f, 0.4f, 0.3f);
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
            var titleColor = _isFeatureRequest
                ? new Color(0.3f, 0.8f, 0.9f)
                : new Color(1f, 0.4f, 0.3f);

            var title = new Label();
            title.Text = titleText;
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 20);
            title.AddThemeColorOverride("font_color", titleColor);
            vbox.AddChild(title);

            var sep1 = new HSeparator();
            vbox.AddChild(sep1);

            // Description input
            var descLabel = new Label();
            descLabel.Text = "What happened? (describe the issue)";
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
                sevLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
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

            // Scene context (auto-captured, read-only)
            var ctxLabel = new Label();
            ctxLabel.Text = "Scene Context (auto-captured):";
            ctxLabel.AddThemeFontSizeOverride("font_size", 12);
            ctxLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
            vbox.AddChild(ctxLabel);

            _contextDisplay = new TextEdit();
            _contextDisplay.Text = _capturedContext;
            _contextDisplay.Editable = false;
            _contextDisplay.CustomMinimumSize = new Vector2(0, 160);
            _contextDisplay.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _contextDisplay.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _contextDisplay.AddThemeFontSizeOverride("font_size", 10);
            _contextDisplay.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 0.5f));
            vbox.AddChild(_contextDisplay);

            // Buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 8);

            var submitBtn = new Button();
            submitBtn.Text = _isFeatureRequest ? "Submit Feature" : "Submit Bug";
            submitBtn.CustomMinimumSize = new Vector2(120, 32);
            submitBtn.AddThemeFontSizeOverride("font_size", 14);
            submitBtn.AddThemeColorOverride("font_color", titleColor);
            submitBtn.Pressed += OnSubmit;
            btnRow.AddChild(submitBtn);

            var copyBtn = new Button();
            copyBtn.Text = "Copy to Clipboard";
            copyBtn.CustomMinimumSize = new Vector2(120, 32);
            copyBtn.AddThemeFontSizeOverride("font_size", 12);
            copyBtn.Pressed += OnCopyToClipboard;
            btnRow.AddChild(copyBtn);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btnRow.AddChild(spacer);

            var cancelBtn = new Button();
            cancelBtn.Text = "Cancel (Esc)";
            cancelBtn.CustomMinimumSize = new Vector2(100, 32);
            cancelBtn.AddThemeFontSizeOverride("font_size", 12);
            cancelBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            cancelBtn.Pressed += Close;
            btnRow.AddChild(cancelBtn);

            vbox.AddChild(btnRow);

            // Status
            _statusLabel = new Label();
            _statusLabel.Text = "";
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(_statusLabel);

            // Focus the description field
            _descriptionEdit.GrabFocus();
        }

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

            // Build the report
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string folderName = $"{type}-{timestamp}";

            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/').TrimEnd('\\');
            string repoRoot = Path.GetDirectoryName(godotDir);
            string reportDir = Path.Combine(repoRoot, "test-reports", "bugs", folderName);

            try
            {
                Directory.CreateDirectory(reportDir);

                string report = BuildReport(type, severity, description);
                File.WriteAllText(Path.Combine(reportDir, "report.md"), report);

                _statusLabel.Text = $"Saved to test-reports/bugs/{folderName}/";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 0.4f));
                GD.Print($"[BugReport] Saved {type} report to {reportDir}");

                // Auto-close after a short delay
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
            string report = $"[{type}] {description}\n\n{_capturedContext}";
            DisplayServer.ClipboardSet(report);

            _statusLabel.Text = "Copied to clipboard!";
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 0.4f));
        }

        private string BuildReport(string type, string severity, string description)
        {
            string typeTitle = _isFeatureRequest ? "Feature Request" : "Bug Report";
            string sevSection = !_isFeatureRequest ? $"**Severity:** {severity}\n" : "";

            return $@"# {typeTitle}: {(description.Length > 60 ? description[..57] + "..." : description)}
**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}
**Type:** {type}
{sevSection}
## Description
{description}

## Scene Context
```
{_capturedContext}
```

## How to Reproduce
1. Open the game
2. Navigate to Sector {GameManager.Instance?.CurrentSector ?? 0}, Area {GameManager.Instance?.CurrentArea ?? 0}
3. [Fill in steps]

## Expected Behavior
[What should have happened]

## Actual Behavior
[What actually happened]
";
        }

        private void Close()
        {
            GetTree().Paused = _wasPaused;
            QueueFree();
        }
    }
}
