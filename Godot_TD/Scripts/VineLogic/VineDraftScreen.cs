using System;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Pre-run role screen: pick 1 of 3 roles. Every role builds every tower; each card shows
    /// how the role plays, what its Spire fights with, and what the role changes (see
    /// <see cref="RoleRun"/>). Full-screen code-built UI matching Tron theme.
    /// </summary>
    public partial class VineDraftScreen : CanvasLayer
    {
        // ── Role Definitions ──

        private struct RoleData
        {
            public string Name;
            public string Tagline;
            public Color Color;
            public VineNodeType[] Nodes;
        }

        public static VineNodeType[] GetRoleNodes(int index)
        {
            if (index < 0 || index >= Roles.Length) return System.Array.Empty<VineNodeType>();
            return Roles[index].Nodes;
        }

        public static int RoleCount => Roles.Length;

        public static string GetRoleName(int index)
        {
            if (index < 0 || index >= Roles.Length) return "";
            return Roles[index].Name;
        }

        private static readonly VineNodeType[] SharedTowers = new[] {
            VineNodeType.DamageTower,
            VineNodeType.SlowField,
            VineNodeType.ScatterCannon,
            VineNodeType.TeslaCoil,
            VineNodeType.FlakBattery,
            VineNodeType.BarrierWall,
            VineNodeType.PushPull,
            VineNodeType.BuffEmitter
        };

        private static readonly RoleData[] Roles = new[]
        {
            new RoleData
            {
                Name = "Obelisk",
                Tagline = "Command the field",
                Color = new Color(0.5f, 0.7f, 1.0f),
                Nodes = SharedTowers
            },
            new RoleData
            {
                Name = "Arcanist",
                Tagline = "Read the signals",
                Color = new Color(0.2f, 0.9f, 0.4f),
                Nodes = SharedTowers
            },
            new RoleData
            {
                Name = "Bruteforge",
                Tagline = "Build the weapons",
                Color = new Color(0.9f, 0.5f, 0.2f),
                Nodes = SharedTowers
            }
        };

        // ── UI references ──
        private PanelContainer[] _cards = new PanelContainer[3];
        /// <summary>The role cards (tests).</summary>
        internal PanelContainer[] Cards => _cards;

        public override void _Ready()
        {
            Layer = 10;
            BuildUI();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    GetViewport().SetInputAsHandled();
                    GameManager.Instance?.ReturnToMainMenu();
                }
            }
        }

        private void BuildUI()
        {
            // Full-screen dark background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = TronTheme.Background;
            AddChild(bg);

            // Outer panel, centred
            var outerCenter = new CenterContainer();
            outerCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(outerCenter);

            var outerPanel = new PanelContainer();
            outerPanel.CustomMinimumSize = new Vector2(1060, 660);
            var outerStyle = new StyleBoxFlat();
            outerStyle.BgColor = new Color(TronTheme.PanelBg.R, TronTheme.PanelBg.G, TronTheme.PanelBg.B, 0.9f);
            outerStyle.BorderColor = TronTheme.GridCyan;
            outerStyle.SetBorderWidthAll(2);
            outerStyle.SetCornerRadiusAll(6);
            outerStyle.ContentMarginLeft = 20;
            outerStyle.ContentMarginRight = 20;
            outerStyle.ContentMarginTop = 16;
            outerStyle.ContentMarginBottom = 16;
            outerPanel.AddThemeStyleboxOverride("panel", outerStyle);
            outerCenter.AddChild(outerPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            outerPanel.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "CHOOSE YOUR ROLE";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 32);
            title.AddThemeColorOverride("font_color", TronTheme.GridCyan);
            vbox.AddChild(title);

            // Subtitle
            var subtitle = new Label();
            subtitle.Text = "Every role builds every tower. The role decides how the run plays.";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 16);
            subtitle.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
            vbox.AddChild(subtitle);

            // Cards row
            var cardRow = new HBoxContainer();
            cardRow.AddThemeConstantOverride("separation", 16);
            cardRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            cardRow.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(cardRow);

            for (int i = 0; i < 3; i++)
                BuildRoleCard(cardRow, i);

            // ESC hint
            var escHint = new Label();
            escHint.Text = "ESC: back to menu";
            escHint.HorizontalAlignment = HorizontalAlignment.Center;
            escHint.AddThemeFontSizeOverride("font_size", 12);
            escHint.AddThemeColorOverride("font_color", new Color(0.35f, 0.35f, 0.35f));
            vbox.AddChild(escHint);
        }

        private void BuildRoleCard(HBoxContainer parent, int roleIndex)
        {
            var role = Roles[roleIndex];

            var card = new PanelContainer();
            card.CustomMinimumSize = new Vector2(320, 560);
            card.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            var cardStyle = new StyleBoxFlat();
            cardStyle.BgColor = new Color(role.Color.R * 0.1f, role.Color.G * 0.1f, role.Color.B * 0.1f, 0.9f);
            cardStyle.BorderColor = new Color(role.Color.R * 0.5f, role.Color.G * 0.5f, role.Color.B * 0.5f, 1f);
            cardStyle.SetBorderWidthAll(2);
            cardStyle.SetCornerRadiusAll(6);
            cardStyle.ContentMarginLeft = 12;
            cardStyle.ContentMarginRight = 12;
            cardStyle.ContentMarginTop = 12;
            cardStyle.ContentMarginBottom = 12;
            card.AddThemeStyleboxOverride("panel", cardStyle);
            parent.AddChild(card);
            _cards[roleIndex] = card;

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            card.AddChild(vbox);

            // Role name
            var nameLabel = new Label();
            nameLabel.Text = role.Name.ToUpper();
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", 22);
            nameLabel.AddThemeColorOverride("font_color", role.Color);
            vbox.AddChild(nameLabel);

            // Tagline
            var tagline = new Label();
            tagline.Text = $"\"{role.Tagline}\"";
            tagline.HorizontalAlignment = HorizontalAlignment.Center;
            tagline.AddThemeFontSizeOverride("font_size", 14);
            tagline.AddThemeColorOverride("font_color", new Color(role.Color.R * 0.6f, role.Color.G * 0.6f, role.Color.B * 0.6f, 1f));
            vbox.AddChild(tagline);

            // Separator
            vbox.AddChild(new HSeparator());

            // What the role changes (Data/Spires/*.json "role"): every role builds the same
            // towers, so the card shows what the Spire fights with and how the run plays
            var spire = SpireData.Get(role.Name);
            var dim = new Color(0.55f, 0.58f, 0.62f);
            void Header(string text)
            {
                var h = new Label { Text = text };
                h.AddThemeFontSizeOverride("font_size", 13);
                h.AddThemeColorOverride("font_color", dim);
                vbox.AddChild(h);
            }
            Label Body(string text, Color color, int size = 15)
            {
                var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
                l.CustomMinimumSize = new Vector2(280, 0);
                l.AddThemeFontSizeOverride("font_size", size);
                l.AddThemeColorOverride("font_color", color);
                vbox.AddChild(l);
                return l;
            }
            if (spire != null && !string.IsNullOrEmpty(spire.RolePlaystyle))
            {
                Header("HOW IT PLAYS");
                Body(spire.RolePlaystyle, new Color(0.92f, 0.92f, 0.95f), 16).Name = "Playstyle";
                Header("THE SPIRE");
                Body(spire.RoleWeapon, role.Color.Lightened(0.35f)).Name = "Weapon";
                Header("ROLE BONUSES");
                int n = 0;
                foreach (var b in spire.RoleBonuses)
                {
                    if (b.Signature || string.IsNullOrEmpty(b.Text)) continue;
                    Body($"+ {b.Text}", new Color(0.85f, 0.88f, 0.9f), 14).Name = $"Bonus{n++}";
                }
                foreach (var b in spire.RoleBonuses)
                {
                    if (!b.Signature || string.IsNullOrEmpty(b.Text)) continue;
                    Header("SIGNATURE");
                    Body(b.Text, new Color(1f, 0.85f, 0.4f)).Name = "Signature";
                }
            }

            // Spacer to push button to bottom
            var spacer = new Control();
            spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            vbox.AddChild(spacer);

            var every = new Label { Text = "Builds every tower", HorizontalAlignment = HorizontalAlignment.Center };
            every.AddThemeFontSizeOverride("font_size", 12);
            every.AddThemeColorOverride("font_color", new Color(0.42f, 0.44f, 0.48f));
            vbox.AddChild(every);

            // Select button
            var selectBtn = new Button();
            selectBtn.Text = "SELECT";
            selectBtn.CustomMinimumSize = new Vector2(0, 36);

            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(role.Color.R * 0.2f, role.Color.G * 0.2f, role.Color.B * 0.2f, 1f);
            btnStyle.BorderColor = role.Color;
            btnStyle.SetBorderWidthAll(1);
            btnStyle.SetCornerRadiusAll(4);
            btnStyle.ContentMarginTop = 4;
            btnStyle.ContentMarginBottom = 4;
            selectBtn.AddThemeStyleboxOverride("normal", btnStyle);
            selectBtn.AddThemeColorOverride("font_color", role.Color);

            var hoverStyle = (StyleBoxFlat)btnStyle.Duplicate();
            hoverStyle.BgColor = new Color(role.Color.R * 0.35f, role.Color.G * 0.35f, role.Color.B * 0.35f, 1f);
            hoverStyle.BorderColor = role.Color;
            selectBtn.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = (StyleBoxFlat)btnStyle.Duplicate();
            pressedStyle.BgColor = new Color(role.Color.R * 0.5f, role.Color.G * 0.5f, role.Color.B * 0.5f, 1f);
            pressedStyle.BorderColor = role.Color;
            selectBtn.AddThemeStyleboxOverride("pressed", pressedStyle);

            int capturedIndex = roleIndex;
            selectBtn.Pressed += () => OnRoleSelected(capturedIndex);
            vbox.AddChild(selectBtn);

            // Hover effect on card
            card.MouseEntered += () =>
            {
                var style = card.GetThemeStylebox("panel") as StyleBoxFlat;
                if (style != null)
                {
                    var brightened = (StyleBoxFlat)style.Duplicate();
                    brightened.BorderColor = role.Color;
                    card.AddThemeStyleboxOverride("panel", brightened);
                }
            };
            card.MouseExited += () =>
            {
                card.AddThemeStyleboxOverride("panel", cardStyle);
            };
        }

        private void OnRoleSelected(int index)
        {
            var role = Roles[index];
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.SelectedRole = role.Name;

            // Use SpireData JSON node list (includes role-specific nodes like Socket/Prism/Pylon),
            // fall back to hardcoded draft list if JSON not found
            var spireData = SpireData.Get(role.Name);
            gm.AvailableNodes = spireData?.Nodes ?? role.Nodes;

            GD.Print($"[VineDraft] Selected role: {role.Name} with {gm.AvailableNodes.Length} nodes (source: {(spireData != null ? "SpireData JSON" : "hardcoded")})");
            gm.StartVineRun();
        }
    }
}
