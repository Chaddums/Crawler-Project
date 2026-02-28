using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Bridges class selection, passive tree, and player stats.
    /// Handles level-up point grants and class-aware stat growth.
    /// </summary>
    public partial class PlayerClassController : Node
    {
        private PlayerStats _stats;
        private CrawlerClassData _classData;
        private PassiveTree _passiveTree;

        public CrawlerClassData ClassData => _classData;
        public PassiveTree PassiveTree => _passiveTree;
        public CrawlerClassName? CurrentClass => _classData?.ClassName;

        public override void _Ready()
        {
            _stats = GetParent().GetNode<PlayerStats>("PlayerStats");
        }

        /// <summary>
        /// Initialize this player with a class. Call once at game start.
        /// </summary>
        public void SelectClass(CrawlerClassName className)
        {
            _classData = CrawlerClassRegistry.GetClass(className);
            if (_classData == null)
            {
                GD.PrintErr($"[PlayerClassController] Class not found: {className}");
                return;
            }

            // Copy base stats from class
            _stats.Stats.CopyBaseStatsFrom(_classData.BaseStats);
            _stats.SetClassData(_classData);

            // Initialize passive tree
            var treeData = PassiveTreeBuilder.Tree;
            _passiveTree = new PassiveTree(treeData, className);

            // Reinitialize health/mana from new stats
            var player = GetParent<PlayerController>();
            if (player?.Health != null)
            {
                float maxHp = _stats.GetStat(StatType.MaxHealth);
                player.Health.SetMaxHealth(maxHp, true);
            }

            float moveSpeed = _stats.GetStat(StatType.MoveSpeed);
            if (moveSpeed > 0)
                player?.Movement?.SetMoveSpeed(moveSpeed);

            GameEvents.OnClassSelected?.Invoke(_classData);
            GD.Print($"[PlayerClassController] Selected class: {_classData.DisplayName}");
        }

        /// <summary>
        /// Try to allocate a passive node using available skill points.
        /// </summary>
        public bool AllocatePassiveNode(string nodeId)
        {
            if (_passiveTree == null) return false;

            if (_passiveTree.AllocateNode(nodeId, _stats.Stats, _stats.AvailableSkillPoints))
            {
                _stats.SpendSkillPoint();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Respec the entire passive tree.
        /// </summary>
        public void Respec()
        {
            if (_passiveTree == null) return;
            int refunded = _passiveTree.Respec(_stats.Stats);
            _stats.RefundSkillPoints(refunded);
        }
    }
}
