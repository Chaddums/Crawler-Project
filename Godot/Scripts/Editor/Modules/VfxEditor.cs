using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// VFX Editor — edit particle parameters with live 3D preview.
    /// DataTable (left) lists all effects, PropertyInspector (right top) edits params,
    /// SubViewport (right bottom) shows live preview. Saves to vfx_config.json.
    /// </summary>
    public partial class VfxEditor : EditorPanel
    {
        public override string PanelName => "VFX";
        public override Color AccentColor => EditorStyles.AccentVfx;

        private DataTable _table;
        private PropertyInspector _inspector;
        private Label _inspectorTitle;
        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _vfxRoot;
        private Camera3D _camera;
        private Label _statusInfo;

        private Dictionary<string, Dictionary<string, object>> _configData;
        private string _selectedEffect;

        private static readonly (string name, string description)[] VfxEffects =
        {
            ("hit_physical", "Physical hit sparks"),
            ("hit_fire", "Fire damage impact"),
            ("hit_ice", "Ice freeze burst"),
            ("hit_lightning", "Electric sparks"),
            ("hit_poison", "Poison cloud"),
            ("hit_dark", "Dark damage"),
            ("death", "Enemy death burst"),
            ("heal", "Healing particles"),
            ("muzzle_flash", "Gun muzzle flash"),
            ("dash_trail", "Dash movement trail"),
            ("celebration", "Room clear celebration"),
            ("loot_burst", "Item drop burst"),
            ("arcane_circle", "Spell cast circle"),
            ("shockwave", "AoE shockwave ring"),
            ("music_notes", "Bard ability notes"),
            ("torch_fire", "Torch flame particles"),
            ("sad_puff", "Junk tier drop puff"),
        };

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
#pragma warning disable CS0618
            split.SplitOffset = 450;
#pragma warning restore CS0618

            // Left: DataTable of effects
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(350, 0);

            _table = new DataTable();
            _table.SizeFlagsVertical = SizeFlags.ExpandFill;
            _table.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.AddChild(_table);
            split.AddChild(leftPanel);

            // Right: inspector + preview
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(350, 0);

            _inspectorTitle = EditorStyles.MakeLabel("Select an effect", EditorStyles.FontHeader, EditorStyles.TextSecondary);
            rightPanel.AddChild(_inspectorTitle);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Inspector (top portion)
            var inspectorScroll = new ScrollContainer();
            inspectorScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            inspectorScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inspectorScroll.CustomMinimumSize = new Vector2(0, 200);

            _inspector = new PropertyInspector();
            _inspector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inspectorScroll.AddChild(_inspector);
            rightPanel.AddChild(inspectorScroll);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Preview viewport (bottom portion)
            var vpTitle = EditorStyles.MakeLabel("3D Preview", EditorStyles.FontSmall, AccentColor);
            rightPanel.AddChild(vpTitle);

            _viewportContainer = new SubViewportContainer();
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.Stretch = true;
            _viewportContainer.CustomMinimumSize = new Vector2(0, 250);

            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(640, 480);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;

            _camera = new Camera3D();
            _camera.Position = new Vector3(0, 3, 5);
            _viewport.AddChild(_camera);
            _camera.LookAt(Vector3.Zero);

            _vfxRoot = new Node3D();
            _viewport.AddChild(_vfxRoot);

            var ground = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(10, 10);
            ground.Mesh = planeMesh;
            var groundMat = new StandardMaterial3D();
            groundMat.AlbedoColor = new Color(0.1f, 0.1f, 0.12f);
            ground.MaterialOverride = groundMat;
            _viewport.AddChild(ground);

            var light = new DirectionalLight3D();
            light.RotationDegrees = new Vector3(-65, 45, 0);
            _viewport.AddChild(light);

            _viewportContainer.AddChild(_viewport);
            rightPanel.AddChild(_viewportContainer);

            // Respawn button
            var respawnBtn = EditorStyles.MakeButton("Respawn Preview", EditorStyles.FontSmall, AccentColor);
            respawnBtn.CustomMinimumSize = new Vector2(0, 24);
            respawnBtn.Pressed += () => RespawnPreview();
            rightPanel.AddChild(respawnBtn);

            _statusInfo = EditorStyles.MakeLabel("Select an effect to edit", EditorStyles.FontSmall, EditorStyles.TextMuted);
            rightPanel.AddChild(_statusInfo);

            split.AddChild(rightPanel);
            content.AddChild(split);
        }

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(WireEvents));
        }

        private void WireEvents()
        {
            _table.OnRowSelected += OnEffectSelected;
            _inspector.OnValueChanged += OnPropertyChanged;
            LoadConfig();
        }

        private void OnEffectSelected(string key, Dictionary<string, object> rowData)
        {
            _selectedEffect = key;
            _inspectorTitle.Text = key;
            _inspectorTitle.AddThemeColorOverride("font_color", AccentColor);

            var detailData = new Dictionary<string, object>(_configData[key]);
            _inspector.Build(detailData, GetVfxHints());

            RespawnPreview();
        }

        private void OnPropertyChanged(string property, object value)
        {
            if (_selectedEffect == null || _configData == null) return;

            if (_configData.TryGetValue(_selectedEffect, out var row))
            {
                row[property] = value;
                _table.UpdateRow(_selectedEffect, row);
            }

            MarkDirty();
            RespawnPreview();
        }

        private void RespawnPreview()
        {
            if (_vfxRoot == null || _selectedEffect == null) return;

            // Clear existing
            var children = _vfxRoot.GetChildren();
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                _vfxRoot.RemoveChild(child);
                child.Free();
            }

            // Spawn from config
            if (!_configData.TryGetValue(_selectedEffect, out var cfg)) return;

            var particles = new GpuParticles3D();
            particles.Amount = GetInt(cfg, "Amount", 12);
            particles.OneShot = true;
            particles.Explosiveness = GetFloat(cfg, "Explosiveness", 0.9f);
            particles.Lifetime = GetFloat(cfg, "Lifetime", 0.5f);
            particles.SpeedScale = GetFloat(cfg, "SpeedScale", 1.5f);

            // Shared draw pass (small sphere billboard)
            var drawMesh = new SphereMesh();
            drawMesh.Radius = 0.06f;
            drawMesh.Height = 0.12f;
            drawMesh.RadialSegments = 4;
            drawMesh.Rings = 2;
            var drawMat = new StandardMaterial3D();
            drawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            drawMat.AlbedoColor = Colors.White;
            drawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            drawMesh.Material = drawMat;
            particles.DrawPass1 = drawMesh;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = GetFloat(cfg, "Spread", 180f);
            mat.InitialVelocityMin = GetFloat(cfg, "InitialVelocityMin", 3f);
            mat.InitialVelocityMax = GetFloat(cfg, "InitialVelocityMax", 6f);
            mat.Gravity = new Vector3(0, GetFloat(cfg, "Gravity", -8f), 0);
            mat.ScaleMin = GetFloat(cfg, "ScaleMin", 0.5f);
            mat.ScaleMax = GetFloat(cfg, "ScaleMax", 1.5f);

            var color = GetColor(cfg);
            mat.Color = color;

            var colorRamp = new GradientTexture1D();
            var gradient = new Gradient();
            gradient.SetColor(0, color);
            gradient.SetColor(1, new Color(color.R, color.G, color.B, 0));
            colorRamp.Gradient = gradient;
            mat.ColorRamp = colorRamp;

            particles.ProcessMaterial = mat;
            particles.Position = Vector3.Up * 1f;
            particles.Emitting = true;

            _vfxRoot.AddChild(particles);
            _statusInfo.Text = $"Preview: {_selectedEffect}";
            _statusInfo.AddThemeColorOverride("font_color", AccentColor);
        }

        private void LoadConfig()
        {
            _configData = new();

            var json = LoadJson("res://Data/vfx_config.json");
            if (json != null)
            {
                foreach (var kvp in json)
                {
                    if (kvp.Value is Dictionary<string, object> entry)
                        _configData[kvp.Key] = entry;
                }
            }

            // Ensure all effects exist with defaults
            foreach (var (name, _) in VfxEffects)
            {
                if (!_configData.ContainsKey(name))
                    _configData[name] = GetDefaultConfig(name);
            }

            var columns = new[] { "Amount", "Lifetime", "SpeedScale" };
            _table.SetData(columns, _configData);
        }

        private static Dictionary<string, object> GetDefaultConfig(string effectName)
        {
            return effectName switch
            {
                "hit_physical" => MakeConfig(12, 0.3f, 2f, 0.9f, 1f, 0.9f, 0.8f, 180f, 3f, 6f, -8f, 0.5f, 1.5f),
                "hit_fire" => MakeConfig(18, 0.35f, 1.5f, 0.9f, 1f, 0.4f, 0.1f, 180f, 4f, 8f, -6f, 0.6f, 1.8f),
                "hit_ice" => MakeConfig(16, 0.4f, 1.5f, 0.85f, 0.5f, 0.85f, 1f, 160f, 2f, 5f, -2f, 0.4f, 1.2f),
                "hit_lightning" => MakeConfig(14, 0.25f, 3f, 0.95f, 0.7f, 0.85f, 1f, 180f, 4f, 8f, -4f, 0.3f, 0.8f),
                "hit_poison" => MakeConfig(24, 2f, 0.6f, 0.3f, 0.3f, 0.9f, 0.2f, 120f, 0.3f, 0.6f, -0.5f, 0.8f, 2f),
                "hit_dark" => MakeConfig(12, 0.3f, 2f, 0.9f, 0.5f, 0.2f, 0.7f, 180f, 3f, 6f, -8f, 0.5f, 1.5f),
                "death" => MakeConfig(24, 0.6f, 1.5f, 0.95f, 1f, 0.4f, 0.1f, 180f, 4f, 8f, -5f, 0.8f, 2f),
                "heal" => MakeConfig(20, 0.8f, 1f, 0.5f, 0.2f, 1f, 0.4f, 180f, 1f, 2f, 0.5f, 0.3f, 0.8f),
                "muzzle_flash" => MakeConfig(8, 0.12f, 3f, 0.95f, 1f, 0.85f, 0.3f, 30f, 5f, 10f, 0f, 0.5f, 1.5f),
                "dash_trail" => MakeConfig(12, 0.4f, 1f, 0.3f, 0.4f, 0.7f, 1f, 60f, 1f, 2f, -1f, 0.3f, 0.6f),
                "celebration" => MakeConfig(30, 1.2f, 1.5f, 0.8f, 1f, 0.85f, 0.3f, 180f, 4f, 8f, -4f, 0.5f, 1.5f),
                "loot_burst" => MakeConfig(16, 0.5f, 1.5f, 0.9f, 1f, 0.85f, 0.3f, 120f, 3f, 5f, -6f, 0.4f, 1.0f),
                "arcane_circle" => MakeConfig(12, 0.6f, 1f, 0.5f, 0.4f, 0.6f, 1f, 180f, 1f, 2f, 0f, 0.3f, 0.6f),
                "shockwave" => MakeConfig(16, 0.3f, 2f, 0.95f, 1f, 0.8f, 0.3f, 180f, 5f, 10f, -2f, 0.3f, 1f),
                "music_notes" => MakeConfig(8, 0.8f, 1f, 0.3f, 0.6f, 0.3f, 0.9f, 90f, 1f, 2f, 1f, 0.5f, 1.2f),
                "torch_fire" => MakeConfig(8, 0.6f, 1f, 0.3f, 1f, 0.5f, 0.1f, 15f, 1f, 2f, 1f, 0.3f, 0.8f),
                "sad_puff" => MakeConfig(6, 0.5f, 1f, 0.8f, 0.4f, 0.4f, 0.45f, 90f, 1f, 2f, -4f, 0.5f, 1f),
                _ => MakeConfig(12, 0.3f, 1.5f, 0.9f, 1f, 1f, 1f, 180f, 3f, 6f, -8f, 0.5f, 1.5f),
            };
        }

        private static Dictionary<string, object> MakeConfig(
            int amount, float lifetime, float speedScale, float explosiveness,
            float colorR, float colorG, float colorB,
            float spread, float velMin, float velMax, float gravity,
            float scaleMin, float scaleMax)
        {
            return new Dictionary<string, object>
            {
                ["Amount"] = (double)amount,
                ["Lifetime"] = (double)lifetime,
                ["SpeedScale"] = (double)speedScale,
                ["Explosiveness"] = (double)explosiveness,
                ["ColorR"] = (double)colorR,
                ["ColorG"] = (double)colorG,
                ["ColorB"] = (double)colorB,
                ["Spread"] = (double)spread,
                ["InitialVelocityMin"] = (double)velMin,
                ["InitialVelocityMax"] = (double)velMax,
                ["Gravity"] = (double)gravity,
                ["ScaleMin"] = (double)scaleMin,
                ["ScaleMax"] = (double)scaleMax,
            };
        }

        private static Dictionary<string, PropertyInspector.PropertyHint> GetVfxHints()
        {
            return new()
            {
                ["Amount"] = new() { Min = 1, Max = 100, Step = 1 },
                ["Lifetime"] = new() { Min = 0.1f, Max = 5f, Step = 0.05f },
                ["SpeedScale"] = new() { Min = 0.5f, Max = 5f, Step = 0.1f },
                ["Explosiveness"] = new() { Min = 0f, Max = 1f, Step = 0.05f },
                ["ColorR"] = new() { Min = 0f, Max = 1f, Step = 0.01f },
                ["ColorG"] = new() { Min = 0f, Max = 1f, Step = 0.01f },
                ["ColorB"] = new() { Min = 0f, Max = 1f, Step = 0.01f },
                ["Spread"] = new() { Min = 0f, Max = 180f, Step = 1f },
                ["InitialVelocityMin"] = new() { Min = 0f, Max = 20f, Step = 0.5f },
                ["InitialVelocityMax"] = new() { Min = 0f, Max = 20f, Step = 0.5f },
                ["Gravity"] = new() { Min = -20f, Max = 20f, Step = 0.5f },
                ["ScaleMin"] = new() { Min = 0.1f, Max = 5f, Step = 0.1f },
                ["ScaleMax"] = new() { Min = 0.1f, Max = 5f, Step = 0.1f },
            };
        }

        private static float GetFloat(Dictionary<string, object> cfg, string key, float fallback)
        {
            if (cfg.TryGetValue(key, out var v)) return Convert.ToSingle(v);
            return fallback;
        }

        private static int GetInt(Dictionary<string, object> cfg, string key, int fallback)
        {
            if (cfg.TryGetValue(key, out var v)) return Convert.ToInt32(v);
            return fallback;
        }

        private static Color GetColor(Dictionary<string, object> cfg)
        {
            float r = GetFloat(cfg, "ColorR", 1f);
            float g = GetFloat(cfg, "ColorG", 1f);
            float b = GetFloat(cfg, "ColorB", 1f);
            return new Color(r, g, b);
        }

        // ===== SAVE / RELOAD =====

        protected override void Reload()
        {
            if (_table == null) return;
            LoadConfig();
            MarkClean();
            SetStatus($"Loaded {_configData.Count} effects", EditorStyles.StatusSaved);
        }

        protected override void Save()
        {
            if (_configData == null) return;
            if (SaveJson("res://Data/vfx_config.json", _configData))
            {
                VfxConfig.Reload();
                MarkClean();
                SetStatus("Saved vfx_config.json", EditorStyles.StatusSaved);
                GD.Print("[VfxEditor] Saved vfx_config.json");
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot)
        {
            var parsed = MiniJson.Deserialize(jsonSnapshot) as Dictionary<string, object>;
            if (parsed == null) return;

            _configData = new();
            foreach (var kvp in parsed)
            {
                if (kvp.Value is Dictionary<string, object> entry)
                    _configData[kvp.Key] = entry;
            }

            var columns = new[] { "Amount", "Lifetime", "SpeedScale" };
            _table.SetData(columns, _configData);
        }
    }
}
