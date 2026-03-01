using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Deals periodic damage to bodies overlapping the parent Area3D.
    /// Attach as child of an Area3D hazard zone. Hurts both players and enemies.
    /// </summary>
    public partial class HazardDamager : Node
    {
        public float DamagePerSecond { get; set; } = 2f;
        public DamageType DamageType { get; set; } = DamageType.Poison;
        public float StunDuration { get; set; } = 0f;

        /// <summary>
        /// If > 0, hazard toggles active/inactive on this interval (seconds).
        /// </summary>
        public float ToggleInterval { get; set; } = 0f;

        private Area3D _area;
        private readonly HashSet<Node3D> _overlapping = new();
        private float _damageTimer;
        private float _toggleTimer;
        private bool _active = true;
        private MeshInstance3D _visualMesh;

        public override void _Ready()
        {
            _area = GetParentOrNull<Area3D>();
            if (_area == null) return;

            _area.BodyEntered += OnBodyEntered;
            _area.BodyExited += OnBodyExited;

            // Cache visual mesh for toggle effect
            foreach (var child in _area.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                {
                    _visualMesh = mesh;
                    break;
                }
            }
        }

        public override void _ExitTree()
        {
            if (_area != null)
            {
                _area.BodyEntered -= OnBodyEntered;
                _area.BodyExited -= OnBodyExited;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Handle toggle
            if (ToggleInterval > 0)
            {
                _toggleTimer += dt;
                if (_toggleTimer >= ToggleInterval)
                {
                    _toggleTimer = 0;
                    _active = !_active;

                    // Visual feedback for toggle
                    if (_visualMesh != null)
                    {
                        var mat = _visualMesh.MaterialOverride as StandardMaterial3D;
                        if (mat != null)
                            mat.EmissionEnergyMultiplier = _active ? 1.5f : 0.1f;
                    }
                }
            }

            if (!_active || _overlapping.Count == 0) return;

            _damageTimer += dt;
            if (_damageTimer < 0.5f) return; // Tick every 0.5s
            _damageTimer = 0;

            float tickDamage = DamagePerSecond * 0.5f;
            // Copy to avoid modification during iteration
            var bodies = new List<Node3D>(_overlapping);

            foreach (var body in bodies)
            {
                if (!IsInstanceValid(body)) { _overlapping.Remove(body); continue; }

                var health = body.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (health == null || !health.IsAlive) continue;

                var info = new DamageInfo
                {
                    RawDamage = tickDamage,
                    FinalDamage = tickDamage,
                    DamageType = DamageType,
                    HitPoint = body.GlobalPosition,
                    StunDuration = StunDuration
                };
                health.TakeDamage(info);

                if (StunDuration > 0 && body is IKnockbackable kb)
                    kb.ApplyStun(StunDuration);
            }
        }

        private void OnBodyEntered(Node3D body)
        {
            _overlapping.Add(body);
        }

        private void OnBodyExited(Node3D body)
        {
            _overlapping.Remove(body);
        }
    }
}
