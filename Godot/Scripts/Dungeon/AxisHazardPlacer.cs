using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Places AXIS-controlled weapon hazards in combat rooms.
    /// Hazards are KitBash3D weapon models that periodically fire projectiles.
    /// Higher sectors get more dangerous and more frequent hazards.
    /// </summary>
    public static class AxisHazardPlacer
    {
        // Hazard configs per sector — which weapon models AXIS deploys
        private static readonly Dictionary<int, string[]> SectorHazards = new()
        {
            { 1, new[] { "axis_turret" } },                                          // Industrial: basic turrets only
            { 2, new[] { "axis_turret", "axis_turret_2" } },                         // Toxic: turret variants
            { 3, new[] { "axis_turret", "axis_turret_2", "axis_turret_3", "axis_weapon" } }, // Military: full turret arsenal
            { 4, new[] { "axis_turret_2", "axis_turret_3", "axis_weapon", "axis_weapon_2" } }, // Lab: advanced weapons
            { 5, new[] { "axis_plasma_gun", "axis_rocket_launcher", "axis_turret_3", "axis_weapon_2" } }, // Core: heavy weapons
        };

        // Chance of a combat room getting hazards, per sector
        private static readonly Dictionary<int, float> HazardChance = new()
        {
            { 1, 0.10f }, // 10% of combat rooms in sector 1
            { 2, 0.15f },
            { 3, 0.25f },
            { 4, 0.35f },
            { 5, 0.50f }, // Half of all combat rooms in the Core
        };

        /// <summary>
        /// Potentially place AXIS weapon hazards in a combat room.
        /// Call from RoomBuilder after room construction.
        /// </summary>
        public static void TryPlaceHazards(Node3D room, Vector2 roomSize, int sectorNumber,
            bool doorN, bool doorS, bool doorE, bool doorW)
        {
            if (sectorNumber <= 0) return;
            if (!SectorHazards.TryGetValue(sectorNumber, out var hazardIds)) return;
            if (!HazardChance.TryGetValue(sectorNumber, out float chance)) return;

            var rng = new RandomNumberGenerator();
            rng.Randomize();

            if (rng.Randf() > chance) return;

            float halfW = roomSize.X / 2f;
            float halfH = roomSize.Y / 2f;
            float doorClearance = 6f;

            // Place 1-2 hazards per room (more in higher sectors)
            int hazardCount = sectorNumber >= 4 ? rng.RandiRange(1, 2) : 1;

            for (int i = 0; i < hazardCount; i++)
            {
                string hazardId = hazardIds[rng.RandiRange(0, hazardIds.Length - 1)];
                var model = ModelLibrary.TryLoad("hazard", hazardId);
                if (model == null) continue;

                // Bug #5: Apply fallback materials to GLB turrets that have no textures
                CharacterMeshBuilder.ApplyFallbackMaterialIfNeeded(model, "hazard");

                // Place hazards along walls (they're mounted/stationed against walls)
                int wall = rng.RandiRange(0, 3);
                float x, z;
                float wallInset = 1.5f;

                switch (wall)
                {
                    case 0: // North wall
                        x = rng.RandfRange(-halfW * 0.5f, halfW * 0.5f);
                        z = -halfH + wallInset;
                        break;
                    case 1: // South wall
                        x = rng.RandfRange(-halfW * 0.5f, halfW * 0.5f);
                        z = halfH - wallInset;
                        break;
                    case 2: // East wall
                        x = halfW - wallInset;
                        z = rng.RandfRange(-halfH * 0.5f, halfH * 0.5f);
                        break;
                    default: // West wall
                        x = -halfW + wallInset;
                        z = rng.RandfRange(-halfH * 0.5f, halfH * 0.5f);
                        break;
                }

                // Skip if near a door
                if (IsNearDoor(x, z, halfW, halfH, doorClearance, doorN, doorS, doorE, doorW))
                    continue;

                model.Name = $"AXIS_Hazard_{hazardId}_{i}";
                RoomBuilder.ScaleModelToFitEffective(model, rng.RandfRange(1.5f, 2.5f));
                model.Position = new Vector3(x, 0, z);

                // Face toward room center
                float angleToCenter = Mathf.Atan2(-x, -z);
                model.RotationDegrees = new Vector3(0, Mathf.RadToDeg(angleToCenter), 0);

                room.AddChild(model);
                RoomBuilder.GroundModel(model);

                // Add hazard behavior (periodic projectile fire)
                var hazard = new AxisHazardBehavior();
                hazard.DamagePerShot = 5f + sectorNumber * 3f;
                hazard.FireInterval = Mathf.Max(2f, 6f - sectorNumber * 0.5f);
                hazard.ProjectileSpeed = 8f + sectorNumber * 2f;
                model.AddChild(hazard);

                // Warning light on the hazard
                var warningLight = new OmniLight3D();
                warningLight.Position = new Vector3(0, 1.5f, 0);
                warningLight.LightColor = new Color(1f, 0.2f, 0.1f);
                warningLight.LightEnergy = 0.6f;
                warningLight.OmniRange = 4f;
                warningLight.ShadowEnabled = false;
                model.AddChild(warningLight);
            }
        }

        private static bool IsNearDoor(float x, float z, float halfW, float halfH,
            float clearance, bool doorN, bool doorS, bool doorE, bool doorW)
        {
            if (doorN && Mathf.Abs(x) < clearance && z < -halfH + clearance) return true;
            if (doorS && Mathf.Abs(x) < clearance && z > halfH - clearance) return true;
            if (doorE && x > halfW - clearance && Mathf.Abs(z) < clearance) return true;
            if (doorW && x < -halfW + clearance && Mathf.Abs(z) < clearance) return true;
            return false;
        }
    }

    /// <summary>
    /// Behavior component for AXIS weapon hazards.
    /// Periodically fires a projectile toward the nearest player.
    /// </summary>
    public partial class AxisHazardBehavior : Node
    {
        public float DamagePerShot { get; set; } = 10f;
        public float FireInterval { get; set; } = 4f;
        public float ProjectileSpeed { get; set; } = 12f;

        private float _timer;
        private bool _active;

        public override void _Ready()
        {
            // Small activation delay so hazards don't fire immediately on room entry
            _timer = -2f;
        }

        public override void _Process(double delta)
        {
            if (!_active)
            {
                // Activate when room is entered (parent is in scene tree and player exists)
                if (ServiceLocator.TryGet<PlayerController>(out _))
                    _active = true;
                else
                    return;
            }

            _timer += (float)delta;
            if (_timer < FireInterval) return;
            _timer = 0f;

            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;

            var parent = GetParent<Node3D>();
            if (parent == null) return;

            // Fire projectile toward player
            var origin = parent.GlobalPosition + Vector3.Up * 1.5f;
            var target = player.GlobalPosition + Vector3.Up * 0.5f;
            var direction = (target - origin).Normalized();

            var damageInfo = new DamageInfo
            {
                RawDamage = DamagePerShot,
                FinalDamage = DamagePerShot,
                DamageType = DamageType.Physical,
                Attacker = parent,
            };

            var projectile = new Projectile();
            parent.GetTree().Root.AddChild(projectile);
            projectile.GlobalPosition = origin;
            projectile.Initialize(direction, ProjectileSpeed, 30f, damageInfo, Team.Enemy);

            // Commentary from AXIS (rare)
            if (GD.Randf() < 0.05f && ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                string[] taunts = {
                    "My turrets say hello.",
                    "Consider that a warning shot. The next dozen won't be.",
                    "You're in MY arena, scrapper.",
                    "Turret defense protocol: active.",
                };
                commentary.QueueLine("AXIS", taunts[GD.RandRange(0, taunts.Length - 1)],
                    CommentaryPriority.Low, CommentaryCategory.CombatReaction);
            }
        }
    }
}
