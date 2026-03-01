using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Static factory building character bodies and weapons.
    /// Tries to load 3D model assets first via ModelLibrary; falls back to procedural geometry.
    /// </summary>
    public static class CharacterMeshBuilder
    {
        // ── Player Body ──

        public static Node3D BuildPlayerBody(BotFrameType className)
        {
            // Try model asset first
            string classId = className.ToString().ToLower();
            var model = ModelLibrary.TryLoad("player", classId);
            if (model != null)
            {
                model.Name = "PlayerBody";
                ScaleModelToFit(model, 1.8f);
                return model;
            }

            // Procedural fallback — Wall-E style junkbot
            return BuildJunkbotBody(className);
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
                ScaleModelToFit(model, 0.8f);
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
            var weapon = BuildWeapon(className);
            if (weapon != null)
            {
                weapon.Position = pos;
                root.AddChild(weapon);
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
            Color plate = new Color(0.32f, 0.28f, 0.22f);

            // Squat single-lens head sunk into shoulders
            var headPivot = CreatePivot("Head", new Vector3(0, 0.95f, -0.02f));
            headPivot.AddChild(CreateMeshNode("_HeadBlock",
                new BoxMesh { Size = new Vector3(0.28f, 0.14f, 0.18f) },
                chassis, Vector3.Zero));
            // Single wide viewport
            headPivot.AddChild(CreateEmissiveMeshNode("_Viewport",
                new BoxMesh { Size = new Vector3(0.22f, 0.05f, 0.01f) },
                eyeColor, eyeColor, new Vector3(0, 0.01f, -0.1f)));
            // Heavy brow plate overhanging the viewport
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
            // Welded armor skirts on sides
            torsoPivot.AddChild(CreateMeshNode("_LeftSkirt",
                new BoxMesh { Size = new Vector3(0.06f, 0.32f, 0.4f) },
                plate, new Vector3(-0.36f, -0.04f, 0)));
            torsoPivot.AddChild(CreateMeshNode("_RightSkirt",
                new BoxMesh { Size = new Vector3(0.06f, 0.32f, 0.4f) },
                plate, new Vector3(0.36f, -0.04f, 0)));
            // Front dozer blade
            torsoPivot.AddChild(CreateMeshNode("_DozerBlade",
                new BoxMesh { Size = new Vector3(0.65f, 0.2f, 0.04f) },
                plate.Lightened(0.05f), new Vector3(0, -0.12f, -0.24f)));
            // Twin exhaust stacks on back
            for (float side = -1; side <= 1; side += 2)
            {
                torsoPivot.AddChild(CreateMeshNode(side < 0 ? "_ExhaustL" : "_ExhaustR",
                    new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.04f, Height = 0.22f, RadialSegments = 6 },
                    new Color(0.2f, 0.2f, 0.2f), new Vector3(side * 0.2f, 0.28f, 0.18f)));
            }
            root.AddChild(torsoPivot);

            // Thick short arms with clamps
            AddClampArm(root, "Left", armColor, new Vector3(-0.42f, 0.65f, 0), shaftLen: 0.25f, clampSize: 0.14f);
            AddClampArm(root, "Right", armColor, new Vector3(0.42f, 0.65f, 0), shaftLen: 0.25f, clampSize: 0.14f);

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

            // Standard clamp arms
            AddClampArm(root, "Left", armColor, new Vector3(-0.3f, 0.8f, 0));
            AddClampArm(root, "Right", armColor, new Vector3(0.3f, 0.8f, 0));

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

            // Thin delicate arms
            AddClampArm(root, "Left", armColor, new Vector3(-0.2f, 1.0f, 0), shaftLen: 0.28f, clampSize: 0.06f);
            AddClampArm(root, "Right", armColor, new Vector3(0.2f, 1.0f, 0), shaftLen: 0.28f, clampSize: 0.06f);

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

            // Slim retractable arms
            AddClampArm(root, "Left", armColor, new Vector3(-0.24f, 0.58f, 0), shaftLen: 0.22f, clampSize: 0.06f);
            AddClampArm(root, "Right", armColor, new Vector3(0.24f, 0.58f, 0), shaftLen: 0.22f, clampSize: 0.06f);

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
            // Triple antenna prongs
            for (int i = -1; i <= 1; i++)
            {
                root.AddChild(CreateMeshNode($"_Antenna{i}",
                    new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.012f, Height = 0.16f, RadialSegments = 4 },
                    armColor, new Vector3(i * 0.05f, 1.52f, 0.02f)));
                root.AddChild(CreateEmissiveMeshNode($"_AntennaTip{i}",
                    new SphereMesh { Radius = 0.012f, Height = 0.024f, RadialSegments = 6, Rings = 3 },
                    glow, glow, new Vector3(i * 0.05f, 1.61f, 0.02f)));
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

            // Short arms
            AddClampArm(root, "Left", armColor, new Vector3(-0.28f, 0.95f, 0), shaftLen: 0.25f, clampSize: 0.08f);
            AddClampArm(root, "Right", armColor, new Vector3(0.28f, 0.95f, 0), shaftLen: 0.25f, clampSize: 0.08f);

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
            // Welded jaw plate
            root.AddChild(CreateMeshNode("_JawPlate",
                new BoxMesh { Size = new Vector3(0.22f, 0.04f, 0.1f) },
                chassis.Darkened(0.1f), new Vector3(0, 1.12f, -0.06f)));

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

            // Oversized hydraulic fist arms
            for (float side = -1; side <= 1; side += 2)
            {
                string name = side < 0 ? "Left" : "Right";
                var armPivot = CreatePivot($"{name}Arm", new Vector3(side * 0.34f, 0.82f, 0));

                armPivot.AddChild(CreateMeshNode($"_{name}Upper",
                    new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.045f, Height = 0.2f, RadialSegments = 6 },
                    armColor, new Vector3(0, -0.1f, 0)));
                armPivot.AddChild(CreateMeshNode($"_{name}PistonRod",
                    new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.18f, RadialSegments = 4 },
                    piston, new Vector3(0.03f, -0.12f, 0)));
                armPivot.AddChild(CreateMeshNode($"_{name}Fist",
                    new BoxMesh { Size = new Vector3(0.13f, 0.13f, 0.11f) },
                    armColor.Darkened(0.1f), new Vector3(0, -0.28f, 0)));
                armPivot.AddChild(CreateMeshNode($"_{name}Knuckle",
                    new BoxMesh { Size = new Vector3(0.14f, 0.04f, 0.01f) },
                    piston, new Vector3(0, -0.26f, -0.06f)));

                root.AddChild(armPivot);
            }

            // Bipedal chicken-walker legs
            AddBipedLegs(root, legColor, xOffset: 0.2f);
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

            var leftLeg = CreatePivot("LeftLeg", new Vector3(-bodyWidth * 0.3f, 0.15f, 0));
            var rightLeg = CreatePivot("RightLeg", new Vector3(bodyWidth * 0.3f, 0.15f, 0));

            // 2 legs per side, spread front-back
            for (int z = -1; z <= 1; z += 2)
            {
                float zOff = z * 0.12f;
                string label = z < 0 ? "Front" : "Rear";

                // ── Left side legs ──
                float xSpread = bodyWidth * 0.6f;

                // Upper segment (angled outward)
                var lUpper = CreateMeshNode($"_L{label}Upper",
                    new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.025f, Height = 0.16f, RadialSegments = 6 },
                    legColor, new Vector3(-0.06f, -0.04f, zOff));
                lUpper.RotateZ(Mathf.DegToRad(35));
                leftLeg.AddChild(lUpper);

                // Knee joint
                leftLeg.AddChild(CreateMeshNode($"_L{label}Knee",
                    new SphereMesh { Radius = 0.022f, Height = 0.044f, RadialSegments = 6, Rings = 3 },
                    jointColor, new Vector3(-0.14f, -0.1f, zOff)));

                // Lower segment (angled down)
                var lLower = CreateMeshNode($"_L{label}Lower",
                    new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.015f, Height = 0.14f, RadialSegments = 6 },
                    legColor, new Vector3(-0.16f, -0.18f, zOff));
                lLower.RotateZ(Mathf.DegToRad(-15));
                leftLeg.AddChild(lLower);

                // Foot spike
                leftLeg.AddChild(CreateMeshNode($"_L{label}Foot",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.015f, Height = 0.03f, RadialSegments = 4 },
                    footColor, new Vector3(-0.15f, -0.26f, zOff)));

                // ── Right side legs ──
                var rUpper = CreateMeshNode($"_R{label}Upper",
                    new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.025f, Height = 0.16f, RadialSegments = 6 },
                    legColor, new Vector3(0.06f, -0.04f, zOff));
                rUpper.RotateZ(Mathf.DegToRad(-35));
                rightLeg.AddChild(rUpper);

                rightLeg.AddChild(CreateMeshNode($"_R{label}Knee",
                    new SphereMesh { Radius = 0.022f, Height = 0.044f, RadialSegments = 6, Rings = 3 },
                    jointColor, new Vector3(0.14f, -0.1f, zOff)));

                var rLower = CreateMeshNode($"_R{label}Lower",
                    new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.015f, Height = 0.14f, RadialSegments = 6 },
                    legColor, new Vector3(0.16f, -0.18f, zOff));
                rLower.RotateZ(Mathf.DegToRad(15));
                rightLeg.AddChild(rLower);

                rightLeg.AddChild(CreateMeshNode($"_R{label}Foot",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.015f, Height = 0.03f, RadialSegments = 4 },
                    footColor, new Vector3(0.15f, -0.26f, zOff)));
            }

            root.AddChild(leftLeg);
            root.AddChild(rightLeg);
        }

        // ── Bipedal Chicken-Walker Locomotion (Clunker) ──

        private static void AddBipedLegs(Node3D root, Color legColor, float xOffset = 0.16f)
        {
            Color jointColor = legColor.Lightened(0.1f);
            Color footColor = legColor.Darkened(0.15f);
            Color pistonColor = new Color(0.5f, 0.5f, 0.52f);

            for (float side = -1; side <= 1; side += 2)
            {
                string name = side < 0 ? "LeftLeg" : "RightLeg";
                var legPivot = CreatePivot(name, new Vector3(side * xOffset, 0.3f, 0));

                // Hip joint
                legPivot.AddChild(CreateMeshNode("_Hip",
                    new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 4 },
                    jointColor, Vector3.Zero));

                // Upper leg (thigh) — angled slightly forward
                var thigh = CreateMeshNode("_Thigh",
                    new BoxMesh { Size = new Vector3(0.06f, 0.18f, 0.06f) },
                    legColor, new Vector3(0, -0.1f, -0.02f));
                legPivot.AddChild(thigh);

                // Knee joint (bigger, industrial)
                legPivot.AddChild(CreateMeshNode("_Knee",
                    new SphereMesh { Radius = 0.04f, Height = 0.06f, RadialSegments = 8, Rings = 4 },
                    jointColor, new Vector3(0, -0.2f, -0.03f)));

                // Lower leg (shin) — angled backward (chicken-walker reverse knee)
                var shin = CreateMeshNode("_Shin",
                    new BoxMesh { Size = new Vector3(0.05f, 0.2f, 0.05f) },
                    legColor, new Vector3(0, -0.32f, 0.03f));
                legPivot.AddChild(shin);

                // Piston rod along shin
                legPivot.AddChild(CreateMeshNode("_Piston",
                    new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = 0.16f, RadialSegments = 4 },
                    pistonColor, new Vector3(0.025f, -0.3f, 0.01f)));

                // Ankle joint
                legPivot.AddChild(CreateMeshNode("_Ankle",
                    new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                    jointColor, new Vector3(0, -0.43f, 0.04f)));

                // Big flat foot
                legPivot.AddChild(CreateMeshNode("_Foot",
                    new BoxMesh { Size = new Vector3(0.1f, 0.03f, 0.14f) },
                    footColor, new Vector3(0, -0.46f, 0)));

                // Toe grips (2 prongs at front of foot)
                for (float t = -1; t <= 1; t += 2)
                {
                    legPivot.AddChild(CreateMeshNode($"_Toe{(t < 0 ? "L" : "R")}",
                        new BoxMesh { Size = new Vector3(0.025f, 0.02f, 0.04f) },
                        footColor.Darkened(0.1f), new Vector3(t * 0.03f, -0.47f, -0.08f)));
                }

                root.AddChild(legPivot);
            }
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

        public static Node3D BuildEnemyBody(string enemyId)
        {
            // Try model asset first
            var model = ModelLibrary.TryLoad("enemy", enemyId);
            if (model != null)
            {
                model.Name = "EnemyBody";
                ScaleModelToFit(model, 1.2f);
                return model;
            }

            // Procedural fallback
            return enemyId switch
            {
                "calibration_target" => BuildCalibrationTargetBody(),
                "scrap_rat" => BuildScrapRatBody(),
                "decoy_unit" => BuildDecoyUnitBody(),
                "wire_worm" => BuildWireWormBody(),
                "corrupted_sentry" => BuildCorruptedSentryBody(),
                "scrap_hydra" => BuildScrapHydraBody(),
                "axis_avatar" => BuildAxisAvatarBody(),
                _ => BuildDefaultEnemyBody()
            };
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
                // Route by specific item ID, fall back to slot-based
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
                return BuildLootBoxModel(lootBox.Tier);

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

        // ── Gun Weapon Models ──

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
            return tier switch
            {
                LootBoxTier.Bronze => BuildBronzeLootBox(),
                LootBoxTier.Silver => BuildSilverLootBox(),
                LootBoxTier.Gold => BuildGoldLootBox(),
                LootBoxTier.Diamond => BuildDiamondLootBox(),
                LootBoxTier.Legendary => BuildLegendaryLootBox(),
                _ => BuildBronzeLootBox()
            };
        }

        private static Node3D BuildBronzeLootBox()
        {
            var root = new Node3D();
            root.Name = "LootBox_Bronze";
            Color rust = new Color(0.6f, 0.4f, 0.2f);
            Color band = new Color(0.45f, 0.35f, 0.2f);
            Color latch = new Color(0.5f, 0.45f, 0.25f);

            // Box body
            root.AddChild(CreateMeshNode("_Body",
                new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.35f) },
                rust, new Vector3(0, 0.175f, 0)));
            // Lid
            root.AddChild(CreateMeshNode("_Lid",
                new BoxMesh { Size = new Vector3(0.52f, 0.06f, 0.37f) },
                rust.Lightened(0.08f), new Vector3(0, 0.38f, 0)));
            // 2 metal bands
            root.AddChild(CreateMeshNode("_Band1",
                new BoxMesh { Size = new Vector3(0.52f, 0.03f, 0.37f) },
                band, new Vector3(0, 0.12f, 0)));
            root.AddChild(CreateMeshNode("_Band2",
                new BoxMesh { Size = new Vector3(0.52f, 0.03f, 0.37f) },
                band, new Vector3(0, 0.26f, 0)));
            // Latch cylinder
            root.AddChild(CreateMeshNode("_Latch",
                new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.04f, RadialSegments = 6 },
                latch, new Vector3(0, 0.2f, -0.185f)));
            // Handle
            root.AddChild(CreateMeshNode("_Handle",
                new BoxMesh { Size = new Vector3(0.1f, 0.02f, 0.02f) },
                band.Darkened(0.1f), new Vector3(0, 0.4f, 0)));

            return root;
        }

        private static Node3D BuildSilverLootBox()
        {
            var root = new Node3D();
            root.Name = "LootBox_Silver";
            Color silver = new Color(0.7f, 0.72f, 0.75f);
            Color band = new Color(0.55f, 0.55f, 0.6f);
            Color rivet = new Color(0.6f, 0.6f, 0.65f);

            // Silver body
            root.AddChild(CreateMeshNode("_Body",
                new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.35f) },
                silver, new Vector3(0, 0.175f, 0)));
            root.AddChild(CreateMeshNode("_Lid",
                new BoxMesh { Size = new Vector3(0.52f, 0.06f, 0.37f) },
                silver.Lightened(0.08f), new Vector3(0, 0.38f, 0)));
            // 3 bands
            for (int i = 0; i < 3; i++)
            {
                root.AddChild(CreateMeshNode($"_Band{i}",
                    new BoxMesh { Size = new Vector3(0.52f, 0.025f, 0.37f) },
                    band, new Vector3(0, 0.08f + i * 0.1f, 0)));
            }
            // 4 corner rivets
            float[] rx = { -0.24f, 0.24f, -0.24f, 0.24f };
            float[] ry = { 0.35f, 0.35f, 0.02f, 0.02f };
            for (int i = 0; i < 4; i++)
            {
                root.AddChild(CreateMeshNode($"_Rivet{i}",
                    new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
                    rivet, new Vector3(rx[i], ry[i], -0.18f)));
            }
            // Emissive keyhole
            root.AddChild(CreateEmissiveMeshNode("_Keyhole",
                new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.025f, RadialSegments = 6 },
                silver.Lightened(0.3f), silver.Lightened(0.3f), new Vector3(0, 0.2f, -0.19f)));

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
            float radius = 1.2f;
            int bladeCount = 4;

            for (int i = 0; i < bladeCount; i++)
            {
                float angle = (float)i / bladeCount * Mathf.Tau;
                var pos = new Vector3(Mathf.Cos(angle) * radius, 0.4f, Mathf.Sin(angle) * radius);

                // Blade body
                var blade = CreateMeshNode($"Blade{i}",
                    new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.01f, Height = 0.5f, RadialSegments = 6 },
                    bladeMetal, pos);
                blade.RotateZ(Mathf.DegToRad(90));
                root.AddChild(blade);

                // Emissive cutting edge
                var edge = CreateEmissiveMeshNode($"Edge{i}",
                    new BoxMesh { Size = new Vector3(0.5f, 0.005f, 0.04f) },
                    edgeGlow, edgeGlow, pos);
                root.AddChild(edge);
            }

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

        /// <summary>
        /// Recursively find the first AnimationPlayer in a model hierarchy.
        /// </summary>
        public static AnimationPlayer FindAnimationPlayer(Node3D model)
        {
            foreach (var child in model.GetChildren())
            {
                if (child is AnimationPlayer found) return found;
                if (child is Node3D childNode)
                {
                    var result = FindAnimationPlayer(childNode);
                    if (result != null) return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Scale a model uniformly so its AABB height matches targetHeight.
        /// </summary>
        public static void ScaleModelToFit(Node3D model, float targetHeight)
        {
            var aabb = GetCombinedAabb(model);
            if (aabb.Size.Y <= 0.001f) return;

            float scale = targetHeight / aabb.Size.Y;
            model.Scale = Vector3.One * scale;
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

        private static Aabb GetCombinedAabb(Node3D node)
        {
            Aabb combined = new Aabb();
            bool first = true;

            if (node is VisualInstance3D vi)
            {
                combined = vi.GetAabb();
                first = false;
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node3D child3d)
                {
                    var childAabb = GetCombinedAabb(child3d);
                    if (childAabb.Size.LengthSquared() > 0)
                    {
                        if (first) { combined = childAabb; first = false; }
                        else combined = combined.Merge(childAabb);
                    }
                }
            }

            return combined;
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
    }
}
