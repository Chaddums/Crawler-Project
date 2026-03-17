using System;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Pre-run draft screen: pick 1 of 3 roles to determine your build bar node pool.
    /// Full-screen code-built UI matching Tron theme.
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

        private static readonly RoleData[] Roles = new[]
        {
            new RoleData
            {
                Name = "Scrapwright",
                Tagline = "Build the maze",
                Color = new Color(0.5f, 0.7f, 1.0f),
                Nodes = new[] {
                    VineNodeType.Extender,
                    VineNodeType.Junction,
                    VineNodeType.Switch,
                    VineNodeType.Gate,
                    VineNodeType.Delay,
                    VineNodeType.Inverter,
                    VineNodeType.ProximitySensor,
                    VineNodeType.DamageTower
                }
            },
            new RoleData
            {
                Name = "Arcanist",
                Tagline = "Read the signals",
                Color = new Color(0.2f, 0.9f, 0.4f),
                Nodes = new[] {
                    VineNodeType.ProximitySensor,
                    VineNodeType.Timer,
                    VineNodeType.CountSensor,
                    VineNodeType.HPSensor,
                    VineNodeType.TypeSensor,
                    VineNodeType.Extender,
                    VineNodeType.DamageTower,
                    VineNodeType.SlowField
                }
            },
            new RoleData
            {
                Name = "Bruteforge",
                Tagline = "Build the weapons",
                Color = new Color(0.9f, 0.5f, 0.2f),
                Nodes = new[] {
                    VineNodeType.DamageTower,
                    VineNodeType.SlowField,
                    VineNodeType.BuffEmitter,
                    VineNodeType.PushPull,
                    VineNodeType.SignalCannon,
                    VineNodeType.Extender,
                    VineNodeType.Junction,
                    VineNodeType.ProximitySensor
                }
            }
        };

        // ── UI references ──
        private PanelContainer[] _cards = new PanelContainer[3];

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

            // Outer panel — centered container
            var outerCenter = new CenterContainer();
            outerCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(outerCenter);

            var outerPanel = new PanelContainer();
            outerPanel.CustomMinimumSize = new Vector2(760, 520);
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
            subtitle.Text = "How will you build?";
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
            escHint.Text = "ESC \u2014 back to menu";
            escHint.HorizontalAlignment = HorizontalAlignment.Center;
            escHint.AddThemeFontSizeOverride("font_size", 12);
            escHint.AddThemeColorOverride("font_color", new Color(0.35f, 0.35f, 0.35f));
            vbox.AddChild(escHint);
        }

        private void BuildRoleCard(HBoxContainer parent, int roleIndex)
        {
            var role = Roles[roleIndex];

            var card = new PanelContainer();
            card.CustomMinimumSize = new Vector2(220, 420);
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

            // Node list header
            var nodesHeader = new Label();
            nodesHeader.Text = "Nodes:";
            nodesHeader.AddThemeFontSizeOverride("font_size", 14);
            nodesHeader.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            vbox.AddChild(nodesHeader);

            // Node entries
            foreach (var nodeType in role.Nodes)
            {
                var data = VineNodeRegistry.Get(nodeType);
                if (data == null) continue;

                string tag = data.Category switch
                {
                    VineNodeCategory.Sensor => "[S]",
                    VineNodeCategory.Effect => "[E]",
                    _ => "[R]"
                };

                Color catColor = data.Category switch
                {
                    VineNodeCategory.Sensor => new Color(0.2f, 0.9f, 0.4f),
                    VineNodeCategory.Effect => new Color(0.9f, 0.5f, 0.2f),
                    _ => new Color(0.5f, 0.7f, 1.0f)
                };

                var nodeLabel = new Label();
                nodeLabel.Text = $"{tag} {data.Name} ({data.GoldCost}g)";
                nodeLabel.AddThemeFontSizeOverride("font_size", 13);
                nodeLabel.AddThemeColorOverride("font_color", catColor);
                vbox.AddChild(nodeLabel);
            }

            // Spacer to push button to bottom
            var spacer = new Control();
            spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            vbox.AddChild(spacer);

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
            gm.AvailableNodes = role.Nodes;

            GD.Print($"[VineDraft] Selected role: {role.Name} with {role.Nodes.Length} nodes");
            gm.StartVineRun();
        }
    }
}
