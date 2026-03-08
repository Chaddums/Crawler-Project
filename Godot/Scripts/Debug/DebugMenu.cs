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
        private Label _floatingFeedback; // Always-visible feedback for panel actions
        private bool _visible;
        private double _feedbackTimer;

        // Debug panel
        private PanelContainer _panel;
        private bool _panelVisible;

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
            BuildPanel();
            BuildFloatingFeedback();
            _bar.Visible = false;
            _panel.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (_feedbackTimer > 0)
            {
                _feedbackTimer -= delta;
                if (_feedbackTimer <= 0)
                {
                    _feedback.Text = "";
                    _floatingFeedback.Text = "";
                    _floatingFeedback.Visible = false;
                }
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
                else if (key.Keycode == Key.Insert)
                {
                    TogglePanel();
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
            _floatingFeedback.Text = msg;
            _floatingFeedback.Visible = true;
            _feedbackTimer = duration;
            GD.Print($"[Console] {msg}");
        }

        private void TogglePanel()
        {
            _panelVisible = !_panelVisible;
            _panel.Visible = _panelVisible;
        }

        private void BuildPanel()
        {
            _panel = new PanelContainer();
            _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
            _panel.GrowHorizontal = Control.GrowDirection.Both;
            _panel.GrowVertical = Control.GrowDirection.Both;
            _panel.OffsetLeft = -220;
            _panel.OffsetRight = 220;
            _panel.OffsetTop = -260;
            _panel.OffsetBottom = 260;

            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.06f, 0.06f, 0.12f, 0.95f);
            panelStyle.BorderColor = new Color(0.9f, 0.8f, 0.3f);
            panelStyle.BorderWidthBottom = 2;
            panelStyle.BorderWidthTop = 2;
            panelStyle.BorderWidthLeft = 2;
            panelStyle.BorderWidthRight = 2;
            panelStyle.CornerRadiusBottomLeft = 8;
            panelStyle.CornerRadiusBottomRight = 8;
            panelStyle.CornerRadiusTopLeft = 8;
            panelStyle.CornerRadiusTopRight = 8;
            panelStyle.ContentMarginLeft = 16;
            panelStyle.ContentMarginRight = 16;
            panelStyle.ContentMarginTop = 16;
            panelStyle.ContentMarginBottom = 16;
            _panel.AddThemeStyleboxOverride("panel", panelStyle);
            AddChild(_panel);

            var scroll = new ScrollContainer();
            scroll.CustomMinimumSize = new Vector2(400, 480);
            _panel.AddChild(scroll);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "DEBUG PANEL";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 24);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
            vbox.AddChild(title);

            var sep = new HSeparator();
            vbox.AddChild(sep);

            // Buttons
            AddPanelButton(vbox, "God Mode", () => { _godMode = !_godMode; ShowFeedback($"God mode: {(_godMode ? "ON" : "OFF")}"); });
            AddPanelButton(vbox, "Instant Kill", () => { _instantKill = !_instantKill; ShowFeedback($"Instant kill: {(_instantKill ? "ON" : "OFF")}"); });
            AddPanelButton(vbox, "Kill All Enemies", () => CmdKillAll());
            AddPanelButton(vbox, "Full Heal + Mana", () => CmdHeal());
            AddPanelButton(vbox, "Level Up x5", () => CmdLevelUp());
            AddPanelButton(vbox, "+10 Skill Points", () => CmdSkillPoints());
            AddPanelButton(vbox, "Skip to Next Area", () =>
            {
                GameManager.Instance?.CallDeferred(nameof(GameManager.AdvanceArea));
                ShowFeedback("Skipping to next area...");
            });
            AddPanelButton(vbox, "Give Diamond Loot Box", () => CmdLootBox());
            AddPanelButton(vbox, "Cycle Weapon", () => CmdNextWeapon());
            AddPanelButton(vbox, "Cycle Ability", () => CmdNextAbility());
            AddPanelButton(vbox, "Suicide", () => CmdSuicide());

            var sep2 = new HSeparator();
            vbox.AddChild(sep2);

            AddPanelButton(vbox, "Bug Report", () => { LaunchReporter("--bug"); TogglePanel(); });
            AddPanelButton(vbox, "Feature Request", () => { LaunchReporter("--feature"); TogglePanel(); });

            // Close hint
            var hint = new Label();
            hint.Text = "[Insert] to close";
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.AddThemeFontSizeOverride("font_size", 12);
            hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            vbox.AddChild(hint);
        }

        private void BuildFloatingFeedback()
        {
            _floatingFeedback = new Label();
            _floatingFeedback.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _floatingFeedback.OffsetTop = 8;
            _floatingFeedback.OffsetBottom = 40;
            _floatingFeedback.HorizontalAlignment = HorizontalAlignment.Center;
            _floatingFeedback.AddThemeFontSizeOverride("font_size", 18);
            _floatingFeedback.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
            _floatingFeedback.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
            _floatingFeedback.Visible = false;
            AddChild(_floatingFeedback);
        }

        private void AddPanelButton(VBoxContainer parent, string text, System.Action action)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(0, 36);
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.FocusMode = Control.FocusModeEnum.None;
            btn.Pressed += () =>
            {
                action();
                // Close panel so keyboard input returns to the game immediately
                if (_panelVisible) TogglePanel();
            };
            parent.AddChild(btn);
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

                case "sector":
                    CmdJumpToSector(arg);
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

            // Close console after executing any command so keyboard input returns to the game
            CloseConsole();
        }

        private void LaunchReporter(string flag)
        {
            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/');
            string repoRoot = System.IO.Path.GetDirectoryName(godotDir);
            string scriptPath = System.IO.Path.Combine(repoRoot, "tools", "playtest-reporter", "reporter.py");

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
            if (EquipDebugWeapon(player, _currentGunIndex))
                ShowFeedback($"Weapon: {_gunNames[_currentGunIndex]}");
            else
                ShowFeedback($"FAILED to equip: {_gunNames[_currentGunIndex]}");
        }

        private void CmdNextAbility()
        {
            var player = PlayerManager.P1;
            if (player == null) { ShowFeedback("No player found"); return; }
            _currentAbilityIndex = (_currentAbilityIndex + 1) % _allAbilities.Length;
            var (id, name, className) = _allAbilities[_currentAbilityIndex];
            var ability = AbilityRegistry.Get(id);
            if (ability == null) { ShowFeedback($"Ability not found: {id}"); return; }

            if (player.Combat == null)
            {
                ShowFeedback("ERROR: PlayerCombat not found!");
                return;
            }
            player.Combat.SetAbility(0, ability);
            player.Stats.RestoreMana(player.Stats.MaxMana);
            ShowFeedback($"Ability [1]: {name} ({className})");
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
                "sector <N> - Jump to sector N (e.g. sector 5)",
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

        private void CmdJumpToSector(string arg)
        {
            if (!int.TryParse(arg, out int targetSector) || targetSector < 1)
            {
                ShowFeedback("Usage: sector <N> (e.g. sector 5)");
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null) { ShowFeedback("No GameManager"); return; }

            ShowFeedback($"Jumping to Sector {targetSector}...");
            gm.JumpToSector(targetSector);
        }

        private static bool EquipDebugWeapon(PlayerController player, int index)
        {
            var baseData = ItemRegistry.GetItem(_gunIds[index]);
            if (baseData == null)
            {
                GD.PrintErr($"[Console] Weapon not found in ItemRegistry: {_gunIds[index]}");
                return false;
            }

            if (player.Inventory.Equipped.ContainsKey(EquipmentSlot.MainHand))
                player.Inventory.Unequip(EquipmentSlot.MainHand);

            var item = new ItemInstance(baseData, ItemRarity.Common);
            player.Inventory.TryAddItem(item);
            player.Inventory.Equip(item, EquipmentSlot.MainHand);
            GD.Print($"[Console] Equipped weapon: {baseData.Id} ({(baseData as EquipmentData)?.WeaponType})");
            return true;
        }
    }
}
