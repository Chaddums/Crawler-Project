using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Manages the safe room between areas. Player's respite space with:
    /// - Couch + holographic display for loot box ceremony
    /// - Functional healing station
    /// - Atmospheric props and lighting
    /// - Continue portal to next area
    /// </summary>
    public partial class SafeRoomManager : Node3D
    {
        [Export] private PackedScene _playerScene;
        [Export] private PackedScene _hudScene;
        [Export] private PackedScene _cameraScene;

        private PlayerController _player;
        private SafeRoomCouch _couch;

        public override void _Ready()
        {
            int sectorNum = GameManager.Instance?.CurrentSector ?? 1;
            int areaNum = GameManager.Instance?.CurrentArea ?? 1;

            // Build the safe room geometry
            BuildRoom();

            // Spawn player at south end (walks north toward couch)
            if (_playerScene != null)
            {
                _player = _playerScene.Instantiate<PlayerController>();
                AddChild(_player);
                _player.GlobalPosition = new Vector3(0, 0.9f, 8);

                var selectedClass = GameManager.Instance?.SelectedClass ?? BotFrameType.TinCan;
                _player.ClassController.SelectClass(selectedClass);
            }

            // Spawn companion
            SpawnCompanion();

            // Spawn HUD
            if (_hudScene != null)
            {
                var hud = _hudScene.Instantiate();
                AddChild(hud);
            }

            // Spawn camera
            if (_cameraScene != null)
            {
                var camera = _cameraScene.Instantiate<IsometricCamera>();
                AddChild(camera);
                camera.Initialize(_player);
            }

            // Support systems
            SpawnSupportSystems();

            GameManager.Instance?.ChangeState(GameState.SafeRoom);

            // Restore player state
            if (GameManager.Instance?.IsLoadingGame == true)
            {
                GameManager.Instance.IsLoadingGame = false;
                SaveManager.ApplyLoadedState(_player);
            }
            else if (SaveManager.SaveFileExists() && _player != null)
            {
                SaveManager.ApplyTransitionState(_player);
            }

            // Auto-save
            if (_player != null)
                SaveManager.SaveGame(_player, sectorNum);

            // Intro commentary
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                if (AchievementManager.PendingLootBoxes.Count > 0)
                    commentary.QueueLine("BIT", "You've got loot boxes! Head to the couch to open them.",
                        CommentaryPriority.Medium, CommentaryCategory.SectorIntro);
                else
                    commentary.QueueLine("BIT", "Take a breather. The healing station is on your right.",
                        CommentaryPriority.Low, CommentaryCategory.SectorIntro);
            }

            GD.Print($"[SafeRoomManager] Safe room ready (Floor {sectorNum}, Area {areaNum})");
        }

        // ===== ROOM CONSTRUCTION =====

        private void BuildRoom()
        {
            float roomSize = 24f;
            float wallHeight = 5f;
            float halfSize = roomSize / 2f;

            var roomNode = new Node3D();
            roomNode.Name = "SafeRoomGeometry";
            AddChild(roomNode);

            // Floor — warm industrial metal
            var floor = new MeshInstance3D();
            var floorMesh = new PlaneMesh();
            floorMesh.Size = new Vector2(roomSize, roomSize);
            floor.Mesh = floorMesh;
            var floorMat = new StandardMaterial3D();
            floorMat.AlbedoColor = new Color(0.15f, 0.18f, 0.25f);
            floor.MaterialOverride = floorMat;
            roomNode.AddChild(floor);

            // Floor collision
            var floorBody = new StaticBody3D();
            floorBody.CollisionLayer = Constants.MASK_GROUND;
            roomNode.AddChild(floorBody);
            var floorShape = new CollisionShape3D();
            var floorBox = new BoxShape3D();
            floorBox.Size = new Vector3(roomSize, 0.1f, roomSize);
            floorShape.Shape = floorBox;
            floorShape.Position = new Vector3(0, -0.05f, 0);
            floorBody.AddChild(floorShape);

            // Navigation region
            var navRegion = new NavigationRegion3D();
            var navMesh = new NavigationMesh();
            navMesh.Vertices = new Vector3[]
            {
                new(-halfSize, 0, -halfSize),
                new(halfSize, 0, -halfSize),
                new(halfSize, 0, halfSize),
                new(-halfSize, 0, halfSize)
            };
            navMesh.AddPolygon(new int[] { 0, 1, 2, 3 });
            navRegion.NavigationMesh = navMesh;
            roomNode.AddChild(navRegion);

            // Walls
            AddWall(roomNode, new Vector3(0, wallHeight / 2, -halfSize), new Vector3(roomSize, wallHeight, 0.3f));
            AddWall(roomNode, new Vector3(0, wallHeight / 2, halfSize), new Vector3(roomSize, wallHeight, 0.3f));
            AddWall(roomNode, new Vector3(-halfSize, wallHeight / 2, 0), new Vector3(0.3f, wallHeight, roomSize));
            AddWall(roomNode, new Vector3(halfSize, wallHeight / 2, 0), new Vector3(0.3f, wallHeight, roomSize));

            // ===== LIGHTING =====

            // Main ambient (overhead, warm blue) — two lights for larger room
            var mainLight = new OmniLight3D();
            mainLight.Position = new Vector3(0, 4f, -3f);
            mainLight.LightColor = new Color(0.35f, 0.45f, 0.7f);
            mainLight.LightEnergy = 1.2f;
            mainLight.OmniRange = 16f;
            roomNode.AddChild(mainLight);

            var mainLight2 = new OmniLight3D();
            mainLight2.Position = new Vector3(0, 4f, 5f);
            mainLight2.LightColor = new Color(0.35f, 0.45f, 0.7f);
            mainLight2.LightEnergy = 1.0f;
            mainLight2.OmniRange = 14f;
            roomNode.AddChild(mainLight2);

            // Warm accent near couch area
            var couchLight = new OmniLight3D();
            couchLight.Position = new Vector3(0, 2.5f, 3f);
            couchLight.LightColor = new Color(0.6f, 0.5f, 0.35f);
            couchLight.LightEnergy = 0.8f;
            couchLight.OmniRange = 6f;
            roomNode.AddChild(couchLight);

            // Crystal wall lights
            AddCrystalLights(roomNode, halfSize, wallHeight);

            // "Safe Room" label floating above center
            var safeLabel = new Label3D();
            safeLabel.Text = "Safe Room";
            safeLabel.FontSize = 48;
            safeLabel.Position = new Vector3(0, 4.2f, 0);
            safeLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            safeLabel.Modulate = new Color(0.5f, 0.7f, 1f);
            safeLabel.OutlineModulate = new Color(0, 0, 0);
            safeLabel.OutlineSize = 6;
            roomNode.AddChild(safeLabel);

            // ===== INTERACTABLES =====

            // Holographic display (in front of couch, floating)
            var display = new HolographicDisplay();
            display.Position = new Vector3(0, 1.5f, 1f);
            AddChild(display);
            display.Initialize();

            // Couch (center-south, faces north toward display)
            _couch = new SafeRoomCouch();
            AddChild(_couch);
            _couch.Initialize(new Vector3(0, 0, 4), display);

            // Healing station (east side)
            var healStation = new HealingStation();
            healStation.Position = new Vector3(7, 0, 0);
            AddChild(healStation);
            healStation.Initialize();

            // ===== ATMOSPHERE PROPS =====

            // --- West side: workshop area ---
            AddProp(roomNode, "computer", new Vector3(-7, 0, -1), 1.8f);
            AddProp(roomNode, "computer_small", new Vector3(-7, 0, 1.5f), 1.2f);
            AddProp(roomNode, "weapon_rack", new Vector3(-9, 0, -4), 2f);
            AddProp(roomNode, "shelf_tall", new Vector3(-9, 0, 0), 2.2f);

            // --- East side: storage area ---
            AddProp(roomNode, "barrel", new Vector3(9, 0, 3), 1.2f);
            AddProp(roomNode, "barrel", new Vector3(8, 0, 4.5f), 1.2f);
            AddProp(roomNode, "crate", new Vector3(9, 0, 6), 1.2f);
            AddProp(roomNode, "crate_long", new Vector3(7, 0, 7), 1f);
            AddProp(roomNode, "vessel_short", new Vector3(9, 0, -2), 1.4f);

            // --- North side: tech/utility area ---
            AddProp(roomNode, "pipes", new Vector3(6, 0, -9), 1.8f);
            AddProp(roomNode, "pipes", new Vector3(-6, 0, -9), 1.8f);
            AddProp(roomNode, "capsule", new Vector3(-3, 0, -8), 1.6f);
            AddProp(roomNode, "vessel_tall", new Vector3(3, 0, -8), 1.8f);

            // --- Columns flanking the couch/display area ---
            AddProp(roomNode, "column_1", new Vector3(-3.5f, 0, 2), 2.5f);
            AddProp(roomNode, "column_1", new Vector3(3.5f, 0, 2), 2.5f);

            // --- South side: entry area ---
            AddProp(roomNode, "crate", new Vector3(-8, 0, 8), 1.2f);
            AddProp(roomNode, "chest", new Vector3(5, 0, 8), 1.2f);
            AddProp(roomNode, "statue", new Vector3(-4, 0, 9), 1.8f);

            // ===== BUILDING BACKDROP =====
            // Place a KitBash3D building behind the safe room as exterior set dressing
            // Different building per sector for visual variety
            int sectorNum = GameManager.Instance?.CurrentSector ?? 1;
            string buildingId = sectorNum switch
            {
                1 => "field_barracks",       // Industrial — military barracks
                2 => "fuel_tanks",           // Toxic — chemical storage
                3 => "checkpoint",           // Military — fortified checkpoint
                4 => "logistics_center",     // Lab — large facility
                _ => "outpost",              // Core / default — command outpost
            };

            var building = ModelLibrary.TryLoad("building", buildingId);
            if (building != null)
            {
                // Scale building down to fit as backdrop (behind north wall, visible but not interactive)
                CharacterMeshBuilder.ScaleModelToFit(building, 20f);
                building.Position = new Vector3(0, 0, -halfSize - 15f);
                building.RotationDegrees = new Vector3(0, 180f, 0);
                building.Name = "SafeRoom_Building_Backdrop";
                roomNode.AddChild(building);

                // Dramatic lighting on the building
                var buildingLight = new OmniLight3D();
                buildingLight.Position = new Vector3(0, 8f, -halfSize - 10f);
                buildingLight.LightColor = new Color(0.3f, 0.4f, 0.6f);
                buildingLight.LightEnergy = 1.5f;
                buildingLight.OmniRange = 25f;
                buildingLight.ShadowEnabled = true;
                roomNode.AddChild(buildingLight);
            }

            // BIT companion idle drone (hovers near player spawn)
            AddBitDrone(roomNode);

            // Continue portal (north end)
            AddContinuePortal(roomNode);
        }

        private void AddProp(Node3D parent, string modelId, Vector3 position, float targetSize)
        {
            var model = ModelLibrary.TryLoad("prop", modelId);
            if (model != null)
            {
                RoomBuilder.ScaleModelToFitEffective(model, targetSize);
                model.Position = position;
                // Strip colliders from decorative props — they can trap the player
                StripColliders(model);
                parent.AddChild(model);
                RoomBuilder.GroundModel(model);
            }
            else
            {
                // Procedural fallback: dark box placeholder
                var mesh = new MeshInstance3D();
                mesh.Mesh = new BoxMesh { Size = new Vector3(targetSize * 0.6f, targetSize, targetSize * 0.6f) };
                mesh.Position = position + Vector3.Up * targetSize * 0.5f;
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(0.15f, 0.17f, 0.22f);
                mesh.MaterialOverride = mat;
                parent.AddChild(mesh);
            }
        }

        private void AddBitDrone(Node3D parent)
        {
            var bit = new Node3D();
            bit.Name = "BIT_Idle";
            bit.Position = new Vector3(3, 1.5f, 6);
            parent.AddChild(bit);

            // Small hovering sphere with blue emission
            var droneMesh = new MeshInstance3D();
            droneMesh.Mesh = new SphereMesh { Radius = 0.15f, Height = 0.3f, RadialSegments = 8, Rings = 4 };
            var droneMat = new StandardMaterial3D();
            droneMat.AlbedoColor = new Color(0.5f, 0.6f, 0.8f);
            droneMat.EmissionEnabled = true;
            droneMat.Emission = new Color(0.3f, 0.5f, 1f);
            droneMat.EmissionEnergyMultiplier = 1f;
            droneMesh.MaterialOverride = droneMat;
            bit.AddChild(droneMesh);

            // Eye light
            var eyeLight = new OmniLight3D();
            eyeLight.Position = new Vector3(0, 0, 0.15f);
            eyeLight.LightColor = new Color(0.3f, 0.6f, 1f);
            eyeLight.LightEnergy = 0.5f;
            eyeLight.OmniRange = 2f;
            bit.AddChild(eyeLight);

            // Idle bob animation
            var tween = bit.CreateTween();
            tween.SetLoops(10000);
            tween.TweenProperty(bit, "position:y", 1.7f, 2f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(bit, "position:y", 1.3f, 2f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }

        private void AddCrystalLights(Node3D parent, float halfSize, float wallHeight)
        {
            Color crystalColor = new Color(0.4f, 0.6f, 1f);
            float crystalY = wallHeight * 0.5f;

            Vector3[] positions = {
                new(0, crystalY, -halfSize + 0.2f),
                new(0, crystalY, halfSize - 0.2f),
                new(-halfSize + 0.2f, crystalY, 0),
                new(halfSize - 0.2f, crystalY, 0)
            };

            foreach (var pos in positions)
            {
                var crystalModel = ModelLibrary.TryLoad("prop", "crystal");
                if (crystalModel != null)
                {
                    RoomBuilder.ScaleModelToFitEffective(crystalModel, 0.3f);
                    crystalModel.Position = pos;
                    parent.AddChild(crystalModel);
                }
                else
                {
                    var crystalMat = new StandardMaterial3D();
                    crystalMat.AlbedoColor = crystalColor;
                    crystalMat.EmissionEnabled = true;
                    crystalMat.Emission = crystalColor;
                    crystalMat.EmissionEnergyMultiplier = 2f;

                    var crystal = new MeshInstance3D();
                    crystal.Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 8, Rings = 4 };
                    crystal.Position = pos;
                    crystal.MaterialOverride = crystalMat;
                    parent.AddChild(crystal);
                }

                var crystalLight = new OmniLight3D();
                crystalLight.Position = pos;
                crystalLight.LightColor = crystalColor;
                crystalLight.LightEnergy = 0.8f;
                crystalLight.OmniRange = 4f;
                crystalLight.ShadowEnabled = false;
                parent.AddChild(crystalLight);
            }
        }

        private void AddWall(Node3D parent, Vector3 position, Vector3 size)
        {
            var wallBody = new StaticBody3D();
            wallBody.Position = position;
            wallBody.CollisionLayer = 1;
            parent.AddChild(wallBody);

            var mesh = new MeshInstance3D();
            var boxMesh = new BoxMesh();
            boxMesh.Size = size;
            mesh.Mesh = boxMesh;
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.12f, 0.14f, 0.2f);
            mesh.MaterialOverride = mat;
            wallBody.AddChild(mesh);

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = size;
            shape.Shape = box;
            wallBody.AddChild(shape);
        }

        private void AddContinuePortal(Node3D parent)
        {
            var trigger = new Area3D();
            trigger.CollisionLayer = 0;
            trigger.CollisionMask = Constants.MASK_PLAYER;
            trigger.Position = new Vector3(0, 1, -9);
            parent.AddChild(trigger);

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(3, 3, 3);
            shape.Shape = box;
            trigger.AddChild(shape);

            var portalModel = ModelLibrary.TryLoad("prop", "teleporter");
            if (portalModel == null) portalModel = ModelLibrary.TryLoad("prop", "portal");
            if (portalModel != null)
            {
                RoomBuilder.ScaleModelToFitEffective(portalModel, 1.5f);
                trigger.AddChild(portalModel);
                RoomBuilder.GroundModel(portalModel);
            }
            else
            {
                var mesh = new MeshInstance3D();
                var cylinder = new CylinderMesh();
                cylinder.TopRadius = 1f;
                cylinder.BottomRadius = 1.5f;
                cylinder.Height = 0.3f;
                mesh.Mesh = cylinder;

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(0.3f, 0.8f, 0.4f);
                mat.EmissionEnabled = true;
                mat.Emission = new Color(0.2f, 0.7f, 0.3f);
                mat.EmissionEnergyMultiplier = 1.5f;
                mesh.MaterialOverride = mat;
                trigger.AddChild(mesh);

                var emissionTween = mesh.CreateTween();
                emissionTween.SetLoops(10000);
                emissionTween.TweenProperty(mat, "emission_energy_multiplier", 2.5f, 1.2f)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                emissionTween.TweenProperty(mat, "emission_energy_multiplier", 1.0f, 1.2f)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            }

            var portalParticles = VfxFactory.CreatePortalParticles(new Color(0.3f, 0.9f, 0.4f));
            portalParticles.Position = new Vector3(0, 0.5f, 0);
            trigger.AddChild(portalParticles);

            var label = new Label3D();
            label.Text = "Continue to Next Area";
            label.FontSize = 36;
            label.Position = new Vector3(0, 2.5f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(0.4f, 0.9f, 0.5f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 4;
            trigger.AddChild(label);

            trigger.BodyEntered += (body) =>
            {
                if (body.IsInGroup(Constants.GROUP_PLAYER))
                {
                    GD.Print("[SafeRoom] Continuing to next area...");
                    Callable.From(() => GameManager.Instance?.ContinueFromSafeRoom()).CallDeferred();
                }
            };
        }

        private static void StripColliders(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is StaticBody3D sb)
                {
                    sb.QueueFree();
                }
                else if (child is Node n)
                {
                    StripColliders(n);
                }
            }
        }

        // ===== SUPPORT =====

        private void SpawnCompanion()
        {
            string companionId = GameManager.Instance?.ActiveCompanionId;
            if (string.IsNullOrEmpty(companionId)) return;

            var companionData = CompanionRegistry.Get(companionId);
            if (companionData == null) return;

            var scene = GD.Load<PackedScene>(Constants.SCENE_COMPANION);
            if (scene == null) return;

            var companion = scene.Instantiate<CompanionController>();
            AddChild(companion);
            companion.GlobalPosition = new Vector3(3, 0, 6);
            companion.Initialize(companionData);
        }

        private void SpawnSupportSystems()
        {
            var combatManager = new CombatManager();
            combatManager.Name = "CombatManager";
            AddChild(combatManager);

            var commentaryManager = new CommentaryManager();
            commentaryManager.Name = "CommentaryManager";
            AddChild(commentaryManager);

            var systemMessages = new SystemMessageManager();
            systemMessages.Name = "SystemMessageManager";
            AddChild(systemMessages);

            var audioManager = new AudioManager();
            audioManager.Name = "AudioManager";
            AddChild(audioManager);
        }
    }
}
