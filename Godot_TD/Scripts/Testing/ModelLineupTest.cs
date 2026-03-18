using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Visual diagnostic: loads every character model side by side with reference
    /// boxes so you can verify they're all correctly normalized.
    ///
    /// Run: add this as a Node3D to any scene, or create ModelLineupTest.tscn
    /// and press F5 with it as the main scene.
    ///
    /// Reference box = original procedural enemy size (0.7 units tall, dark gray).
    /// Green line  = 1 cell height (2 units).
    /// Yellow line = target standard enemy height (1.2 units).
    /// </summary>
    public partial class ModelLineupTest : Node3D
    {
        private const float SPACING = 4f; // Units between models
        private bool _measured;
        private int _frameCount;

        // All character models to test
        private static readonly (string Path, string Label, float TargetHeight)[] _models = {
            // Enemies
            (AssetLibrary.ENEMY_SCRAP_RAT,   "scrap_rat",   Constants.ENEMY_HEIGHT_STANDARD),
            (AssetLibrary.ENEMY_WIRE_WORM,    "wire_worm",   Constants.ENEMY_HEIGHT_STANDARD),
            (AssetLibrary.ENEMY_TRILOBITE,    "trilobite",   Constants.ENEMY_HEIGHT_STANDARD),
            (AssetLibrary.ENEMY_QUAD_SHELL,   "quad_shell",  Constants.ENEMY_HEIGHT_LARGE),
            (AssetLibrary.ENEMY_SPARK_DRONE,  "spark_drone", Constants.ENEMY_HEIGHT_SMALL),
            (AssetLibrary.ENEMY_DECOY,        "decoy_unit",  Constants.ENEMY_HEIGHT_STANDARD),
            // Player
            (AssetLibrary.PLAYER_CLUNKER,     "clunker",     Constants.PLAYER_HEIGHT),
            (AssetLibrary.PLAYER_RUSTBUCKET,  "rustbucket",  Constants.PLAYER_HEIGHT),
            (AssetLibrary.PLAYER_SPARKPLUG,   "sparkplug",   Constants.PLAYER_HEIGHT),
            // Companion
            (AssetLibrary.COMPANION_BIT,      "bit",         Constants.PLAYER_HEIGHT),
            // AXIS
            (AssetLibrary.AXIS_EYE_DRONE,     "eye_drone",   Constants.ENEMY_HEIGHT_STANDARD),
        };

        private readonly List<(Node3D Model, string Label, float Target)> _spawned = new();

        public override void _Ready()
        {
            GD.Print("=== MODEL LINEUP TEST ===");
            GD.Print($"Cell size: {Constants.VINE_CELL_SIZE}");
            GD.Print($"Standard enemy height: {Constants.ENEMY_HEIGHT_STANDARD}");
            GD.Print($"Small enemy height: {Constants.ENEMY_HEIGHT_SMALL}");
            GD.Print($"Large enemy height: {Constants.ENEMY_HEIGHT_LARGE}");
            GD.Print($"Player height: {Constants.PLAYER_HEIGHT}");
            GD.Print("");

            BuildFloor();
            BuildCamera();
            BuildLighting();

            float x = 0f;

            // First: reference procedural box (the "original" enemy)
            SpawnReferenceBox(x, "REF BOX\n(0.7u tall)");
            x += SPACING;

            // Load each model
            for (int i = 0; i < _models.Length; i++)
            {
                var (path, label, target) = _models[i];
                SpawnModel(path, label, target, x);
                x += SPACING;
            }

            // Height reference lines
            BuildHeightMarkers(x);
        }

        public override void _Process(double delta)
        {
            // Wait 2 frames for transforms to propagate, then measure
            _frameCount++;
            if (!_measured && _frameCount >= 3)
            {
                _measured = true;
                MeasureAll();
            }
        }

        private void SpawnModel(string path, string label, float targetHeight, float x)
        {
            if (!ResourceLoader.Exists(path))
            {
                GD.PrintErr($"[Lineup] MISSING: {path}");
                SpawnMissingMarker(x, label);
                return;
            }

            // Load raw (no normalization) to see native size
            var raw = AssetLibrary.Instantiate(path);
            if (raw == null)
            {
                SpawnMissingMarker(x, label);
                return;
            }

            // Print node hierarchy for first model
            GD.Print($"\n--- {label} ({path}) ---");
            PrintHierarchy(raw, 0);

            // Get the AABB BEFORE normalization (to see what native looks like)
            var rawAabb = AssetLibrary.GetCombinedAABB(raw);
            GD.Print($"  Raw AABB (with internal transforms): size={rawAabb.Size}, height={rawAabb.Size.Y:F4}");

            // Clean up raw
            raw.QueueFree();

            // Now load normalized
            var model = AssetLibrary.InstantiateNormalized(path);
            if (model == null)
            {
                SpawnMissingMarker(x, label);
                return;
            }

            // Place in scene
            var holder = new Node3D();
            holder.Position = new Vector3(x, 0, 0);
            AddChild(holder);
            holder.AddChild(model);
            AssetLibrary.GroundModel(model);

            GD.Print($"  Final model scale: {model.Scale}");
            GD.Print($"  Target height: {targetHeight}");

            _spawned.Add((model, label, targetHeight));

            // Label
            SpawnLabel(x, label + $"\ntgt={targetHeight:F1}");
        }

        private void MeasureAll()
        {
            GD.Print("\n=== POST-TREE MEASUREMENTS ===");
            foreach (var (model, label, target) in _spawned)
            {
                if (model == null || !IsInstanceValid(model)) continue;
                var aabb = AssetLibrary.GetCombinedAABB(model);
                float actualHeight = aabb.Size.Y * model.Scale.Y;
                GD.Print($"  {label}: AABB.Y={aabb.Size.Y:F4}, scale={model.Scale.Y:F4}, " +
                         $"actualHeight={actualHeight:F4}, target={target:F2}, " +
                         $"ratio={actualHeight / target:F3}x");
            }
            GD.Print("=== END MEASUREMENTS ===\n");
        }

        private static void PrintHierarchy(Node node, int depth)
        {
            string indent = new string(' ', depth * 2);
            string info = $"{indent}{node.GetType().Name}: \"{node.Name}\"";

            if (node is Node3D n3d)
            {
                var t = n3d.Transform;
                if (t.Basis.Scale != Vector3.One)
                    info += $" scale={t.Basis.Scale}";
                if (t.Origin != Vector3.Zero)
                    info += $" pos={t.Origin}";
            }

            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                var aabb = mesh.GetAabb();
                info += $" meshAABB={aabb.Size} (h={aabb.Size.Y:F3})";
            }

            GD.Print(info);

            foreach (var child in node.GetChildren())
                PrintHierarchy(child, depth + 1);
        }

        private void SpawnReferenceBox(float x, string label)
        {
            // Original procedural enemy: BoxMesh 0.7 units per side
            float size = 0.7f;
            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = new Vector3(size, size, size) };
            mesh.Position = new Vector3(x, size * 0.5f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 0.3f, 0.35f);
            mat.Roughness = 0.8f;
            mesh.MaterialOverride = mat;
            AddChild(mesh);

            SpawnLabel(x, label);
        }

        private void SpawnMissingMarker(float x, string label)
        {
            var mesh = new MeshInstance3D();
            mesh.Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f };
            mesh.Position = new Vector3(x, 0.3f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = Colors.Red;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mesh.MaterialOverride = mat;
            AddChild(mesh);

            SpawnLabel(x, $"MISSING\n{label}");
        }

        private void SpawnLabel(float x, string text)
        {
            var label = new Label3D();
            label.Text = text;
            label.Position = new Vector3(x, -0.3f, 1.2f);
            label.FontSize = 48;
            label.PixelSize = 0.01f;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = Colors.White;
            AddChild(label);
        }

        private void BuildHeightMarkers(float totalWidth)
        {
            // Height reference lines at key heights
            (float height, Color color, string label)[] markers = {
                (Constants.ENEMY_HEIGHT_SMALL,    new Color(0.5f, 0.8f, 1f), $"Small: {Constants.ENEMY_HEIGHT_SMALL}u"),
                (Constants.ENEMY_HEIGHT_STANDARD, new Color(1f, 0.9f, 0.2f), $"Standard: {Constants.ENEMY_HEIGHT_STANDARD}u"),
                (Constants.PLAYER_HEIGHT,         new Color(0.2f, 0.7f, 1f), $"Player: {Constants.PLAYER_HEIGHT}u"),
                (Constants.ENEMY_HEIGHT_LARGE,    new Color(1f, 0.5f, 0.2f), $"Large: {Constants.ENEMY_HEIGHT_LARGE}u"),
                (Constants.VINE_CELL_SIZE,        new Color(0.2f, 0.9f, 0.2f), $"Cell: {Constants.VINE_CELL_SIZE}u"),
            };

            foreach (var (height, color, label) in markers)
            {
                // Thin line spanning all models
                var line = new MeshInstance3D();
                line.Mesh = new BoxMesh { Size = new Vector3(totalWidth + 2f, 0.02f, 0.02f) };
                line.Position = new Vector3(totalWidth * 0.5f - 2f, height, -0.5f);

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = color;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.EmissionEnabled = true;
                mat.Emission = color;
                mat.EmissionEnergyMultiplier = 1.5f;
                line.MaterialOverride = mat;
                AddChild(line);

                // Label for the line
                var lbl = new Label3D();
                lbl.Text = label;
                lbl.Position = new Vector3(-3f, height + 0.1f, -0.5f);
                lbl.FontSize = 36;
                lbl.PixelSize = 0.01f;
                lbl.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                lbl.Modulate = color;
                AddChild(lbl);
            }
        }

        private void BuildFloor()
        {
            var floor = new MeshInstance3D();
            floor.Mesh = new PlaneMesh { Size = new Vector2(80, 20) };
            floor.Position = new Vector3(20, -0.01f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.08f, 0.08f, 0.1f);
            mat.Roughness = 0.9f;
            floor.MaterialOverride = mat;
            AddChild(floor);

            // Grid lines every 2 units (cell size)
            for (float gx = 0; gx <= 60; gx += Constants.VINE_CELL_SIZE)
            {
                var gridLine = new MeshInstance3D();
                gridLine.Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.005f, 20) };
                gridLine.Position = new Vector3(gx, 0.001f, 0);

                var gmat = new StandardMaterial3D();
                gmat.AlbedoColor = new Color(0.15f, 0.15f, 0.2f);
                gmat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                gridLine.MaterialOverride = gmat;
                AddChild(gridLine);
            }
        }

        private void BuildCamera()
        {
            var cam = new Camera3D();
            float totalWidth = (_models.Length + 1) * SPACING;
            cam.Position = new Vector3(totalWidth * 0.5f, 3f, 10f);
            cam.LookAt(new Vector3(totalWidth * 0.5f, 1f, 0), Vector3.Up);
            cam.Fov = 50f;
            cam.Current = true;
            AddChild(cam);
        }

        private void BuildLighting()
        {
            var light = new DirectionalLight3D();
            light.Rotation = new Vector3(Mathf.DegToRad(-45), Mathf.DegToRad(30), 0);
            light.LightColor = Colors.White;
            light.LightEnergy = 1.5f;
            light.ShadowEnabled = true;
            AddChild(light);

            // Ambient fill
            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.05f, 0.05f, 0.08f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.3f, 0.3f, 0.35f);
            envRes.AmbientLightEnergy = 0.8f;
            env.Environment = envRes;
            AddChild(env);
        }
    }
}
