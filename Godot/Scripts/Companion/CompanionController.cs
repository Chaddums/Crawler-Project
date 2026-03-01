using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Root CharacterBody3D for companions. Composes health, AI, combat.
    /// Uses procedural body from CharacterMeshBuilder.
    /// </summary>
    public partial class CompanionController : CharacterBody3D
    {
        private HealthComponent _health;
        private CompanionAI _ai;
        private CompanionCombat _combat;
        private StatusEffectManager _statusEffects;

        private CompanionData _data;
        private StatBlock _stats;
        private Node3D _bodyRoot;

        public HealthComponent Health => _health;
        public CompanionAI AI => _ai;
        public StatBlock Stats => _stats;
        public CompanionData Data => _data;

        public override void _Ready()
        {
            _health = GetNode<HealthComponent>("HealthComponent");
            _ai = GetNode<CompanionAI>("CompanionAI");
            _combat = GetNode<CompanionCombat>("CompanionCombat");
            _statusEffects = GetNodeOrNull<StatusEffectManager>("StatusEffectManager");

            _health.OnDeath += HandleDeath;
            _health.OnDamaged += _ => FlashDamage();
            AddToGroup(Constants.GROUP_COMPANION);
        }

        public void Initialize(CompanionData data)
        {
            _data = data;
            _stats = data.BuildStats();

            float maxHp = _stats.GetStat(StatType.MaxHealth);
            _health.SetMaxHealth(maxHp, true);
            _health.SetTeam(Team.Player);

            _ai.Initialize(data, _stats);
            _combat.Initialize(data, _stats, this);
            _statusEffects?.Initialize(_stats, _health);

            // Replace placeholder capsule mesh with procedural companion body
            var oldMesh = GetNodeOrNull<MeshInstance3D>("CompanionMesh");
            oldMesh?.QueueFree();

            _bodyRoot = CharacterMeshBuilder.BuildCompanionBody(data.Id);
            _bodyRoot.Scale = data.MeshScale;
            AddChild(_bodyRoot);

            GD.Print($"[CompanionController] {data.CompanionName} initialized with procedural body");
        }

        private void FlashDamage()
        {
            if (_bodyRoot == null) return;
            FlashMeshRecursive(_bodyRoot);
        }

        private void FlashMeshRecursive(Node node)
        {
            if (node is MeshInstance3D mesh)
            {
                var originalMat = mesh.MaterialOverride as StandardMaterial3D;

                var flashMat = new StandardMaterial3D();
                flashMat.AlbedoColor = new Color(1f, 0.3f, 0.3f);
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
            _ai.SetState(CompanionAI.State.Dead);
            GD.Print($"[CompanionController] {_data?.CompanionName} has fallen!");

            // Death flash
            if (_bodyRoot != null)
                FlashMeshRecursive(_bodyRoot);

            var tween = CreateTween();
            tween.TweenProperty(this, "scale", Vector3.One * 0.01f, 0.5f)
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
