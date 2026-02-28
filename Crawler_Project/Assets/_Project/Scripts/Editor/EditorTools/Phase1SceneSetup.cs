using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DungeonCrawlerCarl.Editor
{
    public static class Phase1SceneSetup
    {
        [MenuItem("DCC/Setup Phase 1 Test Scene")]
        public static void SetupTestScene()
        {
            if (!EditorUtility.DisplayDialog(
                "DCC Phase 1 Setup",
                "This will set up the TestCombat scene with:\n\n" +
                "• Ground plane (Layer: Ground)\n" +
                "• Player with all components\n" +
                "• Isometric camera\n" +
                "• GameManager\n" +
                "• Directional light\n" +
                "• Walls around the arena\n\n" +
                "Proceed?",
                "Yes, set it up!", "Cancel"))
            {
                return;
            }

            // Create or open a new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // =====================================================
            // 1. DIRECTIONAL LIGHT
            // =====================================================
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f); // warm white
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            // URP additional light data is added automatically by Unity 6

            // =====================================================
            // 2. GROUND PLANE
            // =====================================================
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.layer = Constants.LAYER_GROUND;
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f); // 50x50 unit area
            ground.isStatic = true;

            // Give it a darker material so it looks like dungeon floor
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.25f, 0.22f, 0.2f); // dark stone color
            groundRenderer.material = groundMat;
            AssetDatabase.CreateAsset(groundMat, "Assets/_Project/Art/Materials/M_GroundFloor.mat");

            // Add NavMesh surface for baking
            var navSurface = ground.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Mark ground as navigation static
            GameObjectUtility.SetStaticEditorFlags(ground,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);

            // =====================================================
            // 3. WALLS (so player doesn't walk off the edge)
            // =====================================================
            CreateWall("Wall_North", new Vector3(0, 1.5f, 25f), new Vector3(50f, 3f, 1f));
            CreateWall("Wall_South", new Vector3(0, 1.5f, -25f), new Vector3(50f, 3f, 1f));
            CreateWall("Wall_East", new Vector3(25f, 1.5f, 0), new Vector3(1f, 3f, 50f));
            CreateWall("Wall_West", new Vector3(-25f, 1.5f, 0), new Vector3(1f, 3f, 50f));

            // =====================================================
            // 4. PLAYER
            // =====================================================
            var player = new GameObject("Player");
            player.tag = "Player";
            player.layer = Constants.LAYER_PLAYER;
            player.transform.position = new Vector3(0, 0, 0);

            // Core capsule collider for physics
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 0.5f, 0);
            capsule.radius = 0.3f;
            capsule.height = 1f;

            // Rigidbody (kinematic — NavMesh handles movement)
            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // NavMeshAgent
            var agent = player.AddComponent<NavMeshAgent>();
            agent.speed = Constants.DEFAULT_MOVE_SPEED;
            agent.angularSpeed = 720f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 0.1f;
            agent.radius = 0.3f;
            agent.height = 1f;
            agent.baseOffset = 0f;

            // Sprite child object (billboard sprite)
            var spriteChild = new GameObject("Sprite");
            spriteChild.transform.SetParent(player.transform);
            spriteChild.transform.localPosition = new Vector3(0, 0.75f, 0);
            var spriteRenderer = spriteChild.AddComponent<SpriteRenderer>();
            spriteRenderer.color = Color.white;

            // Create a placeholder sprite programmatically
            var placeholderSprite = CreatePlaceholderSprite();
            spriteRenderer.sprite = placeholderSprite;
            spriteRenderer.sortingOrder = 10;

            // Billboard component so sprite faces camera
            spriteChild.AddComponent<Billboard>();

            // HealthComponent (required by PlayerController)
            var health = player.AddComponent<HealthComponent>();
            // Default 100 HP — will be overridden by Initialize()

            // Input System — PlayerInput component
            var playerInput = player.AddComponent<PlayerInput>();
            // Try to find our input actions asset
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Project/Settings/InputActions/PlayerInputActions.inputactions");
            if (inputActions != null)
            {
                playerInput.actions = inputActions;
                playerInput.defaultActionMap = "Player";
                playerInput.defaultControlScheme = "Keyboard&Mouse";
                playerInput.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            }
            else
            {
                Debug.LogWarning("[DCC] Could not find PlayerInputActions.inputactions! " +
                    "Assign it manually on the PlayerInput component.");
            }

            // Player scripts (RequireComponent will auto-add dependencies)
            // PlayerInputHandler requires PlayerInput (already added)
            var inputHandler = player.AddComponent<PlayerInputHandler>();

            // PlayerMovement requires NavMeshAgent (already added)
            var movement = player.AddComponent<PlayerMovement>();

            // PlayerStats
            var stats = player.AddComponent<PlayerStats>();

            // PlayerInventory
            var inventory = player.AddComponent<PlayerInventory>();

            // PlayerCombat
            var combat = player.AddComponent<PlayerCombat>();

            // PlayerAnimator (safe without Animator — null-checks internally)
            var animator = player.AddComponent<PlayerAnimator>();

            // SpriteDirectionSolver
            var dirSolver = player.AddComponent<SpriteDirectionSolver>();

            // PlayerController (the hub — must be added AFTER all RequireComponent deps)
            var controller = player.AddComponent<PlayerController>();

            // Wire the sprite renderer reference via SerializedObject
            var controllerSO = new SerializedObject(controller);
            var spriteProp = controllerSO.FindProperty("_spriteRenderer");
            if (spriteProp != null)
            {
                spriteProp.objectReferenceValue = spriteRenderer;
                controllerSO.ApplyModifiedProperties();
            }

            // Wire the ground layer mask on PlayerMovement
            var movementSO = new SerializedObject(movement);
            var groundLayerProp = movementSO.FindProperty("_groundLayer");
            if (groundLayerProp != null)
            {
                groundLayerProp.intValue = Constants.MASK_GROUND;
                movementSO.ApplyModifiedProperties();
            }

            // Wire SpriteDirectionSolver's sprite renderer
            var dirSolverSO = new SerializedObject(dirSolver);
            var dirSpriteProp = dirSolverSO.FindProperty("_spriteRenderer");
            if (dirSpriteProp != null)
            {
                dirSpriteProp.objectReferenceValue = spriteRenderer;
                dirSolverSO.ApplyModifiedProperties();
            }

            // Wire PlayerAnimator's sprite renderer
            var animatorSO = new SerializedObject(animator);
            var animSpriteProp = animatorSO.FindProperty("_spriteRenderer");
            if (animSpriteProp != null)
            {
                animSpriteProp.objectReferenceValue = spriteRenderer;
                animatorSO.ApplyModifiedProperties();
            }

            // =====================================================
            // 5. CAMERA
            // =====================================================
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.08f, 0.12f); // dark purple-ish
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cameraObj.AddComponent<AudioListener>();

            // URP camera data (added automatically in Unity 6 URP)
            if (cameraObj.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraObj.AddComponent<UniversalAdditionalCameraData>();

            // Isometric camera controller
            var isoCam = cameraObj.AddComponent<IsometricCameraController>();

            // Position camera at isometric offset from origin
            Quaternion isoRot = Quaternion.Euler(Constants.CAMERA_ANGLE_X, Constants.CAMERA_ANGLE_Y, 0);
            Vector3 offset = isoRot * new Vector3(0, 0, -Constants.CAMERA_DISTANCE);
            cameraObj.transform.position = offset;
            cameraObj.transform.rotation = isoRot;

            // =====================================================
            // 6. GAME MANAGER
            // =====================================================
            var gmObj = new GameObject("GameManager");
            var gm = gmObj.AddComponent<GameManager>();

            // Set initial state to InFloor for testing (skip menus)
            var gmSO = new SerializedObject(gm);
            var stateProp = gmSO.FindProperty("_initialState");
            if (stateProp != null)
            {
                stateProp.enumValueIndex = (int)GameState.InFloor;
                gmSO.ApplyModifiedProperties();
            }

            // =====================================================
            // 7. SOME DECORATION — pillars/crates for visual interest
            // =====================================================
            var decoMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            decoMat.color = new Color(0.4f, 0.35f, 0.3f);
            AssetDatabase.CreateAsset(decoMat, "Assets/_Project/Art/Materials/M_DungeonProps.mat");

            CreateProp("Pillar_1", new Vector3(-8, 1.5f, 8), new Vector3(1.5f, 3f, 1.5f), decoMat);
            CreateProp("Pillar_2", new Vector3(8, 1.5f, 8), new Vector3(1.5f, 3f, 1.5f), decoMat);
            CreateProp("Pillar_3", new Vector3(-8, 1.5f, -8), new Vector3(1.5f, 3f, 1.5f), decoMat);
            CreateProp("Pillar_4", new Vector3(8, 1.5f, -8), new Vector3(1.5f, 3f, 1.5f), decoMat);
            CreateProp("Crate_1", new Vector3(-4, 0.5f, 12), new Vector3(1f, 1f, 1f), decoMat);
            CreateProp("Crate_2", new Vector3(6, 0.5f, -5), new Vector3(1.2f, 1.2f, 1.2f), decoMat);

            // =====================================================
            // 8. BAKE NAVMESH
            // =====================================================
            // Automatically bake the NavMesh
            navSurface.BuildNavMesh();
            Debug.Log("[DCC] NavMesh baked automatically!");

            // =====================================================
            // 9. SAVE SCENE
            // =====================================================
            string scenePath = "Assets/_Project/Scenes/TestCombat.unity";
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[DCC] Scene saved to {scenePath}");

            // Add to build settings if not already there
            AddSceneToBuildSettings(scenePath);

            // =====================================================
            // DONE
            // =====================================================
            Debug.Log("===========================================");
            Debug.Log("[DCC] Phase 1 Test Scene Setup Complete!");
            Debug.Log("===========================================");
            Debug.Log("[DCC] Press PLAY to test!");
            Debug.Log("[DCC] Controls:");
            Debug.Log("[DCC]   WASD — Move (isometric)");
            Debug.Log("[DCC]   Left Click — Click-to-move");
            Debug.Log("[DCC]   Mouse Scroll — Zoom in/out");
            Debug.Log("[DCC]   F — Interact");
            Debug.Log("[DCC]   ESC — Pause");
            Debug.Log("===========================================");

            EditorUtility.DisplayDialog(
                "Phase 1 Ready!",
                "Test scene created successfully!\n\n" +
                "Press PLAY to walk around.\n\n" +
                "Controls:\n" +
                "• WASD — Move\n" +
                "• Left Click — Click-to-move\n" +
                "• Scroll — Zoom\n" +
                "• F — Interact\n",
                "Let's go!");

            // Select the player so it's visible in inspector
            Selection.activeGameObject = player;
        }

        private static void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.layer = Constants.LAYER_DEFAULT;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.isStatic = true;
            GameObjectUtility.SetStaticEditorFlags(wall,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);

            // Make walls a dark grey
            var renderer = wall.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.18f, 0.16f, 0.15f);
            renderer.material = mat;

            // Mark as Not Walkable on NavMesh
            var modifier = wall.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = 1; // Not Walkable
        }

        private static void CreateProp(string name, Vector3 position, Vector3 scale, Material mat)
        {
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = name;
            prop.layer = Constants.LAYER_DEFAULT;
            prop.transform.position = position;
            prop.transform.localScale = scale;
            prop.isStatic = true;
            GameObjectUtility.SetStaticEditorFlags(prop,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);

            prop.GetComponent<MeshRenderer>().material = mat;

            var modifier = prop.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = 1; // Not Walkable
        }

        private static Sprite CreatePlaceholderSprite()
        {
            // Create a simple 64x64 texture — Carl silhouette (cyan-ish)
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color transparent = new Color(0, 0, 0, 0);
            Color bodyColor = new Color(0.2f, 0.7f, 0.9f); // cyan — Carl's frostbitten look
            Color outlineColor = new Color(0.1f, 0.1f, 0.15f);

            // Fill transparent
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    tex.SetPixel(x, y, transparent);

            // Draw a simple humanoid silhouette
            // Head (circle at top)
            FillCircle(tex, 32, 52, 7, bodyColor);
            FillCircle(tex, 32, 52, 8, outlineColor, true);

            // Body (rectangle)
            FillRect(tex, 24, 24, 40, 46, bodyColor);
            DrawRectOutline(tex, 23, 23, 41, 47, outlineColor);

            // Legs
            FillRect(tex, 24, 4, 31, 24, bodyColor);
            FillRect(tex, 33, 4, 40, 24, bodyColor);
            DrawRectOutline(tex, 23, 3, 32, 25, outlineColor);
            DrawRectOutline(tex, 32, 3, 41, 25, outlineColor);

            // Arms
            FillRect(tex, 16, 30, 24, 44, bodyColor);
            FillRect(tex, 40, 30, 48, 44, bodyColor);
            DrawRectOutline(tex, 15, 29, 25, 45, outlineColor);
            DrawRectOutline(tex, 39, 29, 49, 45, outlineColor);

            // Eyes (2 white dots)
            tex.SetPixel(29, 54, Color.white);
            tex.SetPixel(30, 54, Color.white);
            tex.SetPixel(34, 54, Color.white);
            tex.SetPixel(35, 54, Color.white);

            tex.Apply();

            // Save texture as asset
            string texPath = "Assets/_Project/Art/Sprites/Characters/Carl_Placeholder.png";
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, "../", texPath),
                tex.EncodeToPNG());
            AssetDatabase.Refresh();

            // Configure import settings for sprite
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            // Load the sprite back
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
            return sprite ?? Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64);
        }

        private static void FillCircle(Texture2D tex, int cx, int cy, int radius, Color color, bool outlineOnly = false)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                for (int y = cy - radius; y <= cy + radius; y++)
                {
                    if (x < 0 || x >= tex.width || y < 0 || y >= tex.height) continue;
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (outlineOnly)
                    {
                        if (dist >= radius - 0.5f && dist <= radius + 0.5f)
                            tex.SetPixel(x, y, color);
                    }
                    else
                    {
                        if (dist <= radius)
                            tex.SetPixel(x, y, color);
                    }
                }
            }
        }

        private static void FillRect(Texture2D tex, int x1, int y1, int x2, int y2, Color color)
        {
            for (int x = x1; x <= x2; x++)
                for (int y = y1; y <= y2; y++)
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                        tex.SetPixel(x, y, color);
        }

        private static void DrawRectOutline(Texture2D tex, int x1, int y1, int x2, int y2, Color color)
        {
            for (int x = x1; x <= x2; x++)
            {
                if (x >= 0 && x < tex.width)
                {
                    if (y1 >= 0 && y1 < tex.height) tex.SetPixel(x, y1, color);
                    if (y2 >= 0 && y2 < tex.height) tex.SetPixel(x, y2, color);
                }
            }
            for (int y = y1; y <= y2; y++)
            {
                if (y >= 0 && y < tex.height)
                {
                    if (x1 >= 0 && x1 < tex.width) tex.SetPixel(x1, y, color);
                    if (x2 >= 0 && x2 < tex.width) tex.SetPixel(x2, y, color);
                }
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == scenePath) return; // already added
            }

            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            for (int i = 0; i < scenes.Length; i++)
                newScenes[i] = scenes[i];
            newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
            Debug.Log($"[DCC] Added {scenePath} to Build Settings");
        }
    }
}
