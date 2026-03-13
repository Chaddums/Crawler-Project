using Godot;

namespace JunkbotArena
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
        private BossAI _bossAI;
        private StatusEffectManager _statusEffects;

        private EnemyData _data;
        private StatBlock _stats;
        private Node3D _bodyRoot;
        private CharacterAnimator _characterAnimator;
        private ProceduralAnimator _proceduralAnimator;
        private IAnimatable _animatable;
        private EnemyHealthBar3D _healthBar3D;

        public HealthComponent Health => _health;
        public EnemyAI AI => _ai;
        public BossAI BossAI => _bossAI;
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

        private bool _isBoss;
        private bool _isDead;

        /// <summary>
        /// Initialize this enemy from data. Call after instantiation.
        /// </summary>
        public void Initialize(EnemyData data, float difficultyMultiplier = 1f)
        {
            _data = data;
            _isBoss = data.IsBoss;
            _stats = data.BuildStats(difficultyMultiplier);

            float maxHp = _stats.GetStat(StatType.MaxHealth);
            _health.SetMaxHealth(maxHp, true);
            _health.SetTeam(Team.Enemy);

            if (_isBoss)
            {
                // Boss uses BossAI instead of EnemyAI — disable normal AI and combat
                _ai.SetPhysicsProcess(false);
                _ai.SetProcess(false);
                _combat.SetProcess(false);

                _bossAI = new BossAI();
                _bossAI.Name = "BossAI";
                AddChild(_bossAI);
                _bossAI.Initialize(data, _stats, _health);
            }
            else
            {
                _ai.Initialize(data, _stats);
                _combat.Initialize(data, _stats, this);
            }

            _statusEffects?.Initialize(_stats, _health);

            // Replace capsule mesh with procedural enemy body
            var oldMesh = GetNodeOrNull<MeshInstance3D>("EnemyMesh");
            oldMesh?.QueueFree();

            _bodyRoot = CharacterMeshBuilder.BuildEnemyBody(data.Id);
            AddChild(_bodyRoot);

            // Apply configured mesh color to ensure models aren't white/default
            ApplyMeshColor(_bodyRoot, data.MeshColor);

            // If the loaded model has an AnimationPlayer, strip root motion and wire up
            var animPlayer = CharacterMeshBuilder.FindAnimationPlayer(_bodyRoot);
            if (animPlayer != null)
            {
                StripRootMotionTracks(animPlayer);
                _characterAnimator = new CharacterAnimator();
                _characterAnimator.Name = "CharacterAnimator";
                AddChild(_characterAnimator);
                _characterAnimator.Initialize(_bodyRoot);
                _animatable = _characterAnimator;
                GD.Print($"[EnemyController] {data.Id}: CharacterAnimator wired — anims: {string.Join(", ", animPlayer.GetAnimationList())}");
            }
            else
            {
                // No skeletal animations — use ProceduralAnimator for limb-based animation
                GD.Print($"[EnemyController] {data.Id}: No AnimationPlayer, using ProceduralAnimator");
                _proceduralAnimator = new ProceduralAnimator();
                _proceduralAnimator.Name = "ProceduralAnimator";
                AddChild(_proceduralAnimator);
                _proceduralAnimator.Initialize(_bodyRoot);
                _animatable = _proceduralAnimator;
            }

            // Debug name tag (toggle with ~ → names)
            if (DebugMenu.ShowNames)
                DebugMenu.AddNameTag(this, data.Id);

            // Now that the animator is ready, tell BossAI so its first SetState(Intro) works
            if (_isBoss && _bossAI != null)
                _bossAI.SetAnimatable(_animatable);

            if (_isBoss)
                GameEvents.OnBossSpawned?.Invoke(this);

            // World-space health bar above head
            _healthBar3D = new EnemyHealthBar3D();
            _healthBar3D.Name = "EnemyHealthBar3D";
            float modelHeight = CharacterMeshBuilder.GetEnemyModelHeight(data.Id);
            float barHeight = modelHeight + 0.3f;
            _healthBar3D.Position = new Vector3(0, barHeight, 0);
            AddChild(_healthBar3D);
            _health.OnHealthChanged += OnHealthChangedUpdateBar;
        }

        private void OnHealthChangedUpdateBar(float current, float max)
        {
            _healthBar3D?.UpdateHealth(current, max);
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
                    if (GodotObject.IsInstanceValid(this) && IsInsideTree()
                        && GodotObject.IsInstanceValid(mesh) && mesh.IsInsideTree())
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
            if (_isDead) return; // Guard against double-death
            _isDead = true;

            GD.Print($"[EnemyController] HandleDeath fired for {_data?.EnemyName ?? "unknown"} (boss={_isBoss})");

            if (_isBoss)
                _bossAI?.SetDeadState();
            else
                _ai.SetState(EnemyAI.State.Dead);

            _animatable?.SetState(AnimState.Death);

            // Stop physics immediately so dead enemies stay put
            _ai?.SetPhysicsProcess(false);
            _bossAI?.SetPhysicsProcess(false);
            SetPhysicsProcess(false);

            // Award XP
            if (_data != null)
            {
                GameEvents.OnExperienceGained?.Invoke(_data.XpReward);
                GD.Print($"[Enemy] {_data.EnemyName} killed! +{_data.XpReward} XP");
            }

            GD.Print($"[EnemyController] Firing OnEnemyKilled for {_data?.EnemyName ?? "unknown"} (handler null={GameEvents.OnEnemyKilled == null})");
            GameEvents.OnEnemyKilled?.Invoke(this);

            if (_isBoss)
                GameEvents.OnBossDefeated?.Invoke(this);

            // Capture position before particles (node is still in tree here)
            var deathPos = GlobalPosition;

            // Death particles
            var deathColor = _data?.MeshColor ?? new Color(0.8f, 0.2f, 0.2f);
            var deathParticles = VfxFactory.CreateDeathParticles(deathColor);
            GetTree().Root.AddChild(deathParticles);
            deathParticles.GlobalPosition = deathPos + Vector3.Up * 0.6f;

            // Boss gets extra celebration particles
            if (_isBoss)
            {
                var celebration = VfxFactory.CreateCelebrationParticles();
                GetTree().Root.AddChild(celebration);
                celebration.GlobalPosition = deathPos + Vector3.Up * 1f;
            }

            // CoreScavenger (Carrion Beetle Colony): +100% item find
            bool hasScavenger = false;
            if (PlayerManager.PlayerCount > 0)
            {
                var p1 = PlayerManager.Players[0];
                hasScavenger = p1?.PerkProcessor != null
                    && p1.ClassController?.HasPerk(Perks.CoreScavenger) == true;
            }
            float itemFindMult = hasScavenger ? 2f : 1f;

            // Signature drop — each player rolls independently
            bool droppedSignature = false;
            if (_data?.SignatureDrop != null && _data.SignatureDropChance > 0f)
            {
                var dropRarity = _data.Tier switch
                {
                    EnemyTier.Elite => ItemRarity.Rare,
                    EnemyTier.MiniBoss => ItemRarity.Epic,
                    _ => ItemRarity.Uncommon
                };

                int playerCount = PlayerManager.PlayerCount;
                for (int pi = 0; pi < Mathf.Max(1, playerCount); pi++)
                {
                    float roll = (float)GD.Randf();
                    if (roll <= _data.SignatureDropChance * itemFindMult)
                    {
                        var sigItem = new ItemInstance(_data.SignatureDrop, dropRarity);
                        // Offset each drop slightly so they don't stack
                        var spawnPos = deathPos + new Vector3(pi * 0.8f, 0, 1f);
                        var tree = GetTree();
                        var root = tree.Root;
                        tree.CreateTimer(0.1f).Timeout += () =>
                        {
                            if (GodotObject.IsInstanceValid(root))
                                ItemPickup.SpawnAt(root, spawnPos, sigItem);
                        };
                        droppedSignature = true;
                        GD.Print($"[EnemyController] {_data.EnemyName} dropped signature item for P{pi + 1}: {sigItem.GetDisplayName()} ({dropRarity})");
                    }
                }
            }

            // Loot box contribution — each player rolls independently
            if (_data?.LootBoxDrop != null && _data.LootBoxDropChance > 0f)
            {
                int playerCount = PlayerManager.PlayerCount;
                for (int pi = 0; pi < Mathf.Max(1, playerCount); pi++)
                {
                    float boxRoll = (float)GD.Randf();
                    if (boxRoll <= _data.LootBoxDropChance * itemFindMult)
                    {
                        var lootBox = LootBoxFactory.CreateLootBox(_data.LootBoxDrop.Value);
                        if (lootBox != null)
                        {
                            AchievementManager.PendingLootBoxes.Enqueue(lootBox);
                            GD.Print($"[EnemyController] {_data.EnemyName} contributed {_data.LootBoxDrop.Value} loot box for P{pi + 1} to pending pool");
                        }
                    }
                }
            }

            // CoreScavenger: bonus drop — extra random equipment on kill
            if (hasScavenger && GD.Randf() < 0.25f)
            {
                var bonusRarity = _data.Tier switch
                {
                    EnemyTier.Boss => ItemRarity.Epic,
                    EnemyTier.MiniBoss => ItemRarity.Rare,
                    EnemyTier.Elite => ItemRarity.Uncommon,
                    _ => ItemRarity.Common
                };
                var templates = BaseItemPool.Equipment;
                if (templates.Count > 0)
                {
                    var baseData = templates[(int)(GD.Randf() * templates.Count) % templates.Count];
                    var bonusItem = new ItemInstance(baseData, bonusRarity);
                    var bonusPos = deathPos + new Vector3(0.5f, 0, -0.5f);
                    var tree2 = GetTree();
                    var root2 = tree2.Root;
                    tree2.CreateTimer(0.15f).Timeout += () =>
                    {
                        if (GodotObject.IsInstanceValid(root2))
                            ItemPickup.SpawnAt(root2, bonusPos, bonusItem);
                    };
                    droppedSignature = true; // Trigger loot burst VFX
                }
            }

            // Loot burst particles only if something dropped
            if (droppedSignature)
            {
                var lootBurst = VfxFactory.CreateLootBurstParticles(new Color(1f, 0.85f, 0.3f));
                GetTree().Root.AddChild(lootBurst);
                lootBurst.GlobalPosition = deathPos + Vector3.Up * 0.4f;
            }

            // Death flash + scale down + remove
            if (_bodyRoot != null)
                FlashMeshRecursive(_bodyRoot);

            var tween = CreateTween();
            if (tween != null)
            {
                tween.TweenProperty(this, "scale", Vector3.One * 0.01f, _isBoss ? 0.8f : 0.4f)
                    .SetTrans(Tween.TransitionType.Back)
                    .SetEase(Tween.EaseType.In);
                tween.TweenCallback(Callable.From(QueueFree));
            }
            else
            {
                // Fallback: just remove immediately
                QueueFree();
            }
        }

        private static void StripRootMotionTracks(AnimationPlayer animPlayer)
        {
            foreach (var animName in animPlayer.GetAnimationList())
            {
                var anim = animPlayer.GetAnimation(animName);
                if (anim == null) continue;
                for (int t = anim.GetTrackCount() - 1; t >= 0; t--)
                {
                    string path = anim.TrackGetPath(t).ToString();
                    if (path.StartsWith(".:position") || path.StartsWith(".:rotation") ||
                        path.StartsWith(".:transform") || path == ".")
                    {
                        var trackType = anim.TrackGetType(t);
                        if (trackType == Animation.TrackType.Position3D ||
                            trackType == Animation.TrackType.Rotation3D ||
                            trackType == Animation.TrackType.Scale3D)
                        {
                            anim.RemoveTrack(t);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Apply the enemy's configured MeshColor to any MeshInstance3D that has
        /// no material or a default white material. Preserves existing authored materials
        /// including surface_material_overrides from .tscn scenes (FBX imports).
        /// </summary>
        private static void ApplyMeshColor(Node node, Color color)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride == null && mesh.Mesh != null)
            {
                bool hasAuthoredMaterial = false;
                for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                {
                    // Check per-surface override first (set by .tscn scene files for FBX imports)
                    var overrideMat = mesh.GetSurfaceOverrideMaterial(i);
                    if (overrideMat != null)
                    {
                        hasAuthoredMaterial = true;
                        break;
                    }

                    // Then check mesh's built-in material
                    var surfMat = mesh.Mesh.SurfaceGetMaterial(i);
                    if (surfMat is StandardMaterial3D stdMat)
                    {
                        // Has texture OR non-white color → authored material
                        if (stdMat.AlbedoTexture != null || stdMat.AlbedoColor != Colors.White)
                        {
                            hasAuthoredMaterial = true;
                            break;
                        }
                    }
                    else if (surfMat != null)
                    {
                        // Non-StandardMaterial3D (ShaderMaterial, etc.) → authored
                        hasAuthoredMaterial = true;
                        break;
                    }
                }

                if (!hasAuthoredMaterial)
                {
                    var mat = new StandardMaterial3D();
                    mat.AlbedoColor = color;
                    mat.Metallic = 0.3f;
                    mat.Roughness = 0.6f;
                    mesh.MaterialOverride = mat;
                }
            }

            foreach (var child in node.GetChildren())
                if (child is Node n) ApplyMeshColor(n, color);
        }

        public override void _ExitTree()
        {
            _health.OnDeath -= HandleDeath;
            _health.OnHealthChanged -= OnHealthChangedUpdateBar;
        }
    }
}
