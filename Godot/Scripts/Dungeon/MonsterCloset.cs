using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Determines spawn positions and entry animations for enemies in a room.
    /// Instead of all enemies popping into the center, they enter from
    /// walls, corners, trapdoors, drop-ins, or surround the player.
    /// </summary>
    public enum SpawnEntryType
    {
        EdgeBurst,      // Spawn along a random wall, rush inward
        CornerAmbush,   // Spawn in 2-3 corners
        Trapdoor,       // Rise from floor with dust VFX
        DropIn,         // Fall from above with shadow telegraph
        Surround,       // Evenly spaced around room perimeter
    }

    public static class MonsterCloset
    {
        private static readonly RandomNumberGenerator Rng = new();

        static MonsterCloset()
        {
            Rng.Randomize();
        }

        /// <summary>
        /// Pick a random spawn entry type. Optionally exclude a type
        /// (to avoid repeating the same entry for consecutive waves).
        /// </summary>
        public static SpawnEntryType PickEntryType(SpawnEntryType? exclude = null)
        {
            var types = new List<SpawnEntryType>
            {
                SpawnEntryType.EdgeBurst,
                SpawnEntryType.CornerAmbush,
                SpawnEntryType.Trapdoor,
                SpawnEntryType.DropIn,
                SpawnEntryType.Surround,
            };

            if (exclude.HasValue)
                types.Remove(exclude.Value);

            return types[Rng.RandiRange(0, types.Count - 1)];
        }

        /// <summary>
        /// Generate spawn positions for a given entry type within a room.
        /// Returns local positions relative to room center.
        /// Room size is always 32x32.
        /// </summary>
        public static List<Vector3> GetSpawnPositions(SpawnEntryType entry, int enemyCount, float roomHalf = 14f)
        {
            return entry switch
            {
                SpawnEntryType.EdgeBurst => GenerateEdgeBurst(enemyCount, roomHalf),
                SpawnEntryType.CornerAmbush => GenerateCornerAmbush(enemyCount, roomHalf),
                SpawnEntryType.Trapdoor => GenerateTrapdoor(enemyCount, roomHalf),
                SpawnEntryType.DropIn => GenerateDropIn(enemyCount, roomHalf),
                SpawnEntryType.Surround => GenerateSurround(enemyCount, roomHalf),
                _ => GenerateSurround(enemyCount, roomHalf),
            };
        }

        /// <summary>
        /// Apply the entry animation to a spawned enemy. Call after AddChild.
        /// Disables physics while the tween plays so gravity/AI don't fight the animation.
        /// </summary>
        public static void PlayEntryAnimation(EnemyController enemy, SpawnEntryType entry, Vector3 finalPos)
        {
            // Freeze physics during entry so gravity/AI don't interfere with the tween
            enemy.SetPhysicsProcess(false);
            var ai = enemy.GetNodeOrNull<EnemyAI>("EnemyAI");
            ai?.SetPhysicsProcess(false);
            enemy.BossAI?.SetPhysicsProcess(false);

            switch (entry)
            {
                case SpawnEntryType.EdgeBurst:
                    AnimateEdgeBurst(enemy, finalPos);
                    break;
                case SpawnEntryType.CornerAmbush:
                    AnimateCornerAmbush(enemy, finalPos);
                    break;
                case SpawnEntryType.Trapdoor:
                    AnimateTrapdoor(enemy, finalPos);
                    break;
                case SpawnEntryType.DropIn:
                    AnimateDropIn(enemy, finalPos);
                    break;
                case SpawnEntryType.Surround:
                    AnimateSurround(enemy, finalPos);
                    break;
            }
        }

        /// <summary>
        /// Re-enable physics after entry animation completes.
        /// </summary>
        private static void EnablePhysicsAfterEntry(EnemyController enemy, float delay)
        {
            var tree = enemy.GetTree();
            if (tree == null) return;
            tree.CreateTimer(delay).Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(enemy)) return;
                enemy.SetPhysicsProcess(true);
                var ai = enemy.GetNodeOrNull<EnemyAI>("EnemyAI");
                ai?.SetPhysicsProcess(true);
                enemy.BossAI?.SetPhysicsProcess(true);
            };
        }

        // --- Position generators ---

        private static List<Vector3> GenerateEdgeBurst(int count, float roomHalf)
        {
            var positions = new List<Vector3>();
            // Pick a random wall: 0=North, 1=South, 2=East, 3=West
            int wall = Rng.RandiRange(0, 3);
            float spacing = (roomHalf * 2f) / (count + 1);

            for (int i = 0; i < count; i++)
            {
                float along = -roomHalf + spacing * (i + 1);
                Vector3 pos = wall switch
                {
                    0 => new Vector3(along, 0.9f, -roomHalf + 1.5f),  // North wall
                    1 => new Vector3(along, 0.9f, roomHalf - 1.5f),   // South wall
                    2 => new Vector3(roomHalf - 1.5f, 0.9f, along),   // East wall
                    _ => new Vector3(-roomHalf + 1.5f, 0.9f, along),  // West wall
                };
                positions.Add(pos);
            }
            return positions;
        }

        private static List<Vector3> GenerateCornerAmbush(int count, float roomHalf)
        {
            var positions = new List<Vector3>();
            float cornerOffset = roomHalf - 3f;
            var corners = new Vector3[]
            {
                new(-cornerOffset, 0.9f, -cornerOffset),
                new(cornerOffset, 0.9f, -cornerOffset),
                new(-cornerOffset, 0.9f, cornerOffset),
                new(cornerOffset, 0.9f, cornerOffset),
            };

            // Use 2-4 corners
            var usedCorners = new List<Vector3>(corners);
            if (usedCorners.Count > 3 && count <= 4)
                usedCorners.RemoveAt(Rng.RandiRange(0, 3)); // drop one random corner

            for (int i = 0; i < count; i++)
            {
                var corner = usedCorners[i % usedCorners.Count];
                // Jitter around the corner
                float jitterX = Rng.RandfRange(-2f, 2f);
                float jitterZ = Rng.RandfRange(-2f, 2f);
                positions.Add(corner + new Vector3(jitterX, 0, jitterZ));
            }
            return positions;
        }

        private static List<Vector3> GenerateTrapdoor(int count, float roomHalf)
        {
            var positions = new List<Vector3>();
            float spawnRange = roomHalf * 0.6f;
            for (int i = 0; i < count; i++)
            {
                float x = Rng.RandfRange(-spawnRange, spawnRange);
                float z = Rng.RandfRange(-spawnRange, spawnRange);
                positions.Add(new Vector3(x, 0.9f, z));
            }
            return positions;
        }

        private static List<Vector3> GenerateDropIn(int count, float roomHalf)
        {
            // Same ground positions as trapdoor, but enemies start high
            return GenerateTrapdoor(count, roomHalf);
        }

        private static List<Vector3> GenerateSurround(int count, float roomHalf)
        {
            var positions = new List<Vector3>();
            float radius = roomHalf * 0.7f;
            float angleStep = Mathf.Tau / count;
            float startAngle = Rng.RandfRange(0, Mathf.Tau);

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + angleStep * i;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                positions.Add(new Vector3(x, 0.9f, z));
            }
            return positions;
        }

        // --- Entry animations ---

        private static void AnimateEdgeBurst(EnemyController enemy, Vector3 finalPos)
        {
            // Start hidden behind wall, slide in
            var wallDir = finalPos.Normalized();
            Vector3 startPos = finalPos + wallDir * 4f;
            startPos.Y = finalPos.Y;

            enemy.Position = startPos;
            enemy.Scale = Vector3.One;

            var tween = enemy.CreateTween();
            tween.TweenProperty(enemy, "position", finalPos, 0.6f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);

            SpawnDustPuff(enemy, finalPos);
            EnablePhysicsAfterEntry(enemy, 0.65f);
        }

        private static void AnimateCornerAmbush(EnemyController enemy, Vector3 finalPos)
        {
            // Quick scale-in from nothing
            enemy.Position = finalPos;
            enemy.Scale = Vector3.One * 0.01f;

            var tween = enemy.CreateTween();
            tween.TweenProperty(enemy, "scale", Vector3.One * 1.15f, 0.3f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(enemy, "scale", Vector3.One, 0.1f);

            SpawnDustPuff(enemy, finalPos);
            EnablePhysicsAfterEntry(enemy, 0.45f);
        }

        private static void AnimateTrapdoor(EnemyController enemy, Vector3 finalPos)
        {
            // Rise up from below the floor
            Vector3 startPos = finalPos - new Vector3(0, 2f, 0);
            enemy.Position = startPos;
            enemy.Scale = Vector3.One;

            var tween = enemy.CreateTween();
            tween.TweenProperty(enemy, "position", finalPos, 0.8f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);

            SpawnRisingDust(enemy, finalPos);
            EnablePhysicsAfterEntry(enemy, 0.85f);
        }

        private static void AnimateDropIn(EnemyController enemy, Vector3 finalPos)
        {
            // Drop from above with shadow
            Vector3 startPos = finalPos + new Vector3(0, 8f, 0);
            enemy.Position = startPos;
            enemy.Scale = Vector3.One;

            // Shadow telegraph on ground
            SpawnDropShadow(enemy, finalPos);

            // Delay then drop
            var tween = enemy.CreateTween();
            tween.TweenInterval(0.5f); // telegraph time
            tween.TweenProperty(enemy, "position", finalPos, 0.3f)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            tween.TweenCallback(Callable.From(() => SpawnImpactDust(enemy, finalPos)));
            EnablePhysicsAfterEntry(enemy, 0.85f);
        }

        private static void AnimateSurround(EnemyController enemy, Vector3 finalPos)
        {
            // Fade in at position
            enemy.Position = finalPos;
            enemy.Scale = Vector3.One * 0.01f;

            var tween = enemy.CreateTween();
            tween.TweenProperty(enemy, "scale", Vector3.One, 0.5f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Elastic);

            SpawnDustPuff(enemy, finalPos);
            EnablePhysicsAfterEntry(enemy, 0.55f);
        }

        // --- VFX helpers ---

        private static void SpawnDustPuff(Node3D parent, Vector3 pos)
        {
            var particles = new GpuParticles3D();
            particles.Position = pos;
            particles.Emitting = true;
            particles.OneShot = true;
            particles.Amount = 8;
            particles.Lifetime = 0.6f;
            particles.SpeedScale = 2f;

            var mat = new ParticleProcessMaterial();
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 0.5f;
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 60f;
            mat.InitialVelocityMin = 1f;
            mat.InitialVelocityMax = 2.5f;
            mat.Gravity = new Vector3(0, -3, 0);
            mat.ScaleMin = 0.3f;
            mat.ScaleMax = 0.6f;
            mat.Color = new Color(0.6f, 0.55f, 0.4f, 0.7f);
            particles.ProcessMaterial = mat;

            var mesh = new QuadMesh();
            mesh.Size = new Vector2(0.3f, 0.3f);
            particles.DrawPass1 = mesh;

            var particleParent = parent.GetParent();
            if (particleParent != null) particleParent.AddChild(particles);
            var tree1 = parent.GetTree();
            if (tree1 != null)
            {
                tree1.CreateTimer(1.5f).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(particles)) particles.QueueFree();
                };
            }
        }

        private static void SpawnRisingDust(Node3D parent, Vector3 pos)
        {
            var particles = new GpuParticles3D();
            particles.Position = pos - new Vector3(0, 0.5f, 0);
            particles.Emitting = true;
            particles.OneShot = true;
            particles.Amount = 12;
            particles.Lifetime = 0.8f;
            particles.SpeedScale = 1.5f;

            var mat = new ParticleProcessMaterial();
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
            mat.EmissionRingRadius = 1f;
            mat.EmissionRingInnerRadius = 0.3f;
            mat.EmissionRingHeight = 0.1f;
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 30f;
            mat.InitialVelocityMin = 1.5f;
            mat.InitialVelocityMax = 3f;
            mat.Gravity = new Vector3(0, -2, 0);
            mat.ScaleMin = 0.2f;
            mat.ScaleMax = 0.5f;
            mat.Color = new Color(0.5f, 0.45f, 0.3f, 0.8f);
            particles.ProcessMaterial = mat;

            var mesh = new QuadMesh();
            mesh.Size = new Vector2(0.25f, 0.25f);
            particles.DrawPass1 = mesh;

            var particleParent2 = parent.GetParent();
            if (particleParent2 != null) particleParent2.AddChild(particles);
            var tree2 = parent.GetTree();
            if (tree2 != null)
            {
                tree2.CreateTimer(2f).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(particles)) particles.QueueFree();
                };
            }
        }

        private static void SpawnDropShadow(Node3D parent, Vector3 groundPos)
        {
            // Dark circle on ground that grows then vanishes
            var shadow = new MeshInstance3D();
            var quad = new QuadMesh();
            quad.Size = new Vector2(2f, 2f);
            shadow.Mesh = quad;
            shadow.Position = groundPos + new Vector3(0, 0.05f, 0);
            shadow.Rotation = new Vector3(-Mathf.Pi / 2f, 0, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0, 0, 0, 0.5f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            shadow.MaterialOverride = mat;

            shadow.Scale = Vector3.One * 0.01f;
            var shadowParent = parent.GetParent();
            if (shadowParent != null) shadowParent.AddChild(shadow);

            var tween = shadow.CreateTween();
            tween.TweenProperty(shadow, "scale", Vector3.One, 0.4f)
                .SetEase(Tween.EaseType.Out);
            tween.TweenInterval(0.3f);
            tween.TweenProperty(shadow, "scale", Vector3.One * 0.01f, 0.2f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(shadow)) shadow.QueueFree();
            }));
        }

        private static void SpawnImpactDust(Node3D parent, Vector3 pos)
        {
            var particles = new GpuParticles3D();
            particles.Position = pos;
            particles.Emitting = true;
            particles.OneShot = true;
            particles.Amount = 10;
            particles.Lifetime = 0.5f;
            particles.SpeedScale = 2.5f;

            var mat = new ParticleProcessMaterial();
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 0.3f;
            mat.Direction = new Vector3(0, 0, 0);
            mat.Spread = 180f;
            mat.InitialVelocityMin = 2f;
            mat.InitialVelocityMax = 4f;
            mat.Gravity = new Vector3(0, -5, 0);
            mat.ScaleMin = 0.2f;
            mat.ScaleMax = 0.4f;
            mat.Color = new Color(0.5f, 0.45f, 0.35f, 0.8f);
            particles.ProcessMaterial = mat;

            var mesh = new QuadMesh();
            mesh.Size = new Vector2(0.2f, 0.2f);
            particles.DrawPass1 = mesh;

            var particleParent = parent.GetParent();
            if (particleParent != null) particleParent.AddChild(particles);
            var tree1 = parent.GetTree();
            if (tree1 != null)
            {
                tree1.CreateTimer(1.5f).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(particles)) particles.QueueFree();
                };
            }
        }
    }
}
