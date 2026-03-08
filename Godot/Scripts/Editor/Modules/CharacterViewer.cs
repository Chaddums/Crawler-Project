using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Character Viewer — 3D model viewer for all bot frames and weapons.
    /// Select a bot frame to see its procedural mesh in a rotating 3D viewport.
    /// Cycle through weapons to see them attached.
    /// </summary>
    public partial class CharacterViewer : EditorPanel
    {
        public override string PanelName => "Characters";
        public override Color AccentColor => EditorStyles.AccentCharacter;

        private SubViewport _viewport;
        private SubViewportContainer _viewportContainer;
        private Node3D _modelRoot;
        private Camera3D _camera;
        private Label _infoLabel;
        private Label _statsLabel;

        private BotFrameType _currentFrame = BotFrameType.TinCan;
        private WeaponType _currentWeapon = WeaponType.None;
        private float _rotationAngle;
        private bool _autoRotate = true;

        private static readonly BotFrameType[] AllFrames =
        {
            BotFrameType.TinCan, BotFrameType.Scrapheap, BotFrameType.SparkPlug,
            BotFrameType.RustBucket, BotFrameType.NoiseBox, BotFrameType.Clunker
        };

        private static readonly WeaponType[] AllWeapons =
        {
            WeaponType.None, WeaponType.Pistol, WeaponType.Rifle, WeaponType.Shotgun,
            WeaponType.Launcher, WeaponType.Repeater, WeaponType.BladeRing,
            WeaponType.FlailChain, WeaponType.ShockCoil, WeaponType.FlameThrower
        };

        private int _frameIndex;
        private int _weaponIndex;

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 8);

            // Left: Controls
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(280, 0);

            // Bot Frame selector
            leftPanel.AddChild(EditorStyles.MakeLabel("Bot Frame", EditorStyles.FontHeader, AccentColor));
            var frameRow = new HBoxContainer();
            frameRow.AddThemeConstantOverride("separation", 4);

            var prevFrame = EditorStyles.MakeButton("<", EditorStyles.FontBody);
            prevFrame.CustomMinimumSize = new Vector2(32, 28);
            prevFrame.Pressed += () => CycleFrame(-1);
            frameRow.AddChild(prevFrame);

            _infoLabel = EditorStyles.MakeLabel("Tin Can", EditorStyles.FontBody, AccentColor);
            _infoLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _infoLabel.HorizontalAlignment = HorizontalAlignment.Center;
            frameRow.AddChild(_infoLabel);

            var nextFrame = EditorStyles.MakeButton(">", EditorStyles.FontBody);
            nextFrame.CustomMinimumSize = new Vector2(32, 28);
            nextFrame.Pressed += () => CycleFrame(1);
            frameRow.AddChild(nextFrame);
            leftPanel.AddChild(frameRow);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Weapon selector
            leftPanel.AddChild(EditorStyles.MakeLabel("Weapon", EditorStyles.FontHeader, AccentColor));
            var weaponRow = new HBoxContainer();
            weaponRow.AddThemeConstantOverride("separation", 4);

            var prevWeapon = EditorStyles.MakeButton("<", EditorStyles.FontBody);
            prevWeapon.CustomMinimumSize = new Vector2(32, 28);
            prevWeapon.Pressed += () => CycleWeapon(-1);
            weaponRow.AddChild(prevWeapon);

            var weaponLabel = EditorStyles.MakeLabel("None", EditorStyles.FontBody, AccentColor);
            weaponLabel.Name = "WeaponLabel";
            weaponLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            weaponLabel.HorizontalAlignment = HorizontalAlignment.Center;
            weaponRow.AddChild(weaponLabel);

            var nextWeapon = EditorStyles.MakeButton(">", EditorStyles.FontBody);
            nextWeapon.CustomMinimumSize = new Vector2(32, 28);
            nextWeapon.Pressed += () => CycleWeapon(1);
            weaponRow.AddChild(nextWeapon);
            leftPanel.AddChild(weaponRow);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Auto-rotate toggle
            var rotateCheck = new CheckBox();
            rotateCheck.Text = "Auto-Rotate";
            rotateCheck.ButtonPressed = true;
            rotateCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            rotateCheck.Toggled += v => _autoRotate = v;
            leftPanel.AddChild(rotateCheck);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Stats display
            leftPanel.AddChild(EditorStyles.MakeLabel("Base Stats", EditorStyles.FontHeader, EditorStyles.TextSecondary));
            _statsLabel = EditorStyles.MakeLabel("", EditorStyles.FontSmall, EditorStyles.TextMuted);
            leftPanel.AddChild(_statsLabel);

            split.AddChild(leftPanel);

            // Right: 3D viewport
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

            _viewportContainer = new SubViewportContainer();
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.Stretch = true;

            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(800, 600);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;

            // Camera
            _camera = new Camera3D();
            _camera.Position = new Vector3(0, 2, 4);
            _camera.LookAt(new Vector3(0, 1, 0));
            _viewport.AddChild(_camera);

            // Model root (character goes here)
            _modelRoot = new Node3D();
            _viewport.AddChild(_modelRoot);

            // Ground
            var ground = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(8, 8);
            ground.Mesh = planeMesh;
            var groundMat = new StandardMaterial3D();
            groundMat.AlbedoColor = new Color(0.08f, 0.08f, 0.1f);
            ground.MaterialOverride = groundMat;
            _viewport.AddChild(ground);

            // Lighting
            var light = new DirectionalLight3D();
            light.Position = new Vector3(3, 6, 3);
            light.LookAt(Vector3.Zero);
            light.LightEnergy = 1.2f;
            _viewport.AddChild(light);

            var fill = new DirectionalLight3D();
            fill.Position = new Vector3(-3, 4, -2);
            fill.LookAt(Vector3.Zero);
            fill.LightEnergy = 0.4f;
            _viewport.AddChild(fill);

            // Environment for background color
            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.05f, 0.05f, 0.08f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.15f, 0.15f, 0.2f);
            env.Environment = envRes;
            _viewport.AddChild(env);

            _viewportContainer.AddChild(_viewport);
            rightPanel.AddChild(_viewportContainer);
            split.AddChild(rightPanel);

            content.AddChild(split);
        }

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(LoadModel));
        }

        public override void _Process(double delta)
        {
            if (_autoRotate && _modelRoot != null && Visible)
            {
                _rotationAngle += (float)delta * 0.8f;
                _modelRoot.Rotation = new Vector3(0, _rotationAngle, 0);
            }
        }

        private void CycleFrame(int dir)
        {
            _frameIndex = (_frameIndex + dir + AllFrames.Length) % AllFrames.Length;
            _currentFrame = AllFrames[_frameIndex];
            _infoLabel.Text = _currentFrame.ToString();
            LoadModel();
        }

        private void CycleWeapon(int dir)
        {
            _weaponIndex = (_weaponIndex + dir + AllWeapons.Length) % AllWeapons.Length;
            _currentWeapon = AllWeapons[_weaponIndex];
            var weaponLabel = GetNode<Label>("%WeaponLabel");
            // Find weapon label by traversal
            UpdateWeaponLabel();
            LoadModel();
        }

        private void UpdateWeaponLabel()
        {
            // Walk the tree to find the label named WeaponLabel
            var label = FindChildByName<Label>(this, "WeaponLabel");
            if (label != null) label.Text = _currentWeapon.ToString();
        }

        private static T FindChildByName<T>(Node root, string name) where T : Node
        {
            foreach (var child in root.GetChildren())
            {
                if (child is T t && child.Name == name) return t;
                var found = FindChildByName<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void LoadModel()
        {
            if (_modelRoot == null) return;

            // Clear existing
            foreach (var child in _modelRoot.GetChildren())
            {
                if (child is Node n) n.QueueFree();
            }

            try
            {
                // Build body
                var body = CharacterMeshBuilder.BuildPlayerBody(_currentFrame);
                if (body != null)
                    _modelRoot.AddChild(body);

                // Build weapon if selected
                if (_currentWeapon != WeaponType.None)
                {
                    var weapon = CharacterMeshBuilder.BuildWeapon(_currentFrame);
                    if (weapon != null)
                    {
                        weapon.Position = new Vector3(0.6f, 1.0f, 0);
                        _modelRoot.AddChild(weapon);
                    }
                }

                // Build AoE weapon meshes
                if (_currentWeapon == WeaponType.BladeRing)
                {
                    var bladeRing = CharacterMeshBuilder.BuildBladeRing();
                    if (bladeRing != null)
                    {
                        bladeRing.Position = new Vector3(0, 1, 0);
                        _modelRoot.AddChild(bladeRing);
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[CharacterViewer] Error loading model: {e.Message}");
            }

            UpdateStats();
        }

        private void UpdateStats()
        {
            var data = BotFrameRegistry.GetClass(_currentFrame);
            if (data == null)
            {
                _statsLabel.Text = "No data";
                return;
            }

            var lines = new List<string>
            {
                $"HP: {data.BaseStats.GetBaseStat(StatType.MaxHealth)}  Mana: {data.BaseStats.GetBaseStat(StatType.MaxMana)}",
                $"STR: {data.BaseStats.GetBaseStat(StatType.Strength)}  DEX: {data.BaseStats.GetBaseStat(StatType.Dexterity)}",
                $"CON: {data.BaseStats.GetBaseStat(StatType.Constitution)}  INT: {data.BaseStats.GetBaseStat(StatType.Intelligence)}",
                $"CHA: {data.BaseStats.GetBaseStat(StatType.Charisma)}  LCK: {data.BaseStats.GetBaseStat(StatType.Luck)}",
                $"Armor: {data.BaseStats.GetBaseStat(StatType.Armor)}  Speed: {data.BaseStats.GetBaseStat(StatType.MoveSpeed)}",
                $"HP/Lvl: {data.HpPerLevel}  Mana/Lvl: {data.ManaPerLevel}",
                $"Primary: {data.PrimaryStat} (+{data.PrimaryStatPerLevel}/lvl)",
                $"Secondary: {data.SecondaryStat} (+{data.SecondaryStatPerLevel}/lvl)",
                $"Unlock: {data.UnlockCost} scrap"
            };
            _statsLabel.Text = string.Join("\n", lines);
        }

        protected override void Reload()
        {
            LoadModel();
            MarkClean();
        }

        protected override void Save()
        {
            SetStatus("View only — edit stats in Balance > BotFrames", EditorStyles.TextMuted);
        }

        protected override void RestoreSnapshot(string jsonSnapshot) { }
    }
}
