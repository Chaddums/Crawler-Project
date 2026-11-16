using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A bot run to look at (VISUAL_RUN=&lt;folder&gt;, with a display): the bot plays as usual while
    /// this saves screenshots at the moments a player sees something new (the start, the first
    /// waves, each themed block's announcement and fight, the wave card, safe rooms) and goes
    /// through safe rooms the way a player does, with real key presses: O opens the crates, the
    /// reveal, E wears a relic, Space collects, then Space again at once (which must not send a
    /// wave). VISUAL_END=n ends the run on wave n by breaking the Spire, to shoot the debrief.
    /// Each shot is logged as "[Visual] NN label".
    /// </summary>
    public partial class VisualWalk : Node
    {
        public static VisualWalk Instance { get; private set; }

        private string _dir;
        private int _n, _endWave, _lastWave = -1, _safeStep, _safeWave = -1;
        private GamePhase _lastPhase = (GamePhase)(-1);
        private ulong _safeAt;
        private bool _detailsShot, _ended, _started;
        private readonly List<(ulong at, string label)> _due = new();
        private readonly List<(ulong at, string label)> _realDue = new();
        private int _wavesAtCollect = -1;
        private string _lastSaid;

        public static VisualWalk TryCreate(Node parent)
        {
            string dir = OS.GetEnvironment("VISUAL_RUN");
            if (string.IsNullOrEmpty(dir) || DisplayServer.GetName() == "headless") return null;
            DirAccess.MakeDirRecursiveAbsolute(dir);
            var w = new VisualWalk { Name = "VisualWalk", _dir = dir, ProcessMode = ProcessModeEnum.Always };
            int.TryParse(OS.GetEnvironment("VISUAL_END"), out w._endWave);
            parent.AddChild(w);
            Instance = w;
            GameEvents.OnAnnouncement += t => { if (w._warm != 2) w._lastSaid = t; };
            w._watchable = RealRenderer();
            GD.Print($"[Visual] renderer: {RenderingServer.GetVideoAdapterName()} ({(w._watchable ? "drawn throughout, watchable" : "drawn only for shots")})");
            GD.Print($"[Visual] shots to {dir}, ending on W{w._endWave}");
            return w;
        }

        private static ulong Now => Time.GetTicksMsec();

        // The software renderer here draws about a frame a second at 1080p, so the picture is
        // only drawn for a shot: asked for, the render loop comes on for a few frames, the shot
        // is saved and it goes off again. The run plays at a headless run's speed in between.
        private readonly Queue<string> _wanted = new();
        private int _warm;

        public void Shot(string label) { _wanted.Enqueue(label); if (_watchable) _warm = System.Math.Max(_warm, 2); else if (RenderingServer.RenderLoopEnabled) _warm = System.Math.Max(_warm, 3); }

        /// <summary>
        /// On a real graphics card the picture stays on, so the run can be watched; only a software
        /// renderer (llvmpipe and the like, a frame a second) draws just for the shots.
        /// VISUAL_DRAW=1 or 0 says which.
        /// </summary>
        private bool _watchable;

        private static bool RealRenderer()
        {
            string say = OS.GetEnvironment("VISUAL_DRAW");
            if (say == "1") return true;
            if (say == "0") return false;
            string card = (RenderingServer.GetVideoAdapterName() ?? "").ToLowerInvariant();
            return card.Length > 0 && !card.Contains("llvmpipe") && !card.Contains("softpipe") && !card.Contains("swiftshader") && !card.Contains("lavapipe");
        }

        private void Capture()
        {
            if (_watchable)
            {
                // A frame after it's asked for, so what the step just changed is on screen
                if (_wanted.Count > 0 && --_warm <= 0) { Save(_wanted.Dequeue()); _warm = 2; }
                return;
            }
            if (_wanted.Count == 0) { if (RenderingServer.RenderLoopEnabled && _hold <= 0) RenderingServer.RenderLoopEnabled = false; return; }
            if (!RenderingServer.RenderLoopEnabled) { RenderingServer.RenderLoopEnabled = true; _warm = 3; return; }
            if (--_warm > 0)
            {
                // An announcement is gone by the time the picture is being drawn again: say it once more for its shot
                if (_warm == 2 && _wanted.Peek().Contains("announce") && _lastSaid != null) GameEvents.OnAnnouncement?.Invoke(_lastSaid);
                return;
            }
            Save(_wanted.Dequeue());
            _warm = 1;
        }

        /// <summary>Frames to keep drawing after a shot (a sequence that plays in real time).</summary>
        private int _hold;

        private void Save(string label)
        {
            var img = GetViewport().GetTexture().GetImage();
            string file = $"{_dir}/{++_n:00}_{label}.png";
            img.SavePng(file);
            var gm = GameManager.Instance;
            GD.Print($"[Visual] {_n:00} {label} (W{gm?.CurrentWave}, {gm?.CurrentPhase}, {Engine.GetFramesPerSecond():0} fps)");
        }

        // Shots during play are timed in game seconds (the run plays faster than real time)
        private double _clock;
        private ulong Game => (ulong)(_clock * 1000.0);
        private void In(float seconds, string label) => _due.Add((Game + (ulong)(seconds * 1000f), label));

        private static void Press(Key key)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        }

        public override void _Process(double delta)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (!GetTree().Paused) _clock += delta;
            Capture();
            for (int i = _realDue.Count - 1; i >= 0; i--)
                if (Now >= _realDue[i].at) { string l = _realDue[i].label; _realDue.RemoveAt(i); Shot(l); }
            for (int i = _due.Count - 1; i >= 0; i--)
                if (Game >= _due[i].at && !_due[i].label.StartsWith("__")) { string l = _due[i].label; _due.RemoveAt(i); Shot(l); }

            var phase = gm.CurrentPhase;
            int wave = gm.CurrentWave;
            if (phase != _lastPhase)
            {
                if (!_started && phase == GamePhase.Build) { _started = true; In(3f, "start"); In(20f, "start_later"); }
                // Soon after the clear: the bot sends the next wave within a couple of seconds (at
                // 2x the card was shot a wave late), and the clear's banner is still up
                if (phase == GamePhase.Build && wave >= 5 && wave % 5 == 0) In(1.6f, $"W{wave:00}_build_card");
                _lastPhase = phase;
            }
            if (wave != _lastWave && phase == GamePhase.Wave)
            {
                _lastWave = wave;
                bool blockStart = WaveThemes.At(wave) != null && WaveThemes.BlockStart(WaveThemes.BlockOf(wave)) == wave;
                if (blockStart) In(1.5f, $"W{wave:00}_theme_announce");
                if (wave <= 3 || blockStart || WaveThemes.At(wave) != null && (wave - WaveThemes.FirstWave) % WaveThemes.BlockSize == 2)
                    In(14f, $"W{wave:00}_fight");
                if (!_detailsShot && wave == 8) { _detailsShot = true; _due.Add((Game + 20000, "__details")); }
                if (_endWave > 0 && wave >= _endWave && !_ended) { _ended = true; _due.Add((Game + 15000, "__end")); }
            }
            for (int i = _due.Count - 1; i >= 0; i--)
            {
                if (Game < _due[i].at || !_due[i].label.StartsWith("__")) continue;
                string l = _due[i].label;
                _due.RemoveAt(i);
                if (l == "__details") { Press(Key.I); In(1f, "W08_details"); _due.Add((Game + 6000, "__details_off")); }
                else if (l == "__details_off") Press(Key.I);
                else if (l == "__end" && ServiceLocator.TryGet<VineGrid>(out var grid) && grid.Harvester != null)
                {
                    grid.Harvester.TakeDamage(grid.Harvester.CurrentHP + 1f);
                    _hold = 1;
                    _realDue.Add((Now + 3000, "the_end")); _realDue.Add((Now + 12000, "debrief")); _realDue.Add((Now + 18000, "debrief_later"));
                }
            }
        }

        /// <summary>In the middle of a safe room's steps (the room itself ends when the crates are collected).</summary>
        public bool InSafeFlow => _safeStep > 0 && _safeStep < 11;

        /// <summary>
        /// Goes through a safe room as a player does. The bot calls it every frame while one is on;
        /// true once the crates are collected and the room is left.
        /// </summary>
        public bool SafeRoom(VineWaveManager wm, LootManager loot)
        {
            if (_safeWave != wm.SafeRoomWave) { _safeWave = wm.SafeRoomWave; _safeStep = 0; _safeAt = Now; _hold = 1; RenderingServer.RenderLoopEnabled = true; }
            // A shot waits its turn: the steps go on once it's saved
            if (_wanted.Count > 0) { _safeAt = Now; return false; }
            float t = (Now - _safeAt) / 1000f;
            void Next() { _safeStep++; _safeAt = Now; }
            switch (_safeStep)
            {
                case 0: if (t > 2.5f) { Shot($"W{_safeWave:00}_safe_room"); Next(); } break;
                case 1: Press(Key.O); Next(); break;
                case 2:
                    if (!LootCeremony.IsOpen && t > 4f) { GD.PushWarning($"[Visual] O didn't open the crates in the W{_safeWave} safe room"); loot.OpenAll(); Next(); }
                    else if (LootCeremony.IsOpen && t > 1.6f) { Shot($"W{_safeWave:00}_opening"); Next(); }
                    break;
                case 3: if (t > 2.2f) { Shot($"W{_safeWave:00}_opening_later"); Next(); } break;
                case 4: Press(Key.Space); Next(); break;
                case 5: if (t > 2f) { Shot($"W{_safeWave:00}_revealed"); Next(); } break;
                case 6: Press(Key.E); Next(); break;
                case 7: if (t > 1.5f) { Shot($"W{_safeWave:00}_equipped"); Next(); } break;
                case 8: _wavesAtCollect = GameManager.Instance.CurrentWave; Press(Key.Space); Next(); break;
                case 9:
                    // More crates may open one after another: skip and collect each
                    if (LootCeremony.IsOpen && t > 2.5f) { Press(Key.Space); _safeAt = Now; }
                    else if (!LootCeremony.IsOpen && loot.Carried.Count > 0 && t > 1.5f && !loot.OpenAll()) Next();
                    else if (!LootCeremony.IsOpen && loot.Carried.Count == 0) { Press(Key.Space); Press(Key.Space); Next(); }
                    break;
                case 10:
                    if (t > 1.5f)
                    {
                        Shot($"W{_safeWave:00}_after_collect");
                        var gm = GameManager.Instance;
                        GD.Print($"[Visual] Space after collecting: wave {_wavesAtCollect} -> {gm.CurrentWave}, phase {gm.CurrentPhase}"
                            + (gm.CurrentWave != _wavesAtCollect || gm.CurrentPhase == GamePhase.Wave ? "  ** SENT A WAVE **" : " (no wave sent)"));
                        _hold = 0;
                        Next();
                    }
                    break;
                default: _hold = 0; return true;
            }
            return false;
        }
    }
}
