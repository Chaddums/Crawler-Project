using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Runtime enemy that follows a path from spawn to core.
    /// Pillar #5: enemies can gain scrap armor from Fabricators.
    /// </summary>
    public partial class EnemyController : Node3D
    {
        public EnemyData Data { get; private set; }
        public float CurrentHealth { get; private set; }
        public float Armor { get; private set; }
        public float MoveSpeed { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public EnemyTier Tier { get; set; } = EnemyTier.Normal;

        // Scrap armor from Fabricator enemies (Pillar #5)
        public float ScrapArmor { get; set; }

        private List<Vector2I> _path;
        private int _pathIndex;
        private MapGrid _grid;

        // Slow debuff
        private float _slowAmount;
        private float _slowTimer;

        // Burn debuff
        private float _burnDamage;
        private float _burnTimer;

        private MeshInstance3D _mesh;
        private MeshInstance3D _healthBar;

        public void Initialize(EnemyData data, List<Vector2I> path)
        {
            Data = data;
            _path = path;
            _pathIndex = 0;
            CurrentHealth = data.Health;
            Armor = data.Armor;
            MoveSpeed = data.MoveSpeed;

            BuildVisual();
            AddToGroup(Constants.GROUP_ENEMY);

            if (_path != null && _path.Count > 0)
            {
                _grid = ServiceLocator.Get<MapGrid>();
                GlobalPosition = _grid.GridToWorld(_path[0]) + new Vector3(0, data.IsFlying ? 2f : 0.3f, 0);
            }
        }

        private void BuildVisual()
        {
            _mesh = new MeshInstance3D();

            if (Data.IsFlying)
            {
                var sphere = new SphereMesh();
                sphere.Radius = Constants.CELL_SIZE * 0.25f * Data.Scale;
                sphere.Height = Constants.CELL_SIZE * 0.5f * Data.Scale;
                _mesh.Mesh = sphere;
            }
            else
            {
                var box = new BoxMesh();
                float s = Constants.CELL_SIZE * 0.3f * Data.Scale;
                box.Size = new Vector3(s, s * 1.5f, s);
                _mesh.Mesh = box;
            }

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = Data.TintColor;
            mat.Roughness = 0.8f;
            mat.Metallic = 0.3f;
            _mesh.MaterialOverride = mat;
            AddChild(_mesh);

            // Health bar
            _healthBar = new MeshInstance3D();
            var barMesh = new BoxMesh();
            barMesh.Size = new Vector3(0.8f, 0.08f, 0.08f);
            _healthBar.Mesh = barMesh;
            _healthBar.Position = new Vector3(0, Constants.CELL_SIZE * 0.4f * Data.Scale + 0.3f, 0);
            var barMat = new StandardMaterial3D();
            barMat.AlbedoColor = new Color(0.1f, 0.9f, 0.1f);
            barMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _healthBar.MaterialOverride = barMat;
            AddChild(_healthBar);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsAlive || _path == null || _pathIndex >= _path.Count) return;

            // Burn tick
            if (_burnTimer > 0)
            {
                _burnTimer -= (float)delta;
                CurrentHealth -= _burnDamage * (float)delta;
                if (!IsAlive) { Die(); return; }
            }

            // Movement
            float speed = MoveSpeed;
            if (_slowTimer > 0)
            {
                speed *= (1f - _slowAmount);
                _slowTimer -= (float)delta;
            }

            var targetPos = _grid.GridToWorld(_path[_pathIndex]) + new Vector3(0, Data.IsFlying ? 2f : 0.3f, 0);
            var dir = (targetPos - GlobalPosition);
            dir.Y = 0;
            float dist = dir.Length();

            if (dist < 0.15f)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    ReachCore();
                    return;
                }
            }
            else
            {
                GlobalPosition += dir.Normalized() * speed * (float)delta;

                // Face movement direction
                if (dir.LengthSquared() > 0.001f)
                {
                    float angle = Mathf.Atan2(dir.X, dir.Z);
                    _mesh.Rotation = new Vector3(0, angle, 0);
                }
            }

            UpdateHealthBar();
        }

        public void TakeDamage(DamageInfo damage)
        {
            if (!IsAlive) return;

            // Apply armor reduction: damage * 100 / (100 + armor)
            float totalArmor = Armor + ScrapArmor;
            float reduction = totalArmor / (totalArmor + 100f);
            damage.FinalDamage = damage.RawDamage * (1f - reduction);

            CurrentHealth -= damage.FinalDamage;

            // Apply status effects
            if (damage.SlowAmount > 0)
            {
                _slowAmount = Mathf.Max(_slowAmount, damage.SlowAmount);
                _slowTimer = Mathf.Max(_slowTimer, damage.SlowDuration);
            }
            if (damage.BurnDamage > 0)
            {
                _burnDamage = damage.BurnDamage;
                _burnTimer = damage.BurnDuration;
            }

            GameEvents.OnDamageDealt?.Invoke(damage);

            if (!IsAlive)
                Die();
        }

        public void ApplyScrapArmor(float amount)
        {
            ScrapArmor += amount;
            // Visual feedback — darken tint slightly
            if (_mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = Data.TintColor.Darkened(0.2f);
                mat.Metallic = 0.6f;
            }
        }

        private void Die()
        {
            // Drop scrap at death position
            GameEvents.OnScrapDropped?.Invoke(GlobalPosition, Data.ScrapValue);
            GameEvents.OnEnemyKilled?.Invoke(this);
            QueueFree();
        }

        private void ReachCore()
        {
            GameEvents.OnEnemyLeaked?.Invoke(this, GlobalPosition);
            GameManager.Instance?.OnEnemyReachedCore();
            QueueFree();
        }

        private void UpdateHealthBar()
        {
            if (_healthBar == null || Data == null) return;
            float pct = Mathf.Clamp(CurrentHealth / Data.Health, 0f, 1f);
            _healthBar.Scale = new Vector3(pct, 1, 1);
            _healthBar.Position = new Vector3((pct - 1f) * 0.4f, _healthBar.Position.Y, 0);

            if (_healthBar.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = pct > 0.5f
                    ? new Color(0.1f, 0.9f, 0.1f)
                    : pct > 0.25f
                        ? new Color(0.9f, 0.7f, 0.1f)
                        : new Color(0.9f, 0.1f, 0.1f);
        }
    }
}
