using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Root CharacterBody3D for enemies. Composes health, AI, combat.
    /// Initializes from EnemyData with difficulty scaling.
    /// Uses procedural multi-part body mesh per enemy type.
    /// </summary>
    public partial class EnemyController : CharacterBody3D
    {
        private HealthComponent _health;
        private EnemyAI _ai;
        private EnemyCombat _combat;
        private StatusEffectManager _statusEffects;

        private EnemyData _data;
        private StatBlock _stats;
        private Node3D _bodyRoot;
        private CharacterAnimator _characterAnimator;
        private ProceduralAnimator _proceduralAnimator;
        private IAnimatable _animatable;

        public HealthComponent Health => _health;
        public EnemyAI AI => _ai;
        public EnemyCombat Combat => _combat;
        public StatBlock Stats => _stats;
        public EnemyData Data => _data;
        public IAnimatable Animatable => _animatable;

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

            // Replace capsule mesh with procedural enemy body
            var oldMesh = GetNodeOrNull<MeshInstance3D>("EnemyMesh");
            oldMesh?.QueueFree();

            _bodyRoot = CharacterMeshBuilder.BuildEnemyBody(data.Id);
            AddChild(_bodyRoot);

            // If the loaded model has an AnimationPlayer, wire up CharacterAnimator
            var animPlayer = CharacterMeshBuilder.FindAnimationPlayer(_bodyRoot);
            if (animPlayer != null)
            {
                _characterAnimator = new CharacterAnimator();
                _characterAnimator.Name = "CharacterAnimator";
                AddChild(_characterAnimator);
                _characterAnimator.Initialize(_bodyRoot);
                _animatable = _characterAnimator;
            }
            else
            {
                // No skeletal animations — use ProceduralAnimator for limb-based animation
                _proceduralAnimator = new ProceduralAnimator();
                _proceduralAnimator.Name = "ProceduralAnimator";
                AddChild(_proceduralAnimator);
                _proceduralAnimator.Initialize(_bodyRoot);
                _animatable = _proceduralAnimator;
            }
        }

        public void FlashDamage()
        {
            if (_bodyRoot == null) return;

            _animatable?.SetState(AnimState.Hit);

            // Flash all mesh children recursively
            FlashMeshRecursive(_bodyRoot);
        }

        private void FlashMeshRecursive(Node node)
        {
            if (node is MeshInstance3D mesh)
            {
                var originalMat = mesh.MaterialOverride as StandardMaterial3D;

                var flashMat = new StandardMaterial3D();
                flashMat.AlbedoColor = new Color(1f, 0.2f, 0.2f);
                flashMat.EmissionEnabled = true;
                flashMat.Emission = new Color(1f, 0.1f, 0.1f);
                flashMat.EmissionEnergyMultiplier = 2f;
                mesh.MaterialOverride = flashMat;

                var timer = GetTree().CreateTimer(0.12f);
                timer.Timeout += () =>
                {
                    if (IsInsideTree() && GodotObject.IsInstanceValid(mesh) && mesh.IsInsideTree())
                        mesh.MaterialOverride = originalMat;
                };
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    FlashMeshRecursive(childNode);
            }
        }

        private void HandleDeath()
        {
            _ai.SetState(EnemyAI.State.Dead);
            _animatable?.SetState(AnimState.Death);

            // Award XP
            if (_data != null)
            {
                GameEvents.OnExperienceGained?.Invoke(_data.XpReward);
                GD.Print($"[Enemy] {_data.EnemyName} killed! +{_data.XpReward} XP");
            }

            GameEvents.OnEnemyKilled?.Invoke(this);

            // Death particles
            var deathColor = _data?.MeshColor ?? new Color(0.8f, 0.2f, 0.2f);
            var deathParticles = VfxFactory.CreateDeathParticles(deathColor);
            deathParticles.GlobalPosition = GlobalPosition + Vector3.Up * 0.6f;
            GetTree().Root.AddChild(deathParticles);

            // Loot burst particles before items
            var lootBurst = VfxFactory.CreateLootBurstParticles(new Color(1f, 0.85f, 0.3f));
            lootBurst.GlobalPosition = GlobalPosition + Vector3.Up * 0.4f;
            GetTree().Root.AddChild(lootBurst);

            // Drop loot with staggered angular offsets
            if (_data?.LootTable != null)
            {
                var items = LootTableResolver.Resolve(_data.LootTable);
                for (int i = 0; i < items.Count; i++)
                {
                    float angle = (float)i / Mathf.Max(1, items.Count) * Mathf.Tau;
                    float dist = 1.5f;
                    var offset = new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                    var spawnPos = GlobalPosition + offset;

                    // Stagger spawn timing
                    int index = i;
                    var item = items[i];
                    GetTree().CreateTimer(index * 0.08f).Timeout += () =>
                    {
                        if (IsInsideTree())
                            ItemPickup.SpawnAt(GetTree().Root, spawnPos, item);
                    };
                }
            }

            // Death flash + scale down + remove
            if (_bodyRoot != null)
                FlashMeshRecursive(_bodyRoot);

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
