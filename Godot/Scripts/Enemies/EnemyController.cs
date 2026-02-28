using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Root CharacterBody3D for enemies. Composes health, AI, combat.
    /// Initializes from EnemyData with difficulty scaling.
    /// </summary>
    public partial class EnemyController : CharacterBody3D
    {
        private HealthComponent _health;
        private EnemyAI _ai;
        private EnemyCombat _combat;
        private StatusEffectManager _statusEffects;

        private EnemyData _data;
        private StatBlock _stats;

        public HealthComponent Health => _health;
        public EnemyAI AI => _ai;
        public EnemyCombat Combat => _combat;
        public StatBlock Stats => _stats;
        public EnemyData Data => _data;

        public override void _Ready()
        {
            _health = GetNode<HealthComponent>("HealthComponent");
            _ai = GetNode<EnemyAI>("EnemyAI");
            _combat = GetNode<EnemyCombat>("EnemyCombat");
            _statusEffects = GetNodeOrNull<StatusEffectManager>("StatusEffectManager");

            _health.OnDeath += HandleDeath;
            _health.OnDamaged += _ => FlashDamage();
            AddToGroup(Constants.GROUP_ENEMY);
        }

        /// <summary>
        /// Initialize this enemy from data. Call after instantiation.
        /// </summary>
        public void Initialize(EnemyData data, float difficultyMultiplier = 1f)
        {
            _data = data;
            _stats = data.BuildStats(difficultyMultiplier);

            float maxHp = _stats.GetStat(StatType.MaxHealth);
            _health.SetMaxHealth(maxHp, true);
            _health.SetTeam(Team.Enemy);

            _ai.Initialize(data, _stats);
            _combat.Initialize(data, _stats, this);
            _statusEffects?.Initialize(_stats, _health);

            // Set mesh color
            var mesh = GetNodeOrNull<MeshInstance3D>("EnemyMesh");
            if (mesh?.GetActiveMaterial(0) is StandardMaterial3D mat)
            {
                var newMat = (StandardMaterial3D)mat.Duplicate();
                newMat.AlbedoColor = data.MeshColor;
                mesh.SetSurfaceOverrideMaterial(0, newMat);
            }
        }

        public void FlashDamage()
        {
            var mesh = GetNodeOrNull<MeshInstance3D>("EnemyMesh");
            if (mesh == null) return;

            var flashMat = new StandardMaterial3D();
            flashMat.AlbedoColor = new Color(1f, 0.2f, 0.2f);
            flashMat.EmissionEnabled = true;
            flashMat.Emission = new Color(1f, 0.1f, 0.1f);
            flashMat.EmissionEnergyMultiplier = 2f;

            var originalMat = mesh.GetSurfaceOverrideMaterial(0) ?? mesh.GetActiveMaterial(0);
            mesh.SetSurfaceOverrideMaterial(0, flashMat);

            var timer = GetTree().CreateTimer(0.12f);
            timer.Timeout += () =>
            {
                if (IsInsideTree() && mesh.IsInsideTree())
                    mesh.SetSurfaceOverrideMaterial(0, originalMat as StandardMaterial3D);
            };
        }

        private void HandleDeath()
        {
            _ai.SetState(EnemyAI.State.Dead);

            // Award XP
            if (_data != null)
            {
                GameEvents.OnExperienceGained?.Invoke(_data.XpReward);
                GD.Print($"[Enemy] {_data.EnemyName} killed! +{_data.XpReward} XP");
            }

            GameEvents.OnEnemyKilled?.Invoke(this);

            // Drop loot
            if (_data?.LootTable != null)
            {
                var items = LootTableResolver.Resolve(_data.LootTable);
                foreach (var item in items)
                {
                    ItemPickup.SpawnAt(GetTree().Root, GlobalPosition, item);
                }
            }

            // Death flash + scale down + remove
            var mesh = GetNodeOrNull<MeshInstance3D>("EnemyMesh");
            if (mesh != null)
            {
                var deathMat = new StandardMaterial3D();
                deathMat.AlbedoColor = new Color(1f, 0.3f, 0.1f);
                deathMat.EmissionEnabled = true;
                deathMat.Emission = new Color(1f, 0.2f, 0.05f);
                deathMat.EmissionEnergyMultiplier = 3f;
                mesh.SetSurfaceOverrideMaterial(0, deathMat);
            }

            var tween = CreateTween();
            tween.TweenProperty(this, "scale", Vector3.Zero, 0.4f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(QueueFree));
        }

        public override void _ExitTree()
        {
            _health.OnDeath -= HandleDeath;
        }
    }
}
