using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// VFX Editor — preview particle effects and damage type colors.
    /// Shows a 3D viewport with spawnable VFX, and editable damage type color table.
    /// </summary>
    public partial class VfxEditor : EditorPanel
    {
        public override string PanelName => "VFX";
        public override Color AccentColor => EditorStyles.AccentVfx;

        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _vfxRoot;
        private Camera3D _camera;
        private Label _statusInfo;

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
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // Left: VFX list
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

            var title = EditorStyles.MakeLabel("Particle Effects", EditorStyles.FontHeader, AccentColor);
            leftPanel.AddChild(title);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var list = new VBoxContainer();
            list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            list.AddThemeConstantOverride("separation", 2);

            foreach (var (name, desc) in VfxEffects)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                var spawnBtn = EditorStyles.MakeButton("Spawn", EditorStyles.FontSmall, AccentColor);
                spawnBtn.CustomMinimumSize = new Vector2(60, 24);
                var captured = name;
                spawnBtn.Pressed += () => SpawnEffect(captured);
                row.AddChild(spawnBtn);

                var label = EditorStyles.MakeLabel($"{name} — {desc}", EditorStyles.FontSmall);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(label);

                list.AddChild(row);
            }

            scroll.AddChild(list);
            leftPanel.AddChild(scroll);

            // Clear button
            var clearBtn = EditorStyles.MakeButton("Clear All VFX", EditorStyles.FontSmall, EditorStyles.StatusError);
            clearBtn.Pressed += ClearVfx;
            leftPanel.AddChild(clearBtn);

            split.AddChild(leftPanel);

            // Right: 3D viewport preview
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(400, 0);

            var vpTitle = EditorStyles.MakeLabel("3D Preview", EditorStyles.FontHeader, AccentColor);
            rightPanel.AddChild(vpTitle);

            _viewportContainer = new SubViewportContainer();
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.Stretch = true;

            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(640, 480);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;

            // Camera
            _camera = new Camera3D();
            _camera.Position = new Vector3(0, 3, 5);
            _camera.LookAt(Vector3.Zero);
            _viewport.AddChild(_camera);

            // VFX spawn root
            _vfxRoot = new Node3D();
            _viewport.AddChild(_vfxRoot);

            // Reference ground plane
            var ground = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(10, 10);
            ground.Mesh = planeMesh;
            var groundMat = new StandardMaterial3D();
            groundMat.AlbedoColor = new Color(0.1f, 0.1f, 0.12f);
            ground.MaterialOverride = groundMat;
            _viewport.AddChild(ground);

            // Ambient light
            var light = new DirectionalLight3D();
            light.Position = new Vector3(2, 5, 2);
            light.LookAt(Vector3.Zero);
            _viewport.AddChild(light);

            _viewportContainer.AddChild(_viewport);
            rightPanel.AddChild(_viewportContainer);

            _statusInfo = EditorStyles.MakeLabel("Spawn effects to preview", EditorStyles.FontSmall, EditorStyles.TextMuted);
            rightPanel.AddChild(_statusInfo);

            // Damage type color reference
            rightPanel.AddChild(EditorStyles.MakeSeparator());
            var colorTitle = EditorStyles.MakeLabel("Damage Type Colors", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            rightPanel.AddChild(colorTitle);

            var damageColors = new (string name, Color color)[]
            {
                ("Physical", new Color(1f, 0.9f, 0.8f)),
                ("Fire", new Color(1f, 0.4f, 0.1f)),
                ("Ice", new Color(0.3f, 0.7f, 1f)),
                ("Lightning", new Color(0.8f, 0.8f, 1f)),
                ("Poison", new Color(0.3f, 0.9f, 0.3f)),
                ("Dark", new Color(0.5f, 0.2f, 0.7f)),
                ("Holy", new Color(1f, 1f, 0.6f)),
            };

            foreach (var (cName, color) in damageColors)
            {
                var colorRow = new HBoxContainer();
                colorRow.AddThemeConstantOverride("separation", 8);

                var swatch = new ColorRect();
                swatch.Color = color;
                swatch.CustomMinimumSize = new Vector2(20, 14);
                colorRow.AddChild(swatch);

                colorRow.AddChild(EditorStyles.MakeLabel(cName, EditorStyles.FontTiny));
                rightPanel.AddChild(colorRow);
            }

            split.AddChild(rightPanel);
            content.AddChild(split);
        }

        private void SpawnEffect(string name)
        {
            if (_vfxRoot == null) return;

            Node3D effect = null;
            var pos = Vector3.Up * 1f;

            switch (name)
            {
                case "hit_physical":
                    effect = VfxFactory.CreateHitParticles(new Color(1f, 0.9f, 0.8f));
                    break;
                case "hit_fire":
                    effect = VfxFactory.CreateImpactBurst(new Color(1f, 0.4f, 0.1f));
                    break;
                case "hit_ice":
                    effect = VfxFactory.CreateFreezeBurst();
                    break;
                case "hit_lightning":
                    effect = VfxFactory.CreateElectricSparks();
                    break;
                case "hit_poison":
                    effect = VfxFactory.CreatePoisonCloud();
                    break;
                case "hit_dark":
                    effect = VfxFactory.CreateHitParticles(new Color(0.5f, 0.2f, 0.7f));
                    break;
                case "death":
                    effect = VfxFactory.CreateDeathParticles(new Color(1f, 0.4f, 0.1f));
                    break;
                case "heal":
                    effect = VfxFactory.CreateHealParticles();
                    break;
                case "muzzle_flash":
                    effect = VfxFactory.CreateMuzzleFlash();
                    break;
                case "dash_trail":
                    effect = VfxFactory.CreateDashTrail(new Color(0.4f, 0.7f, 1f));
                    break;
                case "celebration":
                    effect = VfxFactory.CreateCelebrationParticles();
                    break;
                case "loot_burst":
                    effect = VfxFactory.CreateLootBurstParticles(new Color(1f, 0.6f, 0f));
                    break;
                case "arcane_circle":
                    effect = VfxFactory.CreateArcaneCircle(new Color(0.4f, 0.6f, 1f));
                    break;
                case "shockwave":
                    effect = VfxFactory.CreateShockwaveRing(new Color(1f, 0.8f, 0.3f));
                    break;
                case "music_notes":
                    effect = VfxFactory.CreateMusicNotes(new Color(0.6f, 0.3f, 0.9f));
                    break;
                case "torch_fire":
                    effect = VfxFactory.CreateTorchFireParticles();
                    break;
                case "sad_puff":
                    effect = VfxFactory.CreateSadPuff();
                    break;
            }

            if (effect != null)
            {
                effect.Position = pos;
                _vfxRoot.AddChild(effect);
                _statusInfo.Text = $"Spawned: {name}";
                _statusInfo.AddThemeColorOverride("font_color", AccentColor);
            }
        }

        private void ClearVfx()
        {
            if (_vfxRoot == null) return;
            foreach (var child in _vfxRoot.GetChildren())
            {
                if (child is Node n) n.QueueFree();
            }
            _statusInfo.Text = "Cleared all effects";
        }

        protected override void Reload()
        {
            SetStatus("VFX preview ready", EditorStyles.StatusSaved);
        }

        protected override void Save()
        {
            SetStatus("No data to save (preview only)", EditorStyles.TextMuted);
        }

        protected override void RestoreSnapshot(string jsonSnapshot) { }
    }
}
