using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// S4: Boss run confirmation screen.
    /// Shows suit preview, section info, and warning about suit destruction.
    /// Requires deliberate confirmation — no accidental boss runs.
    /// Code-built CanvasLayer.
    /// </summary>
    public partial class BossConfirmScreen : CanvasLayer
    {
        private static readonly Color Accent = new(0.0f, 0.85f, 0.95f);
        private static readonly Color WarningRed = new(0.9f, 0.2f, 0.15f);
        private static readonly Color SafeGreen = new(0.3f, 0.7f, 0.4f);

        private int _suitIndex;
        private SuitSaveData _suit;
        private string _sectionId;

        public override void _Ready()
        {
            Layer = 10;

            var gm = GameManager.Instance;
            _suitIndex = gm?.EquippedSuitIndex ?? 0;
            _sectionId = gm?.BossSectionId ?? "";

            var suits = SuitManager.GetAll();
            _suit = (_suitIndex >= 0 && _suitIndex < suits.Length) ? suits[_suitIndex] : null;

            BuildUI();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    GetViewport().SetInputAsHandled();
                    GameManager.Instance?.ShowTerritory();
                }
            }
        }

        private void BuildUI()
        {
            // Full-screen dark background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(0.01f, 0.01f, 0.02f, 0.95f);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(center);

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(600, 500);
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.03f, 0.03f, 0.05f, 0.98f);
            panelStyle.BorderColor = WarningRed;
            panelStyle.SetBorderWidthAll(3);
            panelStyle.SetCornerRadiusAll(8);
            panelStyle.ContentMarginLeft = 30;
            panelStyle.ContentMarginRight = 30;
            panelStyle.ContentMarginTop = 24;
            panelStyle.ContentMarginBottom = 24;
            panel.AddThemeStyleboxOverride("panel", panelStyle);
            center.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 16);
            panel.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "BOSS RUN";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 36);
            title.AddThemeColorOverride("font_color", WarningRed);
            vbox.AddChild(title);

            // Section info
            var section = TerritoryLoader.GetSection(_sectionId);
            if (section != null)
            {
                var sectionLabel = new Label();
                sectionLabel.Text = $"Target: {section.Name} — Boss at Wave {section.BossWave}";
                sectionLabel.HorizontalAlignment = HorizontalAlignment.Center;
                sectionLabel.AddThemeFontSizeOverride("font_size", 18);
                sectionLabel.AddThemeColorOverride("font_color", Accent);
                vbox.AddChild(sectionLabel);
            }

            // Suit preview
            var suitPanel = new PanelContainer();
            var suitStyle = new StyleBoxFlat();
            suitStyle.BgColor = new Color(0.05f, 0.05f, 0.08f);
            suitStyle.BorderColor = Accent;
            suitStyle.SetBorderWidthAll(1);
            suitStyle.SetCornerRadiusAll(4);
            suitStyle.ContentMarginLeft = 16;
            suitStyle.ContentMarginRight = 16;
            suitStyle.ContentMarginTop = 12;
            suitStyle.ContentMarginBottom = 12;
            suitPanel.AddThemeStyleboxOverride("panel", suitStyle);
            vbox.AddChild(suitPanel);

            var suitInfo = new VBoxContainer();
            suitInfo.AddThemeConstantOverride("separation", 6);
            suitPanel.AddChild(suitInfo);

            if (_suit != null)
            {
                AddInfoRow(suitInfo, "Suit", _suit.Name, Accent);
                AddInfoRow(suitInfo, "Role", _suit.Role, new Color(0.7f, 0.8f, 0.9f));
                AddInfoRow(suitInfo, "Planet", $"P{_suit.Planet}", new Color(0.7f, 0.8f, 0.9f));
                AddInfoRow(suitInfo, "Material", _suit.Material.ToString(), new Color(0.7f, 0.8f, 0.9f));
                AddInfoRow(suitInfo, "Nodes", $"{_suit.Nodes.Count} placed", new Color(0.7f, 0.8f, 0.9f));
            }
            else
            {
                var noSuit = new Label();
                noSuit.Text = "No suit selected";
                noSuit.AddThemeFontSizeOverride("font_size", 16);
                noSuit.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                suitInfo.AddChild(noSuit);
            }

            // Warning
            var warning = new Label();
            warning.Text = "WARNING: This suit will be DESTROYED if you fail.";
            warning.HorizontalAlignment = HorizontalAlignment.Center;
            warning.AddThemeFontSizeOverride("font_size", 18);
            warning.AddThemeColorOverride("font_color", WarningRed);
            warning.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(warning);

            var subWarning = new Label();
            subWarning.Text = "Succeed and the territory section will be permanently cleared.";
            subWarning.HorizontalAlignment = HorizontalAlignment.Center;
            subWarning.AddThemeFontSizeOverride("font_size", 14);
            subWarning.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            subWarning.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(subWarning);

            // Suit selector (if multiple suits available)
            var availableSuits = SuitManager.GetAvailableSuits();
            if (availableSuits.Count > 1)
            {
                var suitSelectRow = new HBoxContainer();
                suitSelectRow.AddThemeConstantOverride("separation", 8);
                suitSelectRow.Alignment = BoxContainer.AlignmentMode.Center;
                vbox.AddChild(suitSelectRow);

                var selectLabel = new Label();
                selectLabel.Text = "Select Suit:";
                selectLabel.AddThemeFontSizeOverride("font_size", 14);
                selectLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
                suitSelectRow.AddChild(selectLabel);

                var allSuits = SuitManager.GetAll();
                for (int i = 0; i < allSuits.Length; i++)
                {
                    if (allSuits[i] == null || allSuits[i].Consumed || allSuits[i].Nodes.Count == 0) continue;
                    var suitBtn = new Button();
                    suitBtn.Text = allSuits[i].Name ?? $"Suit {i + 1}";
                    suitBtn.AddThemeFontSizeOverride("font_size", 14);
                    int idx = i;
                    suitBtn.Pressed += () => SwitchSuit(idx);
                    var btnStyle = CreateButtonStyle(i == _suitIndex ? Accent : new Color(0.4f, 0.4f, 0.4f));
                    suitBtn.AddThemeStyleboxOverride("normal", btnStyle);
                    suitSelectRow.AddChild(suitBtn);
                }
            }

            // Action buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 20);
            btnRow.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(btnRow);

            var cancelBtn = new Button();
            cancelBtn.Text = "Back to Safety";
            cancelBtn.AddThemeFontSizeOverride("font_size", 20);
            var cancelStyle = CreateButtonStyle(SafeGreen);
            cancelBtn.AddThemeStyleboxOverride("normal", cancelStyle);
            cancelBtn.Pressed += () => GameManager.Instance?.ShowTerritory();
            btnRow.AddChild(cancelBtn);

            var confirmBtn = new Button();
            confirmBtn.Text = "RISK IT";
            confirmBtn.AddThemeFontSizeOverride("font_size", 22);
            var confirmStyle = CreateButtonStyle(WarningRed);
            confirmBtn.AddThemeStyleboxOverride("normal", confirmStyle);
            confirmBtn.Disabled = _suit == null;
            confirmBtn.Pressed += () =>
            {
                var gm = GameManager.Instance;
                if (gm != null)
                    gm.StartBossRun(gm.CurrentPlanet, _suitIndex, _sectionId);
            };
            btnRow.AddChild(confirmBtn);
        }

        private void SwitchSuit(int newIndex)
        {
            _suitIndex = newIndex;
            GameManager.Instance.EquippedSuitIndex = newIndex;
            // Rebuild
            foreach (var child in GetChildren())
                child.QueueFree();
            BuildUI();
        }

        private void AddInfoRow(VBoxContainer parent, string label, string value, Color valueColor)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            parent.AddChild(row);

            var lbl = new Label();
            lbl.Text = $"{label}:";
            lbl.AddThemeFontSizeOverride("font_size", 16);
            lbl.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            lbl.CustomMinimumSize = new Vector2(100, 0);
            row.AddChild(lbl);

            var val = new Label();
            val.Text = value;
            val.AddThemeFontSizeOverride("font_size", 16);
            val.AddThemeColorOverride("font_color", valueColor);
            row.AddChild(val);
        }

        private static StyleBoxFlat CreateButtonStyle(Color borderColor)
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(borderColor.R * 0.15f, borderColor.G * 0.15f, borderColor.B * 0.15f, 0.9f);
            style.BorderColor = borderColor;
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(6);
            style.ContentMarginLeft = 20;
            style.ContentMarginRight = 20;
            style.ContentMarginTop = 10;
            style.ContentMarginBottom = 10;
            return style;
        }
    }
}
