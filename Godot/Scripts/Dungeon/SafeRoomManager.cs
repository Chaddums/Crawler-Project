using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Manages the safe room between areas. Spawns player, companion, HUD, camera,
    /// and a portal to continue to the next area. Includes healing fountain,
    /// crystal wall lights, and portal particles.
    /// </summary>
    public partial class SafeRoomManager : Node3D
    {
        [Export] private PackedScene _playerScene;
        [Export] private PackedScene _hudScene;
        [Export] private PackedScene _cameraScene;

        private PlayerController _player;

        public override void _Ready()
        {
            int sectorNum = GameManager.Instance?.CurrentSector ?? 1;
            int areaNum = GameManager.Instance?.CurrentArea ?? 1;

            // Build the safe room geometry
            BuildRoom();

            // Spawn player at center
            if (_playerScene != null)
            {
                _player = _playerScene.Instantiate<PlayerController>();
                AddChild(_player);
                _player.GlobalPosition = new Vector3(0, 0.9f, 3);

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

            GD.Print($"[SafeRoomManager] Safe room ready (Floor {sectorNum}, Area {areaNum})");

            // Open pending achievement loot boxes after a brief settle delay
            if (AchievementManager.PendingLootBoxes.Count > 0)
            {
                GetTree().CreateTimer(1.5f).Timeout += () => ProcessPendingLootBoxes();
            }
        }

        private void ProcessPendingLootBoxes()
        {
            if (AchievementManager.PendingLootBoxes.Count == 0) return;

            var lootBox = AchievementManager.PendingLootBoxes.Dequeue();
            if (lootBox?.BaseData is not LootBoxData lootBoxData) return;

            var ceremony = new LootBoxCeremonyUI();
            GetTree().Root.AddChild(ceremony);
            ceremony.StartCeremony(lootBoxData);

            // Chain: when this ceremony is collected, open the next one (if any)
            ceremony.CeremonyCollected += () =>
            {
                if (AchievementManager.PendingLootBoxes.Count > 0)
                {
                    // Brief pause between ceremonies
                    GetTree().CreateTimer(0.8f).Timeout += () => ProcessPendingLootBoxes();
                }
            };
        }

        private void BuildRoom()
        {
            float roomSize = 16f;
            float wallHeight = 4f;
            float halfSize = roomSize / 2f;

            var roomNode = new Node3D();
            roomNode.Name = "SafeRoomGeometry";
            AddChild(roomNode);

            // Floor
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

            // Walls (4 sides, no door openings needed)
            AddWall(roomNode, new Vector3(0, wallHeight / 2, -halfSize), new Vector3(roomSize, wallHeight, 0.3f));
            AddWall(roomNode, new Vector3(0, wallHeight / 2, halfSize), new Vector3(roomSize, wallHeight, 0.3f));
            AddWall(roomNode, new Vector3(-halfSize, wallHeight / 2, 0), new Vector3(0.3f, wallHeight, roomSize));
            AddWall(roomNode, new Vector3(halfSize, wallHeight / 2, 0), new Vector3(0.3f, wallHeight, roomSize));

            // Soft blue ambient light
            var light = new OmniLight3D();
            light.Position = new Vector3(0, 3.5f, 0);
            light.LightColor = new Color(0.4f, 0.5f, 0.8f);
            light.LightEnergy = 1.5f;
            light.OmniRange = 12f;
            roomNode.AddChild(light);

            // "Safe Room" label floating above center
            var safeLabel = new Label3D();
            safeLabel.Text = "Safe Room";
            safeLabel.FontSize = 48;
            safeLabel.Position = new Vector3(0, 3.5f, 0);
            safeLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            safeLabel.Modulate = new Color(0.5f, 0.7f, 1f);
            safeLabel.OutlineModulate = new Color(0, 0, 0);
            safeLabel.OutlineSize = 6;
            roomNode.AddChild(safeLabel);

            // Healing fountain at center
            AddHealingFountain(roomNode);

            // Crystal wall lights
            AddCrystalLights(roomNode, halfSize, wallHeight);

            // Continue portal at far end of room
            AddContinuePortal(roomNode);
        }

        private void AddHealingFountain(Node3D parent)
        {
            var fountain = new Node3D();
            fountain.Name = "HealingFountain";
            fountain.Position = new Vector3(3, 0, 0);
            parent.AddChild(fountain);

            // Try model fountain first
            var fountainModel = ModelLibrary.TryLoad("prop", "fountain");
            if (fountainModel != null)
            {
                RoomBuilder.ScaleModelToFitEffective(fountainModel, 1.2f);
                fountain.AddChild(fountainModel);
            }
            else
            {
                // Base pedestal
                var baseMat = new StandardMaterial3D();
                baseMat.AlbedoColor = new Color(0.3f, 0.35f, 0.45f);
                var baseMesh = new MeshInstance3D();
                baseMesh.Mesh = new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.9f, Height = 0.4f, RadialSegments = 12 };
                baseMesh.Position = new Vector3(0, 0.2f, 0);
                baseMesh.MaterialOverride = baseMat;
                fountain.AddChild(baseMesh);

                // Water sphere
                var waterMat = new StandardMaterial3D();
                waterMat.AlbedoColor = new Color(0.2f, 0.5f, 0.9f, 0.7f);
                waterMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                waterMat.EmissionEnabled = true;
                waterMat.Emission = new Color(0.3f, 0.5f, 1f);
                waterMat.EmissionEnergyMultiplier = 1.5f;

                var waterMesh = new MeshInstance3D();
                waterMesh.Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f, RadialSegments = 12, Rings = 6 };
                waterMesh.Position = new Vector3(0, 0.8f, 0);
                waterMesh.MaterialOverride = waterMat;
                fountain.AddChild(waterMesh);

                // Pulsing water tween (finite loop to avoid Godot infinite loop error)
                var tween = waterMesh.CreateTween();
                tween.SetLoops(10000);
                tween.TweenProperty(waterMesh, "scale", new Vector3(1.1f, 1.1f, 1.1f), 1.5f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(waterMesh, "scale", Vector3.One, 1.5f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.InOut);
            }

            // Particles + light — always added regardless of model
            var particles = VfxFactory.CreateAmbientParticles(new Color(0.3f, 0.6f, 1f), 0.6f);
            particles.Position = new Vector3(0, 0.6f, 0);
            fountain.AddChild(particles);

            var fountainLight = new OmniLight3D();
            fountainLight.Position = new Vector3(0, 1.2f, 0);
            fountainLight.LightColor = new Color(0.3f, 0.5f, 1f);
            fountainLight.LightEnergy = 1f;
            fountainLight.OmniRange = 4f;
            fountain.AddChild(fountainLight);
        }

        private void AddCrystalLights(Node3D parent, float halfSize, float wallHeight)
        {
            Color crystalColor = new Color(0.4f, 0.6f, 1f);
            float crystalY = wallHeight * 0.5f;

            // One crystal at midpoint of each wall
            Vector3[] positions = {
                new(0, crystalY, -halfSize + 0.2f),
                new(0, crystalY, halfSize - 0.2f),
                new(-halfSize + 0.2f, crystalY, 0),
                new(halfSize - 0.2f, crystalY, 0)
            };

            foreach (var pos in positions)
            {
                // Try model crystal first
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

                // Light — always added
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
            wallBody.CollisionLayer = 1; // Default layer
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
            trigger.Position = new Vector3(0, 1, -5);
            parent.AddChild(trigger);

            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(3, 3, 3);
            shape.Shape = box;
            trigger.AddChild(shape);

            // Try model portal first
            var portalModel = ModelLibrary.TryLoad("prop", "portal");
            if (portalModel != null)
            {
                RoomBuilder.ScaleModelToFitEffective(portalModel, 2f);
                trigger.AddChild(portalModel);
            }
            else
            {
                // Green portal mesh with pulsing emission
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

                // Pulsing emission tween on the mesh node
                var emissionTween = mesh.CreateTween();
                emissionTween.SetLoops(10000);
                emissionTween.TweenProperty(mat, "emission_energy_multiplier", 2.5f, 1.2f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.InOut);
                emissionTween.TweenProperty(mat, "emission_energy_multiplier", 1.0f, 1.2f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.InOut);
            }

            // Particles + label + trigger — always added
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
                    // Defer scene change to avoid removing CollisionObject during physics callback
                    Callable.From(() => GameManager.Instance?.ContinueFromSafeRoom()).CallDeferred();
                }
            };
        }

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
            companion.GlobalPosition = new Vector3(2, 0, 4);
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
