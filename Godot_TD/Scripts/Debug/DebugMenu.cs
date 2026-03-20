using Godot;
using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// In-game debug menu with two modes:
    /// ~ (backtick) — console bar at top of screen with buttons + command input
    /// Insert — centered modal panel with categorized debug sections
    /// </summary>
    public partial class DebugMenu : CanvasLayer
    {
        // ── Console bar (backtick) ──
        private PanelContainer _panel;
        private LineEdit _input;
        private Label _feedback;
        private bool _visible;
        private double _feedbackTimer;

        // ── Insert panel ──
        private PanelContainer _insertPanel;
        private bool _insertVisible;

        // ── Floating feedback (always visible) ──
        private Label _floatingFeedback;
        private double _floatingTimer;

        // ── Cheat state ──
        private static bool _godMode;
        private static bool _instantKill;
        public static bool GodMode => _godMode;
        public static bool InstantKill => _instantKill;

        // ── Name tags ──
        private static bool _showNames;
        private readonly List<Label3D> _nameLabels = new();
        private readonly List<Node3D> _nameTargets = new();

        // ── HP bars ──
        private static bool _showHP;
        private readonly List<Label3D> _hpLabels = new();
        private readonly List<Node3D> _hpTargets = new();

        // ── Freecam ──
        private static bool _freecam;
        private Vector3 _freecamPos;
        private float _freecamYaw;
        private float _freecamPitch = -55f;

        // ── Style constants ──
        private static readonly Color CyanHeader = new(0.3f, 0.85f, 1f);
        private static readonly Color GoldHeader = new(1f, 0.85f, 0.3f);
        private static readonly Color GrayHeader = new(0.6f, 0.6f, 0.6f);
        private static readonly Color BtnNormal = new(0.8f, 0.8f, 0.8f);

        public override void _Ready()
        {
            Layer = 99;
            ProcessMode = ProcessModeEnum.Always;
            BuildConsoleBar();
            BuildInsertPanel();
            BuildFloatingFeedback();
            _panel.Visible = false;
            _insertPanel.Visible = false;
        }

        public override void _Process(double delta)
        {
            // Console feedback timer
            if (_feedbackTimer > 0)
            {
                _feedbackTimer -= delta;
                if (_feedbackTimer <= 0) _feedback.Text = "";
            }

            // Floating feedback timer
            if (_floatingTimer > 0)
            {
                _floatingTimer -= delta;
                if (_floatingTimer <= 0)
                {
                    _floatingFeedback.Text = "";
                    _floatingFeedback.Visible = false;
                }
            }

            // Update name tags
            if (_showNames) UpdateNameTags();

            // Update HP bars
            if (_showHP) UpdateHPBars();

            // Freecam movement
            if (_freecam) ProcessFreecam(delta);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
            {
                if (_visible)
                {
                    _visible = false;
                    _panel.Visible = false;
                    GetViewport().SetInputAsHandled();
                }
                else if (_insertVisible)
                {
                    _insertVisible = false;
                    _insertPanel.Visible = false;
                    GetViewport().SetInputAsHandled();
                }
            }

            // Consume mouse wheel events when insert panel is open so they don't reach the game camera
            if (_insertVisible && @event is InputEventMouseButton mb &&
                (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Quoteleft) // ~ tilde
                {
                    _visible = !_visible;
                    _panel.Visible = _visible;
                    if (_visible) _input.GrabFocus();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.Keycode == Key.Insert)
                {
                    _insertVisible = !_insertVisible;
                    _insertPanel.Visible = _insertVisible;
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Console bar (existing ~ menu)
        // ════════════════════════════════════════════════════════════════

        private void BuildConsoleBar()
        {
            _panel = new PanelContainer();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            style.BorderColor = new Color(0.3f, 0.7f, 0.9f, 0.6f);
            style.SetBorderWidthAll(1);
            style.SetContentMarginAll(10);
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            _panel.AddThemeStyleboxOverride("panel", style);
            _panel.AnchorLeft = 0;
            _panel.AnchorRight = 1;
            _panel.AnchorTop = 0;
            _panel.AnchorBottom = 0;
            _panel.OffsetBottom = 0;
            AddChild(_panel);

            var mainVBox = new VBoxContainer();
            mainVBox.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(mainVBox);

            // Title
            var title = new Label();
            title.Text = "DEBUG MENU (~)";
            title.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1f));
            title.AddThemeFontSizeOverride("font_size", 14);
            mainVBox.AddChild(title);

            // Button rows
            var row1 = new HBoxContainer();
            row1.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(row1);

            AddBtn(row1, "+100 Scrap", () => { GameManager.Instance?.AddScrap(100); Msg("+100 Scrap"); });
            AddBtn(row1, "+500 Scrap", () => { GameManager.Instance?.AddScrap(500); Msg("+500 Scrap"); });
            AddBtn(row1, "Kill All", () => { KillAllEnemies(); Msg("All enemies killed"); });
            AddBtn(row1, "Skip Wave", () => { KillAllEnemies(); Msg("Wave skipped"); });
            AddBtn(row1, "God Mode", () => { _godMode = !_godMode; Msg($"God Mode: {(_godMode ? "ON" : "OFF")}"); });
            AddBtn(row1, "Insta-Kill", () => { _instantKill = !_instantKill; Msg($"Instant Kill: {(_instantKill ? "ON" : "OFF")}"); });

            var row2 = new HBoxContainer();
            row2.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(row2);

            AddBtn(row2, "Heal Harvester", () => {
                if (ServiceLocator.TryGet<VineHarvester>(out var h)) { h.Heal(h.MaxHP); Msg("Harvester healed"); }
            });
            AddBtn(row2, "Damage Harvester", () => {
                if (ServiceLocator.TryGet<VineHarvester>(out var h)) { h.TakeDamage(20); Msg("Harvester -20 HP"); }
            });
            AddBtn(row2, "Toggle Mining", () => {
                if (ServiceLocator.TryGet<VineHarvester>(out var h)) { h.ToggleMode(); Msg($"Mining: {h.CurrentMode}"); }
            });

            // Magic type row
            var row3 = new HBoxContainer();
            row3.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(row3);

            var magicLabel = new Label();
            magicLabel.Text = "Set Magic:";
            magicLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            magicLabel.AddThemeFontSizeOverride("font_size", 12);
            row3.AddChild(magicLabel);

            AddBtn(row3, "Chaos", () => { SetMagic(MagicType.Chaos); }, new Color(0.7f, 0.2f, 0.9f));
            AddBtn(row3, "Power", () => { SetMagic(MagicType.Power); }, new Color(1f, 0.7f, 0.1f));
            AddBtn(row3, "Environment", () => { SetMagic(MagicType.Environment); }, new Color(0.2f, 0.85f, 0.3f));

            // Player row
            var row4 = new HBoxContainer();
            row4.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(row4);

            AddBtn(row4, "Heal Player", () => {
                if (ServiceLocator.TryGet<VinePlayer>(out var p))
                {
                    p.CurrentHP = p.MaxHP;
                    GameEvents.OnPlayerHPChanged?.Invoke(p.CurrentHP, p.MaxHP);
                    Msg("Player healed");
                }
            });
            AddBtn(row4, "Max Mana", () => {
                if (ServiceLocator.TryGet<VinePlayer>(out var p))
                {
                    p.CurrentMagic = p.MaxMagic;
                    GameEvents.OnPlayerMagicChanged?.Invoke(p.CurrentMagic, p.MaxMagic);
                    Msg("Mana maxed");
                }
            });
            AddBtn(row4, "Speed x1", () => { Engine.TimeScale = 1; Msg("Speed: 1x"); });
            AddBtn(row4, "Speed x3", () => { Engine.TimeScale = 3; Msg("Speed: 3x"); });
            AddBtn(row4, "Speed x10", () => { Engine.TimeScale = 10; Msg("Speed: 10x"); });

            // Command input
            var inputRow = new HBoxContainer();
            inputRow.AddThemeConstantOverride("separation", 4);
            mainVBox.AddChild(inputRow);

            var prompt = new Label();
            prompt.Text = ">";
            prompt.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1f));
            inputRow.AddChild(prompt);

            _input = new LineEdit();
            _input.PlaceholderText = "type command...";
            _input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _input.AddThemeColorOverride("font_color", Colors.White);
            _input.AddThemeFontSizeOverride("font_size", 13);
            _input.TextSubmitted += OnCommand;
            inputRow.AddChild(_input);

            // Feedback
            _feedback = new Label();
            _feedback.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 0.5f));
            _feedback.AddThemeFontSizeOverride("font_size", 12);
            mainVBox.AddChild(_feedback);
        }

        // ════════════════════════════════════════════════════════════════
        // Insert panel (centered modal with sections)
        // ════════════════════════════════════════════════════════════════

        private void BuildInsertPanel()
        {
            _insertPanel = new PanelContainer();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.03f, 0.04f, 0.08f, 0.95f);
            style.BorderColor = GoldHeader;
            style.SetBorderWidthAll(2);
            style.SetContentMarginAll(16);
            style.SetCornerRadiusAll(6);
            _insertPanel.AddThemeStyleboxOverride("panel", style);

            // Center the panel
            _insertPanel.AnchorLeft = 0.5f;
            _insertPanel.AnchorRight = 0.5f;
            _insertPanel.AnchorTop = 0.5f;
            _insertPanel.AnchorBottom = 0.5f;
            _insertPanel.OffsetLeft = -220;
            _insertPanel.OffsetRight = 220;
            _insertPanel.OffsetTop = -360;
            _insertPanel.OffsetBottom = 360;
            AddChild(_insertPanel);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _insertPanel.AddChild(scroll);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "DEBUG PANEL (Insert)";
            title.AddThemeColorOverride("font_color", GoldHeader);
            title.AddThemeFontSizeOverride("font_size", 16);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(title);

            AddSeparator(vbox);

            // ── NAMES / LABELS ──
            AddSectionHeader(vbox, "NAMES / LABELS", CyanHeader);
            AddPanelBtn(vbox, "Toggle Name Tags", () => { ToggleNameTags(); });
            AddPanelBtn(vbox, "Toggle HP Bars", () => { ToggleHPBars(); });

            AddSeparator(vbox);

            // ── AUDIO ──
            AddSectionHeader(vbox, "AUDIO", CyanHeader);
            AddPanelBtn(vbox, "List Audio Buses", () => { ListAudioBuses(); });
            AddPanelBtn(vbox, "Mute SFX", () => { ToggleBusMute("SFX"); });
            AddPanelBtn(vbox, "Mute Music", () => { ToggleBusMute("Music"); });
            AddPanelBtn(vbox, "Play Test Sound", () => { PlayTestSound(); });

            AddSeparator(vbox);

            // ── CHARACTERS ──
            AddSectionHeader(vbox, "CHARACTERS", CyanHeader);
            AddPanelBtn(vbox, "Player Stats", () => { PrintPlayerStats(); });
            AddPanelBtn(vbox, "Harvester Stats", () => { PrintHarvesterStats(); });
            AddPanelBtn(vbox, "List Enemies", () => { ListEnemies(); });
            AddPanelBtn(vbox, "Damage Player 20", () => { DamagePlayer(20); });
            AddPanelBtn(vbox, "Max All Stats", () => { MaxAllStats(); });

            AddSeparator(vbox);

            // ── CAMERA ──
            AddSectionHeader(vbox, "CAMERA", CyanHeader);
            AddPanelBtn(vbox, "Toggle Freecam", () => { ToggleFreecam(); });
            AddPanelBtn(vbox, "Reset Camera", () => { ResetCamera(); });
            AddPanelBtn(vbox, "Zoom In", () => { AdjustZoom(-5f); });
            AddPanelBtn(vbox, "Zoom Out", () => { AdjustZoom(5f); });

            AddSeparator(vbox);

            // ── GAME SPEED ──
            AddSectionHeader(vbox, "GAME SPEED", CyanHeader);
            var speedRow = new HBoxContainer();
            speedRow.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(speedRow);
            AddBtn(speedRow, "1x", () => { Engine.TimeScale = 1; FloatMsg("Speed: 1x"); });
            AddBtn(speedRow, "3x", () => { Engine.TimeScale = 3; FloatMsg("Speed: 3x"); });
            AddBtn(speedRow, "5x", () => { Engine.TimeScale = 5; FloatMsg("Speed: 5x"); });
            AddBtn(speedRow, "10x", () => { Engine.TimeScale = 10; FloatMsg("Speed: 10x"); });
            AddPanelBtn(vbox, "Pause", () => {
                GameManager.Instance?.TogglePause();
                FloatMsg(GetTree().Paused ? "Paused" : "Unpaused");
            });

            AddSeparator(vbox);

            // ── UNLOCK / CHEATS ──
            AddSectionHeader(vbox, "UNLOCK / CHEATS", GoldHeader);
            AddPanelBtn(vbox, "God Mode", () => { _godMode = !_godMode; FloatMsg($"God Mode: {(_godMode ? "ON" : "OFF")}"); });
            AddPanelBtn(vbox, "Instant Kill", () => { _instantKill = !_instantKill; FloatMsg($"Instant Kill: {(_instantKill ? "ON" : "OFF")}"); });
            AddPanelBtn(vbox, "+1000 Scrap", () => { GameManager.Instance?.AddScrap(1000); FloatMsg("+1000 Scrap"); });
            AddPanelBtn(vbox, "+10 Lives", () => {
                var gm = GameManager.Instance;
                if (gm != null) { gm.SetCoreLives(gm.CoreLives + 10); FloatMsg($"Lives: {gm.CoreLives}"); }
            });
            AddPanelBtn(vbox, "Max Magic", () => {
                if (ServiceLocator.TryGet<VinePlayer>(out var p))
                {
                    p.CurrentMagic = p.MaxMagic;
                    GameEvents.OnPlayerMagicChanged?.Invoke(p.CurrentMagic, p.MaxMagic);
                }
                GameManager.Instance?.SetMagic(100f);
                FloatMsg("Magic maxed");
            });
            AddPanelBtn(vbox, "Unlock All Perks", () => { UnlockAllPerks(); });
            AddPanelBtn(vbox, "Skip to Boss", () => { SkipToBoss(); });
            AddPanelBtn(vbox, "Win Floor", () => { WinFloor(); });

            AddSeparator(vbox);

            // ── AXIS CHAOS ──
            AddSectionHeader(vbox, "AXIS CHAOS", new Color(0.95f, 0.2f, 0.2f));
            AddPanelBtn(vbox, "Trigger AXIS Chaos", () => { TriggerChaos(); });

            AddSeparator(vbox);

            // ── UTILITY ──
            AddSectionHeader(vbox, "UTILITY", GrayHeader);
            AddPanelBtn(vbox, "Bug Report", () => { BugReportDialog.Show(GetTree()); });
            AddPanelBtn(vbox, "Kill All Enemies", () => { KillAllEnemies(); FloatMsg("All enemies killed"); });
        }

        // ════════════════════════════════════════════════════════════════
        // Floating feedback (always visible at top-center)
        // ════════════════════════════════════════════════════════════════

        private void BuildFloatingFeedback()
        {
            _floatingFeedback = new Label();
            _floatingFeedback.AnchorLeft = 0.5f;
            _floatingFeedback.AnchorRight = 0.5f;
            _floatingFeedback.AnchorTop = 0;
            _floatingFeedback.AnchorBottom = 0;
            _floatingFeedback.OffsetTop = 8;
            _floatingFeedback.OffsetLeft = -200;
            _floatingFeedback.OffsetRight = 200;
            _floatingFeedback.HorizontalAlignment = HorizontalAlignment.Center;
            _floatingFeedback.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.5f));
            _floatingFeedback.AddThemeFontSizeOverride("font_size", 14);
            _floatingFeedback.Visible = false;
            AddChild(_floatingFeedback);
        }

        // ════════════════════════════════════════════════════════════════
        // Panel UI helpers
        // ════════════════════════════════════════════════════════════════

        private void AddSectionHeader(VBoxContainer parent, string text, Color color)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.AddThemeColorOverride("font_color", color);
            lbl.AddThemeFontSizeOverride("font_size", 13);
            parent.AddChild(lbl);
        }

        private void AddPanelBtn(VBoxContainer parent, string text, System.Action action)
        {
            var btn = new Button();
            btn.Text = text;
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.Pressed += () => action();
            parent.AddChild(btn);
        }

        private static void AddSeparator(VBoxContainer parent)
        {
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 6);
            parent.AddChild(sep);
        }

        private void AddBtn(HBoxContainer row, string text, System.Action action, Color? tint = null)
        {
            var btn = new Button();
            btn.Text = text;
            btn.AddThemeFontSizeOverride("font_size", 11);
            if (tint.HasValue)
                btn.AddThemeColorOverride("font_color", tint.Value);
            btn.Pressed += () => action();
            row.AddChild(btn);
        }

        // ════════════════════════════════════════════════════════════════
        // Name tags
        // ════════════════════════════════════════════════════════════════

        private void ToggleNameTags()
        {
            _showNames = !_showNames;
            if (_showNames)
                SpawnNameTags();
            else
                ClearNameTags();
            FloatMsg($"Name Tags: {(_showNames ? "ON" : "OFF")}");
        }

        private void SpawnNameTags()
        {
            ClearNameTags();
            var tree = GetTree();
            if (tree == null) return;

            // Enemies
            foreach (var node in tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (node is VineEnemy enemy && enemy.IsAlive)
                    AddNameLabel(enemy, enemy.EnemyName, new Color(1f, 0.3f, 0.3f));
            }

            // Player
            if (ServiceLocator.TryGet<VinePlayer>(out var player))
                AddNameLabel(player, "Player", CyanHeader);

            // Harvester
            if (ServiceLocator.TryGet<VineHarvester>(out var harv))
                AddNameLabel(harv, "Harvester", GoldHeader);

            // Towers / vine nodes
            foreach (var node in tree.GetNodesInGroup(Constants.GROUP_VINE_NODE))
            {
                if (node is Node3D n3d)
                    AddNameLabel(n3d, n3d.Name, new Color(0.3f, 1f, 0.4f));
            }
        }

        private void AddNameLabel(Node3D target, string text, Color color)
        {
            var lbl = new Label3D();
            lbl.Text = text;
            lbl.Modulate = color;
            lbl.FontSize = 32;
            lbl.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            lbl.NoDepthTest = true;
            lbl.FixedSize = true;
            lbl.PixelSize = 0.005f;
            target.AddChild(lbl);
            lbl.Position = new Vector3(0, 2f, 0);
            _nameLabels.Add(lbl);
            _nameTargets.Add(target);
        }

        private void UpdateNameTags()
        {
            for (int i = _nameLabels.Count - 1; i >= 0; i--)
            {
                if (!IsInstanceValid(_nameTargets[i]) || !IsInstanceValid(_nameLabels[i]))
                {
                    if (IsInstanceValid(_nameLabels[i])) _nameLabels[i].QueueFree();
                    _nameLabels.RemoveAt(i);
                    _nameTargets.RemoveAt(i);
                }
            }
        }

        private void ClearNameTags()
        {
            foreach (var lbl in _nameLabels)
            {
                if (IsInstanceValid(lbl)) lbl.QueueFree();
            }
            _nameLabels.Clear();
            _nameTargets.Clear();
        }

        // ════════════════════════════════════════════════════════════════
        // HP bars
        // ════════════════════════════════════════════════════════════════

        private void ToggleHPBars()
        {
            _showHP = !_showHP;
            if (_showHP)
                SpawnHPBars();
            else
                ClearHPBars();
            FloatMsg($"HP Bars: {(_showHP ? "ON" : "OFF")}");
        }

        private void SpawnHPBars()
        {
            ClearHPBars();
            var tree = GetTree();
            if (tree == null) return;

            foreach (var node in tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (node is VineEnemy enemy && enemy.IsAlive)
                    AddHPLabel(enemy);
            }

            if (ServiceLocator.TryGet<VinePlayer>(out var player))
                AddHPLabel(player);
        }

        private void AddHPLabel(Node3D target)
        {
            var lbl = new Label3D();
            lbl.Modulate = new Color(1f, 1f, 0.3f);
            lbl.FontSize = 28;
            lbl.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            lbl.NoDepthTest = true;
            lbl.FixedSize = true;
            lbl.PixelSize = 0.004f;
            float yOffset = _showNames ? 2.8f : 2f;
            target.AddChild(lbl);
            lbl.Position = new Vector3(0, yOffset, 0);
            _hpLabels.Add(lbl);
            _hpTargets.Add(target);
        }

        private void UpdateHPBars()
        {
            for (int i = _hpLabels.Count - 1; i >= 0; i--)
            {
                if (!IsInstanceValid(_hpTargets[i]) || !IsInstanceValid(_hpLabels[i]))
                {
                    if (IsInstanceValid(_hpLabels[i])) _hpLabels[i].QueueFree();
                    _hpLabels.RemoveAt(i);
                    _hpTargets.RemoveAt(i);
                    continue;
                }

                var target = _hpTargets[i];
                if (target is VineEnemy enemy)
                    _hpLabels[i].Text = $"{enemy.CurrentHealth:F0}/{enemy.MaxHealth:F0}";
                else if (target is VinePlayer player)
                    _hpLabels[i].Text = $"{player.CurrentHP:F0}/{player.MaxHP:F0}";
            }

            // Spawn labels for new enemies that appeared since last spawn
            var tree = GetTree();
            if (tree == null) return;
            foreach (var node in tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (node is VineEnemy enemy && enemy.IsAlive && !_hpTargets.Contains(enemy))
                    AddHPLabel(enemy);
            }
        }

        private void ClearHPBars()
        {
            foreach (var lbl in _hpLabels)
            {
                if (IsInstanceValid(lbl)) lbl.QueueFree();
            }
            _hpLabels.Clear();
            _hpTargets.Clear();
        }

        // ════════════════════════════════════════════════════════════════
        // Audio
        // ════════════════════════════════════════════════════════════════

        private void ListAudioBuses()
        {
            int count = AudioServer.BusCount;
            var lines = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string name = AudioServer.GetBusName(i);
                float vol = AudioServer.GetBusVolumeDb(i);
                bool muted = AudioServer.IsBusMute(i);
                lines.Add($"{name}: {vol:F1}dB {(muted ? "[MUTED]" : "")}");
            }
            FloatMsg(string.Join(" | ", lines));
        }

        private void ToggleBusMute(string busName)
        {
            int idx = AudioServer.GetBusIndex(busName);
            if (idx < 0) { FloatMsg($"Bus '{busName}' not found"); return; }
            bool muted = AudioServer.IsBusMute(idx);
            AudioServer.SetBusMute(idx, !muted);
            FloatMsg($"{busName}: {(!muted ? "MUTED" : "UNMUTED")}");
        }

        private void PlayTestSound()
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
            {
                audio.PlaySFXByName("sfx.hit_1");
                FloatMsg("Playing test sound");
            }
            else
            {
                FloatMsg("AudioManager not available");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Characters
        // ════════════════════════════════════════════════════════════════

        private void PrintPlayerStats()
        {
            if (!ServiceLocator.TryGet<VinePlayer>(out var p)) { FloatMsg("Player not found"); return; }
            FloatMsg($"HP:{p.CurrentHP:F0}/{p.MaxHP:F0} Mana:{p.CurrentMagic:F0}/{p.MaxMagic:F0} Spd:{p.MoveSpeed:F1} Atk:{p.AttackDamage:F1} Pos:{p.GlobalPosition}");
        }

        private void PrintHarvesterStats()
        {
            if (!ServiceLocator.TryGet<VineHarvester>(out var h)) { FloatMsg("Harvester not found"); return; }
            FloatMsg($"HP:{h.CurrentHP:F0}/{h.MaxHP:F0} Mode:{h.CurrentMode} Magic:{h.SelectedMagic} Acc:{h.MagicAccumulated:F1}");
        }

        private void ListEnemies()
        {
            var tree = GetTree();
            if (tree == null) return;
            var enemies = tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
            int alive = 0;
            var factions = new Dictionary<string, int>();
            foreach (var node in enemies)
            {
                if (node is VineEnemy enemy && enemy.IsAlive)
                {
                    alive++;
                    string f = enemy.Faction.ToString();
                    factions[f] = factions.GetValueOrDefault(f) + 1;
                }
            }
            string breakdown = string.Join(", ", factions);
            FloatMsg($"Enemies: {alive} alive — {breakdown}");
        }

        private void DamagePlayer(float amount)
        {
            if (ServiceLocator.TryGet<VinePlayer>(out var p))
            {
                p.TakeDamage(amount);
                FloatMsg($"Player -{amount} HP ({p.CurrentHP:F0} remaining)");
            }
            else
            {
                FloatMsg("Player not found");
            }
        }

        private void MaxAllStats()
        {
            if (ServiceLocator.TryGet<VinePlayer>(out var p))
            {
                p.CurrentHP = p.MaxHP;
                p.CurrentMagic = p.MaxMagic;
                GameEvents.OnPlayerHPChanged?.Invoke(p.CurrentHP, p.MaxHP);
                GameEvents.OnPlayerMagicChanged?.Invoke(p.CurrentMagic, p.MaxMagic);
            }
            if (ServiceLocator.TryGet<VineHarvester>(out var h))
                h.Heal(h.MaxHP);
            FloatMsg("All stats maxed");
        }

        // ════════════════════════════════════════════════════════════════
        // Camera
        // ════════════════════════════════════════════════════════════════

        private void ToggleFreecam()
        {
            _freecam = !_freecam;
            if (_freecam && ServiceLocator.TryGet<TDCamera>(out var cam))
            {
                _freecamPos = cam.GlobalPosition;
                _freecamYaw = cam.GlobalRotation.Y;
                _freecamPitch = Mathf.RadToDeg(cam.GlobalRotation.X);
            }
            FloatMsg($"Freecam: {(_freecam ? "ON (WASD+QE)" : "OFF")}");
        }

        private void ProcessFreecam(double delta)
        {
            if (!ServiceLocator.TryGet<TDCamera>(out var cam)) return;

            float dt = (float)delta;
            float speed = 20f;
            if (Input.IsKeyPressed(Key.Shift)) speed *= 3f;

            var move = Vector3.Zero;
            if (Input.IsKeyPressed(Key.W)) move.Z -= 1;
            if (Input.IsKeyPressed(Key.S)) move.Z += 1;
            if (Input.IsKeyPressed(Key.A)) move.X -= 1;
            if (Input.IsKeyPressed(Key.D)) move.X += 1;
            if (Input.IsKeyPressed(Key.Q)) move.Y -= 1;
            if (Input.IsKeyPressed(Key.E)) move.Y += 1;

            if (move.LengthSquared() > 0)
            {
                move = move.Normalized() * speed * dt;
                // Transform movement by camera yaw
                var basis = new Basis(Vector3.Up, _freecamYaw);
                _freecamPos += basis * move;
            }

            cam.GlobalPosition = _freecamPos;
            cam.GlobalRotation = new Vector3(Mathf.DegToRad(_freecamPitch), _freecamYaw, 0);
        }

        private void ResetCamera()
        {
            _freecam = false;
            FloatMsg("Camera reset to player follow");
        }

        private void AdjustZoom(float amount)
        {
            if (!ServiceLocator.TryGet<TDCamera>(out var cam)) return;
            // Simulate scroll zoom by adjusting the camera's zoom field via reflection-free approach:
            // We directly reposition. But TDCamera controls its own position, so we just message.
            // The simplest approach: use InputEventMouseButton to fake scroll
            var ev = new InputEventMouseButton();
            ev.ButtonIndex = amount < 0 ? MouseButton.WheelUp : MouseButton.WheelDown;
            ev.Pressed = true;
            // Send multiple scroll events for larger zoom steps
            int steps = Mathf.Abs((int)(amount));
            for (int i = 0; i < steps; i++)
                cam._UnhandledInput(ev);
            FloatMsg($"Zoom {(amount < 0 ? "In" : "Out")}");
        }

        // ════════════════════════════════════════════════════════════════
        // Unlock / Cheats
        // ════════════════════════════════════════════════════════════════

        private void UnlockAllPerks()
        {
            var gm = GameManager.Instance;
            if (gm == null) { FloatMsg("GameManager not available"); return; }

            var allPerks = VinePerkRegistry.GetAll();
            int added = 0;
            foreach (var perk in allPerks)
            {
                if (!gm.ActivePerks.Contains(perk))
                {
                    gm.AddPerk(perk);
                    added++;
                }
            }
            FloatMsg($"Unlocked {added} perks ({allPerks.Count} total)");
        }

        private void SkipToBoss()
        {
            // Kill all current enemies to clear the wave, triggering wave completion
            // The last wave of a floor is the boss wave
            KillAllEnemies();
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // Set wave to last wave so next wave start will be boss
                gm.CurrentWave = Constants.VINE_WAVES_PER_FLOOR;
                gm.SetPhase(GamePhase.Build);
            }
            FloatMsg("Skipped to boss wave — start next wave for boss");
        }

        private void WinFloor()
        {
            KillAllEnemies();
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SetPhase(GamePhase.FloorComplete);
                GameEvents.OnFloorCompleted?.Invoke(gm.CurrentFloor);
                GetTree().CreateTimer(1.0f).Timeout += () =>
                    gm.ShowMetaPerkOrPerkSelect();
            }
            FloatMsg("Floor complete!");
        }

        // ════════════════════════════════════════════════════════════════
        // Shared helpers
        // ════════════════════════════════════════════════════════════════

        private void TriggerChaos()
        {
            var cm = GetTree().CurrentScene?.GetNodeOrNull<CorruptionManager>("CorruptionManager");
            if (cm == null) { FloatMsg("CorruptionManager not found (are you in battle?)"); return; }
            cm.DebugTriggerChaos();
            FloatMsg("AXIS CHAOS triggered!");
        }

        private void SetMagic(MagicType type)
        {
            if (ServiceLocator.TryGet<VineHarvester>(out var h))
            {
                h.SelectMagicType(type);
                Msg($"Magic type: {type}");
            }
        }

        private void KillAllEnemies()
        {
            var tree = GetTree();
            if (tree == null) return;
            foreach (var node in tree.GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (node is VineEnemy enemy && enemy.IsAlive)
                    enemy.TakeDamage(99999);
            }
        }

        private void Msg(string text)
        {
            _feedback.Text = text;
            _feedbackTimer = 4.0;
            GD.Print($"[Debug] {text}");
        }

        private void FloatMsg(string text)
        {
            _floatingFeedback.Text = text;
            _floatingFeedback.Visible = true;
            _floatingTimer = 3.0;
            GD.Print($"[Debug] {text}");
        }

        // ════════════════════════════════════════════════════════════════
        // Console commands
        // ════════════════════════════════════════════════════════════════

        private void OnCommand(string text)
        {
            _input.Clear();
            if (string.IsNullOrWhiteSpace(text)) return;

            string cmd = text.Trim().ToLower();
            string[] parts = cmd.Split(' ');

            switch (parts[0])
            {
                case "help":
                    Msg("scrap [n], kill, god, instakill, heal, damage [n], magic [type], toggle, speed [n], floor [n], names, hp, freecam, perks, lives [n], boss, win, chaos");
                    break;
                case "scrap":
                    int amount = parts.Length > 1 && int.TryParse(parts[1], out int s) ? s : 100;
                    GameManager.Instance?.AddScrap(amount);
                    Msg($"+{amount} Scrap");
                    break;
                case "kill":
                    KillAllEnemies();
                    Msg("All enemies killed");
                    break;
                case "god":
                    _godMode = !_godMode;
                    Msg($"God Mode: {(_godMode ? "ON" : "OFF")}");
                    break;
                case "instakill":
                    _instantKill = !_instantKill;
                    Msg($"Instant Kill: {(_instantKill ? "ON" : "OFF")}");
                    break;
                case "heal":
                    if (ServiceLocator.TryGet<VineHarvester>(out var hh)) hh.Heal(hh.MaxHP);
                    if (ServiceLocator.TryGet<VinePlayer>(out var pp))
                    {
                        pp.CurrentHP = pp.MaxHP;
                        GameEvents.OnPlayerHPChanged?.Invoke(pp.CurrentHP, pp.MaxHP);
                    }
                    Msg("Healed all");
                    break;
                case "damage":
                    float dmg = parts.Length > 1 && float.TryParse(parts[1], out float d) ? d : 20;
                    if (ServiceLocator.TryGet<VineHarvester>(out var hd)) hd.TakeDamage(dmg);
                    Msg($"Harvester -{dmg} HP");
                    break;
                case "magic":
                    if (parts.Length > 1)
                    {
                        MagicType mt = parts[1] switch
                        {
                            "chaos" => MagicType.Chaos,
                            "power" => MagicType.Power,
                            "env" or "environment" => MagicType.Environment,
                            _ => MagicType.None
                        };
                        if (mt != MagicType.None) SetMagic(mt);
                        else Msg("Unknown magic type. Use: chaos, power, env");
                    }
                    break;
                case "toggle":
                    if (ServiceLocator.TryGet<VineHarvester>(out var ht)) { ht.ToggleMode(); Msg($"Mining: {ht.CurrentMode}"); }
                    break;
                case "speed":
                    double spd = parts.Length > 1 && double.TryParse(parts[1], out double sv) ? sv : 1;
                    Engine.TimeScale = spd;
                    Msg($"Speed: {spd}x");
                    break;
                case "floor":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int fl))
                    {
                        if (GameManager.Instance != null) GameManager.Instance.CurrentFloor = fl;
                        Msg($"Floor set to {fl} (restart battle to see)");
                    }
                    break;
                // ── New commands ──
                case "names":
                    ToggleNameTags();
                    break;
                case "hp":
                    ToggleHPBars();
                    break;
                case "freecam":
                    ToggleFreecam();
                    break;
                case "perks":
                    UnlockAllPerks();
                    break;
                case "lives":
                    int lv = parts.Length > 1 && int.TryParse(parts[1], out int lvi) ? lvi : 10;
                    GameManager.Instance?.SetCoreLives(lv);
                    Msg($"Lives set to {lv}");
                    break;
                case "boss":
                    SkipToBoss();
                    break;
                case "win":
                    WinFloor();
                    break;
                case "chaos":
                case "corrupt":
                    TriggerChaos();
                    break;
                default:
                    Msg($"Unknown: {cmd}. Type 'help'.");
                    break;
            }
        }
    }
}
