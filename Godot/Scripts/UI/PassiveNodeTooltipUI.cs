using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Tooltip for passive tree nodes: name, type, stat bonuses, perk description, allocation hint.
    /// </summary>
    public partial class PassiveNodeTooltipUI : PanelContainer
    {
        private Label _nameLabel;
        private Label _typeLabel;
        private VBoxContainer _statsBox;
        private Label _descLabel;
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
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            AddThemeStyleboxOverride("panel", style);

            CustomMinimumSize = new Vector2(250, 40);
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 100;

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            AddChild(vbox);

            _nameLabel = new Label();
            _nameLabel.AddThemeFontSizeOverride("font_size", 16);
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            vbox.AddChild(_nameLabel);

            _typeLabel = new Label();
            _typeLabel.AddThemeFontSizeOverride("font_size", 12);
            _typeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            vbox.AddChild(_typeLabel);

            _statsBox = new VBoxContainer();
            _statsBox.AddThemeConstantOverride("separation", 1);
            vbox.AddChild(_statsBox);

            _descLabel = new Label();
            _descLabel.AddThemeFontSizeOverride("font_size", 13);
            _descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
            _descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _descLabel.CustomMinimumSize = new Vector2(230, 0);
            vbox.AddChild(_descLabel);

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

            // Node type with color coding
            var typeColor = node.NodeType switch
            {
                SkillNodeType.Pinnacle => new Color(1f, 0.6f, 0.2f),
                SkillNodeType.Keystone => new Color(0.8f, 0.5f, 0.9f),
                SkillNodeType.Notable => new Color(0.9f, 0.8f, 0.3f),
                SkillNodeType.CoreSocket => new Color(0.4f, 0.8f, 0.9f),
                _ => new Color(0.6f, 0.6f, 0.6f)
            };
            _typeLabel.Text = node.NodeType switch
            {
                SkillNodeType.CoreSocket => "Graft Socket",
                SkillNodeType.Pinnacle => "Pinnacle — Frame Upgrade",
                _ => node.NodeType.ToString()
            };
            _typeLabel.AddThemeColorOverride("font_color", typeColor);

            // Name color matches type for keystones/pinnacles
            _nameLabel.AddThemeColorOverride("font_color",
                node.NodeType is SkillNodeType.Pinnacle or SkillNodeType.Keystone
                    ? typeColor : new Color(0.9f, 0.8f, 0.3f));

            // Stat bonuses
            foreach (var child in _statsBox.GetChildren())
                if (child is Node n) n.QueueFree();

            bool hasStats = node.StatBonuses != null && node.StatBonuses.Count > 0;
            if (hasStats)
            {
                foreach (var bonus in node.StatBonuses)
                {
                    string sign = bonus.Value >= 0 ? "+" : "";
                    string text = bonus.ModType == ModifierType.Percent
                        ? $"{sign}{bonus.Value * 100:F0}% {bonus.StatType}"
                        : $"{sign}{bonus.Value:F0} {bonus.StatType}";

                    var label = new Label();
                    label.Text = text;
                    label.AddThemeFontSizeOverride("font_size", 14);
                    label.AddThemeColorOverride("font_color",
                        bonus.Value >= 0
                            ? new Color(0.3f, 0.9f, 0.3f)
                            : new Color(0.9f, 0.3f, 0.3f));
                    _statsBox.AddChild(label);
                }
            }
            _statsBox.Visible = hasStats;

            // Perk description (gameplay effect)
            bool hasDesc = !string.IsNullOrEmpty(node.Description);
            _descLabel.Text = hasDesc ? node.Description : "";
            _descLabel.Visible = hasDesc;

            // Socketed core info for CoreSocket nodes
            if (node.NodeType == SkillNodeType.CoreSocket && node.SocketedCore != null)
            {
                var coreLabel = new Label();
                coreLabel.Text = $"[{node.SocketedCore.CoreName}]";
                coreLabel.AddThemeFontSizeOverride("font_size", 14);
                coreLabel.AddThemeColorOverride("font_color", node.SocketedCore.Rarity switch
                {
                    SalvageCoreRarity.Legendary => new Color(1f, 0.6f, 0.2f),
                    SalvageCoreRarity.Epic => new Color(0.8f, 0.5f, 0.9f),
                    _ => new Color(0.4f, 0.8f, 0.9f)
                });
                _statsBox.AddChild(coreLabel);

                var coreDesc = new Label();
                coreDesc.Text = node.SocketedCore.Description;
                coreDesc.AddThemeFontSizeOverride("font_size", 12);
                coreDesc.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
                coreDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                coreDesc.CustomMinimumSize = new Vector2(230, 0);
                _statsBox.AddChild(coreDesc);
                _statsBox.Visible = true;
            }

            // Hint
            if (playerTree != null)
            {
                bool isAllocated = playerTree.AllocatedNodes.Contains(node.Id);
                int points = 0;
                if (ServiceLocator.TryGet<PlayerController>(out var player))
                    points = player.Stats.AvailableSkillPoints;

                if (isAllocated)
                {
                    if (node.NodeType == SkillNodeType.CoreSocket)
                        _hintLabel.Text = node.SocketedCore != null
                            ? "Right-click to change graft"
                            : "Right-click to socket graft";
                    else
                        _hintLabel.Text = "Allocated";
                }
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

            // Position — clamp to viewport
            var viewport = GetViewport();
            var vpSize = viewport?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var pos = screenPos + new Vector2(20, 10);
            if (pos.X + Size.X > vpSize.X - 10)
                pos.X = screenPos.X - Size.X - 10;
            if (pos.Y + Size.Y > vpSize.Y - 10)
                pos.Y = vpSize.Y - Size.Y - 10;

            Position = pos;
            Visible = true;
        }

        public void HideTooltip()
        {
            Visible = false;
        }
    }
}
