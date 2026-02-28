using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Root CharacterBody3D for companions. Composes health, AI, combat.
    /// </summary>
    public partial class CompanionController : CharacterBody3D
    {
        private HealthComponent _health;
        private CompanionAI _ai;
        private CompanionCombat _combat;
        private StatusEffectManager _statusEffects;

        private CompanionData _data;
        private StatBlock _stats;

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

            // Set mesh color and scale
            var mesh = GetNodeOrNull<MeshInstance3D>("CompanionMesh");
            if (mesh != null)
            {
                if (mesh.GetActiveMaterial(0) is StandardMaterial3D mat)
                {
                    var newMat = (StandardMaterial3D)mat.Duplicate();
                    newMat.AlbedoColor = data.MeshColor;
                    mesh.SetSurfaceOverrideMaterial(0, newMat);
                }
                mesh.Scale = data.MeshScale;
            }

            GD.Print($"[CompanionController] {data.CompanionName} initialized");
        }

        private void FlashDamage()
        {
            var mesh = GetNodeOrNull<MeshInstance3D>("CompanionMesh");
            if (mesh == null) return;

            var flashMat = new StandardMaterial3D();
            flashMat.AlbedoColor = new Color(1f, 0.3f, 0.3f);
            flashMat.EmissionEnabled = true;
            flashMat.Emission = new Color(1f, 0.1f, 0.1f);

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
            _ai.SetState(CompanionAI.State.Dead);
            GD.Print($"[CompanionController] {_data?.CompanionName} has fallen!");

            // Death flash + shrink
            var mesh = GetNodeOrNull<MeshInstance3D>("CompanionMesh");
            if (mesh != null)
            {
                var deathMat = new StandardMaterial3D();
                deathMat.AlbedoColor = new Color(0.8f, 0.8f, 1f);
                deathMat.EmissionEnabled = true;
                deathMat.Emission = new Color(0.5f, 0.5f, 1f);
                deathMat.EmissionEnergyMultiplier = 2f;
                mesh.SetSurfaceOverrideMaterial(0, deathMat);
            }

            var tween = CreateTween();
            tween.TweenProperty(this, "scale", Vector3.Zero, 0.5f)
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
