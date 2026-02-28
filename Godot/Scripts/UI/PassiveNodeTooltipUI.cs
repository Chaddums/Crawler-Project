using System.Linq;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Tooltip for passive tree nodes: name, type, stat bonuses, allocation hint.
    /// </summary>
    public partial class PassiveNodeTooltipUI : PanelContainer
    {
        private Label _nameLabel;
        private Label _typeLabel;
        private Label _statsLabel;
        private Label _hintLabel;

        public override void _Ready()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
            style.BorderColor = new Color(0.6f, 0.5f, 0.2f);
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            AddThemeStyleboxOverride("panel", style);

            CustomMinimumSize = new Vector2(200, 40);
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 100;

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 3);
            AddChild(vbox);

            _nameLabel = new Label();
            _nameLabel.AddThemeFontSizeOverride("font_size", 16);
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            vbox.AddChild(_nameLabel);

            _typeLabel = new Label();
            _typeLabel.AddThemeFontSizeOverride("font_size", 12);
            _typeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            vbox.AddChild(_typeLabel);

            _statsLabel = new Label();
            _statsLabel.AddThemeFontSizeOverride("font_size", 14);
            _statsLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(_statsLabel);

            _hintLabel = new Label();
            _hintLabel.AddThemeFontSizeOverride("font_size", 12);
            _hintLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.5f));
            vbox.AddChild(_hintLabel);

            Visible = false;
        }

        public void ShowNode(PassiveNodeData node, PassiveTree playerTree, Vector2 screenPos)
        {
            if (node == null)
            {
                Visible = false;
                return;
            }

            _nameLabel.Text = node.NodeName;
            _typeLabel.Text = node.NodeType.ToString();

            // Stat bonuses
            var statsText = "";
            foreach (var bonus in node.StatBonuses)
            {
                string sign = bonus.Value >= 0 ? "+" : "";
                if (bonus.ModType == ModifierType.Percent)
                    statsText += $"{sign}{bonus.Value * 100:F0}% {bonus.StatType}\n";
                else
                    statsText += $"{sign}{bonus.Value:F0} {bonus.StatType}\n";
            }
            _statsLabel.Text = statsText.TrimEnd('\n');
            _statsLabel.Visible = !string.IsNullOrEmpty(statsText);

            // Description
            if (!string.IsNullOrEmpty(node.Description))
                _typeLabel.Text += " - " + node.Description;

            // Hint
            if (playerTree != null)
            {
                bool isAllocated = playerTree.AllocatedNodes.Contains(node.Id);
                int points = 0;
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                    points = player.Stats.AvailableSkillPoints;

                if (isAllocated)
                    _hintLabel.Text = "Allocated";
                else if (playerTree.CanAllocate(node.Id, points))
                    _hintLabel.Text = "Click to allocate";
                else
                    _hintLabel.Text = "Not available";

                _hintLabel.AddThemeColorOverride("font_color",
                    isAllocated ? new Color(0.5f, 0.7f, 0.5f) :
                    playerTree.CanAllocate(node.Id, points) ? new Color(0.7f, 0.7f, 0.3f) :
                    new Color(0.5f, 0.5f, 0.5f));
            }
            else
            {
                _hintLabel.Text = "";
            }
            _hintLabel.Visible = !string.IsNullOrEmpty(_hintLabel.Text);

            // Position
            Position = screenPos + new Vector2(20, 10);
            Visible = true;
        }

        public void HideTooltip()
        {
            Visible = false;
        }
    }
}
