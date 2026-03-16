using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// VFX Editor — edit particle parameters with live 3D preview.
    /// DataTable (left) lists all effects grouped by category,
    /// custom inspector (right top) edits params with color wheel,
    /// SubViewport (right bottom) shows live preview. Saves to vfx_config.json.
    /// </summary>
    public partial class VfxEditor : EditorPanel
    {
        public override string PanelName => "VFX";
        public override Color AccentColor => EditorStyles.AccentVfx;

        private DataTable _table;
        private VBoxContainer _inspectorFields;
        private ScrollContainer _inspectorScroll;
        private Label _inspectorTitle;
        private Label _inspectorDesc;
        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _vfxRoot;
        private Camera3D _camera;
        private Label _statusInfo;
        private ColorPickerButton _colorPicker;

        private Dictionary<string, Dictionary<string, object>> _configData;
        private string _selectedEffect;

        // ═══════════════════════════════════════════════════════════════
        //  COMPLETE VFX CATALOG — every effect in the game
        // ═══════════════════════════════════════════════════════════════

        private static readonly (string name, string category, string description)[] VfxEffects =
        {
            // Combat hits
            ("hit_physical",       "Combat",       "Physical hit sparks"),
            ("hit_fire",           "Combat",       "Fire damage impact"),
            ("hit_ice",            "Combat",       "Ice freeze burst"),
            ("hit_lightning",      "Combat",       "Electric sparks"),
            ("hit_poison",         "Combat",       "Poison cloud"),
            ("hit_dark",           "Combat",       "Dark damage"),
            ("death",              "Combat",       "Enemy death burst"),
            ("impact_burst",       "Combat",       "Projectile impact"),
            ("freeze_burst",       "Combat",       "Ice crystal burst"),
            ("electric_sparks",    "Combat",       "Lightning/stun sparks"),
            ("poison_cloud",       "Combat",       "Lingering AoE cloud"),
            ("melee_slash",        "Combat",       "Melee sweep crescent"),
            ("stun_indicator",     "Combat",       "Orbiting stun sparks"),
            ("aura_ring",          "Combat",       "Ring-shaped aura"),

            // Abilities
            ("muzzle_flash",       "Ability",      "Gun muzzle flash"),
            ("dash_trail",         "Ability",      "Dash movement trail"),
            ("heal",               "Ability",      "Healing particles"),
            ("arcane_circle",      "Ability",      "Spell cast circle"),
            ("shockwave",          "Ability",      "AoE shockwave ring"),
            ("music_notes",        "Ability",      "Bard ability notes"),
            ("aoe_indicator",      "Ability",      "Ground ring AoE preview"),
            ("lightning_arc",      "Ability",      "Jagged bolt between points"),

            // Celebration
            ("celebration",        "Celebration",  "Room clear celebration"),
            ("celebration_burst",  "Celebration",  "Configurable color burst"),
            ("confetti_storm",     "Celebration",  "Rain-down confetti"),
            ("orbiting_sparkles",  "Celebration",  "POE2-style glow ring"),
            ("ground_sparks",      "Celebration",  "Scattered floor sparks"),
            ("sad_puff",           "Celebration",  "Junk tier drop puff"),

            // Loot
            ("loot_burst",         "Loot",         "Item drop burst"),
            ("pickup_trail",       "Loot",         "Item collect trail"),
            ("light_pillar",       "Loot",         "Tall emissive pillar"),
            ("rarity_aura",        "Loot",         "Persistent item glow"),

            // Environment
            ("torch_fire",         "Environment",  "Torch flame particles"),
            ("portal_particles",   "Environment",  "Rotating portal swirl"),
            ("ambient_particles",  "Environment",  "Slow floating motes"),
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
            _inspectorDesc = EditorStyles.MakeLabel("", EditorStyles.FontTiny, EditorStyles.TextMuted);
            rightPanel.AddChild(_inspectorDesc);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Custom inspector with color wheel (top portion)
            _inspectorScroll = new ScrollContainer();
            _inspectorScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _inspectorScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _inspectorScroll.CustomMinimumSize = new Vector2(0, 220);

            _inspectorFields = new VBoxContainer();
            _inspectorFields.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _inspectorFields.AddThemeConstantOverride("separation", 4);
            _inspectorScroll.AddChild(_inspectorFields);
            rightPanel.AddChild(_inspectorScroll);

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
            _camera.Transform = _camera.Transform.LookingAt(Vector3.Zero, Vector3.Up);
            _viewport.AddChild(_camera);

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

            // Lighting
            var light = new DirectionalLight3D();
            light.RotationDegrees = new Vector3(-65, 45, 0);
            light.LightEnergy = 1.5f;
            _viewport.AddChild(light);

            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.05f, 0.05f, 0.08f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.3f, 0.3f, 0.35f);
            envRes.AmbientLightEnergy = 0.8f;
            env.Environment = envRes;
            _viewport.AddChild(env);

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
            LoadConfig();
        }

        // ═══════════════════════════════════════════════════════════════
        //  SELECTION & EDITING
        // ═══════════════════════════════════════════════════════════════

        private void OnEffectSelected(string key, Dictionary<string, object> rowData)
        {
            _selectedEffect = key;
            var entry = VfxEffects.FirstOrDefault(e => e.name == key);
            _inspectorTitle.Text = key;
            _inspectorTitle.AddThemeColorOverride("font_color", GetCategoryColor(entry.category ?? ""));
            _inspectorDesc.Text = entry.description ?? "";

            RebuildInspector();
            RespawnPreview();
        }

        private void RebuildInspector()
        {
            foreach (var child in _inspectorFields.GetChildren())
                if (child is Node n) n.QueueFree();

            if (_selectedEffect == null || !_configData.TryGetValue(_selectedEffect, out var cfg)) return;

            // Color wheel
            var colorRow = new HBoxContainer();
            colorRow.AddThemeConstantOverride("separation", 8);
            var colorLabel = EditorStyles.MakeLabel("Color", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            colorRow.AddChild(colorLabel);

            _colorPicker = new ColorPickerButton();
            _colorPicker.CustomMinimumSize = new Vector2(120, 28);
            _colorPicker.Color = GetColor(cfg);
            _colorPicker.EditAlpha = false;
            _colorPicker.ColorChanged += OnColorChanged;
            colorRow.AddChild(_colorPicker);

            // Color preset buttons
            var presetRow = new HBoxContainer();
            presetRow.AddThemeConstantOverride("separation", 2);
            AddColorPreset(presetRow, "Fire", new Color(1f, 0.4f, 0.1f));
            AddColorPreset(presetRow, "Ice", new Color(0.5f, 0.85f, 1f));
            AddColorPreset(presetRow, "Zap", new Color(0.7f, 0.85f, 1f));
            AddColorPreset(presetRow, "Poison", new Color(0.3f, 0.9f, 0.2f));
            AddColorPreset(presetRow, "Dark", new Color(0.5f, 0.2f, 0.7f));
            AddColorPreset(presetRow, "Gold", new Color(1f, 0.85f, 0.3f));
            AddColorPreset(presetRow, "Heal", new Color(0.2f, 1f, 0.4f));

            _inspectorFields.AddChild(colorRow);
            _inspectorFields.AddChild(presetRow);
            _inspectorFields.AddChild(EditorStyles.MakeSeparator());

            // Particle properties
            AddSpinProperty("Amount", cfg, 1, 100, 1);
            AddSpinProperty("Lifetime", cfg, 0.05f, 10f, 0.05f);
            AddSpinProperty("SpeedScale", cfg, 0.1f, 5f, 0.1f);
            AddSpinProperty("Explosiveness", cfg, 0f, 1f, 0.05f);

            _inspectorFields.AddChild(EditorStyles.MakeSeparator());
            _inspectorFields.AddChild(EditorStyles.MakeLabel("Motion", EditorStyles.FontSmall, AccentColor));

            AddSpinProperty("Spread", cfg, 0f, 180f, 1f);
            AddSpinProperty("InitialVelocityMin", cfg, 0f, 20f, 0.5f);
            AddSpinProperty("InitialVelocityMax", cfg, 0f, 20f, 0.5f);
            AddSpinProperty("Gravity", cfg, -20f, 20f, 0.5f);

            _inspectorFields.AddChild(EditorStyles.MakeSeparator());
            _inspectorFields.AddChild(EditorStyles.MakeLabel("Scale", EditorStyles.FontSmall, AccentColor));

            AddSpinProperty("ScaleMin", cfg, 0.05f, 5f, 0.05f);
            AddSpinProperty("ScaleMax", cfg, 0.05f, 5f, 0.05f);

            // Duration (for mesh/tween VFX)
            var entry = VfxEffects.FirstOrDefault(e => e.name == _selectedEffect);
            if (IsMeshVfx(entry.name))
            {
                _inspectorFields.AddChild(EditorStyles.MakeSeparator());
                _inspectorFields.AddChild(EditorStyles.MakeLabel("Mesh / Tween", EditorStyles.FontSmall, AccentColor));
                AddSpinProperty("Duration", cfg, 0.1f, 5f, 0.05f);
                AddSpinProperty("MeshScale", cfg, 0.1f, 10f, 0.1f);
            }
        }

        private void AddSpinProperty(string propName, Dictionary<string, object> cfg, float min, float max, float step)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var label = EditorStyles.MakeLabel(propName, EditorStyles.FontSmall, EditorStyles.TextSecondary);
            label.CustomMinimumSize = new Vector2(130, 0);
            row.AddChild(label);

            var spin = new SpinBox();
            spin.MinValue = min;
            spin.MaxValue = max;
            spin.Step = step;
            spin.Value = GetFloat(cfg, propName, (min + max) / 2f);
            spin.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            string captured = propName;
            spin.ValueChanged += v =>
            {
                if (_restoringSnapshot) return;
                if (_selectedEffect != null && _configData.TryGetValue(_selectedEffect, out var data))
                {
                    PushUndo(MiniJsonWriter.Serialize(_configData));
                    data[captured] = (double)v;
                    _table.UpdateRow(_selectedEffect, data);
                    MarkDirty();
                    RespawnPreview();
                }
            };
            row.AddChild(spin);
            _inspectorFields.AddChild(row);
        }

        private void AddColorPreset(HBoxContainer parent, string label, Color color)
        {
            var btn = new Button();
            btn.Text = label;
            btn.AddThemeFontSizeOverride("font_size", 9);
            btn.CustomMinimumSize = new Vector2(36, 20);
            btn.Pressed += () =>
            {
                if (_colorPicker != null) _colorPicker.Color = color;
                OnColorChanged(color);
            };
            parent.AddChild(btn);
        }

        private void OnColorChanged(Color color)
        {
            if (_restoringSnapshot) return;
            if (_selectedEffect == null || !_configData.TryGetValue(_selectedEffect, out var cfg)) return;
            PushUndo(MiniJsonWriter.Serialize(_configData));
            cfg["ColorR"] = (double)color.R;
            cfg["ColorG"] = (double)color.G;
            cfg["ColorB"] = (double)color.B;
            _table.UpdateRow(_selectedEffect, cfg);
            MarkDirty();
            RespawnPreview();
        }

        // ═══════════════════════════════════════════════════════════════
        //  LIVE PREVIEW
        // ═══════════════════════════════════════════════════════════════

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

            if (!_configData.TryGetValue(_selectedEffect, out var cfg)) return;

            var color = GetColor(cfg);

            // Try spawning via VfxFactory for mesh-based effects
            if (TrySpawnFactoryPreview(_selectedEffect, color, cfg))
            {
                _statusInfo.Text = $"Preview: {_selectedEffect} (factory)";
                _statusInfo.AddThemeColorOverride("font_color", AccentColor);
                return;
            }

            // Default: particle preview from config values
            SpawnParticlePreview(cfg, color);
            _statusInfo.Text = $"Preview: {_selectedEffect}";
            _statusInfo.AddThemeColorOverride("font_color", AccentColor);
        }

        private bool TrySpawnFactoryPreview(string fx, Color color, Dictionary<string, object> cfg)
        {
            Node3D node = null;
            try
            {
                node = fx switch
                {
                    "arcane_circle" => VfxFactory.CreateArcaneCircle(color),
                    "shockwave" => VfxFactory.CreateShockwaveRing(color),
                    "melee_slash" => VfxFactory.CreateMeleeSlashArc(color, Vector3.Forward),
                    "stun_indicator" => VfxFactory.CreateStunIndicator(),
                    "aura_ring" => VfxFactory.CreateAuraRing(color, GetFloat(cfg, "MeshScale", 2f)),
                    "aoe_indicator" => VfxFactory.CreateAoEIndicator(color, GetFloat(cfg, "MeshScale", 2f), GetFloat(cfg, "Duration", 0.6f)),
                    "lightning_arc" => VfxFactory.CreateLightningArc(new Vector3(-1, 1, 0), new Vector3(1, 2, 0), color),
                    "light_pillar" => VfxFactory.CreateLightPillar(ItemRarity.Epic),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[VfxEditor] Factory preview failed for {fx}: {ex.Message}");
            }

            if (node == null) return false;

            node.Position = Vector3.Up * 1f;
            _vfxRoot.AddChild(node);
            return true;
        }

        private void SpawnParticlePreview(Dictionary<string, object> cfg, Color color)
        {
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
        }

        // ═══════════════════════════════════════════════════════════════
        //  CONFIG LOAD / SAVE
        // ═══════════════════════════════════════════════════════════════

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
            foreach (var (name, _, _) in VfxEffects)
            {
                if (!_configData.ContainsKey(name))
                    _configData[name] = GetDefaultConfig(name);
            }

            var columns = new[] { "Category", "Amount", "Lifetime", "SpeedScale" };
            _table.SetData(columns, _configData);
        }

        protected override void Reload()
        {
            if (_table == null) return;
            LoadConfig();
            MarkClean();
            SetStatus($"Loaded {_configData.Count} effects", EditorStyles.StatusSaved);
            PushInitialState(MiniJsonWriter.Serialize(_configData));
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

            var columns = new[] { "Category", "Amount", "Lifetime", "SpeedScale" };
            _table.SetData(columns, _configData);
        }

        // ═══════════════════════════════════════════════════════════════
        //  DEFAULT CONFIGS
        // ═══════════════════════════════════════════════════════════════

        private static bool IsMeshVfx(string name)
        {
            return name is "arcane_circle" or "shockwave" or "melee_slash" or "stun_indicator"
                or "aura_ring" or "aoe_indicator" or "lightning_arc" or "light_pillar";
        }

        private static Color GetCategoryColor(string category)
        {
            return category switch
            {
                "Combat" => new Color(0.9f, 0.3f, 0.3f),
                "Ability" => new Color(0.3f, 0.6f, 1f),
                "Celebration" => new Color(1f, 0.85f, 0.3f),
                "Loot" => new Color(0.3f, 0.9f, 0.5f),
                "Environment" => new Color(0.6f, 0.8f, 0.6f),
                _ => new Color(0.7f, 0.7f, 0.7f),
            };
        }

        private Dictionary<string, object> GetDefaultConfig(string effectName)
        {
            // Find category for this effect
            var entry = VfxEffects.FirstOrDefault(e => e.name == effectName);
            string cat = entry.category ?? "Combat";

            var cfg = effectName switch
            {
                // Combat
                "hit_physical"     => MakeConfig(12, 0.3f, 2f, 0.9f, 1f, 0.9f, 0.8f, 180f, 3f, 6f, -8f, 0.5f, 1.5f),
                "hit_fire"         => MakeConfig(18, 0.35f, 1.5f, 0.9f, 1f, 0.4f, 0.1f, 180f, 4f, 8f, -6f, 0.6f, 1.8f),
                "hit_ice"          => MakeConfig(16, 0.4f, 1.5f, 0.85f, 0.5f, 0.85f, 1f, 160f, 2f, 5f, -2f, 0.4f, 1.2f),
                "hit_lightning"    => MakeConfig(14, 0.25f, 3f, 0.95f, 0.7f, 0.85f, 1f, 180f, 4f, 8f, -4f, 0.3f, 0.8f),
                "hit_poison"       => MakeConfig(24, 2f, 0.6f, 0.3f, 0.3f, 0.9f, 0.2f, 120f, 0.3f, 0.6f, -0.5f, 0.8f, 2f),
                "hit_dark"         => MakeConfig(12, 0.3f, 2f, 0.9f, 0.5f, 0.2f, 0.7f, 180f, 3f, 6f, -8f, 0.5f, 1.5f),
                "death"            => MakeConfig(24, 0.6f, 1.5f, 0.95f, 1f, 0.4f, 0.1f, 180f, 4f, 8f, -5f, 0.8f, 2f),
                "impact_burst"     => MakeConfig(10, 0.2f, 2.5f, 0.95f, 1f, 0.7f, 0.3f, 180f, 5f, 10f, -6f, 0.3f, 1f),
                "freeze_burst"     => MakeConfig(16, 0.4f, 1.5f, 0.85f, 0.5f, 0.85f, 1f, 160f, 2f, 5f, -2f, 0.4f, 1.2f),
                "electric_sparks"  => MakeConfig(14, 0.25f, 3f, 0.95f, 0.7f, 0.85f, 1f, 180f, 4f, 8f, -4f, 0.3f, 0.8f),
                "poison_cloud"     => MakeConfig(24, 2f, 0.6f, 0.3f, 0.3f, 0.9f, 0.2f, 120f, 0.3f, 0.6f, -0.5f, 0.8f, 2f),
                "melee_slash"      => MakeConfig(1, 0.3f, 1f, 1f, 0.8f, 0.8f, 0.9f, 0f, 0f, 0f, 0f, 1f, 1f),
                "stun_indicator"   => MakeConfig(6, 1f, 1f, 0.3f, 0.9f, 0.8f, 0.1f, 360f, 1f, 2f, 0f, 0.3f, 0.6f),
                "aura_ring"        => MakeConfig(12, 1.5f, 1f, 0.3f, 0.3f, 0.6f, 1f, 180f, 0.5f, 1f, 0f, 0.3f, 0.6f),

                // Abilities
                "muzzle_flash"     => MakeConfig(8, 0.12f, 3f, 0.95f, 1f, 0.85f, 0.3f, 30f, 5f, 10f, 0f, 0.5f, 1.5f),
                "dash_trail"       => MakeConfig(12, 0.4f, 1f, 0.3f, 0.4f, 0.7f, 1f, 60f, 1f, 2f, -1f, 0.3f, 0.6f),
                "heal"             => MakeConfig(20, 0.8f, 1f, 0.5f, 0.2f, 1f, 0.4f, 30f, 1.5f, 3f, 0.5f, 0.3f, 0.8f),
                "arcane_circle"    => MakeConfig(12, 0.6f, 1f, 0.5f, 0.4f, 0.6f, 1f, 180f, 1f, 2f, 0f, 0.3f, 0.6f),
                "shockwave"        => MakeConfig(16, 0.3f, 2f, 0.95f, 1f, 0.8f, 0.3f, 180f, 5f, 10f, -2f, 0.3f, 1f),
                "music_notes"      => MakeConfig(8, 0.8f, 1f, 0.3f, 0.6f, 0.3f, 0.9f, 90f, 1f, 2f, 1f, 0.5f, 1.2f),
                "aoe_indicator"    => MakeConfig(1, 0.6f, 1f, 1f, 0.8f, 0.3f, 0.3f, 0f, 0f, 0f, 0f, 1f, 1f),
                "lightning_arc"    => MakeConfig(1, 0.3f, 1f, 1f, 0.6f, 0.8f, 1f, 0f, 0f, 0f, 0f, 1f, 1f),

                // Celebration
                "celebration"      => MakeConfig(30, 1.2f, 1.5f, 0.8f, 1f, 0.85f, 0.3f, 180f, 4f, 8f, -4f, 0.5f, 1.5f),
                "celebration_burst" => MakeConfig(40, 0.8f, 2f, 0.95f, 1f, 0.5f, 0.8f, 180f, 5f, 10f, -5f, 0.4f, 1.2f),
                "confetti_storm"   => MakeConfig(60, 2f, 1f, 0.3f, 1f, 0.85f, 0.3f, 180f, 2f, 4f, -3f, 0.3f, 0.8f),
                "orbiting_sparkles" => MakeConfig(12, 1.5f, 1f, 0.2f, 1f, 0.9f, 0.5f, 180f, 0.5f, 1f, 0f, 0.2f, 0.5f),
                "ground_sparks"    => MakeConfig(20, 0.6f, 1.5f, 0.9f, 1f, 0.85f, 0.3f, 180f, 3f, 6f, -8f, 0.3f, 0.8f),
                "sad_puff"         => MakeConfig(6, 0.5f, 1f, 0.8f, 0.4f, 0.4f, 0.45f, 90f, 1f, 2f, -4f, 0.5f, 1f),

                // Loot
                "loot_burst"       => MakeConfig(16, 0.5f, 1.5f, 0.9f, 1f, 0.85f, 0.3f, 120f, 3f, 5f, -6f, 0.4f, 1.0f),
                "pickup_trail"     => MakeConfig(6, 0.3f, 1.5f, 0.8f, 1f, 1f, 1f, 60f, 1f, 2f, 1f, 0.2f, 0.5f),
                "light_pillar"     => MakeConfig(1, 1.5f, 1f, 1f, 1f, 0.85f, 0.3f, 0f, 0f, 0f, 0f, 1f, 3f),
                "rarity_aura"      => MakeConfig(8, 2f, 0.5f, 0.2f, 0.3f, 0.6f, 1f, 180f, 0.3f, 0.6f, 0.2f, 0.3f, 0.6f),

                // Environment
                "torch_fire"       => MakeConfig(8, 0.6f, 1f, 0.3f, 1f, 0.5f, 0.1f, 15f, 1f, 2f, 1f, 0.3f, 0.8f),
                "portal_particles" => MakeConfig(16, 2f, 1f, 0.3f, 0.4f, 0.6f, 1f, 180f, 0.5f, 1.5f, 0f, 0.4f, 0.8f),
                "ambient_particles" => MakeConfig(20, 4f, 0.5f, 0.1f, 0.6f, 0.6f, 0.7f, 180f, 0.2f, 0.5f, 0.1f, 0.3f, 0.6f),

                _ => MakeConfig(12, 0.3f, 1.5f, 0.9f, 1f, 1f, 1f, 180f, 3f, 6f, -8f, 0.5f, 1.5f),
            };

            cfg["Category"] = cat;

            // Mesh VFX get extra defaults
            if (IsMeshVfx(effectName))
            {
                if (!cfg.ContainsKey("Duration")) cfg["Duration"] = 0.4;
                if (!cfg.ContainsKey("MeshScale")) cfg["MeshScale"] = 1.0;
            }

            return cfg;
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

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════════
        //  TEST API
        // ═══════════════════════════════════════════════════════════════

        private int _testEffectIndex;
        private int _testColorIndex;

        public override SubViewport TestGetViewport() => _viewport;

        public override void TestCycleNext(string property)
        {
            switch (property)
            {
                case "effect":
                    if (VfxEffects.Length == 0) break;
                    _testEffectIndex = (_testEffectIndex + 1) % VfxEffects.Length;
                    var effectName = VfxEffects[_testEffectIndex].name;
                    if (_configData != null && _configData.TryGetValue(effectName, out var rowData))
                    {
                        OnEffectSelected(effectName, rowData);
                        // Re-spawn immediately so the effect is fresh for screenshot capture
                        RespawnPreview();
                    }
                    break;
                case "color":
                    var presets = new[]
                    {
                        ("Fire", new Color(1f, 0.4f, 0.1f)),
                        ("Ice", new Color(0.5f, 0.85f, 1f)),
                        ("Zap", new Color(0.7f, 0.85f, 1f)),
                        ("Poison", new Color(0.3f, 0.9f, 0.2f)),
                        ("Dark", new Color(0.5f, 0.2f, 0.7f)),
                        ("Gold", new Color(1f, 0.85f, 0.3f)),
                    };
                    _testColorIndex = (_testColorIndex + 1) % presets.Length;
                    if (_colorPicker != null)
                    {
                        _colorPicker.Color = presets[_testColorIndex].Item2;
                        OnColorChanged(presets[_testColorIndex].Item2);
                    }
                    break;
            }
        }
    }
}
