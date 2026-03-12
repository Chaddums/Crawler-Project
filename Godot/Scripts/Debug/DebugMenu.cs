using System.Collections.Generic;
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
        private static bool _showNames;
        public static bool ShowNames => _showNames;

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
            _panel.OffsetTop = -360;
            _panel.OffsetBottom = 360;

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
            scroll.CustomMinimumSize = new Vector2(400, 680);
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
            AddPanelButton(vbox, "Give Diamond Loot Box", () => CmdLootBox("diamond"));
            AddPanelButton(vbox, "Cycle Weapon", () => CmdNextWeapon());
            AddPanelButton(vbox, "Cycle Ability", () => CmdNextAbility());
            AddPanelButton(vbox, "Suicide", () => CmdSuicide());

            // --- Room Warp ---
            var roomLabel = new Label();
            roomLabel.Text = "ROOM WARP";
            roomLabel.HorizontalAlignment = HorizontalAlignment.Center;
            roomLabel.AddThemeFontSizeOverride("font_size", 16);
            roomLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
            vbox.AddChild(roomLabel);

            AddPanelButton(vbox, "Warp: Combat", () => CmdRoom("combat"));
            AddPanelButton(vbox, "Warp: Puzzle", () => CmdRoom("puzzle"));
            AddPanelButton(vbox, "Warp: Treasure", () => CmdRoom("treasure"));
            AddPanelButton(vbox, "Warp: Boss", () => CmdRoom("boss"));
            AddPanelButton(vbox, "Warp: Shop", () => CmdRoom("shop"));
            AddPanelButton(vbox, "Warp: Event", () => CmdRoom("event"));

            // --- Item Spawn ---
            var itemLabel = new Label();
            itemLabel.Text = "ITEM SPAWN";
            itemLabel.HorizontalAlignment = HorizontalAlignment.Center;
            itemLabel.AddThemeFontSizeOverride("font_size", 16);
            itemLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1f, 0.5f));
            vbox.AddChild(itemLabel);

            AddPanelButton(vbox, "Give All Weapons", () => CmdGiveItems("weapons"));
            AddPanelButton(vbox, "Give All Armor", () => CmdGiveItems("armor"));
            AddPanelButton(vbox, "Give All Consumables", () => CmdGiveItems("consumables"));
            AddPanelButton(vbox, "Give All Relics", () => CmdGiveItems("relics"));
            AddPanelButton(vbox, "Give All Grafts", () => CmdGiveItems("grafts"));
            AddPanelButton(vbox, "Queue All Loot Boxes", () => CmdLootBoxAll());

            var sep2 = new HSeparator();
            vbox.AddChild(sep2);

            AddPanelButton(vbox, "Bug Report", () => { BugReportDialog.Show(GetTree(), false); });
            AddPanelButton(vbox, "Feature Request", () => { BugReportDialog.Show(GetTree(), true); });

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
                    BugReportDialog.Show(GetTree(), false);
                    ShowFeedback("Bug report dialog opened");
                    CloseConsole();
                    break;

                case "feature":
                    BugReportDialog.Show(GetTree(), true);
                    ShowFeedback("Feature request dialog opened");
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
                    CmdLootBox(arg);
                    break;

                case "lootboxall":
                    CmdLootBoxAll();
                    break;

                case "room":
                    CmdRoom(arg);
                    break;

                case "give":
                    CmdGiveItems(arg);
                    break;

                case "drop":
                    CmdDropItems(arg);
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

                case "names":
                    _showNames = !_showNames;
                    ToggleNameTags(_showNames);
                    ShowFeedback($"Name tags: {(_showNames ? "ON" : "OFF")}");
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

        private void CmdLootBox(string arg = "")
        {
            var player = PlayerManager.P1;
            if (player?.Inventory == null) { ShowFeedback("No player/inventory"); return; }

            var tier = LootBoxTier.Diamond;
            if (!string.IsNullOrEmpty(arg))
            {
                if (!TryParseLootBoxTier(arg, out tier))
                {
                    ShowFeedback("Usage: lootbox [junk|bronze|silver|gold|diamond|legendary|celestial]");
                    return;
                }
            }

            var box = LootBoxFactory.CreateLootBox(tier);
            if (box != null)
                player.Inventory.TryAddItem(box);
            ShowFeedback($"Gave {tier} loot box");
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
                "lootbox [tier] - Give loot box (junk/bronze/silver/gold/diamond/legendary/celestial)",
                "lootboxall - Queue one of every loot box tier for safe room ceremony",
                "room <type> - Warp to room (combat/puzzle/treasure/event/shop/boss/megabonk/safe)",
                "give <cat> - Add items to inventory (weapons/armor/consumables/relics/grafts/all)",
                "drop <cat> - Spawn items on ground (weapons/armor/consumables/relics/grafts/all)",
                "weapon - Cycle to next weapon",
                "ability - Cycle to next ability",
                "damage <N> - Set damage multiplier",
                "instantkill - Toggle instant kill",
                "names - Toggle ID name tags above entities",
                "die - Suicide",
            };
            // Print to Godot console since it won't fit in the feedback label
            GD.Print("[Console] === COMMANDS ===");
            foreach (var line in lines)
                GD.Print($"  {line}");
            ShowFeedback("Commands listed in console output (see log)", 5.0);
        }

        private void ToggleNameTags(bool on)
        {
            // Tag all existing enemies
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
            foreach (var node in enemies)
            {
                if (node is not Node3D n3d) continue;
                var existing = n3d.GetNodeOrNull<Label3D>("DebugNameTag");
                if (on && existing == null)
                    AddNameTag(n3d, (n3d as EnemyController)?.Data?.Id ?? n3d.Name);
                else if (!on && existing != null)
                    existing.QueueFree();
            }

            // Tag player
            var players = GetTree().GetNodesInGroup(Constants.GROUP_PLAYER);
            foreach (var node in players)
            {
                if (node is not Node3D p3d) continue;
                var existing = p3d.GetNodeOrNull<Label3D>("DebugNameTag");
                if (on && existing == null)
                {
                    string frameId = (p3d as PlayerController)?.ClassController?.CurrentClass.ToString() ?? "Player";
                    AddNameTag(p3d, frameId);
                }
                else if (!on && existing != null)
                    existing.QueueFree();
            }

            // Tag dungeon assets (props, walls, doors, details, floors)
            string[] prefixes = { "Prop_", "Decor_", "Wall_", "door_", "Detail_", "SM_", "FbxFloor", "Obstacle", "Hazard_", "DoorPanel", "LootBox" };
            if (on)
            {
                // Find all Node3D descendants matching asset prefixes
                var allNodes = GetTree().Root.FindChildren("*", "Node3D", true, false);
                int tagged = 0;
                foreach (var node in allNodes)
                {
                    if (node is not Node3D n3d) continue;
                    string nodeName = n3d.Name.ToString();
                    bool match = false;
                    foreach (var prefix in prefixes)
                    {
                        if (nodeName.StartsWith(prefix)) { match = true; break; }
                    }
                    if (!match) continue;
                    if (n3d.GetNodeOrNull<Label3D>("DebugNameTag") != null) continue;
                    AddNameTag(n3d, nodeName, 32, new Color(0.6f, 0.9f, 1f));
                    tagged++;
                }
                GD.Print($"[DebugMenu] Tagged {tagged} asset nodes");
            }
            else
            {
                // Remove all debug name tags
                var tags = GetTree().Root.FindChildren("DebugNameTag", "Label3D", true, false);
                foreach (var tag in tags)
                    tag.QueueFree();
            }
        }

        /// <summary>
        /// Attach a floating Label3D name tag above a 3D entity.
        /// Called from ToggleNameTags and also from EnemyController.Initialize when ShowNames is on.
        /// </summary>
        public static void AddNameTag(Node3D target, string text, int fontSize = 48, Color? color = null)
        {
            if (target.GetNodeOrNull<Label3D>("DebugNameTag") != null) return;

            var label = new Label3D();
            label.Name = "DebugNameTag";
            label.Text = text;
            label.FontSize = fontSize;
            label.OutlineSize = fontSize > 20 ? 8 : 4;
            label.Modulate = color ?? new Color(1f, 1f, 0.4f);
            label.OutlineModulate = new Color(0, 0, 0);
            label.Position = new Vector3(0, fontSize >= 48 ? 2.5f : 1.8f, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.NoDepthTest = true;
            target.AddChild(label);
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

        // ─── Room Warp ───────────────────────────────────────────────

        private void CmdRoom(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                ShowFeedback("Usage: room <combat|puzzle|treasure|event|shop|boss|megabonk|safe>");
                return;
            }

            if (!TryParseRoomType(arg, out RoomType targetType))
            {
                ShowFeedback($"Unknown room type: {arg}");
                return;
            }

            var player = PlayerManager.P1;
            if (player == null) { ShowFeedback("No player found"); return; }

            var sectorMgr = FindSectorManager(GetTree().Root);
            if (sectorMgr?.Generator == null) { ShowFeedback("No dungeon loaded"); return; }

            var controllers = sectorMgr.Generator.RoomControllers;
            RoomController closest = null;
            float bestDist = float.MaxValue;

            foreach (var kvp in controllers)
            {
                if (kvp.Value.RoomType != targetType) continue;
                float dist = player.GlobalPosition.DistanceTo(kvp.Value.GlobalPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    closest = kvp.Value;
                }
            }

            if (closest == null)
            {
                ShowFeedback($"No {targetType} room found in this dungeon");
                return;
            }

            player.GlobalPosition = closest.GlobalPosition + Vector3.Up * 1f;
            ShowFeedback($"Warped to {targetType} room at {closest.GridPosition}");
        }

        private static bool TryParseRoomType(string input, out RoomType type)
        {
            type = input.ToLower() switch
            {
                "combat" => RoomType.Combat,
                "puzzle" => RoomType.Puzzle,
                "treasure" => RoomType.Treasure,
                "event" => RoomType.Event,
                "shop" => RoomType.Shop,
                "boss" => RoomType.Boss,
                "megabonk" => RoomType.Megabonk,
                "safe" or "saferoom" => RoomType.SafeRoom,
                "entrance" => RoomType.Entrance,
                "lift" => RoomType.Lift,
                _ => RoomType.Combat
            };
            // Return false only for truly unrecognized input
            return input.ToLower() is "combat" or "puzzle" or "treasure" or "event" or "shop"
                or "boss" or "megabonk" or "safe" or "saferoom" or "entrance" or "lift";
        }

        private static SectorManager FindSectorManager(Node root)
        {
            if (root is SectorManager sm) return sm;
            foreach (var child in root.GetChildren())
            {
                var found = FindSectorManager(child);
                if (found != null) return found;
            }
            return null;
        }

        // ─── Item Spawning ───────────────────────────────────────────

        private void CmdGiveItems(string arg)
        {
            var player = PlayerManager.P1;
            if (player?.Inventory == null) { ShowFeedback("No player/inventory"); return; }

            if (string.IsNullOrEmpty(arg))
            {
                ShowFeedback("Usage: give <weapons|armor|consumables|relics|grafts|all>");
                return;
            }

            var items = CollectItemsByCategory(arg);
            if (items == null)
            {
                ShowFeedback($"Unknown category: {arg}. Use weapons/armor/consumables/relics/grafts/all");
                return;
            }

            int added = 0;
            foreach (var item in items)
            {
                if (player.Inventory.TryAddItem(item))
                    added++;
            }
            ShowFeedback($"Added {added}/{items.Count} items ({arg})");
        }

        private void CmdDropItems(string arg)
        {
            var player = PlayerManager.P1;
            if (player == null) { ShowFeedback("No player found"); return; }

            if (string.IsNullOrEmpty(arg))
            {
                ShowFeedback("Usage: drop <weapons|armor|consumables|relics|grafts|all>");
                return;
            }

            var items = CollectItemsByCategory(arg);
            if (items == null)
            {
                ShowFeedback($"Unknown category: {arg}. Use weapons/armor/consumables/relics/grafts/all");
                return;
            }

            var basePos = player.GlobalPosition;
            int count = items.Count;
            for (int i = 0; i < count; i++)
            {
                // Arrange items in a circle around the player
                float angle = (float)i / count * Mathf.Tau;
                float radius = 2f + (count > 10 ? 1f : 0f);
                var offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                ItemPickup.SpawnAt(GetTree().Root, basePos + offset, items[i]);
            }
            ShowFeedback($"Dropped {count} items ({arg})");
        }

        private List<ItemInstance> CollectItemsByCategory(string category)
        {
            var items = new List<ItemInstance>();

            switch (category.ToLower())
            {
                case "weapons":
                    foreach (var eq in BaseItemPool.Equipment)
                    {
                        if (eq.Slot == EquipmentSlot.MainHand)
                            items.Add(new ItemInstance(eq, ItemRarity.Rare));
                    }
                    break;

                case "armor":
                    foreach (var eq in BaseItemPool.Equipment)
                    {
                        if (eq.Slot != EquipmentSlot.MainHand)
                            items.Add(new ItemInstance(eq, ItemRarity.Rare));
                    }
                    break;

                case "consumables":
                    foreach (var kvp in ConsumableRegistry.Consumables)
                        items.Add(new ItemInstance(kvp.Value));
                    break;

                case "relics":
                    foreach (var id in RelicRegistry.AllIds)
                    {
                        var relic = RelicRegistry.Get(id);
                        if (relic != null)
                            items.Add(new ItemInstance(relic));
                    }
                    break;

                case "grafts":
                    foreach (var kvp in SalvageCoreRegistry.All)
                        items.Add(new ItemInstance(new SalvageCoreItemData(kvp.Value)));
                    break;

                case "all":
                    items.AddRange(CollectItemsByCategory("weapons"));
                    items.AddRange(CollectItemsByCategory("armor"));
                    items.AddRange(CollectItemsByCategory("consumables"));
                    items.AddRange(CollectItemsByCategory("relics"));
                    items.AddRange(CollectItemsByCategory("grafts"));
                    break;

                default:
                    return null;
            }

            return items;
        }

        // ─── Loot Box Queue ─────────────────────────────────────────

        private void CmdLootBoxAll()
        {
            var tiers = new[]
            {
                LootBoxTier.Junk, LootBoxTier.Bronze, LootBoxTier.Silver,
                LootBoxTier.Gold, LootBoxTier.Diamond, LootBoxTier.Legendary,
                LootBoxTier.Celestial
            };

            int queued = 0;
            foreach (var tier in tiers)
            {
                var box = LootBoxFactory.CreateLootBox(tier);
                if (box != null)
                {
                    AchievementManager.PendingLootBoxes.Enqueue(box);
                    queued++;
                }
            }
            ShowFeedback($"Queued {queued} loot boxes — enter safe room to open");
        }

        private static bool TryParseLootBoxTier(string input, out LootBoxTier tier)
        {
            tier = input.ToLower() switch
            {
                "junk" => LootBoxTier.Junk,
                "bronze" => LootBoxTier.Bronze,
                "silver" => LootBoxTier.Silver,
                "gold" => LootBoxTier.Gold,
                "diamond" => LootBoxTier.Diamond,
                "legendary" => LootBoxTier.Legendary,
                "celestial" => LootBoxTier.Celestial,
                _ => LootBoxTier.Diamond
            };
            return input.ToLower() is "junk" or "bronze" or "silver" or "gold"
                or "diamond" or "legendary" or "celestial";
        }
    }
}
