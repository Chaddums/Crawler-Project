using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Library of distinct room layout blueprints. Each blueprint defines a unique
    /// obstacle pattern, lighting mood, floor features, and structural elements
    /// so that every room feels like a surprise.
    /// </summary>
    public static class RoomLayoutLibrary
    {
        public enum LayoutId
        {
            // Combat layouts (20)
            Pillbox,          // 4 pillars forming a box in center
            Trench,           // Long low walls creating lanes
            Arena,            // Open center, ring of cover around edges
            Maze,             // Dense obstacles forming tight corridors
            Sniper,           // One high pillar in center, scattered low cover
            Bunker,           // Heavy cover clusters in two halves
            Gauntlet,         // Obstacles form a winding path
            Crossroads,       // X-shaped open paths, cover in quadrants
            Pillars,          // Grid of evenly-spaced pillars
            Scrapyard,        // Dense random debris clusters
            FiringRange,      // Rows of cover like a shooting range
            CargoBay,         // Stacked crate walls forming rooms-within-rooms
            Reactor,          // Central hazard with ring walkway
            CircuitBoard,     // Geometric right-angle walls
            Ambush,           // Minimal cover, enemies have advantage
            Fortress,         // Central fortified position
            Catwalk,          // Elevated walkways with ramps over lower floor
            Workshop,         // Workbenches and tool stations
            ServerRoom,       // Rows of server racks
            JunkPile,         // Asymmetric piles of scrap
            HighGround,       // Corner platforms with ramps, fight for elevation
            Overlook,         // Central elevated platform with surrounding pit
            MultiLevel,       // Staggered platforms at different heights

            // Special mood variants
            DarkRoom,         // Very dim lighting, glowing floor strips
            RedAlert,         // Red emergency lighting, alarm feel
            Overgrown,        // Green-tinted, vine/moss decorations
            Frozen,           // Blue-tinted, ice crystal decorations
            Toxic,            // Green pools, warning signs
            Scorched,         // Blackened floor, ember particles
        }

        public struct LayoutBlueprint
        {
            public LayoutId Id;
            public string DisplayName;
            public Action<Node3D, Vector2, RandomNumberGenerator, SectorData> Build;
            public Color? LightTint;
            public float LightEnergy;
            public bool HasFloorAccent;
            public Color FloorAccentColor;
            public bool HasAmbientParticles;
            public Color ParticleColor;
        }

        private static readonly List<LayoutBlueprint> _combatLayouts = new();
        private static readonly List<LayoutBlueprint> _moodVariants = new();
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            RegisterCombatLayouts();
            RegisterMoodVariants();
        }

        /// <summary>
        /// Pick a random combat layout for a room. Uses grid position as seed
        /// for determinism but ensures variety.
        /// </summary>
        public static LayoutBlueprint GetCombatLayout(Vector2I gridPos, RandomNumberGenerator rng)
        {
            EnsureInitialized();
            // Mix grid position with randomness so adjacent rooms differ
            int hash = HashCode.Combine(gridPos.X * 7919, gridPos.Y * 6271, rng.RandiRange(0, 999));
            int idx = ((hash % _combatLayouts.Count) + _combatLayouts.Count) % _combatLayouts.Count;
            return _combatLayouts[idx];
        }

        /// <summary>
        /// 25% chance to apply a mood variant on top of the base layout.
        /// </summary>
        public static LayoutBlueprint? GetMoodVariant(RandomNumberGenerator rng)
        {
            EnsureInitialized();
            if (rng.Randf() > 0.25f) return null;
            return _moodVariants[rng.RandiRange(0, _moodVariants.Count - 1)];
        }

        // ── Combat Layout Registration ──

        private static void RegisterCombatLayouts()
        {
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Pillbox, DisplayName = "Pillbox",
                LightEnergy = 1.2f,
                Build = BuildPillbox
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Trench, DisplayName = "Trench",
                LightEnergy = 1.0f, HasFloorAccent = true,
                FloorAccentColor = new Color(0.6f, 0.5f, 0.2f),
                Build = BuildTrench
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Arena, DisplayName = "Arena",
                LightEnergy = 1.5f,
                HasFloorAccent = true, FloorAccentColor = new Color(0.8f, 0.3f, 0.1f),
                Build = BuildArena
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Maze, DisplayName = "Maze",
                LightEnergy = 0.8f,
                Build = BuildMaze
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Sniper, DisplayName = "Sniper Nest",
                LightEnergy = 1.1f,
                Build = BuildSniper
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Bunker, DisplayName = "Bunker",
                LightEnergy = 0.9f,
                Build = BuildBunker
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Gauntlet, DisplayName = "Gauntlet",
                LightEnergy = 1.3f, HasFloorAccent = true,
                FloorAccentColor = new Color(0.9f, 0.5f, 0.1f),
                Build = BuildGauntlet
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Crossroads, DisplayName = "Crossroads",
                LightEnergy = 1.2f,
                Build = BuildCrossroads
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Pillars, DisplayName = "Pillars",
                LightEnergy = 1.0f,
                Build = BuildPillars
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Scrapyard, DisplayName = "Scrapyard",
                LightEnergy = 0.7f,
                HasAmbientParticles = true, ParticleColor = new Color(0.5f, 0.4f, 0.3f),
                Build = BuildScrapyard
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.FiringRange, DisplayName = "Firing Range",
                LightEnergy = 1.4f,
                Build = BuildFiringRange
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.CargoBay, DisplayName = "Cargo Bay",
                LightEnergy = 0.9f,
                Build = BuildCargoBay
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Reactor, DisplayName = "Reactor",
                LightTint = new Color(0.2f, 0.8f, 0.4f), LightEnergy = 1.3f,
                HasAmbientParticles = true, ParticleColor = new Color(0.3f, 0.9f, 0.4f),
                Build = BuildReactor
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.CircuitBoard, DisplayName = "Circuit Board",
                LightEnergy = 1.1f, HasFloorAccent = true,
                FloorAccentColor = new Color(0.1f, 0.7f, 0.5f),
                Build = BuildCircuitBoard
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Ambush, DisplayName = "Kill Zone",
                LightEnergy = 0.6f,
                Build = BuildAmbush
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Fortress, DisplayName = "Fortress",
                LightEnergy = 1.0f,
                Build = BuildFortress
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Catwalk, DisplayName = "Catwalk",
                LightEnergy = 1.2f, HasFloorAccent = true,
                FloorAccentColor = new Color(0.5f, 0.5f, 0.6f),
                Build = BuildCatwalk
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Workshop, DisplayName = "Workshop",
                LightEnergy = 1.3f,
                Build = BuildWorkshop
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.ServerRoom, DisplayName = "Server Room",
                LightTint = new Color(0.3f, 0.5f, 0.9f), LightEnergy = 0.8f,
                Build = BuildServerRoom
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.JunkPile, DisplayName = "Junk Pile",
                LightEnergy = 0.7f,
                HasAmbientParticles = true, ParticleColor = new Color(0.6f, 0.5f, 0.3f),
                Build = BuildJunkPile
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.HighGround, DisplayName = "High Ground",
                LightEnergy = 1.2f,
                Build = BuildHighGround
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.Overlook, DisplayName = "Overlook",
                LightEnergy = 1.4f, HasFloorAccent = true,
                FloorAccentColor = new Color(0.7f, 0.5f, 0.2f),
                Build = BuildOverlook
            });
            _combatLayouts.Add(new LayoutBlueprint
            {
                Id = LayoutId.MultiLevel, DisplayName = "Multi-Level",
                LightEnergy = 1.1f,
                Build = BuildMultiLevel
            });
        }

        private static void RegisterMoodVariants()
        {
            _moodVariants.Add(new LayoutBlueprint
            {
                Id = LayoutId.DarkRoom, DisplayName = "Dark",
                LightEnergy = 0.3f, HasFloorAccent = true,
                FloorAccentColor = new Color(0.2f, 0.6f, 0.9f),
                Build = BuildDarkMood
            });
            _moodVariants.Add(new LayoutBlueprint
            {
                Id = LayoutId.RedAlert, DisplayName = "Red Alert",
                LightTint = new Color(0.9f, 0.15f, 0.1f), LightEnergy = 1.5f,
                HasAmbientParticles = true, ParticleColor = new Color(0.9f, 0.2f, 0.1f),
                Build = BuildRedAlertMood
            });
            _moodVariants.Add(new LayoutBlueprint
            {
                Id = LayoutId.Overgrown, DisplayName = "Overgrown",
                LightTint = new Color(0.3f, 0.8f, 0.3f), LightEnergy = 0.9f,
                HasAmbientParticles = true, ParticleColor = new Color(0.2f, 0.7f, 0.3f),
                Build = BuildOvergrownMood
            });
            _moodVariants.Add(new LayoutBlueprint
            {
                Id = LayoutId.Frozen, DisplayName = "Frozen",
                LightTint = new Color(0.5f, 0.7f, 1f), LightEnergy = 1.1f,
                HasAmbientParticles = true, ParticleColor = new Color(0.6f, 0.8f, 1f),
                Build = BuildFrozenMood
            });
            _moodVariants.Add(new LayoutBlueprint
            {
                Id = LayoutId.Toxic, DisplayName = "Toxic",
                LightTint = new Color(0.4f, 0.9f, 0.1f), LightEnergy = 1.0f,
                Build = BuildToxicMood
            });
            _moodVariants.Add(new LayoutBlueprint
            {
                Id = LayoutId.Scorched, DisplayName = "Scorched",
                LightTint = new Color(0.9f, 0.5f, 0.1f), LightEnergy = 0.8f,
                HasAmbientParticles = true, ParticleColor = new Color(0.9f, 0.4f, 0.1f),
                Build = BuildScorchedMood
            });
        }

        // ── Shared helpers ──

        private static void PlacePillar(Node3D parent, Vector3 pos, float height, RandomNumberGenerator rng)
        {
            float radius = rng.RandfRange(0.4f, 0.7f);
            var model = ModelLibrary.TryLoad("prop", "column_" + rng.RandiRange(1, 3));
            if (model != null)
            {
                RoomBuilder.ScaleModelToFitEffective(model, height);
                AddObstacleWithModel(parent, pos, model,
                    new CylinderShape3D { Radius = radius, Height = height },
                    new Vector3(0, height / 2f, 0));
            }
            else
            {
                AddObstacle(parent, pos,
                    new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 8 },
                    new CylinderShape3D { Radius = radius, Height = height },
                    new Vector3(0, height / 2f, 0),
                    new Color(0.35f, 0.33f, 0.3f), 0.6f, 0.5f);
            }
        }

        private static void PlaceCrate(Node3D parent, Vector3 pos, float scale, RandomNumberGenerator rng)
        {
            string id = rng.Randf() > 0.5f ? "crate" : "barrel";
            var model = ModelLibrary.TryLoad("prop", id);
            if (model != null)
            {
                RoomBuilder.ScaleModelToFitEffective(model, scale);
                AddObstacleWithModel(parent, pos, model,
                    new BoxShape3D { Size = new Vector3(scale, scale, scale) },
                    new Vector3(0, scale / 2f, 0));
            }
            else
            {
                AddObstacle(parent, pos,
                    new BoxMesh { Size = new Vector3(scale, scale * 0.8f, scale) },
                    new BoxShape3D { Size = new Vector3(scale, scale * 0.8f, scale) },
                    new Vector3(0, scale * 0.4f, 0),
                    new Color(0.4f, 0.3f, 0.18f), 0.3f, 0.7f);
            }
        }

        private static void PlaceLowWall(Node3D parent, Vector3 pos, float length, float rot)
        {
            var body = AddObstacle(parent, pos,
                new BoxMesh { Size = new Vector3(length, 1.2f, 0.5f) },
                new BoxShape3D { Size = new Vector3(length, 1.2f, 0.5f) },
                new Vector3(0, 0.6f, 0),
                new Color(0.32f, 0.3f, 0.28f), 0.5f, 0.6f);
            body.RotateY(rot);
        }

        private static void PlaceTallWall(Node3D parent, Vector3 pos, float length, float height, float rot)
        {
            var body = AddObstacle(parent, pos,
                new BoxMesh { Size = new Vector3(length, height, 0.5f) },
                new BoxShape3D { Size = new Vector3(length, height, 0.5f) },
                new Vector3(0, height / 2f, 0),
                new Color(0.38f, 0.36f, 0.32f), 0.5f, 0.55f);
            body.RotateY(rot);
        }

        private static void AddFloorStrip(Node3D parent, Vector3 pos, Vector3 size, Color color)
        {
            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = size };
            mesh.Position = pos + new Vector3(0, 0.01f, 0);
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 0.5f;
            mat.Metallic = 0.6f;
            mat.Roughness = 0.4f;
            mesh.MaterialOverride = mat;
            parent.AddChild(mesh);
        }

        private static void AddFloorGrate(Node3D parent, Vector3 pos, Vector2 size)
        {
            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = new Vector3(size.X, 0.05f, size.Y) };
            mesh.Position = pos + new Vector3(0, 0.02f, 0);
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.25f, 0.25f, 0.27f);
            mat.Metallic = 0.7f;
            mat.Roughness = 0.4f;
            mesh.MaterialOverride = mat;
            parent.AddChild(mesh);
        }

        private static void AddCeilingLight(Node3D parent, Vector3 pos, Color color, float energy = 1.5f, float range = 8f)
        {
            var light = new OmniLight3D();
            light.Position = pos;
            light.LightColor = color;
            light.LightEnergy = energy;
            light.OmniRange = range;
            light.ShadowEnabled = false;
            parent.AddChild(light);
        }

        private static StaticBody3D AddObstacle(Node3D parent, Vector3 floorPos,
            Mesh mesh, Shape3D shape, Vector3 offset, Color color, float metallic, float roughness)
        {
            var body = new StaticBody3D();
            body.Position = floorPos;
            body.CollisionLayer = 1;
            parent.AddChild(body);

            var meshNode = new MeshInstance3D();
            meshNode.Mesh = mesh;
            meshNode.Position = offset;
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.Metallic = metallic;
            mat.Roughness = roughness;
            meshNode.MaterialOverride = mat;
            body.AddChild(meshNode);

            var col = new CollisionShape3D();
            col.Shape = shape;
            col.Position = offset;
            body.AddChild(col);
            return body;
        }

        private static StaticBody3D AddObstacleWithModel(Node3D parent, Vector3 floorPos,
            Node3D model, Shape3D shape, Vector3 colOffset)
        {
            var body = new StaticBody3D();
            body.Position = floorPos;
            body.CollisionLayer = 1;
            parent.AddChild(body);
            body.AddChild(model);
            var col = new CollisionShape3D();
            col.Shape = shape;
            col.Position = colOffset;
            body.AddChild(col);
            return body;
        }

        private static bool IsClearOfCenter(float x, float z, float clearance = 4f)
        {
            return Mathf.Abs(x) > clearance || Mathf.Abs(z) > clearance;
        }

        private static bool IsClearOfDoors(float x, float z, float halfW, float halfH, float clearance = 3f)
        {
            // Near any edge center = potential door zone
            if (Mathf.Abs(x) < clearance && (z < -halfH + clearance || z > halfH - clearance))
                return false;
            if (Mathf.Abs(z) < clearance && (x < -halfW + clearance || x > halfW - clearance))
                return false;
            return true;
        }

        // ── Combat Layout Builders ──

        private static void BuildPillbox(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            float inset = h * 0.35f;
            // 4 pillars forming a box
            PlacePillar(parent, new Vector3(-inset, 0, -inset), 4f, rng);
            PlacePillar(parent, new Vector3(inset, 0, -inset), 4f, rng);
            PlacePillar(parent, new Vector3(-inset, 0, inset), 4f, rng);
            PlacePillar(parent, new Vector3(inset, 0, inset), 4f, rng);
            // Low cover between pillars
            PlaceLowWall(parent, new Vector3(0, 0, -inset), inset * 1.2f, 0);
            PlaceLowWall(parent, new Vector3(0, 0, inset), inset * 1.2f, 0);
            // Scattered crates in corners
            for (int i = 0; i < 3; i++)
            {
                float x = rng.RandfRange(-h * 0.7f, h * 0.7f);
                float z = rng.RandfRange(-h * 0.7f, h * 0.7f);
                if (IsClearOfCenter(x, z) && IsClearOfDoors(x, z, h, h))
                    PlaceCrate(parent, new Vector3(x, 0, z), rng.RandfRange(0.6f, 1f), rng);
            }
        }

        private static void BuildTrench(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Two parallel trench walls creating lanes
            float laneOffset = h * 0.3f;
            PlaceTallWall(parent, new Vector3(0, 0, -laneOffset), h * 1.2f, 2f, 0);
            PlaceTallWall(parent, new Vector3(0, 0, laneOffset), h * 1.2f, 2f, 0);
            // Gaps in walls (remove sections by adding cover at ends instead)
            PlaceLowWall(parent, new Vector3(-h * 0.5f, 0, 0), 2f, Mathf.Pi / 2f);
            PlaceLowWall(parent, new Vector3(h * 0.5f, 0, 0), 2f, Mathf.Pi / 2f);
            // Floor marking strips along lanes
            AddFloorStrip(parent, new Vector3(0, 0, -laneOffset - 2f),
                new Vector3(h * 1.4f, 0.02f, 0.3f), new Color(0.7f, 0.5f, 0.1f));
            AddFloorStrip(parent, new Vector3(0, 0, laneOffset + 2f),
                new Vector3(h * 1.4f, 0.02f, 0.3f), new Color(0.7f, 0.5f, 0.1f));
        }

        private static void BuildArena(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Ring of cover around edges, open center
            float ringR = h * 0.6f;
            int segments = rng.RandiRange(6, 10);
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.Tau;
                float x = Mathf.Cos(angle) * ringR;
                float z = Mathf.Sin(angle) * ringR;
                if (!IsClearOfDoors(x, z, h, h)) continue;
                PlaceLowWall(parent, new Vector3(x, 0, z), 2.5f, angle + Mathf.Pi / 2f);
            }
            // Center arena marking
            AddFloorStrip(parent, Vector3.Zero,
                new Vector3(ringR * 1.6f, 0.02f, 0.4f), new Color(0.8f, 0.3f, 0.1f));
            AddFloorStrip(parent, Vector3.Zero,
                new Vector3(0.4f, 0.02f, ringR * 1.6f), new Color(0.8f, 0.3f, 0.1f));
        }

        private static void BuildMaze(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Dense low walls forming a maze-like pattern
            int wallCount = rng.RandiRange(8, 14);
            var placed = new List<Vector3>();
            for (int i = 0; i < wallCount * 5 && placed.Count < wallCount; i++)
            {
                float x = rng.RandfRange(-h * 0.7f, h * 0.7f);
                float z = rng.RandfRange(-h * 0.7f, h * 0.7f);
                if (!IsClearOfCenter(x, z, 3f) || !IsClearOfDoors(x, z, h, h)) continue;
                bool tooClose = false;
                foreach (var p in placed)
                    if (new Vector3(x, 0, z).DistanceTo(p) < 2.5f) { tooClose = true; break; }
                if (tooClose) continue;
                placed.Add(new Vector3(x, 0, z));
                float rot = rng.Randf() > 0.5f ? 0 : Mathf.Pi / 2f;
                if (rng.Randf() > 0.3f)
                    PlaceLowWall(parent, new Vector3(x, 0, z), rng.RandfRange(2f, 4f), rot);
                else
                    PlacePillar(parent, new Vector3(x, 0, z), rng.RandfRange(2f, 4f), rng);
            }
        }

        private static void BuildSniper(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Tall central pillar
            PlacePillar(parent, new Vector3(0, 0, 0), 5f, rng);
            // Scattered low cover around perimeter
            for (int i = 0; i < 6; i++)
            {
                float angle = (i / 6f) * Mathf.Tau + rng.RandfRange(-0.3f, 0.3f);
                float dist = h * rng.RandfRange(0.45f, 0.7f);
                float x = Mathf.Cos(angle) * dist;
                float z = Mathf.Sin(angle) * dist;
                if (!IsClearOfDoors(x, z, h, h)) continue;
                PlaceCrate(parent, new Vector3(x, 0, z), rng.RandfRange(0.5f, 0.9f), rng);
            }
            // Spotlight on center
            AddCeilingLight(parent, new Vector3(0, 4.5f, 0), new Color(1f, 0.9f, 0.7f), 2f, 6f);
        }

        private static void BuildBunker(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Two bunker positions on opposite sides
            float offset = h * 0.45f;
            // Left bunker
            PlaceTallWall(parent, new Vector3(-offset, 0, -2f), 4f, 2.5f, Mathf.Pi / 2f);
            PlaceTallWall(parent, new Vector3(-offset, 0, 2f), 4f, 2.5f, Mathf.Pi / 2f);
            PlaceLowWall(parent, new Vector3(-offset, 0, 0), 4f, 0);
            // Right bunker
            PlaceTallWall(parent, new Vector3(offset, 0, -2f), 4f, 2.5f, Mathf.Pi / 2f);
            PlaceTallWall(parent, new Vector3(offset, 0, 2f), 4f, 2.5f, Mathf.Pi / 2f);
            PlaceLowWall(parent, new Vector3(offset, 0, 0), 4f, 0);
        }

        private static void BuildGauntlet(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Zigzag obstacles forming a winding path
            int steps = 5;
            for (int i = 0; i < steps; i++)
            {
                float z = -h * 0.6f + (i / (float)(steps - 1)) * h * 1.2f;
                float xOff = (i % 2 == 0) ? -h * 0.25f : h * 0.25f;
                PlaceTallWall(parent, new Vector3(xOff, 0, z), h * 0.5f, 2f, 0);
            }
            // Floor arrows pointing forward
            AddFloorStrip(parent, new Vector3(0, 0, -h * 0.3f),
                new Vector3(0.5f, 0.02f, 3f), new Color(0.9f, 0.5f, 0.1f));
            AddFloorStrip(parent, new Vector3(0, 0, h * 0.3f),
                new Vector3(0.5f, 0.02f, 3f), new Color(0.9f, 0.5f, 0.1f));
        }

        private static void BuildCrossroads(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            float q = h * 0.4f;
            // Cover in each quadrant, leaving X-shaped open paths
            PlaceLowWall(parent, new Vector3(-q, 0, -q), 3f, Mathf.Pi / 4f);
            PlaceLowWall(parent, new Vector3(q, 0, -q), 3f, -Mathf.Pi / 4f);
            PlaceLowWall(parent, new Vector3(-q, 0, q), 3f, -Mathf.Pi / 4f);
            PlaceLowWall(parent, new Vector3(q, 0, q), 3f, Mathf.Pi / 4f);
            // Crate clusters in quadrants
            PlaceCrate(parent, new Vector3(-q - 1f, 0, -q - 1f), 0.8f, rng);
            PlaceCrate(parent, new Vector3(q + 1f, 0, -q - 1f), 0.8f, rng);
            PlaceCrate(parent, new Vector3(-q - 1f, 0, q + 1f), 0.8f, rng);
            PlaceCrate(parent, new Vector3(q + 1f, 0, q + 1f), 0.8f, rng);
            // Center cross floor accent
            AddFloorStrip(parent, Vector3.Zero, new Vector3(h * 1.2f, 0.02f, 0.5f), new Color(0.5f, 0.5f, 0.6f));
            AddFloorStrip(parent, Vector3.Zero, new Vector3(0.5f, 0.02f, h * 1.2f), new Color(0.5f, 0.5f, 0.6f));
        }

        private static void BuildPillars(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // 3x3 grid of pillars
            float spacing = h * 0.45f;
            for (int gx = -1; gx <= 1; gx++)
            for (int gz = -1; gz <= 1; gz++)
            {
                if (gx == 0 && gz == 0) continue; // keep center clear
                float x = gx * spacing;
                float z = gz * spacing;
                if (!IsClearOfDoors(x, z, h, h)) continue;
                PlacePillar(parent, new Vector3(x, 0, z), rng.RandfRange(3f, 5f), rng);
            }
        }

        private static void BuildScrapyard(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Dense random debris — 8-15 pieces
            int count = rng.RandiRange(8, 15);
            var placed = new List<Vector3>();
            for (int attempt = 0; attempt < count * 8 && placed.Count < count; attempt++)
            {
                float x = rng.RandfRange(-h * 0.75f, h * 0.75f);
                float z = rng.RandfRange(-h * 0.75f, h * 0.75f);
                if (!IsClearOfCenter(x, z, 3f) || !IsClearOfDoors(x, z, h, h)) continue;
                bool tooClose = false;
                foreach (var p in placed)
                    if (new Vector3(x, 0, z).DistanceTo(p) < 2f) { tooClose = true; break; }
                if (tooClose) continue;
                placed.Add(new Vector3(x, 0, z));

                int type = rng.RandiRange(0, 3);
                if (type == 0) PlaceCrate(parent, new Vector3(x, 0, z), rng.RandfRange(0.4f, 0.9f), rng);
                else if (type == 1) PlacePillar(parent, new Vector3(x, 0, z), rng.RandfRange(1f, 2.5f), rng);
                else if (type == 2) PlaceLowWall(parent, new Vector3(x, 0, z), rng.RandfRange(1.5f, 3f), rng.RandfRange(0, Mathf.Tau));
                else
                {
                    // Scrap pile — cluster of small boxes
                    for (int j = 0; j < 3; j++)
                    {
                        float ox = rng.RandfRange(-0.5f, 0.5f);
                        float oz = rng.RandfRange(-0.5f, 0.5f);
                        float s = rng.RandfRange(0.2f, 0.5f);
                        var scrap = new MeshInstance3D();
                        scrap.Mesh = new BoxMesh { Size = new Vector3(s, s * 0.6f, s) };
                        scrap.Position = new Vector3(x + ox, s * 0.3f, z + oz);
                        scrap.RotateY(rng.RandfRange(0, Mathf.Tau));
                        scrap.RotateX(rng.RandfRange(-0.3f, 0.3f));
                        var mat = new StandardMaterial3D
                        {
                            AlbedoColor = new Color(rng.RandfRange(0.2f, 0.4f), rng.RandfRange(0.18f, 0.35f), rng.RandfRange(0.15f, 0.25f)),
                            Metallic = 0.5f, Roughness = 0.7f
                        };
                        scrap.MaterialOverride = mat;
                        parent.AddChild(scrap);
                    }
                }
            }
        }

        private static void BuildFiringRange(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // 3 rows of low cover like a shooting range
            for (int row = -1; row <= 1; row++)
            {
                float z = row * h * 0.35f;
                int segments = rng.RandiRange(2, 3);
                float segSpacing = h * 1f / segments;
                for (int s = 0; s < segments; s++)
                {
                    float x = -h * 0.5f + s * segSpacing + rng.RandfRange(-1f, 1f);
                    if (!IsClearOfDoors(x, z, h, h)) continue;
                    PlaceLowWall(parent, new Vector3(x, 0, z), rng.RandfRange(2f, 3.5f), 0);
                }
            }
            // Lane markings
            for (int lane = -1; lane <= 1; lane += 2)
            {
                AddFloorStrip(parent, new Vector3(lane * h * 0.3f, 0, 0),
                    new Vector3(0.15f, 0.02f, h * 1.4f), new Color(0.9f, 0.9f, 0.1f));
            }
        }

        private static void BuildCargoBay(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Stacked crates forming walls and rooms-within-rooms
            // L-shaped crate wall on left
            for (int i = 0; i < 3; i++)
                PlaceCrate(parent, new Vector3(-h * 0.5f, 0, -h * 0.3f + i * 1.2f), 1f, rng);
            for (int i = 0; i < 2; i++)
                PlaceCrate(parent, new Vector3(-h * 0.5f + (i + 1) * 1.2f, 0, -h * 0.3f), 1f, rng);
            // Cluster on right side
            for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                PlaceCrate(parent, new Vector3(h * 0.3f + i * 1.2f, 0, h * 0.15f + j * 1.2f), 1f, rng);
            // Scattered singles
            PlaceCrate(parent, new Vector3(h * 0.1f, 0, -h * 0.5f), 0.7f, rng);
            PlaceCrate(parent, new Vector3(-h * 0.15f, 0, h * 0.5f), 0.7f, rng);
        }

        private static void BuildReactor(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Central reactor core (glowing cylinder)
            var core = new MeshInstance3D();
            core.Mesh = new CylinderMesh { TopRadius = 1.5f, BottomRadius = 1.5f, Height = 3f, RadialSegments = 12 };
            core.Position = new Vector3(0, 1.5f, 0);
            var coreMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.1f, 0.3f, 0.15f),
                EmissionEnabled = true,
                Emission = new Color(0.2f, 0.9f, 0.3f),
                EmissionEnergyMultiplier = 2f,
                Metallic = 0.8f, Roughness = 0.3f
            };
            core.MaterialOverride = coreMat;
            parent.AddChild(core);
            // Core collision
            var coreBody = new StaticBody3D();
            coreBody.CollisionLayer = 1;
            var coreCol = new CollisionShape3D();
            coreCol.Shape = new CylinderShape3D { Radius = 1.5f, Height = 3f };
            coreCol.Position = new Vector3(0, 1.5f, 0);
            coreBody.AddChild(coreCol);
            parent.AddChild(coreBody);
            // Core light
            AddCeilingLight(parent, new Vector3(0, 3f, 0), new Color(0.2f, 0.9f, 0.3f), 3f, 10f);
            // Ring of pillars around core
            for (int i = 0; i < 6; i++)
            {
                float angle = (i / 6f) * Mathf.Tau;
                float x = Mathf.Cos(angle) * h * 0.5f;
                float z = Mathf.Sin(angle) * h * 0.5f;
                if (!IsClearOfDoors(x, z, h, h)) continue;
                PlacePillar(parent, new Vector3(x, 0, z), 3f, rng);
            }
        }

        private static void BuildCircuitBoard(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Right-angle walls forming circuit trace patterns
            float unit = h * 0.25f;
            // Horizontal traces
            PlaceLowWall(parent, new Vector3(-unit, 0, -unit * 2), unit * 2f, 0);
            PlaceLowWall(parent, new Vector3(unit, 0, unit), unit * 2.5f, 0);
            // Vertical traces
            PlaceLowWall(parent, new Vector3(-unit * 2, 0, 0), unit * 1.5f, Mathf.Pi / 2f);
            PlaceLowWall(parent, new Vector3(unit * 1.5f, 0, -unit), unit * 2f, Mathf.Pi / 2f);
            // "Component" nodes at intersections
            for (int i = 0; i < 4; i++)
            {
                float x = rng.RandfRange(-h * 0.5f, h * 0.5f);
                float z = rng.RandfRange(-h * 0.5f, h * 0.5f);
                if (!IsClearOfCenter(x, z, 2.5f)) continue;
                AddFloorStrip(parent, new Vector3(x, 0, z),
                    new Vector3(1f, 0.02f, 1f), new Color(0.1f, 0.7f, 0.5f));
            }
            // Trace floor lines
            AddFloorStrip(parent, new Vector3(0, 0, -unit),
                new Vector3(h * 1.2f, 0.02f, 0.15f), new Color(0.1f, 0.6f, 0.4f));
            AddFloorStrip(parent, new Vector3(-unit * 0.5f, 0, 0),
                new Vector3(0.15f, 0.02f, h * 1f), new Color(0.1f, 0.6f, 0.4f));
        }

        private static void BuildAmbush(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Minimal cover — just 2-3 small obstacles near edges
            PlaceCrate(parent, new Vector3(-h * 0.6f, 0, -h * 0.5f), 0.7f, rng);
            PlaceCrate(parent, new Vector3(h * 0.55f, 0, h * 0.45f), 0.7f, rng);
            if (rng.Randf() > 0.4f)
                PlaceLowWall(parent, new Vector3(h * 0.3f, 0, -h * 0.3f), 2f, rng.RandfRange(0, Mathf.Pi));
            // Warning floor markings
            AddFloorStrip(parent, new Vector3(-h * 0.3f, 0, 0),
                new Vector3(0.3f, 0.02f, h * 0.8f), new Color(0.9f, 0.2f, 0.1f));
            AddFloorStrip(parent, new Vector3(h * 0.3f, 0, 0),
                new Vector3(0.3f, 0.02f, h * 0.8f), new Color(0.9f, 0.2f, 0.1f));
        }

        private static void BuildFortress(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Central fortified square
            float fort = h * 0.25f;
            PlaceLowWall(parent, new Vector3(0, 0, -fort), fort * 2.5f, 0);
            PlaceLowWall(parent, new Vector3(0, 0, fort), fort * 2.5f, 0);
            PlaceLowWall(parent, new Vector3(-fort, 0, 0), fort * 2f, Mathf.Pi / 2f);
            PlaceLowWall(parent, new Vector3(fort, 0, 0), fort * 2f, Mathf.Pi / 2f);
            // Corner towers
            PlacePillar(parent, new Vector3(-fort, 0, -fort), 3.5f, rng);
            PlacePillar(parent, new Vector3(fort, 0, -fort), 3.5f, rng);
            PlacePillar(parent, new Vector3(-fort, 0, fort), 3.5f, rng);
            PlacePillar(parent, new Vector3(fort, 0, fort), 3.5f, rng);
            // Outer scattered cover
            PlaceCrate(parent, new Vector3(-h * 0.6f, 0, 0), 0.8f, rng);
            PlaceCrate(parent, new Vector3(h * 0.6f, 0, 0), 0.8f, rng);
        }

        private static void BuildCatwalk(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            float walkHeight = 2f;

            // Two elevated catwalks running north-south along the sides
            PlaceBridge(parent, new Vector3(-h * 0.35f, 0, 0), h * 1.2f, walkHeight, 0f);
            PlaceBridge(parent, new Vector3(h * 0.35f, 0, 0), h * 1.2f, walkHeight, 0f);

            // Cross-bridge connecting the two catwalks
            PlaceBridge(parent, new Vector3(0, 0, 0), h * 0.7f, walkHeight, Mathf.Pi / 2f);

            // Ramps at the south ends of each catwalk
            PlaceRamp(parent, new Vector3(-h * 0.35f, 0, h * 0.6f + 1.5f), 2.5f, 3f, walkHeight, Mathf.Pi);
            PlaceRamp(parent, new Vector3(h * 0.35f, 0, h * 0.6f + 1.5f), 2.5f, 3f, walkHeight, Mathf.Pi);

            // Ground-level cover below the catwalks
            PlaceCrate(parent, new Vector3(0, 0, -h * 0.4f), 0.7f, rng);
            PlaceCrate(parent, new Vector3(0, 0, h * 0.4f), 0.7f, rng);
            PlaceLowWall(parent, new Vector3(0, 0, h * 0.15f), 3f, Mathf.Pi / 2f);

            // Under-catwalk lighting
            AddCeilingLight(parent, new Vector3(-h * 0.35f, walkHeight - 0.3f, 0), new Color(0.5f, 0.6f, 0.8f), 1f, 6f);
            AddCeilingLight(parent, new Vector3(h * 0.35f, walkHeight - 0.3f, 0), new Color(0.5f, 0.6f, 0.8f), 1f, 6f);
        }

        private static void BuildWorkshop(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Workbenches (long low tables)
            for (int i = -1; i <= 1; i += 2)
            {
                float z = i * h * 0.35f;
                AddObstacle(parent, new Vector3(-h * 0.2f, 0, z),
                    new BoxMesh { Size = new Vector3(4f, 0.9f, 1.2f) },
                    new BoxShape3D { Size = new Vector3(4f, 0.9f, 1.2f) },
                    new Vector3(0, 0.45f, 0),
                    new Color(0.35f, 0.3f, 0.25f), 0.4f, 0.6f);
            }
            // Tool racks on walls
            PlaceCrate(parent, new Vector3(-h * 0.65f, 0, -h * 0.5f), 0.8f, rng);
            PlaceCrate(parent, new Vector3(-h * 0.65f, 0, h * 0.5f), 0.8f, rng);
            PlaceCrate(parent, new Vector3(h * 0.5f, 0, 0), 0.6f, rng);
            // Bright work lights
            AddCeilingLight(parent, new Vector3(-h * 0.2f, 4f, -h * 0.35f), new Color(1f, 0.95f, 0.9f), 2f, 8f);
            AddCeilingLight(parent, new Vector3(-h * 0.2f, 4f, h * 0.35f), new Color(1f, 0.95f, 0.9f), 2f, 8f);
        }

        private static void BuildServerRoom(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Rows of server racks (tall thin boxes)
            int rows = rng.RandiRange(3, 5);
            float rowSpacing = h * 1.2f / rows;
            for (int r = 0; r < rows; r++)
            {
                float x = -h * 0.5f + r * rowSpacing;
                float length = h * rng.RandfRange(0.5f, 0.8f);
                if (!IsClearOfDoors(x, 0, h, h)) continue;
                AddObstacle(parent, new Vector3(x, 0, 0),
                    new BoxMesh { Size = new Vector3(0.8f, 3f, length) },
                    new BoxShape3D { Size = new Vector3(0.8f, 3f, length) },
                    new Vector3(0, 1.5f, 0),
                    new Color(0.15f, 0.15f, 0.18f), 0.5f, 0.4f);
                // LED strip on rack
                AddFloorStrip(parent, new Vector3(x + 0.45f, 2f, 0),
                    new Vector3(0.05f, 0.05f, length * 0.8f),
                    new Color(0.2f, 0.5f, 0.9f));
            }
            // Cool blue ceiling lights
            AddCeilingLight(parent, new Vector3(0, 4.5f, -h * 0.3f), new Color(0.3f, 0.5f, 0.9f), 1.5f, 10f);
            AddCeilingLight(parent, new Vector3(0, 4.5f, h * 0.3f), new Color(0.3f, 0.5f, 0.9f), 1.5f, 10f);
        }

        private static void BuildJunkPile(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // 2-3 large asymmetric junk piles
            int piles = rng.RandiRange(2, 3);
            for (int p = 0; p < piles; p++)
            {
                float cx, cz;
                int attempts = 0;
                do
                {
                    cx = rng.RandfRange(-h * 0.5f, h * 0.5f);
                    cz = rng.RandfRange(-h * 0.5f, h * 0.5f);
                    attempts++;
                } while (attempts < 20 && (!IsClearOfCenter(cx, cz, 3.5f) || !IsClearOfDoors(cx, cz, h, h)));

                if (attempts >= 20) continue;

                // Each pile is 4-7 random objects clustered
                int pieces = rng.RandiRange(4, 7);
                for (int i = 0; i < pieces; i++)
                {
                    float ox = rng.RandfRange(-1.5f, 1.5f);
                    float oz = rng.RandfRange(-1.5f, 1.5f);
                    float s = rng.RandfRange(0.3f, 0.8f);
                    float y = rng.RandfRange(0, 0.3f) * i * 0.3f; // Stack up slightly
                    var scrap = new MeshInstance3D();
                    if (rng.Randf() > 0.5f)
                        scrap.Mesh = new BoxMesh { Size = new Vector3(s, s * rng.RandfRange(0.4f, 1f), s * rng.RandfRange(0.6f, 1.2f)) };
                    else
                        scrap.Mesh = new CylinderMesh { TopRadius = s * 0.4f, BottomRadius = s * 0.5f, Height = s, RadialSegments = 6 };
                    scrap.Position = new Vector3(cx + ox, s * 0.3f + y, cz + oz);
                    scrap.RotateY(rng.RandfRange(0, Mathf.Tau));
                    scrap.RotateX(rng.RandfRange(-0.2f, 0.2f));
                    var mat = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(rng.RandfRange(0.2f, 0.45f), rng.RandfRange(0.18f, 0.38f), rng.RandfRange(0.12f, 0.28f)),
                        Metallic = rng.RandfRange(0.3f, 0.7f), Roughness = rng.RandfRange(0.5f, 0.8f)
                    };
                    scrap.MaterialOverride = mat;
                    parent.AddChild(scrap);
                }
                // Add collision for the pile center
                var pileBody = new StaticBody3D();
                pileBody.Position = new Vector3(cx, 0, cz);
                pileBody.CollisionLayer = 1;
                var pileCol = new CollisionShape3D();
                pileCol.Shape = new CylinderShape3D { Radius = 1.5f, Height = 1.5f };
                pileCol.Position = new Vector3(0, 0.75f, 0);
                pileBody.AddChild(pileCol);
                parent.AddChild(pileBody);
            }
        }

        // ── Verticality Helpers ──

        /// <summary>
        /// Creates a raised platform with walkable surface and collision.
        /// The platform is a solid block the player can stand on.
        /// </summary>
        private static void PlaceRaisedPlatform(Node3D parent, Vector3 pos, Vector2 size, float height)
        {
            var body = new StaticBody3D();
            body.Position = pos;
            body.CollisionLayer = 1 | Constants.MASK_GROUND;
            parent.AddChild(body);

            // Platform top surface
            var meshNode = new MeshInstance3D();
            meshNode.Mesh = new BoxMesh { Size = new Vector3(size.X, height, size.Y) };
            meshNode.Position = new Vector3(0, height / 2f, 0);
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.3f, 0.28f, 0.25f),
                Metallic = 0.5f,
                Roughness = 0.6f
            };
            meshNode.MaterialOverride = mat;
            body.AddChild(meshNode);

            var col = new CollisionShape3D();
            col.Shape = new BoxShape3D { Size = new Vector3(size.X, height, size.Y) };
            col.Position = new Vector3(0, height / 2f, 0);
            body.AddChild(col);

            // Edge trim (subtle lip around the top)
            var trim = new MeshInstance3D();
            trim.Mesh = new BoxMesh { Size = new Vector3(size.X + 0.2f, 0.08f, size.Y + 0.2f) };
            trim.Position = new Vector3(0, height + 0.04f, 0);
            var trimMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.5f, 0.4f, 0.2f),
                Metallic = 0.7f,
                Roughness = 0.3f
            };
            trim.MaterialOverride = trimMat;
            body.AddChild(trim);

            // Support pillars underneath (visual)
            if (height >= 1.5f)
            {
                float pillarR = 0.15f;
                float px = size.X / 2f - 0.3f;
                float pz = size.Y / 2f - 0.3f;
                var pillarMat = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.24f, 0.22f), Metallic = 0.6f, Roughness = 0.5f };
                foreach (var corner in new[] { new Vector3(-px, 0, -pz), new Vector3(px, 0, -pz), new Vector3(-px, 0, pz), new Vector3(px, 0, pz) })
                {
                    var pillar = new MeshInstance3D();
                    pillar.Mesh = new CylinderMesh { TopRadius = pillarR, BottomRadius = pillarR, Height = height, RadialSegments = 6 };
                    pillar.Position = corner + new Vector3(0, height / 2f, 0);
                    pillar.MaterialOverride = pillarMat;
                    body.AddChild(pillar);
                }
            }
        }

        /// <summary>
        /// Creates a ramp (sloped surface) from ground level to a target height.
        /// rampDir: normalized XZ direction the ramp ascends toward.
        /// </summary>
        private static void PlaceRamp(Node3D parent, Vector3 basePos, float width, float length, float height, float rotY)
        {
            var body = new StaticBody3D();
            body.Position = basePos;
            body.CollisionLayer = 1 | Constants.MASK_GROUND;
            body.RotateY(rotY);
            parent.AddChild(body);

            // Ramp mesh — a box rotated to form a slope
            float rampLength = Mathf.Sqrt(length * length + height * height);
            float angle = Mathf.Atan2(height, length);

            var meshNode = new MeshInstance3D();
            meshNode.Mesh = new BoxMesh { Size = new Vector3(width, 0.15f, rampLength) };
            meshNode.Position = new Vector3(0, height / 2f, length / 2f);
            meshNode.RotateX(-angle);
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 0.32f, 0.28f),
                Metallic = 0.5f,
                Roughness = 0.55f
            };
            meshNode.MaterialOverride = mat;
            body.AddChild(meshNode);

            // Collision — use same rotated box
            var col = new CollisionShape3D();
            col.Shape = new BoxShape3D { Size = new Vector3(width, 0.15f, rampLength) };
            col.Position = new Vector3(0, height / 2f, length / 2f);
            col.RotateX(-angle);
            body.AddChild(col);

            // Side rails (thin vertical strips)
            var railMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.35f, 0.2f), Metallic = 0.6f, Roughness = 0.4f };
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = new MeshInstance3D();
                rail.Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.6f, length) };
                rail.Position = new Vector3(side * width / 2f, height / 2f + 0.3f, length / 2f);
                rail.MaterialOverride = railMat;
                body.AddChild(rail);
            }
        }

        /// <summary>
        /// Elevated narrow bridge connecting two points.
        /// </summary>
        private static void PlaceBridge(Node3D parent, Vector3 pos, float length, float height, float rotY)
        {
            float bridgeWidth = 2.5f;
            var body = new StaticBody3D();
            body.Position = pos;
            body.CollisionLayer = 1 | Constants.MASK_GROUND;
            body.RotateY(rotY);
            parent.AddChild(body);

            var meshNode = new MeshInstance3D();
            meshNode.Mesh = new BoxMesh { Size = new Vector3(bridgeWidth, 0.2f, length) };
            meshNode.Position = new Vector3(0, height, 0);
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.28f, 0.27f, 0.25f),
                Metallic = 0.6f,
                Roughness = 0.5f
            };
            meshNode.MaterialOverride = mat;
            body.AddChild(meshNode);

            var col = new CollisionShape3D();
            col.Shape = new BoxShape3D { Size = new Vector3(bridgeWidth, 0.2f, length) };
            col.Position = new Vector3(0, height, 0);
            body.AddChild(col);

            // Railings
            var railMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.35f, 0.2f), Metallic = 0.7f, Roughness = 0.4f };
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = new MeshInstance3D();
                rail.Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.8f, length) };
                rail.Position = new Vector3(side * bridgeWidth / 2f, height + 0.4f, 0);
                rail.MaterialOverride = railMat;
                body.AddChild(rail);
            }

            // Support columns
            var supportMat = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.24f, 0.22f), Metallic = 0.6f, Roughness = 0.5f };
            for (float z = -length / 2f + 1f; z <= length / 2f - 1f; z += length / 2f)
            {
                var support = new MeshInstance3D();
                support.Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.15f, Height = height, RadialSegments = 6 };
                support.Position = new Vector3(0, height / 2f, z);
                support.MaterialOverride = supportMat;
                body.AddChild(support);
            }
        }

        // ── Vertical Layout Builders ──

        private static void BuildHighGround(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;

            // Two raised platforms in opposite corners — fight for the high ground
            float platSize = h * 0.4f;
            float platHeight = 2f;

            // Northeast platform
            PlaceRaisedPlatform(parent, new Vector3(h * 0.4f, 0, -h * 0.4f), new Vector2(platSize, platSize), platHeight);
            PlaceRamp(parent, new Vector3(h * 0.4f, 0, -h * 0.4f + platSize / 2f + 1.5f), 2.5f, 3f, platHeight, Mathf.Pi);

            // Southwest platform
            PlaceRaisedPlatform(parent, new Vector3(-h * 0.4f, 0, h * 0.4f), new Vector2(platSize, platSize), platHeight);
            PlaceRamp(parent, new Vector3(-h * 0.4f, 0, h * 0.4f - platSize / 2f - 1.5f), 2.5f, 3f, platHeight, 0f);

            // Low cover in the central area between platforms
            PlaceLowWall(parent, new Vector3(0, 0, 0), 4f, Mathf.Pi / 4f);
            PlaceCrate(parent, new Vector3(-h * 0.15f, 0, -h * 0.15f), 0.7f, rng);
            PlaceCrate(parent, new Vector3(h * 0.15f, 0, h * 0.15f), 0.7f, rng);

            // Lights on platforms
            AddCeilingLight(parent, new Vector3(h * 0.4f, platHeight + 3f, -h * 0.4f), new Color(1f, 0.9f, 0.7f), 1.5f, 8f);
            AddCeilingLight(parent, new Vector3(-h * 0.4f, platHeight + 3f, h * 0.4f), new Color(1f, 0.9f, 0.7f), 1.5f, 8f);
        }

        private static void BuildOverlook(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;

            // Central elevated platform — king of the hill
            float centerHeight = 2.5f;
            PlaceRaisedPlatform(parent, Vector3.Zero, new Vector2(h * 0.5f, h * 0.5f), centerHeight);

            // Four ramps approaching from cardinal directions
            float rampOffset = h * 0.25f + 2f;
            PlaceRamp(parent, new Vector3(0, 0, -rampOffset), 2.5f, 3.5f, centerHeight, Mathf.Pi);  // from south
            PlaceRamp(parent, new Vector3(0, 0, rampOffset), 2.5f, 3.5f, centerHeight, 0f);          // from north
            PlaceRamp(parent, new Vector3(-rampOffset, 0, 0), 2.5f, 3.5f, centerHeight, Mathf.Pi / 2f);  // from east
            PlaceRamp(parent, new Vector3(rampOffset, 0, 0), 2.5f, 3.5f, centerHeight, -Mathf.Pi / 2f); // from west

            // Cover around the base of the platform
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.Pi / 2f + Mathf.Pi / 4f;
                float cx = Mathf.Cos(angle) * h * 0.5f;
                float cz = Mathf.Sin(angle) * h * 0.5f;
                if (IsClearOfDoors(cx, cz, h, h))
                    PlaceCrate(parent, new Vector3(cx, 0, cz), 0.7f, rng);
            }

            // Spotlight on the platform
            AddCeilingLight(parent, new Vector3(0, centerHeight + 4f, 0), new Color(1f, 0.85f, 0.5f), 2.5f, 10f);
        }

        private static void BuildMultiLevel(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;

            // Three platforms at staggered heights creating a multi-tier arena
            // Low tier (west) — height 1
            PlaceRaisedPlatform(parent, new Vector3(-h * 0.45f, 0, 0), new Vector2(h * 0.35f, h * 0.6f), 1f);
            PlaceRamp(parent, new Vector3(-h * 0.45f + h * 0.35f / 2f + 1.5f, 0, 0), 2.5f, 2.5f, 1f, -Mathf.Pi / 2f);

            // Mid tier (north) — height 1.8
            PlaceRaisedPlatform(parent, new Vector3(0, 0, -h * 0.45f), new Vector2(h * 0.4f, h * 0.3f), 1.8f);
            PlaceRamp(parent, new Vector3(0, 0, -h * 0.45f + h * 0.3f / 2f + 1.5f), 2.5f, 3f, 1.8f, Mathf.Pi);

            // High tier (east) — height 2.5
            PlaceRaisedPlatform(parent, new Vector3(h * 0.4f, 0, h * 0.2f), new Vector2(h * 0.3f, h * 0.35f), 2.5f);
            PlaceRamp(parent, new Vector3(h * 0.4f - h * 0.3f / 2f - 1.5f, 0, h * 0.2f), 2.5f, 3.5f, 2.5f, Mathf.Pi / 2f);

            // Bridge connecting mid and high tiers
            PlaceBridge(parent, new Vector3(h * 0.2f, 0, -h * 0.15f), h * 0.3f, 1.8f, Mathf.Pi / 4f);

            // Ground level cover
            PlaceLowWall(parent, new Vector3(-h * 0.1f, 0, h * 0.3f), 3f, 0f);
            PlaceCrate(parent, new Vector3(h * 0.1f, 0, h * 0.5f), 0.6f, rng);

            // Lighting at each tier
            AddCeilingLight(parent, new Vector3(-h * 0.45f, 4f, 0), new Color(0.7f, 0.8f, 1f), 1.2f, 7f);
            AddCeilingLight(parent, new Vector3(0, 4f, -h * 0.45f), new Color(1f, 0.9f, 0.7f), 1.5f, 7f);
            AddCeilingLight(parent, new Vector3(h * 0.4f, 4f, h * 0.2f), new Color(1f, 0.7f, 0.4f), 1.8f, 8f);
        }

        // ── Mood Variant Builders ──
        // These add atmospheric elements on top of the base layout

        private static void BuildDarkMood(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Dim the room, add glowing floor strips
            AddFloorStrip(parent, new Vector3(-h * 0.7f, 0, 0), new Vector3(0.2f, 0.02f, h * 1.4f), new Color(0.2f, 0.5f, 0.9f));
            AddFloorStrip(parent, new Vector3(h * 0.7f, 0, 0), new Vector3(0.2f, 0.02f, h * 1.4f), new Color(0.2f, 0.5f, 0.9f));
            AddFloorStrip(parent, new Vector3(0, 0, -h * 0.7f), new Vector3(h * 1.4f, 0.02f, 0.2f), new Color(0.2f, 0.5f, 0.9f));
            AddFloorStrip(parent, new Vector3(0, 0, h * 0.7f), new Vector3(h * 1.4f, 0.02f, 0.2f), new Color(0.2f, 0.5f, 0.9f));
        }

        private static void BuildRedAlertMood(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Red emergency lights in corners
            AddCeilingLight(parent, new Vector3(-h * 0.6f, 4f, -h * 0.6f), new Color(0.9f, 0.1f, 0.05f), 1.5f, 8f);
            AddCeilingLight(parent, new Vector3(h * 0.6f, 4f, h * 0.6f), new Color(0.9f, 0.1f, 0.05f), 1.5f, 8f);
            // Warning stripes on floor
            for (int i = -2; i <= 2; i++)
            {
                AddFloorStrip(parent, new Vector3(i * h * 0.3f, 0, 0),
                    new Vector3(0.4f, 0.02f, h * 1.2f), new Color(0.9f, 0.2f, 0.05f));
            }
        }

        private static void BuildOvergrownMood(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Green-tinted floor patches
            for (int i = 0; i < 5; i++)
            {
                float x = rng.RandfRange(-h * 0.7f, h * 0.7f);
                float z = rng.RandfRange(-h * 0.7f, h * 0.7f);
                float patchSize = rng.RandfRange(1.5f, 3.5f);
                var patch = new MeshInstance3D();
                patch.Mesh = new CylinderMesh { TopRadius = patchSize, BottomRadius = patchSize, Height = 0.03f, RadialSegments = 8 };
                patch.Position = new Vector3(x, 0.01f, z);
                var mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.15f, 0.3f + rng.RandfRange(0, 0.15f), 0.1f, 0.6f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    Roughness = 0.9f
                };
                patch.MaterialOverride = mat;
                parent.AddChild(patch);
            }
            // Vine-like vertical strips on walls
            for (int i = 0; i < 3; i++)
            {
                float x = rng.RandfRange(-h * 0.8f, h * 0.8f);
                var vine = new MeshInstance3D();
                vine.Mesh = new BoxMesh { Size = new Vector3(0.15f, 4f, 0.1f) };
                vine.Position = new Vector3(x, 2f, -h + 0.3f);
                var vineMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.1f, 0.35f, 0.08f),
                    Roughness = 0.85f
                };
                vine.MaterialOverride = vineMat;
                parent.AddChild(vine);
            }
        }

        private static void BuildFrozenMood(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Ice crystal decorations
            for (int i = 0; i < 4; i++)
            {
                float x = rng.RandfRange(-h * 0.6f, h * 0.6f);
                float z = rng.RandfRange(-h * 0.6f, h * 0.6f);
                if (!IsClearOfCenter(x, z, 2.5f)) continue;
                float crystalH = rng.RandfRange(0.8f, 2f);
                var crystal = new MeshInstance3D();
                crystal.Mesh = new PrismMesh { Size = new Vector3(0.4f, crystalH, 0.4f) };
                crystal.Position = new Vector3(x, crystalH / 2f, z);
                crystal.RotateY(rng.RandfRange(0, Mathf.Tau));
                crystal.RotateX(rng.RandfRange(-0.15f, 0.15f));
                var mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.6f, 0.8f, 1f, 0.7f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    EmissionEnabled = true,
                    Emission = new Color(0.4f, 0.6f, 0.9f),
                    EmissionEnergyMultiplier = 0.8f,
                    Metallic = 0.3f, Roughness = 0.2f
                };
                crystal.MaterialOverride = mat;
                parent.AddChild(crystal);
            }
            // Frost floor patches
            for (int i = 0; i < 3; i++)
            {
                float x = rng.RandfRange(-h * 0.5f, h * 0.5f);
                float z = rng.RandfRange(-h * 0.5f, h * 0.5f);
                AddFloorStrip(parent, new Vector3(x, 0, z),
                    new Vector3(rng.RandfRange(2f, 4f), 0.02f, rng.RandfRange(2f, 4f)),
                    new Color(0.5f, 0.7f, 0.95f));
            }
        }

        private static void BuildToxicMood(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Toxic puddles
            for (int i = 0; i < 3; i++)
            {
                float x = rng.RandfRange(-h * 0.5f, h * 0.5f);
                float z = rng.RandfRange(-h * 0.5f, h * 0.5f);
                if (!IsClearOfCenter(x, z, 2f)) continue;
                float poolR = rng.RandfRange(1f, 2.5f);
                var pool = new MeshInstance3D();
                pool.Mesh = new CylinderMesh { TopRadius = poolR, BottomRadius = poolR, Height = 0.04f, RadialSegments = 10 };
                pool.Position = new Vector3(x, 0.02f, z);
                var mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.2f, 0.5f, 0.05f, 0.7f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    EmissionEnabled = true,
                    Emission = new Color(0.3f, 0.8f, 0.1f),
                    EmissionEnergyMultiplier = 0.5f
                };
                pool.MaterialOverride = mat;
                parent.AddChild(pool);
            }
            // Warning sign floor markings
            AddFloorStrip(parent, new Vector3(0, 0, -h * 0.6f),
                new Vector3(3f, 0.02f, 0.5f), new Color(0.9f, 0.8f, 0.1f));
            AddFloorStrip(parent, new Vector3(0, 0, h * 0.6f),
                new Vector3(3f, 0.02f, 0.5f), new Color(0.9f, 0.8f, 0.1f));
        }

        private static void BuildScorchedMood(Node3D parent, Vector2 size, RandomNumberGenerator rng, SectorData sector)
        {
            float h = size.X / 2f;
            // Scorch marks (dark floor patches)
            for (int i = 0; i < 4; i++)
            {
                float x = rng.RandfRange(-h * 0.6f, h * 0.6f);
                float z = rng.RandfRange(-h * 0.6f, h * 0.6f);
                float scorchSize = rng.RandfRange(1.5f, 3f);
                var scorch = new MeshInstance3D();
                scorch.Mesh = new CylinderMesh { TopRadius = scorchSize, BottomRadius = scorchSize, Height = 0.02f, RadialSegments = 8 };
                scorch.Position = new Vector3(x, 0.01f, z);
                var mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.08f, 0.06f, 0.04f),
                    Roughness = 0.95f
                };
                scorch.MaterialOverride = mat;
                parent.AddChild(scorch);
            }
            // Ember glow from cracks
            AddFloorStrip(parent, new Vector3(rng.RandfRange(-3f, 3f), 0, rng.RandfRange(-3f, 3f)),
                new Vector3(rng.RandfRange(3f, 6f), 0.02f, 0.3f), new Color(0.9f, 0.3f, 0.05f));
            AddFloorStrip(parent, new Vector3(rng.RandfRange(-3f, 3f), 0, rng.RandfRange(-3f, 3f)),
                new Vector3(0.3f, 0.02f, rng.RandfRange(3f, 6f)), new Color(0.9f, 0.3f, 0.05f));
        }
    }
}
