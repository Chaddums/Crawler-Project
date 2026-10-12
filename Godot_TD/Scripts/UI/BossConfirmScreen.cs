using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Before a boss fight: the target, and which suit to take. A suit's towers start on the
    /// build bar (KIT), free to place anywhere at their saved levels; lose the fight and the suit
    /// is lost. Going in without a suit risks nothing. Code-built CanvasLayer.
    /// </summary>
    public partial class BossConfirmScreen : CanvasLayer
    {
        private static readonly Color Accent = new(0.0f, 0.85f, 0.95f);
        private static readonly Color WarningRed = new(0.9f, 0.2f, 0.15f);
        private static readonly Color SafeGreen = new(0.3f, 0.7f, 0.4f);
        private static readonly Color Dim = new(0.62f, 0.66f, 0.72f);

        private int _suitIndex = -1;
        private SuitSaveData _suit;
        private string _sectionId;
        /// <summary>The fight button (tests).</summary>
        internal Button ConfirmButton { get; private set; }
        internal int SuitIndex => _suitIndex;

        public override void _Ready()
        {
            Layer = 10;

            var gm = GameManager.Instance;
            _sectionId = gm?.BossSectionId ?? "";
            var suits = SuitManager.GetAll();
            int asked = gm?.EquippedSuitIndex ?? 0;
            // Callers pass slot 0 even when it's empty or lost: take the first usable suit then
            if (asked >= 0 && asked < suits.Length && suits[asked] != null && suits[asked].Usable)
                _suitIndex = asked;
            else
                _suitIndex = System.Array.FindIndex(suits, x => x != null && x.Usable);
            _suit = _suitIndex >= 0 ? suits[_suitIndex] : null;
            if (gm != null) gm.EquippedSuitIndex = _suit != null ? _suitIndex : null;

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

        private static Label L(string text, int size, Color color, bool center = false)
        {
            var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            if (center) l.HorizontalAlignment = HorizontalAlignment.Center;
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            return l;
        }

        private void BuildUI()
        {
            var gm = GameManager.Instance;
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(0.01f, 0.01f, 0.02f, 0.95f);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(center);

            var panel = new PanelContainer { CustomMinimumSize = new Vector2(680, 520) };
            var panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.03f, 0.03f, 0.05f, 0.98f),
                BorderColor = _suit != null ? WarningRed : Accent,
                ContentMarginLeft = 30, ContentMarginRight = 30, ContentMarginTop = 24, ContentMarginBottom = 24,
            };
            panelStyle.SetBorderWidthAll(3);
            panelStyle.SetCornerRadiusAll(8);
            panel.AddThemeStyleboxOverride("panel", panelStyle);
            center.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 14);
            panel.AddChild(vbox);

            vbox.AddChild(L("BOSS FIGHT", 36, WarningRed, true));
            var section = TerritoryLoader.GetSection(_sectionId);
            if (section != null)
                vbox.AddChild(L($"{section.Name}: the boss comes at wave {section.BossWave}", 18, Accent, true));

            // Which suit
            var pick = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            pick.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(pick);
            var take = L("Take:", 15, Dim);
            take.AutowrapMode = TextServer.AutowrapMode.Off;
            take.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            pick.AddChild(take);
            var all = SuitManager.GetAll();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].Usable) continue;
                int idx = i;
                pick.AddChild(PickButton(all[i].Name ?? $"Suit {i + 1}", i == _suitIndex, () => SwitchSuit(idx)));
            }
            pick.AddChild(PickButton("No suit", _suitIndex < 0, () => SwitchSuit(-1)));

            // What it brings
            var suitPanel = new PanelContainer();
            var suitStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.05f, 0.08f), BorderColor = Accent,
                ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12,
            };
            suitStyle.SetBorderWidthAll(1);
            suitStyle.SetCornerRadiusAll(4);
            suitPanel.AddThemeStyleboxOverride("panel", suitStyle);
            vbox.AddChild(suitPanel);
            var info = new VBoxContainer { Name = "SuitInfo" };
            info.AddThemeConstantOverride("separation", 4);
            suitPanel.AddChild(info);

            if (_suit != null)
            {
                info.AddChild(L($"{_suit.Name}   {_suit.Role}   from {SuitManager.Origin(_suit)}", 17, Accent));
                info.AddChild(L("Its towers start on your build bar marked KIT, free to place anywhere, at these levels:", 14, Dim));
                foreach (var line in SuitManager.Describe(_suit))
                    info.AddChild(L($"   {SuitManager.LineLabel(line)}", 15, line.Color.Lightened(0.35f)));
                vbox.AddChild(L("Lose this fight and the suit is lost. Win and you keep it.", 18, WarningRed, true));
            }
            else
            {
                string role = string.IsNullOrEmpty(gm?.SelectedRole) ? "Obelisk" : gm.SelectedRole;
                info.AddChild(L($"No suit: you start with the Spire and your Resources, as {role}.", 16, Accent));
                info.AddChild(L(all.Any(x => x != null && x.Usable)
                    ? "Nothing to lose."
                    : "You have no suits yet. Save your best towers as one at the end of a run, and bring them here.", 14, Dim));
            }
            vbox.AddChild(L("Win and the site is cleared for good.", 14, Dim, true));

            var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            btnRow.AddThemeConstantOverride("separation", 20);
            vbox.AddChild(btnRow);

            var cancelBtn = new Button { Text = "Back to Safety" };
            cancelBtn.AddThemeFontSizeOverride("font_size", 20);
            cancelBtn.AddThemeStyleboxOverride("normal", CreateButtonStyle(SafeGreen));
            cancelBtn.Pressed += () => GameManager.Instance?.ShowTerritory();
            btnRow.AddChild(cancelBtn);

            ConfirmButton = new Button { Text = _suit != null ? "RISK IT" : "FIGHT", Name = "Confirm" };
            ConfirmButton.AddThemeFontSizeOverride("font_size", 22);
            ConfirmButton.AddThemeStyleboxOverride("normal", CreateButtonStyle(_suit != null ? WarningRed : Accent));
            ConfirmButton.Pressed += () =>
            {
                var g = GameManager.Instance;
                g?.StartBossRun(g.CurrentPlanet, _suit != null ? _suitIndex : -1, _sectionId);
            };
            btnRow.AddChild(ConfirmButton);
        }

        private Button PickButton(string text, bool on, System.Action pressed)
        {
            var b = new Button { Text = text, ToggleMode = true, ButtonPressed = on };
            b.AddThemeFontSizeOverride("font_size", 15);
            b.AddThemeStyleboxOverride("normal", CreateButtonStyle(on ? Accent : new Color(0.4f, 0.4f, 0.4f)));
            b.AddThemeStyleboxOverride("pressed", CreateButtonStyle(Accent));
            b.Pressed += pressed;
            return b;
        }

        internal void SwitchSuit(int newIndex)
        {
            var all = SuitManager.GetAll();
            _suitIndex = newIndex >= 0 && newIndex < all.Length && all[newIndex] != null && all[newIndex].Usable ? newIndex : -1;
            _suit = _suitIndex >= 0 ? all[_suitIndex] : null;
            if (GameManager.Instance != null) GameManager.Instance.EquippedSuitIndex = _suit != null ? _suitIndex : null;
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
