using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Perk selection screen shown between floors.
    /// Pick 1 of 3 perks, then advance to the next floor.
    /// Code-built UI (same pattern as VineDraftScreen).
    /// </summary>
    public partial class VinePerkScreen : CanvasLayer
    {
        private List<PerkData> _choices;

        public override void _Ready()
        {
            Layer = 10;

            // Pick 3 random perks excluding already-selected ones
            var gm = GameManager.Instance;
            _choices = VinePerkRegistry.PickRandom(3, gm?.ActivePerks);

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

            // Outer centered container
            var outerCenter = new CenterContainer();
            outerCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(outerCenter);

            var outerPanel = new PanelContainer();
            outerPanel.CustomMinimumSize = new Vector2(760, 420);
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
            vbox.AddThemeConstantOverride("separation", 16);
            outerPanel.AddChild(vbox);

            // Header
            int currentFloor = GameManager.Instance?.CurrentFloor ?? 1;
            var title = new Label();
            title.Text = $"FLOOR {currentFloor} COMPLETE";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 32);
            title.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.3f));
            vbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = "Choose an upgrade:";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeFontSizeOverride("font_size", 18);
            subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
            vbox.AddChild(subtitle);

            // Perk cards row
            var cardRow = new HBoxContainer();
            cardRow.AddThemeConstantOverride("separation", 16);
            cardRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            cardRow.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(cardRow);

            for (int i = 0; i < _choices.Count; i++)
                BuildPerkCard(cardRow, i);

            // ESC hint
            var hint = new Label();
            hint.Text = "ESC \u2014 back to menu";
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.AddThemeFontSizeOverride("font_size", 12);
            hint.AddThemeColorOverride("font_color", new Color(0.35f, 0.35f, 0.35f));
            vbox.AddChild(hint);
        }

        private void BuildPerkCard(HBoxContainer parent, int index)
        {
            var perk = _choices[index];

            var card = new PanelContainer();
            card.CustomMinimumSize = new Vector2(210, 280);
            card.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            var cardStyle = new StyleBoxFlat();
            cardStyle.BgColor = new Color(perk.Color.R * 0.1f, perk.Color.G * 0.1f, perk.Color.B * 0.1f, 0.9f);
            cardStyle.BorderColor = new Color(perk.Color.R * 0.5f, perk.Color.G * 0.5f, perk.Color.B * 0.5f, 1f);
            cardStyle.SetBorderWidthAll(2);
            cardStyle.SetCornerRadiusAll(6);
            cardStyle.ContentMarginLeft = 16;
            cardStyle.ContentMarginRight = 16;
            cardStyle.ContentMarginTop = 16;
            cardStyle.ContentMarginBottom = 16;
            card.AddThemeStyleboxOverride("panel", cardStyle);
            parent.AddChild(card);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            card.AddChild(vbox);

            // Perk name
            var nameLabel = new Label();
            nameLabel.Text = perk.Name.ToUpper();
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", 20);
            nameLabel.AddThemeColorOverride("font_color", perk.Color);
            vbox.AddChild(nameLabel);

            // Separator
            vbox.AddChild(new HSeparator());

            // Description
            var desc = new Label();
            desc.Text = perk.Description;
            desc.HorizontalAlignment = HorizontalAlignment.Center;
            desc.AddThemeFontSizeOverride("font_size", 16);
            desc.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.75f));
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(desc);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            vbox.AddChild(spacer);

            // Select button
            var selectBtn = new Button();
            selectBtn.Text = "SELECT";
            selectBtn.CustomMinimumSize = new Vector2(0, 40);

            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(perk.Color.R * 0.2f, perk.Color.G * 0.2f, perk.Color.B * 0.2f, 1f);
            btnStyle.BorderColor = perk.Color;
            btnStyle.SetBorderWidthAll(1);
            btnStyle.SetCornerRadiusAll(4);
            btnStyle.ContentMarginTop = 4;
            btnStyle.ContentMarginBottom = 4;
            selectBtn.AddThemeStyleboxOverride("normal", btnStyle);
            selectBtn.AddThemeColorOverride("font_color", perk.Color);

            var hoverStyle = (StyleBoxFlat)btnStyle.Duplicate();
            hoverStyle.BgColor = new Color(perk.Color.R * 0.35f, perk.Color.G * 0.35f, perk.Color.B * 0.35f, 1f);
            selectBtn.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = (StyleBoxFlat)btnStyle.Duplicate();
            pressedStyle.BgColor = new Color(perk.Color.R * 0.5f, perk.Color.G * 0.5f, perk.Color.B * 0.5f, 1f);
            selectBtn.AddThemeStyleboxOverride("pressed", pressedStyle);

            int capturedIndex = index;
            selectBtn.Pressed += () => OnPerkSelected(capturedIndex);
            vbox.AddChild(selectBtn);

            // Hover effect
            card.MouseEntered += () =>
            {
                var bright = (StyleBoxFlat)cardStyle.Duplicate();
                bright.BorderColor = perk.Color;
                card.AddThemeStyleboxOverride("panel", bright);
            };
            card.MouseExited += () => card.AddThemeStyleboxOverride("panel", cardStyle);
        }

        private void OnPerkSelected(int index)
        {
            var perk = _choices[index];
            var gm = GameManager.Instance;
            if (gm == null) return;

            GD.Print($"[VinePerk] Selected: {perk.Name}");
            gm.AddPerk(perk);
            gm.StartVineFloor(gm.CurrentFloor + 1);
        }
    }
}
