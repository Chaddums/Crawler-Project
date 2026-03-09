using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Builds the massive AXIS boss visual — a towering upper body (chest up) that
    /// looms over the arena. Only head, torso, and two floating hands are visible.
    /// Hands animate independently for attacks. Head has a glowing visor eye.
    /// The body rises from a glowing rift in the floor.
    /// </summary>
    public static class AxisBossBody
    {
        // Colors
        private static readonly Color CorePurple = new(0.35f, 0.15f, 0.55f);
        private static readonly Color DarkPlate = new(0.12f, 0.1f, 0.18f);
        private static readonly Color HoloBlue = new(0.3f, 0.7f, 1f);
        private static readonly Color EyeRed = new(1f, 0.15f, 0.1f);
        private static readonly Color Gold = new(0.85f, 0.7f, 0.2f);
        private static readonly Color DataGreen = new(0.2f, 1f, 0.4f);

        /// <summary>
        /// Build the complete AXIS upper-body boss model.
        /// Returns a Node3D with named parts: "Head", "Torso", "LeftHand", "RightHand", "Rift".
        /// Total height ~12 units (looms above the arena).
        /// </summary>
        public static Node3D Build()
        {
            var root = new Node3D();
            root.Name = "AxisBossBody";

            root.AddChild(BuildRift());
            root.AddChild(BuildTorso());
            root.AddChild(BuildHead());
            root.AddChild(BuildLeftHand());
            root.AddChild(BuildRightHand());
            root.AddChild(BuildAmbientEffects());

            return root;
        }

        private static Node3D BuildTorso()
        {
            var torso = new Node3D();
            torso.Name = "Torso";
            torso.Position = new Vector3(0, 3f, 0);

            // Main chest — broad, imposing
            var chest = CreateMesh("_Chest",
                new BoxMesh { Size = new Vector3(4f, 3f, 2f) },
                DarkPlate, Vector3.Zero);
            torso.AddChild(chest);

            // Armor plates layered on front
            for (int i = 0; i < 3; i++)
            {
                float y = 0.8f - i * 0.7f;
                float w = 3.5f - i * 0.3f;
                torso.AddChild(CreateMesh($"_ArmorPlate{i}",
                    new BoxMesh { Size = new Vector3(w, 0.4f, 0.3f) },
                    CorePurple.Darkened(i * 0.1f), new Vector3(0, y, -1.1f)));
            }

            // Central core gem (glowing)
            torso.AddChild(CreateGlow("_CoreGem",
                new SphereMesh { Radius = 0.4f, Height = 0.8f, RadialSegments = 12, Rings = 6 },
                HoloBlue, new Vector3(0, 0.3f, -1.2f), 3f));

            // Pulsing ring around core
            torso.AddChild(CreateGlow("_CoreRing",
                new TorusMesh { InnerRadius = 0.5f, OuterRadius = 0.65f, Rings = 12, RingSegments = 16 },
                HoloBlue, new Vector3(0, 0.3f, -1.15f), 1.5f));

            // Shoulder pylons
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 2.3f;
                torso.AddChild(CreateMesh($"_Shoulder{(side < 0 ? "L" : "R")}",
                    new BoxMesh { Size = new Vector3(1.2f, 1.5f, 1.4f) },
                    DarkPlate.Lightened(0.05f), new Vector3(x, 0.8f, 0)));

                // Shoulder glow strip
                torso.AddChild(CreateGlow($"_ShoulderGlow{(side < 0 ? "L" : "R")}",
                    new BoxMesh { Size = new Vector3(0.1f, 1.2f, 0.1f) },
                    CorePurple, new Vector3(x, 0.8f, -0.75f), 2f));
            }

            // Status lights across chest (like a server rack)
            for (int i = 0; i < 5; i++)
            {
                float x = -1f + i * 0.5f;
                var color = i % 2 == 0 ? DataGreen : EyeRed;
                torso.AddChild(CreateGlow($"_StatusLight{i}",
                    new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                    color, new Vector3(x, -0.5f, -1.15f), 2f));
            }

            // Collar/neck area
            torso.AddChild(CreateMesh("_Neck",
                new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.8f, Height = 1f, RadialSegments = 10 },
                DarkPlate, new Vector3(0, 2f, 0)));

            return torso;
        }

        private static Node3D BuildHead()
        {
            var head = new Node3D();
            head.Name = "Head";
            head.Position = new Vector3(0, 7f, 0);

            // Main skull — angular, intimidating
            head.AddChild(CreateMesh("_Skull",
                new BoxMesh { Size = new Vector3(1.8f, 1.4f, 1.6f) },
                DarkPlate, Vector3.Zero));

            // Face plate — slightly forward
            head.AddChild(CreateMesh("_FacePlate",
                new BoxMesh { Size = new Vector3(1.6f, 1.0f, 0.3f) },
                CorePurple.Darkened(0.15f), new Vector3(0, -0.1f, -0.9f)));

            // THE EYE — single wide visor, menacing red glow
            head.AddChild(CreateGlow("_Visor",
                new BoxMesh { Size = new Vector3(1.2f, 0.25f, 0.1f) },
                EyeRed, new Vector3(0, 0.05f, -1.05f), 5f));

            // Scanning eye that moves (smaller bright dot inside visor)
            head.AddChild(CreateGlow("_EyePupil",
                new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 8, Rings = 4 },
                EyeRed, new Vector3(0, 0.05f, -1.1f), 8f));

            // Crown/antenna spikes
            for (int i = 0; i < 5; i++)
            {
                float x = -0.6f + i * 0.3f;
                float h = 0.6f + (i == 2 ? 0.4f : 0f); // center spike tallest
                head.AddChild(CreateGlow($"_Spike{i}",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.08f, Height = h, RadialSegments = 4 },
                    Gold, new Vector3(x, 0.7f + h / 2f, 0), 1.5f));
            }

            // Side data antennae
            for (int side = -1; side <= 1; side += 2)
            {
                head.AddChild(CreateMesh($"_Antenna{(side < 0 ? "L" : "R")}",
                    new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.06f, Height = 0.8f, RadialSegments = 4 },
                    DarkPlate, new Vector3(side * 1.1f, 0.2f, 0)));

                head.AddChild(CreateGlow($"_AntennaTip{(side < 0 ? "L" : "R")}",
                    new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                    HoloBlue, new Vector3(side * 1.1f, 0.65f, 0), 3f));
            }

            return head;
        }

        private static Node3D BuildLeftHand()
        {
            return BuildHand("LeftArm", new Vector3(-5f, 4.5f, -2f));
        }

        private static Node3D BuildRightHand()
        {
            return BuildHand("RightArm", new Vector3(5f, 4.5f, -2f));
        }

        private static Node3D BuildHand(string name, Vector3 position)
        {
            var hand = new Node3D();
            hand.Name = name;
            hand.Position = position;

            bool isLeft = name.Contains("Left");
            float mirror = isLeft ? 1f : -1f;

            // Palm — large flat slab
            hand.AddChild(CreateMesh("_Palm",
                new BoxMesh { Size = new Vector3(1.8f, 0.5f, 2f) },
                DarkPlate, Vector3.Zero));

            // Palm glow circle (attack telegraph)
            hand.AddChild(CreateGlow("_PalmGlow",
                new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.05f, RadialSegments = 12 },
                CorePurple, new Vector3(0, -0.28f, 0), 2f));

            // Fingers — 4 thick articulated digits
            for (int i = 0; i < 4; i++)
            {
                float x = -0.6f + i * 0.4f;

                // Proximal segment
                hand.AddChild(CreateMesh($"_Finger{i}A",
                    new BoxMesh { Size = new Vector3(0.3f, 0.35f, 0.7f) },
                    DarkPlate.Lightened(0.03f), new Vector3(x, 0, -1.2f)));

                // Distal segment (tip)
                hand.AddChild(CreateMesh($"_Finger{i}B",
                    new BoxMesh { Size = new Vector3(0.25f, 0.3f, 0.5f) },
                    DarkPlate.Lightened(0.06f), new Vector3(x, 0, -1.7f)));

                // Finger joint glow
                hand.AddChild(CreateGlow($"_FingerGlow{i}",
                    new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                    CorePurple, new Vector3(x, 0, -0.85f), 1.5f));
            }

            // Thumb
            hand.AddChild(CreateMesh("_Thumb",
                new BoxMesh { Size = new Vector3(0.35f, 0.35f, 0.6f) },
                DarkPlate.Lightened(0.03f), new Vector3(mirror * 1.1f, 0, -0.5f)));

            // Wrist connector (emissive ring)
            hand.AddChild(CreateGlow("_WristRing",
                new TorusMesh { InnerRadius = 0.4f, OuterRadius = 0.55f, Rings = 8, RingSegments = 12 },
                HoloBlue, new Vector3(0, 0, 1.2f), 2f));

            // Floating particle emitter on palm (for energy attacks)
            var palmLight = new OmniLight3D();
            palmLight.Name = "_PalmLight";
            palmLight.LightColor = CorePurple;
            palmLight.LightEnergy = 2f;
            palmLight.OmniRange = 4f;
            palmLight.Position = new Vector3(0, -0.5f, 0);
            hand.AddChild(palmLight);

            return hand;
        }

        private static Node3D BuildRift()
        {
            // Glowing rift at the base where AXIS emerges from
            var rift = new Node3D();
            rift.Name = "Rift";
            rift.Position = new Vector3(0, 0.1f, 0);

            // Large glowing disc on the ground
            var disc = CreateGlow("_RiftDisc",
                new CylinderMesh { TopRadius = 5f, BottomRadius = 5f, Height = 0.1f, RadialSegments = 24 },
                CorePurple, Vector3.Zero, 1.5f);
            disc.Rotation = new Vector3(0, 0, 0);
            rift.AddChild(disc);

            // Inner ring (brighter)
            rift.AddChild(CreateGlow("_RiftInner",
                new TorusMesh { InnerRadius = 2f, OuterRadius = 2.5f, Rings = 16, RingSegments = 24 },
                HoloBlue, new Vector3(0, 0.15f, 0), 3f));

            // Outer ring
            rift.AddChild(CreateGlow("_RiftOuter",
                new TorusMesh { InnerRadius = 4f, OuterRadius = 4.3f, Rings = 16, RingSegments = 24 },
                CorePurple, new Vector3(0, 0.12f, 0), 1.5f));

            // Rising energy particles
            var particles = new GpuParticles3D();
            particles.Name = "_RiftParticles";
            particles.Amount = 30;
            particles.Lifetime = 3f;
            particles.Preprocess = 1f;

            var mat = new ParticleProcessMaterial();
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
            mat.EmissionRingRadius = 4f;
            mat.EmissionRingInnerRadius = 1f;
            mat.EmissionRingHeight = 0.1f;
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 10f;
            mat.InitialVelocityMin = 2f;
            mat.InitialVelocityMax = 5f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.1f;
            mat.ScaleMax = 0.3f;
            mat.Color = new Color(0.5f, 0.3f, 0.9f, 0.7f);
            particles.ProcessMaterial = mat;

            var mesh = new SphereMesh();
            mesh.Radius = 0.1f;
            mesh.Height = 0.2f;
            mesh.RadialSegments = 4;
            mesh.Rings = 2;
            var meshMat = new StandardMaterial3D();
            meshMat.AlbedoColor = CorePurple;
            meshMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            meshMat.EmissionEnabled = true;
            meshMat.Emission = CorePurple;
            meshMat.EmissionEnergyMultiplier = 3f;
            mesh.Material = meshMat;
            particles.DrawPass1 = mesh;

            rift.AddChild(particles);

            return rift;
        }

        private static Node3D BuildAmbientEffects()
        {
            var effects = new Node3D();
            effects.Name = "AmbientEffects";

            // Floating data streams around the body
            for (int i = 0; i < 6; i++)
            {
                float angle = (float)i / 6f * Mathf.Tau;
                float radius = 3.5f;
                var stream = CreateGlow($"_DataStream{i}",
                    new BoxMesh { Size = new Vector3(0.05f, 3f + i * 0.3f, 0.05f) },
                    DataGreen, new Vector3(
                        Mathf.Cos(angle) * radius,
                        4f + i * 0.5f,
                        Mathf.Sin(angle) * radius), 1.5f);
                effects.AddChild(stream);
            }

            // Orbiting point lights for dramatic lighting
            for (int i = 0; i < 3; i++)
            {
                var light = new OmniLight3D();
                light.Name = $"_OrbitLight{i}";
                float angle = (float)i / 3f * Mathf.Tau;
                light.Position = new Vector3(Mathf.Cos(angle) * 4f, 5f, Mathf.Sin(angle) * 4f);
                light.LightColor = CorePurple;
                light.LightEnergy = 1.5f;
                light.OmniRange = 8f;
                effects.AddChild(light);
            }

            return effects;
        }

        // --- Mesh helpers ---

        private static MeshInstance3D CreateMesh(string name, Mesh mesh, Color color, Vector3 position)
        {
            var node = new MeshInstance3D();
            node.Name = name;
            node.Mesh = mesh;
            node.Position = position;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            node.MaterialOverride = mat;

            return node;
        }

        private static MeshInstance3D CreateGlow(string name, Mesh mesh, Color color, Vector3 position, float emissionStrength)
        {
            var node = new MeshInstance3D();
            node.Name = name;
            node.Mesh = mesh;
            node.Position = position;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = emissionStrength;
            node.MaterialOverride = mat;

            return node;
        }
    }
}
