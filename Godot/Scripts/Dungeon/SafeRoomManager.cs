using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Manages the safe room between areas. Spawns player, companion, HUD, camera,
    /// and a portal to continue to the next area. Small peaceful room with no enemies.
    /// </summary>
    public partial class SafeRoomManager : Node3D
    {
        [Export] private PackedScene _playerScene;
        [Export] private PackedScene _hudScene;
        [Export] private PackedScene _cameraScene;

        private PlayerController _player;

        public override void _Ready()
        {
            int floorNum = GameManager.Instance?.CurrentFloor ?? 1;
            int areaNum = GameManager.Instance?.CurrentArea ?? 1;

            // Build the safe room geometry
            BuildRoom();

            // Spawn player at center
            if (_playerScene != null)
            {
                _player = _playerScene.Instantiate<PlayerController>();
                AddChild(_player);
                _player.GlobalPosition = new Vector3(0, 0.9f, 3);

                var selectedClass = GameManager.Instance?.SelectedClass ?? CrawlerClassName.BoringOlFighter;
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

            // Apply saved state if loading
            if (GameManager.Instance?.IsLoadingGame == true)
            {
                GameManager.Instance.IsLoadingGame = false;
                SaveManager.ApplyLoadedState(_player);
            }

            // Auto-save
            if (_player != null)
                SaveManager.SaveGame(_player, floorNum);

            GD.Print($"[SafeRoomManager] Safe room ready (Floor {floorNum}, Area {areaNum})");
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

            // Safe room decorations — soft blue ambient light
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

            // Continue portal at far end of room
            AddContinuePortal(roomNode);
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

            // Green portal (continue to next area)
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

            // Label
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
                    GameManager.Instance?.ContinueFromSafeRoom();
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
