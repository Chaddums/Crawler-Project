using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Static factory building character bodies and weapons.
    /// Tries to load 3D model assets first via ModelLibrary; falls back to procedural geometry.
    /// </summary>
    public static class CharacterMeshBuilder
    {
        // ── Player Body ──

        // Player model target height — sized to feel small relative to imposing rooms/walls.
        // 1.8 units keeps proportions consistent with saved character configs.
        private const float PlayerModelHeight = 1.8f;

        // ── Color Variant Textures ──
        // Maps each BotFrameType to its base mech texture. Frames sharing the same
        // model get the same texture, then color tinting differentiates them.
        private static readonly Dictionary<BotFrameType, string> FrameTextureMap = new()
        {
            { BotFrameType.TinCan,     "Stan_Texture" },       // Stan model
            { BotFrameType.NoiseBox,   "Stan_Texture" },       // Stan model (tinted)
            { BotFrameType.Scrapheap,  "George_Texture" },     // George model
            { BotFrameType.Clunker,    "George_Texture" },     // George model (tinted)
            { BotFrameType.SparkPlug,  "Leela_Texture" },      // Leela model
            { BotFrameType.RustBucket, "Leela_Texture" },      // Leela model (tinted steel blue)
        };

        private static readonly Dictionary<string, Texture2D> _textureCache = new();

        public static Node3D BuildPlayerBody(BotFrameType className)
        {
            // Try loading the Quaternius animated mech model for this frame
            string frameId = className.ToString().ToLower();
            var model = ModelLibrary.TryLoad("player", frameId);
            if (model != null)
            {
                // Check if model has a renderable mesh (not just an empty node)
                bool hasMesh = false;
                void CheckMesh(Node n) { if (n is MeshInstance3D mi && mi.Mesh != null) hasMesh = true; foreach (var c in n.GetChildren()) if (c is Node cn) CheckMesh(cn); }
                CheckMesh(model);

                if (hasMesh)
                {
                    ScaleModelToFit(model, PlayerModelHeight);
                    // FBX models face +Z (Blender convention) but Godot's LookAt targets -Z
                    model.RotateY(Mathf.Pi);
                    GD.Print($"[CharacterMeshBuilder] Loaded player model '{frameId}' from mech FBX");

                    // Apply color variant texture so shared models look distinct
                    ApplyFrameColorVariant(model, className);

                    // Wire up animator if AnimationPlayer exists
                    var animPlayer = FindAnimationPlayer(model);
                    if (animPlayer != null)
                        GD.Print($"[CharacterMeshBuilder] Player '{frameId}' has AnimationPlayer with {animPlayer.GetAnimationList().Length} anims");

                    // Map FBX bones to game pivots FIRST so detail pieces can attach
                    FbxPivotMapper.MapHierarchy(model);

                    // Attach default weapon to the WeaponMount created by the mapper
                    var weaponMount = FbxPivotMapper.FindNodeRecursive(model, "WeaponMount") as Marker3D;
                    if (weaponMount != null)
                    {
                        var weapon = BuildWeapon(className);
                        if (weapon != null)
                        {
                            weapon.Position = Vector3.Zero;
                            weaponMount.AddChild(weapon);
                        }
                    }

                    CharacterConfigLoader.ApplyPartOverrides(model, className);
                    CharacterConfigLoader.SpawnDetailPieces(model, className);

                    return model;
                }
                else
                {
                    GD.Print($"[CharacterMeshBuilder] Player model '{frameId}' loaded but has no mesh — falling back to procedural");
                    model.QueueFree();
                }
            }

            // Procedural fallback
            var procedural = BuildJunkbotBody(className);
            ScaleModelToFit(procedural, PlayerModelHeight);

            // Apply editor part overrides (position/rotation/color tweaks from Characters tab)
            CharacterConfigLoader.ApplyPartOverrides(procedural, className);

            // Spawn editor-placed detail pieces (bolts, rivets, plates, etc.)
            CharacterConfigLoader.SpawnDetailPieces(procedural, className);

            return procedural;
        }

        // ── Weapons ──

        public static Node3D BuildWeapon(BotFrameType className)
        {
            // Try model asset first
            string weaponId = className.ToString().ToLower() + "_blaster";
            var model = ModelLibrary.TryLoad("weapon", weaponId);
            if (model != null)
            {
                model.Name = "Weapon";
                ScaleModelToFit(model, 0.5f);
                return model;
            }

            // Procedural blaster fallback (all classes)
            return BuildBlaster(className);
        }

        private static Node3D BuildBlaster(BotFrameType className)
        {
            return className switch
            {
                BotFrameType.Scrapheap => BuildHeavyCannon(className),
                BotFrameType.SparkPlug => BuildArcCaster(className),
                BotFrameType.RustBucket => BuildNeedler(className),
                BotFrameType.NoiseBox => BuildPulseEmitter(className),
                BotFrameType.Clunker => BuildRivetGun(className),
                _ => BuildStandardBlaster(className) // TinCan default
            };
        }

        private static Node3D BuildStandardBlaster(BotFrameType className)
        {
            var root = new Node3D();
            root.Name = "Weapon";
            Color classColor = GetClassColor(className);
            Color darkMetal = new Color(0.25f, 0.25f, 0.27f);
            Color medMetal = new Color(0.4f, 0.4f, 0.42f);

            // Barrel
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.35f, RadialSegments = 8 },
                darkMetal, new Vector3(0, 0, -0.175f));
            barrel.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrel);

            // Body
            root.AddChild(CreateMeshNode("Body",
                new BoxMesh { Size = new Vector3(0.08f, 0.12f, 0.06f) },
                medMetal, new Vector3(0, 0, 0.05f)));

            // Grip
            root.AddChild(CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.04f, 0.08f, 0.04f) },
                darkMetal, new Vector3(0, -0.1f, 0.05f)));

            // Muzzle tip
            root.AddChild(CreateEmissiveMeshNode("Muzzle",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                classColor, classColor, new Vector3(0, 0, -0.36f)));

            return root;
        }

        private static Node3D BuildHeavyCannon(BotFrameType className)
        {
            var root = new Node3D();
            root.Name = "Weapon";
            Color classColor = GetClassColor(className);
            Color hull = new Color(0.3f, 0.28f, 0.22f);
            Color darkMetal = new Color(0.2f, 0.2f, 0.22f);

            // Fat short barrel
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.07f, Height = 0.28f, RadialSegments = 10 },
                darkMetal, new Vector3(0, 0, -0.14f));
            barrel.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrel);

            // Muzzle brake ring
            var brake = CreateMeshNode("MuzzleBrake",
                new TorusMesh { InnerRadius = 0.055f, OuterRadius = 0.075f, Rings = 10, RingSegments = 6 },
                hull, new Vector3(0, 0, -0.29f));
            root.AddChild(brake);

            // Boxy receiver
            root.AddChild(CreateMeshNode("Receiver",
                new BoxMesh { Size = new Vector3(0.14f, 0.14f, 0.1f) },
                hull, new Vector3(0, 0, 0.04f)));

            // Ammo box underneath
            root.AddChild(CreateMeshNode("AmmoBox",
                new BoxMesh { Size = new Vector3(0.1f, 0.08f, 0.08f) },
                darkMetal, new Vector3(0, -0.1f, 0.02f)));

            // Top rail
            root.AddChild(CreateMeshNode("Rail",
                new BoxMesh { Size = new Vector3(0.03f, 0.02f, 0.2f) },
                hull.Lightened(0.1f), new Vector3(0, 0.08f, -0.06f)));

            // Grip
            root.AddChild(CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.05f, 0.08f, 0.05f) },
                darkMetal, new Vector3(0, -0.1f, 0.06f)));

            // Muzzle glow
            root.AddChild(CreateEmissiveMeshNode("Muzzle",
                new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 4 },
                classColor, classColor, new Vector3(0, 0, -0.3f)));

            return root;
        }

        private static Node3D BuildArcCaster(BotFrameType className)
        {
            var root = new Node3D();
            root.Name = "Weapon";
            Color classColor = GetClassColor(className);
            Color frame = new Color(0.3f, 0.28f, 0.38f);
            Color energy = new Color(0.6f, 0.3f, 1f);

            // Thin barrel with coil wraps
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.32f, RadialSegments = 6 },
                frame, new Vector3(0, 0, -0.16f));
            barrel.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrel);

            // Energy coil rings (3)
            for (int i = 0; i < 3; i++)
            {
                var coil = CreateEmissiveMeshNode($"Coil{i}",
                    new TorusMesh { InnerRadius = 0.025f, OuterRadius = 0.04f, Rings = 8, RingSegments = 6 },
                    energy, energy, new Vector3(0, 0, -0.06f - i * 0.08f));
                root.AddChild(coil);
            }

            // Compact body
            root.AddChild(CreateMeshNode("Body",
                new BoxMesh { Size = new Vector3(0.06f, 0.08f, 0.08f) },
                frame, new Vector3(0, 0, 0.04f)));

            // Conduit grip
            root.AddChild(CreateMeshNode("Grip",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.025f, Height = 0.08f, RadialSegments = 6 },
                frame.Darkened(0.15f), new Vector3(0, -0.06f, 0.04f)));

            // Emissive orb at tip
            root.AddChild(CreateEmissiveMeshNode("OrbTip",
                new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 8, Rings = 4 },
                energy, energy, new Vector3(0, 0, -0.34f)));

            return root;
        }

        private static Node3D BuildNeedler(BotFrameType className)
        {
            var root = new Node3D();
            root.Name = "Weapon";
            Color classColor = GetClassColor(className);
            Color gunMetal = new Color(0.22f, 0.22f, 0.26f);
            Color suppressor = new Color(0.15f, 0.15f, 0.18f);

            // Long thin barrel with suppressor
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.022f, Height = 0.2f, RadialSegments = 6 },
                gunMetal, new Vector3(0, 0, -0.1f));
            barrel.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrel);

            // Suppressor (wider cylinder over barrel tip)
            var supp = CreateMeshNode("Suppressor",
                new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.035f, Height = 0.12f, RadialSegments = 8 },
                suppressor, new Vector3(0, 0, -0.22f));
            supp.RotateX(Mathf.DegToRad(90));
            root.AddChild(supp);

            // Slim receiver
            root.AddChild(CreateMeshNode("Receiver",
                new BoxMesh { Size = new Vector3(0.05f, 0.06f, 0.08f) },
                gunMetal, new Vector3(0, 0, 0.02f)));

            // Needle magazine (thin box on top)
            root.AddChild(CreateMeshNode("Magazine",
                new BoxMesh { Size = new Vector3(0.025f, 0.06f, 0.04f) },
                gunMetal.Lightened(0.08f), new Vector3(0, 0.05f, 0.01f)));

            // Minimal grip
            root.AddChild(CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.03f, 0.06f, 0.03f) },
                suppressor, new Vector3(0, -0.05f, 0.03f)));

            // Dim green muzzle dot
            root.AddChild(CreateEmissiveMeshNode("Muzzle",
                new SphereMesh { Radius = 0.012f, Height = 0.024f, RadialSegments = 6, Rings = 3 },
                classColor, classColor, new Vector3(0, 0, -0.29f)));

            return root;
        }

        private static Node3D BuildPulseEmitter(BotFrameType className)
        {
            var root = new Node3D();
            root.Name = "Weapon";
            Color classColor = GetClassColor(className);
            Color frame = new Color(0.32f, 0.3f, 0.38f);
            Color glow = new Color(0.6f, 0.3f, 0.8f);

            // Wide cone barrel (speaker cone shape)
            var cone = CreateMeshNode("Cone",
                new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.03f, Height = 0.2f, RadialSegments = 10 },
                frame, new Vector3(0, 0, -0.1f));
            cone.RotateX(Mathf.DegToRad(90));
            root.AddChild(cone);

            // Emissive ring at the mouth
            var ring = CreateEmissiveMeshNode("EmitRing",
                new TorusMesh { InnerRadius = 0.05f, OuterRadius = 0.065f, Rings = 10, RingSegments = 6 },
                glow, glow, new Vector3(0, 0, -0.21f));
            root.AddChild(ring);

            // Compact boxy body
            root.AddChild(CreateMeshNode("Body",
                new BoxMesh { Size = new Vector3(0.07f, 0.08f, 0.08f) },
                frame, new Vector3(0, 0, 0.02f)));

            // Sound cell (cylinder on side)
            var cell = CreateMeshNode("SoundCell",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.06f, RadialSegments = 6 },
                frame.Lightened(0.1f), new Vector3(0.045f, 0, 0));
            cell.RotateZ(Mathf.DegToRad(90));
            root.AddChild(cell);

            // Grip
            root.AddChild(CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.035f, 0.07f, 0.035f) },
                frame.Darkened(0.1f), new Vector3(0, -0.06f, 0.03f)));

            // Emissive center dot in cone
            root.AddChild(CreateEmissiveMeshNode("PulseDot",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                glow, glow, new Vector3(0, 0, -0.15f)));

            return root;
        }

        private static Node3D BuildRivetGun(BotFrameType className)
        {
            var root = new Node3D();
            root.Name = "Weapon";
            Color classColor = GetClassColor(className);
            Color iron = new Color(0.38f, 0.35f, 0.3f);
            Color darkMetal = new Color(0.22f, 0.2f, 0.18f);
            Color brass = new Color(0.65f, 0.5f, 0.2f);

            // Stubby wide barrel
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.045f, Height = 0.18f, RadialSegments = 8 },
                darkMetal, new Vector3(0, 0, -0.09f));
            barrel.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrel);

            // Chunky receiver
            root.AddChild(CreateMeshNode("Receiver",
                new BoxMesh { Size = new Vector3(0.1f, 0.12f, 0.1f) },
                iron, new Vector3(0, 0, 0.03f)));

            // Top-mounted rivet magazine (vertical box)
            root.AddChild(CreateMeshNode("Magazine",
                new BoxMesh { Size = new Vector3(0.04f, 0.12f, 0.05f) },
                darkMetal, new Vector3(0, 0.1f, 0.01f)));

            // Magazine cap
            root.AddChild(CreateMeshNode("MagCap",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.015f, RadialSegments = 6 },
                brass, new Vector3(0, 0.165f, 0.01f)));

            // Pneumatic piston on side
            var piston = CreateMeshNode("Piston",
                new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.018f, Height = 0.14f, RadialSegments = 6 },
                iron.Lightened(0.1f), new Vector3(0.055f, 0.02f, -0.02f));
            piston.RotateX(Mathf.DegToRad(90));
            root.AddChild(piston);

            // Grip
            root.AddChild(CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.045f, 0.08f, 0.04f) },
                darkMetal, new Vector3(0, -0.1f, 0.04f)));

            // Muzzle
            root.AddChild(CreateEmissiveMeshNode("Muzzle",
                new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                classColor, classColor, new Vector3(0, 0, -0.19f)));

            return root;
        }

        // ── Junkbot Player Body ──

        private static Node3D BuildJunkbotBody(BotFrameType className)
        {
            return className switch
            {
                BotFrameType.Scrapheap => BuildScrapheapBody(),
                BotFrameType.TinCan => BuildTinCanBody(),
                BotFrameType.SparkPlug => BuildSparkPlugBody(),
                BotFrameType.RustBucket => BuildRustBucketBody(),
                BotFrameType.NoiseBox => BuildNoiseBoxBody(),
                BotFrameType.Clunker => BuildClunkerBody(),
                _ => BuildTinCanBody()
            };
        }

        // ── Shared junkbot parts ──

        private static void AddBinocularHead(Node3D root, Color chassis, Color eyeColor, Vector3 pos,
            float eyeSpacing = 0.08f, float eyeRadius = 0.07f, float lensRadius = 0.055f, float tilt = -5f)
        {
            var headPivot = CreatePivot("Head", pos);
            var neck = CreateMeshNode("_NeckStalk",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 0.2f, RadialSegments = 6 },
                chassis, new Vector3(0, -0.05f, 0));
            headPivot.AddChild(neck);

            // Connecting bar between eyes
            headPivot.AddChild(CreateMeshNode("_EyeBar",
                new BoxMesh { Size = new Vector3(eyeSpacing * 2f + 0.02f, 0.04f, 0.04f) },
                chassis.Darkened(0.1f), new Vector3(0, 0.08f, 0)));

            var leftHousing = CreateMeshNode("_LeftEyeHousing",
                new CylinderMesh { TopRadius = eyeRadius, BottomRadius = eyeRadius, Height = 0.1f, RadialSegments = 8 },
                chassis, new Vector3(-eyeSpacing, 0.08f, 0));
            leftHousing.RotateX(Mathf.DegToRad(90));
            headPivot.AddChild(leftHousing);
            headPivot.AddChild(CreateEmissiveMeshNode("_LeftLens",
                new SphereMesh { Radius = lensRadius, Height = lensRadius * 2, RadialSegments = 8, Rings = 4 },
                eyeColor, eyeColor, new Vector3(-eyeSpacing, 0.08f, -0.06f)));

            var rightHousing = CreateMeshNode("_RightEyeHousing",
                new CylinderMesh { TopRadius = eyeRadius, BottomRadius = eyeRadius, Height = 0.1f, RadialSegments = 8 },
                chassis, new Vector3(eyeSpacing, 0.08f, 0));
            rightHousing.RotateX(Mathf.DegToRad(90));
            headPivot.AddChild(rightHousing);
            headPivot.AddChild(CreateEmissiveMeshNode("_RightLens",
                new SphereMesh { Radius = lensRadius, Height = lensRadius * 2, RadialSegments = 8, Rings = 4 },
                eyeColor, eyeColor, new Vector3(eyeSpacing, 0.08f, -0.06f)));

            headPivot.RotateX(Mathf.DegToRad(tilt));
            root.AddChild(headPivot);
        }

        /// <summary>
        /// Articulated arm with shoulder → elbow → hand hierarchy.
        /// Nested pivots give natural IK-like animation.
        /// Hierarchy: {Side}Arm → {Side}Elbow → {Side}Hand
        /// </summary>
        private static void AddArticulatedArm(Node3D root, string side, Color armColor, Color jointColor,
            Vector3 pivotPos, float upperLen = 0.16f, float forearmLen = 0.14f,
            float thickness = 0.035f, ArmHandStyle handStyle = ArmHandStyle.Clamp)
        {
            var shoulderPivot = CreatePivot($"{side}Arm", pivotPos);

            // Shoulder ball joint
            shoulderPivot.AddChild(CreateMeshNode($"_{side}ShoulderBall",
                new SphereMesh { Radius = thickness * 1.4f, Height = thickness * 2.8f, RadialSegments = 8, Rings = 4 },
                jointColor, Vector3.Zero));

            // Upper arm segment
            shoulderPivot.AddChild(CreateMeshNode($"_{side}UpperArm",
                new CylinderMesh { TopRadius = thickness, BottomRadius = thickness * 0.9f, Height = upperLen, RadialSegments = 6 },
                armColor, new Vector3(0, -upperLen / 2f, 0)));

            // Elbow pivot (nested inside shoulder)
            var elbowPivot = CreatePivot($"{side}Elbow", new Vector3(0, -upperLen, 0));

            // Elbow ball joint
            elbowPivot.AddChild(CreateMeshNode($"_{side}ElbowBall",
                new SphereMesh { Radius = thickness * 1.2f, Height = thickness * 2.4f, RadialSegments = 8, Rings = 4 },
                jointColor, Vector3.Zero));

            // Forearm segment
            elbowPivot.AddChild(CreateMeshNode($"_{side}Forearm",
                new CylinderMesh { TopRadius = thickness * 0.85f, BottomRadius = thickness * 0.75f, Height = forearmLen, RadialSegments = 6 },
                armColor, new Vector3(0, -forearmLen / 2f, 0)));

            // Hand pivot (nested inside elbow)
            var handPivot = CreatePivot($"{side}Hand", new Vector3(0, -forearmLen, 0));

            // Wrist joint
            handPivot.AddChild(CreateMeshNode($"_{side}WristBall",
                new SphereMesh { Radius = thickness * 0.9f, Height = thickness * 1.8f, RadialSegments = 6, Rings = 3 },
                jointColor, Vector3.Zero));

            // Hand geometry based on style
            switch (handStyle)
            {
                case ArmHandStyle.Clamp:
                    BuildClampHand(handPivot, side, armColor, thickness);
                    break;
                case ArmHandStyle.Fist:
                    BuildFistHand(handPivot, side, armColor, jointColor, thickness);
                    break;
                case ArmHandStyle.Claw:
                    BuildClawHand(handPivot, side, armColor, thickness);
                    break;
                case ArmHandStyle.Probe:
                    BuildProbeHand(handPivot, side, armColor, jointColor, thickness);
                    break;
            }

            elbowPivot.AddChild(handPivot);
            shoulderPivot.AddChild(elbowPivot);
            root.AddChild(shoulderPivot);
        }

        private enum ArmHandStyle { Clamp, Fist, Claw, Probe }

        private static void BuildClampHand(Node3D handPivot, string side, Color color, float thickness)
        {
            float clampSize = thickness * 2.5f;
            float clampY = -clampSize * 0.4f;
            var clampA = CreateMeshNode($"_{side}ClampA",
                new BoxMesh { Size = new Vector3(0.025f, clampSize, 0.018f) },
                color, new Vector3(-0.025f, clampY, 0));
            clampA.RotateZ(Mathf.DegToRad(10));
            handPivot.AddChild(clampA);
            var clampB = CreateMeshNode($"_{side}ClampB",
                new BoxMesh { Size = new Vector3(0.025f, clampSize, 0.018f) },
                color, new Vector3(0.025f, clampY, 0));
            clampB.RotateZ(Mathf.DegToRad(-10));
            handPivot.AddChild(clampB);
        }

        private static void BuildFistHand(Node3D handPivot, string side, Color color, Color jointColor, float thickness)
        {
            float fistSize = thickness * 2.8f;
            handPivot.AddChild(CreateMeshNode($"_{side}Fist",
                new BoxMesh { Size = new Vector3(fistSize, fistSize * 0.9f, fistSize * 0.8f) },
                color, new Vector3(0, -fistSize * 0.4f, 0)));
            // Knuckle ridge
            handPivot.AddChild(CreateMeshNode($"_{side}Knuckle",
                new BoxMesh { Size = new Vector3(fistSize * 1.05f, fistSize * 0.2f, 0.01f) },
                jointColor, new Vector3(0, -fistSize * 0.25f, -fistSize * 0.42f)));
        }

        private static void BuildClawHand(Node3D handPivot, string side, Color color, float thickness)
        {
            // 3 tapered claw fingers
            for (int i = -1; i <= 1; i++)
            {
                float angle = i * 18f;
                var finger = CreateMeshNode($"_{side}Finger{i}",
                    new CylinderMesh { TopRadius = 0.005f, BottomRadius = thickness * 0.5f, Height = thickness * 3f, RadialSegments = 4 },
                    color, new Vector3(i * 0.02f, -thickness * 1.2f, 0));
                finger.RotateZ(Mathf.DegToRad(angle));
                handPivot.AddChild(finger);
            }
        }

        private static void BuildProbeHand(Node3D handPivot, string side, Color color, Color glowColor, float thickness)
        {
            // Thin probe rod with emissive tip
            handPivot.AddChild(CreateMeshNode($"_{side}ProbeRod",
                new CylinderMesh { TopRadius = thickness * 0.3f, BottomRadius = thickness * 0.4f, Height = thickness * 3f, RadialSegments = 4 },
                color, new Vector3(0, -thickness * 1.2f, 0)));
            handPivot.AddChild(CreateEmissiveMeshNode($"_{side}ProbeTip",
                new SphereMesh { Radius = thickness * 0.5f, Height = thickness, RadialSegments = 6, Rings = 3 },
                glowColor, glowColor, new Vector3(0, -thickness * 2.8f, 0)));
        }

        // Legacy single-pivot arm for enemies that don't need articulation
        private static void AddClampArm(Node3D root, string side, Color armColor, Vector3 pivotPos,
            float shaftLen = 0.35f, float clampSize = 0.1f)
        {
            float sign = side == "Left" ? -1f : 1f;
            var pivot = CreatePivot($"{side}Arm", pivotPos);
            pivot.AddChild(CreateMeshNode($"_{side}ArmShaft",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.035f, Height = shaftLen, RadialSegments = 6 },
                armColor, new Vector3(0, -shaftLen / 2f, 0)));
            float clampY = -(shaftLen + clampSize * 0.5f);
            var clampA = CreateMeshNode($"_{side}ClampA",
                new BoxMesh { Size = new Vector3(0.03f, clampSize, 0.02f) },
                armColor, new Vector3(-0.03f, clampY, 0));
            clampA.RotateZ(Mathf.DegToRad(10));
            pivot.AddChild(clampA);
            var clampB = CreateMeshNode($"_{side}ClampB",
                new BoxMesh { Size = new Vector3(0.03f, clampSize, 0.02f) },
                armColor, new Vector3(0.03f, clampY, 0));
            clampB.RotateZ(Mathf.DegToRad(-10));
            pivot.AddChild(clampB);
            root.AddChild(pivot);
        }

        private static void AddTracks(Node3D root, Color trackColor, float xOffset = 0.2f)
        {
            var leftLeg = CreatePivot("LeftLeg", new Vector3(-xOffset, 0.25f, 0));
            BuildTrackAssembly(leftLeg, trackColor, false);
            root.AddChild(leftLeg);
            var rightLeg = CreatePivot("RightLeg", new Vector3(xOffset, 0.25f, 0));
            BuildTrackAssembly(rightLeg, trackColor, true);
            root.AddChild(rightLeg);
        }

        private static void AddWeaponMount(Node3D root, BotFrameType className, Vector3 pos)
        {
            // Add a Marker3D as a stable attachment point for weapon swapping
            var mount = new Marker3D();
            mount.Name = "WeaponMount";
            mount.Position = pos;
            root.AddChild(mount);

            var weapon = BuildWeapon(className);
            if (weapon != null)
            {
                weapon.Position = Vector3.Zero; // relative to mount
                mount.AddChild(weapon);
            }
        }

        // ── Scrapheap — squat bulldozer tank, widest + shortest, heavy tracks ──

        private static Node3D BuildScrapheapBody()
        {
            var root = new Node3D();
            root.Name = "PlayerBody";
            Color chassis = new Color(0.35f, 0.3f, 0.25f);
            Color accent = GetClassColor(BotFrameType.Scrapheap);
            Color eyeColor = new Color(1f, 0.6f, 0.15f);
            Color trackColor = new Color(0.18f, 0.15f, 0.12f);
            Color armColor = new Color(0.4f, 0.35f, 0.28f);
            Color jointColor = new Color(0.45f, 0.42f, 0.35f);
            Color plate = new Color(0.32f, 0.28f, 0.22f);

            // Squat single-lens head sunk into shoulders
            var headPivot = CreatePivot("Head", new Vector3(0, 0.95f, -0.02f));
            headPivot.AddChild(CreateMeshNode("_HeadBlock",
                new BoxMesh { Size = new Vector3(0.28f, 0.14f, 0.18f) },
                chassis, Vector3.Zero));
            headPivot.AddChild(CreateEmissiveMeshNode("_Viewport",
                new BoxMesh { Size = new Vector3(0.22f, 0.05f, 0.01f) },
                eyeColor, eyeColor, new Vector3(0, 0.01f, -0.1f)));
            headPivot.AddChild(CreateMeshNode("_BrowPlate",
                new BoxMesh { Size = new Vector3(0.32f, 0.04f, 0.2f) },
                plate, new Vector3(0, 0.08f, -0.01f)));
            headPivot.RotateX(Mathf.DegToRad(-5));
            root.AddChild(headPivot);

            // Very wide, very squat torso — bulldozer profile
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.55f, 0));
            torsoPivot.AddChild(CreateMeshNode("_TorsoBox",
                new BoxMesh { Size = new Vector3(0.72f, 0.4f, 0.45f) },
                chassis, Vector3.Zero));
            torsoPivot.AddChild(CreateMeshNode("_AccentStripe",
                new BoxMesh { Size = new Vector3(0.6f, 0.05f, 0.01f) },
                accent, new Vector3(0, 0, -0.24f)));
            torsoPivot.AddChild(CreateMeshNode("_LeftSkirt",
                new BoxMesh { Size = new Vector3(0.06f, 0.32f, 0.4f) },
                plate, new Vector3(-0.36f, -0.04f, 0)));
            torsoPivot.AddChild(CreateMeshNode("_RightSkirt",
                new BoxMesh { Size = new Vector3(0.06f, 0.32f, 0.4f) },
                plate, new Vector3(0.36f, -0.04f, 0)));
            torsoPivot.AddChild(CreateMeshNode("_DozerBlade",
                new BoxMesh { Size = new Vector3(0.65f, 0.2f, 0.04f) },
                plate.Lightened(0.05f), new Vector3(0, -0.12f, -0.24f)));
            for (float side = -1; side <= 1; side += 2)
            {
                torsoPivot.AddChild(CreateMeshNode(side < 0 ? "_ExhaustL" : "_ExhaustR",
                    new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.04f, Height = 0.22f, RadialSegments = 6 },
                    new Color(0.2f, 0.2f, 0.2f), new Vector3(side * 0.2f, 0.28f, 0.18f)));
            }
            root.AddChild(torsoPivot);

            // Thick articulated arms with heavy clamp hands
            AddArticulatedArm(root, "Left", armColor, jointColor,
                new Vector3(-0.42f, 0.65f, 0), upperLen: 0.14f, forearmLen: 0.12f,
                thickness: 0.045f, handStyle: ArmHandStyle.Clamp);
            AddArticulatedArm(root, "Right", armColor, jointColor,
                new Vector3(0.42f, 0.65f, 0), upperLen: 0.14f, forearmLen: 0.12f,
                thickness: 0.045f, handStyle: ArmHandStyle.Clamp);

            // Extra-wide heavy tracks
            AddTracks(root, trackColor, xOffset: 0.32f);
            AddWeaponMount(root, BotFrameType.Scrapheap, new Vector3(0.52f, 0.7f, -0.2f));
            return root;
        }

        // ── TinCan — balanced military rover, medium build, 4 wheels ──

        private static Node3D BuildTinCanBody()
        {
            var root = new Node3D();
            root.Name = "PlayerBody";
            Color chassis = new Color(0.3f, 0.3f, 0.32f);
            Color accent = GetClassColor(BotFrameType.TinCan);
            Color eyeColor = new Color(0.2f, 0.8f, 1.0f);
            Color wheelColor = new Color(0.15f, 0.15f, 0.17f);
            Color armColor = new Color(0.4f, 0.4f, 0.42f);

            // Standard binocular head
            AddBinocularHead(root, chassis, eyeColor, new Vector3(0, 1.2f, 0));

            // Medium torso with antenna and shield mount
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.7f, 0));
            torsoPivot.AddChild(CreateMeshNode("_TorsoBox",
                new BoxMesh { Size = new Vector3(0.48f, 0.45f, 0.32f) },
                chassis, Vector3.Zero));
            torsoPivot.AddChild(CreateMeshNode("_AccentStripe",
                new BoxMesh { Size = new Vector3(0.4f, 0.05f, 0.01f) },
                accent, new Vector3(0, 0, -0.17f)));
            torsoPivot.AddChild(CreateMeshNode("_Antenna",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.015f, Height = 0.14f, RadialSegments = 4 },
                armColor, new Vector3(0.12f, 0.28f, 0.05f)));
            // Shield mount on left
            torsoPivot.AddChild(CreateMeshNode("_ShieldMount",
                new BoxMesh { Size = new Vector3(0.04f, 0.28f, 0.2f) },
                accent.Lightened(0.15f), new Vector3(-0.27f, 0, -0.04f)));
            // Rear storage box
            torsoPivot.AddChild(CreateMeshNode("_StorageBox",
                new BoxMesh { Size = new Vector3(0.3f, 0.12f, 0.08f) },
                chassis.Darkened(0.1f), new Vector3(0, -0.12f, 0.16f)));
            root.AddChild(torsoPivot);

            // Articulated military arms with clamp hands
            Color jointColor = armColor.Lightened(0.12f);
            AddArticulatedArm(root, "Left", armColor, jointColor,
                new Vector3(-0.3f, 0.8f, 0), upperLen: 0.16f, forearmLen: 0.14f,
                thickness: 0.035f, handStyle: ArmHandStyle.Clamp);
            AddArticulatedArm(root, "Right", armColor, jointColor,
                new Vector3(0.3f, 0.8f, 0), upperLen: 0.16f, forearmLen: 0.14f,
                thickness: 0.035f, handStyle: ArmHandStyle.Clamp);

            // 4-wheel rover locomotion
            AddWheelAxles(root, wheelColor, xOffset: 0.24f, zSpacing: 0.14f);
            AddWeaponMount(root, BotFrameType.TinCan, new Vector3(0.42f, 0.85f, -0.15f));
            return root;
        }

        // ── SparkPlug — tallest + narrowest, floating on hover pads, Tesla coil ──

        private static Node3D BuildSparkPlugBody()
        {
            var root = new Node3D();
            root.Name = "PlayerBody";
            Color chassis = new Color(0.28f, 0.25f, 0.35f);
            Color accent = GetClassColor(BotFrameType.SparkPlug);
            Color eyeColor = new Color(0.7f, 0.3f, 1f);
            Color armColor = new Color(0.35f, 0.3f, 0.4f);
            Color energy = new Color(0.6f, 0.3f, 1f);
            Color padColor = new Color(0.25f, 0.22f, 0.3f);

            // Small head with large single eye — ethereal look
            var headPivot = CreatePivot("Head", new Vector3(0, 1.55f, 0));
            headPivot.AddChild(CreateMeshNode("_NeckStalk",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.03f, Height = 0.18f, RadialSegments = 6 },
                chassis, new Vector3(0, -0.08f, 0)));
            // Slim head housing
            headPivot.AddChild(CreateMeshNode("_HeadCase",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.1f, Height = 0.1f, RadialSegments = 8 },
                chassis, new Vector3(0, 0.06f, 0)));
            // Single large cyclopean lens
            headPivot.AddChild(CreateEmissiveMeshNode("_CyclopeanLens",
                new SphereMesh { Radius = 0.06f, Height = 0.08f, RadialSegments = 8, Rings = 4 },
                eyeColor, eyeColor, new Vector3(0, 0.06f, -0.08f)));
            root.AddChild(headPivot);

            // Tall narrow torso — spire-like
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.9f, 0));
            torsoPivot.AddChild(CreateMeshNode("_TorsoCore",
                new BoxMesh { Size = new Vector3(0.3f, 0.6f, 0.24f) },
                chassis, Vector3.Zero));
            // Energy conduit lines on front
            torsoPivot.AddChild(CreateEmissiveMeshNode("_ConduitLeft",
                new BoxMesh { Size = new Vector3(0.015f, 0.5f, 0.008f) },
                energy, energy, new Vector3(-0.08f, 0, -0.13f)));
            torsoPivot.AddChild(CreateEmissiveMeshNode("_ConduitRight",
                new BoxMesh { Size = new Vector3(0.015f, 0.5f, 0.008f) },
                energy, energy, new Vector3(0.08f, 0, -0.13f)));
            // Tesla coil on back — taller
            torsoPivot.AddChild(CreateMeshNode("_CoilBase",
                new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 0.18f, RadialSegments = 8 },
                chassis, new Vector3(0, 0.32f, 0.08f)));
            torsoPivot.AddChild(CreateEmissiveMeshNode("_CoilTop",
                new SphereMesh { Radius = 0.065f, Height = 0.13f, RadialSegments = 8, Rings = 4 },
                energy, energy, new Vector3(0, 0.48f, 0.08f)));
            var coilRing = CreateEmissiveMeshNode("_CoilRing",
                new TorusMesh { InnerRadius = 0.035f, OuterRadius = 0.08f, Rings = 12, RingSegments = 8 },
                energy, energy, new Vector3(0, 0.4f, 0.08f));
            coilRing.RotateX(Mathf.DegToRad(90));
            torsoPivot.AddChild(coilRing);
            // Accent stripe
            torsoPivot.AddChild(CreateMeshNode("_AccentStripe",
                new BoxMesh { Size = new Vector3(0.24f, 0.04f, 0.008f) },
                accent, new Vector3(0, 0.1f, -0.13f)));
            root.AddChild(torsoPivot);

            // Thin delicate articulated arms with probe tips
            Color jointColor = armColor.Lightened(0.15f);
            AddArticulatedArm(root, "Left", armColor, jointColor,
                new Vector3(-0.2f, 1.0f, 0), upperLen: 0.18f, forearmLen: 0.16f,
                thickness: 0.025f, handStyle: ArmHandStyle.Probe);
            AddArticulatedArm(root, "Right", armColor, jointColor,
                new Vector3(0.2f, 1.0f, 0), upperLen: 0.18f, forearmLen: 0.16f,
                thickness: 0.025f, handStyle: ArmHandStyle.Probe);

            // Anti-gravity hover pads
            AddHoverPads(root, padColor, energy, spread: 0.16f);
            AddWeaponMount(root, BotFrameType.SparkPlug, new Vector3(0.32f, 1.05f, -0.12f));
            return root;
        }

        // ── RustBucket — lowest profile, compact, 4 spider legs ──

        private static Node3D BuildRustBucketBody()
        {
            var root = new Node3D();
            root.Name = "PlayerBody";
            Color chassis = new Color(0.22f, 0.22f, 0.25f);
            Color accent = GetClassColor(BotFrameType.RustBucket);
            Color eyeColor = new Color(0.1f, 1f, 0.4f);
            Color legColor = new Color(0.18f, 0.18f, 0.2f);
            Color armColor = new Color(0.28f, 0.28f, 0.3f);

            // Flat wedge head — sensor array, recessed into body
            var headPivot = CreatePivot("Head", new Vector3(0, 0.78f, -0.05f));
            headPivot.AddChild(CreateMeshNode("_HeadCase",
                new BoxMesh { Size = new Vector3(0.2f, 0.08f, 0.14f) },
                chassis, Vector3.Zero));
            // Three-dot sensor cluster instead of visor
            for (int i = -1; i <= 1; i++)
            {
                headPivot.AddChild(CreateEmissiveMeshNode($"_Sensor{i}",
                    new SphereMesh { Radius = 0.015f, Height = 0.03f, RadialSegments = 6, Rings = 3 },
                    eyeColor, eyeColor, new Vector3(i * 0.05f, 0, -0.075f)));
            }
            headPivot.RotateX(Mathf.DegToRad(-2));
            root.AddChild(headPivot);

            // Low, wide, flat torso — crouching profile
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.52f, 0));
            torsoPivot.AddChild(CreateMeshNode("_TorsoBox",
                new BoxMesh { Size = new Vector3(0.4f, 0.25f, 0.35f) },
                chassis, Vector3.Zero));
            torsoPivot.AddChild(CreateMeshNode("_AccentStripe",
                new BoxMesh { Size = new Vector3(0.32f, 0.03f, 0.008f) },
                accent, new Vector3(0, 0, -0.18f)));
            // Panel seam lines
            torsoPivot.AddChild(CreateMeshNode("_PanelLine1",
                new BoxMesh { Size = new Vector3(0.008f, 0.2f, 0.008f) },
                accent.Lightened(0.1f), new Vector3(-0.1f, 0, -0.18f)));
            torsoPivot.AddChild(CreateMeshNode("_PanelLine2",
                new BoxMesh { Size = new Vector3(0.008f, 0.2f, 0.008f) },
                accent.Lightened(0.1f), new Vector3(0.1f, 0, -0.18f)));
            // Sensor dish on back
            var dish = CreateMeshNode("_SensorDish",
                new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.02f, Height = 0.03f, RadialSegments = 8 },
                armColor, new Vector3(-0.1f, 0.14f, 0.12f));
            dish.RotateX(Mathf.DegToRad(-20));
            torsoPivot.AddChild(dish);
            // Stealth emitter (dim glow on belly)
            torsoPivot.AddChild(CreateEmissiveMeshNode("_StealthEmitter",
                new SphereMesh { Radius = 0.03f, Height = 0.02f, RadialSegments = 6, Rings = 3 },
                eyeColor * 0.3f, eyeColor * 0.3f, new Vector3(0, -0.13f, 0)));
            root.AddChild(torsoPivot);

            // Slim articulated arms with claw hands
            Color jointColor = armColor.Lightened(0.12f);
            AddArticulatedArm(root, "Left", armColor, jointColor,
                new Vector3(-0.24f, 0.58f, 0), upperLen: 0.12f, forearmLen: 0.1f,
                thickness: 0.025f, handStyle: ArmHandStyle.Claw);
            AddArticulatedArm(root, "Right", armColor, jointColor,
                new Vector3(0.24f, 0.58f, 0), upperLen: 0.12f, forearmLen: 0.1f,
                thickness: 0.025f, handStyle: ArmHandStyle.Claw);

            // Spider legs — 4 articulated legs
            AddSpiderLegs(root, legColor, bodyWidth: 0.2f);
            AddWeaponMount(root, BotFrameType.RustBucket, new Vector3(0.34f, 0.62f, -0.18f));
            return root;
        }

        // ── NoiseBox — round body on mono-ball, BB-8 style, speaker arrays ──

        private static Node3D BuildNoiseBoxBody()
        {
            var root = new Node3D();
            root.Name = "PlayerBody";
            Color chassis = new Color(0.3f, 0.28f, 0.35f);
            Color accent = GetClassColor(BotFrameType.NoiseBox);
            Color eyeColor = new Color(0.8f, 0.4f, 1f);
            Color ballColor = new Color(0.25f, 0.23f, 0.3f);
            Color armColor = new Color(0.38f, 0.35f, 0.42f);
            Color glow = new Color(0.6f, 0.3f, 0.8f);

            // Head with antenna array
            AddBinocularHead(root, chassis, eyeColor, new Vector3(0, 1.35f, 0),
                eyeSpacing: 0.07f, tilt: -4f);
            // Triple antenna prongs — attached to Head pivot so they move with the head
            var noiseHead = root.GetNodeOrNull<Node3D>("Head");
            if (noiseHead != null)
            {
                for (int i = -1; i <= 1; i++)
                {
                    noiseHead.AddChild(CreateMeshNode($"_Antenna{i}",
                        new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.012f, Height = 0.16f, RadialSegments = 4 },
                        armColor, new Vector3(i * 0.05f, 0.17f, 0.02f)));
                    noiseHead.AddChild(CreateEmissiveMeshNode($"_AntennaTip{i}",
                        new SphereMesh { Radius = 0.012f, Height = 0.024f, RadialSegments = 6, Rings = 3 },
                        glow, glow, new Vector3(i * 0.05f, 0.26f, 0.02f)));
                }
            }

            // Round torso — drum-shaped, wider than tall
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.85f, 0));
            torsoPivot.AddChild(CreateMeshNode("_TorsoDrum",
                new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.24f, Height = 0.4f, RadialSegments = 12 },
                chassis, Vector3.Zero));
            // Speaker grille — horizontal slats on front face
            for (int i = -2; i <= 2; i++)
            {
                torsoPivot.AddChild(CreateMeshNode($"_Slat{i}",
                    new BoxMesh { Size = new Vector3(0.28f, 0.015f, 0.008f) },
                    accent.Lightened(0.2f), new Vector3(0, i * 0.05f, -0.23f)));
            }
            // Side speaker cones (left + right)
            for (float side = -1; side <= 1; side += 2)
            {
                var cone = CreateMeshNode(side < 0 ? "_SpeakerL" : "_SpeakerR",
                    new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.04f, Height = 0.04f, RadialSegments = 8 },
                    armColor, new Vector3(side * 0.24f, 0, 0));
                cone.RotateZ(Mathf.DegToRad(side * 90));
                torsoPivot.AddChild(cone);
                torsoPivot.AddChild(CreateEmissiveMeshNode(side < 0 ? "_SpeakerGlowL" : "_SpeakerGlowR",
                    new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                    glow, glow, new Vector3(side * 0.26f, 0, 0)));
            }
            // Resonance dish on back
            var resDish = CreateEmissiveMeshNode("_ResonanceDish",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.03f, Height = 0.05f, RadialSegments = 10 },
                glow, glow, new Vector3(0, 0.08f, 0.2f));
            resDish.RotateX(Mathf.DegToRad(15));
            torsoPivot.AddChild(resDish);
            root.AddChild(torsoPivot);

            // Short articulated arms with clamp hands
            Color jointColor = armColor.Lightened(0.12f);
            AddArticulatedArm(root, "Left", armColor, jointColor,
                new Vector3(-0.28f, 0.95f, 0), upperLen: 0.13f, forearmLen: 0.11f,
                thickness: 0.032f, handStyle: ArmHandStyle.Clamp);
            AddArticulatedArm(root, "Right", armColor, jointColor,
                new Vector3(0.28f, 0.95f, 0), upperLen: 0.13f, forearmLen: 0.11f,
                thickness: 0.032f, handStyle: ArmHandStyle.Clamp);

            // Mono-ball locomotion (BB-8 style)
            AddMonoBall(root, ballColor, accent, radius: 0.2f);
            AddWeaponMount(root, BotFrameType.NoiseBox, new Vector3(0.4f, 1.0f, -0.15f));
            return root;
        }

        // ── Clunker — stocky hunched brawler, bipedal chicken-walker legs ──

        private static Node3D BuildClunkerBody()
        {
            var root = new Node3D();
            root.Name = "PlayerBody";
            Color chassis = new Color(0.4f, 0.32f, 0.25f);
            Color accent = GetClassColor(BotFrameType.Clunker);
            Color eyeColor = new Color(1f, 0.85f, 0.2f);
            Color legColor = new Color(0.35f, 0.3f, 0.22f);
            Color armColor = new Color(0.45f, 0.38f, 0.3f);
            Color piston = new Color(0.5f, 0.5f, 0.52f);

            // Compact head sunk forward into shoulders — hunched look
            AddBinocularHead(root, chassis, eyeColor, new Vector3(0, 1.15f, -0.06f),
                eyeSpacing: 0.09f, tilt: -12f);
            // Welded jaw plate — attached to Head pivot so it moves with the head
            var clunkerHead = root.GetNodeOrNull<Node3D>("Head");
            clunkerHead?.AddChild(CreateMeshNode("_JawPlate",
                new BoxMesh { Size = new Vector3(0.22f, 0.04f, 0.1f) },
                chassis.Darkened(0.1f), new Vector3(0, -0.03f, 0)));

            // Stocky wide torso — hunched forward
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.75f, 0));
            torsoPivot.AddChild(CreateMeshNode("_TorsoBox",
                new BoxMesh { Size = new Vector3(0.55f, 0.4f, 0.4f) },
                chassis, Vector3.Zero));
            torsoPivot.AddChild(CreateMeshNode("_AccentStripe",
                new BoxMesh { Size = new Vector3(0.45f, 0.05f, 0.008f) },
                accent, new Vector3(0, 0, -0.21f)));
            // Piston housings on shoulders
            for (float side = -1; side <= 1; side += 2)
            {
                torsoPivot.AddChild(CreateMeshNode(side < 0 ? "_LeftPiston" : "_RightPiston",
                    new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.055f, Height = 0.14f, RadialSegments = 6 },
                    piston, new Vector3(side * 0.3f, 0.14f, 0)));
                // Exhaust vent on each shoulder
                torsoPivot.AddChild(CreateMeshNode(side < 0 ? "_VentL" : "_VentR",
                    new BoxMesh { Size = new Vector3(0.06f, 0.03f, 0.04f) },
                    chassis.Darkened(0.15f), new Vector3(side * 0.28f, 0.22f, -0.1f)));
            }
            // Rear engine block
            torsoPivot.AddChild(CreateMeshNode("_EngineBlock",
                new BoxMesh { Size = new Vector3(0.25f, 0.18f, 0.1f) },
                chassis.Darkened(0.08f), new Vector3(0, -0.05f, 0.22f)));
            root.AddChild(torsoPivot);

            // Oversized articulated hydraulic fist arms
            Color jointColor = piston;
            AddArticulatedArm(root, "Left", armColor, jointColor,
                new Vector3(-0.34f, 0.82f, 0), upperLen: 0.16f, forearmLen: 0.14f,
                thickness: 0.048f, handStyle: ArmHandStyle.Fist);
            AddArticulatedArm(root, "Right", armColor, jointColor,
                new Vector3(0.34f, 0.82f, 0), upperLen: 0.16f, forearmLen: 0.14f,
                thickness: 0.048f, handStyle: ArmHandStyle.Fist);

            // Articulated chicken-walker biped legs
            AddArticulatedBipedLegs(root, legColor, xOffset: 0.2f,
                thighLen: 0.18f, shinLen: 0.2f, thickness: 0.035f, chickenWalker: true);
            AddWeaponMount(root, BotFrameType.Clunker, new Vector3(0.46f, 0.85f, -0.18f));
            return root;
        }

        private static void BuildTrackAssembly(Node3D pivot, Color trackColor, bool mirror)
        {
            float mx = mirror ? -1f : 1f;

            // Track housing box
            var housing = CreateMeshNode("_TrackHousing",
                new BoxMesh { Size = new Vector3(0.12f, 0.15f, 0.35f) },
                trackColor, new Vector3(0, -0.08f, 0));
            pivot.AddChild(housing);

            // 3 wheel cylinders inside tracks
            for (int i = 0; i < 3; i++)
            {
                float zOff = (i - 1) * 0.12f;
                var wheel = CreateMeshNode($"_Wheel{i}",
                    new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 0.06f, RadialSegments = 8 },
                    trackColor.Lightened(0.1f), new Vector3(0, -0.08f, zOff));
                wheel.RotateZ(Mathf.DegToRad(90));
                pivot.AddChild(wheel);
            }
        }

        // ── Wheel Axle Locomotion (TinCan) ──

        private static void AddWheelAxles(Node3D root, Color wheelColor, float xOffset = 0.22f, float zSpacing = 0.14f)
        {
            Color hubColor = wheelColor.Lightened(0.15f);
            Color axleColor = wheelColor.Lightened(0.08f);

            var leftLeg = CreatePivot("LeftLeg", new Vector3(-xOffset, 0.12f, 0));
            var rightLeg = CreatePivot("RightLeg", new Vector3(xOffset, 0.12f, 0));

            // 2 wheels per side (front + rear)
            for (int z = -1; z <= 1; z += 2)
            {
                string label = z < 0 ? "Front" : "Rear";
                float zPos = z * zSpacing;

                // Left wheel
                var lWheel = CreateMeshNode($"_L{label}Wheel",
                    new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.05f, RadialSegments = 10 },
                    wheelColor, new Vector3(0, -0.04f, zPos));
                lWheel.RotateZ(Mathf.DegToRad(90));
                leftLeg.AddChild(lWheel);

                // Left hub cap
                leftLeg.AddChild(CreateMeshNode($"_L{label}Hub",
                    new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.02f, RadialSegments = 6 },
                    hubColor, new Vector3(-0.035f, -0.04f, zPos)));

                // Right wheel
                var rWheel = CreateMeshNode($"_R{label}Wheel",
                    new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.05f, RadialSegments = 10 },
                    wheelColor, new Vector3(0, -0.04f, zPos));
                rWheel.RotateZ(Mathf.DegToRad(90));
                rightLeg.AddChild(rWheel);

                // Right hub cap
                rightLeg.AddChild(CreateMeshNode($"_R{label}Hub",
                    new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.02f, RadialSegments = 6 },
                    hubColor, new Vector3(0.035f, -0.04f, zPos)));
            }

            // Axle bars connecting front and rear wheels on each side
            leftLeg.AddChild(CreateMeshNode("_LAxle",
                new BoxMesh { Size = new Vector3(0.03f, 0.03f, zSpacing * 2f) },
                axleColor, new Vector3(0, -0.04f, 0)));
            rightLeg.AddChild(CreateMeshNode("_RAxle",
                new BoxMesh { Size = new Vector3(0.03f, 0.03f, zSpacing * 2f) },
                axleColor, new Vector3(0, -0.04f, 0)));

            // Suspension springs (small cylinders between axle and body)
            for (int z = -1; z <= 1; z += 2)
            {
                float zPos = z * zSpacing * 0.6f;
                leftLeg.AddChild(CreateMeshNode($"_LSpring{z}",
                    new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.015f, Height = 0.06f, RadialSegments = 4 },
                    hubColor, new Vector3(0, 0.02f, zPos)));
                rightLeg.AddChild(CreateMeshNode($"_RSpring{z}",
                    new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.015f, Height = 0.06f, RadialSegments = 4 },
                    hubColor, new Vector3(0, 0.02f, zPos)));
            }

            root.AddChild(leftLeg);
            root.AddChild(rightLeg);
        }

        // ── Hover Pad Locomotion (SparkPlug) ──

        private static void AddHoverPads(Node3D root, Color padColor, Color glowColor, float spread = 0.18f)
        {
            // 3 pads in triangle: 2 rear, 1 front
            var positions = new[]
            {
                new Vector3(0, 0.05f, -spread),         // front center
                new Vector3(-spread, 0.05f, spread * 0.7f), // rear left
                new Vector3(spread, 0.05f, spread * 0.7f)   // rear right
            };
            string[] names = { "Front", "RearL", "RearR" };

            // Use LeftLeg for front+left, RightLeg for right
            var leftLeg = CreatePivot("LeftLeg", Vector3.Zero);
            var rightLeg = CreatePivot("RightLeg", Vector3.Zero);

            for (int i = 0; i < 3; i++)
            {
                var parent = i == 2 ? rightLeg : leftLeg;

                // Pad disc
                var disc = CreateMeshNode($"_Pad{names[i]}",
                    new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.06f, Height = 0.025f, RadialSegments = 10 },
                    padColor, positions[i]);
                parent.AddChild(disc);

                // Emissive glow ring underneath
                var glow = CreateEmissiveMeshNode($"_Glow{names[i]}",
                    new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.065f, Rings = 10, RingSegments = 6 },
                    glowColor, glowColor, positions[i] + new Vector3(0, -0.02f, 0));
                parent.AddChild(glow);

                // Anti-grav emitter (small sphere underneath)
                parent.AddChild(CreateEmissiveMeshNode($"_Emitter{names[i]}",
                    new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                    glowColor, glowColor, positions[i] + new Vector3(0, -0.03f, 0)));
            }

            root.AddChild(leftLeg);
            root.AddChild(rightLeg);
        }

        // ── Spider Leg Locomotion (RustBucket) ──

        private static void AddSpiderLegs(Node3D root, Color legColor, float bodyWidth = 0.2f)
        {
            Color jointColor = legColor.Lightened(0.12f);
            Color footColor = legColor.Darkened(0.1f);
            Color pistonColor = new Color(0.5f, 0.5f, 0.52f);

            // Leg pivots at bottom edge of torso so they connect to the body
            var leftLeg = CreatePivot("LeftLeg", new Vector3(-bodyWidth, 0.42f, 0));
            var rightLeg = CreatePivot("RightLeg", new Vector3(bodyWidth, 0.42f, 0));

            // 2 legs per side, spread front-back
            for (int z = -1; z <= 1; z += 2)
            {
                float zOff = z * 0.14f;
                string label = z < 0 ? "Front" : "Rear";

                // ── Left side legs ──

                // Hip joint at attachment point
                leftLeg.AddChild(CreateMeshNode($"_L{label}Hip",
                    new SphereMesh { Radius = 0.028f, Height = 0.056f, RadialSegments = 6, Rings = 3 },
                    jointColor, new Vector3(0, 0, zOff)));

                // Upper segment (angled outward from body)
                var lUpper = CreateMeshNode($"_L{label}Upper",
                    new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.026f, Height = 0.22f, RadialSegments = 6 },
                    legColor, new Vector3(-0.07f, -0.085f, zOff));
                lUpper.RotateZ(Mathf.DegToRad(40));
                leftLeg.AddChild(lUpper);

                // Knee joint
                leftLeg.AddChild(CreateMeshNode($"_L{label}Knee",
                    new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                    jointColor, new Vector3(-0.14f, -0.17f, zOff)));

                // Piston rod along upper segment
                var lPiston = CreateMeshNode($"_L{label}Piston",
                    new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.18f, RadialSegments = 4 },
                    pistonColor, new Vector3(-0.05f, -0.075f, zOff + 0.02f));
                lPiston.RotateZ(Mathf.DegToRad(40));
                leftLeg.AddChild(lPiston);

                // Lower segment (angled steeply down to ground)
                var lLower = CreateMeshNode($"_L{label}Lower",
                    new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.016f, Height = 0.26f, RadialSegments = 6 },
                    legColor, new Vector3(-0.16f, -0.29f, zOff));
                lLower.RotateZ(Mathf.DegToRad(8));
                leftLeg.AddChild(lLower);

                // Foot pad
                leftLeg.AddChild(CreateMeshNode($"_L{label}Foot",
                    new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.018f, Height = 0.02f, RadialSegments = 4 },
                    footColor, new Vector3(-0.18f, -0.42f, zOff)));

                // ── Right side legs ──

                rightLeg.AddChild(CreateMeshNode($"_R{label}Hip",
                    new SphereMesh { Radius = 0.028f, Height = 0.056f, RadialSegments = 6, Rings = 3 },
                    jointColor, new Vector3(0, 0, zOff)));

                var rUpper = CreateMeshNode($"_R{label}Upper",
                    new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.026f, Height = 0.22f, RadialSegments = 6 },
                    legColor, new Vector3(0.07f, -0.085f, zOff));
                rUpper.RotateZ(Mathf.DegToRad(-40));
                rightLeg.AddChild(rUpper);

                rightLeg.AddChild(CreateMeshNode($"_R{label}Knee",
                    new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                    jointColor, new Vector3(0.14f, -0.17f, zOff)));

                var rPiston = CreateMeshNode($"_R{label}Piston",
                    new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.18f, RadialSegments = 4 },
                    pistonColor, new Vector3(0.05f, -0.075f, zOff + 0.02f));
                rPiston.RotateZ(Mathf.DegToRad(-40));
                rightLeg.AddChild(rPiston);

                var rLower = CreateMeshNode($"_R{label}Lower",
                    new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.016f, Height = 0.26f, RadialSegments = 6 },
                    legColor, new Vector3(0.16f, -0.29f, zOff));
                rLower.RotateZ(Mathf.DegToRad(-8));
                rightLeg.AddChild(rLower);

                rightLeg.AddChild(CreateMeshNode($"_R{label}Foot",
                    new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.018f, Height = 0.02f, RadialSegments = 4 },
                    footColor, new Vector3(0.18f, -0.42f, zOff)));
            }

            root.AddChild(leftLeg);
            root.AddChild(rightLeg);
        }

        // ── Articulated Bipedal Legs (nested hip → knee → ankle pivots) ──

        private static void AddArticulatedBipedLegs(Node3D root, Color legColor, float xOffset = 0.16f,
            float thighLen = 0.18f, float shinLen = 0.2f, float thickness = 0.03f, bool chickenWalker = false)
        {
            Color jointColor = legColor.Lightened(0.1f);
            Color footColor = legColor.Darkened(0.15f);
            Color pistonColor = new Color(0.5f, 0.5f, 0.52f);

            for (float side = -1; side <= 1; side += 2)
            {
                string sName = side < 0 ? "Left" : "Right";
                var hipPivot = CreatePivot($"{sName}Leg", new Vector3(side * xOffset, 0.3f, 0));

                // Hip ball joint
                hipPivot.AddChild(CreateMeshNode($"_{sName}Hip",
                    new SphereMesh { Radius = thickness * 1.4f, Height = thickness * 2.8f, RadialSegments = 8, Rings = 4 },
                    jointColor, Vector3.Zero));

                // Thigh
                hipPivot.AddChild(CreateMeshNode($"_{sName}Thigh",
                    new BoxMesh { Size = new Vector3(thickness * 2f, thighLen, thickness * 2f) },
                    legColor, new Vector3(0, -thighLen / 2f, chickenWalker ? -0.02f : 0)));

                // Knee pivot (nested inside hip)
                var kneePivot = CreatePivot($"{sName}Knee", new Vector3(0, -thighLen, chickenWalker ? -0.03f : 0));

                // Knee ball joint (bigger for industrial look)
                kneePivot.AddChild(CreateMeshNode($"_{sName}KneeBall",
                    new SphereMesh { Radius = thickness * 1.5f, Height = thickness * 2.5f, RadialSegments = 8, Rings = 4 },
                    jointColor, Vector3.Zero));

                // Shin
                float shinAngleZ = chickenWalker ? 0.03f : 0;
                kneePivot.AddChild(CreateMeshNode($"_{sName}Shin",
                    new BoxMesh { Size = new Vector3(thickness * 1.7f, shinLen, thickness * 1.7f) },
                    legColor, new Vector3(0, -shinLen / 2f, shinAngleZ)));

                // Piston rod along shin
                kneePivot.AddChild(CreateMeshNode($"_{sName}Piston",
                    new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = shinLen * 0.8f, RadialSegments = 4 },
                    pistonColor, new Vector3(thickness * 0.8f, -shinLen * 0.45f, shinAngleZ * 0.5f)));

                // Ankle pivot (nested inside knee)
                var anklePivot = CreatePivot($"{sName}Ankle", new Vector3(0, -shinLen, shinAngleZ));

                // Ankle ball
                anklePivot.AddChild(CreateMeshNode($"_{sName}AnkleBall",
                    new SphereMesh { Radius = thickness * 1f, Height = thickness * 2f, RadialSegments = 6, Rings = 3 },
                    jointColor, Vector3.Zero));

                // Foot
                float footLen = thickness * 4.5f;
                float footWidth = thickness * 3.3f;
                anklePivot.AddChild(CreateMeshNode($"_{sName}Foot",
                    new BoxMesh { Size = new Vector3(footWidth, thickness * 0.8f, footLen) },
                    footColor, new Vector3(0, -thickness * 0.6f, -footLen * 0.15f)));

                // Toe grips
                for (float t = -1; t <= 1; t += 2)
                {
                    anklePivot.AddChild(CreateMeshNode($"_{sName}Toe{(t < 0 ? "L" : "R")}",
                        new BoxMesh { Size = new Vector3(thickness * 0.7f, thickness * 0.5f, thickness * 1.2f) },
                        footColor.Darkened(0.1f), new Vector3(t * thickness * 0.9f, -thickness * 0.8f, -footLen * 0.45f)));
                }

                kneePivot.AddChild(anklePivot);
                hipPivot.AddChild(kneePivot);
                root.AddChild(hipPivot);
            }
        }

        // Legacy flat bipedal legs (for enemies that don't need articulation)
        private static void AddBipedLegs(Node3D root, Color legColor, float xOffset = 0.16f)
        {
            AddArticulatedBipedLegs(root, legColor, xOffset, chickenWalker: true);
        }

        // ── Mono-Ball Locomotion (NoiseBox) ──

        private static void AddMonoBall(Node3D root, Color ballColor, Color accentColor, float radius = 0.18f)
        {
            // LeftLeg/RightLeg pivots wrap the mono-ball for equipment compatibility
            var leftLeg = CreatePivot("LeftLeg", new Vector3(-0.05f, radius, 0));
            var rightLeg = CreatePivot("RightLeg", new Vector3(0.05f, radius, 0));

            // Main rolling sphere
            var ball = CreateMeshNode("_Ball",
                new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 14, Rings = 8 },
                ballColor, new Vector3(0.05f, 0, 0));
            leftLeg.AddChild(ball);

            // Equator band
            var band = CreateMeshNode("_Band",
                new TorusMesh { InnerRadius = radius - 0.01f, OuterRadius = radius + 0.01f, Rings = 14, RingSegments = 6 },
                accentColor, new Vector3(0.05f, 0, 0));
            band.RotateX(Mathf.DegToRad(90));
            leftLeg.AddChild(band);

            // Traction treads (3 horizontal grooves)
            for (int i = -1; i <= 1; i++)
            {
                var groove = CreateMeshNode($"_Groove{i}",
                    new TorusMesh { InnerRadius = radius - 0.005f, OuterRadius = radius + 0.008f, Rings = 12, RingSegments = 4 },
                    ballColor.Darkened(0.15f), new Vector3(0.05f, i * 0.06f, 0));
                groove.RotateX(Mathf.DegToRad(90));
                leftLeg.AddChild(groove);
            }

            // Small stabilizer fins (2 on sides)
            rightLeg.AddChild(CreateMeshNode("_StabilizerR",
                new BoxMesh { Size = new Vector3(0.015f, 0.08f, 0.06f) },
                accentColor, new Vector3(radius * 0.7f, 0.02f, 0)));
            leftLeg.AddChild(CreateMeshNode("_StabilizerL",
                new BoxMesh { Size = new Vector3(0.015f, 0.08f, 0.06f) },
                accentColor, new Vector3(-radius * 0.7f + 0.05f, 0.02f, 0)));

            root.AddChild(leftLeg);
            root.AddChild(rightLeg);
        }

        // ── Companion Bodies ──

        public static Node3D BuildCompanionBody(string companionId)
        {
            // Try model asset first
            var model = ModelLibrary.TryLoad("companion", companionId);
            if (model != null)
            {
                model.Name = "CompanionBody";
                ScaleModelToFit(model, 0.6f);
                return model;
            }

            // Procedural fallback — hovering drone junkbot
            return BuildBitDroneBody();
        }

        /// <summary>
        /// BIT — Basic Intelligence Terminal: small hovering drone with antenna, eye, and fins.
        /// ~12 parts. Named pivots for ProceduralAnimator compatibility.
        /// </summary>
        private static Node3D BuildBitDroneBody()
        {
            var root = new Node3D();
            root.Name = "CompanionBody";

            Color shellColor = new Color(0.35f, 0.75f, 0.95f);
            Color darkMetal = new Color(0.25f, 0.28f, 0.3f);
            Color accentColor = new Color(0.1f, 0.9f, 0.95f);

            // ── Body pivot — main chassis ──
            var body = CreatePivot("Body", Vector3.Zero);
            root.AddChild(body);

            // Main hull — squashed sphere
            var hull = CreateMeshNode("Hull",
                new SphereMesh { Radius = 0.2f, Height = 0.28f, RadialSegments = 12, Rings = 6 },
                shellColor, Vector3.Zero);
            body.AddChild(hull);

            // Belly plate
            var belly = CreateMeshNode("BellyPlate",
                new BoxMesh { Size = new Vector3(0.22f, 0.04f, 0.22f) },
                darkMetal, new Vector3(0, -0.1f, 0));
            body.AddChild(belly);

            // Top vent grill
            var vent = CreateMeshNode("Vent",
                new BoxMesh { Size = new Vector3(0.12f, 0.02f, 0.08f) },
                darkMetal, new Vector3(0, 0.14f, 0));
            body.AddChild(vent);

            // Side thruster pods
            for (int side = -1; side <= 1; side += 2)
            {
                var thruster = CreateMeshNode($"Thruster{(side < 0 ? "L" : "R")}",
                    new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.06f, Height = 0.1f, RadialSegments = 6 },
                    darkMetal, new Vector3(side * 0.2f, -0.04f, 0));
                body.AddChild(thruster);

                // Thruster glow
                var thrusterGlow = CreateEmissiveMeshNode($"ThrusterGlow{(side < 0 ? "L" : "R")}",
                    new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                    accentColor, accentColor, new Vector3(side * 0.2f, -0.1f, 0));
                body.AddChild(thrusterGlow);
            }

            // ── Head pivot — eye/sensor dome ──
            var head = CreatePivot("Head", new Vector3(0, 0.16f, 0));
            root.AddChild(head);

            // Sensor dome
            var dome = CreateMeshNode("Dome",
                new SphereMesh { Radius = 0.08f, Height = 0.1f, RadialSegments = 8, Rings = 4 },
                new Color(0.6f, 0.65f, 0.7f), Vector3.Zero);
            head.AddChild(dome);

            // Main eye lens
            var eye = CreateEmissiveMeshNode("Eye",
                new SphereMesh { Radius = 0.05f, Height = 0.04f, RadialSegments = 8, Rings = 4 },
                accentColor, accentColor, new Vector3(0, 0, 0.07f));
            head.AddChild(eye);

            // Antenna
            var antenna = CreateMeshNode("Antenna",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.012f, Height = 0.12f, RadialSegments = 4 },
                darkMetal, new Vector3(0.03f, 0.08f, 0));
            head.AddChild(antenna);

            // Antenna tip
            var antennaTip = CreateEmissiveMeshNode("AntennaTip",
                new SphereMesh { Radius = 0.015f, Height = 0.03f, RadialSegments = 4, Rings = 2 },
                new Color(1f, 0.3f, 0.1f), new Color(1f, 0.3f, 0.1f), new Vector3(0.03f, 0.14f, 0));
            head.AddChild(antennaTip);

            // ── Stabilizer fins ──
            var leftFin = CreateMeshNode("FinLeft",
                new BoxMesh { Size = new Vector3(0.14f, 0.02f, 0.08f) },
                shellColor.Darkened(0.15f), new Vector3(-0.16f, 0.02f, -0.06f));
            leftFin.RotateZ(Mathf.DegToRad(-15));
            root.AddChild(leftFin);

            var rightFin = CreateMeshNode("FinRight",
                new BoxMesh { Size = new Vector3(0.14f, 0.02f, 0.08f) },
                shellColor.Darkened(0.15f), new Vector3(0.16f, 0.02f, -0.06f));
            rightFin.RotateZ(Mathf.DegToRad(15));
            root.AddChild(rightFin);

            return root;
        }

        // ── Enemy Bodies ──

        /// <summary>
        /// Per-enemy model height targets. Most enemies should be equal or larger than player.
        /// Small enemies (scrap_rat, wire_worm) are intentionally small swarm types.
        /// </summary>
        public static float GetEnemyModelHeight(string enemyId) => enemyId switch
        {
            "calibration_target" => PlayerModelHeight * 1.6f,   // training dummy, imposing
            "scrap_rat"          => PlayerModelHeight * 0.7f,   // small swarm enemy
            "decoy_unit"         => PlayerModelHeight * 1.2f,   // mimic, clearly bigger
            "wire_worm"          => PlayerModelHeight * 0.8f,   // ground crawler, slightly bigger
            "corrupted_sentry"   => PlayerModelHeight * 2.2f,   // large imposing boss
            "scrap_hydra"        => PlayerModelHeight * 2.0f,   // multi-headed boss
            "axis_avatar"        => 12f,                         // massive upper-body boss, custom build
            "rust_titan"         => PlayerModelHeight * 2.2f,   // sector 2 boss
            "null_warden"        => PlayerModelHeight * 2.4f,   // sector 4 boss
            "rust_mite"          => PlayerModelHeight * 0.4f,   // tiny swarm enemy
            "volt_sprinter"      => PlayerModelHeight * 0.9f,   // lean fast charger
            "shard_lobber"       => PlayerModelHeight * 1.1f,   // squat artillery
            "scrap_golem"        => PlayerModelHeight * 1.8f,   // heavy tank
            "glitch_phantom"     => PlayerModelHeight * 1.0f,   // same height range, eerie
            "overclock_drone"    => PlayerModelHeight * 0.6f,   // small flying support
            "axis_disciple"      => PlayerModelHeight * 1.5f,   // imposing AXIS servant
            _                    => PlayerModelHeight * 1.4f,   // default: bigger than player
        };

        public static Node3D BuildEnemyBody(string enemyId)
        {
            float targetHeight = GetEnemyModelHeight(enemyId);

            // Try model asset first — but validate it has renderable mesh content
            var model = ModelLibrary.TryLoad("enemy", enemyId);
            if (model != null)
            {
                var mesh = FindMeshInModel(model);
                if (mesh != null)
                {
                    var container = new Node3D();
                    container.Name = "EnemyBody";
                    ScaleModelToFit(model, targetHeight);
                    model.RotateY(Mathf.DegToRad(180f));
                    container.AddChild(model);

                    // Play idle animation if available (fixes T-pose on POLYGON characters)
                    var animPlayer = FindAnimationPlayer(model);
                    if (animPlayer != null)
                    {
                        var anims = animPlayer.GetAnimationList();
                        string idleAnim = null;
                        foreach (var anim in anims)
                        {
                            var lower = anim.ToLower();
                            if (lower.Contains("idle"))
                            {
                                idleAnim = anim;
                                break;
                            }
                        }
                        // Fallback: play first animation if no idle found
                        if (idleAnim == null && anims.Length > 0)
                            idleAnim = anims[0];

                        if (idleAnim != null)
                        {
                            animPlayer.Play(idleAnim);
                            GD.Print($"[CharacterMeshBuilder] Enemy '{enemyId}' playing animation '{idleAnim}'");
                        }
                    }

                    GD.Print($"[CharacterMeshBuilder] Loaded enemy model '{enemyId}', scaled to {targetHeight}m");
                    return container;
                }
                else
                {
                    // Model loaded but has no mesh — discard and use procedural
                    GD.Print($"[CharacterMeshBuilder] Enemy model '{enemyId}' has no mesh, using procedural");
                    model.QueueFree();
                }
            }

            // AXIS gets a completely custom upper-body build (not scaled)
            if (enemyId == "axis_avatar")
            {
                GD.Print("[CharacterMeshBuilder] Building custom AXIS upper-body boss");
                return AxisBossBody.Build();
            }

            GD.Print($"[CharacterMeshBuilder] No model for enemy '{enemyId}', using procedural fallback");
            // Procedural fallback — scale to target height
            var proceduralEnemy = enemyId switch
            {
                "calibration_target" => BuildCalibrationTargetBody(),
                "scrap_rat" => BuildScrapRatBody(),
                "decoy_unit" => BuildDecoyUnitBody(),
                "wire_worm" => BuildWireWormBody(),
                "corrupted_sentry" => BuildCorruptedSentryBody(),
                "scrap_hydra" => BuildScrapHydraBody(),
                "axis_avatar" => BuildAxisAvatarBody(),
                "rust_titan" => BuildCorruptedSentryBody(),    // reuse sentry body, different color via EnemyData
                "null_warden" => BuildScrapHydraBody(),        // reuse hydra body, different color via EnemyData
                _ => BuildDefaultEnemyBody()
            };
            ScaleModelToFit(proceduralEnemy, targetHeight);
            return proceduralEnemy;
        }

        private static Node3D BuildCalibrationTargetBody()
        {
            var root = new Node3D();
            root.Name = "CalibrationTargetBody";
            Color straw = new Color(0.65f, 0.6f, 0.3f);
            Color wood = new Color(0.4f, 0.3f, 0.2f);
            Color metal = new Color(0.45f, 0.45f, 0.48f);
            Color targetRed = new Color(0.85f, 0.15f, 0.1f);
            Color targetWhite = new Color(0.9f, 0.9f, 0.88f);
            Color wireColor = new Color(0.3f, 0.3f, 0.32f);

            // Body pivot — barrel cylinder with target rings
            var bodyPivot = CreatePivot("Body", new Vector3(0, 0.7f, 0));
            bodyPivot.AddChild(CreateMeshNode("_Barrel",
                new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.25f, Height = 0.8f, RadialSegments = 8 },
                straw, Vector3.Zero));
            // Emissive target rings on front
            var outerRing = CreateEmissiveMeshNode("_TargetOuter",
                new TorusMesh { InnerRadius = 0.14f, OuterRadius = 0.18f, Rings = 12, RingSegments = 8 },
                targetRed, targetRed, new Vector3(0, 0.05f, -0.22f));
            outerRing.RotateX(Mathf.DegToRad(90));
            bodyPivot.AddChild(outerRing);
            var middleRing = CreateEmissiveMeshNode("_TargetMiddle",
                new TorusMesh { InnerRadius = 0.07f, OuterRadius = 0.11f, Rings = 10, RingSegments = 8 },
                targetWhite, targetWhite * 0.6f, new Vector3(0, 0.05f, -0.23f));
            middleRing.RotateX(Mathf.DegToRad(90));
            bodyPivot.AddChild(middleRing);
            bodyPivot.AddChild(CreateEmissiveMeshNode("_Bullseye",
                new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 8, Rings = 4 },
                targetRed, targetRed, new Vector3(0, 0.05f, -0.25f)));
            // Side panels
            bodyPivot.AddChild(CreateMeshNode("_LeftPanel",
                new BoxMesh { Size = new Vector3(0.04f, 0.5f, 0.2f) },
                metal, new Vector3(-0.22f, 0, 0)));
            bodyPivot.AddChild(CreateMeshNode("_RightPanel",
                new BoxMesh { Size = new Vector3(0.04f, 0.5f, 0.2f) },
                metal, new Vector3(0.22f, 0, 0)));
            root.AddChild(bodyPivot);

            // Head pivot — sphere with flat cap helmet
            var headPivot = CreatePivot("Head", new Vector3(0, 1.3f, 0));
            headPivot.AddChild(CreateMeshNode("_Skull",
                new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 10, Rings = 5 },
                straw.Lightened(0.1f), Vector3.Zero));
            headPivot.AddChild(CreateMeshNode("_HelmetCap",
                new BoxMesh { Size = new Vector3(0.28f, 0.06f, 0.22f) },
                metal, new Vector3(0, 0.14f, 0)));
            root.AddChild(headPivot);

            // Crossbar pivot — main bar with padded target areas
            var crossPivot = CreatePivot("Crossbar", new Vector3(0, 1.0f, 0));
            crossPivot.AddChild(CreateMeshNode("_Bar",
                new BoxMesh { Size = new Vector3(0.9f, 0.06f, 0.06f) },
                wood, Vector3.Zero));
            crossPivot.AddChild(CreateMeshNode("_LeftPad",
                new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.08f) },
                straw.Darkened(0.1f), new Vector3(-0.4f, 0, 0)));
            crossPivot.AddChild(CreateMeshNode("_RightPad",
                new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.08f) },
                straw.Darkened(0.1f), new Vector3(0.4f, 0, 0)));
            root.AddChild(crossPivot);

            // Dangling wire bundles
            root.AddChild(CreateMeshNode("_WireLeft",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.008f, Height = 0.18f, RadialSegments = 4 },
                wireColor, new Vector3(-0.15f, 0.55f, -0.18f)));
            root.AddChild(CreateMeshNode("_WireRight",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.008f, Height = 0.18f, RadialSegments = 4 },
                wireColor, new Vector3(0.15f, 0.55f, -0.18f)));

            // Post base cylinder
            root.AddChild(CreateMeshNode("_Post",
                new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 0.5f, RadialSegments = 6 },
                wood, new Vector3(0, 0.25f, 0)));
            // Flat base footing
            root.AddChild(CreateMeshNode("_BaseFoot",
                new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.15f, Height = 0.04f, RadialSegments = 8 },
                wood.Darkened(0.15f), new Vector3(0, 0.02f, 0)));

            return root;
        }

        private static Node3D BuildScrapRatBody()
        {
            var root = new Node3D();
            root.Name = "ScrapRatBody";
            Color brown = new Color(0.5f, 0.35f, 0.25f);
            Color metal = new Color(0.4f, 0.4f, 0.42f);
            Color bone = new Color(0.65f, 0.6f, 0.5f);
            Color eyeColor = new Color(1f, 0.4f, 0.15f);

            // Head pivot — skull with antenna ears, eyes, snout
            var headPivot = CreatePivot("Head", new Vector3(0, 0.4f, -0.25f));
            headPivot.AddChild(CreateMeshNode("_Skull",
                new SphereMesh { Radius = 0.12f, Height = 0.2f, RadialSegments = 8, Rings = 4 },
                bone, Vector3.Zero));
            // Antenna ears
            headPivot.AddChild(CreateMeshNode("_LeftEar",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.02f, Height = 0.1f, RadialSegments = 4 },
                metal, new Vector3(-0.06f, 0.1f, 0)));
            headPivot.AddChild(CreateMeshNode("_RightEar",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.02f, Height = 0.1f, RadialSegments = 4 },
                metal, new Vector3(0.06f, 0.1f, 0)));
            // Emissive red-orange eyes
            headPivot.AddChild(CreateEmissiveMeshNode("_LeftEye",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                eyeColor, eyeColor, new Vector3(-0.06f, 0.03f, -0.09f)));
            headPivot.AddChild(CreateEmissiveMeshNode("_RightEye",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                eyeColor, eyeColor, new Vector3(0.06f, 0.03f, -0.09f)));
            // Snout cylinder
            headPivot.AddChild(CreateMeshNode("_Snout",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.05f, Height = 0.08f, RadialSegments = 6 },
                bone.Darkened(0.1f), new Vector3(0, -0.02f, -0.12f)));
            root.AddChild(headPivot);

            // Body pivot — squashed main sphere + spine plate ridge + gear discs
            var bodyPivot = CreatePivot("Body", new Vector3(0, 0.35f, 0));
            bodyPivot.AddChild(CreateMeshNode("_MainBody",
                new SphereMesh { Radius = 0.25f, Height = 0.3f, RadialSegments = 10, Rings = 5 },
                brown, Vector3.Zero));
            bodyPivot.AddChild(CreateMeshNode("_SpineRidge",
                new BoxMesh { Size = new Vector3(0.04f, 0.06f, 0.3f) },
                metal, new Vector3(0, 0.14f, 0)));
            // Visible gear discs on sides
            var leftGear = CreateMeshNode("_LeftGear",
                new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.015f, RadialSegments = 10 },
                metal.Lightened(0.1f), new Vector3(-0.22f, 0.05f, 0));
            leftGear.RotateZ(Mathf.DegToRad(90));
            bodyPivot.AddChild(leftGear);
            var rightGear = CreateMeshNode("_RightGear",
                new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.015f, RadialSegments = 10 },
                metal.Lightened(0.1f), new Vector3(0.22f, 0.05f, 0));
            rightGear.RotateZ(Mathf.DegToRad(90));
            bodyPivot.AddChild(rightGear);
            root.AddChild(bodyPivot);

            // 4 articulated legs — front pair as LeftArm/RightArm, rear as LeftLeg/RightLeg
            string[] legNames = { "LeftArm", "RightArm", "LeftLeg", "RightLeg" };
            Vector3[] legPositions = {
                new Vector3(-0.18f, 0.3f, -0.12f), new Vector3(0.18f, 0.3f, -0.12f),
                new Vector3(-0.18f, 0.3f, 0.12f), new Vector3(0.18f, 0.3f, 0.12f)
            };
            for (int i = 0; i < 4; i++)
            {
                var legPivot = CreatePivot(legNames[i], legPositions[i]);
                legPivot.AddChild(CreateMeshNode("_Upper",
                    new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.02f, Height = 0.12f, RadialSegments = 4 },
                    brown.Darkened(0.1f), new Vector3(0, -0.06f, 0)));
                legPivot.AddChild(CreateMeshNode("_Lower",
                    new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.015f, Height = 0.1f, RadialSegments = 4 },
                    brown.Darkened(0.15f), new Vector3(0, -0.17f, 0)));
                legPivot.AddChild(CreateMeshNode("_Foot",
                    new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 4, Rings = 2 },
                    metal, new Vector3(0, -0.23f, 0)));
                root.AddChild(legPivot);
            }

            // Tail — 3 tapering segmented cylinders angled upward
            var tailPivot = CreatePivot("Tail", new Vector3(0, 0.35f, 0.22f));
            float[] tailRadii = { 0.04f, 0.025f, 0.012f };
            float[] tailHeights = { 0.12f, 0.1f, 0.08f };
            float yOff = 0;
            for (int i = 0; i < 3; i++)
            {
                var seg = CreateMeshNode($"_TailSeg{i}",
                    new CylinderMesh { TopRadius = tailRadii[i] * 0.7f, BottomRadius = tailRadii[i], Height = tailHeights[i], RadialSegments = 4 },
                    brown.Darkened(0.15f), new Vector3(0, yOff + tailHeights[i] * 0.3f, i * 0.06f));
                seg.RotateX(Mathf.DegToRad(-30));
                tailPivot.AddChild(seg);
                yOff += tailHeights[i] * 0.4f;
            }
            root.AddChild(tailPivot);

            return root;
        }

        private static Node3D BuildDecoyUnitBody()
        {
            var root = new Node3D();
            root.Name = "DecoyUnitBody";
            Color gold = new Color(0.7f, 0.6f, 0.2f);
            Color wood = new Color(0.45f, 0.3f, 0.15f);
            Color metal = new Color(0.35f, 0.35f, 0.38f);
            Color lockColor = new Color(0.5f, 0.45f, 0.25f);
            Color ruby = new Color(0.85f, 0.1f, 0.15f);
            Color chainColor = new Color(0.4f, 0.38f, 0.35f);

            // Body pivot — chest box with metal bands, hinges, lock plate, keyhole
            var bodyPivot = CreatePivot("Body", new Vector3(0, 0.5f, 0));
            bodyPivot.AddChild(CreateMeshNode("_ChestBox",
                new BoxMesh { Size = new Vector3(0.6f, 0.4f, 0.4f) },
                wood, Vector3.Zero));
            // Metal bands — 2 horizontal, 2 vertical
            bodyPivot.AddChild(CreateMeshNode("_HBand1",
                new BoxMesh { Size = new Vector3(0.62f, 0.03f, 0.42f) },
                metal, new Vector3(0, 0.1f, 0)));
            bodyPivot.AddChild(CreateMeshNode("_HBand2",
                new BoxMesh { Size = new Vector3(0.62f, 0.03f, 0.42f) },
                metal, new Vector3(0, -0.1f, 0)));
            bodyPivot.AddChild(CreateMeshNode("_VBand1",
                new BoxMesh { Size = new Vector3(0.03f, 0.42f, 0.42f) },
                metal, new Vector3(-0.18f, 0, 0)));
            bodyPivot.AddChild(CreateMeshNode("_VBand2",
                new BoxMesh { Size = new Vector3(0.03f, 0.42f, 0.42f) },
                metal, new Vector3(0.18f, 0, 0)));
            // Hinges on back
            bodyPivot.AddChild(CreateMeshNode("_HingeLeft",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.06f, RadialSegments = 6 },
                metal.Lightened(0.1f), new Vector3(-0.2f, 0.2f, 0.21f)));
            bodyPivot.AddChild(CreateMeshNode("_HingeRight",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.06f, RadialSegments = 6 },
                metal.Lightened(0.1f), new Vector3(0.2f, 0.2f, 0.21f)));
            // Lock plate on front
            bodyPivot.AddChild(CreateMeshNode("_LockPlate",
                new BoxMesh { Size = new Vector3(0.1f, 0.12f, 0.02f) },
                lockColor, new Vector3(0, 0, -0.21f)));
            bodyPivot.AddChild(CreateEmissiveMeshNode("_Keyhole",
                new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.025f, RadialSegments = 6 },
                gold, gold, new Vector3(0, -0.02f, -0.225f)));
            root.AddChild(bodyPivot);

            // Head pivot (lid) — lid box with ridge inset
            var headPivot = CreatePivot("Head", new Vector3(0, 0.75f, -0.08f));
            var lid = CreateMeshNode("_LidBox",
                new BoxMesh { Size = new Vector3(0.62f, 0.08f, 0.42f) },
                wood.Lightened(0.1f), Vector3.Zero);
            lid.RotateX(Mathf.DegToRad(-15));
            headPivot.AddChild(lid);
            var ridgeInset = CreateMeshNode("_RidgeInset",
                new BoxMesh { Size = new Vector3(0.5f, 0.02f, 0.3f) },
                wood.Darkened(0.1f), new Vector3(0, 0.04f, 0));
            ridgeInset.RotateX(Mathf.DegToRad(-15));
            headPivot.AddChild(ridgeInset);
            root.AddChild(headPivot);

            // 8 teeth — alternating heights
            for (int i = -3; i <= 4; i++)
            {
                float h = (i % 2 == 0) ? 0.1f : 0.06f;
                root.AddChild(CreateMeshNode($"_Tooth{i}",
                    new BoxMesh { Size = new Vector3(0.05f, h, 0.03f) },
                    Colors.White, new Vector3(i * 0.065f, 0.7f + h * 0.5f - 0.04f, -0.2f)));
            }

            // 2 emissive ruby red eyes
            root.AddChild(CreateEmissiveMeshNode("_LeftEye",
                new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 },
                ruby, ruby, new Vector3(-0.15f, 0.65f, -0.22f)));
            root.AddChild(CreateEmissiveMeshNode("_RightEye",
                new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 },
                ruby, ruby, new Vector3(0.15f, 0.65f, -0.22f)));

            // 2 gold clasps
            root.AddChild(CreateMeshNode("_LeftClasp",
                new BoxMesh { Size = new Vector3(0.04f, 0.06f, 0.03f) },
                gold, new Vector3(-0.28f, 0.5f, -0.21f)));
            root.AddChild(CreateMeshNode("_RightClasp",
                new BoxMesh { Size = new Vector3(0.04f, 0.06f, 0.03f) },
                gold, new Vector3(0.28f, 0.5f, -0.21f)));

            // Chain dangle — cylinder links + torus rings
            root.AddChild(CreateMeshNode("_ChainLink1",
                new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = 0.06f, RadialSegments = 4 },
                chainColor, new Vector3(0, 0.38f, -0.22f)));
            root.AddChild(CreateMeshNode("_ChainLink2",
                new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = 0.06f, RadialSegments = 4 },
                chainColor, new Vector3(0, 0.3f, -0.22f)));
            var chainRing1 = CreateMeshNode("_ChainRing1",
                new TorusMesh { InnerRadius = 0.015f, OuterRadius = 0.025f, Rings = 6, RingSegments = 4 },
                chainColor, new Vector3(0, 0.34f, -0.22f));
            chainRing1.RotateX(Mathf.DegToRad(90));
            root.AddChild(chainRing1);
            var chainRing2 = CreateMeshNode("_ChainRing2",
                new TorusMesh { InnerRadius = 0.012f, OuterRadius = 0.02f, Rings = 6, RingSegments = 4 },
                chainColor, new Vector3(0, 0.26f, -0.22f));
            chainRing2.RotateX(Mathf.DegToRad(90));
            root.AddChild(chainRing2);

            return root;
        }

        private static Node3D BuildWireWormBody()
        {
            var root = new Node3D();
            root.Name = "WireWormBody";
            Color green = new Color(0.4f, 0.7f, 0.3f);
            Color greenGlow = new Color(0.3f, 0.6f, 0.2f);
            Color wireColor = new Color(0.3f, 0.3f, 0.32f);
            Color eyeColor = new Color(0.9f, 1f, 0.3f);

            // Head pivot — main sphere + mandibles + antennae + emissive eyes
            var headPivot = CreatePivot("Head", new Vector3(0, 0.5f, 0));
            headPivot.AddChild(CreateMeshNode("_HeadSphere",
                new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 8, Rings = 4 },
                green, Vector3.Zero));
            // Mandibles
            var leftMandible = CreateMeshNode("_LeftMandible",
                new BoxMesh { Size = new Vector3(0.03f, 0.04f, 0.12f) },
                green.Darkened(0.2f), new Vector3(-0.08f, -0.08f, -0.15f));
            leftMandible.RotateX(Mathf.DegToRad(20));
            headPivot.AddChild(leftMandible);
            var rightMandible = CreateMeshNode("_RightMandible",
                new BoxMesh { Size = new Vector3(0.03f, 0.04f, 0.12f) },
                green.Darkened(0.2f), new Vector3(0.08f, -0.08f, -0.15f));
            rightMandible.RotateX(Mathf.DegToRad(20));
            headPivot.AddChild(rightMandible);
            // Antennae
            headPivot.AddChild(CreateMeshNode("_LeftAntenna",
                new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.012f, Height = 0.15f, RadialSegments = 4 },
                wireColor, new Vector3(-0.1f, 0.15f, -0.05f)));
            headPivot.AddChild(CreateMeshNode("_RightAntenna",
                new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.012f, Height = 0.15f, RadialSegments = 4 },
                wireColor, new Vector3(0.1f, 0.15f, -0.05f)));
            // Emissive eyes
            headPivot.AddChild(CreateEmissiveMeshNode("_LeftEye",
                new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                eyeColor, eyeColor, new Vector3(-0.1f, 0.04f, -0.16f)));
            headPivot.AddChild(CreateEmissiveMeshNode("_RightEye",
                new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                eyeColor, eyeColor, new Vector3(0.1f, 0.04f, -0.16f)));
            root.AddChild(headPivot);

            // Body pivot — 5 spheres in undulating arc with cylinder wire connectors
            var bodyPivot = CreatePivot("Body", new Vector3(0, 0.35f, 0.1f));
            float[] bodyRadii = { 0.18f, 0.16f, 0.14f, 0.12f, 0.1f };
            float[] bodyZ = { 0, 0.16f, 0.3f, 0.42f, 0.52f };
            float[] bodyY = { 0, 0.04f, 0, -0.04f, 0 };
            for (int i = 0; i < 5; i++)
            {
                bodyPivot.AddChild(CreateMeshNode($"_Seg{i}",
                    new SphereMesh { Radius = bodyRadii[i], Height = bodyRadii[i] * 2, RadialSegments = 8, Rings = 4 },
                    green, new Vector3(0, bodyY[i], bodyZ[i])));
                // Wire connector between segments
                if (i < 4)
                {
                    float midZ = (bodyZ[i] + bodyZ[i + 1]) * 0.5f;
                    float midY = (bodyY[i] + bodyY[i + 1]) * 0.5f;
                    bodyPivot.AddChild(CreateMeshNode($"_Wire{i}",
                        new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.08f, RadialSegments = 4 },
                        wireColor, new Vector3(0, midY, midZ)));
                }
            }
            root.AddChild(bodyPivot);

            // Tail pivot — tip sphere + emissive glow
            var tailPivot = CreatePivot("Tail", new Vector3(0, 0.28f, 0.65f));
            tailPivot.AddChild(CreateMeshNode("_TailTip",
                new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                green.Darkened(0.1f), Vector3.Zero));
            tailPivot.AddChild(CreateEmissiveMeshNode("_TailGlow",
                new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 },
                greenGlow, greenGlow, new Vector3(0, 0, 0.06f)));
            root.AddChild(tailPivot);

            // Scattered emissive glow nodes along body
            root.AddChild(CreateEmissiveMeshNode("_Glow1",
                new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 4, Rings = 2 },
                greenGlow, greenGlow, new Vector3(0.12f, 0.4f, 0.18f)));
            root.AddChild(CreateEmissiveMeshNode("_Glow2",
                new SphereMesh { Radius = 0.018f, Height = 0.036f, RadialSegments = 4, Rings = 2 },
                greenGlow, greenGlow, new Vector3(-0.1f, 0.33f, 0.35f)));
            root.AddChild(CreateEmissiveMeshNode("_Glow3",
                new SphereMesh { Radius = 0.015f, Height = 0.03f, RadialSegments = 4, Rings = 2 },
                greenGlow, greenGlow, new Vector3(0.08f, 0.3f, 0.5f)));

            // Exposed wire strands
            var wireStrand1 = CreateMeshNode("_WireStrand1",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.12f, RadialSegments = 4 },
                wireColor, new Vector3(-0.15f, 0.38f, 0.22f));
            wireStrand1.RotateZ(Mathf.DegToRad(30));
            root.AddChild(wireStrand1);
            var wireStrand2 = CreateMeshNode("_WireStrand2",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.1f, RadialSegments = 4 },
                wireColor, new Vector3(0.13f, 0.32f, 0.4f));
            wireStrand2.RotateZ(Mathf.DegToRad(-25));
            root.AddChild(wireStrand2);

            return root;
        }

        private static Node3D BuildCorruptedSentryBody()
        {
            var root = new Node3D();
            root.Name = "CorruptedSentryBody";
            Color skin = new Color(0.45f, 0.55f, 0.25f);
            Color armor = new Color(0.4f, 0.3f, 0.2f);
            Color corruption = new Color(0.2f, 0.9f, 0.3f);
            Color exhaust = new Color(0.3f, 0.3f, 0.32f);
            Color exhaustTip = new Color(1f, 0.5f, 0.1f);
            Color wireColor = new Color(0.35f, 0.35f, 0.38f);

            // Head pivot — skull, broken visor, eyes (one damaged), cracked panel, hanging wires
            var headPivot = CreatePivot("Head", new Vector3(0, 1.45f, 0));
            headPivot.AddChild(CreateMeshNode("_Skull",
                new SphereMesh { Radius = 0.25f, Height = 0.45f, RadialSegments = 10, Rings = 5 },
                skin.Lightened(0.1f), Vector3.Zero));
            // Broken visor plate
            headPivot.AddChild(CreateMeshNode("_VisorPlate",
                new BoxMesh { Size = new Vector3(0.3f, 0.08f, 0.04f) },
                armor.Darkened(0.1f), new Vector3(0, 0, -0.22f)));
            // Eyes — one normal, one smaller/damaged
            headPivot.AddChild(CreateEmissiveMeshNode("_LeftEye",
                new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 },
                corruption, corruption, new Vector3(-0.1f, 0.02f, -0.22f)));
            headPivot.AddChild(CreateEmissiveMeshNode("_RightEye",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                corruption, corruption * 0.6f, new Vector3(0.1f, 0.04f, -0.22f)));
            // Cracked panel on side
            headPivot.AddChild(CreateMeshNode("_CrackedPanel",
                new BoxMesh { Size = new Vector3(0.04f, 0.15f, 0.12f) },
                armor.Lightened(0.05f), new Vector3(-0.22f, -0.02f, 0)));
            // Hanging wires from head
            headPivot.AddChild(CreateMeshNode("_HangWire1",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.006f, Height = 0.12f, RadialSegments = 4 },
                wireColor, new Vector3(-0.18f, -0.18f, -0.08f)));
            headPivot.AddChild(CreateMeshNode("_HangWire2",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.006f, Height = 0.1f, RadialSegments = 4 },
                wireColor, new Vector3(0.15f, -0.15f, -0.1f)));
            root.AddChild(headPivot);

            // Torso pivot — body cylinder, chest armor, corruption cracks, back plate, exhaust
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.75f, 0));
            torsoPivot.AddChild(CreateMeshNode("_BodyCylinder",
                new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.35f, Height = 0.9f, RadialSegments = 10 },
                skin, Vector3.Zero));
            torsoPivot.AddChild(CreateMeshNode("_ChestArmor",
                new BoxMesh { Size = new Vector3(0.55f, 0.5f, 0.08f) },
                armor, new Vector3(0, 0.1f, -0.2f)));
            // Corruption crack lines (emissive green)
            torsoPivot.AddChild(CreateEmissiveMeshNode("_Crack1",
                new BoxMesh { Size = new Vector3(0.02f, 0.35f, 0.01f) },
                corruption, corruption, new Vector3(-0.12f, 0.05f, -0.25f)));
            torsoPivot.AddChild(CreateEmissiveMeshNode("_Crack2",
                new BoxMesh { Size = new Vector3(0.02f, 0.28f, 0.01f) },
                corruption, corruption, new Vector3(0.08f, 0.1f, -0.25f)));
            torsoPivot.AddChild(CreateEmissiveMeshNode("_Crack3",
                new BoxMesh { Size = new Vector3(0.15f, 0.02f, 0.01f) },
                corruption, corruption, new Vector3(0, -0.1f, -0.25f)));
            // Back plate
            torsoPivot.AddChild(CreateMeshNode("_BackPlate",
                new BoxMesh { Size = new Vector3(0.4f, 0.4f, 0.06f) },
                armor.Darkened(0.1f), new Vector3(0, 0.05f, 0.2f)));
            // Exhaust pipes
            torsoPivot.AddChild(CreateMeshNode("_ExhaustL",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.2f, RadialSegments = 6 },
                exhaust, new Vector3(-0.2f, 0.4f, 0.18f)));
            torsoPivot.AddChild(CreateMeshNode("_ExhaustR",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.2f, RadialSegments = 6 },
                exhaust, new Vector3(0.2f, 0.4f, 0.18f)));
            // Emissive exhaust tips
            torsoPivot.AddChild(CreateEmissiveMeshNode("_ExhaustTipL",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                exhaustTip, exhaustTip, new Vector3(-0.2f, 0.52f, 0.18f)));
            torsoPivot.AddChild(CreateEmissiveMeshNode("_ExhaustTipR",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                exhaustTip, exhaustTip, new Vector3(0.2f, 0.52f, 0.18f)));
            root.AddChild(torsoPivot);

            // LeftArm — upper arm, shield remnant plate, forearm
            var leftArmPivot = CreatePivot("LeftArm", new Vector3(-0.4f, 1.1f, 0));
            leftArmPivot.AddChild(CreateMeshNode("_UpperArm",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.09f, Height = 0.3f, RadialSegments = 6 },
                skin.Darkened(0.1f), new Vector3(0, -0.15f, 0)));
            leftArmPivot.AddChild(CreateMeshNode("_ShieldRemnant",
                new BoxMesh { Size = new Vector3(0.06f, 0.2f, 0.15f) },
                armor, new Vector3(-0.08f, -0.15f, -0.05f)));
            leftArmPivot.AddChild(CreateMeshNode("_Forearm",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.25f, RadialSegments = 6 },
                skin.Darkened(0.15f), new Vector3(0, -0.42f, 0)));
            root.AddChild(leftArmPivot);

            // RightArm — upper arm, forearm
            var rightArmPivot = CreatePivot("RightArm", new Vector3(0.4f, 1.1f, 0));
            rightArmPivot.AddChild(CreateMeshNode("_UpperArm",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.09f, Height = 0.3f, RadialSegments = 6 },
                skin.Darkened(0.1f), new Vector3(0, -0.15f, 0)));
            rightArmPivot.AddChild(CreateMeshNode("_Forearm",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.25f, RadialSegments = 6 },
                skin.Darkened(0.15f), new Vector3(0, -0.42f, 0)));
            root.AddChild(rightArmPivot);

            // Weapon pivot — club shaft, spiked club head, 3 protruding spikes
            var weaponPivot = CreatePivot("Weapon", new Vector3(0.5f, 1.1f, -0.2f));
            var clubShaft = CreateMeshNode("_ClubShaft",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.05f, Height = 0.6f, RadialSegments = 6 },
                new Color(0.4f, 0.3f, 0.18f), Vector3.Zero);
            clubShaft.RotateZ(Mathf.DegToRad(-30));
            weaponPivot.AddChild(clubShaft);
            weaponPivot.AddChild(CreateMeshNode("_ClubHead",
                new BoxMesh { Size = new Vector3(0.15f, 0.18f, 0.12f) },
                armor, new Vector3(-0.15f, 0.25f, 0)));
            // Protruding spike cones
            weaponPivot.AddChild(CreateMeshNode("_Spike1",
                new CylinderMesh { TopRadius = 0f, BottomRadius = 0.025f, Height = 0.08f, RadialSegments = 4 },
                armor.Lightened(0.15f), new Vector3(-0.15f, 0.35f, -0.08f)));
            weaponPivot.AddChild(CreateMeshNode("_Spike2",
                new CylinderMesh { TopRadius = 0f, BottomRadius = 0.025f, Height = 0.08f, RadialSegments = 4 },
                armor.Lightened(0.15f), new Vector3(-0.22f, 0.28f, 0)));
            weaponPivot.AddChild(CreateMeshNode("_Spike3",
                new CylinderMesh { TopRadius = 0f, BottomRadius = 0.025f, Height = 0.08f, RadialSegments = 4 },
                armor.Lightened(0.15f), new Vector3(-0.08f, 0.28f, 0.06f)));
            root.AddChild(weaponPivot);

            // LeftLeg / RightLeg — upper leg + heavy foot pad
            var leftLegPivot = CreatePivot("LeftLeg", new Vector3(-0.15f, 0.3f, 0));
            leftLegPivot.AddChild(CreateMeshNode("_UpperLeg",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.4f, RadialSegments = 6 },
                skin.Darkened(0.15f), new Vector3(0, -0.1f, 0)));
            leftLegPivot.AddChild(CreateMeshNode("_FootPad",
                new BoxMesh { Size = new Vector3(0.14f, 0.04f, 0.18f) },
                armor.Darkened(0.15f), new Vector3(0, -0.32f, 0)));
            root.AddChild(leftLegPivot);

            var rightLegPivot = CreatePivot("RightLeg", new Vector3(0.15f, 0.3f, 0));
            rightLegPivot.AddChild(CreateMeshNode("_UpperLeg",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.4f, RadialSegments = 6 },
                skin.Darkened(0.15f), new Vector3(0, -0.1f, 0)));
            rightLegPivot.AddChild(CreateMeshNode("_FootPad",
                new BoxMesh { Size = new Vector3(0.14f, 0.04f, 0.18f) },
                armor.Darkened(0.15f), new Vector3(0, -0.32f, 0)));
            root.AddChild(rightLegPivot);

            return root;
        }

        private static Node3D BuildScrapHydraBody()
        {
            var root = new Node3D();
            root.Name = "ScrapHydraBody";
            Color gold = new Color(0.85f, 0.7f, 0.2f);
            Color wood = new Color(0.5f, 0.35f, 0.18f);
            Color metal = new Color(0.4f, 0.4f, 0.42f);
            Color teeth = new Color(0.95f, 0.93f, 0.85f);
            Color redEye = new Color(1f, 0.1f, 0.05f);
            Color blueGem = new Color(0.2f, 0.5f, 1f);
            Color greenGem = new Color(0.2f, 0.9f, 0.3f);

            // Torso pivot — chest box with metal bands, rivets, scrap spikes, center gem
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.6f, 0));
            torsoPivot.AddChild(CreateMeshNode("_ChestBox",
                new BoxMesh { Size = new Vector3(0.9f, 0.6f, 0.6f) },
                wood, Vector3.Zero));
            // 3 horizontal metal bands
            for (int i = -1; i <= 1; i++)
            {
                torsoPivot.AddChild(CreateMeshNode($"_HBand{i}",
                    new BoxMesh { Size = new Vector3(0.92f, 0.03f, 0.62f) },
                    metal, new Vector3(0, i * 0.15f, 0)));
            }
            // 2 vertical bands
            torsoPivot.AddChild(CreateMeshNode("_VBandL",
                new BoxMesh { Size = new Vector3(0.03f, 0.62f, 0.62f) },
                metal, new Vector3(-0.25f, 0, 0)));
            torsoPivot.AddChild(CreateMeshNode("_VBandR",
                new BoxMesh { Size = new Vector3(0.03f, 0.62f, 0.62f) },
                metal, new Vector3(0.25f, 0, 0)));
            // 4 emissive corner rivet spheres
            float[] cx = { -0.42f, 0.42f, -0.42f, 0.42f };
            float[] cy = { 0.27f, 0.27f, -0.27f, -0.27f };
            for (int i = 0; i < 4; i++)
            {
                torsoPivot.AddChild(CreateEmissiveMeshNode($"_Rivet{i}",
                    new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                    gold, gold, new Vector3(cx[i], cy[i], -0.31f)));
            }
            // 4 protruding scrap spike cones on sides
            torsoPivot.AddChild(CreateMeshNode("_SpikeFL",
                new CylinderMesh { TopRadius = 0f, BottomRadius = 0.03f, Height = 0.1f, RadialSegments = 4 },
                metal.Lightened(0.1f), new Vector3(-0.48f, 0.15f, -0.15f)));
            torsoPivot.AddChild(CreateMeshNode("_SpikeFR",
                new CylinderMesh { TopRadius = 0f, BottomRadius = 0.03f, Height = 0.1f, RadialSegments = 4 },
                metal.Lightened(0.1f), new Vector3(0.48f, 0.15f, -0.15f)));
            torsoPivot.AddChild(CreateMeshNode("_SpikeBL",
                new CylinderMesh { TopRadius = 0f, BottomRadius = 0.03f, Height = 0.1f, RadialSegments = 4 },
                metal.Lightened(0.1f), new Vector3(-0.48f, -0.1f, 0.15f)));
            torsoPivot.AddChild(CreateMeshNode("_SpikeBR",
                new CylinderMesh { TopRadius = 0f, BottomRadius = 0.03f, Height = 0.1f, RadialSegments = 4 },
                metal.Lightened(0.1f), new Vector3(0.48f, -0.1f, 0.15f)));
            // Center gem sphere
            torsoPivot.AddChild(CreateEmissiveMeshNode("_CenterGem",
                new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 8, Rings = 4 },
                gold, gold, new Vector3(0, 0, -0.32f)));
            // Large red emissive eyes on body
            torsoPivot.AddChild(CreateEmissiveMeshNode("_LeftBodyEye",
                new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 8, Rings = 4 },
                redEye, redEye, new Vector3(-0.2f, 0.2f, -0.32f)));
            torsoPivot.AddChild(CreateEmissiveMeshNode("_RightBodyEye",
                new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 8, Rings = 4 },
                redEye, redEye, new Vector3(0.2f, 0.2f, -0.32f)));
            root.AddChild(torsoPivot);

            // Head pivot (lid/crown) — lid box, crown cylinder, 6 spikes, 3 gem studs
            var headPivot = CreatePivot("Head", new Vector3(0, 0.95f, -0.12f));
            var lid = CreateMeshNode("_LidBox",
                new BoxMesh { Size = new Vector3(0.92f, 0.1f, 0.62f) },
                wood.Lightened(0.1f), Vector3.Zero);
            lid.RotateX(Mathf.DegToRad(-20));
            headPivot.AddChild(lid);
            headPivot.AddChild(CreateEmissiveMeshNode("_Crown",
                new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.15f, Height = 0.15f, RadialSegments = 8 },
                gold, gold, new Vector3(0, 0.2f, 0.02f)));
            // 6 crown spikes
            for (int i = 0; i < 6; i++)
            {
                float angle = (float)i / 6f * Mathf.Tau;
                headPivot.AddChild(CreateEmissiveMeshNode($"_CrownSpike{i}",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.03f, Height = 0.12f, RadialSegments = 4 },
                    gold, gold, new Vector3(Mathf.Cos(angle) * 0.15f, 0.33f, 0.02f + Mathf.Sin(angle) * 0.15f)));
            }
            // 3 emissive gem studs (red/blue/green)
            Color[] gemColors = { redEye, blueGem, greenGem };
            for (int i = 0; i < 3; i++)
            {
                float gAngle = (float)i / 3f * Mathf.Tau;
                headPivot.AddChild(CreateEmissiveMeshNode($"_GemStud{i}",
                    new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                    gemColors[i], gemColors[i],
                    new Vector3(Mathf.Cos(gAngle) * 0.1f, 0.28f, 0.02f + Mathf.Sin(gAngle) * 0.1f)));
            }
            root.AddChild(headPivot);

            // Center neck (under Head) — 3 segments + jaw + teeth + eye
            var centerNeck = CreatePivot("_CenterNeck", new Vector3(0, 1.15f, -0.15f));
            for (int s = 0; s < 3; s++)
            {
                centerNeck.AddChild(CreateMeshNode($"_NeckSeg{s}",
                    new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.07f, Height = 0.12f, RadialSegments = 6 },
                    wood.Darkened(0.1f), new Vector3(0, s * 0.14f, -s * 0.06f)));
            }
            centerNeck.AddChild(CreateMeshNode("_Jaw",
                new BoxMesh { Size = new Vector3(0.14f, 0.08f, 0.1f) },
                wood, new Vector3(0, 0.42f, -0.22f)));
            centerNeck.AddChild(CreateMeshNode("_JawToothL",
                new BoxMesh { Size = new Vector3(0.03f, 0.05f, 0.02f) },
                teeth, new Vector3(-0.04f, 0.38f, -0.27f)));
            centerNeck.AddChild(CreateMeshNode("_JawToothR",
                new BoxMesh { Size = new Vector3(0.03f, 0.05f, 0.02f) },
                teeth, new Vector3(0.04f, 0.38f, -0.27f)));
            centerNeck.AddChild(CreateEmissiveMeshNode("_NeckEye",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                redEye, redEye, new Vector3(0, 0.46f, -0.24f)));
            root.AddChild(centerNeck);

            // LeftArm pivot — side hydra neck (left)
            var leftNeck = CreatePivot("LeftArm", new Vector3(-0.35f, 1.0f, -0.1f));
            for (int s = 0; s < 3; s++)
            {
                leftNeck.AddChild(CreateMeshNode($"_NeckSeg{s}",
                    new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 0.12f, RadialSegments = 6 },
                    wood.Darkened(0.1f), new Vector3(-s * 0.06f, s * 0.12f, -s * 0.05f)));
            }
            leftNeck.AddChild(CreateMeshNode("_SnapJaw",
                new BoxMesh { Size = new Vector3(0.12f, 0.07f, 0.09f) },
                wood, new Vector3(-0.18f, 0.36f, -0.18f)));
            leftNeck.AddChild(CreateMeshNode("_ToothA",
                new BoxMesh { Size = new Vector3(0.025f, 0.04f, 0.02f) },
                teeth, new Vector3(-0.15f, 0.32f, -0.23f)));
            leftNeck.AddChild(CreateMeshNode("_ToothB",
                new BoxMesh { Size = new Vector3(0.025f, 0.04f, 0.02f) },
                teeth, new Vector3(-0.21f, 0.32f, -0.23f)));
            leftNeck.AddChild(CreateEmissiveMeshNode("_NeckEye",
                new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                redEye, redEye, new Vector3(-0.18f, 0.4f, -0.2f)));
            root.AddChild(leftNeck);

            // RightArm pivot — side hydra neck (right)
            var rightNeck = CreatePivot("RightArm", new Vector3(0.35f, 1.0f, -0.1f));
            for (int s = 0; s < 3; s++)
            {
                rightNeck.AddChild(CreateMeshNode($"_NeckSeg{s}",
                    new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 0.12f, RadialSegments = 6 },
                    wood.Darkened(0.1f), new Vector3(s * 0.06f, s * 0.12f, -s * 0.05f)));
            }
            rightNeck.AddChild(CreateMeshNode("_SnapJaw",
                new BoxMesh { Size = new Vector3(0.12f, 0.07f, 0.09f) },
                wood, new Vector3(0.18f, 0.36f, -0.18f)));
            rightNeck.AddChild(CreateMeshNode("_ToothA",
                new BoxMesh { Size = new Vector3(0.025f, 0.04f, 0.02f) },
                teeth, new Vector3(0.15f, 0.32f, -0.23f)));
            rightNeck.AddChild(CreateMeshNode("_ToothB",
                new BoxMesh { Size = new Vector3(0.025f, 0.04f, 0.02f) },
                teeth, new Vector3(0.21f, 0.32f, -0.23f)));
            rightNeck.AddChild(CreateEmissiveMeshNode("_NeckEye",
                new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                redEye, redEye, new Vector3(0.18f, 0.4f, -0.2f)));
            root.AddChild(rightNeck);

            // Main mouth teeth (8 on body front)
            for (int i = -3; i <= 4; i++)
            {
                float h = (i % 2 == 0) ? 0.12f : 0.08f;
                root.AddChild(CreateMeshNode($"_Tooth{i}",
                    new BoxMesh { Size = new Vector3(0.06f, h, 0.04f) },
                    teeth, new Vector3(i * 0.09f, 0.88f + h * 0.5f - 0.05f, -0.3f)));
            }

            // Hinge cylinders at jaw corners
            root.AddChild(CreateMeshNode("_HingeL",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.05f, RadialSegments = 6 },
                metal, new Vector3(-0.4f, 0.9f, -0.28f)));
            root.AddChild(CreateMeshNode("_HingeR",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.05f, RadialSegments = 6 },
                metal, new Vector3(0.4f, 0.9f, -0.28f)));

            // LeftLeg / RightLeg — stubby support pads
            var leftLegPivot = CreatePivot("LeftLeg", new Vector3(-0.25f, 0.15f, 0));
            leftLegPivot.AddChild(CreateMeshNode("_Pad",
                new BoxMesh { Size = new Vector3(0.2f, 0.08f, 0.3f) },
                wood.Darkened(0.2f), Vector3.Zero));
            root.AddChild(leftLegPivot);

            var rightLegPivot = CreatePivot("RightLeg", new Vector3(0.25f, 0.15f, 0));
            rightLegPivot.AddChild(CreateMeshNode("_Pad",
                new BoxMesh { Size = new Vector3(0.2f, 0.08f, 0.3f) },
                wood.Darkened(0.2f), Vector3.Zero));
            root.AddChild(rightLegPivot);

            return root;
        }

        private static Node3D BuildAxisAvatarBody()
        {
            var root = new Node3D();
            root.Name = "AxisAvatarBody";
            Color purple = new Color(0.45f, 0.25f, 0.55f);
            Color gold = new Color(0.85f, 0.7f, 0.2f);
            Color holoBlue = new Color(0.3f, 0.7f, 1f);
            Color bladeColor = new Color(0.8f, 0.8f, 0.9f);
            Color bladeGlow = new Color(0.6f, 0.5f, 0.9f);

            // Head pivot — skull, face plate, eyes, visor glow, floating crown, data streams
            var headPivot = CreatePivot("Head", new Vector3(0, 2.05f, 0));
            headPivot.AddChild(CreateMeshNode("_Skull",
                new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 10, Rings = 5 },
                purple.Lightened(0.15f), Vector3.Zero));
            headPivot.AddChild(CreateMeshNode("_FacePlate",
                new BoxMesh { Size = new Vector3(0.22f, 0.12f, 0.04f) },
                purple.Darkened(0.1f), new Vector3(0, -0.02f, -0.18f)));
            // Emissive holoBlue eyes
            headPivot.AddChild(CreateEmissiveMeshNode("_LeftEye",
                new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                holoBlue, holoBlue, new Vector3(-0.07f, 0.02f, -0.19f)));
            headPivot.AddChild(CreateEmissiveMeshNode("_RightEye",
                new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                holoBlue, holoBlue, new Vector3(0.07f, 0.02f, -0.19f)));
            // Visor glow bar
            headPivot.AddChild(CreateEmissiveMeshNode("_VisorGlow",
                new BoxMesh { Size = new Vector3(0.2f, 0.02f, 0.01f) },
                holoBlue, holoBlue, new Vector3(0, 0, -0.2f)));
            // Floating crown (base + 5 spikes + center gem)
            headPivot.AddChild(CreateEmissiveMeshNode("_CrownBase",
                new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.18f, Height = 0.12f, RadialSegments = 8 },
                gold, gold, new Vector3(0, 0.25f, 0)));
            for (int i = 0; i < 5; i++)
            {
                float angle = (float)i / 5f * Mathf.Tau;
                headPivot.AddChild(CreateEmissiveMeshNode($"_CrownSpike{i}",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.025f, Height = 0.15f, RadialSegments = 4 },
                    gold, gold, new Vector3(Mathf.Cos(angle) * 0.18f, 0.38f, Mathf.Sin(angle) * 0.18f)));
            }
            headPivot.AddChild(CreateEmissiveMeshNode("_CrownGem",
                new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 },
                holoBlue, holoBlue, new Vector3(0, 0.35f, 0)));
            // Upward data stream emissive lines
            headPivot.AddChild(CreateEmissiveMeshNode("_DataStream1",
                new BoxMesh { Size = new Vector3(0.01f, 0.2f, 0.01f) },
                holoBlue, holoBlue, new Vector3(-0.08f, 0.5f, 0)));
            headPivot.AddChild(CreateEmissiveMeshNode("_DataStream2",
                new BoxMesh { Size = new Vector3(0.01f, 0.15f, 0.01f) },
                holoBlue, holoBlue, new Vector3(0.08f, 0.48f, 0)));
            root.AddChild(headPivot);

            // Torso pivot — body, segmented armor, holographic panels, status lights, cape, wings
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 1.3f, 0));
            torsoPivot.AddChild(CreateMeshNode("_BodyCylinder",
                new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.22f, Height = 1.0f, RadialSegments = 10 },
                purple, Vector3.Zero));
            // 4 overlapping segmented armor plates
            for (int i = 0; i < 4; i++)
            {
                float yOff = 0.3f - i * 0.18f;
                torsoPivot.AddChild(CreateMeshNode($"_ArmorPlate{i}",
                    new BoxMesh { Size = new Vector3(0.5f - i * 0.04f, 0.12f, 0.06f) },
                    purple.Darkened(0.05f + i * 0.03f), new Vector3(0, yOff, -0.2f)));
            }
            // Emissive holographic side panels
            torsoPivot.AddChild(CreateEmissiveMeshNode("_HoloPanelL",
                new BoxMesh { Size = new Vector3(0.02f, 0.4f, 0.15f) },
                holoBlue, holoBlue, new Vector3(-0.3f, 0, 0)));
            torsoPivot.AddChild(CreateEmissiveMeshNode("_HoloPanelR",
                new BoxMesh { Size = new Vector3(0.02f, 0.4f, 0.15f) },
                holoBlue, holoBlue, new Vector3(0.3f, 0, 0)));
            // 3 status light spheres (red/green/blue)
            Color[] statusColors = { new Color(1f, 0.2f, 0.1f), new Color(0.2f, 1f, 0.3f), new Color(0.2f, 0.4f, 1f) };
            for (int i = 0; i < 3; i++)
            {
                torsoPivot.AddChild(CreateEmissiveMeshNode($"_StatusLight{i}",
                    new SphereMesh { Radius = 0.015f, Height = 0.03f, RadialSegments = 6, Rings = 3 },
                    statusColors[i], statusColors[i], new Vector3(-0.08f + i * 0.08f, -0.15f, -0.23f)));
            }
            // Back cape box
            torsoPivot.AddChild(CreateMeshNode("_Cape",
                new BoxMesh { Size = new Vector3(0.35f, 0.6f, 0.02f) },
                purple.Darkened(0.15f), new Vector3(0, -0.1f, 0.18f)));
            torsoPivot.AddChild(CreateEmissiveMeshNode("_CapeEdgeGlow",
                new BoxMesh { Size = new Vector3(0.36f, 0.02f, 0.01f) },
                holoBlue, holoBlue, new Vector3(0, -0.4f, 0.19f)));
            // Energy wing boxes (emissive, angled outward)
            var leftWing = CreateEmissiveMeshNode("_LeftWing",
                new BoxMesh { Size = new Vector3(0.3f, 0.35f, 0.015f) },
                holoBlue, holoBlue, new Vector3(-0.35f, 0.15f, 0.12f));
            leftWing.RotateY(Mathf.DegToRad(25));
            torsoPivot.AddChild(leftWing);
            var rightWing = CreateEmissiveMeshNode("_RightWing",
                new BoxMesh { Size = new Vector3(0.3f, 0.35f, 0.015f) },
                holoBlue, holoBlue, new Vector3(0.35f, 0.15f, 0.12f));
            rightWing.RotateY(Mathf.DegToRad(-25));
            torsoPivot.AddChild(rightWing);
            root.AddChild(torsoPivot);

            // LeftArm — pauldron, upper arm, forearm, hand, 2 fingers
            var leftArmPivot = CreatePivot("LeftArm", new Vector3(-0.4f, 1.75f, 0));
            leftArmPivot.AddChild(CreateEmissiveMeshNode("_Pauldron",
                new SphereMesh { Radius = 0.15f, Height = 0.2f, RadialSegments = 8, Rings = 4 },
                gold, gold, Vector3.Zero));
            leftArmPivot.AddChild(CreateMeshNode("_UpperArm",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.3f, RadialSegments = 6 },
                purple.Darkened(0.1f), new Vector3(0, -0.25f, 0)));
            leftArmPivot.AddChild(CreateMeshNode("_Forearm",
                new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.06f, Height = 0.25f, RadialSegments = 6 },
                purple.Darkened(0.15f), new Vector3(0, -0.52f, 0)));
            leftArmPivot.AddChild(CreateMeshNode("_Hand",
                new BoxMesh { Size = new Vector3(0.08f, 0.06f, 0.06f) },
                purple.Darkened(0.1f), new Vector3(0, -0.68f, 0)));
            leftArmPivot.AddChild(CreateMeshNode("_FingerA",
                new BoxMesh { Size = new Vector3(0.02f, 0.04f, 0.02f) },
                purple.Darkened(0.15f), new Vector3(-0.02f, -0.73f, -0.02f)));
            leftArmPivot.AddChild(CreateMeshNode("_FingerB",
                new BoxMesh { Size = new Vector3(0.02f, 0.04f, 0.02f) },
                purple.Darkened(0.15f), new Vector3(0.02f, -0.73f, -0.02f)));
            root.AddChild(leftArmPivot);

            // RightArm — pauldron, upper arm, forearm, hand, 2 fingers
            var rightArmPivot = CreatePivot("RightArm", new Vector3(0.4f, 1.75f, 0));
            rightArmPivot.AddChild(CreateEmissiveMeshNode("_Pauldron",
                new SphereMesh { Radius = 0.15f, Height = 0.2f, RadialSegments = 8, Rings = 4 },
                gold, gold, Vector3.Zero));
            rightArmPivot.AddChild(CreateMeshNode("_UpperArm",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.3f, RadialSegments = 6 },
                purple.Darkened(0.1f), new Vector3(0, -0.25f, 0)));
            rightArmPivot.AddChild(CreateMeshNode("_Forearm",
                new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.06f, Height = 0.25f, RadialSegments = 6 },
                purple.Darkened(0.15f), new Vector3(0, -0.52f, 0)));
            rightArmPivot.AddChild(CreateMeshNode("_Hand",
                new BoxMesh { Size = new Vector3(0.08f, 0.06f, 0.06f) },
                purple.Darkened(0.1f), new Vector3(0, -0.68f, 0)));
            rightArmPivot.AddChild(CreateMeshNode("_FingerA",
                new BoxMesh { Size = new Vector3(0.02f, 0.04f, 0.02f) },
                purple.Darkened(0.15f), new Vector3(-0.02f, -0.73f, -0.02f)));
            rightArmPivot.AddChild(CreateMeshNode("_FingerB",
                new BoxMesh { Size = new Vector3(0.02f, 0.04f, 0.02f) },
                purple.Darkened(0.15f), new Vector3(0.02f, -0.73f, -0.02f)));
            root.AddChild(rightArmPivot);

            // Weapon pivot — emissive blade, edge glow, hilt, gem, grip
            var weaponPivot = CreatePivot("Weapon", new Vector3(0.55f, 1.1f, -0.2f));
            var blade = CreateEmissiveMeshNode("_Blade",
                new BoxMesh { Size = new Vector3(0.1f, 1.0f, 0.04f) },
                bladeColor, bladeGlow, new Vector3(0, 0.3f, 0));
            blade.RotateZ(Mathf.DegToRad(-15));
            weaponPivot.AddChild(blade);
            var edgeGlow = CreateEmissiveMeshNode("_BladeEdge",
                new BoxMesh { Size = new Vector3(0.01f, 0.95f, 0.01f) },
                holoBlue, holoBlue, new Vector3(-0.05f, 0.3f, -0.02f));
            edgeGlow.RotateZ(Mathf.DegToRad(-15));
            weaponPivot.AddChild(edgeGlow);
            weaponPivot.AddChild(CreateMeshNode("_Hilt",
                new BoxMesh { Size = new Vector3(0.25f, 0.06f, 0.06f) },
                gold, new Vector3(0, -0.22f, 0)));
            weaponPivot.AddChild(CreateEmissiveMeshNode("_HiltGem",
                new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                holoBlue, holoBlue, new Vector3(0, -0.22f, -0.04f)));
            weaponPivot.AddChild(CreateMeshNode("_Grip",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.12f, RadialSegments = 6 },
                purple.Darkened(0.2f), new Vector3(0, -0.32f, 0)));
            root.AddChild(weaponPivot);

            // LeftLeg — upper leg, knee joint, lower leg, foot plate
            var leftLegPivot = CreatePivot("LeftLeg", new Vector3(-0.14f, 0.7f, 0));
            leftLegPivot.AddChild(CreateMeshNode("_UpperLeg",
                new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.08f, Height = 0.35f, RadialSegments = 6 },
                purple.Darkened(0.2f), new Vector3(0, -0.1f, 0)));
            leftLegPivot.AddChild(CreateMeshNode("_KneeJoint",
                new SphereMesh { Radius = 0.06f, Height = 0.1f, RadialSegments = 6, Rings = 3 },
                purple.Darkened(0.1f), new Vector3(0, -0.3f, 0)));
            leftLegPivot.AddChild(CreateMeshNode("_LowerLeg",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.3f, RadialSegments = 6 },
                purple.Darkened(0.25f), new Vector3(0, -0.5f, 0)));
            leftLegPivot.AddChild(CreateMeshNode("_FootPlate",
                new BoxMesh { Size = new Vector3(0.12f, 0.03f, 0.16f) },
                purple.Darkened(0.15f), new Vector3(0, -0.67f, 0)));
            root.AddChild(leftLegPivot);

            // RightLeg — upper leg, knee joint, lower leg, foot plate
            var rightLegPivot = CreatePivot("RightLeg", new Vector3(0.14f, 0.7f, 0));
            rightLegPivot.AddChild(CreateMeshNode("_UpperLeg",
                new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.08f, Height = 0.35f, RadialSegments = 6 },
                purple.Darkened(0.2f), new Vector3(0, -0.1f, 0)));
            rightLegPivot.AddChild(CreateMeshNode("_KneeJoint",
                new SphereMesh { Radius = 0.06f, Height = 0.1f, RadialSegments = 6, Rings = 3 },
                purple.Darkened(0.1f), new Vector3(0, -0.3f, 0)));
            rightLegPivot.AddChild(CreateMeshNode("_LowerLeg",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.3f, RadialSegments = 6 },
                purple.Darkened(0.25f), new Vector3(0, -0.5f, 0)));
            rightLegPivot.AddChild(CreateMeshNode("_FootPlate",
                new BoxMesh { Size = new Vector3(0.12f, 0.03f, 0.16f) },
                purple.Darkened(0.15f), new Vector3(0, -0.67f, 0)));
            root.AddChild(rightLegPivot);

            return root;
        }

        private static Node3D BuildDefaultEnemyBody()
        {
            var root = new Node3D();
            root.Name = "EnemyBody";

            var body = CreateMeshNode("Body", new CapsuleMesh { Radius = 0.3f, Height = 1.2f, RadialSegments = 8, Rings = 4 },
                new Color(0.8f, 0.2f, 0.2f), new Vector3(0, 0.6f, 0));
            root.AddChild(body);

            return root;
        }

        // ── Item Meshes ──

        public static Mesh BuildItemMesh(ItemInstance item)
        {
            // Try model asset first
            string itemModelId = GetItemModelId(item);
            if (itemModelId != null)
            {
                var model = ModelLibrary.TryLoad("item", itemModelId);
                if (model != null)
                {
                    var mesh = FindMeshInModel(model);
                    if (mesh != null) return mesh;
                    // Model had no mesh, free it and fall through
                    model.QueueFree();
                }
            }

            // Procedural fallback
            if (item.BaseData is EquipmentData equipment)
            {
                return equipment.Id switch
                {
                    "base_shield" => BuildShieldMesh(),
                    "base_helmet" => BuildHelmetMesh(),
                    "base_chestplate" => BuildChestplateMesh(),
                    "base_robe" => BuildRobeMesh(),
                    "base_greaves" => BuildGreavesMesh(),
                    "base_boots" => BuildBootsMesh(),
                    "base_gauntlets" => BuildGauntletsMesh(),
                    "base_amulet" => BuildAmuletMesh(),
                    "base_ring" => BuildRingMesh(),
                    "base_cloak" => BuildCloakMesh(),
                    _ => equipment.Slot switch
                    {
                        EquipmentSlot.MainHand or EquipmentSlot.OffHand => BuildDefaultItemMesh(),
                        EquipmentSlot.Head => BuildHelmetMesh(),
                        EquipmentSlot.Chest => BuildChestplateMesh(),
                        EquipmentSlot.Legs => BuildGreavesMesh(),
                        EquipmentSlot.Feet => BuildBootsMesh(),
                        EquipmentSlot.Hands => BuildGauntletsMesh(),
                        EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => BuildRingMesh(),
                        EquipmentSlot.Amulet => BuildAmuletMesh(),
                        EquipmentSlot.Back => BuildCloakMesh(),
                        _ => BuildDefaultItemMesh()
                    }
                };
            }

            if (item.BaseData.Type == ItemType.Consumable)
            {
                return item.BaseData.Id switch
                {
                    string id when id.StartsWith("potion_health") => BuildRepairKitMesh(),
                    string id when id.StartsWith("potion_mana") => BuildBatteryPackMesh(),
                    "elixir_fortitude" => BuildPlatingBoosterMesh(),
                    "overclock_injector" => BuildOverclockInjectorMesh(),
                    _ => BuildPotionMesh()
                };
            }

            return BuildDefaultItemMesh();
        }

        /// <summary>
        /// Try to load an item as a full Node3D model (for reparenting into scene).
        /// Returns null if no model asset available.
        /// </summary>
        public static Node3D TryLoadItemModel(ItemInstance item)
        {
            string itemModelId = GetItemModelId(item);
            if (itemModelId == null) return null;

            var model = ModelLibrary.TryLoad("item", itemModelId);
            if (model != null)
                ScaleModelToFit(model, 0.4f);
            return model;
        }

        private static string GetItemModelId(ItemInstance item)
        {
            return item.BaseData.Id;
        }

        /// <summary>
        /// Build a multi-part Node3D item model (better visuals than single mesh).
        /// </summary>
        public static Node3D BuildItemModel(ItemInstance item)
        {
            if (item.BaseData is EquipmentData equipment)
            {
                // Try loading a real weapon model from ModelLibrary first
                if (equipment.Slot == EquipmentSlot.MainHand || equipment.Slot == EquipmentSlot.OffHand)
                {
                    var weaponModel = TryLoadWeaponModel(equipment);
                    if (weaponModel != null) return weaponModel;
                }

                // Fall back to procedural models
                return equipment.Id switch
                {
                    "base_pistol" => BuildPistolModel(),
                    "base_rifle" => BuildRifleModel(),
                    "base_shotgun" => BuildShotgunModel(),
                    "base_launcher" => BuildLauncherModel(),
                    "base_repeater" => BuildRepeaterModel(),
                    "base_shield" => BuildShieldModel(),
                    "base_helmet" => BuildHelmetModel(),
                    "base_chestplate" => BuildChestplateModel(),
                    "base_robe" => BuildRobeModel(),
                    "base_greaves" => BuildGreavesModel(),
                    "base_boots" => BuildBootsModel(),
                    "base_gauntlets" => BuildGauntletsModel(),
                    "base_amulet" => BuildAmuletModel(),
                    "base_ring" => BuildRingModel(),
                    "base_cloak" => BuildCloakModel(),
                    _ => equipment.Slot switch
                    {
                        EquipmentSlot.MainHand or EquipmentSlot.OffHand => BuildPistolModel(),
                        EquipmentSlot.Head => BuildHelmetModel(),
                        EquipmentSlot.Chest => BuildChestplateModel(),
                        EquipmentSlot.Legs => BuildGreavesModel(),
                        EquipmentSlot.Feet => BuildBootsModel(),
                        EquipmentSlot.Hands => BuildGauntletsModel(),
                        EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => BuildRingModel(),
                        EquipmentSlot.Amulet => BuildAmuletModel(),
                        EquipmentSlot.Back => BuildCloakModel(),
                        _ => BuildDefaultItemModel()
                    }
                };
            }

            if (item.BaseData.Type == ItemType.Consumable)
            {
                return item.BaseData.Id switch
                {
                    string id when id.StartsWith("potion_health") => BuildRepairKitModel(),
                    string id when id.StartsWith("potion_mana") => BuildBatteryPackModel(),
                    "elixir_fortitude" => BuildPlatingBoosterModel(),
                    "overclock_injector" => BuildOverclockInjectorModel(),
                    _ => BuildPotionModel()
                };
            }

            if (item.BaseData is LootBoxData lootBox)
            {
                var boxModel = BuildLootBoxModel(lootBox.Tier);
                boxModel.Scale *= 0.25f;
                ApplyLootBoxTierMaterial(boxModel, lootBox.Tier);
                LootBoxPresenter.Attach(boxModel, lootBox.Tier);
                return boxModel;
            }

            return BuildDefaultItemModel();
        }

        private static Node3D BuildSwordModel()
        {
            var root = new Node3D();
            root.Name = "SwordItem";

            // Tapered blade
            var blade = CreateMeshNode("Blade", new BoxMesh { Size = new Vector3(0.06f, 0.4f, 0.02f) },
                new Color(0.8f, 0.82f, 0.85f), new Vector3(0, 0.22f, 0));
            root.AddChild(blade);

            // Crossguard
            var guard = CreateMeshNode("Guard", new BoxMesh { Size = new Vector3(0.18f, 0.03f, 0.04f) },
                new Color(0.5f, 0.4f, 0.2f), Vector3.Zero);
            root.AddChild(guard);

            // Grip
            var grip = CreateMeshNode("Grip", new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.12f, RadialSegments = 6 },
                new Color(0.35f, 0.25f, 0.12f), new Vector3(0, -0.08f, 0));
            root.AddChild(grip);

            // Pommel
            var pommel = CreateMeshNode("Pommel", new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 6, Rings = 3 },
                new Color(0.6f, 0.5f, 0.25f), new Vector3(0, -0.16f, 0));
            root.AddChild(pommel);

            return root;
        }

        private static Node3D BuildHelmetModel()
        {
            var root = new Node3D();
            root.Name = "HelmetItem";

            // Half-sphere dome
            var dome = CreateMeshNode("Dome", new SphereMesh { Radius = 0.18f, Height = 0.22f, RadialSegments = 10, Rings = 5 },
                new Color(0.55f, 0.55f, 0.6f), new Vector3(0, 0.05f, 0));
            root.AddChild(dome);

            // Visor slit
            var visor = CreateMeshNode("Visor", new BoxMesh { Size = new Vector3(0.2f, 0.03f, 0.02f) },
                new Color(0.1f, 0.1f, 0.1f), new Vector3(0, 0.02f, -0.17f));
            root.AddChild(visor);

            // Crest ridge
            var crest = CreateMeshNode("Crest", new BoxMesh { Size = new Vector3(0.03f, 0.1f, 0.25f) },
                new Color(0.7f, 0.3f, 0.2f), new Vector3(0, 0.16f, 0));
            root.AddChild(crest);

            return root;
        }

        private static Node3D BuildChestplateModel()
        {
            var root = new Node3D();
            root.Name = "ChestplateItem";

            // Torso plate
            var torso = CreateMeshNode("Plate", new BoxMesh { Size = new Vector3(0.3f, 0.25f, 0.12f) },
                new Color(0.5f, 0.52f, 0.55f), Vector3.Zero);
            root.AddChild(torso);

            // Left pauldron
            var leftPauldron = CreateMeshNode("LeftPauldron", new SphereMesh { Radius = 0.08f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                new Color(0.55f, 0.55f, 0.6f), new Vector3(-0.2f, 0.1f, 0));
            root.AddChild(leftPauldron);

            // Right pauldron
            var rightPauldron = CreateMeshNode("RightPauldron", new SphereMesh { Radius = 0.08f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                new Color(0.55f, 0.55f, 0.6f), new Vector3(0.2f, 0.1f, 0));
            root.AddChild(rightPauldron);

            return root;
        }

        private static Node3D BuildPotionModel()
        {
            var root = new Node3D();
            root.Name = "PotionItem";

            // Bottle body (glass-like)
            var body = CreateMeshNode("Body", new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.09f, Height = 0.18f, RadialSegments = 8 },
                new Color(0.6f, 0.8f, 0.9f, 0.5f), Vector3.Zero);
            var bodyMat = body.MaterialOverride as StandardMaterial3D;
            if (bodyMat != null)
                bodyMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            root.AddChild(body);

            // Neck
            var neck = CreateMeshNode("Neck", new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 0.06f, RadialSegments = 6 },
                new Color(0.6f, 0.8f, 0.9f, 0.5f), new Vector3(0, 0.12f, 0));
            var neckMat = neck.MaterialOverride as StandardMaterial3D;
            if (neckMat != null)
                neckMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            root.AddChild(neck);

            // Cork
            var cork = CreateMeshNode("Cork", new SphereMesh { Radius = 0.035f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                new Color(0.6f, 0.45f, 0.25f), new Vector3(0, 0.17f, 0));
            root.AddChild(cork);

            // Liquid inside (slightly smaller, colored)
            var liquid = CreateEmissiveMeshNode("Liquid",
                new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.08f, Height = 0.14f, RadialSegments = 8 },
                new Color(0.2f, 0.9f, 0.3f), new Color(0.1f, 0.7f, 0.2f),
                new Vector3(0, -0.01f, 0));
            root.AddChild(liquid);

            return root;
        }

        private static Node3D BuildRingModel()
        {
            var root = new Node3D();
            root.Name = "RingItem";

            var ring = CreateMeshNode("Ring", new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.1f, Rings = 12, RingSegments = 8 },
                new Color(0.7f, 0.6f, 0.2f), Vector3.Zero);
            root.AddChild(ring);

            return root;
        }

        private static Node3D BuildAmuletModel()
        {
            var root = new Node3D();
            root.Name = "AmuletItem";

            // Chain hint (thin cylinder)
            var chain = CreateMeshNode("Chain", new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.01f, Height = 0.15f, RadialSegments = 4 },
                new Color(0.6f, 0.55f, 0.3f), new Vector3(0, 0.1f, 0));
            root.AddChild(chain);

            // Pendant (teardrop: sphere)
            var pendant = CreateEmissiveMeshNode("Pendant",
                new SphereMesh { Radius = 0.08f, Height = 0.12f, RadialSegments = 8, Rings = 4 },
                new Color(0.3f, 0.5f, 0.8f), new Color(0.2f, 0.4f, 0.7f), Vector3.Zero);
            root.AddChild(pendant);

            // Small cone below
            var drop = CreateMeshNode("Drop", new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.0f, Height = 0.08f, RadialSegments = 6 },
                new Color(0.3f, 0.5f, 0.8f), new Vector3(0, -0.08f, 0));
            root.AddChild(drop);

            return root;
        }

        // ── New Equipment Models ──

        private static Node3D BuildStaffModel()
        {
            var root = new Node3D();
            root.Name = "StaffItem";
            Color iron = new Color(0.35f, 0.35f, 0.38f);
            Color copper = new Color(0.72f, 0.45f, 0.2f);
            Color rubber = new Color(0.15f, 0.12f, 0.1f);
            Color energy = new Color(0.3f, 0.7f, 1f);

            // Iron shaft
            var shaft = CreateMeshNode("Shaft",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.6f, RadialSegments = 6 },
                iron, new Vector3(0, 0.05f, 0));
            root.AddChild(shaft);

            // Copper coil wrapping upper shaft
            var coil = CreateMeshNode("Coil",
                new TorusMesh { InnerRadius = 0.03f, OuterRadius = 0.055f, Rings = 10, RingSegments = 6 },
                copper, new Vector3(0, 0.25f, 0));
            root.AddChild(coil);

            // Emissive energy orb at top
            var orb = CreateEmissiveMeshNode("EnergyOrb",
                new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 8, Rings = 4 },
                energy, energy, new Vector3(0, 0.4f, 0));
            root.AddChild(orb);

            // Rubber grip at bottom
            var grip = CreateMeshNode("Grip",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.028f, Height = 0.12f, RadialSegments = 6 },
                rubber, new Vector3(0, -0.2f, 0));
            root.AddChild(grip);

            return root;
        }

        private static Node3D BuildDaggerModel()
        {
            var root = new Node3D();
            root.Name = "DaggerItem";
            Color blade = new Color(0.75f, 0.78f, 0.82f);
            Color guard = new Color(0.4f, 0.35f, 0.25f);
            Color wire = new Color(0.3f, 0.22f, 0.12f);

            // Short blade
            var bladeNode = CreateMeshNode("Blade",
                new BoxMesh { Size = new Vector3(0.04f, 0.22f, 0.015f) },
                blade, new Vector3(0, 0.13f, 0));
            root.AddChild(bladeNode);

            // Cone tip
            var tip = CreateMeshNode("Tip",
                new CylinderMesh { TopRadius = 0f, BottomRadius = 0.02f, Height = 0.06f, RadialSegments = 6 },
                blade, new Vector3(0, 0.27f, 0));
            root.AddChild(tip);

            // Small crossguard
            var crossguard = CreateMeshNode("Guard",
                new BoxMesh { Size = new Vector3(0.1f, 0.02f, 0.03f) },
                guard, Vector3.Zero);
            root.AddChild(crossguard);

            // Wire grip
            var grip = CreateMeshNode("Grip",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.018f, Height = 0.08f, RadialSegments = 6 },
                wire, new Vector3(0, -0.06f, 0));
            root.AddChild(grip);

            return root;
        }

        // ── Weapon Model Loading (FBX-first with procedural fallback) ──

        /// <summary>
        /// Try to load a real weapon FBX model from ModelLibrary.
        /// Maps WeaponType to model aliases, scales to fit the player's hand.
        /// Returns null if no model found (caller falls back to procedural).
        /// </summary>
        private static Node3D TryLoadWeaponModel(EquipmentData equipment)
        {
            // Map weapon type to ModelLibrary alias
            string modelKey = equipment.WeaponType switch
            {
                WeaponType.Pistol => "pistol",
                WeaponType.Rifle => "rifle",
                WeaponType.Shotgun => "shotgun",
                WeaponType.Launcher => "launcher",
                WeaponType.Repeater => "repeater",
                WeaponType.BladeRing => "blade_ring",
                WeaponType.FlailChain => "flail_chain",
                WeaponType.ShockCoil => "shock_coil",
                WeaponType.FlameThrower => "flame_thrower",
                _ => null,
            };

            if (modelKey == null) return null;

            var model = ModelLibrary.TryLoad("weapon", modelKey);
            if (model == null) return null;

            // Apply weapon texture atlas if meshes are untextured (white)
            ApplyWeaponTextures(model);

            var root = new Node3D();
            root.Name = $"{equipment.WeaponType}Model";

            // Compute centering BEFORE scaling — GetEffectiveAabb strips root scale,
            // so we need the offset in unscaled space first, then apply scale to both.
            var aabb = GetEffectiveAabb(model);
            float cx = -(aabb.Position.X + aabb.Size.X / 2f);
            float cy = -aabb.Position.Y; // bottom of AABB at origin (grip point)
            float cz = -(aabb.Position.Z + aabb.Size.Z / 2f);

            ScaleModelToFit(model, 0.3f);

            // Position is in parent space; AABB offset is in unscaled model space.
            // Multiply by model scale so the offset matches the scaled mesh.
            float s = model.Scale.X; // uniform scale from ScaleModelToFit
            model.Position = new Vector3(cx * s, cy * s, cz * s);

            root.AddChild(model);
            return root;
        }

        // ── Weapon texture atlas paths (texture-sheet FBX weapons) ──
        private static readonly string[] _weaponTexturePaths = new[]
        {
            "res://Models/Weapons/Textures/T_Guns_Batch1_BaseColor.png",
            "res://Models/Weapons/Textures/T_Guns_Batch2_BaseColor.png",
        };
        private static readonly string[] _weaponNormalPaths = new[]
        {
            "res://Models/Weapons/Textures/T_Guns_Batch1_Normal.png",
            "res://Models/Weapons/Textures/T_Guns_Batch2_Normal.png",
        };
        private static readonly string[] _weaponOrmPaths = new[]
        {
            "res://Models/Weapons/Textures/T_Guns_Batch1_ORM.png",
            "res://Models/Weapons/Textures/T_Guns_Batch2_ORM.png",
        };
        private static readonly string[] _weaponEmissivePaths = new[]
        {
            "res://Models/Weapons/Textures/T_Guns_Batch1_Emissive.png",
            "res://Models/Weapons/Textures/T_Guns_Batch2_Emissive.png",
        };

        /// <summary>
        /// Apply PBR weapon textures to FBX weapon meshes that import with no materials.
        /// Checks if any mesh has a default/white material and applies the texture atlas.
        /// </summary>
        private static void ApplyWeaponTextures(Node3D model)
        {
            bool needsTextures = false;
            CheckNeedsTextures(model, ref needsTextures);
            if (!needsTextures) return;

            // Try to load the first available texture batch
            StandardMaterial3D weaponMat = null;
            for (int i = 0; i < _weaponTexturePaths.Length; i++)
            {
                var baseTex = GD.Load<Texture2D>(_weaponTexturePaths[i]);
                if (baseTex == null) continue;

                weaponMat = new StandardMaterial3D();
                weaponMat.AlbedoTexture = baseTex;
                weaponMat.Metallic = 0.5f;
                weaponMat.Roughness = 0.4f;

                // Normal map
                var normalTex = GD.Load<Texture2D>(_weaponNormalPaths[i]);
                if (normalTex != null)
                {
                    weaponMat.NormalEnabled = true;
                    weaponMat.NormalTexture = normalTex;
                }

                // Emissive
                var emissiveTex = GD.Load<Texture2D>(_weaponEmissivePaths[i]);
                if (emissiveTex != null)
                {
                    weaponMat.EmissionEnabled = true;
                    weaponMat.EmissionTexture = emissiveTex;
                    weaponMat.EmissionEnergyMultiplier = 0.5f;
                    weaponMat.Emission = Colors.White;
                }

                break; // Use first available batch
            }

            if (weaponMat == null)
            {
                // No texture atlas found — apply a sensible default dark metal
                weaponMat = new StandardMaterial3D();
                weaponMat.AlbedoColor = new Color(0.3f, 0.3f, 0.35f);
                weaponMat.Metallic = 0.7f;
                weaponMat.Roughness = 0.3f;
            }

            ApplyMaterialToMeshes(model, weaponMat);
        }

        private static void CheckNeedsTextures(Node node, ref bool needs)
        {
            if (needs) return;
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                // Check if mesh has no material or a default white material
                var mat = mi.MaterialOverride ?? mi.GetActiveMaterial(0);
                if (mat == null)
                {
                    needs = true;
                    return;
                }
                if (mat is StandardMaterial3D stdMat && stdMat.AlbedoTexture == null)
                {
                    // White default material — albedo color close to white with no texture
                    var c = stdMat.AlbedoColor;
                    if (c.R > 0.8f && c.G > 0.8f && c.B > 0.8f)
                        needs = true;
                }
            }
            foreach (var child in node.GetChildren())
                if (child is Node n) CheckNeedsTextures(n, ref needs);
        }

        private static void ApplyMaterialToMeshes(Node node, StandardMaterial3D mat)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                mi.MaterialOverride = mat;
            }
            foreach (var child in node.GetChildren())
                if (child is Node n) ApplyMaterialToMeshes(n, mat);
        }

        // ── Gun Weapon Models (procedural fallback) ──

        private static Node3D BuildPistolModel()
        {
            var root = new Node3D();
            root.Name = "PistolItem";
            Color frame = new Color(0.35f, 0.35f, 0.38f);
            Color darkMetal = new Color(0.22f, 0.22f, 0.25f);
            Color accent = new Color(0.6f, 0.45f, 0.2f); // brass
            Color muzzleGlow = new Color(0.4f, 0.8f, 1f);

            // Barrel — short cylinder pointing forward (-Z)
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.028f, Height = 0.18f, RadialSegments = 8 },
                darkMetal, new Vector3(0, 0.04f, -0.09f));
            barrel.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrel);

            // Receiver body
            var body = CreateMeshNode("Receiver",
                new BoxMesh { Size = new Vector3(0.06f, 0.08f, 0.12f) },
                frame, new Vector3(0, 0.04f, 0.02f));
            root.AddChild(body);

            // Trigger guard — thin box arc
            var triggerGuard = CreateMeshNode("TriggerGuard",
                new BoxMesh { Size = new Vector3(0.04f, 0.01f, 0.06f) },
                darkMetal, new Vector3(0, -0.02f, 0.01f));
            root.AddChild(triggerGuard);

            // Grip — angled box
            var grip = CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.04f, 0.1f, 0.04f) },
                accent, new Vector3(0, -0.06f, 0.04f));
            root.AddChild(grip);

            // Brass bolt on the side
            var bolt = CreateMeshNode("Bolt",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.01f, Height = 0.025f, RadialSegments = 6 },
                accent, new Vector3(0.035f, 0.06f, -0.01f));
            bolt.RotateZ(Mathf.DegToRad(90));
            root.AddChild(bolt);

            // Muzzle glow
            var muzzle = CreateEmissiveMeshNode("Muzzle",
                new SphereMesh { Radius = 0.018f, Height = 0.036f, RadialSegments = 6, Rings = 3 },
                muzzleGlow, muzzleGlow, new Vector3(0, 0.04f, -0.19f));
            root.AddChild(muzzle);

            return root;
        }

        private static Node3D BuildRifleModel()
        {
            var root = new Node3D();
            root.Name = "RifleItem";
            Color gunMetal = new Color(0.3f, 0.3f, 0.33f);
            Color darkSteel = new Color(0.2f, 0.2f, 0.22f);
            Color copper = new Color(0.65f, 0.4f, 0.18f);
            Color scopeGlow = new Color(0.9f, 0.2f, 0.1f);

            // Long barrel
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.025f, Height = 0.4f, RadialSegments = 8 },
                darkSteel, new Vector3(0, 0.05f, -0.2f));
            barrel.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrel);

            // Barrel shroud — slotted heat vents
            var shroud = CreateMeshNode("Shroud",
                new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.15f) },
                gunMetal, new Vector3(0, 0.05f, -0.22f));
            root.AddChild(shroud);
            for (int i = 0; i < 3; i++)
            {
                var vent = CreateMeshNode($"Vent{i}",
                    new BoxMesh { Size = new Vector3(0.055f, 0.008f, 0.01f) },
                    darkSteel.Darkened(0.2f), new Vector3(0, 0.05f, -0.17f - i * 0.04f));
                root.AddChild(vent);
            }

            // Receiver
            var receiver = CreateMeshNode("Receiver",
                new BoxMesh { Size = new Vector3(0.065f, 0.07f, 0.14f) },
                gunMetal, new Vector3(0, 0.05f, 0.02f));
            root.AddChild(receiver);

            // Stock — extends back
            var stock = CreateMeshNode("Stock",
                new BoxMesh { Size = new Vector3(0.04f, 0.06f, 0.14f) },
                copper, new Vector3(0, 0.04f, 0.14f));
            root.AddChild(stock);

            // Stock butt plate
            var buttPlate = CreateMeshNode("ButtPlate",
                new BoxMesh { Size = new Vector3(0.05f, 0.08f, 0.015f) },
                darkSteel, new Vector3(0, 0.04f, 0.22f));
            root.AddChild(buttPlate);

            // Scope mount
            var scopeMount = CreateMeshNode("ScopeMount",
                new BoxMesh { Size = new Vector3(0.02f, 0.015f, 0.06f) },
                gunMetal, new Vector3(0, 0.095f, -0.02f));
            root.AddChild(scopeMount);

            // Scope tube
            var scope = CreateMeshNode("Scope",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.08f, RadialSegments = 8 },
                darkSteel, new Vector3(0, 0.12f, -0.02f));
            scope.RotateX(Mathf.DegToRad(90));
            root.AddChild(scope);

            // Scope lens (emissive red)
            var lens = CreateEmissiveMeshNode("ScopeLens",
                new SphereMesh { Radius = 0.018f, Height = 0.01f, RadialSegments = 6, Rings = 3 },
                scopeGlow, scopeGlow, new Vector3(0, 0.12f, -0.065f));
            root.AddChild(lens);

            // Grip
            var grip = CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.035f, 0.08f, 0.035f) },
                copper, new Vector3(0, -0.03f, 0.03f));
            root.AddChild(grip);

            // Muzzle brake — wider cylinder at barrel tip
            var muzzleBrake = CreateMeshNode("MuzzleBrake",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.03f, RadialSegments = 8 },
                gunMetal.Lightened(0.1f), new Vector3(0, 0.05f, -0.41f));
            muzzleBrake.RotateX(Mathf.DegToRad(90));
            root.AddChild(muzzleBrake);

            return root;
        }

        private static Node3D BuildShotgunModel()
        {
            var root = new Node3D();
            root.Name = "ShotgunItem";
            Color iron = new Color(0.32f, 0.32f, 0.35f);
            Color darkIron = new Color(0.2f, 0.2f, 0.23f);
            Color wood = new Color(0.45f, 0.3f, 0.15f);
            Color brass = new Color(0.7f, 0.55f, 0.2f);
            Color muzzleGlow = new Color(1f, 0.6f, 0.2f);

            // Double barrel — two cylinders side by side
            for (float side = -1; side <= 1; side += 2)
            {
                var brl = CreateMeshNode(side < 0 ? "BarrelL" : "BarrelR",
                    new CylinderMesh { TopRadius = 0.028f, BottomRadius = 0.03f, Height = 0.3f, RadialSegments = 8 },
                    darkIron, new Vector3(side * 0.025f, 0.05f, -0.15f));
                brl.RotateX(Mathf.DegToRad(90));
                root.AddChild(brl);
            }

            // Barrel bridge — connects the two barrels
            var bridge = CreateMeshNode("Bridge",
                new BoxMesh { Size = new Vector3(0.07f, 0.02f, 0.05f) },
                iron, new Vector3(0, 0.05f, -0.08f));
            root.AddChild(bridge);

            // Receiver — chunky box
            var receiver = CreateMeshNode("Receiver",
                new BoxMesh { Size = new Vector3(0.08f, 0.09f, 0.1f) },
                iron, new Vector3(0, 0.045f, 0.03f));
            root.AddChild(receiver);

            // Pump slide underneath
            var pump = CreateMeshNode("Pump",
                new BoxMesh { Size = new Vector3(0.05f, 0.04f, 0.1f) },
                wood, new Vector3(0, -0.01f, -0.08f));
            root.AddChild(pump);

            // Pump rail
            var rail = CreateMeshNode("PumpRail",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.2f, RadialSegments = 6 },
                darkIron, new Vector3(0, 0.01f, -0.08f));
            rail.RotateX(Mathf.DegToRad(90));
            root.AddChild(rail);

            // Stock
            var stock = CreateMeshNode("Stock",
                new BoxMesh { Size = new Vector3(0.05f, 0.07f, 0.12f) },
                wood, new Vector3(0, 0.04f, 0.12f));
            root.AddChild(stock);

            // Grip
            var grip = CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.035f, 0.08f, 0.035f) },
                wood.Darkened(0.15f), new Vector3(0, -0.03f, 0.04f));
            root.AddChild(grip);

            // Brass ejection port
            var eject = CreateMeshNode("EjectPort",
                new BoxMesh { Size = new Vector3(0.015f, 0.025f, 0.04f) },
                brass, new Vector3(0.045f, 0.07f, 0.01f));
            root.AddChild(eject);

            // Muzzle glow (wide spread implied)
            for (float side = -1; side <= 1; side += 2)
            {
                var glow = CreateEmissiveMeshNode(side < 0 ? "MuzzleL" : "MuzzleR",
                    new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                    muzzleGlow, muzzleGlow, new Vector3(side * 0.025f, 0.05f, -0.31f));
                root.AddChild(glow);
            }

            return root;
        }

        private static Node3D BuildLauncherModel()
        {
            var root = new Node3D();
            root.Name = "LauncherItem";
            Color hull = new Color(0.35f, 0.38f, 0.4f);
            Color darkPlate = new Color(0.22f, 0.24f, 0.26f);
            Color hazardOrange = new Color(0.9f, 0.5f, 0.1f);
            Color ventGlow = new Color(0.3f, 0.9f, 0.4f);

            // Main tube — big bore
            var tube = CreateMeshNode("Tube",
                new CylinderMesh { TopRadius = 0.055f, BottomRadius = 0.06f, Height = 0.35f, RadialSegments = 10 },
                hull, new Vector3(0, 0.06f, -0.1f));
            tube.RotateX(Mathf.DegToRad(90));
            root.AddChild(tube);

            // Bore ring at muzzle
            var boreRing = CreateMeshNode("BoreRing",
                new TorusMesh { InnerRadius = 0.045f, OuterRadius = 0.06f, Rings = 10, RingSegments = 6 },
                darkPlate, new Vector3(0, 0.06f, -0.28f));
            root.AddChild(boreRing);

            // Hazard stripes — 2 orange bands
            for (int i = 0; i < 2; i++)
            {
                var stripe = CreateEmissiveMeshNode($"HazardStripe{i}",
                    new TorusMesh { InnerRadius = 0.055f, OuterRadius = 0.065f, Rings = 10, RingSegments = 6 },
                    hazardOrange, hazardOrange * 0.6f, new Vector3(0, 0.06f, -0.2f + i * 0.12f));
                root.AddChild(stripe);
            }

            // Shoulder brace behind
            var brace = CreateMeshNode("Brace",
                new BoxMesh { Size = new Vector3(0.08f, 0.1f, 0.08f) },
                darkPlate, new Vector3(0, 0.06f, 0.12f));
            root.AddChild(brace);

            // Handle on top
            var handle = CreateMeshNode("Handle",
                new BoxMesh { Size = new Vector3(0.04f, 0.025f, 0.1f) },
                hull.Lightened(0.1f), new Vector3(0, 0.11f, -0.02f));
            root.AddChild(handle);
            // Handle uprights
            for (float side = -1; side <= 1; side += 2)
            {
                var upright = CreateMeshNode(side < 0 ? "HandleL" : "HandleR",
                    new BoxMesh { Size = new Vector3(0.01f, 0.03f, 0.01f) },
                    hull.Lightened(0.1f), new Vector3(0, 0.095f, -0.02f + side * 0.045f));
                root.AddChild(upright);
            }

            // Grip underneath
            var grip = CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.04f, 0.09f, 0.04f) },
                darkPlate, new Vector3(0, -0.03f, 0.02f));
            root.AddChild(grip);

            // Exhaust vents on back (emissive green)
            for (int i = -1; i <= 1; i++)
            {
                var vent = CreateEmissiveMeshNode($"Vent{i}",
                    new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.018f, Height = 0.025f, RadialSegments = 6 },
                    ventGlow, ventGlow, new Vector3(i * 0.03f, 0.06f, 0.17f));
                vent.RotateX(Mathf.DegToRad(90));
                root.AddChild(vent);
            }

            return root;
        }

        private static Node3D BuildRepeaterModel()
        {
            var root = new Node3D();
            root.Name = "RepeaterItem";
            Color gunMetal = new Color(0.3f, 0.32f, 0.35f);
            Color darkMetal = new Color(0.2f, 0.2f, 0.23f);
            Color copper = new Color(0.65f, 0.42f, 0.18f);
            Color barrelGlow = new Color(0.4f, 0.7f, 1f);

            // Tri-barrel cluster — 3 barrels in triangle pattern
            float[] bx = { 0f, -0.025f, 0.025f };
            float[] by = { 0.08f, 0.04f, 0.04f };
            for (int i = 0; i < 3; i++)
            {
                var brl = CreateMeshNode($"Barrel{i}",
                    new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.02f, Height = 0.28f, RadialSegments = 6 },
                    darkMetal, new Vector3(bx[i], by[i], -0.14f));
                brl.RotateX(Mathf.DegToRad(90));
                root.AddChild(brl);
            }

            // Barrel housing — cylinder enclosing the cluster
            var housing = CreateMeshNode("Housing",
                new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.055f, Height = 0.1f, RadialSegments = 10 },
                gunMetal, new Vector3(0, 0.055f, -0.05f));
            housing.RotateX(Mathf.DegToRad(90));
            root.AddChild(housing);

            // Barrel spin ring (copper band at muzzle end)
            var spinRing = CreateMeshNode("SpinRing",
                new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.052f, Rings = 10, RingSegments = 6 },
                copper, new Vector3(0, 0.055f, -0.22f));
            root.AddChild(spinRing);

            // Receiver body
            var receiver = CreateMeshNode("Receiver",
                new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.12f) },
                gunMetal, new Vector3(0, 0.055f, 0.04f));
            root.AddChild(receiver);

            // Ammo drum — cylinder underneath/behind
            var drum = CreateMeshNode("AmmoDrum",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.08f, RadialSegments = 8 },
                darkMetal, new Vector3(0, 0.02f, 0.06f));
            root.AddChild(drum);

            // Drum band
            var drumBand = CreateEmissiveMeshNode("DrumBand",
                new TorusMesh { InnerRadius = 0.035f, OuterRadius = 0.045f, Rings = 8, RingSegments = 6 },
                copper, copper * 0.5f, new Vector3(0, 0.02f, 0.06f));
            drumBand.RotateX(Mathf.DegToRad(90));
            root.AddChild(drumBand);

            // Grip
            var grip = CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.035f, 0.08f, 0.035f) },
                copper, new Vector3(0, -0.02f, 0.04f));
            root.AddChild(grip);

            // Rear handle / stock stub
            var stockStub = CreateMeshNode("StockStub",
                new BoxMesh { Size = new Vector3(0.04f, 0.05f, 0.06f) },
                gunMetal.Lightened(0.05f), new Vector3(0, 0.05f, 0.13f));
            root.AddChild(stockStub);

            // Muzzle glow — 3 emissive tips
            for (int i = 0; i < 3; i++)
            {
                var glow = CreateEmissiveMeshNode($"MuzzleGlow{i}",
                    new SphereMesh { Radius = 0.012f, Height = 0.024f, RadialSegments = 6, Rings = 3 },
                    barrelGlow, barrelGlow, new Vector3(bx[i], by[i], -0.29f));
                root.AddChild(glow);
            }

            return root;
        }

        private static Node3D BuildShieldModel()
        {
            var root = new Node3D();
            root.Name = "ShieldItem";
            Color metal = new Color(0.45f, 0.45f, 0.5f);
            Color rim = new Color(0.55f, 0.5f, 0.35f);
            Color boss = new Color(0.6f, 0.58f, 0.4f);
            Color strap = new Color(0.3f, 0.2f, 0.1f);

            // Flat disc body
            var disc = CreateMeshNode("Disc",
                new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.18f, Height = 0.02f, RadialSegments = 12 },
                metal, Vector3.Zero);
            disc.RotateX(Mathf.DegToRad(90));
            root.AddChild(disc);

            // Center boss dome
            var bossNode = CreateMeshNode("Boss",
                new SphereMesh { Radius = 0.05f, Height = 0.06f, RadialSegments = 8, Rings = 4 },
                boss, new Vector3(0, 0, -0.02f));
            root.AddChild(bossNode);

            // Rim torus
            var rimNode = CreateMeshNode("Rim",
                new TorusMesh { InnerRadius = 0.16f, OuterRadius = 0.19f, Rings = 16, RingSegments = 6 },
                rim, Vector3.Zero);
            rimNode.RotateX(Mathf.DegToRad(90));
            root.AddChild(rimNode);

            // Bolt heads (4 around the rim)
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.Pi / 2f;
                var bolt = CreateMeshNode($"Bolt{i}",
                    new SphereMesh { Radius = 0.015f, Height = 0.03f, RadialSegments = 4, Rings = 2 },
                    boss, new Vector3(Mathf.Cos(angle) * 0.14f, Mathf.Sin(angle) * 0.14f, -0.02f));
                root.AddChild(bolt);
            }

            // Arm strap on back
            var strapNode = CreateMeshNode("Strap",
                new BoxMesh { Size = new Vector3(0.08f, 0.03f, 0.02f) },
                strap, new Vector3(0, 0, 0.02f));
            root.AddChild(strapNode);

            return root;
        }

        private static Node3D BuildRobeModel()
        {
            var root = new Node3D();
            root.Name = "RobeItem";
            Color fabric = new Color(0.25f, 0.2f, 0.35f);
            Color wire = new Color(0.5f, 0.4f, 0.25f);
            Color circuit = new Color(0.3f, 0.6f, 0.9f);

            // Tapered cylinder body
            var body = CreateMeshNode("Body",
                new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 0.3f, RadialSegments = 8 },
                fabric, Vector3.Zero);
            root.AddChild(body);

            // 3 wire strand accents
            for (int i = -1; i <= 1; i++)
            {
                var strand = CreateMeshNode($"Wire{i}",
                    new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.005f, Height = 0.25f, RadialSegments = 4 },
                    wire, new Vector3(i * 0.06f, 0, -0.13f));
                root.AddChild(strand);
            }

            // Collar torus
            var collar = CreateMeshNode("Collar",
                new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.13f, Rings = 10, RingSegments = 6 },
                fabric.Lightened(0.15f), new Vector3(0, 0.15f, 0));
            root.AddChild(collar);

            // Emissive circuit trace on front
            var trace = CreateEmissiveMeshNode("CircuitTrace",
                new BoxMesh { Size = new Vector3(0.08f, 0.2f, 0.005f) },
                circuit, circuit, new Vector3(0, -0.02f, -0.14f));
            root.AddChild(trace);

            return root;
        }

        private static Node3D BuildGreavesModel()
        {
            var root = new Node3D();
            root.Name = "GreavesItem";
            Color plate = new Color(0.48f, 0.48f, 0.52f);
            Color knee = new Color(0.55f, 0.55f, 0.58f);
            Color strap = new Color(0.3f, 0.22f, 0.12f);

            // Twin shin plates
            for (float side = -1; side <= 1; side += 2)
            {
                var shin = CreateMeshNode(side < 0 ? "LeftShin" : "RightShin",
                    new BoxMesh { Size = new Vector3(0.08f, 0.22f, 0.06f) },
                    plate, new Vector3(side * 0.06f, 0, 0));
                root.AddChild(shin);

                // Knee cap sphere
                var kneeCap = CreateMeshNode(side < 0 ? "LeftKnee" : "RightKnee",
                    new SphereMesh { Radius = 0.04f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                    knee, new Vector3(side * 0.06f, 0.13f, -0.03f));
                root.AddChild(kneeCap);
            }

            // Connecting strap
            var strapNode = CreateMeshNode("Strap",
                new BoxMesh { Size = new Vector3(0.16f, 0.03f, 0.02f) },
                strap, new Vector3(0, 0.05f, 0.03f));
            root.AddChild(strapNode);

            return root;
        }

        private static Node3D BuildBootsModel()
        {
            var root = new Node3D();
            root.Name = "BootsItem";
            Color bootColor = new Color(0.3f, 0.28f, 0.25f);
            Color tread = new Color(0.18f, 0.16f, 0.14f);
            Color grip = new Color(0.22f, 0.2f, 0.18f);

            // Chunky boot boxes
            for (float side = -1; side <= 1; side += 2)
            {
                var boot = CreateMeshNode(side < 0 ? "LeftBoot" : "RightBoot",
                    new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.14f) },
                    bootColor, new Vector3(side * 0.06f, 0, 0));
                root.AddChild(boot);

                // Tread plate underneath
                var treadPlate = CreateMeshNode(side < 0 ? "LeftTread" : "RightTread",
                    new BoxMesh { Size = new Vector3(0.09f, 0.02f, 0.15f) },
                    tread, new Vector3(side * 0.06f, -0.05f, 0));
                root.AddChild(treadPlate);

                // Grip ridges (3 per boot)
                for (int r = -1; r <= 1; r++)
                {
                    var ridge = CreateMeshNode($"{(side < 0 ? "L" : "R")}Ridge{r}",
                        new BoxMesh { Size = new Vector3(0.07f, 0.01f, 0.015f) },
                        grip, new Vector3(side * 0.06f, -0.06f, r * 0.04f));
                    root.AddChild(ridge);
                }
            }

            return root;
        }

        private static Node3D BuildGauntletsModel()
        {
            var root = new Node3D();
            root.Name = "GauntletsItem";
            Color armor = new Color(0.45f, 0.45f, 0.5f);
            Color knuckle = new Color(0.55f, 0.52f, 0.48f);
            Color clamp = new Color(0.35f, 0.33f, 0.3f);

            // Armored hand boxes
            for (float side = -1; side <= 1; side += 2)
            {
                var hand = CreateMeshNode(side < 0 ? "LeftHand" : "RightHand",
                    new BoxMesh { Size = new Vector3(0.07f, 0.06f, 0.1f) },
                    armor, new Vector3(side * 0.06f, 0, 0));
                root.AddChild(hand);

                // Knuckle guard
                var guard = CreateMeshNode(side < 0 ? "LeftKnuckle" : "RightKnuckle",
                    new BoxMesh { Size = new Vector3(0.08f, 0.02f, 0.04f) },
                    knuckle, new Vector3(side * 0.06f, 0.03f, -0.04f));
                root.AddChild(guard);

                // Finger clamps (2 per hand)
                for (int f = 0; f < 2; f++)
                {
                    var finger = CreateMeshNode($"{(side < 0 ? "L" : "R")}Clamp{f}",
                        new BoxMesh { Size = new Vector3(0.015f, 0.04f, 0.02f) },
                        clamp, new Vector3(side * 0.06f + (f - 0.5f) * 0.03f, -0.02f, -0.06f));
                    root.AddChild(finger);
                }
            }

            return root;
        }

        private static Node3D BuildCloakModel()
        {
            var root = new Node3D();
            root.Name = "CloakItem";
            Color brass = new Color(0.65f, 0.55f, 0.25f);
            Color cape = new Color(0.2f, 0.18f, 0.22f);
            Color wireAccent = new Color(0.5f, 0.4f, 0.2f);

            // Brass clasp disc at top
            var clasp = CreateMeshNode("Clasp",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.015f, RadialSegments = 8 },
                brass, new Vector3(0, 0.14f, 0));
            root.AddChild(clasp);

            // Flat cape body
            var capeBody = CreateMeshNode("Cape",
                new BoxMesh { Size = new Vector3(0.22f, 0.28f, 0.015f) },
                cape, Vector3.Zero);
            root.AddChild(capeBody);

            // Tattered edge strips at bottom
            for (int i = -2; i <= 2; i++)
            {
                var strip = CreateMeshNode($"Tatter{i}",
                    new BoxMesh { Size = new Vector3(0.03f, 0.05f, 0.01f) },
                    cape.Lightened(0.08f), new Vector3(i * 0.04f, -0.17f, 0));
                root.AddChild(strip);
            }

            // Wire accent line
            var wire = CreateMeshNode("WireAccent",
                new BoxMesh { Size = new Vector3(0.18f, 0.01f, 0.005f) },
                wireAccent, new Vector3(0, 0.06f, -0.01f));
            root.AddChild(wire);

            return root;
        }

        // ── New Consumable Models ──

        private static Node3D BuildRepairKitModel()
        {
            var root = new Node3D();
            root.Name = "RepairKitItem";
            Color metalCase = new Color(0.4f, 0.42f, 0.45f);
            Color red = new Color(0.85f, 0.15f, 0.1f);
            Color handle = new Color(0.3f, 0.28f, 0.25f);
            Color latch = new Color(0.6f, 0.58f, 0.4f);

            // Metal case
            var caseBox = CreateMeshNode("Case",
                new BoxMesh { Size = new Vector3(0.18f, 0.1f, 0.12f) },
                metalCase, Vector3.Zero);
            root.AddChild(caseBox);

            // Lid
            var lid = CreateMeshNode("Lid",
                new BoxMesh { Size = new Vector3(0.19f, 0.02f, 0.13f) },
                metalCase.Lightened(0.1f), new Vector3(0, 0.06f, 0));
            root.AddChild(lid);

            // Red cross emblem (horizontal + vertical)
            var crossH = CreateEmissiveMeshNode("CrossH",
                new BoxMesh { Size = new Vector3(0.08f, 0.025f, 0.005f) },
                red, red, new Vector3(0, 0.075f, -0.068f));
            root.AddChild(crossH);
            var crossV = CreateEmissiveMeshNode("CrossV",
                new BoxMesh { Size = new Vector3(0.025f, 0.08f, 0.005f) },
                red, red, new Vector3(0, 0.075f, -0.068f));
            root.AddChild(crossV);

            // Handle
            var handleNode = CreateMeshNode("Handle",
                new BoxMesh { Size = new Vector3(0.1f, 0.02f, 0.02f) },
                handle, new Vector3(0, 0.08f, 0));
            root.AddChild(handleNode);

            // Latch
            var latchNode = CreateMeshNode("Latch",
                new BoxMesh { Size = new Vector3(0.03f, 0.03f, 0.015f) },
                latch, new Vector3(0, 0, -0.068f));
            root.AddChild(latchNode);

            return root;
        }

        private static Node3D BuildBatteryPackModel()
        {
            var root = new Node3D();
            root.Name = "BatteryPackItem";
            Color cell = new Color(0.25f, 0.28f, 0.35f);
            Color terminal = new Color(0.6f, 0.58f, 0.4f);
            Color energy = new Color(0.2f, 0.5f, 1f);

            // Cylindrical cell body
            var body = CreateMeshNode("Cell",
                new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.2f, RadialSegments = 10 },
                cell, Vector3.Zero);
            root.AddChild(body);

            // Top terminal
            var topTerminal = CreateMeshNode("TopTerminal",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.03f, RadialSegments = 6 },
                terminal, new Vector3(0, 0.115f, 0));
            root.AddChild(topTerminal);

            // Bottom terminal
            var bottomTerminal = CreateMeshNode("BottomTerminal",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.025f, Height = 0.03f, RadialSegments = 6 },
                terminal, new Vector3(0, -0.115f, 0));
            root.AddChild(bottomTerminal);

            // Emissive blue energy bands (3 rings)
            for (int i = -1; i <= 1; i++)
            {
                var band = CreateEmissiveMeshNode($"Band{i}",
                    new TorusMesh { InnerRadius = 0.055f, OuterRadius = 0.065f, Rings = 10, RingSegments = 6 },
                    energy, energy, new Vector3(0, i * 0.06f, 0));
                root.AddChild(band);
            }

            return root;
        }

        private static Node3D BuildPlatingBoosterModel()
        {
            var root = new Node3D();
            root.Name = "PlatingBoosterItem";
            Color casing = new Color(0.4f, 0.38f, 0.35f);
            Color plunger = new Color(0.3f, 0.3f, 0.32f);
            Color nozzle = new Color(0.5f, 0.5f, 0.52f);
            Color gold = new Color(0.85f, 0.7f, 0.2f);

            // Boxy injector body
            var body = CreateMeshNode("Body",
                new BoxMesh { Size = new Vector3(0.1f, 0.06f, 0.16f) },
                casing, Vector3.Zero);
            root.AddChild(body);

            // Plunger on back
            var plungerNode = CreateMeshNode("Plunger",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.06f, RadialSegments = 6 },
                plunger, new Vector3(0, 0, 0.11f));
            plungerNode.RotateX(Mathf.DegToRad(90));
            root.AddChild(plungerNode);

            // Nozzle on front
            var nozzleNode = CreateMeshNode("Nozzle",
                new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.025f, Height = 0.05f, RadialSegments = 6 },
                nozzle, new Vector3(0, 0, -0.105f));
            nozzleNode.RotateX(Mathf.DegToRad(90));
            root.AddChild(nozzleNode);

            // Emissive gold shield icon on top
            var icon = CreateEmissiveMeshNode("ShieldIcon",
                new SphereMesh { Radius = 0.025f, Height = 0.035f, RadialSegments = 6, Rings = 3 },
                gold, gold, new Vector3(0, 0.04f, 0));
            root.AddChild(icon);

            return root;
        }

        private static Node3D BuildOverclockInjectorModel()
        {
            var root = new Node3D();
            root.Name = "OverclockInjectorItem";
            Color barrel = new Color(0.5f, 0.5f, 0.52f);
            Color fluid = new Color(1f, 0.55f, 0.1f);
            Color plunger = new Color(0.35f, 0.35f, 0.38f);
            Color hazard = new Color(1f, 0.8f, 0f);

            // Syringe barrel
            var barrelNode = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.2f, RadialSegments = 8 },
                barrel, Vector3.Zero);
            barrelNode.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrelNode);

            // Orange fluid inside (slightly smaller)
            var fluidNode = CreateEmissiveMeshNode("Fluid",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.12f, RadialSegments = 8 },
                fluid, fluid, new Vector3(0, 0, -0.02f));
            fluidNode.RotateX(Mathf.DegToRad(90));
            root.AddChild(fluidNode);

            // Plunger on back
            var plungerNode = CreateMeshNode("Plunger",
                new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.02f, Height = 0.08f, RadialSegments = 6 },
                plunger, new Vector3(0, 0, 0.14f));
            plungerNode.RotateX(Mathf.DegToRad(90));
            root.AddChild(plungerNode);

            // Needle tip
            var needle = CreateMeshNode("Needle",
                new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.01f, Height = 0.06f, RadialSegments = 4 },
                barrel.Lightened(0.2f), new Vector3(0, 0, -0.13f));
            needle.RotateX(Mathf.DegToRad(90));
            root.AddChild(needle);

            // Hazard ring
            var hazardRing = CreateEmissiveMeshNode("HazardRing",
                new TorusMesh { InnerRadius = 0.028f, OuterRadius = 0.038f, Rings = 8, RingSegments = 6 },
                hazard, hazard, new Vector3(0, 0, -0.08f));
            hazardRing.RotateX(Mathf.DegToRad(90));
            root.AddChild(hazardRing);

            return root;
        }

        // ── Loot Box Model ──

        public static Node3D BuildLootBoxModel(LootBoxTier tier)
        {
            var model = TryLoadLootBoxModel(tier);
            if (model != null) return model;

            // Procedural fallback
            var box = tier switch
            {
                LootBoxTier.Junk => BuildBronzeLootBox(),
                LootBoxTier.Bronze => BuildBronzeLootBox(),
                LootBoxTier.Silver => BuildSilverLootBox(),
                LootBoxTier.Gold => BuildGoldLootBox(),
                LootBoxTier.Diamond => BuildDiamondLootBox(),
                LootBoxTier.Legendary => BuildLegendaryLootBox(),
                LootBoxTier.Celestial => BuildLegendaryLootBox(),
                _ => BuildBronzeLootBox()
            };

            // Raise the model so the box bottom clears the floor
            box.Position = new Vector3(0, 0.4f, 0);
            return box;
        }

        private static Node3D TryLoadLootBoxModel(LootBoxTier tier)
        {
            var model = ModelLibrary.TryLoad("item", "lootbox");
            if (model == null) return null;

            model.Name = $"LootBox_{tier}";
            model.Scale = Vector3.One * 0.15f;
            // Raise the model so it floats above the floor
            model.Position = new Vector3(0, 0.5f, 0);
            ApplyLootBoxTierMaterial(model, tier);
            return model;
        }

        private static void ApplyLootBoxTierMaterial(Node node, LootBoxTier tier)
        {
            // Apply alternating materials to mesh children for visual contrast
            // Even-indexed meshes get the body material, odd get the accent material
            int meshIndex = 0;
            ApplyLootBoxTierMaterialRecursive(node, tier, ref meshIndex);
        }

        private static void ApplyLootBoxTierMaterialRecursive(Node node, LootBoxTier tier, ref int meshIndex)
        {
            if (node is MeshInstance3D mesh)
            {
                var (body, accent) = CreateLootBoxMaterials(tier);
                // Alternate body/accent for visual variety on the FBX mesh parts
                mesh.MaterialOverride = (meshIndex % 2 == 0) ? body : accent;
                meshIndex++;
            }

            foreach (var child in node.GetChildren())
                ApplyLootBoxTierMaterialRecursive(child, tier, ref meshIndex);
        }

        /// <summary>
        /// Returns (bodyMaterial, accentMaterial) pair for high-contrast loot box visuals.
        /// Body = darker base, Accent = bright metallic trim/bands.
        /// </summary>
        private static (StandardMaterial3D body, StandardMaterial3D accent) CreateLootBoxMaterials(LootBoxTier tier)
        {
            var body = new StandardMaterial3D();
            var accent = new StandardMaterial3D();

            switch (tier)
            {
                case LootBoxTier.Junk:
                    // Dull grey metal
                    body.AlbedoColor = new Color(0.4f, 0.4f, 0.4f);
                    body.Metallic = 0.6f;
                    body.Roughness = 0.5f;
                    accent.AlbedoColor = new Color(0.55f, 0.55f, 0.5f);
                    accent.Metallic = 0.7f;
                    accent.Roughness = 0.4f;
                    break;
                case LootBoxTier.Bronze:
                    // Polished bronze metal — warm copper-bronze, not brown
                    body.AlbedoColor = new Color(0.72f, 0.45f, 0.2f);
                    body.Metallic = 0.85f;
                    body.Roughness = 0.25f;
                    accent.AlbedoColor = new Color(0.9f, 0.65f, 0.3f);
                    accent.Metallic = 0.95f;
                    accent.Roughness = 0.15f;
                    accent.Emission = new Color(0.8f, 0.5f, 0.15f);
                    accent.EmissionEnabled = true;
                    accent.EmissionEnergyMultiplier = 0.4f;
                    break;
                case LootBoxTier.Silver:
                    // Polished silver metal
                    body.AlbedoColor = new Color(0.7f, 0.72f, 0.78f);
                    body.Metallic = 0.9f;
                    body.Roughness = 0.2f;
                    accent.AlbedoColor = new Color(0.88f, 0.9f, 0.95f);
                    accent.Metallic = 0.95f;
                    accent.Roughness = 0.1f;
                    accent.Emission = new Color(0.7f, 0.75f, 0.85f);
                    accent.EmissionEnabled = true;
                    accent.EmissionEnergyMultiplier = 0.5f;
                    break;
                case LootBoxTier.Gold:
                    // Gleaming gold
                    body.AlbedoColor = new Color(0.85f, 0.7f, 0.1f);
                    body.Metallic = 0.95f;
                    body.Roughness = 0.15f;
                    accent.AlbedoColor = new Color(1.0f, 0.85f, 0.15f);
                    accent.Metallic = 1.0f;
                    accent.Roughness = 0.08f;
                    accent.Emission = new Color(1.0f, 0.84f, 0.0f);
                    accent.EmissionEnabled = true;
                    accent.EmissionEnergyMultiplier = 0.6f;
                    break;
                case LootBoxTier.Diamond:
                    // Crystalline cyan
                    body.AlbedoColor = new Color(0.3f, 0.7f, 0.85f);
                    body.Metallic = 0.8f;
                    body.Roughness = 0.1f;
                    accent.AlbedoColor = new Color(0.5f, 0.95f, 1.0f);
                    accent.Metallic = 0.9f;
                    accent.Roughness = 0.05f;
                    accent.Emission = new Color(0.4f, 0.9f, 1.0f);
                    accent.EmissionEnabled = true;
                    accent.EmissionEnergyMultiplier = 1.5f;
                    break;
                case LootBoxTier.Legendary:
                    // Glowing violet
                    body.AlbedoColor = new Color(0.5f, 0.2f, 0.7f);
                    body.Metallic = 0.9f;
                    body.Roughness = 0.1f;
                    accent.AlbedoColor = new Color(0.75f, 0.35f, 1.0f);
                    accent.Metallic = 0.95f;
                    accent.Roughness = 0.05f;
                    accent.Emission = new Color(0.7f, 0.3f, 0.9f);
                    accent.EmissionEnabled = true;
                    accent.EmissionEnergyMultiplier = 2.5f;
                    break;
                case LootBoxTier.Celestial:
                    // Radiant white-gold
                    body.AlbedoColor = new Color(0.9f, 0.85f, 0.6f);
                    body.Metallic = 1.0f;
                    body.Roughness = 0.05f;
                    accent.AlbedoColor = new Color(1.0f, 0.97f, 0.8f);
                    accent.Metallic = 1.0f;
                    accent.Roughness = 0.02f;
                    accent.Emission = new Color(1.0f, 0.95f, 0.7f);
                    accent.EmissionEnabled = true;
                    accent.EmissionEnergyMultiplier = 4.0f;
                    break;
            }

            return (body, accent);
        }

        private static Node3D BuildBronzeLootBox()
        {
            var root = new Node3D();
            root.Name = "LootBox_Bronze";

            // High-contrast colors: dark wood body, bright metallic bands
            Color woodDark = new Color(0.25f, 0.15f, 0.08f);
            Color woodLight = new Color(0.35f, 0.22f, 0.12f);
            Color metalBronze = new Color(0.75f, 0.55f, 0.25f);
            Color metalLatch = new Color(0.85f, 0.65f, 0.2f);

            // Box body — dark wood
            var body = CreateMeshNode("_Body",
                new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.35f) },
                woodDark, new Vector3(0, 0.175f, 0));
            ((StandardMaterial3D)body.MaterialOverride).Roughness = 0.85f;
            root.AddChild(body);

            // Lid — slightly lighter wood
            var lid = CreateMeshNode("_Lid",
                new BoxMesh { Size = new Vector3(0.52f, 0.06f, 0.37f) },
                woodLight, new Vector3(0, 0.38f, 0));
            ((StandardMaterial3D)lid.MaterialOverride).Roughness = 0.8f;
            root.AddChild(lid);

            // 2 metal bands — bright metallic bronze
            for (int i = 0; i < 2; i++)
            {
                root.AddChild(CreateMetallicMeshNode($"_Band{i}",
                    new BoxMesh { Size = new Vector3(0.53f, 0.035f, 0.38f) },
                    metalBronze, 0.7f, 0.3f, new Vector3(0, 0.12f + i * 0.14f, 0)));
            }

            // Latch — emissive metallic accent
            root.AddChild(CreateEmissiveMeshNode("_Latch",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.045f, RadialSegments = 8 },
                metalLatch, metalLatch * 0.4f, new Vector3(0, 0.2f, -0.19f)));

            // Handle — bright metal
            root.AddChild(CreateMetallicMeshNode("_Handle",
                new BoxMesh { Size = new Vector3(0.12f, 0.025f, 0.025f) },
                metalBronze, 0.6f, 0.35f, new Vector3(0, 0.42f, 0)));

            // Corner brackets — extra visual detail
            float[] cx = { -0.24f, 0.24f, -0.24f, 0.24f };
            float[] cy = { 0.34f, 0.34f, 0.02f, 0.02f };
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateMetallicMeshNode($"_Corner{i}",
                    new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.02f) },
                    metalBronze, 0.65f, 0.35f, new Vector3(cx[i], cy[i], -0.18f)));
            }

            return root;
        }

        private static Node3D BuildSilverLootBox()
        {
            var root = new Node3D();
            root.Name = "LootBox_Silver";

            // Dark body with bright silver metallic bands
            Color bodyDark = new Color(0.18f, 0.18f, 0.22f);
            Color silver = new Color(0.82f, 0.84f, 0.88f);
            Color silverBright = new Color(0.9f, 0.92f, 0.95f);

            // Dark body
            var body = CreateMeshNode("_Body",
                new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.35f) },
                bodyDark, new Vector3(0, 0.175f, 0));
            ((StandardMaterial3D)body.MaterialOverride).Metallic = 0.3f;
            ((StandardMaterial3D)body.MaterialOverride).Roughness = 0.6f;
            root.AddChild(body);

            // Silver lid
            root.AddChild(CreateMetallicMeshNode("_Lid",
                new BoxMesh { Size = new Vector3(0.52f, 0.06f, 0.37f) },
                silver, 0.7f, 0.25f, new Vector3(0, 0.38f, 0)));

            // 3 emissive silver bands
            for (int i = 0; i < 3; i++)
            {
                root.AddChild(CreateEmissiveMeshNode($"_Band{i}",
                    new BoxMesh { Size = new Vector3(0.53f, 0.028f, 0.38f) },
                    silverBright, silverBright * 0.3f, new Vector3(0, 0.08f + i * 0.1f, 0)));
            }

            // 4 corner rivets — bright silver
            float[] rx = { -0.24f, 0.24f, -0.24f, 0.24f };
            float[] ry = { 0.35f, 0.35f, 0.02f, 0.02f };
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateMetallicMeshNode($"_Rivet{i}",
                    new SphereMesh { Radius = 0.022f, Height = 0.044f, RadialSegments = 8, Rings = 4 },
                    silverBright, 0.8f, 0.15f, new Vector3(rx[i], ry[i], -0.18f)));
            }

            // Emissive keyhole
            root.AddChild(CreateEmissiveMeshNode("_Keyhole",
                new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = 0.03f, RadialSegments = 8 },
                silverBright, silverBright * 0.5f, new Vector3(0, 0.2f, -0.19f)));

            return root;
        }

        private static Node3D BuildGoldLootBox()
        {
            var root = new Node3D();
            root.Name = "LootBox_Gold";
            Color gold = new Color(0.85f, 0.7f, 0.2f);
            Color darkGold = new Color(0.65f, 0.5f, 0.15f);
            Color gem = new Color(0.9f, 0.15f, 0.1f);

            // Gold body
            root.AddChild(CreateMeshNode("_Body",
                new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.35f) },
                gold, new Vector3(0, 0.175f, 0)));
            root.AddChild(CreateMeshNode("_Lid",
                new BoxMesh { Size = new Vector3(0.52f, 0.06f, 0.37f) },
                gold.Lightened(0.1f), new Vector3(0, 0.38f, 0)));
            root.AddChild(CreateMeshNode("_LidRidge",
                new BoxMesh { Size = new Vector3(0.42f, 0.02f, 0.28f) },
                darkGold, new Vector3(0, 0.4f, 0)));
            // 4 emissive gold bands
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateEmissiveMeshNode($"_Band{i}",
                    new BoxMesh { Size = new Vector3(0.52f, 0.02f, 0.37f) },
                    gold, gold * 0.8f, new Vector3(0, 0.06f + i * 0.08f, 0)));
            }
            // 4 corner rivets
            float[] rx = { -0.24f, 0.24f, -0.24f, 0.24f };
            float[] ry = { 0.33f, 0.33f, 0.02f, 0.02f };
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateMeshNode($"_Rivet{i}",
                    new SphereMesh { Radius = 0.022f, Height = 0.044f, RadialSegments = 6, Rings = 3 },
                    gold.Lightened(0.15f), new Vector3(rx[i], ry[i], -0.18f)));
            }
            // Lock + gem
            root.AddChild(CreateMeshNode("_Lock",
                new BoxMesh { Size = new Vector3(0.06f, 0.08f, 0.025f) },
                darkGold, new Vector3(0, 0.2f, -0.19f)));
            root.AddChild(CreateEmissiveMeshNode("_LockGem",
                new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                gem, gem, new Vector3(0, 0.2f, -0.205f)));
            // 2 decorative flourishes
            root.AddChild(CreateMeshNode("_FlourishL",
                new BoxMesh { Size = new Vector3(0.03f, 0.12f, 0.01f) },
                darkGold, new Vector3(-0.22f, 0.18f, -0.18f)));
            root.AddChild(CreateMeshNode("_FlourishR",
                new BoxMesh { Size = new Vector3(0.03f, 0.12f, 0.01f) },
                darkGold, new Vector3(0.22f, 0.18f, -0.18f)));

            return root;
        }

        private static Node3D BuildDiamondLootBox()
        {
            var root = new Node3D();
            root.Name = "LootBox_Diamond";
            Color cyan = new Color(0.4f, 0.85f, 0.95f);
            Color crystalGlow = new Color(0.3f, 0.9f, 1f);
            Color band = new Color(0.5f, 0.8f, 0.85f);

            // Cyan body
            root.AddChild(CreateMeshNode("_Body",
                new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.35f) },
                cyan, new Vector3(0, 0.175f, 0)));
            // Translucent lid
            var lid = CreateMeshNode("_Lid",
                new BoxMesh { Size = new Vector3(0.52f, 0.06f, 0.37f) },
                cyan.Lightened(0.15f), new Vector3(0, 0.38f, 0));
            var lidMat = lid.MaterialOverride as StandardMaterial3D;
            if (lidMat != null)
            {
                lidMat.AlbedoColor = new Color(cyan.R, cyan.G, cyan.B, 0.7f);
                lidMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            }
            root.AddChild(lid);
            // Bands + rivets
            for (int i = 0; i < 3; i++)
            {
                root.AddChild(CreateEmissiveMeshNode($"_Band{i}",
                    new BoxMesh { Size = new Vector3(0.52f, 0.02f, 0.37f) },
                    band, band, new Vector3(0, 0.08f + i * 0.1f, 0)));
            }
            float[] rx = { -0.24f, 0.24f, -0.24f, 0.24f };
            float[] ry = { 0.33f, 0.33f, 0.02f, 0.02f };
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateMeshNode($"_Rivet{i}",
                    new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                    band, new Vector3(rx[i], ry[i], -0.18f)));
            }
            // 4 emissive crystal spheres
            float[] csx = { -0.18f, 0.18f, -0.18f, 0.18f };
            float[] csy = { 0.3f, 0.3f, 0.06f, 0.06f };
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateEmissiveMeshNode($"_Crystal{i}",
                    new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 8, Rings = 4 },
                    crystalGlow, crystalGlow, new Vector3(csx[i], csy[i], -0.19f)));
            }
            // Floating emissive ring (torus)
            var floatRing = CreateEmissiveMeshNode("_FloatRing",
                new TorusMesh { InnerRadius = 0.18f, OuterRadius = 0.22f, Rings = 14, RingSegments = 8 },
                crystalGlow, crystalGlow, new Vector3(0, 0.2f, 0));
            root.AddChild(floatRing);

            return root;
        }

        private static Node3D BuildLegendaryLootBox()
        {
            var root = new Node3D();
            root.Name = "LootBox_Legendary";
            Color purpleGold = new Color(0.55f, 0.3f, 0.6f);
            Color gold = new Color(0.85f, 0.7f, 0.2f);
            Color gemRed = new Color(0.9f, 0.15f, 0.1f);
            Color gemBlue = new Color(0.2f, 0.4f, 1f);
            Color gemGreen = new Color(0.2f, 0.9f, 0.3f);
            Color gemPurple = new Color(0.7f, 0.3f, 0.9f);
            Color energy = new Color(0.8f, 0.5f, 1f);

            // Purple-gold body
            root.AddChild(CreateMeshNode("_Body",
                new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.35f) },
                purpleGold, new Vector3(0, 0.175f, 0)));
            root.AddChild(CreateMeshNode("_Lid",
                new BoxMesh { Size = new Vector3(0.52f, 0.06f, 0.37f) },
                purpleGold.Lightened(0.1f), new Vector3(0, 0.38f, 0)));
            // 4 emissive gold bands
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateEmissiveMeshNode($"_Band{i}",
                    new BoxMesh { Size = new Vector3(0.52f, 0.02f, 0.37f) },
                    gold, gold, new Vector3(0, 0.06f + i * 0.08f, 0)));
            }
            // 4 corner rivets
            float[] rx = { -0.24f, 0.24f, -0.24f, 0.24f };
            float[] ry = { 0.33f, 0.33f, 0.02f, 0.02f };
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateMeshNode($"_Rivet{i}",
                    new SphereMesh { Radius = 0.022f, Height = 0.044f, RadialSegments = 6, Rings = 3 },
                    gold, new Vector3(rx[i], ry[i], -0.18f)));
            }
            // 4+ glowing gems (different colors)
            Color[] gemColors = { gemRed, gemBlue, gemGreen, gemPurple };
            float[] gx = { -0.15f, 0.15f, -0.15f, 0.15f };
            float[] gy = { 0.28f, 0.28f, 0.08f, 0.08f };
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateEmissiveMeshNode($"_Gem{i}",
                    new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                    gemColors[i], gemColors[i], new Vector3(gx[i], gy[i], -0.19f)));
            }
            // Crown with spikes on top
            root.AddChild(CreateEmissiveMeshNode("_Crown",
                new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.12f, Height = 0.08f, RadialSegments = 8 },
                gold, gold, new Vector3(0, 0.44f, 0)));
            for (int i = 0; i < 5; i++)
            {
                float angle = (float)i / 5f * Mathf.Tau;
                root.AddChild(CreateEmissiveMeshNode($"_CrownSpike{i}",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.02f, Height = 0.08f, RadialSegments = 4 },
                    gold, gold, new Vector3(Mathf.Cos(angle) * 0.11f, 0.52f, Mathf.Sin(angle) * 0.11f)));
            }
            // Orbiting energy torus
            var orbitRing = CreateEmissiveMeshNode("_OrbitRing",
                new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.24f, Rings = 14, RingSegments = 8 },
                energy, energy, new Vector3(0, 0.2f, 0));
            orbitRing.RotateX(Mathf.DegToRad(15));
            root.AddChild(orbitRing);
            // Accent spheres
            root.AddChild(CreateEmissiveMeshNode("_AccentSphereL",
                new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                energy, energy, new Vector3(-0.28f, 0.2f, 0)));
            root.AddChild(CreateEmissiveMeshNode("_AccentSphereR",
                new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                energy, energy, new Vector3(0.28f, 0.2f, 0)));
            // Chain detail on front
            root.AddChild(CreateMeshNode("_ChainLink1",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.01f, Height = 0.04f, RadialSegments = 4 },
                gold.Darkened(0.15f), new Vector3(0, 0.14f, -0.19f)));
            root.AddChild(CreateMeshNode("_ChainLink2",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.01f, Height = 0.04f, RadialSegments = 4 },
                gold.Darkened(0.15f), new Vector3(0, 0.1f, -0.19f)));

            return root;
        }

        private static Node3D BuildDefaultItemModel()
        {
            var root = new Node3D();
            root.Name = "DefaultItem";

            var box = CreateMeshNode("Box", new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) },
                new Color(0.5f, 0.5f, 0.5f), Vector3.Zero);
            root.AddChild(box);

            return root;
        }

        // ── Single-mesh versions (BuildItemMesh fallbacks) ──

        private static Mesh BuildSwordMesh()
        {
            return new BoxMesh { Size = new Vector3(0.08f, 0.5f, 0.03f) };
        }

        private static Mesh BuildStaffMesh()
        {
            return new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.6f, RadialSegments = 6 };
        }

        private static Mesh BuildDaggerMesh()
        {
            return new BoxMesh { Size = new Vector3(0.04f, 0.28f, 0.015f) };
        }

        private static Mesh BuildShieldMesh()
        {
            return new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.18f, Height = 0.025f, RadialSegments = 12 };
        }

        private static Mesh BuildHelmetMesh()
        {
            return new SphereMesh { Radius = 0.2f, Height = 0.25f, RadialSegments = 10, Rings = 5 };
        }

        private static Mesh BuildChestplateMesh()
        {
            return new BoxMesh { Size = new Vector3(0.35f, 0.3f, 0.15f) };
        }

        private static Mesh BuildRobeMesh()
        {
            return new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 0.3f, RadialSegments = 8 };
        }

        private static Mesh BuildGreavesMesh()
        {
            return new BoxMesh { Size = new Vector3(0.16f, 0.22f, 0.06f) };
        }

        private static Mesh BuildBootsMesh()
        {
            return new BoxMesh { Size = new Vector3(0.16f, 0.08f, 0.14f) };
        }

        private static Mesh BuildGauntletsMesh()
        {
            return new BoxMesh { Size = new Vector3(0.14f, 0.06f, 0.1f) };
        }

        private static Mesh BuildCloakMesh()
        {
            return new BoxMesh { Size = new Vector3(0.22f, 0.28f, 0.015f) };
        }

        private static Mesh BuildPotionMesh()
        {
            return new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.1f, Height = 0.25f, RadialSegments = 8 };
        }

        private static Mesh BuildRepairKitMesh()
        {
            return new BoxMesh { Size = new Vector3(0.18f, 0.1f, 0.12f) };
        }

        private static Mesh BuildBatteryPackMesh()
        {
            return new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.2f, RadialSegments = 10 };
        }

        private static Mesh BuildPlatingBoosterMesh()
        {
            return new BoxMesh { Size = new Vector3(0.1f, 0.06f, 0.16f) };
        }

        private static Mesh BuildOverclockInjectorMesh()
        {
            return new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.2f, RadialSegments = 8 };
        }

        private static Mesh BuildRingMesh()
        {
            return new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.1f, Rings = 12, RingSegments = 8 };
        }

        private static Mesh BuildAmuletMesh()
        {
            return new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 8, Rings = 4 };
        }

        private static Mesh BuildDefaultItemMesh()
        {
            return new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) };
        }

        // ── Blade Ring ──

        public static Node3D BuildBladeRing()
        {
            var root = new Node3D();
            root.Name = "BladeRingVisual";

            Color bladeMetal = new Color(0.55f, 0.55f, 0.6f);
            Color edgeGlow = new Color(1f, 0.6f, 0.2f);
            Color hubColor = new Color(0.4f, 0.4f, 0.45f);
            float radius = 1.8f;
            int bladeCount = 4;
            float ringY = 0.6f; // waist-height orbit

            // Central hub ring (torus-like)
            var hub = CreateEmissiveMeshNode("Hub",
                new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.25f, Height = 0.06f, RadialSegments = 16 },
                hubColor, edgeGlow * 0.3f, new Vector3(0f, ringY, 0f));
            root.AddChild(hub);

            // Outer guide ring
            root.AddChild(CreateEmissiveMeshNode("GuideRing",
                new TorusMesh { InnerRadius = radius * 0.45f, OuterRadius = radius * 0.5f },
                hubColor, edgeGlow * 0.15f, new Vector3(0f, ringY, 0f)));

            for (int i = 0; i < bladeCount; i++)
            {
                float angle = (float)i / bladeCount * Mathf.Tau;
                var bladeDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var pos = bladeDir * radius * 0.45f + new Vector3(0f, ringY, 0f);

                var pivot = new Node3D();
                pivot.Name = $"BladePivot{i}";
                pivot.Position = pos;
                pivot.RotateY(-angle);
                root.AddChild(pivot);

                // Blade body — curved, thinner profile
                var blade = CreateMeshNode($"Blade{i}",
                    new BoxMesh { Size = new Vector3(0.8f, 0.05f, 0.22f) },
                    bladeMetal, Vector3.Zero);
                pivot.AddChild(blade);

                // Emissive cutting edge
                var edge = CreateEmissiveMeshNode($"Edge{i}",
                    new BoxMesh { Size = new Vector3(0.84f, 0.02f, 0.06f) },
                    edgeGlow, edgeGlow, new Vector3(0f, 0f, 0.1f));
                pivot.AddChild(edge);

                // Connecting arm from hub to blade
                var arm = CreateMeshNode($"Arm{i}",
                    new BoxMesh { Size = new Vector3(0.06f, 0.04f, radius * 0.2f) },
                    hubColor, new Vector3(0f, 0f, -0.2f));
                pivot.AddChild(arm);
            }

            return root;
        }

        public static Node3D BuildFlailChain()
        {
            var root = new Node3D();
            root.Name = "FlailChainVisual";

            Color chainColor = new Color(0.5f, 0.5f, 0.55f);
            Color ballColor = new Color(0.7f, 0.2f, 0.2f);
            Color spikeGlow = new Color(1f, 0.3f, 0.1f);

            // Chain links from center outward
            for (int i = 0; i < 6; i++)
            {
                float t = (i + 1) / 7f;
                var linkPos = new Vector3(t * 2f, 0.5f, 0f);
                var link = CreateMeshNode($"Link{i}",
                    new BoxMesh { Size = new Vector3(0.12f, 0.06f, 0.08f) },
                    chainColor, linkPos);
                root.AddChild(link);
            }

            // Wrecking ball at end
            var ball = CreateEmissiveMeshNode("Ball",
                new SphereMesh { Radius = 0.35f, Height = 0.7f, RadialSegments = 12, Rings = 6 },
                ballColor, spikeGlow, new Vector3(2f, 0.5f, 0f));
            root.AddChild(ball);

            // Spikes on ball
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.Tau / 4f;
                var spikePos = new Vector3(2f + Mathf.Cos(angle) * 0.3f, 0.5f + Mathf.Sin(angle) * 0.3f, 0f);
                var spike = CreateEmissiveMeshNode($"Spike{i}",
                    new BoxMesh { Size = new Vector3(0.15f, 0.15f, 0.15f) },
                    spikeGlow, spikeGlow, spikePos);
                root.AddChild(spike);
            }

            return root;
        }

        public static Node3D BuildShockCoil()
        {
            var root = new Node3D();
            root.Name = "ShockCoilVisual";

            Color coilColor = new Color(0.3f, 0.3f, 0.4f);
            Color sparkColor = new Color(0.5f, 0.8f, 1f);
            Color arcGlow = new Color(0.3f, 0.6f, 1f);
            Color metalColor = new Color(0.4f, 0.4f, 0.45f);

            // Backpack housing (mounted on back)
            root.AddChild(CreateMeshNode("BackpackBase",
                new BoxMesh { Size = new Vector3(0.35f, 0.4f, 0.15f) },
                metalColor, new Vector3(0f, 0.8f, 0.2f)));

            // Central Tesla coil pillar (shorter, on the backpack)
            root.AddChild(CreateMeshNode("Pillar",
                new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.1f, Height = 0.45f, RadialSegments = 8 },
                coilColor, new Vector3(0f, 1.2f, 0.2f)));

            // Top conductor sphere
            root.AddChild(CreateEmissiveMeshNode("TopSphere",
                new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 10, Rings = 5 },
                sparkColor, arcGlow, new Vector3(0f, 1.5f, 0.2f)));

            // Coil rings around pillar
            for (int i = 0; i < 3; i++)
            {
                float y = 1.05f + i * 0.15f;
                root.AddChild(CreateEmissiveMeshNode($"Ring{i}",
                    new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.14f },
                    sparkColor, arcGlow, new Vector3(0f, y, 0.2f)));
            }

            // Side arc emitter prongs (left + right, angled outward)
            for (float side = -1; side <= 1; side += 2)
            {
                string sName = side < 0 ? "L" : "R";

                // Emitter arm from backpack
                var arm = CreateMeshNode($"EmitterArm{sName}",
                    new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.25f, RadialSegments = 6 },
                    metalColor, new Vector3(side * 0.22f, 1.0f, 0.15f));
                arm.RotateZ(Mathf.DegToRad(side * 30));
                root.AddChild(arm);

                // Emitter tip (glowing)
                root.AddChild(CreateEmissiveMeshNode($"EmitterTip{sName}",
                    new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 },
                    sparkColor, arcGlow, new Vector3(side * 0.32f, 1.12f, 0.15f)));

                // Arc trace between emitter and central pillar
                root.AddChild(CreateEmissiveMeshNode($"ArcTrace{sName}",
                    new BoxMesh { Size = new Vector3(0.18f, 0.015f, 0.015f) },
                    arcGlow, arcGlow, new Vector3(side * 0.16f, 1.3f, 0.2f)));
            }

            // Power conduit cables from backpack down
            for (float side = -1; side <= 1; side += 2)
            {
                root.AddChild(CreateMeshNode(side < 0 ? "ConduitL" : "ConduitR",
                    new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.3f, RadialSegments = 4 },
                    coilColor, new Vector3(side * 0.12f, 0.55f, 0.2f)));
            }

            return root;
        }

        public static Node3D BuildFlameThrower()
        {
            var root = new Node3D();
            root.Name = "FlameThrowerVisual";

            Color metalColor = new Color(0.4f, 0.35f, 0.3f);
            Color tankColor = new Color(0.5f, 0.25f, 0.15f);
            Color nozzleColor = new Color(0.6f, 0.3f, 0.1f);
            Color flameGlow = new Color(1f, 0.5f, 0.1f);
            Color hoseColor = new Color(0.25f, 0.25f, 0.28f);

            // Dual fuel cylinders on back (backpack style)
            for (float side = -1; side <= 1; side += 2)
            {
                string sName = side < 0 ? "L" : "R";
                // Main tank cylinder
                root.AddChild(CreateMeshNode($"Tank{sName}",
                    new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.5f, RadialSegments = 8 },
                    tankColor, new Vector3(side * 0.1f, 0.8f, 0.22f)));
                // Tank cap
                root.AddChild(CreateMeshNode($"TankCap{sName}",
                    new SphereMesh { Radius = 0.08f, Height = 0.1f, RadialSegments = 8, Rings = 4 },
                    metalColor, new Vector3(side * 0.1f, 1.06f, 0.22f)));
                // Pressure gauge
                root.AddChild(CreateMeshNode($"Gauge{sName}",
                    new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.03f, RadialSegments = 6 },
                    metalColor, new Vector3(side * 0.1f + side * 0.08f, 0.95f, 0.22f)));
            }

            // Backpack frame/harness connecting tanks
            root.AddChild(CreateMeshNode("BackpackFrame",
                new BoxMesh { Size = new Vector3(0.28f, 0.08f, 0.1f) },
                metalColor, new Vector3(0f, 0.95f, 0.22f)));
            root.AddChild(CreateMeshNode("BackpackFrameLow",
                new BoxMesh { Size = new Vector3(0.28f, 0.06f, 0.08f) },
                metalColor, new Vector3(0f, 0.62f, 0.22f)));

            // Fuel hose from backpack to gun (routed over shoulder)
            var hose1 = CreateMeshNode("HoseVert",
                new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = 0.2f, RadialSegments = 4 },
                hoseColor, new Vector3(0.15f, 0.95f, 0.12f));
            root.AddChild(hose1);
            var hose2 = CreateMeshNode("HoseHoriz",
                new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = 0.25f, RadialSegments = 4 },
                hoseColor, new Vector3(0.15f, 0.85f, -0.02f));
            hose2.RotateX(Mathf.DegToRad(90f));
            root.AddChild(hose2);

            // Gun barrel (offset to the right side, held in hand)
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.055f, Height = 0.6f, RadialSegments = 8 },
                metalColor, new Vector3(0.3f, 0.7f, -0.25f));
            barrel.RotateX(Mathf.DegToRad(90f));
            root.AddChild(barrel);

            // Barrel shroud (heat shield)
            var shroud = CreateMeshNode("Shroud",
                new CylinderMesh { TopRadius = 0.065f, BottomRadius = 0.07f, Height = 0.15f, RadialSegments = 8 },
                metalColor.Darkened(0.1f), new Vector3(0.3f, 0.7f, -0.52f));
            shroud.RotateX(Mathf.DegToRad(90f));
            root.AddChild(shroud);

            // Nozzle/pilot light at barrel end
            root.AddChild(CreateEmissiveMeshNode("Nozzle",
                new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 8, Rings = 4 },
                nozzleColor, flameGlow, new Vector3(0.3f, 0.7f, -0.6f)));

            // Pilot flame glow
            root.AddChild(CreateEmissiveMeshNode("PilotFlame",
                new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                flameGlow, flameGlow, new Vector3(0.3f, 0.7f, -0.65f)));

            return root;
        }

        // ── Helpers ──

        private static Node3D CreatePivot(string name, Vector3 position)
        {
            var pivot = new Node3D();
            pivot.Name = name;
            pivot.Position = position;
            return pivot;
        }

        private static MeshInstance3D CreateMeshNode(string name, Mesh mesh, Color color, Vector3 position)
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

        private static MeshInstance3D CreateEmissiveMeshNode(string name, Mesh mesh, Color color, Color emission, Vector3 position)
        {
            var node = new MeshInstance3D();
            node.Name = name;
            node.Mesh = mesh;
            node.Position = position;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.EmissionEnabled = true;
            mat.Emission = emission;
            mat.EmissionEnergyMultiplier = 1.5f;
            node.MaterialOverride = mat;

            return node;
        }

        private static MeshInstance3D CreateMetallicMeshNode(string name, Mesh mesh, Color color, float metallic, float roughness, Vector3 position)
        {
            var node = new MeshInstance3D();
            node.Name = name;
            node.Mesh = mesh;
            node.Position = position;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.Metallic = metallic;
            mat.Roughness = roughness;
            node.MaterialOverride = mat;

            return node;
        }

        /// <summary>
        /// Recursively find the first AnimationPlayer in a model hierarchy.
        /// </summary>
        public static AnimationPlayer FindAnimationPlayer(Node3D model)
        {
            return FindAnimationPlayerRecursive(model);
        }

        private static AnimationPlayer FindAnimationPlayerRecursive(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is AnimationPlayer found) return found;
                var result = FindAnimationPlayerRecursive(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Scale a model uniformly so its AABB height matches targetHeight.
        /// </summary>
        public static void ScaleModelToFit(Node3D model, float targetHeight)
        {
            // Use transform-aware AABB that accounts for intermediate FBX
            // scale/rotation nodes (e.g. Z-up to Y-up coordinate conversion).
            // Includes root rotation but excludes root scale (since we override it).
            var aabb = GetEffectiveAabb(model);

            if (aabb.Size.Y <= 0.001f)
            {
                // Last resort: apply a conservative default scale
                GD.Print($"[CharacterMeshBuilder] AABB detection failed for model, applying fallback scale for {targetHeight}m");
                model.Scale = Vector3.One * 0.01f * targetHeight;
                return;
            }

            // Use the largest AABB dimension so wide T-pose models don't end up oversized
            float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
            float scale = targetHeight / maxDim;
            model.Scale = Vector3.One * scale;
            GD.Print($"[ScaleModelToFit] '{model.Name}' AABB={aabb.Size} maxDim={maxDim} targetH={targetHeight} scale={scale}");
        }

        /// <summary>
        /// Scale a weapon model so it appears at the desired WORLD size,
        /// compensating for the parent body's scale. Must be called AFTER
        /// the weapon has been added to the scene tree (so GlobalTransform is valid).
        /// </summary>
        public static void ScaleWeaponToWorldSize(Node3D weapon, float desiredWorldSize)
        {
            var aabb = GetEffectiveAabb(weapon);
            float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
            if (maxDim <= 0.001f)
            {
                weapon.Scale = Vector3.One * 0.01f * desiredWorldSize;
                return;
            }

            // Compute local scale that produces the target world size
            float localScale = desiredWorldSize / maxDim;

            // Compensate for parent's accumulated scale
            var parent = weapon.GetParent<Node3D>();
            if (parent != null)
            {
                var parentGlobalScale = parent.GlobalTransform.Basis.Scale;
                float avgParentScale = (parentGlobalScale.X + parentGlobalScale.Y + parentGlobalScale.Z) / 3f;
                if (avgParentScale > 0.001f)
                    localScale /= avgParentScale;
            }

            weapon.Scale = Vector3.One * localScale;
            GD.Print($"[ScaleWeaponToWorldSize] '{weapon.Name}' AABB.maxDim={maxDim} desiredWorld={desiredWorldSize} localScale={localScale}");
        }

        /// <summary>
        /// Compute the effective AABB of a model by walking the scene tree and
        /// accumulating intermediate transforms (handles FBX scale/rotation nodes).
        /// Includes root rotation (for coordinate conversion) but excludes root
        /// scale since callers override root.Scale.
        /// </summary>
        private static Aabb GetEffectiveAabb(Node3D root)
        {
            Aabb combined = new Aabb();
            bool first = true;

            // Include root's rotation but strip its scale
            var rootRotation = new Transform3D(root.Basis.Orthonormalized(), Vector3.Zero);

            foreach (var child in root.GetChildren())
                CollectTransformedAabbs(child, rootRotation, ref combined, ref first);

            // If root itself is a mesh with no children
            if (first && root is MeshInstance3D mi && mi.Mesh != null)
                combined = mi.Mesh.GetAabb();

            return combined;
        }

        private static void CollectTransformedAabbs(Node node, Transform3D accumulated,
            ref Aabb combined, ref bool first)
        {
            Transform3D current = accumulated;
            if (node is Node3D n3d)
                current = accumulated * n3d.Transform;

            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var meshAabb = mi.Mesh.GetAabb();
                var pos = meshAabb.Position;
                var end = meshAabb.End;

                // Transform all 8 AABB corners to get the true extent
                var c0 = current * new Vector3(pos.X, pos.Y, pos.Z);
                var minV = c0;
                var maxV = c0;
                Vector3[] corners =
                {
                    current * new Vector3(end.X, pos.Y, pos.Z),
                    current * new Vector3(pos.X, end.Y, pos.Z),
                    current * new Vector3(end.X, end.Y, pos.Z),
                    current * new Vector3(pos.X, pos.Y, end.Z),
                    current * new Vector3(end.X, pos.Y, end.Z),
                    current * new Vector3(pos.X, end.Y, end.Z),
                    current * new Vector3(end.X, end.Y, end.Z),
                };
                foreach (var c in corners)
                {
                    minV = new Vector3(Mathf.Min(minV.X, c.X), Mathf.Min(minV.Y, c.Y), Mathf.Min(minV.Z, c.Z));
                    maxV = new Vector3(Mathf.Max(maxV.X, c.X), Mathf.Max(maxV.Y, c.Y), Mathf.Max(maxV.Z, c.Z));
                }

                var transformedAabb = new Aabb(minV, maxV - minV);
                if (first) { combined = transformedAabb; first = false; }
                else combined = combined.Merge(transformedAabb);
            }

            foreach (var child in node.GetChildren())
                CollectTransformedAabbs(child, current, ref combined, ref first);
        }

        /// <summary>
        /// Recursively find the first Mesh resource in a model's MeshInstance3D children.
        /// </summary>
        private static Mesh FindMeshInModel(Node model)
        {
            if (model is MeshInstance3D mi && mi.Mesh != null) return mi.Mesh;
            foreach (var child in model.GetChildren())
            {
                if (child is Node node)
                {
                    var mesh = FindMeshInModel(node);
                    if (mesh != null) return mesh;
                }
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  GROWTH PIECES — add-on geometry per tier per frame
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Build add-on geometry for a given growth tier. Returns null for Base tier.
        /// Attach the returned node as a child of the body root.
        /// </summary>
        public static Node3D BuildGrowthPieces(BotFrameType className, GrowthTier tier)
        {
            if (tier == GrowthTier.Base) return null;

            var root = new Node3D();
            root.Name = $"Growth_{tier}";
            Color accent = GetClassColor(className);
            Color metal = new Color(0.35f, 0.35f, 0.38f);
            Color darkMetal = new Color(0.22f, 0.22f, 0.25f);

            // Each tier is cumulative — higher tiers include lower tier pieces
            if (tier >= GrowthTier.Plated)
                AddTier1Pieces(root, className, accent, metal);
            if (tier >= GrowthTier.Armored)
                AddTier2Pieces(root, className, accent, metal, darkMetal);
            if (tier >= GrowthTier.Heavy)
                AddTier3Pieces(root, className, accent, metal, darkMetal);
            if (tier >= GrowthTier.Evolved)
                AddTier4Pieces(root, className, accent);

            return root;
        }

        // Tier 1: Plated — shoulder armor, forearm guards, frame-specific locomotion guards
        private static void AddTier1Pieces(Node3D root, BotFrameType frame, Color accent, Color metal)
        {
            Color plate = metal.Darkened(0.08f);
            Color rivet = metal.Lightened(0.15f);

            float shoulderY = frame switch
            {
                BotFrameType.Scrapheap => 0.68f,
                BotFrameType.SparkPlug => 1.02f,
                BotFrameType.RustBucket => 0.58f,
                BotFrameType.NoiseBox => 0.92f,
                BotFrameType.Clunker => 0.82f,
                _ => 0.82f
            };
            float shoulderX = frame switch
            {
                BotFrameType.Scrapheap => 0.36f,
                BotFrameType.SparkPlug => 0.18f,
                BotFrameType.RustBucket => 0.22f,
                _ => 0.26f
            };

            // ── Shoulder guards — angled plates flush against torso top ──
            for (float side = -1; side <= 1; side += 2)
            {
                // Main shoulder plate — angled outward like a layered scrap panel
                var shoulder = CreateMeshNode(side < 0 ? "_T1_LeftShoulder" : "_T1_RightShoulder",
                    new BoxMesh { Size = new Vector3(0.16f, 0.05f, 0.14f) },
                    plate, new Vector3(side * shoulderX, shoulderY, 0));
                shoulder.RotationDegrees = new Vector3(0, 0, side * -15f);
                root.AddChild(shoulder);

                // Shoulder accent strip — thin colored line on top edge
                root.AddChild(CreateMeshNode(side < 0 ? "_T1_ShoulderTrimL" : "_T1_ShoulderTrimR",
                    new BoxMesh { Size = new Vector3(0.14f, 0.012f, 0.06f) },
                    accent, new Vector3(side * shoulderX, shoulderY + 0.03f, -0.04f)));

                // Rivet row on shoulder plate
                for (int r = 0; r < 3; r++)
                {
                    root.AddChild(CreateMeshNode($"_T1_Rivet{(side < 0 ? "L" : "R")}{r}",
                        new SphereMesh { Radius = 0.008f, Height = 0.016f, RadialSegments = 4, Rings = 2 },
                        rivet, new Vector3(side * (shoulderX - 0.05f + r * 0.05f), shoulderY + 0.028f, -0.065f)));
                }

                // Forearm guard — wraps around the forearm
                float armY = shoulderY - 0.28f;
                var gauntlet = CreateMeshNode(side < 0 ? "_T1_LeftGauntlet" : "_T1_RightGauntlet",
                    new BoxMesh { Size = new Vector3(0.07f, 0.12f, 0.08f) },
                    plate, new Vector3(side * (shoulderX + 0.02f), armY, -0.02f));
                root.AddChild(gauntlet);

                // Gauntlet pipe detail
                var pipe = CreateMeshNode(side < 0 ? "_T1_GauntPipeL" : "_T1_GauntPipeR",
                    new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = 0.1f, RadialSegments = 6 },
                    metal.Darkened(0.15f), new Vector3(side * (shoulderX + 0.05f), armY, 0));
                root.AddChild(pipe);
            }

            // ── Frame-specific locomotion guards ──
            switch (frame)
            {
                case BotFrameType.Scrapheap:
                    // Track fenders — wrap over the treads with angled lip
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T1_TrackFenderL" : "_T1_TrackFenderR",
                            new BoxMesh { Size = new Vector3(0.14f, 0.04f, 0.36f) },
                            plate, new Vector3(side * 0.39f, 0.24f, 0)));
                        // Front fender lip
                        var lip = CreateMeshNode(side < 0 ? "_T1_FenderLipL" : "_T1_FenderLipR",
                            new BoxMesh { Size = new Vector3(0.14f, 0.06f, 0.02f) },
                            plate, new Vector3(side * 0.39f, 0.22f, -0.18f));
                        lip.RotationDegrees = new Vector3(20f, 0, 0);
                        root.AddChild(lip);
                    }
                    break;
                case BotFrameType.TinCan:
                    // Wheel fender covers — curved shape approximated with angled boxes
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T1_WheelGuardL" : "_T1_WheelGuardR",
                            new BoxMesh { Size = new Vector3(0.06f, 0.05f, 0.3f) },
                            plate, new Vector3(side * 0.28f, 0.18f, 0)));
                        // Mud flap behind wheel
                        root.AddChild(CreateMeshNode(side < 0 ? "_T1_MudFlapL" : "_T1_MudFlapR",
                            new BoxMesh { Size = new Vector3(0.06f, 0.08f, 0.02f) },
                            metal.Darkened(0.2f), new Vector3(side * 0.28f, 0.12f, 0.16f)));
                    }
                    break;
                case BotFrameType.SparkPlug:
                    // Hover ring reinforcement — outer energy ring with support struts
                    root.AddChild(CreateEmissiveMeshNode("_T1_HoverRing",
                        new TorusMesh { InnerRadius = 0.18f, OuterRadius = 0.22f, Rings = 14, RingSegments = 8 },
                        accent * 0.5f, accent, new Vector3(0, 0.12f, 0)));
                    // 4 support struts connecting ring to body
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.DegToRad(45f + 90f * i);
                        var strut = CreateMeshNode($"_T1_HoverStrut{i}",
                            new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.01f, Height = 0.16f, RadialSegments = 4 },
                            metal, new Vector3(Mathf.Cos(angle) * 0.1f, 0.2f, Mathf.Sin(angle) * 0.1f));
                        strut.RotationDegrees = new Vector3(Mathf.Sin(angle) * 40f, 0, -Mathf.Cos(angle) * 40f);
                        root.AddChild(strut);
                    }
                    break;
                case BotFrameType.RustBucket:
                    // Spider leg joint caps — armored knee covers at each leg root
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.DegToRad(45f + 90f * i);
                        float kx = Mathf.Cos(angle) * 0.26f;
                        float kz = Mathf.Sin(angle) * 0.26f;
                        root.AddChild(CreateMeshNode($"_T1_LegCap{i}",
                            new SphereMesh { Radius = 0.04f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                            plate, new Vector3(kx, 0.28f, kz)));
                        // Leg armor plate along upper leg
                        var legPlate = CreateMeshNode($"_T1_LegPlate{i}",
                            new BoxMesh { Size = new Vector3(0.05f, 0.04f, 0.1f) },
                            plate, new Vector3(kx * 1.2f, 0.2f, kz * 1.2f));
                        legPlate.RotationDegrees = new Vector3(0, Mathf.RadToDeg(angle) + 90f, 0);
                        root.AddChild(legPlate);
                    }
                    break;
                case BotFrameType.NoiseBox:
                    // Ball guard bumper ring — protective hoop around mono-ball
                    root.AddChild(CreateMeshNode("_T1_BallGuard",
                        new TorusMesh { InnerRadius = 0.21f, OuterRadius = 0.25f, Rings = 14, RingSegments = 8 },
                        plate, new Vector3(0, 0.2f, 0)));
                    // Two stabilizer fins
                    for (float side = -1; side <= 1; side += 2)
                    {
                        var fin = CreateMeshNode(side < 0 ? "_T1_StabFinL" : "_T1_StabFinR",
                            new BoxMesh { Size = new Vector3(0.02f, 0.12f, 0.08f) },
                            plate, new Vector3(side * 0.26f, 0.2f, 0));
                        fin.RotationDegrees = new Vector3(0, 0, side * -10f);
                        root.AddChild(fin);
                    }
                    break;
                case BotFrameType.Clunker:
                    // Thigh armor wraps on chicken-walker legs
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T1_ThighArmorL" : "_T1_ThighArmorR",
                            new BoxMesh { Size = new Vector3(0.08f, 0.16f, 0.09f) },
                            plate, new Vector3(side * 0.22f, 0.32f, -0.02f)));
                        // Knee joint guard
                        root.AddChild(CreateMeshNode(side < 0 ? "_T1_KneeGuardL" : "_T1_KneeGuardR",
                            new SphereMesh { Radius = 0.035f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                            rivet, new Vector3(side * 0.22f, 0.2f, -0.06f)));
                    }
                    break;
            }
        }

        // Tier 2: Armored — chest overlay, torso widening, head crest, frame-specific bulk
        private static void AddTier2Pieces(Node3D root, BotFrameType frame, Color accent, Color metal, Color dark)
        {
            Color trim = accent.Darkened(0.3f);
            Color panelDark = dark.Darkened(0.05f);

            float torsoY = frame switch
            {
                BotFrameType.Scrapheap => 0.55f,
                BotFrameType.SparkPlug => 0.9f,
                BotFrameType.RustBucket => 0.52f,
                BotFrameType.NoiseBox => 0.85f,
                BotFrameType.Clunker => 0.75f,
                _ => 0.7f
            };
            float torsoHalfW = frame switch
            {
                BotFrameType.Scrapheap => 0.36f,
                BotFrameType.SparkPlug => 0.15f,
                BotFrameType.RustBucket => 0.2f,
                BotFrameType.NoiseBox => 0.22f, // cylinder radius
                BotFrameType.Clunker => 0.275f,
                _ => 0.24f
            };
            float headY = frame switch
            {
                BotFrameType.Scrapheap => 0.95f,
                BotFrameType.SparkPlug => 1.55f,
                BotFrameType.RustBucket => 0.78f,
                BotFrameType.NoiseBox => 1.35f,
                BotFrameType.Clunker => 1.15f,
                _ => 1.2f
            };

            // ── Chest plate — front armor overlay flush with torso ──
            float chestW = torsoHalfW * 1.6f;
            root.AddChild(CreateMeshNode("_T2_ChestPlate",
                new BoxMesh { Size = new Vector3(chestW, 0.2f, 0.04f) },
                panelDark, new Vector3(0, torsoY + 0.02f, -(torsoHalfW * 0.55f + 0.02f))));
            // Accent line across chest plate
            root.AddChild(CreateMeshNode("_T2_ChestTrim",
                new BoxMesh { Size = new Vector3(chestW - 0.04f, 0.015f, 0.008f) },
                accent, new Vector3(0, torsoY + 0.08f, -(torsoHalfW * 0.55f + 0.04f))));

            // ── Side flank armor — widens the profile ──
            for (float side = -1; side <= 1; side += 2)
            {
                root.AddChild(CreateMeshNode(side < 0 ? "_T2_FlankL" : "_T2_FlankR",
                    new BoxMesh { Size = new Vector3(0.04f, 0.18f, 0.16f) },
                    panelDark, new Vector3(side * (torsoHalfW + 0.03f), torsoY, 0)));
                // Panel line on flank
                root.AddChild(CreateMeshNode(side < 0 ? "_T2_FlankLineL" : "_T2_FlankLineR",
                    new BoxMesh { Size = new Vector3(0.008f, 0.14f, 0.008f) },
                    trim, new Vector3(side * (torsoHalfW + 0.05f), torsoY, -0.06f)));
            }

            // ── Head crest — antenna/fin depending on frame style ──
            root.AddChild(CreateMeshNode("_T2_HeadCrest",
                new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.04f, Height = 0.14f, RadialSegments = 5 },
                metal, new Vector3(0, headY + 0.1f, 0)));
            root.AddChild(CreateEmissiveMeshNode("_T2_CrestTip",
                new SphereMesh { Radius = 0.018f, Height = 0.03f, RadialSegments = 5, Rings = 3 },
                accent, accent, new Vector3(0, headY + 0.18f, 0)));
            // Side horns — angled outward
            for (float side = -1; side <= 1; side += 2)
            {
                var horn = CreateMeshNode(side < 0 ? "_T2_HornL" : "_T2_HornR",
                    new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.02f, Height = 0.1f, RadialSegments = 4 },
                    metal, new Vector3(side * 0.08f, headY + 0.06f, 0));
                horn.RotationDegrees = new Vector3(0, 0, side * 25f);
                root.AddChild(horn);
            }

            // ── Back plate — spine armor ──
            root.AddChild(CreateMeshNode("_T2_BackPlate",
                new BoxMesh { Size = new Vector3(chestW * 0.7f, 0.16f, 0.04f) },
                panelDark, new Vector3(0, torsoY, torsoHalfW * 0.5f + 0.02f)));

            // ── Frame-specific torso additions ──
            switch (frame)
            {
                case BotFrameType.Scrapheap:
                    // Reinforced dozer blade extension — wider, angled teeth
                    root.AddChild(CreateMeshNode("_T2_DozerExtend",
                        new BoxMesh { Size = new Vector3(0.7f, 0.06f, 0.05f) },
                        dark, new Vector3(0, 0.3f, -0.26f)));
                    // Dozer teeth
                    for (int i = -2; i <= 2; i++)
                    {
                        var tooth = CreateMeshNode($"_T2_DozerTooth{i}",
                            new BoxMesh { Size = new Vector3(0.03f, 0.04f, 0.03f) },
                            metal.Lightened(0.1f), new Vector3(i * 0.12f, 0.25f, -0.28f));
                        tooth.RotationDegrees = new Vector3(15f, 0, 0);
                        root.AddChild(tooth);
                    }
                    break;
                case BotFrameType.TinCan:
                    // Tactical pods on torso sides
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T2_TacPodL" : "_T2_TacPodR",
                            new BoxMesh { Size = new Vector3(0.06f, 0.12f, 0.1f) },
                            panelDark, new Vector3(side * 0.29f, torsoY + 0.08f, 0.06f)));
                        root.AddChild(CreateMeshNode(side < 0 ? "_T2_PodLensL" : "_T2_PodLensR",
                            new SphereMesh { Radius = 0.015f, Height = 0.02f, RadialSegments = 5, Rings = 3 },
                            accent, new Vector3(side * 0.29f, torsoY + 0.12f, 0.005f)));
                    }
                    break;
                case BotFrameType.SparkPlug:
                    // Energy conduit manifold — wider distribution ring on chest
                    root.AddChild(CreateEmissiveMeshNode("_T2_ConduitRing",
                        new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.11f, Rings = 10, RingSegments = 6 },
                        accent * 0.6f, accent, new Vector3(0, torsoY + 0.1f, -0.15f)));
                    // Floating conduit slivers
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T2_ConduitL" : "_T2_ConduitR",
                            new BoxMesh { Size = new Vector3(0.02f, 0.2f, 0.008f) },
                            accent * 0.4f, accent, new Vector3(side * 0.18f, torsoY, -0.12f)));
                    }
                    break;
                case BotFrameType.RustBucket:
                    // Carapace back extension — wider shell plate
                    root.AddChild(CreateMeshNode("_T2_Carapace",
                        new BoxMesh { Size = new Vector3(0.42f, 0.06f, 0.14f) },
                        dark, new Vector3(0, torsoY + 0.1f, 0.12f)));
                    // Carapace ribs
                    for (int i = -1; i <= 1; i++)
                    {
                        root.AddChild(CreateMeshNode($"_T2_CarapaceRib{i}",
                            new BoxMesh { Size = new Vector3(0.008f, 0.04f, 0.12f) },
                            metal.Lightened(0.08f), new Vector3(i * 0.12f, torsoY + 0.13f, 0.12f)));
                    }
                    break;
                case BotFrameType.NoiseBox:
                    // Larger speaker cone extensions flush with drum torso
                    for (float side = -1; side <= 1; side += 2)
                    {
                        var cone = CreateMeshNode(side < 0 ? "_T2_BigSpeakerL" : "_T2_BigSpeakerR",
                            new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.05f, Height = 0.05f, RadialSegments = 10 },
                            metal, new Vector3(side * 0.28f, torsoY, 0));
                        cone.RotateZ(Mathf.DegToRad(side * 90));
                        root.AddChild(cone);
                        // Speaker grille ring
                        root.AddChild(CreateMeshNode(side < 0 ? "_T2_GrilleRingL" : "_T2_GrilleRingR",
                            new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.08f, Rings = 8, RingSegments = 6 },
                            dark, new Vector3(side * 0.3f, torsoY, 0)));
                    }
                    break;
                case BotFrameType.Clunker:
                    // Bigger piston housings — visible hydraulics on shoulders
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T2_PistonL" : "_T2_PistonR",
                            new CylinderMesh { TopRadius = 0.055f, BottomRadius = 0.065f, Height = 0.18f, RadialSegments = 8 },
                            metal, new Vector3(side * 0.32f, torsoY + 0.2f, 0)));
                        // Piston rod visible below
                        root.AddChild(CreateMeshNode(side < 0 ? "_T2_PistonRodL" : "_T2_PistonRodR",
                            new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.1f, RadialSegments = 4 },
                            metal.Lightened(0.2f), new Vector3(side * 0.32f, torsoY + 0.06f, 0)));
                    }
                    // Belly plate reinforcement
                    root.AddChild(CreateMeshNode("_T2_BellyPlate",
                        new BoxMesh { Size = new Vector3(0.4f, 0.04f, 0.3f) },
                        panelDark, new Vector3(0, torsoY - 0.2f, 0)));
                    break;
            }
        }

        // Tier 3: Heavy — massive pauldrons, back reactor/exhaust, frame-specific mobility upgrades
        private static void AddTier3Pieces(Node3D root, BotFrameType frame, Color accent, Color metal, Color dark)
        {
            Color exhaust = new Color(0.2f, 0.2f, 0.22f);
            Color reactorGlow = accent.Lightened(0.2f);

            float shoulderY = frame switch
            {
                BotFrameType.Scrapheap => 0.72f,
                BotFrameType.SparkPlug => 1.08f,
                BotFrameType.RustBucket => 0.62f,
                BotFrameType.NoiseBox => 0.98f,
                BotFrameType.Clunker => 0.88f,
                _ => 0.88f
            };
            float shoulderX = frame switch
            {
                BotFrameType.Scrapheap => 0.4f,
                BotFrameType.SparkPlug => 0.2f,
                BotFrameType.RustBucket => 0.24f,
                _ => 0.3f
            };
            float torsoY = frame switch
            {
                BotFrameType.Scrapheap => 0.55f,
                BotFrameType.SparkPlug => 0.9f,
                BotFrameType.RustBucket => 0.52f,
                BotFrameType.NoiseBox => 0.85f,
                BotFrameType.Clunker => 0.75f,
                _ => 0.7f
            };

            // ── Massive pauldrons — layered, with accent ridges ──
            for (float side = -1; side <= 1; side += 2)
            {
                // Main pauldron body — thick angled plate
                var pauld = CreateMeshNode(side < 0 ? "_T3_PauldronL" : "_T3_PauldronR",
                    new BoxMesh { Size = new Vector3(0.2f, 0.07f, 0.18f) },
                    dark, new Vector3(side * (shoulderX + 0.06f), shoulderY + 0.04f, 0));
                pauld.RotationDegrees = new Vector3(0, 0, side * -18f);
                root.AddChild(pauld);

                // Pauldron upper ridge — accent colored
                root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T3_PauldRidgeL" : "_T3_PauldRidgeR",
                    new BoxMesh { Size = new Vector3(0.16f, 0.018f, 0.06f) },
                    accent, accent * 0.8f, new Vector3(side * (shoulderX + 0.06f), shoulderY + 0.09f, 0)));

                // Pauldron underplate — darker layer visible from below
                root.AddChild(CreateMeshNode(side < 0 ? "_T3_PauldUnderL" : "_T3_PauldUnderR",
                    new BoxMesh { Size = new Vector3(0.15f, 0.02f, 0.14f) },
                    dark.Darkened(0.1f), new Vector3(side * (shoulderX + 0.04f), shoulderY - 0.01f, 0)));

                // Bolt rivets on pauldron
                for (int r = 0; r < 2; r++)
                {
                    root.AddChild(CreateMeshNode($"_T3_PauldBolt{(side < 0 ? "L" : "R")}{r}",
                        new SphereMesh { Radius = 0.01f, Height = 0.018f, RadialSegments = 4, Rings = 2 },
                        metal.Lightened(0.2f), new Vector3(side * (shoulderX + 0.02f + r * 0.08f), shoulderY + 0.06f, -0.08f)));
                }
            }

            // ── Back reactor — power pack with exhaust pipes ──
            root.AddChild(CreateMeshNode("_T3_ReactorHousing",
                new BoxMesh { Size = new Vector3(0.2f, 0.22f, 0.1f) },
                dark, new Vector3(0, torsoY + 0.02f, 0.14f)));
            // Reactor core glow
            root.AddChild(CreateEmissiveMeshNode("_T3_ReactorCore",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.08f, RadialSegments = 8 },
                accent, reactorGlow, new Vector3(0, torsoY + 0.08f, 0.2f)));
            // Exhaust pipes — twin stacks rising from reactor
            for (float side = -1; side <= 1; side += 2)
            {
                root.AddChild(CreateMeshNode(side < 0 ? "_T3_ExhaustL" : "_T3_ExhaustR",
                    new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.032f, Height = 0.16f, RadialSegments = 6 },
                    exhaust, new Vector3(side * 0.08f, torsoY + 0.2f, 0.16f)));
                // Exhaust cap ring
                root.AddChild(CreateMeshNode(side < 0 ? "_T3_ExCapL" : "_T3_ExCapR",
                    new TorusMesh { InnerRadius = 0.02f, OuterRadius = 0.032f, Rings = 6, RingSegments = 4 },
                    exhaust, new Vector3(side * 0.08f, torsoY + 0.29f, 0.16f)));
            }
            // Reactor vent glow dots — 2x2 grid on back face
            for (int i = 0; i < 4; i++)
            {
                float vx = (i % 2 == 0 ? -1 : 1) * 0.05f;
                float vy = (i < 2 ? 1 : -1) * 0.04f;
                root.AddChild(CreateEmissiveMeshNode($"_T3_Vent{i}",
                    new BoxMesh { Size = new Vector3(0.025f, 0.025f, 0.012f) },
                    accent, accent, new Vector3(vx, torsoY + 0.02f + vy, 0.2f)));
            }

            // ── Hip armor — lower torso protection ──
            for (float side = -1; side <= 1; side += 2)
            {
                root.AddChild(CreateMeshNode(side < 0 ? "_T3_HipPlateL" : "_T3_HipPlateR",
                    new BoxMesh { Size = new Vector3(0.08f, 0.12f, 0.14f) },
                    dark.Lightened(0.03f), new Vector3(side * shoulderX * 0.85f, torsoY - 0.16f, 0)));
            }

            // ── Frame-specific heavy mobility upgrades ──
            switch (frame)
            {
                case BotFrameType.Scrapheap:
                    // Extended armored track skirts with reinforcement ribs
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T3_TrackSkirtL" : "_T3_TrackSkirtR",
                            new BoxMesh { Size = new Vector3(0.16f, 0.18f, 0.4f) },
                            dark, new Vector3(side * 0.44f, 0.16f, 0)));
                        // Reinforcement ribs on skirt
                        for (int r = -1; r <= 1; r++)
                        {
                            root.AddChild(CreateMeshNode($"_T3_SkirtRib{(side < 0 ? "L" : "R")}{r}",
                                new BoxMesh { Size = new Vector3(0.16f, 0.015f, 0.02f) },
                                metal, new Vector3(side * 0.44f, 0.16f + r * 0.06f, -0.2f)));
                        }
                    }
                    break;
                case BotFrameType.TinCan:
                    // Reinforced bumper and bigger wheel housings
                    root.AddChild(CreateMeshNode("_T3_BumperBar",
                        new BoxMesh { Size = new Vector3(0.5f, 0.05f, 0.05f) },
                        dark, new Vector3(0, 0.16f, -0.2f)));
                    root.AddChild(CreateEmissiveMeshNode("_T3_BumperStripe",
                        new BoxMesh { Size = new Vector3(0.4f, 0.015f, 0.008f) },
                        accent, accent * 0.6f, new Vector3(0, 0.18f, -0.22f)));
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T3_WheelHouseL" : "_T3_WheelHouseR",
                            new BoxMesh { Size = new Vector3(0.1f, 0.12f, 0.32f) },
                            dark, new Vector3(side * 0.3f, 0.13f, 0)));
                    }
                    break;
                case BotFrameType.SparkPlug:
                    // Secondary hover array — 4 stabilizer pods
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.DegToRad(45f + 90f * i);
                        root.AddChild(CreateEmissiveMeshNode($"_T3_HoverPod{i}",
                            new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.035f, Height = 0.025f, RadialSegments = 8 },
                            accent * 0.6f, accent, new Vector3(Mathf.Cos(angle) * 0.22f, 0.06f, Mathf.Sin(angle) * 0.22f)));
                        // Pod support arm
                        root.AddChild(CreateMeshNode($"_T3_PodArm{i}",
                            new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.008f, Height = 0.12f, RadialSegments = 4 },
                            metal, new Vector3(Mathf.Cos(angle) * 0.14f, 0.12f, Mathf.Sin(angle) * 0.14f)));
                    }
                    break;
                case BotFrameType.RustBucket:
                    // Auxiliary spider leg struts — 4 stabilizer limbs
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.DegToRad(90f * i);
                        var strut = CreateMeshNode($"_T3_AuxLeg{i}",
                            new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.018f, Height = 0.18f, RadialSegments = 5 },
                            metal, new Vector3(Mathf.Cos(angle) * 0.28f, 0.2f, Mathf.Sin(angle) * 0.28f));
                        strut.RotationDegrees = new Vector3(Mathf.Sin(angle) * 35f, 0, -Mathf.Cos(angle) * 35f);
                        root.AddChild(strut);
                        // Foot pad at end
                        root.AddChild(CreateMeshNode($"_T3_AuxFoot{i}",
                            new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.02f, Height = 0.012f, RadialSegments = 6 },
                            dark, new Vector3(Mathf.Cos(angle) * 0.38f, 0.06f, Mathf.Sin(angle) * 0.38f)));
                    }
                    break;
                case BotFrameType.NoiseBox:
                    // Subwoofer module — big bass unit on back
                    root.AddChild(CreateMeshNode("_T3_SubwooferBox",
                        new BoxMesh { Size = new Vector3(0.24f, 0.18f, 0.12f) },
                        dark, new Vector3(0, torsoY + 0.02f, 0.22f)));
                    root.AddChild(CreateEmissiveMeshNode("_T3_SubCone",
                        new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.035f, Height = 0.03f, RadialSegments = 10 },
                        accent * 0.6f, accent, new Vector3(0, torsoY + 0.02f, 0.29f)));
                    // Bass port tubes
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T3_BassPortL" : "_T3_BassPortR",
                            new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.06f, RadialSegments = 6 },
                            dark, new Vector3(side * 0.1f, torsoY + 0.02f, 0.27f)));
                    }
                    break;
                case BotFrameType.Clunker:
                    // Shin armor + stomper feet
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T3_ShinArmorL" : "_T3_ShinArmorR",
                            new BoxMesh { Size = new Vector3(0.08f, 0.2f, 0.09f) },
                            dark, new Vector3(side * 0.22f, 0.14f, -0.03f)));
                        // Stomper base — wide flat foot plate
                        root.AddChild(CreateMeshNode(side < 0 ? "_T3_StomperL" : "_T3_StomperR",
                            new BoxMesh { Size = new Vector3(0.13f, 0.035f, 0.16f) },
                            dark, new Vector3(side * 0.22f, 0.02f, 0)));
                        // Toe claw
                        root.AddChild(CreateMeshNode(side < 0 ? "_T3_ToeClawL" : "_T3_ToeClawR",
                            new BoxMesh { Size = new Vector3(0.04f, 0.03f, 0.04f) },
                            metal, new Vector3(side * 0.22f, 0.02f, -0.1f)));
                    }
                    break;
            }
        }

        // Tier 4: Evolved — dramatic class-fantasy pinnacle with emissive glow
        private static void AddTier4Pieces(Node3D root, BotFrameType frame, Color accent)
        {
            Color glow = accent * 1.4f;
            Color dark = new Color(0.16f, 0.16f, 0.18f);
            Color metal = new Color(0.32f, 0.32f, 0.34f);

            switch (frame)
            {
                case BotFrameType.Scrapheap:
                {
                    // ═══ SIEGE ENGINE — armored war rig with ram blade and exhaust stacks ═══

                    // Massive front ram blade — extends well beyond body width
                    root.AddChild(CreateEmissiveMeshNode("_T4_RamBlade",
                        new BoxMesh { Size = new Vector3(0.85f, 0.08f, 0.06f) },
                        accent, glow, new Vector3(0, 0.3f, -0.3f)));
                    // Ram blade angled wings
                    for (float side = -1; side <= 1; side += 2)
                    {
                        var wing = CreateEmissiveMeshNode(side < 0 ? "_T4_RamWingL" : "_T4_RamWingR",
                            new BoxMesh { Size = new Vector3(0.15f, 0.07f, 0.04f) },
                            accent, glow, new Vector3(side * 0.44f, 0.3f, -0.25f));
                        wing.RotationDegrees = new Vector3(0, side * -30f, 0);
                        root.AddChild(wing);
                    }
                    // Cowcatcher teeth
                    for (int i = -3; i <= 3; i++)
                    {
                        root.AddChild(CreateMeshNode($"_T4_Tooth{i}",
                            new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.02f, Height = 0.08f, RadialSegments = 4 },
                            metal, new Vector3(i * 0.1f, 0.22f, -0.32f)));
                    }
                    // Twin tall exhaust stacks with glow tips
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T4_StackL" : "_T4_StackR",
                            new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 0.32f, RadialSegments = 6 },
                            dark, new Vector3(side * 0.26f, 0.92f, 0.14f)));
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_StackGlowL" : "_T4_StackGlowR",
                            new TorusMesh { InnerRadius = 0.025f, OuterRadius = 0.04f, Rings = 6, RingSegments = 4 },
                            accent, glow, new Vector3(side * 0.26f, 1.09f, 0.14f)));
                    }
                    // Extended armored track pods — massive width extension
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T4_TrackPodL" : "_T4_TrackPodR",
                            new BoxMesh { Size = new Vector3(0.18f, 0.16f, 0.48f) },
                            dark, new Vector3(side * 0.48f, 0.1f, 0)));
                        // Track pod accent stripe
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_TrackStripeL" : "_T4_TrackStripeR",
                            new BoxMesh { Size = new Vector3(0.18f, 0.015f, 0.008f) },
                            accent, glow, new Vector3(side * 0.48f, 0.16f, -0.24f)));
                    }
                    // Frontal armor V-plate — sharp aggressive front profile
                    var vplate = CreateEmissiveMeshNode("_T4_VPlate",
                        new BoxMesh { Size = new Vector3(0.4f, 0.15f, 0.04f) },
                        accent, glow * 0.5f, new Vector3(0, 0.55f, -0.24f));
                    vplate.RotationDegrees = new Vector3(10f, 0, 0);
                    root.AddChild(vplate);
                    break;
                }
                case BotFrameType.TinCan:
                {
                    // ═══ COMMAND PLATFORM — tactical commander with visor and antenna ═══

                    // Wide tactical visor across head
                    root.AddChild(CreateEmissiveMeshNode("_T4_TacVisor",
                        new BoxMesh { Size = new Vector3(0.26f, 0.035f, 0.04f) },
                        accent, glow, new Vector3(0, 1.22f, -0.1f)));
                    // Tall antenna mast with comm dish
                    root.AddChild(CreateMeshNode("_T4_AntennaMast",
                        new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.018f, Height = 0.28f, RadialSegments = 4 },
                        metal, new Vector3(0, 1.45f, 0)));
                    root.AddChild(CreateEmissiveMeshNode("_T4_CommDish",
                        new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.015f, Height = 0.015f, RadialSegments = 8 },
                        accent, glow, new Vector3(0, 1.6f, 0)));
                    // Tactical side fins — extend profile
                    for (float side = -1; side <= 1; side += 2)
                    {
                        var fin = CreateEmissiveMeshNode(side < 0 ? "_T4_TacFinL" : "_T4_TacFinR",
                            new BoxMesh { Size = new Vector3(0.025f, 0.18f, 0.12f) },
                            accent, glow * 0.5f, new Vector3(side * 0.2f, 1.02f, 0.04f));
                        fin.RotationDegrees = new Vector3(0, 0, side * -8f);
                        root.AddChild(fin);
                        // Fin accent line
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_FinLineL" : "_T4_FinLineR",
                            new BoxMesh { Size = new Vector3(0.008f, 0.14f, 0.008f) },
                            accent, glow, new Vector3(side * 0.22f, 1.02f, -0.02f)));
                    }
                    // Armored wheel pod extensions
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T4_WheelPodL" : "_T4_WheelPodR",
                            new BoxMesh { Size = new Vector3(0.12f, 0.14f, 0.34f) },
                            dark, new Vector3(side * 0.34f, 0.1f, 0)));
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_PodGlowL" : "_T4_PodGlowR",
                            new BoxMesh { Size = new Vector3(0.12f, 0.015f, 0.008f) },
                            accent, glow, new Vector3(side * 0.34f, 0.15f, -0.17f)));
                    }
                    // Rear tactical pack — extended storage
                    root.AddChild(CreateMeshNode("_T4_TacPack",
                        new BoxMesh { Size = new Vector3(0.28f, 0.16f, 0.08f) },
                        dark, new Vector3(0, 0.68f, 0.2f)));
                    root.AddChild(CreateEmissiveMeshNode("_T4_PackLight",
                        new SphereMesh { Radius = 0.015f, Height = 0.025f, RadialSegments = 5, Rings = 3 },
                        accent, glow, new Vector3(0, 0.72f, 0.25f)));
                    break;
                }
                case BotFrameType.SparkPlug:
                {
                    // ═══ ARC ASCENDANT — floating energy crown, conduit wings, overcharged hover ═══

                    // Energy crown — floating ring above head
                    root.AddChild(CreateEmissiveMeshNode("_T4_CrownRing",
                        new TorusMesh { InnerRadius = 0.14f, OuterRadius = 0.2f, Rings = 16, RingSegments = 8 },
                        accent, glow, new Vector3(0, 1.72f, 0)));
                    // Crown support pylons — 4 thin energy beams connecting crown to head
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.DegToRad(45f + 90f * i);
                        root.AddChild(CreateEmissiveMeshNode($"_T4_CrownBeam{i}",
                            new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.008f, Height = 0.14f, RadialSegments = 4 },
                            accent * 0.5f, glow, new Vector3(Mathf.Cos(angle) * 0.12f, 1.64f, Mathf.Sin(angle) * 0.12f)));
                    }
                    // Lightning rod spires — 4 tall rods from crown ring
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.DegToRad(90f * i);
                        root.AddChild(CreateEmissiveMeshNode($"_T4_Spire{i}",
                            new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.015f, Height = 0.2f, RadialSegments = 4 },
                            accent, glow, new Vector3(Mathf.Cos(angle) * 0.16f, 1.84f, Mathf.Sin(angle) * 0.16f)));
                        // Spire tip glow
                        root.AddChild(CreateEmissiveMeshNode($"_T4_SpireTip{i}",
                            new SphereMesh { Radius = 0.012f, Height = 0.02f, RadialSegments = 5, Rings = 3 },
                            accent, glow * 1.5f, new Vector3(Mathf.Cos(angle) * 0.16f, 1.95f, Mathf.Sin(angle) * 0.16f)));
                    }
                    // Floating conduit wings — energy panels flanking body
                    for (float side = -1; side <= 1; side += 2)
                    {
                        var wing = CreateEmissiveMeshNode(side < 0 ? "_T4_ConduitWingL" : "_T4_ConduitWingR",
                            new BoxMesh { Size = new Vector3(0.2f, 0.025f, 0.1f) },
                            accent, glow * 0.6f, new Vector3(side * 0.26f, 1.02f, 0.04f));
                        wing.RotationDegrees = new Vector3(0, 0, side * -20f);
                        root.AddChild(wing);
                        // Wing support strut
                        root.AddChild(CreateMeshNode(side < 0 ? "_T4_WingStrutL" : "_T4_WingStrutR",
                            new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.008f, Height = 0.1f, RadialSegments = 4 },
                            metal, new Vector3(side * 0.2f, 0.98f, 0.04f)));
                    }
                    // Overcharged hover array — larger ring + 4 big pads
                    root.AddChild(CreateEmissiveMeshNode("_T4_HoverMegaRing",
                        new TorusMesh { InnerRadius = 0.24f, OuterRadius = 0.3f, Rings = 16, RingSegments = 8 },
                        accent, glow, new Vector3(0, 0.04f, 0)));
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.DegToRad(45f + 90f * i);
                        root.AddChild(CreateEmissiveMeshNode($"_T4_MegaPad{i}",
                            new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.04f, Height = 0.02f, RadialSegments = 8 },
                            accent, glow, new Vector3(Mathf.Cos(angle) * 0.26f, 0.02f, Mathf.Sin(angle) * 0.26f)));
                    }
                    break;
                }
                case BotFrameType.RustBucket:
                {
                    // ═══ PREDATOR CARAPACE — wide wing plates, sensor dome, extra legs ═══

                    // Folding wing plates — aggressive spread like insect wings
                    for (float side = -1; side <= 1; side += 2)
                    {
                        var wing = CreateEmissiveMeshNode(side < 0 ? "_T4_WingL" : "_T4_WingR",
                            new BoxMesh { Size = new Vector3(0.28f, 0.025f, 0.18f) },
                            accent, glow * 0.5f, new Vector3(side * 0.26f, 0.66f, 0.04f));
                        wing.RotationDegrees = new Vector3(-5f, 0, side * -25f);
                        root.AddChild(wing);
                        // Wing rib
                        root.AddChild(CreateMeshNode(side < 0 ? "_T4_WingRibL" : "_T4_WingRibR",
                            new BoxMesh { Size = new Vector3(0.24f, 0.012f, 0.015f) },
                            metal, new Vector3(side * 0.28f, 0.68f, -0.04f)));
                        // Wing tip glow
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_WingTipL" : "_T4_WingTipR",
                            new SphereMesh { Radius = 0.015f, Height = 0.025f, RadialSegments = 5, Rings = 3 },
                            accent, glow, new Vector3(side * 0.45f, 0.72f, 0)));
                    }
                    // Enhanced sensor dome — larger, with ring
                    root.AddChild(CreateEmissiveMeshNode("_T4_SensorDome",
                        new SphereMesh { Radius = 0.06f, Height = 0.07f, RadialSegments = 10, Rings = 5 },
                        accent, glow, new Vector3(0, 0.84f, -0.06f)));
                    root.AddChild(CreateEmissiveMeshNode("_T4_SensorRing",
                        new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.06f, Rings = 8, RingSegments = 6 },
                        accent * 0.5f, glow * 0.6f, new Vector3(0, 0.82f, -0.06f)));
                    // Extra rear spider legs — wider stance, longer reach
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1f : 1f;
                        // Upper leg segment
                        var upperLeg = CreateEmissiveMeshNode(i == 0 ? "_T4_RearLegUpperL" : "_T4_RearLegUpperR",
                            new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.02f, Height = 0.16f, RadialSegments = 5 },
                            accent, glow * 0.5f, new Vector3(side * 0.28f, 0.3f, 0.16f));
                        upperLeg.RotationDegrees = new Vector3(-25f, 0, side * -35f);
                        root.AddChild(upperLeg);
                        // Lower leg segment
                        var lowerLeg = CreateMeshNode(i == 0 ? "_T4_RearLegLowerL" : "_T4_RearLegLowerR",
                            new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.015f, Height = 0.14f, RadialSegments = 5 },
                            metal, new Vector3(side * 0.44f, 0.1f, 0.28f));
                        lowerLeg.RotationDegrees = new Vector3(15f, 0, side * -10f);
                        root.AddChild(lowerLeg);
                        // Foot pad
                        root.AddChild(CreateEmissiveMeshNode(i == 0 ? "_T4_FootPadL" : "_T4_FootPadR",
                            new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.025f, Height = 0.012f, RadialSegments = 6 },
                            accent, glow, new Vector3(side * 0.5f, 0.04f, 0.32f)));
                    }
                    // Stealth panel overlays — thin dark plates on body
                    root.AddChild(CreateMeshNode("_T4_StealthPanelFront",
                        new BoxMesh { Size = new Vector3(0.38f, 0.02f, 0.12f) },
                        dark, new Vector3(0, 0.56f, -0.2f)));
                    break;
                }
                case BotFrameType.NoiseBox:
                {
                    // ═══ RESONANCE TITAN — massive horn array, bass cannon, amplifier wings ═══

                    // Twin horn speakers — tall cones projecting upward from head area
                    for (float side = -1; side <= 1; side += 2)
                    {
                        var horn = CreateEmissiveMeshNode(side < 0 ? "_T4_HornL" : "_T4_HornR",
                            new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.025f, Height = 0.2f, RadialSegments = 8 },
                            accent, glow, new Vector3(side * 0.2f, 1.18f, -0.04f));
                        horn.RotationDegrees = new Vector3(0, 0, side * 10f);
                        root.AddChild(horn);
                        // Horn mouth ring
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_HornRingL" : "_T4_HornRingR",
                            new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.085f, Rings = 8, RingSegments = 6 },
                            accent, glow, new Vector3(side * 0.22f, 1.29f, -0.04f)));
                    }
                    // Front bass cannon — large forward-facing speaker
                    root.AddChild(CreateMeshNode("_T4_BassHousing",
                        new BoxMesh { Size = new Vector3(0.28f, 0.22f, 0.05f) },
                        dark, new Vector3(0, 0.85f, -0.23f)));
                    root.AddChild(CreateEmissiveMeshNode("_T4_BassCone",
                        new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.06f, Height = 0.04f, RadialSegments = 12 },
                        accent, glow, new Vector3(0, 0.85f, -0.27f)));
                    root.AddChild(CreateEmissiveMeshNode("_T4_BassDustCap",
                        new SphereMesh { Radius = 0.03f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                        accent, glow * 1.5f, new Vector3(0, 0.85f, -0.3f)));
                    // Amplifier wing panels — extend silhouette sideways
                    for (float side = -1; side <= 1; side += 2)
                    {
                        var amp = CreateEmissiveMeshNode(side < 0 ? "_T4_AmpWingL" : "_T4_AmpWingR",
                            new BoxMesh { Size = new Vector3(0.16f, 0.22f, 0.025f) },
                            accent, glow * 0.4f, new Vector3(side * 0.34f, 0.88f, 0.06f));
                        amp.RotationDegrees = new Vector3(0, side * -15f, side * -5f);
                        root.AddChild(amp);
                        // Amp panel grille lines
                        for (int g = -1; g <= 1; g++)
                        {
                            root.AddChild(CreateMeshNode($"_T4_AmpGrille{(side < 0 ? "L" : "R")}{g}",
                                new BoxMesh { Size = new Vector3(0.12f, 0.012f, 0.008f) },
                                metal, new Vector3(side * 0.35f, 0.88f + g * 0.06f, 0.08f)));
                        }
                    }
                    // Stabilizer mega-ring — larger glow ring around ball
                    root.AddChild(CreateEmissiveMeshNode("_T4_StabRing",
                        new TorusMesh { InnerRadius = 0.24f, OuterRadius = 0.3f, Rings = 16, RingSegments = 8 },
                        accent, glow, new Vector3(0, 0.12f, 0)));
                    // Resonance field markers — 4 floating orbs
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = Mathf.DegToRad(45f + 90f * i);
                        root.AddChild(CreateEmissiveMeshNode($"_T4_ResOrb{i}",
                            new SphereMesh { Radius = 0.02f, Height = 0.035f, RadialSegments = 6, Rings = 3 },
                            accent, glow, new Vector3(Mathf.Cos(angle) * 0.32f, 0.85f, Mathf.Sin(angle) * 0.32f)));
                    }
                    break;
                }
                case BotFrameType.Clunker:
                {
                    // ═══ BERSERKER JUGGERNAUT — massive fists, jaw plate, power plant, stompers ═══

                    // Oversized knuckle guards — huge gauntlets
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_KnuckleL" : "_T4_KnuckleR",
                            new BoxMesh { Size = new Vector3(0.14f, 0.07f, 0.09f) },
                            accent, glow * 0.6f, new Vector3(side * 0.36f, 0.44f, -0.1f)));
                        // Knuckle spikes
                        for (int s = 0; s < 3; s++)
                        {
                            root.AddChild(CreateEmissiveMeshNode($"_T4_Spike{(side < 0 ? "L" : "R")}{s}",
                                new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.012f, Height = 0.05f, RadialSegments = 4 },
                                accent, glow, new Vector3(side * (0.32f + s * 0.04f), 0.44f, -0.16f)));
                        }
                        // Forearm armor wrap
                        root.AddChild(CreateMeshNode(side < 0 ? "_T4_ForearmWrapL" : "_T4_ForearmWrapR",
                            new BoxMesh { Size = new Vector3(0.09f, 0.14f, 0.08f) },
                            dark, new Vector3(side * 0.34f, 0.52f, -0.02f)));
                    }
                    // Reinforced jaw plate — jutting aggressive chin
                    root.AddChild(CreateEmissiveMeshNode("_T4_JawPlate",
                        new BoxMesh { Size = new Vector3(0.18f, 0.05f, 0.08f) },
                        accent, glow * 0.6f, new Vector3(0, 1.08f, -0.12f)));
                    root.AddChild(CreateMeshNode("_T4_JawBolts",
                        new SphereMesh { Radius = 0.01f, Height = 0.016f, RadialSegments = 4, Rings = 2 },
                        metal, new Vector3(-0.06f, 1.08f, -0.17f)));
                    root.AddChild(CreateMeshNode("_T4_JawBolts2",
                        new SphereMesh { Radius = 0.01f, Height = 0.016f, RadialSegments = 4, Rings = 2 },
                        metal, new Vector3(0.06f, 1.08f, -0.17f)));
                    // Massive back power pistons — hydraulic power plant
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_PowerPistonL" : "_T4_PowerPistonR",
                            new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 0.28f, RadialSegments = 8 },
                            accent, glow * 0.5f, new Vector3(side * 0.14f, 0.82f, 0.18f)));
                        // Piston housing ring
                        root.AddChild(CreateMeshNode(side < 0 ? "_T4_PistonRingL" : "_T4_PistonRingR",
                            new TorusMesh { InnerRadius = 0.025f, OuterRadius = 0.04f, Rings = 6, RingSegments = 4 },
                            metal, new Vector3(side * 0.14f, 0.95f, 0.18f)));
                    }
                    // Wider stomper feet — massive treads
                    for (float side = -1; side <= 1; side += 2)
                    {
                        root.AddChild(CreateMeshNode(side < 0 ? "_T4_StomperWideL" : "_T4_StomperWideR",
                            new BoxMesh { Size = new Vector3(0.16f, 0.045f, 0.2f) },
                            dark, new Vector3(side * 0.24f, 0.01f, 0)));
                        // Toe claws — 2 per foot
                        for (int t = 0; t < 2; t++)
                        {
                            root.AddChild(CreateMeshNode($"_T4_ToeClaw{(side < 0 ? "L" : "R")}{t}",
                                new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.012f, Height = 0.04f, RadialSegments = 4 },
                                metal, new Vector3(side * 0.24f + (t - 0.5f) * 0.06f, 0.01f, -0.12f)));
                        }
                        // Foot glow strip
                        root.AddChild(CreateEmissiveMeshNode(side < 0 ? "_T4_FootGlowL" : "_T4_FootGlowR",
                            new BoxMesh { Size = new Vector3(0.14f, 0.008f, 0.008f) },
                            accent, glow, new Vector3(side * 0.24f, 0.04f, -0.1f)));
                    }
                    break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  GROWTH PIECE → BODY PIVOT MAPPING (for animation attachment)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// All valid body pivot names that growth pieces can be parented to.
        /// </summary>
        public static readonly string[] BodyPivotNames =
        {
            "Body", "Head", "Torso", "LeftArm", "RightArm", "LeftLeg", "RightLeg",
            "LeftElbow", "RightElbow", "LeftHand", "RightHand",
            "LeftKnee", "RightKnee", "LeftAnkle", "RightAnkle", "Weapon"
        };

        /// <summary>
        /// Determine which body pivot a growth piece should be parented to based on its name.
        /// Returns "Body" (the root) for pieces that don't clearly belong to a limb.
        /// </summary>
        public static string GetGrowthPieceParent(string pieceName)
        {
            // Strip tier prefix to get the semantic name: _T1_LeftShoulder → LeftShoulder
            string name = pieceName;
            if (name.StartsWith("_T") && name.Length > 4 && name[3] == '_')
                name = name.Substring(4);

            // ── Head pieces ──
            if (name.Contains("Head") || name.Contains("Crest") || name.Contains("Crown")
                || name.Contains("Horn") || name.Contains("Visor") || name.Contains("Antenna")
                || name.Contains("CommDish") || name.Contains("TacVisor") || name.Contains("Jaw")
                || name.Contains("Sensor") || name.Contains("CrestTip"))
                return "Head";

            // ── Torso / chest / back pieces ──
            if (name.Contains("Chest") || name.Contains("Back") || name.Contains("Reactor")
                || name.Contains("Flank") || name.Contains("Belly") || name.Contains("Carapace")
                || name.Contains("TacPack") || name.Contains("PackLight") || name.Contains("Stealth")
                || name.Contains("BassHousing") || name.Contains("BassCone") || name.Contains("BassDust")
                || name.Contains("SubwooferBox") || name.Contains("SubCone")
                || name.Contains("Dozer") || name.Contains("Bumper") || name.Contains("VPlate"))
                return "Torso";

            // ── Left arm pieces ──
            if (name.Contains("LeftShoulder") || name.Contains("ShoulderTrimL")
                || name.Contains("LeftGauntlet") || name.Contains("GauntPipeL")
                || name.Contains("PauldronL") || name.Contains("PauldRidgeL") || name.Contains("PauldUnderL")
                || name.Contains("KnuckleL") || name.Contains("ForearmWrapL")
                || name.Contains("PowerPistonL") || name.Contains("PistonRingL")
                || name.Contains("ConduitWingL") || name.Contains("WingStrutL")
                || name.Contains("AmpWingL"))
                return "LeftArm";

            // ── Right arm pieces ──
            if (name.Contains("RightShoulder") || name.Contains("ShoulderTrimR")
                || name.Contains("RightGauntlet") || name.Contains("GauntPipeR")
                || name.Contains("PauldronR") || name.Contains("PauldRidgeR") || name.Contains("PauldUnderR")
                || name.Contains("KnuckleR") || name.Contains("ForearmWrapR")
                || name.Contains("PowerPistonR") || name.Contains("PistonRingR")
                || name.Contains("ConduitWingR") || name.Contains("WingStrutR")
                || name.Contains("AmpWingR"))
                return "RightArm";

            // ── Left leg pieces ──
            if (name.Contains("TrackFenderL") || name.Contains("FenderLipL")
                || name.Contains("WheelGuardL") || name.Contains("MudFlapL")
                || name.Contains("ThighArmorL") || name.Contains("KneeGuardL")
                || name.Contains("HipPlateL") || name.Contains("TrackSkirtL")
                || name.Contains("WheelHouseL") || name.Contains("WheelPodL") || name.Contains("PodGlowL")
                || name.Contains("ShinArmorL") || name.Contains("StomperL") || name.Contains("ToeClawL")
                || name.Contains("FootGlowL") || name.Contains("StomperWideL") || name.Contains("FootPadL")
                || name.Contains("TrackPodL") || name.Contains("TrackStripeL")
                || name.Contains("RearLegUpperL") || name.Contains("RearLegLowerL")
                || name.Contains("TacPodL") || name.Contains("PodLensL")
                || name.Contains("TacFinL") || name.Contains("FinLineL")
                || name.Contains("StabFinL") || name.Contains("BassPortL")
                || name.Contains("PistonL") || name.Contains("PistonRodL")
                || name.Contains("BigSpeakerL") || name.Contains("GrilleRingL"))
                return "LeftLeg";

            // ── Right leg pieces ──
            if (name.Contains("TrackFenderR") || name.Contains("FenderLipR")
                || name.Contains("WheelGuardR") || name.Contains("MudFlapR")
                || name.Contains("ThighArmorR") || name.Contains("KneeGuardR")
                || name.Contains("HipPlateR") || name.Contains("TrackSkirtR")
                || name.Contains("WheelHouseR") || name.Contains("WheelPodR") || name.Contains("PodGlowR")
                || name.Contains("ShinArmorR") || name.Contains("StomperR") || name.Contains("ToeClawR")
                || name.Contains("FootGlowR") || name.Contains("StomperWideR") || name.Contains("FootPadR")
                || name.Contains("TrackPodR") || name.Contains("TrackStripeR")
                || name.Contains("RearLegUpperR") || name.Contains("RearLegLowerR")
                || name.Contains("TacPodR") || name.Contains("PodLensR")
                || name.Contains("TacFinR") || name.Contains("FinLineR")
                || name.Contains("StabFinR") || name.Contains("BassPortR")
                || name.Contains("PistonR") || name.Contains("PistonRodR")
                || name.Contains("BigSpeakerR") || name.Contains("GrilleRingR"))
                return "RightLeg";

            // ── Generic L/R fallbacks (pieces with L/R suffix not caught above) ──
            if (name.EndsWith("L") || name.Contains("Left"))
            {
                // Shoulder-area → arm, leg-area → leg, else torso
                if (name.Contains("Shoulder") || name.Contains("Gaunt") || name.Contains("Pauld")
                    || name.Contains("Wing") || name.Contains("Arm"))
                    return "LeftArm";
                if (name.Contains("Track") || name.Contains("Wheel") || name.Contains("Leg")
                    || name.Contains("Shin") || name.Contains("Stomp") || name.Contains("Foot")
                    || name.Contains("Hip") || name.Contains("Knee") || name.Contains("Ankle"))
                    return "LeftLeg";
            }
            if (name.EndsWith("R") || name.Contains("Right"))
            {
                if (name.Contains("Shoulder") || name.Contains("Gaunt") || name.Contains("Pauld")
                    || name.Contains("Wing") || name.Contains("Arm"))
                    return "RightArm";
                if (name.Contains("Track") || name.Contains("Wheel") || name.Contains("Leg")
                    || name.Contains("Shin") || name.Contains("Stomp") || name.Contains("Foot")
                    || name.Contains("Hip") || name.Contains("Knee") || name.Contains("Ankle"))
                    return "RightLeg";
            }

            // ── Center pieces that stay on body root ──
            // HoverRing, BallGuard, StabRing, ConduitRing, RamBlade, RamWing, ExhaustL/R, etc.
            // Stack pieces are exhaust pipes on the back → torso
            if (name.Contains("Stack") || name.Contains("Exhaust") || name.Contains("ExCap"))
                return "Torso";

            // Rivet rows stay with their shoulder context but are named generically
            if (name.Contains("Rivet"))
            {
                if (name.Contains("L")) return "LeftArm";
                if (name.Contains("R")) return "RightArm";
            }

            // Default: parent to body root (won't animate independently, but stays in place)
            return "Body";
        }

        /// <summary>
        /// Reparent growth pieces from the flat Growth_{tier} root onto the correct
        /// animated body pivots so they move with the body during animations.
        /// Accepts an optional parentOverrides dictionary (piece name → pivot name) for user overrides.
        /// </summary>
        public static void AttachGrowthToSkeleton(Node3D body, Node3D growthRoot,
            System.Collections.Generic.Dictionary<string, string> parentOverrides = null)
        {
            if (body == null || growthRoot == null) return;

            // Collect all growth pieces first (can't modify children while iterating)
            var pieces = new System.Collections.Generic.List<Node3D>();
            foreach (var child in growthRoot.GetChildren())
            {
                if (child is Node3D piece)
                    pieces.Add(piece);
            }

            foreach (var piece in pieces)
            {
                string pieceName = piece.Name.ToString();

                // Determine target pivot
                string targetPivot = parentOverrides != null
                    && parentOverrides.TryGetValue(pieceName, out var overridePivot)
                    ? overridePivot
                    : GetGrowthPieceParent(pieceName);

                // "Body" means keep on body root directly
                Node3D target;
                if (targetPivot == "Body")
                {
                    target = body;
                }
                else
                {
                    target = FindPartRecursive(body, targetPivot);
                    if (target == null) target = body; // fallback
                }

                // Convert position from body-root-local space to target-local space.
                // Growth pieces are authored with positions relative to the body root.
                // The growthRoot itself sits at (0,0,0) relative to body, so piece.Position
                // is already in body-root space. We need to subtract the target pivot's
                // position (also in body-root space) to get the offset relative to the pivot.
                // This avoids ToGlobal/ToLocal which require valid global transforms.
                Vector3 bodySpacePos = piece.Position;
                Vector3 localPos = bodySpacePos;
                if (target != body)
                {
                    // Walk up from target to body to accumulate the target's position in body space
                    Vector3 targetPosInBodySpace = GetPositionRelativeTo(target, body);
                    localPos = bodySpacePos - targetPosInBodySpace;
                }

                // Reparent
                growthRoot.RemoveChild(piece);
                target.AddChild(piece);
                piece.Position = localPos;

                if (target != body)
                    GD.Print($"[GrowthAttach] {pieceName} → {target.Name} (offset {localPos:F2})");
            }

            int attached = pieces.Count;
            GD.Print($"[GrowthAttach] Reparented {attached} growth pieces onto body pivots");

            // Remove empty growth root
            if (growthRoot.GetChildCount() == 0)
            {
                growthRoot.GetParent()?.RemoveChild(growthRoot);
                growthRoot.QueueFree();
            }
        }

        /// <summary>
        /// Walk up the hierarchy from child to ancestor, accumulating local positions.
        /// Returns the child's position expressed in the ancestor's local space.
        /// Works without needing valid global transforms (pure local math).
        /// </summary>
        public static Vector3 GetPositionRelativeToPublic(Node3D child, Node3D ancestor)
            => GetPositionRelativeTo(child, ancestor);

        private static Vector3 GetPositionRelativeTo(Node3D child, Node3D ancestor)
        {
            Vector3 pos = Vector3.Zero;
            Node3D current = child;
            while (current != null && current != ancestor)
            {
                pos += current.Position;
                current = current.GetParent() as Node3D;
            }
            return pos;
        }

        private static Node3D FindPartRecursive(Node parent, string name)
        {
            if (parent == null) return null;
            foreach (var child in parent.GetChildren())
            {
                if (child is Node3D n3d && n3d.Name.ToString() == name)
                    return n3d;
                var found = FindPartRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Get the growth tier for a given player level.
        /// </summary>
        public static GrowthTier GetGrowthTierForLevel(int level) => level switch
        {
            < 5 => GrowthTier.Base,
            < 10 => GrowthTier.Plated,
            < 15 => GrowthTier.Armored,
            < 20 => GrowthTier.Heavy,
            _ => GrowthTier.Evolved
        };

        // ══════════════════════════════════════════════════════════════════
        //  WEAPON MOUNT POINTS — multiple mount locations per frame
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Get the position for a given weapon mount type on a specific frame.
        /// </summary>
        public static Vector3 GetMountPosition(BotFrameType frame, WeaponMountType mountType)
        {
            return mountType switch
            {
                WeaponMountType.ShoulderMount => GetShoulderMountPos(frame),
                WeaponMountType.BackMount => GetBackMountPos(frame),
                WeaponMountType.ArmIntegrated => GetArmIntegratedPos(frame),
                _ => GetHandMountPos(frame) // HandHeld
            };
        }

        private static Vector3 GetHandMountPos(BotFrameType frame) => frame switch
        {
            BotFrameType.Scrapheap => new Vector3(0.52f, 0.7f, -0.2f),
            BotFrameType.TinCan => new Vector3(0.42f, 0.85f, -0.15f),
            BotFrameType.SparkPlug => new Vector3(0.32f, 1.05f, -0.12f),
            BotFrameType.RustBucket => new Vector3(0.34f, 0.62f, -0.18f),
            BotFrameType.NoiseBox => new Vector3(0.4f, 1.0f, -0.15f),
            BotFrameType.Clunker => new Vector3(0.46f, 0.85f, -0.18f),
            _ => new Vector3(0.42f, 0.85f, -0.15f)
        };

        private static Vector3 GetShoulderMountPos(BotFrameType frame) => frame switch
        {
            BotFrameType.Scrapheap => new Vector3(0.3f, 0.82f, -0.05f),
            BotFrameType.TinCan => new Vector3(0.22f, 1.0f, -0.02f),
            BotFrameType.SparkPlug => new Vector3(0.2f, 1.2f, 0f),
            BotFrameType.RustBucket => new Vector3(0.22f, 0.72f, -0.04f),
            BotFrameType.NoiseBox => new Vector3(0.25f, 1.1f, -0.02f),
            BotFrameType.Clunker => new Vector3(0.26f, 1.0f, -0.04f),
            _ => new Vector3(0.22f, 1.0f, -0.02f)
        };

        private static Vector3 GetBackMountPos(BotFrameType frame) => frame switch
        {
            BotFrameType.Scrapheap => new Vector3(0.1f, 0.75f, 0.18f),
            BotFrameType.TinCan => new Vector3(0.08f, 0.9f, 0.15f),
            BotFrameType.SparkPlug => new Vector3(0.06f, 1.1f, 0.12f),
            BotFrameType.RustBucket => new Vector3(0.08f, 0.6f, 0.14f),
            BotFrameType.NoiseBox => new Vector3(0.08f, 0.95f, 0.13f),
            BotFrameType.Clunker => new Vector3(0.1f, 0.85f, 0.16f),
            _ => new Vector3(0.08f, 0.9f, 0.15f)
        };

        private static Vector3 GetArmIntegratedPos(BotFrameType frame) => frame switch
        {
            BotFrameType.Scrapheap => new Vector3(0.45f, 0.5f, -0.15f),
            BotFrameType.TinCan => new Vector3(0.38f, 0.65f, -0.12f),
            BotFrameType.SparkPlug => new Vector3(0.3f, 0.85f, -0.1f),
            BotFrameType.RustBucket => new Vector3(0.3f, 0.45f, -0.14f),
            BotFrameType.NoiseBox => new Vector3(0.35f, 0.8f, -0.12f),
            BotFrameType.Clunker => new Vector3(0.4f, 0.6f, -0.14f),
            _ => new Vector3(0.38f, 0.65f, -0.12f)
        };

        /// <summary>
        /// Get weapon rotation for a mount type (degrees).
        /// Shoulder/back mounts angle the weapon differently than hand-held.
        /// </summary>
        public static Vector3 GetMountRotation(WeaponMountType mountType) => mountType switch
        {
            WeaponMountType.ShoulderMount => new Vector3(-15f, 0f, 0f),  // Tilted forward
            WeaponMountType.BackMount => new Vector3(-30f, 15f, 0f),      // Over-the-shoulder angle
            WeaponMountType.ArmIntegrated => new Vector3(0f, 0f, 0f),     // Aligned with arm
            _ => Vector3.Zero
        };

        /// <summary>
        /// Get weapon scale for a mount type.
        /// Shoulder/back mounts are slightly larger, arm-integrated matches arm size.
        /// </summary>
        public static float GetMountScale(WeaponMountType mountType) => mountType switch
        {
            WeaponMountType.ShoulderMount => 1.1f,
            WeaponMountType.BackMount => 1.0f,
            WeaponMountType.ArmIntegrated => 0.85f,
            _ => 1.0f
        };

        // ══════════════════════════════════════════════════════════════════
        //  GRAFT VISUALS — socketed salvage cores add visible body mods
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Build a visual attachment for a socketed salvage core.
        /// Returns null if no visual is defined for this core.
        /// Attach as child of the player body root.
        /// </summary>
        public static Node3D BuildGraftVisual(string coreId, BotFrameType frame)
        {
            return coreId switch
            {
                // Rare
                "core_fortified" => BuildGraftHeartstone(frame),
                "core_capacitor" => BuildGraftNerveCluster(frame),
                "core_precision" => BuildGraftStalkersEye(frame),
                "core_accelerator" => BuildGraftSinewBundle(frame),
                // Epic
                "core_vampiric" => BuildGraftLeechGland(frame),
                "core_scavenger" => BuildGraftBeetleColony(frame),
                "core_prismatic" => BuildGraftChromaticTumor(frame),
                "core_volatile" => BuildGraftBloatSac(frame),
                "core_singularity" => BuildGraftGravityParasite(frame),
                // Legendary
                "core_cross_wired_tank" => BuildGraftCarapace(frame),
                "core_cross_wired_caster" => BuildGraftOverloadedSynapse(frame),
                "core_cross_wired_brawler" => BuildGraftAdrenalGland(frame),
                "core_cross_wired_agile" => BuildGraftHollowBone(frame),
                "core_amplifier_shield_bash" => BuildGraftSkullCap(frame),
                "core_amplifier_fireball" => BuildGraftMagmaGland(frame),
                "core_amplifier_chain_shot" => BuildGraftHydraStrand(frame),
                // Mythic
                "core_mythic_immortal_engine" => BuildGraftImmortalEngine(frame),
                "core_mythic_devourer" => BuildGraftDevourer(frame),
                "core_mythic_neural_hijack" => BuildGraftNeuralHijack(frame),
                "core_mythic_time_loop" => BuildGraftParadoxGland(frame),
                "core_mythic_storm_caller" => BuildGraftStormCore(frame),
                "core_mythic_void_heart" => BuildGraftVoidHeart(frame),
                "core_mythic_echo_chamber" => BuildGraftEchoChamber(frame),
                "core_mythic_blood_economy" => BuildGraftHemorrhageEngine(frame),
                _ => null
            };
        }

        private static float GraftTorsoY(BotFrameType f) => f switch
        {
            BotFrameType.Scrapheap => 0.5f, BotFrameType.SparkPlug => 0.8f,
            BotFrameType.RustBucket => 0.4f, BotFrameType.NoiseBox => 0.7f,
            BotFrameType.Clunker => 0.6f, _ => 0.65f
        };

        private static float GraftHeadY(BotFrameType f) => f switch
        {
            BotFrameType.Scrapheap => 0.85f, BotFrameType.SparkPlug => 1.25f,
            BotFrameType.RustBucket => 0.7f, BotFrameType.NoiseBox => 1.15f,
            BotFrameType.Clunker => 1.0f, _ => 1.0f
        };

        // ── Rare grafts (subtle) ──

        private static Node3D BuildGraftHeartstone(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_Heartstone" };
            float y = GraftTorsoY(f);
            root.AddChild(CreateEmissiveMeshNode("_Heart",
                new SphereMesh { Radius = 0.05f, Height = 0.08f, RadialSegments = 8, Rings = 4 },
                new Color(0.8f, 0.3f, 0.1f), new Color(1f, 0.4f, 0.15f),
                new Vector3(0, y, -0.1f)));
            root.AddChild(CreateMeshNode("_Deposit1",
                new BoxMesh { Size = new Vector3(0.03f, 0.04f, 0.02f) },
                new Color(0.6f, 0.25f, 0.1f), new Vector3(0.04f, y - 0.02f, -0.11f)));
            root.AddChild(CreateMeshNode("_Deposit2",
                new BoxMesh { Size = new Vector3(0.02f, 0.03f, 0.02f) },
                new Color(0.5f, 0.2f, 0.08f), new Vector3(-0.03f, y + 0.02f, -0.1f)));
            return root;
        }

        private static Node3D BuildGraftNerveCluster(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_NerveCluster" };
            float y = GraftTorsoY(f);
            Color nerve = new Color(0.3f, 0.5f, 1f);
            root.AddChild(CreateEmissiveMeshNode("_Cluster",
                new SphereMesh { Radius = 0.04f, Height = 0.07f, RadialSegments = 6, Rings = 3 },
                nerve, nerve, new Vector3(0, y + 0.05f, 0.12f)));
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.DegToRad(120f * i - 60f);
                var tendril = CreateEmissiveMeshNode($"_Tendril{i}",
                    new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.012f, Height = 0.08f, RadialSegments = 4 },
                    nerve, nerve * 0.7f,
                    new Vector3(Mathf.Cos(a) * 0.04f, y + 0.05f, 0.12f + Mathf.Sin(a) * 0.03f));
                tendril.RotationDegrees = new Vector3(45f * Mathf.Cos(a), 0, 45f * Mathf.Sin(a));
                root.AddChild(tendril);
            }
            return root;
        }

        private static Node3D BuildGraftStalkersEye(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_StalkersEye" };
            float y = GraftTorsoY(f) + 0.2f;
            root.AddChild(CreateMeshNode("_EyeSocket",
                new SphereMesh { Radius = 0.035f, Height = 0.06f, RadialSegments = 8, Rings = 4 },
                new Color(0.7f, 0.7f, 0.65f), new Vector3(-0.15f, y, -0.03f)));
            root.AddChild(CreateEmissiveMeshNode("_Pupil",
                new SphereMesh { Radius = 0.018f, Height = 0.03f, RadialSegments = 6, Rings = 3 },
                new Color(1f, 0.2f, 0.1f), new Color(1f, 0.3f, 0.1f),
                new Vector3(-0.15f, y, -0.055f)));
            return root;
        }

        private static Node3D BuildGraftSinewBundle(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_SinewBundle" };
            Color sinew = new Color(0.6f, 0.25f, 0.2f);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.DegToRad(90f * i);
                root.AddChild(CreateMeshNode($"_Fiber{i}",
                    new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.008f, Height = 0.15f, RadialSegments = 4 },
                    sinew, new Vector3(Mathf.Cos(a) * 0.12f, 0.22f, Mathf.Sin(a) * 0.1f)));
            }
            return root;
        }

        // ── Epic grafts (more prominent) ──

        private static Node3D BuildGraftLeechGland(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_LeechGland" };
            float y = GraftTorsoY(f) - 0.05f;
            Color flesh = new Color(0.5f, 0.15f, 0.2f);
            Color glow = new Color(0.8f, 0.1f, 0.15f);
            root.AddChild(CreateEmissiveMeshNode("_Gland",
                new SphereMesh { Radius = 0.045f, Height = 0.06f, RadialSegments = 8, Rings = 4 },
                flesh, glow, new Vector3(0.12f, y, 0.05f)));
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                root.AddChild(CreateMeshNode($"_Filament{i}",
                    new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.003f, Height = 0.12f, RadialSegments = 3 },
                    flesh, new Vector3(0.12f - t * 0.2f, y + (i % 2) * 0.03f, 0.03f - t * 0.06f)));
            }
            return root;
        }

        private static Node3D BuildGraftBeetleColony(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_BeetleColony" };
            float y = GraftTorsoY(f);
            Color canister = new Color(0.3f, 0.28f, 0.2f);
            Color beetle = new Color(0.15f, 0.12f, 0.08f);
            root.AddChild(CreateMeshNode("_Canister",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.1f, RadialSegments = 8 },
                canister, new Vector3(0, y, 0.13f)));
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.DegToRad(60f * i);
                root.AddChild(CreateMeshNode($"_Beetle{i}",
                    new SphereMesh { Radius = 0.012f, Height = 0.015f, RadialSegments = 4, Rings = 2 },
                    beetle, new Vector3(Mathf.Cos(a) * 0.08f, y + 0.06f - i * 0.015f,
                        0.1f + Mathf.Sin(a) * 0.04f)));
            }
            return root;
        }

        private static Node3D BuildGraftChromaticTumor(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_ChromaticTumor" };
            float y = GraftTorsoY(f) + 0.1f;
            Color[] c = {
                new Color(1f, 0.3f, 0.2f), new Color(0.2f, 0.8f, 1f),
                new Color(0.3f, 1f, 0.4f), new Color(0.9f, 0.7f, 0.1f)
            };
            root.AddChild(CreateEmissiveMeshNode("_TumorCore",
                new SphereMesh { Radius = 0.04f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
                c[0], c[0], new Vector3(0.2f, y, -0.06f)));
            for (int i = 1; i < 4; i++)
            {
                float a = Mathf.DegToRad(120f * i);
                root.AddChild(CreateEmissiveMeshNode($"_Node{i}",
                    new SphereMesh { Radius = 0.02f, Height = 0.03f, RadialSegments = 4, Rings = 2 },
                    c[i], c[i],
                    new Vector3(0.2f + Mathf.Cos(a) * 0.035f, y + Mathf.Sin(a) * 0.02f, -0.06f)));
            }
            return root;
        }

        private static Node3D BuildGraftBloatSac(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_BloatSac" };
            float y = GraftTorsoY(f) - 0.1f;
            Color sac = new Color(0.5f, 0.35f, 0.15f);
            Color warn = new Color(1f, 0.6f, 0.1f);
            root.AddChild(CreateEmissiveMeshNode("_Sac",
                new SphereMesh { Radius = 0.06f, Height = 0.08f, RadialSegments = 8, Rings = 4 },
                sac, warn, new Vector3(-0.08f, y, 0.1f)));
            root.AddChild(CreateEmissiveMeshNode("_Stripe",
                new BoxMesh { Size = new Vector3(0.08f, 0.01f, 0.04f) },
                warn, warn, new Vector3(-0.08f, y, 0.14f)));
            return root;
        }

        private static Node3D BuildGraftGravityParasite(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_GravityParasite" };
            float y = GraftTorsoY(f) + 0.15f;
            Color dark = new Color(0.1f, 0.05f, 0.2f);
            Color glow = new Color(0.4f, 0.2f, 0.8f);
            root.AddChild(CreateEmissiveMeshNode("_Core",
                new SphereMesh { Radius = 0.035f, Height = 0.06f, RadialSegments = 8, Rings = 4 },
                dark, glow, new Vector3(0, y, 0.08f)));
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.DegToRad(90f * i);
                root.AddChild(CreateMeshNode($"_Debris{i}",
                    new BoxMesh { Size = new Vector3(0.015f, 0.015f, 0.015f) },
                    new Color(0.4f, 0.4f, 0.45f),
                    new Vector3(Mathf.Cos(a) * 0.06f, y + Mathf.Sin(a) * 0.03f, 0.08f)));
            }
            return root;
        }

        // ── Legendary grafts (significant body mods) ──

        private static Node3D BuildGraftCarapace(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_Carapace" };
            float y = GraftTorsoY(f);
            Color shell = new Color(0.45f, 0.4f, 0.3f);
            root.AddChild(CreateMeshNode("_FrontPlate",
                new BoxMesh { Size = new Vector3(0.2f, 0.12f, 0.025f) },
                shell, new Vector3(0, y, -0.09f)));
            root.AddChild(CreateMeshNode("_BackPlate",
                new BoxMesh { Size = new Vector3(0.18f, 0.14f, 0.025f) },
                shell, new Vector3(0, y, 0.12f)));
            root.AddChild(CreateMeshNode("_LeftFlank",
                new BoxMesh { Size = new Vector3(0.025f, 0.1f, 0.1f) },
                shell, new Vector3(-0.12f, y, 0)));
            root.AddChild(CreateMeshNode("_RightFlank",
                new BoxMesh { Size = new Vector3(0.025f, 0.1f, 0.1f) },
                shell, new Vector3(0.12f, y, 0)));
            return root;
        }

        private static Node3D BuildGraftOverloadedSynapse(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_Synapse" };
            float y = GraftHeadY(f);
            Color hot = new Color(1f, 0.6f, 0.1f);
            root.AddChild(CreateEmissiveMeshNode("_BrainNode",
                new SphereMesh { Radius = 0.04f, Height = 0.06f, RadialSegments = 8, Rings = 4 },
                hot, hot, new Vector3(0.06f, y + 0.05f, 0)));
            root.AddChild(CreateEmissiveMeshNode("_Arc1",
                new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.004f, Height = 0.06f, RadialSegments = 3 },
                hot, hot * 0.8f, new Vector3(0.08f, y + 0.08f, 0.02f)));
            root.AddChild(CreateEmissiveMeshNode("_Arc2",
                new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.004f, Height = 0.05f, RadialSegments = 3 },
                hot, hot * 0.8f, new Vector3(0.04f, y + 0.07f, -0.02f)));
            return root;
        }

        private static Node3D BuildGraftAdrenalGland(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_AdrenalGland" };
            float y = GraftTorsoY(f);
            Color angry = new Color(0.7f, 0.15f, 0.1f);
            Color vein = new Color(0.5f, 0.1f, 0.08f);
            root.AddChild(CreateEmissiveMeshNode("_Gland",
                new SphereMesh { Radius = 0.05f, Height = 0.07f, RadialSegments = 8, Rings = 4 },
                angry, angry, new Vector3(-0.14f, y - 0.05f, -0.04f)));
            root.AddChild(CreateMeshNode("_Vein1",
                new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.005f, Height = 0.1f, RadialSegments = 3 },
                vein, new Vector3(-0.1f, y - 0.02f, -0.05f)));
            root.AddChild(CreateMeshNode("_Vein2",
                new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.004f, Height = 0.08f, RadialSegments = 3 },
                vein, new Vector3(-0.12f, y - 0.08f, -0.03f)));
            return root;
        }

        private static Node3D BuildGraftHollowBone(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_HollowBone" };
            float y = GraftTorsoY(f) + 0.1f;
            Color bone = new Color(0.85f, 0.8f, 0.7f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.2f;
                string s = side > 0 ? "R" : "L";
                root.AddChild(CreateMeshNode($"_Strut{s}1",
                    new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.14f, RadialSegments = 4 },
                    bone, new Vector3(x, y, -0.02f)));
                root.AddChild(CreateMeshNode($"_Strut{s}2",
                    new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f, Height = 0.1f, RadialSegments = 4 },
                    bone, new Vector3(x + side * 0.02f, y - 0.06f, -0.03f)));
            }
            return root;
        }

        private static Node3D BuildGraftSkullCap(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_SkullCap" };
            float y = GraftHeadY(f);
            Color plate = new Color(0.5f, 0.45f, 0.35f);
            root.AddChild(CreateMeshNode("_Plate",
                new BoxMesh { Size = new Vector3(0.14f, 0.06f, 0.03f) },
                plate, new Vector3(0, y - 0.02f, -0.09f)));
            root.AddChild(CreateMeshNode("_Ridge",
                new BoxMesh { Size = new Vector3(0.04f, 0.08f, 0.02f) },
                plate, new Vector3(0, y + 0.01f, -0.1f)));
            return root;
        }

        private static Node3D BuildGraftMagmaGland(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_MagmaGland" };
            float y = GraftTorsoY(f) + 0.05f;
            Color lava = new Color(1f, 0.4f, 0.05f);
            root.AddChild(CreateEmissiveMeshNode("_MagmaOrgan",
                new SphereMesh { Radius = 0.04f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                lava, lava, new Vector3(0.18f, y, -0.08f)));
            root.AddChild(CreateEmissiveMeshNode("_HeatVent",
                new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.01f, Height = 0.04f, RadialSegments = 4 },
                lava, lava * 1.3f, new Vector3(0.18f, y + 0.04f, -0.08f)));
            return root;
        }

        private static Node3D BuildGraftHydraStrand(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_HydraStrand" };
            float y = GraftTorsoY(f) + 0.2f;
            Color nerve = new Color(0.3f, 0.6f, 0.4f);
            root.AddChild(CreateMeshNode("_MainStrand",
                new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.015f, Height = 0.12f, RadialSegments = 4 },
                nerve, new Vector3(0.15f, y, 0)));
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.DegToRad(120f * i);
                var split = CreateEmissiveMeshNode($"_Split{i}",
                    new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.006f, Height = 0.06f, RadialSegments = 3 },
                    nerve, nerve, new Vector3(0.15f + Mathf.Cos(a) * 0.02f, y + 0.08f, Mathf.Sin(a) * 0.02f));
                split.RotationDegrees = new Vector3(Mathf.Cos(a) * 30f, 0, Mathf.Sin(a) * 30f);
                root.AddChild(split);
            }
            return root;
        }

        // ── Mythic grafts (dramatic, build-defining) ──

        private static Node3D BuildGraftImmortalEngine(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_ImmortalEngine" };
            float y = GraftTorsoY(f);
            Color life = new Color(0.2f, 1f, 0.4f);
            root.AddChild(CreateEmissiveMeshNode("_Core",
                new SphereMesh { Radius = 0.06f, Height = 0.09f, RadialSegments = 10, Rings = 5 },
                life, life, new Vector3(0, y, -0.08f)));
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.DegToRad(60f * i);
                var vein = CreateEmissiveMeshNode($"_Vein{i}",
                    new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.008f, Height = 0.15f, RadialSegments = 3 },
                    life * 0.6f, life * 0.5f,
                    new Vector3(Mathf.Cos(a) * 0.06f, y + Mathf.Sin(a) * 0.06f, -0.07f));
                vein.RotationDegrees = new Vector3(Mathf.Sin(a) * 50f, 0, -Mathf.Cos(a) * 50f);
                root.AddChild(vein);
            }
            return root;
        }

        private static Node3D BuildGraftDevourer(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_Devourer" };
            float y = GraftTorsoY(f) - 0.05f;
            Color mass = new Color(0.15f, 0.08f, 0.12f);
            Color maw = new Color(0.8f, 0.1f, 0.2f);
            root.AddChild(CreateEmissiveMeshNode("_Mass",
                new SphereMesh { Radius = 0.07f, Height = 0.09f, RadialSegments = 8, Rings = 4 },
                mass, maw * 0.3f, new Vector3(-0.1f, y, 0.06f)));
            root.AddChild(CreateEmissiveMeshNode("_Maw",
                new TorusMesh { InnerRadius = 0.02f, OuterRadius = 0.04f, Rings = 8, RingSegments = 6 },
                maw, maw, new Vector3(-0.1f, y, 0.01f)));
            for (int i = 0; i < 3; i++)
                root.AddChild(CreateMeshNode($"_Tendril{i}",
                    new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.006f, Height = 0.1f, RadialSegments = 3 },
                    mass, new Vector3(-0.1f + (i - 1) * 0.04f, y - 0.06f, 0.04f)));
            return root;
        }

        private static Node3D BuildGraftNeuralHijack(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_NeuralHijack" };
            float y = GraftHeadY(f);
            Color nerve = new Color(0.4f, 0.8f, 0.5f);
            var main = CreateEmissiveMeshNode("_MainTendril",
                new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.012f, Height = 0.2f, RadialSegments = 4 },
                nerve, nerve, new Vector3(0, y + 0.05f, -0.04f));
            main.RotationDegrees = new Vector3(-40f, 0, 0);
            root.AddChild(main);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.DegToRad(90f * i);
                var branch = CreateEmissiveMeshNode($"_Branch{i}",
                    new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.005f, Height = 0.08f, RadialSegments = 3 },
                    nerve * 0.7f, nerve * 0.5f,
                    new Vector3(Mathf.Cos(a) * 0.04f, y + 0.15f, -0.1f + Mathf.Sin(a) * 0.03f));
                branch.RotationDegrees = new Vector3(-20f + i * 10f, i * 30f, 0);
                root.AddChild(branch);
            }
            return root;
        }

        private static Node3D BuildGraftParadoxGland(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_ParadoxGland" };
            float y = GraftTorsoY(f) + 0.1f;
            Color time = new Color(0.5f, 0.7f, 1f);
            root.AddChild(CreateEmissiveMeshNode("_GlandCore",
                new SphereMesh { Radius = 0.05f, Height = 0.07f, RadialSegments = 10, Rings = 5 },
                time, time, new Vector3(0.08f, y, 0.08f)));
            for (int i = 1; i <= 2; i++)
                root.AddChild(CreateEmissiveMeshNode($"_Echo{i}",
                    new SphereMesh { Radius = 0.04f, Height = 0.055f, RadialSegments = 6, Rings = 3 },
                    time * (0.5f / i), time * (0.3f / i),
                    new Vector3(0.08f + i * 0.03f, y, 0.08f + i * 0.025f)));
            return root;
        }

        private static Node3D BuildGraftStormCore(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_StormCore" };
            float y = GraftTorsoY(f) + 0.05f;
            Color lightning = new Color(0.4f, 0.7f, 1f);
            Color bright = new Color(0.7f, 0.9f, 1f);
            root.AddChild(CreateEmissiveMeshNode("_StormSphere",
                new SphereMesh { Radius = 0.055f, Height = 0.08f, RadialSegments = 10, Rings = 5 },
                lightning, bright, new Vector3(0, y, -0.09f)));
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.DegToRad(90f * i + 45f);
                var rod = CreateEmissiveMeshNode($"_Arc{i}",
                    new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.008f, Height = 0.1f, RadialSegments = 3 },
                    bright, bright,
                    new Vector3(Mathf.Cos(a) * 0.06f, y + 0.04f, -0.09f + Mathf.Sin(a) * 0.04f));
                rod.RotationDegrees = new Vector3(Mathf.Sin(a) * 40f, 0, -Mathf.Cos(a) * 40f);
                root.AddChild(rod);
            }
            return root;
        }

        private static Node3D BuildGraftVoidHeart(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_VoidHeart" };
            float y = GraftTorsoY(f);
            Color void_ = new Color(0.02f, 0.01f, 0.03f);
            Color rift = new Color(0.5f, 0.1f, 0.8f);
            root.AddChild(CreateMeshNode("_VoidSphere",
                new SphereMesh { Radius = 0.05f, Height = 0.08f, RadialSegments = 10, Rings = 5 },
                void_, new Vector3(0, y + 0.05f, -0.07f)));
            root.AddChild(CreateEmissiveMeshNode("_RiftRing",
                new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.055f, Rings = 12, RingSegments = 6 },
                rift, rift, new Vector3(0, y + 0.05f, -0.07f)));
            return root;
        }

        private static Node3D BuildGraftEchoChamber(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_EchoChamber" };
            float y = GraftTorsoY(f) + 0.1f;
            Color chamber = new Color(0.6f, 0.5f, 0.3f);
            Color resonance = new Color(0.9f, 0.7f, 0.3f);
            root.AddChild(CreateMeshNode("_OuterHorn",
                new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.03f, Height = 0.1f, RadialSegments = 8 },
                chamber, new Vector3(0, y, 0.12f)));
            root.AddChild(CreateEmissiveMeshNode("_InnerResonator",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.015f, Height = 0.08f, RadialSegments = 6 },
                resonance, resonance, new Vector3(0, y + 0.01f, 0.12f)));
            root.AddChild(CreateMeshNode("_Amplifier",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.025f, Height = 0.07f, RadialSegments = 6 },
                chamber, new Vector3(0.1f, y - 0.05f, 0.1f)));
            return root;
        }

        private static Node3D BuildGraftHemorrhageEngine(BotFrameType f)
        {
            var root = new Node3D { Name = "Graft_HemorrhageEngine" };
            float y = GraftTorsoY(f);
            Color blood = new Color(0.7f, 0.05f, 0.08f);
            Color bright = new Color(1f, 0.15f, 0.1f);
            root.AddChild(CreateEmissiveMeshNode("_Pump",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.06f, RadialSegments = 8 },
                blood, bright, new Vector3(0, y - 0.02f, -0.09f)));
            root.AddChild(CreateEmissiveMeshNode("_TubeLeft",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.01f, Height = 0.2f, RadialSegments = 4 },
                blood, bright * 0.5f, new Vector3(-0.06f, y + 0.02f, -0.07f)));
            root.AddChild(CreateEmissiveMeshNode("_TubeRight",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.01f, Height = 0.2f, RadialSegments = 4 },
                blood, bright * 0.5f, new Vector3(0.06f, y + 0.02f, -0.07f)));
            root.AddChild(CreateEmissiveMeshNode("_Gauge",
                new SphereMesh { Radius = 0.02f, Height = 0.03f, RadialSegments = 6, Rings = 3 },
                bright, bright, new Vector3(0, y + 0.05f, -0.1f)));
            return root;
        }

        public static Color GetClassColor(BotFrameType className) => className switch
        {
            BotFrameType.TinCan => new Color(0.6f, 0.62f, 0.65f),  // Steel grey
            BotFrameType.SparkPlug => new Color(0.45f, 0.3f, 0.65f),        // Purple
            BotFrameType.RustBucket => new Color(0.25f, 0.25f, 0.3f),            // Dark
            BotFrameType.Scrapheap => new Color(0.5f, 0.4f, 0.3f),             // Earthy brown
            BotFrameType.NoiseBox => new Color(0.35f, 0.3f, 0.4f),         // Muted violet
            BotFrameType.Clunker => new Color(0.6f, 0.45f, 0.35f),         // Warm tan
            _ => new Color(0.5f, 0.5f, 0.5f)
        };

        // ── Frame Color Variant Application ──

        /// <summary>
        /// Applies the correct texture variant to a loaded FBX model so that frames
        /// sharing the same underlying mech (e.g. TinCan + NoiseBox both use Stan)
        /// are visually distinct.
        /// </summary>
        private static void ApplyFrameColorVariant(Node3D model, BotFrameType frame)
        {
            if (!FrameTextureMap.TryGetValue(frame, out string textureName))
                return;

            string resPath = $"res://Models/Characters/Player/Textures/{textureName}.png";

            if (!_textureCache.TryGetValue(resPath, out Texture2D texture))
            {
                texture = GD.Load<Texture2D>(resPath);
                if (texture != null)
                    _textureCache[resPath] = texture;
            }

            // Apply texture if available (ensures correct look even if FBX import cache is stale)
            if (texture != null)
            {
                ApplyTextureRecursive(model, texture, textureName);
                GD.Print($"[CharacterMeshBuilder] Applied texture '{textureName}' to frame {frame}");
            }

            // Always tint — differentiates frames sharing the same model (e.g. TinCan vs NoiseBox)
            Color tint = GetFrameColorTint(frame);
            ApplyColorTintRecursive(model, tint);
        }

        /// <summary>
        /// Recursively applies a texture to all MeshInstance3D nodes in a model tree.
        /// Creates unique StandardMaterial3D overrides so the original resource is untouched.
        /// </summary>
        private static void ApplyTextureRecursive(Node node, Texture2D texture, string textureName)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    var existing = mi.GetActiveMaterial(i);
                    if (existing is StandardMaterial3D existStd)
                    {
                        // Only replace texture on surfaces that match the frame's base material.
                        // Other surfaces (Main, Black, Grey) keep their original look.
                        if (existStd.ResourceName != textureName)
                            continue;
                        var mat = (StandardMaterial3D)existStd.Duplicate();
                        mat.AlbedoTexture = texture;
                        mi.SetSurfaceOverrideMaterial(i, mat);
                    }
                }
            }
            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    ApplyTextureRecursive(childNode, texture, textureName);
            }
        }

        /// <summary>
        /// Recursively applies a color tint to all MeshInstance3D nodes (fallback when textures are missing).
        /// </summary>
        private static void ApplyColorTintRecursive(Node node, Color tint)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    var existing = mi.GetActiveMaterial(i);
                    if (existing is StandardMaterial3D existStd)
                    {
                        // Duplicate to preserve all FBX material properties (textures, normals, etc.)
                        var mat = (StandardMaterial3D)existStd.Duplicate();
                        mat.AlbedoColor = existStd.AlbedoColor * tint;
                        mi.SetSurfaceOverrideMaterial(i, mat);
                    }
                    else if (existing == null)
                    {
                        var mat = new StandardMaterial3D();
                        mat.AlbedoColor = tint;
                        mi.SetSurfaceOverrideMaterial(i, mat);
                    }
                }
            }
            foreach (var child in node.GetChildren())
            {
                if (child is Node childNode)
                    ApplyColorTintRecursive(childNode, tint);
            }
        }

        /// <summary>
        /// Returns a distinctive color tint for frames that share a model,
        /// used as a fallback when texture variant PNGs are unavailable.
        /// </summary>
        private static Color GetFrameColorTint(BotFrameType frame) => frame switch
        {
            BotFrameType.TinCan     => new Color(0.7f, 0.8f, 0.95f),  // blue/silver
            BotFrameType.NoiseBox   => new Color(0.45f, 0.7f, 0.45f), // green
            BotFrameType.Scrapheap  => new Color(0.9f, 0.65f, 0.35f), // rust-orange
            BotFrameType.Clunker    => new Color(0.65f, 0.55f, 0.75f),// purple/gunmetal
            BotFrameType.SparkPlug  => new Color(0.55f, 0.4f, 0.8f),  // purple
            BotFrameType.RustBucket => new Color(0.5f, 0.6f, 0.75f),   // steel blue
            _ => new Color(0.6f, 0.6f, 0.6f)
        };

    }
}
