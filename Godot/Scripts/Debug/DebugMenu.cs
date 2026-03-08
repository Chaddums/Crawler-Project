using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// In-game debug console toggled with ~ (tilde/backtick).
    /// Type commands and press Enter to execute cheats and actions.
    /// Type "help" to see all available commands.
    /// </summary>
    public partial class DebugMenu : CanvasLayer
    {
        private PanelContainer _bar;
        private LineEdit _input;
        private Label _feedback;
        private bool _visible;
        private double _feedbackTimer;

        // Cheat state
        private static bool _godMode;
        private static float _damageMultiplier = 1f;
        private static bool _instantKill;

        // Weapon cycling
        private static readonly string[] _gunIds = { "base_pistol", "base_rifle", "base_shotgun", "base_launcher", "base_repeater", "base_blade_ring", "base_flail_chain", "base_shock_coil", "base_flame_thrower" };
        private static readonly string[] _gunNames = { "Pistol", "Rifle", "Shotgun", "Launcher", "Repeater", "Blade Ring", "Flail Chain", "Shock Coil", "Flame Thrower" };
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
            _bar.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (_feedbackTimer > 0)
            {
                _feedbackTimer -= delta;
                if (_feedbackTimer <= 0)
                    _feedback.Text = "";
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Quoteleft) // ~ / ` (tilde/backtick)
                {
                    ToggleConsole();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (!_visible) return;

            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    CloseConsole();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.Keycode == Key.Quoteleft)
                {
                    CloseConsole();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void ToggleConsole()
        {
            _visible = !_visible;
            _bar.Visible = _visible;
            if (_visible)
            {
                _input.Text = "";
                _input.GrabFocus();
            }
            else
            {
                _input.ReleaseFocus();
            }
        }

        private void CloseConsole()
        {
            _visible = false;
            _bar.Visible = false;
            _input.ReleaseFocus();
        }

        private void BuildUI()
        {
            _bar = new PanelContainer();
            _bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _bar.OffsetBottom = 40;

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            _bar.AddThemeStyleboxOverride("panel", style);
            AddChild(_bar);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 10);
            _bar.AddChild(hbox);

            // Prompt label
            var prompt = new Label();
            prompt.Text = ">";
            prompt.AddThemeFontSizeOverride("font_size", 16);
            prompt.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
            hbox.AddChild(prompt);

            // Text input
            _input = new LineEdit();
            _input.PlaceholderText = "type a command... (help for list)";
            _input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _input.AddThemeFontSizeOverride("font_size", 16);
            _input.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
            _input.AddThemeColorOverride("font_placeholder_color", new Color(0.5f, 0.5f, 0.5f));

            var inputStyle = new StyleBoxFlat();
            inputStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            inputStyle.BorderColor = new Color(0.3f, 0.3f, 0.4f);
            inputStyle.BorderWidthBottom = 1;
            inputStyle.BorderWidthTop = 1;
            inputStyle.BorderWidthLeft = 1;
            inputStyle.BorderWidthRight = 1;
            inputStyle.ContentMarginLeft = 8;
            inputStyle.ContentMarginRight = 8;
            inputStyle.ContentMarginTop = 4;
            inputStyle.ContentMarginBottom = 4;
            _input.AddThemeStyleboxOverride("normal", inputStyle);
            _input.TextSubmitted += OnCommandSubmitted;
            hbox.AddChild(_input);

            // Feedback label
            _feedback = new Label();
            _feedback.Text = "";
            _feedback.AddThemeFontSizeOverride("font_size", 14);
            _feedback.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
            _feedback.CustomMinimumSize = new Vector2(300, 0);
            hbox.AddChild(_feedback);
        }

        private void ShowFeedback(string msg, double duration = 3.0)
        {
            _feedback.Text = msg;
            _feedbackTimer = duration;
            GD.Print($"[Console] {msg}");
        }

        private void OnCommandSubmitted(string text)
        {
            _input.Text = "";
            var raw = text.Trim().ToLower();
            if (string.IsNullOrEmpty(raw)) return;

            // Split command and args
            var parts = raw.Split(' ', 2);
            var cmd = parts[0];
            var arg = parts.Length > 1 ? parts[1].Trim() : "";

            switch (cmd)
            {
                case "bug":
                    LaunchReporter("--bug");
                    ShowFeedback("Launching bug reporter...");
                    CloseConsole();
                    break;

                case "feature":
                    LaunchReporter("--feature");
                    ShowFeedback("Launching feature reporter...");
                    CloseConsole();
                    break;

                case "god":
                    _godMode = !_godMode;
                    ShowFeedback($"God mode: {(_godMode ? "ON" : "OFF")}");
                    break;

                case "kill":
                    CmdKillAll();
                    break;

                case "heal":
                    CmdHeal();
                    break;

                case "levelup":
                    CmdLevelUp();
                    break;

                case "skillpoints":
                    CmdSkillPoints();
                    break;

                case "skip":
                    GameManager.Instance?.CallDeferred(nameof(GameManager.AdvanceArea));
                    ShowFeedback("Skipping to next area...");
                    break;

                case "lootbox":
                    CmdLootBox();
                    break;

                case "weapon":
                    CmdNextWeapon();
                    break;

                case "ability":
                    CmdNextAbility();
                    break;

                case "damage":
                    CmdDamage(arg);
                    break;

                case "die":
                    CmdSuicide();
                    break;

                case "instantkill":
                    _instantKill = !_instantKill;
                    ShowFeedback($"Instant kill: {(_instantKill ? "ON" : "OFF")}");
                    break;

                case "help":
                    CmdHelp();
                    break;

                default:
                    ShowFeedback($"Unknown command: {cmd}");
                    break;
            }
        }

        private void LaunchReporter(string flag)
        {
            string projectRoot = ProjectSettings.GlobalizePath("res://").GetBaseDir();
            string scriptPath = System.IO.Path.Combine(projectRoot, "tools", "playtest-reporter", "reporter.py");

            if (!System.IO.File.Exists(scriptPath))
            {
                ShowFeedback("Reporter script not found!");
                return;
            }

            OS.CreateProcess("python", new string[] { scriptPath, flag });
        }

        private void CmdKillAll()
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
            ShowFeedback($"Killed {killed} enemies");
        }

        private void CmdHeal()
        {
            var player = PlayerManager.P1;
            if (player == null) { ShowFeedback("No player found"); return; }
            player.Health.Heal(player.Health.MaxHealth);
            player.Stats.RestoreMana(player.Stats.MaxMana);
            ShowFeedback("Full heal + mana");
        }

        private void CmdLevelUp()
        {
            var player = PlayerManager.P1;
            if (player == null) { ShowFeedback("No player found"); return; }
            for (int i = 0; i < 5; i++)
                player.Stats.AddExperience(player.Stats.ExperienceToNextLevel);
            player.Health.SetMaxHealth(player.Stats.GetStat(StatType.MaxHealth), false);
            player.Health.Heal(player.Health.MaxHealth);
            ShowFeedback($"Leveled up to {player.Stats.Level}");
        }

        private void CmdSkillPoints()
        {
            var player = PlayerManager.P1;
            if (player == null) { ShowFeedback("No player found"); return; }
            player.Stats.SetSkillPoints(player.Stats.AvailableSkillPoints + 10);
            ShowFeedback($"Skill points: {player.Stats.AvailableSkillPoints}");
        }

        private void CmdLootBox()
        {
            var player = PlayerManager.P1;
            if (player?.Inventory == null) { ShowFeedback("No player/inventory"); return; }
            var box = LootBoxFactory.CreateLootBox(LootBoxTier.Diamond);
            if (box != null)
                player.Inventory.TryAddItem(box);
            ShowFeedback("Gave Diamond loot box");
        }

        private void CmdNextWeapon()
        {
            var player = PlayerManager.P1;
            if (player?.Inventory == null) { ShowFeedback("No player/inventory"); return; }
            _currentGunIndex = (_currentGunIndex + 1) % _gunIds.Length;
            EquipDebugWeapon(player, _currentGunIndex);
            ShowFeedback($"Weapon: {_gunNames[_currentGunIndex]}");
        }

        private void CmdNextAbility()
        {
            var player = PlayerManager.P1;
            if (player == null) { ShowFeedback("No player found"); return; }
            _currentAbilityIndex = (_currentAbilityIndex + 1) % _allAbilities.Length;
            var (id, name, className) = _allAbilities[_currentAbilityIndex];
            var ability = AbilityRegistry.Get(id);
            if (ability == null) { ShowFeedback($"Ability not found: {id}"); return; }

            var combat = player.GetNodeOrNull<PlayerCombat>("PlayerCombat");
            combat?.SetAbility(0, ability);
            player.Stats.RestoreMana(player.Stats.MaxMana);
            ShowFeedback($"Ability [Q]: {name} ({className})");
        }

        private void CmdDamage(string arg)
        {
            if (float.TryParse(arg, out float val) && val > 0)
            {
                _damageMultiplier = val;
                ShowFeedback($"Damage multiplier: {_damageMultiplier:F1}x");
            }
            else
            {
                ShowFeedback($"Usage: damage <number>  (current: {_damageMultiplier:F1}x)");
            }
        }

        private void CmdSuicide()
        {
            var player = PlayerManager.P1;
            if (player?.Health == null) { ShowFeedback("No player found"); return; }
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
            ShowFeedback("Suicide triggered");
        }

        private void CmdHelp()
        {
            var lines = new string[]
            {
                "bug - Bug report (screenshot + dialog)",
                "feature - Feature request (screenshot + dialog)",
                "god - Toggle god mode",
                "kill - Kill all enemies",
                "heal - Full heal + mana",
                "levelup - Level up x5",
                "skillpoints - Give +10 skill points",
                "skip - Skip to next area",
                "lootbox - Give Diamond loot box",
                "weapon - Cycle to next weapon",
                "ability - Cycle to next ability",
                "damage <N> - Set damage multiplier",
                "instantkill - Toggle instant kill",
                "die - Suicide",
            };
            // Print to Godot console since it won't fit in the feedback label
            GD.Print("[Console] === COMMANDS ===");
            foreach (var line in lines)
                GD.Print($"  {line}");
            ShowFeedback("Commands listed in console output (see log)", 5.0);
        }

        private static void EquipDebugWeapon(PlayerController player, int index)
        {
            var baseData = ItemRegistry.GetItem(_gunIds[index]);
            if (baseData == null)
            {
                GD.Print($"[Console] Weapon not found: {_gunIds[index]}");
                return;
            }

            if (player.Inventory.Equipped.ContainsKey(EquipmentSlot.MainHand))
                player.Inventory.Unequip(EquipmentSlot.MainHand);

            var item = new ItemInstance(baseData, ItemRarity.Common);
            player.Inventory.TryAddItem(item);
            player.Inventory.Equip(item, EquipmentSlot.MainHand);
        }
    }
}
