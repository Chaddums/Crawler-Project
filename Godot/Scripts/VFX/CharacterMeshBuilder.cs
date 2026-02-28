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
            var root = new Node3D();
            root.Name = "Weapon";
            Color classColor = GetClassColor(className);
            Color darkMetal = new Color(0.25f, 0.25f, 0.27f);
            Color medMetal = new Color(0.4f, 0.4f, 0.42f);

            // Barrel — cylinder pointing forward (-Z)
            var barrel = CreateMeshNode("Barrel",
                new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.35f, RadialSegments = 8 },
                darkMetal, new Vector3(0, 0, -0.175f));
            barrel.RotateX(Mathf.DegToRad(90));
            root.AddChild(barrel);

            // Body — box behind barrel
            var body = CreateMeshNode("Body",
                new BoxMesh { Size = new Vector3(0.08f, 0.12f, 0.06f) },
                medMetal, new Vector3(0, 0, 0.05f));
            root.AddChild(body);

            // Grip — small box underneath
            var grip = CreateMeshNode("Grip",
                new BoxMesh { Size = new Vector3(0.04f, 0.08f, 0.04f) },
                darkMetal, new Vector3(0, -0.1f, 0.05f));
            root.AddChild(grip);

            // Muzzle tip — emissive class-colored glow
            var muzzle = CreateEmissiveMeshNode("Muzzle",
                new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                classColor, classColor, new Vector3(0, 0, -0.36f));
            root.AddChild(muzzle);

            return root;
        }

        // ── Junkbot Player Body ──

        private static Node3D BuildJunkbotBody(BotFrameType className)
        {
            var root = new Node3D();
            root.Name = "PlayerBody";

            Color chassis = new Color(0.3f, 0.3f, 0.32f);
            Color accent = GetClassColor(className);
            Color eyeColor = new Color(0.2f, 0.8f, 1.0f);
            Color trackColor = new Color(0.15f, 0.15f, 0.17f);
            Color armColor = new Color(0.4f, 0.4f, 0.42f);

            // ── Head — binocular eyes on neck stalk ──
            var headPivot = CreatePivot("Head", new Vector3(0, 1.3f, 0));
            // Neck stalk
            var neck = CreateMeshNode("_NeckStalk",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 0.2f, RadialSegments = 6 },
                chassis, new Vector3(0, -0.05f, 0));
            headPivot.AddChild(neck);
            // Left eye housing
            var leftEyeHousing = CreateMeshNode("_LeftEyeHousing",
                new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = 0.1f, RadialSegments = 8 },
                chassis, new Vector3(-0.08f, 0.08f, 0));
            leftEyeHousing.RotateX(Mathf.DegToRad(90));
            headPivot.AddChild(leftEyeHousing);
            // Left eye lens
            var leftLens = CreateEmissiveMeshNode("_LeftLens",
                new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 8, Rings = 4 },
                eyeColor, eyeColor, new Vector3(-0.08f, 0.08f, -0.06f));
            headPivot.AddChild(leftLens);
            // Right eye housing
            var rightEyeHousing = CreateMeshNode("_RightEyeHousing",
                new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = 0.1f, RadialSegments = 8 },
                chassis, new Vector3(0.08f, 0.08f, 0));
            rightEyeHousing.RotateX(Mathf.DegToRad(90));
            headPivot.AddChild(rightEyeHousing);
            // Right eye lens
            var rightLens = CreateEmissiveMeshNode("_RightLens",
                new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 8, Rings = 4 },
                eyeColor, eyeColor, new Vector3(0.08f, 0.08f, -0.06f));
            headPivot.AddChild(rightLens);
            // Slight forward tilt for character
            headPivot.RotateX(Mathf.DegToRad(-5));
            root.AddChild(headPivot);

            // ── Torso — boxy chassis with accent stripe and antenna ──
            var torsoPivot = CreatePivot("Torso", new Vector3(0, 0.75f, 0));
            var torsoBox = CreateMeshNode("_TorsoBox",
                new BoxMesh { Size = new Vector3(0.5f, 0.5f, 0.35f) },
                chassis, Vector3.Zero);
            torsoPivot.AddChild(torsoBox);
            // Accent stripe on front
            var stripe = CreateMeshNode("_AccentStripe",
                new BoxMesh { Size = new Vector3(0.42f, 0.06f, 0.01f) },
                accent, new Vector3(0, 0, -0.18f));
            torsoPivot.AddChild(stripe);
            // Antenna nub on top
            var antenna = CreateMeshNode("_Antenna",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.02f, Height = 0.12f, RadialSegments = 4 },
                armColor, new Vector3(0.1f, 0.31f, 0));
            torsoPivot.AddChild(antenna);
            root.AddChild(torsoPivot);

            // ── Left Arm — hydraulic arm + clamp hand ──
            var leftArmPivot = CreatePivot("LeftArm", new Vector3(-0.32f, 0.85f, 0));
            var leftArmShaft = CreateMeshNode("_LeftArmShaft",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.035f, Height = 0.35f, RadialSegments = 6 },
                armColor, new Vector3(0, -0.18f, 0));
            leftArmPivot.AddChild(leftArmShaft);
            // Clamp fingers
            var leftClampA = CreateMeshNode("_LeftClampA",
                new BoxMesh { Size = new Vector3(0.03f, 0.1f, 0.02f) },
                armColor, new Vector3(-0.03f, -0.4f, 0));
            leftClampA.RotateZ(Mathf.DegToRad(10));
            leftArmPivot.AddChild(leftClampA);
            var leftClampB = CreateMeshNode("_LeftClampB",
                new BoxMesh { Size = new Vector3(0.03f, 0.1f, 0.02f) },
                armColor, new Vector3(0.03f, -0.4f, 0));
            leftClampB.RotateZ(Mathf.DegToRad(-10));
            leftArmPivot.AddChild(leftClampB);
            root.AddChild(leftArmPivot);

            // ── Right Arm — same as left, mirrored ──
            var rightArmPivot = CreatePivot("RightArm", new Vector3(0.32f, 0.85f, 0));
            var rightArmShaft = CreateMeshNode("_RightArmShaft",
                new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.035f, Height = 0.35f, RadialSegments = 6 },
                armColor, new Vector3(0, -0.18f, 0));
            rightArmPivot.AddChild(rightArmShaft);
            var rightClampA = CreateMeshNode("_RightClampA",
                new BoxMesh { Size = new Vector3(0.03f, 0.1f, 0.02f) },
                armColor, new Vector3(-0.03f, -0.4f, 0));
            rightClampA.RotateZ(Mathf.DegToRad(10));
            rightArmPivot.AddChild(rightClampA);
            var rightClampB = CreateMeshNode("_RightClampB",
                new BoxMesh { Size = new Vector3(0.03f, 0.1f, 0.02f) },
                armColor, new Vector3(0.03f, -0.4f, 0));
            rightClampB.RotateZ(Mathf.DegToRad(-10));
            rightArmPivot.AddChild(rightClampB);
            root.AddChild(rightArmPivot);

            // ── Left Leg — track assembly ──
            var leftLegPivot = CreatePivot("LeftLeg", new Vector3(-0.2f, 0.25f, 0));
            BuildTrackAssembly(leftLegPivot, trackColor, false);
            root.AddChild(leftLegPivot);

            // ── Right Leg — track assembly, mirrored ──
            var rightLegPivot = CreatePivot("RightLeg", new Vector3(0.2f, 0.25f, 0));
            BuildTrackAssembly(rightLegPivot, trackColor, true);
            root.AddChild(rightLegPivot);

            // ── Weapon ──
            var weapon = BuildWeapon(className);
            if (weapon != null)
            {
                weapon.Position = new Vector3(0.45f, 0.9f, -0.15f);
                root.AddChild(weapon);
            }

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

            // Body cylinder
            var body = CreateMeshNode("Body", new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.25f, Height = 0.8f, RadialSegments = 8 },
                straw, new Vector3(0, 0.7f, 0));
            root.AddChild(body);

            // Head sphere
            var head = CreateMeshNode("Head", new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 10, Rings = 5 },
                straw.Lightened(0.1f), new Vector3(0, 1.3f, 0));
            root.AddChild(head);

            // Crossbar arms
            var crossbar = CreateMeshNode("Crossbar", new BoxMesh { Size = new Vector3(0.9f, 0.06f, 0.06f) },
                new Color(0.4f, 0.3f, 0.2f), new Vector3(0, 1.0f, 0));
            root.AddChild(crossbar);

            // Post
            var post = CreateMeshNode("Post", new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 0.5f, RadialSegments = 6 },
                new Color(0.4f, 0.3f, 0.2f), new Vector3(0, 0.25f, 0));
            root.AddChild(post);

            return root;
        }

        private static Node3D BuildScrapRatBody()
        {
            var root = new Node3D();
            root.Name = "ScrapRatBody";
            Color brown = new Color(0.5f, 0.35f, 0.25f);

            // Squashed body sphere
            var body = CreateMeshNode("Body", new SphereMesh { Radius = 0.25f, Height = 0.3f, RadialSegments = 10, Rings = 5 },
                brown, new Vector3(0, 0.35f, 0));
            root.AddChild(body);

            // Small head
            var head = CreateMeshNode("Head", new SphereMesh { Radius = 0.12f, Height = 0.2f, RadialSegments = 8, Rings = 4 },
                brown.Lightened(0.05f), new Vector3(0, 0.4f, -0.25f));
            root.AddChild(head);

            // Tail
            var tail = CreateMeshNode("Tail", new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.04f, Height = 0.4f, RadialSegments = 4 },
                brown.Darkened(0.15f), new Vector3(0, 0.35f, 0.3f));
            tail.RotateX(Mathf.DegToRad(60));
            root.AddChild(tail);

            return root;
        }

        private static Node3D BuildDecoyUnitBody()
        {
            var root = new Node3D();
            root.Name = "DecoyUnitBody";
            Color gold = new Color(0.7f, 0.6f, 0.2f);
            Color wood = new Color(0.45f, 0.3f, 0.15f);

            // Chest body (box)
            var chest = CreateMeshNode("Chest", new BoxMesh { Size = new Vector3(0.6f, 0.4f, 0.4f) },
                wood, new Vector3(0, 0.5f, 0));
            root.AddChild(chest);

            // Lid (angled)
            var lid = CreateMeshNode("Lid", new BoxMesh { Size = new Vector3(0.62f, 0.08f, 0.42f) },
                wood.Lightened(0.1f), new Vector3(0, 0.75f, -0.08f));
            lid.RotateX(Mathf.DegToRad(-15));
            root.AddChild(lid);

            // Teeth (row of small boxes)
            for (int i = -2; i <= 2; i++)
            {
                var tooth = CreateMeshNode($"Tooth{i}", new BoxMesh { Size = new Vector3(0.05f, 0.08f, 0.03f) },
                    Colors.White, new Vector3(i * 0.1f, 0.7f, -0.2f));
                root.AddChild(tooth);
            }

            // Gold trim
            var trim = CreateMeshNode("Trim", new BoxMesh { Size = new Vector3(0.64f, 0.04f, 0.03f) },
                gold, new Vector3(0, 0.5f, -0.21f));
            root.AddChild(trim);

            return root;
        }

        private static Node3D BuildWireWormBody()
        {
            var root = new Node3D();
            root.Name = "WireWormBody";
            Color green = new Color(0.4f, 0.7f, 0.3f);

            // 3 descending spheres
            float[] radii = { 0.2f, 0.16f, 0.12f };
            float[] yPos = { 0.5f, 0.35f, 0.25f };
            float[] zPos = { 0, 0.18f, 0.34f };

            for (int i = 0; i < 3; i++)
            {
                var segment = CreateEmissiveMeshNode($"Segment{i}",
                    new SphereMesh { Radius = radii[i], Height = radii[i] * 2, RadialSegments = 8, Rings = 4 },
                    green, new Color(0.3f, 0.6f, 0.2f),
                    new Vector3(0, yPos[i], zPos[i]));
                root.AddChild(segment);
            }

            return root;
        }

        private static Node3D BuildCorruptedSentryBody()
        {
            var root = new Node3D();
            root.Name = "CorruptedSentryBody";
            Color skin = new Color(0.45f, 0.55f, 0.25f);
            Color armor = new Color(0.4f, 0.3f, 0.2f);

            // Stocky body
            var body = CreateMeshNode("Body", new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.35f, Height = 0.9f, RadialSegments = 10 },
                skin, new Vector3(0, 0.75f, 0));
            root.AddChild(body);

            // Head (large, brutish)
            var head = CreateMeshNode("Head", new SphereMesh { Radius = 0.25f, Height = 0.45f, RadialSegments = 10, Rings = 5 },
                skin.Lightened(0.1f), new Vector3(0, 1.45f, 0));
            root.AddChild(head);

            // Armor plate on chest
            var chestPlate = CreateMeshNode("ChestPlate", new BoxMesh { Size = new Vector3(0.55f, 0.5f, 0.15f) },
                armor, new Vector3(0, 0.9f, -0.12f));
            root.AddChild(chestPlate);

            // Left arm (thick)
            var leftArm = CreateMeshNode("LeftArm",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.6f, RadialSegments = 6 },
                skin.Darkened(0.1f), new Vector3(-0.4f, 0.9f, 0));
            root.AddChild(leftArm);

            // Right arm (thick)
            var rightArm = CreateMeshNode("RightArm",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.6f, RadialSegments = 6 },
                skin.Darkened(0.1f), new Vector3(0.4f, 0.9f, 0));
            root.AddChild(rightArm);

            // Legs
            var leftLeg = CreateMeshNode("LeftLeg",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.5f, RadialSegments = 6 },
                skin.Darkened(0.15f), new Vector3(-0.15f, 0.25f, 0));
            root.AddChild(leftLeg);

            var rightLeg = CreateMeshNode("RightLeg",
                new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.5f, RadialSegments = 6 },
                skin.Darkened(0.15f), new Vector3(0.15f, 0.25f, 0));
            root.AddChild(rightLeg);

            // Crude club weapon
            var club = CreateMeshNode("Club",
                new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.05f, Height = 0.8f, RadialSegments = 6 },
                new Color(0.4f, 0.3f, 0.18f), new Vector3(0.5f, 1.1f, -0.2f));
            club.RotateZ(Mathf.DegToRad(-30));
            root.AddChild(club);

            return root;
        }

        private static Node3D BuildScrapHydraBody()
        {
            var root = new Node3D();
            root.Name = "ScrapHydraBody";
            Color gold = new Color(0.85f, 0.7f, 0.2f);
            Color wood = new Color(0.5f, 0.35f, 0.18f);

            // Oversized chest body
            var chest = CreateMeshNode("Chest", new BoxMesh { Size = new Vector3(0.9f, 0.6f, 0.6f) },
                wood, new Vector3(0, 0.6f, 0));
            root.AddChild(chest);

            // Lid (angled open)
            var lid = CreateMeshNode("Lid", new BoxMesh { Size = new Vector3(0.92f, 0.1f, 0.62f) },
                wood.Lightened(0.1f), new Vector3(0, 0.95f, -0.12f));
            lid.RotateX(Mathf.DegToRad(-20));
            root.AddChild(lid);

            // Gold crown on lid
            var crown = CreateEmissiveMeshNode("Crown",
                new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.15f, Height = 0.15f, RadialSegments = 8 },
                gold, gold, new Vector3(0, 1.15f, -0.1f));
            root.AddChild(crown);

            // Crown spikes
            for (int i = 0; i < 5; i++)
            {
                float angle = (float)i / 5f * Mathf.Tau;
                var spike = CreateEmissiveMeshNode($"CrownSpike{i}",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.03f, Height = 0.12f, RadialSegments = 4 },
                    gold, gold, new Vector3(Mathf.Cos(angle) * 0.15f, 1.28f, -0.1f + Mathf.Sin(angle) * 0.15f));
                root.AddChild(spike);
            }

            // Large teeth (gold-tinted)
            for (int i = -3; i <= 3; i++)
            {
                var tooth = CreateMeshNode($"Tooth{i}",
                    new BoxMesh { Size = new Vector3(0.06f, 0.12f, 0.04f) },
                    new Color(0.95f, 0.93f, 0.85f), new Vector3(i * 0.11f, 0.88f, -0.3f));
                root.AddChild(tooth);
            }

            // Red emissive eyes
            var leftEye = CreateEmissiveMeshNode("LeftEye",
                new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 8, Rings = 4 },
                new Color(1f, 0.1f, 0.05f), new Color(1f, 0.15f, 0.05f),
                new Vector3(-0.2f, 0.85f, -0.32f));
            root.AddChild(leftEye);

            var rightEye = CreateEmissiveMeshNode("RightEye",
                new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 8, Rings = 4 },
                new Color(1f, 0.1f, 0.05f), new Color(1f, 0.15f, 0.05f),
                new Vector3(0.2f, 0.85f, -0.32f));
            root.AddChild(rightEye);

            // Gold trim bands
            var trim = CreateEmissiveMeshNode("Trim",
                new BoxMesh { Size = new Vector3(0.94f, 0.06f, 0.04f) },
                gold, gold, new Vector3(0, 0.6f, -0.32f));
            root.AddChild(trim);

            var trimBottom = CreateEmissiveMeshNode("TrimBottom",
                new BoxMesh { Size = new Vector3(0.94f, 0.06f, 0.04f) },
                gold, gold, new Vector3(0, 0.35f, -0.32f));
            root.AddChild(trimBottom);

            return root;
        }

        private static Node3D BuildAxisAvatarBody()
        {
            var root = new Node3D();
            root.Name = "AxisAvatarBody";
            Color purple = new Color(0.45f, 0.25f, 0.55f);
            Color gold = new Color(0.85f, 0.7f, 0.2f);

            // Tall torso
            var torso = CreateMeshNode("Torso",
                new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.22f, Height = 1.0f, RadialSegments = 10 },
                purple, new Vector3(0, 1.3f, 0));
            root.AddChild(torso);

            // Head
            var head = CreateMeshNode("Head",
                new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 10, Rings = 5 },
                purple.Lightened(0.15f), new Vector3(0, 2.05f, 0));
            root.AddChild(head);

            // Emissive crown
            var crown = CreateEmissiveMeshNode("Crown",
                new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.18f, Height = 0.12f, RadialSegments = 8 },
                gold, gold, new Vector3(0, 2.3f, 0));
            root.AddChild(crown);

            for (int i = 0; i < 5; i++)
            {
                float angle = (float)i / 5f * Mathf.Tau;
                var spike = CreateEmissiveMeshNode($"CrownSpike{i}",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.025f, Height = 0.15f, RadialSegments = 4 },
                    gold, gold, new Vector3(Mathf.Cos(angle) * 0.18f, 2.43f, Mathf.Sin(angle) * 0.18f));
                root.AddChild(spike);
            }

            // Shoulder pauldrons (gold)
            var leftPauldron = CreateEmissiveMeshNode("LeftPauldron",
                new SphereMesh { Radius = 0.15f, Height = 0.2f, RadialSegments = 8, Rings = 4 },
                gold, gold, new Vector3(-0.4f, 1.75f, 0));
            root.AddChild(leftPauldron);

            var rightPauldron = CreateEmissiveMeshNode("RightPauldron",
                new SphereMesh { Radius = 0.15f, Height = 0.2f, RadialSegments = 8, Rings = 4 },
                gold, gold, new Vector3(0.4f, 1.75f, 0));
            root.AddChild(rightPauldron);

            // Arms
            var leftArm = CreateMeshNode("LeftArm",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.7f, RadialSegments = 6 },
                purple.Darkened(0.1f), new Vector3(-0.4f, 1.2f, 0));
            root.AddChild(leftArm);

            var rightArm = CreateMeshNode("RightArm",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.07f, Height = 0.7f, RadialSegments = 6 },
                purple.Darkened(0.1f), new Vector3(0.4f, 1.2f, 0));
            root.AddChild(rightArm);

            // Legs
            var leftLeg = CreateMeshNode("LeftLeg",
                new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.7f, RadialSegments = 6 },
                purple.Darkened(0.2f), new Vector3(-0.14f, 0.45f, 0));
            root.AddChild(leftLeg);

            var rightLeg = CreateMeshNode("RightLeg",
                new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.7f, RadialSegments = 6 },
                purple.Darkened(0.2f), new Vector3(0.14f, 0.45f, 0));
            root.AddChild(rightLeg);

            // Large emissive sword
            var blade = CreateEmissiveMeshNode("Blade",
                new BoxMesh { Size = new Vector3(0.1f, 1.0f, 0.04f) },
                new Color(0.8f, 0.8f, 0.9f), new Color(0.6f, 0.5f, 0.9f),
                new Vector3(0.55f, 1.4f, -0.2f));
            blade.RotateZ(Mathf.DegToRad(-15));
            root.AddChild(blade);

            var hilt = CreateMeshNode("Hilt",
                new BoxMesh { Size = new Vector3(0.25f, 0.06f, 0.06f) },
                gold, new Vector3(0.5f, 0.85f, -0.2f));
            root.AddChild(hilt);

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
                return equipment.Slot switch
                {
                    EquipmentSlot.MainHand or EquipmentSlot.OffHand => BuildSwordMesh(),
                    EquipmentSlot.Head => BuildHelmetMesh(),
                    EquipmentSlot.Chest or EquipmentSlot.Legs => BuildArmorMesh(),
                    EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => BuildRingMesh(),
                    EquipmentSlot.Amulet => BuildAmuletMesh(),
                    _ => BuildDefaultItemMesh()
                };
            }

            if (item.BaseData.Type == ItemType.Consumable)
                return BuildPotionMesh();

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
            if (item.BaseData is EquipmentData equipment)
            {
                return equipment.Slot switch
                {
                    EquipmentSlot.MainHand or EquipmentSlot.OffHand => "sword",
                    EquipmentSlot.Head => "helmet",
                    EquipmentSlot.Chest => "armor",
                    EquipmentSlot.Legs => "leggings",
                    EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => "ring",
                    EquipmentSlot.Amulet => "amulet",
                    _ => null
                };
            }
            if (item.BaseData.Type == ItemType.Consumable)
                return "potion";
            return null;
        }

        /// <summary>
        /// Build a multi-part Node3D item model (better visuals than single mesh).
        /// </summary>
        public static Node3D BuildItemModel(ItemInstance item)
        {
            if (item.BaseData is EquipmentData equipment)
            {
                return equipment.Slot switch
                {
                    EquipmentSlot.MainHand or EquipmentSlot.OffHand => BuildSwordModel(),
                    EquipmentSlot.Head => BuildHelmetModel(),
                    EquipmentSlot.Chest or EquipmentSlot.Legs => BuildArmorModel(),
                    EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => BuildRingModel(),
                    EquipmentSlot.Amulet => BuildAmuletModel(),
                    _ => BuildDefaultItemModel()
                };
            }

            if (item.BaseData.Type == ItemType.Consumable)
                return BuildPotionModel();

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

        private static Node3D BuildArmorModel()
        {
            var root = new Node3D();
            root.Name = "ArmorItem";

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

        private static Node3D BuildDefaultItemModel()
        {
            var root = new Node3D();
            root.Name = "DefaultItem";

            var box = CreateMeshNode("Box", new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) },
                new Color(0.5f, 0.5f, 0.5f), Vector3.Zero);
            root.AddChild(box);

            return root;
        }

        // Keep single-mesh versions for backward compatibility (BuildItemMesh still used)
        private static Mesh BuildSwordMesh()
        {
            return new BoxMesh { Size = new Vector3(0.08f, 0.5f, 0.03f) };
        }

        private static Mesh BuildHelmetMesh()
        {
            return new SphereMesh { Radius = 0.2f, Height = 0.25f, RadialSegments = 10, Rings = 5 };
        }

        private static Mesh BuildArmorMesh()
        {
            return new BoxMesh { Size = new Vector3(0.35f, 0.3f, 0.15f) };
        }

        private static Mesh BuildPotionMesh()
        {
            return new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.1f, Height = 0.25f, RadialSegments = 8 };
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

        private static Color GetClassColor(BotFrameType className) => className switch
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
