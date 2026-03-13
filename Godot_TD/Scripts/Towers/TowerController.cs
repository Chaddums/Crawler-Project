using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Runtime tower instance. Handles targeting, shooting, and mod slots.
    /// Attached to tower scene root (Node3D).
    /// </summary>
    public partial class TowerController : Node3D
    {
        public TowerData Data { get; private set; }
        public StatBlock Stats { get; private set; } = new();
        public Vector2I GridPosition { get; set; }
        public TowerRarity Rarity { get; set; } = TowerRarity.Scrap;

        private readonly List<ModComponentType> _mods = new();
        public IReadOnlyList<ModComponentType> Mods => _mods;

        private float _fireCooldown;
        private Node3D _target;
        private MeshInstance3D _meshBody;
        private MeshInstance3D _meshBarrel;

        public void Initialize(TowerData data, Vector2I gridPos)
        {
            Data = data;
            GridPosition = gridPos;

            Stats.SetBaseStat(StatType.AttackDamage, data.BaseDamage);
            Stats.SetBaseStat(StatType.Range, data.BaseRange);
            Stats.SetBaseStat(StatType.AttackSpeed, data.BaseFireRate);
            Stats.SetBaseStat(StatType.MaxHealth, data.BaseHealth);
            Stats.SetBaseStat(StatType.SplashRadius, data.SplashRadius);

            BuildVisual();
            AddToGroup(Constants.GROUP_TOWER);
        }

        private void BuildVisual()
        {
            // Base platform
            var baseMesh = new MeshInstance3D();
            var baseCylinder = new CylinderMesh();
            baseCylinder.TopRadius = Constants.CELL_SIZE * 0.35f;
            baseCylinder.BottomRadius = Constants.CELL_SIZE * 0.4f;
            baseCylinder.Height = 0.3f;
            baseMesh.Mesh = baseCylinder;
            baseMesh.Position = new Vector3(0, 0.15f, 0);

            var baseMat = new StandardMaterial3D();
            baseMat.AlbedoColor = Data.TintColor.Darkened(0.3f);
            baseMat.Roughness = 0.8f;
            baseMesh.MaterialOverride = baseMat;
            AddChild(baseMesh);

            // Tower body
            _meshBody = new MeshInstance3D();
            var bodyBox = new BoxMesh();
            float h = Data.TowerHeight;
            bodyBox.Size = new Vector3(Constants.CELL_SIZE * 0.3f, h, Constants.CELL_SIZE * 0.3f);
            _meshBody.Mesh = bodyBox;
            _meshBody.Position = new Vector3(0, 0.3f + h / 2f, 0);

            var bodyMat = new StandardMaterial3D();
            bodyMat.AlbedoColor = Data.TintColor;
            bodyMat.Roughness = 0.7f;
            bodyMat.Metallic = 0.4f;
            _meshBody.MaterialOverride = bodyMat;
            AddChild(_meshBody);

            // Barrel / turret top
            _meshBarrel = new MeshInstance3D();
            var barrelBox = new BoxMesh();
            barrelBox.Size = new Vector3(0.15f, 0.15f, Constants.CELL_SIZE * 0.4f);
            _meshBarrel.Mesh = barrelBox;
            _meshBarrel.Position = new Vector3(0, 0.3f + h, 0);

            var barrelMat = new StandardMaterial3D();
            barrelMat.AlbedoColor = Data.TintColor.Lightened(0.1f);
            barrelMat.Metallic = 0.6f;
            _meshBarrel.MaterialOverride = barrelMat;
            AddChild(_meshBarrel);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Data.BaseFireRate <= 0) return; // Recycler doesn't shoot

            _fireCooldown -= (float)delta;

            _target = FindTarget();

            if (_target != null)
            {
                // Rotate barrel toward target
                var dir = (_target.GlobalPosition - GlobalPosition).Normalized();
                dir.Y = 0;
                if (dir.LengthSquared() > 0.001f)
                {
                    float angle = Mathf.Atan2(dir.X, dir.Z);
                    _meshBarrel.Rotation = new Vector3(0, angle, 0);
                }

                if (_fireCooldown <= 0)
                {
                    Fire(_target);
                    _fireCooldown = 1f / Stats.GetStat(StatType.AttackSpeed);
                }
            }
        }

        private Node3D FindTarget()
        {
            float range = Stats.GetStat(StatType.Range) * Constants.CELL_SIZE;
            Node3D closest = null;
            float closestDist = float.MaxValue;

            foreach (var node in GetTree().GetNodesInGroup(Constants.GROUP_ENEMY))
            {
                if (node is not Node3D enemy) continue;
                float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
                if (dist <= range && dist < closestDist)
                {
                    closest = enemy;
                    closestDist = dist;
                }
            }
            return closest;
        }

        private void Fire(Node3D target)
        {
            float damage = Stats.GetStat(StatType.AttackDamage);
            float splash = Stats.GetStat(StatType.SplashRadius);

            // Create projectile
            var projectile = new TowerProjectile();
            projectile.Initialize(this, target, damage, Data.DamageType, splash);
            GetTree().CurrentScene.AddChild(projectile);
            projectile.GlobalPosition = _meshBarrel.GlobalPosition;
        }

        // --- Fabrication (Pillar #2) ---

        public bool CanAttachMod()
        {
            return _mods.Count < Data.MaxModSlots;
        }

        public void AttachMod(ModComponentType mod)
        {
            if (!CanAttachMod()) return;
            _mods.Add(mod);
            ApplyModEffect(mod);
            GameEvents.OnModAttached?.Invoke(this, mod);
        }

        private void ApplyModEffect(ModComponentType mod)
        {
            switch (mod)
            {
                case ModComponentType.Gyroscope:
                    // Handled in targeting — improves accuracy vs fast enemies
                    break;
                case ModComponentType.HeatCoil:
                    Stats.AddModifier(new StatModifier(StatType.BurnDamage, ModifierType.Flat, 3f, mod));
                    break;
                case ModComponentType.CryoCell:
                    Stats.AddModifier(new StatModifier(StatType.SlowPotency, ModifierType.Flat, 0.3f, mod));
                    break;
                case ModComponentType.ChargeCapacitor:
                    Stats.AddModifier(new StatModifier(StatType.AttackDamage, ModifierType.Percent, 0.5f, mod));
                    Stats.AddModifier(new StatModifier(StatType.AttackSpeed, ModifierType.Percent, -0.25f, mod));
                    break;
                case ModComponentType.SplitPrism:
                    // Handled in projectile — splits on hit
                    break;
                case ModComponentType.ReinforcedPlating:
                    Stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierType.Percent, 0.5f, mod));
                    break;
                case ModComponentType.OverclockModule:
                    Stats.AddModifier(new StatModifier(StatType.AttackSpeed, ModifierType.Percent, 0.4f, mod));
                    break;
                case ModComponentType.SalvageHopper:
                    // Handled in economy — more scrap from kills in range
                    break;
                case ModComponentType.RangeExtender:
                    Stats.AddModifier(new StatModifier(StatType.Range, ModifierType.Percent, 0.3f, mod));
                    break;
                case ModComponentType.ShockAbsorber:
                    Stats.AddModifier(new StatModifier(StatType.Armor, ModifierType.Flat, 10f, mod));
                    break;
            }
        }

        public int GetSellValue()
        {
            return Mathf.FloorToInt(Data.ScrapCost * Constants.TOWER_SELL_REFUND);
        }
    }
}
