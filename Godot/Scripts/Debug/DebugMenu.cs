using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// In-game debug menu toggled with F1. Provides cheats for testing:
    /// damage scaling, god mode, instant kill, suicide, level up, give items, etc.
    /// Only active in debug builds or when --autoplay is used.
    /// </summary>
    public partial class DebugMenu : CanvasLayer
    {
        private PanelContainer _panel;
        private bool _visible;

        // Cheat state
        private static bool _godMode;
        private static float _damageMultiplier = 1f;
        private static bool _instantKill;

        // Weapon cycling
        private static readonly string[] _gunIds = { "base_pistol", "base_rifle", "base_shotgun", "base_launcher", "base_repeater", "base_blade_ring" };
        private static readonly string[] _gunNames = { "Pistol", "Rifle", "Shotgun", "Launcher", "Repeater", "Blade Ring" };
        private int _currentGunIndex = -1;

        // Ability cycling
        private static readonly (string id, string name, string className)[] _allAbilities =
        {
            ("ability_burst_fire",   "Burst Fire",        "Tin Can"),
            ("ability_strike",       "Piston Strike",     "Tin Can"),
            ("ability_shield_bash",  "Bulkhead Slam",     "Tin Can"),
            ("ability_whirlwind",    "Rotary Shred",      "Tin Can"),
            ("ability_cannon_blast", "Cannon Blast",      "Scrapheap"),
            ("ability_slam",         "Chassis Slam",      "Scrapheap"),
            ("ability_feral_roar",   "Threat Broadcast",  "Scrapheap"),
            ("ability_earthquake",   "Seismic Pound",     "Scrapheap"),
            ("ability_arcane_bolt",  "Arc Discharge",     "Spark Plug"),
            ("ability_frost_nova",   "Cryo Burst",        "Spark Plug"),
            ("ability_meteor",       "Orbital Drop",      "Spark Plug"),
            ("ability_snipe_shot",   "Snipe Shot",        "Rust Bucket"),
            ("ability_backstab",     "Blind Spot Strike", "Rust Bucket"),
            ("ability_smoke_bomb",   "EMP Grenade",       "Rust Bucket"),
            ("ability_assassinate",  "Core Breach",       "Rust Bucket"),
            ("ability_dark_chord",   "Dissonance Pulse",  "Noise Box"),
            ("ability_raise_dead",   "Salvage Drone",     "Noise Box"),
            ("ability_death_ballad", "Feedback Loop",     "Noise Box"),
            ("ability_rivet_burst",  "Rivet Burst",       "Clunker"),
            ("ability_flurry",       "Piston Flurry",     "Clunker"),
            ("ability_uppercut",     "Pneumatic Uppercut", "Clunker"),
            ("ability_hundred_fists","Overdrive Barrage",  "Clunker"),
        };
        private int _currentAbilityIndex = -1;

        public static bool GodMode => _godMode;
        public static float DamageMultiplier => _damageMultiplier;
        public static bool InstantKill => _instantKill;

        public override void _Ready()
        {
            Layer = 99;
            ProcessMode = ProcessModeEnum.Always;
            BuildUI();
            _panel.Visible = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F3)
            {
                _visible = !_visible;
                _panel.Visible = _visible;
                GetViewport().SetInputAsHandled();
            }
        }

        private void BuildUI()
        {
            _panel = new PanelContainer();
            _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _panel.Position = new Vector2(10, 10);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f);
            style.BorderColor = new Color(0.8f, 0.3f, 0.3f);
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 12;
            style.ContentMarginBottom = 12;
            _panel.AddThemeStyleboxOverride("panel", style);
            AddChild(_panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "DEBUG MENU (F3)";
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
            vbox.AddChild(title);

            AddSeparator(vbox);

            // Toggle buttons
            AddToggleButton(vbox, "God Mode (Deathless)", () =>
            {
                _godMode = !_godMode;
                GD.Print($"[DebugMenu] God Mode: {_godMode}");
                return _godMode;
            });

            AddToggleButton(vbox, "Instant Kill", () =>
            {
                _instantKill = !_instantKill;
                GD.Print($"[DebugMenu] Instant Kill: {_instantKill}");
                return _instantKill;
            });

            AddSeparator(vbox);

            // Damage scaler
            var dmgLabel = new Label();
            dmgLabel.Text = "Damage Multiplier: 1.0x";
            dmgLabel.AddThemeFontSizeOverride("font_size", 14);
            dmgLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            vbox.AddChild(dmgLabel);

            var dmgSlider = new HSlider();
            dmgSlider.MinValue = 0.1;
            dmgSlider.MaxValue = 20.0;
            dmgSlider.Step = 0.1;
            dmgSlider.Value = 1.0;
            dmgSlider.CustomMinimumSize = new Vector2(250, 20);
            dmgSlider.ValueChanged += (val) =>
            {
                _damageMultiplier = (float)val;
                dmgLabel.Text = $"Damage Multiplier: {_damageMultiplier:F1}x";
            };
            vbox.AddChild(dmgSlider);

            AddSeparator(vbox);

            // Action buttons
            AddActionButton(vbox, "Suicide", () =>
            {
                var player = PlayerManager.P1;
                if (player?.Health == null) return;
                var dmg = new DamageInfo
                {
                    RawDamage = 999999f,
                    FinalDamage = 999999f,
                    DamageType = DamageType.Physical,
                    Attacker = player,
                    Target = player
                };
                bool wasGod = _godMode;
                _godMode = false;
                player.Health.TakeDamage(dmg);
                _godMode = wasGod;
                GD.Print("[DebugMenu] Suicide triggered");
            });

            AddActionButton(vbox, "Full Heal + Mana", () =>
            {
                var player = PlayerManager.P1;
                if (player == null) return;
                player.Health.Heal(player.Health.MaxHealth);
                player.Stats.RestoreMana(player.Stats.MaxMana);
                GD.Print("[DebugMenu] Full heal + mana restore");
            });

            AddActionButton(vbox, "Level Up (+5)", () =>
            {
                var player = PlayerManager.P1;
                if (player == null) return;
                for (int i = 0; i < 5; i++)
                    player.Stats.AddExperience(player.Stats.ExperienceToNextLevel);
                player.Health.SetMaxHealth(player.Stats.GetStat(StatType.MaxHealth), false);
                player.Health.Heal(player.Health.MaxHealth);
                GD.Print($"[DebugMenu] Leveled up to {player.Stats.Level}");
            });

            AddActionButton(vbox, "Give +10 Skill Points", () =>
            {
                var player = PlayerManager.P1;
                if (player == null) return;
                player.Stats.SetSkillPoints(player.Stats.AvailableSkillPoints + 10);
                GD.Print($"[DebugMenu] Skill points: {player.Stats.AvailableSkillPoints}");
            });

            AddActionButton(vbox, "Kill All Enemies", () =>
            {
                var enemies = GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
                int killed = 0;
                foreach (var node in enemies)
                {
                    var health = node is Node3D n3d
                        ? n3d.GetNodeOrNull<HealthComponent>("HealthComponent")
                        : null;
                    if (health != null && health.IsAlive)
                    {
                        var dmg = new DamageInfo
                        {
                            RawDamage = 999999f,
                            FinalDamage = 999999f,
                            DamageType = DamageType.Physical,
                            Attacker = PlayerManager.P1,
                            Target = node
                        };
                        health.TakeDamage(dmg);
                        killed++;
                    }
                }
                GD.Print($"[DebugMenu] Killed {killed} enemies");
            });

            AddActionButton(vbox, "Skip to Next Area", () =>
            {
                GameManager.Instance?.CallDeferred(nameof(GameManager.AdvanceArea));
                GD.Print("[DebugMenu] Skipping to next area");
            });

            AddActionButton(vbox, "Give Diamond Loot Box", () =>
            {
                var player = PlayerManager.P1;
                if (player?.Inventory == null) return;
                var box = LootBoxFactory.CreateLootBox(LootBoxTier.Diamond);
                if (box != null)
                    player.Inventory.TryAddItem(box);
                GD.Print("[DebugMenu] Gave Diamond loot box");
            });

            AddSeparator(vbox);

            // Weapon cycling
            var weaponLabel = new Label();
            weaponLabel.Text = "Weapon: (none)";
            weaponLabel.AddThemeFontSizeOverride("font_size", 16);
            weaponLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.7f));
            vbox.AddChild(weaponLabel);

            AddActionButton(vbox, "Next Weapon  [>>]", () =>
            {
                var player = PlayerManager.P1;
                if (player?.Inventory == null) return;
                _currentGunIndex = (_currentGunIndex + 1) % _gunIds.Length;
                EquipDebugWeapon(player, _currentGunIndex);
                weaponLabel.Text = $"Weapon: {_gunNames[_currentGunIndex]}";
            });

            AddActionButton(vbox, "Prev Weapon  [<<]", () =>
            {
                var player = PlayerManager.P1;
                if (player?.Inventory == null) return;
                _currentGunIndex = (_currentGunIndex - 1 + _gunIds.Length) % _gunIds.Length;
                EquipDebugWeapon(player, _currentGunIndex);
                weaponLabel.Text = $"Weapon: {_gunNames[_currentGunIndex]}";
            });

            AddSeparator(vbox);

            // Ability cycling
            var abilityClassLabel = new Label();
            abilityClassLabel.Text = "Class: --";
            abilityClassLabel.AddThemeFontSizeOverride("font_size", 14);
            abilityClassLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            vbox.AddChild(abilityClassLabel);

            var abilityLabel = new Label();
            abilityLabel.Text = "Ability [Q]: (none)";
            abilityLabel.AddThemeFontSizeOverride("font_size", 16);
            abilityLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
            vbox.AddChild(abilityLabel);

            var abilityTypeLabel = new Label();
            abilityTypeLabel.Text = "";
            abilityTypeLabel.AddThemeFontSizeOverride("font_size", 12);
            abilityTypeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            vbox.AddChild(abilityTypeLabel);

            AddActionButton(vbox, "Next Ability  [>>]", () =>
            {
                var player = PlayerManager.P1;
                if (player == null) return;
                _currentAbilityIndex = (_currentAbilityIndex + 1) % _allAbilities.Length;
                SetDebugAbility(player, _currentAbilityIndex, abilityClassLabel, abilityLabel, abilityTypeLabel);
            });

            AddActionButton(vbox, "Prev Ability  [<<]", () =>
            {
                var player = PlayerManager.P1;
                if (player == null) return;
                _currentAbilityIndex = (_currentAbilityIndex - 1 + _allAbilities.Length) % _allAbilities.Length;
                SetDebugAbility(player, _currentAbilityIndex, abilityClassLabel, abilityLabel, abilityTypeLabel);
            });
        }

        private static void EquipDebugWeapon(PlayerController player, int index)
        {
            var baseData = ItemRegistry.GetItem(_gunIds[index]);
            if (baseData == null)
            {
                GD.Print($"[DebugMenu] Weapon not found: {_gunIds[index]}");
                return;
            }

            // Unequip current MainHand if any
            if (player.Inventory.Equipped.ContainsKey(EquipmentSlot.MainHand))
                player.Inventory.Unequip(EquipmentSlot.MainHand);

            // Create a fresh instance and equip it
            var item = new ItemInstance(baseData, ItemRarity.Common);
            player.Inventory.TryAddItem(item);
            player.Inventory.Equip(item, EquipmentSlot.MainHand);
            GD.Print($"[DebugMenu] Equipped weapon: {_gunNames[index]}");
        }

        private static void SetDebugAbility(PlayerController player, int index, Label classLabel, Label nameLabel, Label typeLabel)
        {
            var (id, name, className) = _allAbilities[index];
            var ability = AbilityRegistry.Get(id);
            if (ability == null)
            {
                GD.Print($"[DebugMenu] Ability not found: {id}");
                return;
            }

            // Slot into Q (slot 0) so it's immediately usable
            var combat = player.GetNodeOrNull<PlayerCombat>("PlayerCombat");
            combat?.SetAbility(0, ability);

            classLabel.Text = $"Class: {className}";
            nameLabel.Text = $"Ability [Q]: {name}";
            typeLabel.Text = $"{ability.Type} | {ability.DamageType} | Dmg:{ability.BaseDamage} | CD:{ability.Cooldown}s | Range:{ability.Range}m";

            // Give mana so we can test freely
            player.Stats.RestoreMana(player.Stats.MaxMana);

            GD.Print($"[DebugMenu] Set ability Q: {name} ({className}) — {ability.Type}, {ability.DamageType}");
        }

        private static void AddToggleButton(VBoxContainer parent, string text, System.Func<bool> onToggle)
        {
            var btn = new Button();
            btn.Text = $"[ ] {text}";
            btn.CustomMinimumSize = new Vector2(250, 36);
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.Alignment = HorizontalAlignment.Left;
            btn.Pressed += () =>
            {
                bool state = onToggle();
                btn.Text = state ? $"[X] {text}" : $"[ ] {text}";
            };
            parent.AddChild(btn);
        }

        private static void AddActionButton(VBoxContainer parent, string text, System.Action onClick)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(250, 36);
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.Pressed += () => onClick();
            parent.AddChild(btn);
        }

        private static void AddSeparator(VBoxContainer parent)
        {
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 4);
            parent.AddChild(sep);
        }
    }
}
