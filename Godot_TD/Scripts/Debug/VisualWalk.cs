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
            w.LoadSweep();
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
            if (_sweep != null) { Sweep(); return; }
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
                // Straight after the clear: the bot sends the next wave within a second (at 2x the
                // card was shot a wave late), and the clear's banner is up
                if (phase == GamePhase.Build && wave >= 5 && wave % 5 == 0) In(0.5f, $"W{wave:00}_build_card");
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

        // ── Look sweep ──
        // A file "sweep.json" in the shots folder turns the run into a comparison of looks on
        // this machine's renderer: a few seconds in, three enemies are stood on the floor by the
        // Spire, the game is frozen, and each variant in the file is applied in turn and shot
        // ("sweep_NN_name.png"), the same frame under each look. Then the game quits.
        // {"variants":[{"name":"as_is"},{"name":"glow_off","glow":0},{"name":"dim","bitEmission":0.04,"gridEmission":0.15}]}
        // Keys: glow (0/1), glowIntensity, glowBloom, glowThreshold, glowStrength, exposure,
        // ambient, sun, fill, bitEmission, gridEmission, gridWidth, bitMetallic, bitRoughness,
        // domeRim, domeFill.
        private List<(string name, Dictionary<string, float> set)> _sweep;
        private Dictionary<string, float> _sweepBase;
        private int _sweepAt = -2, _sweepFrames;

        private void LoadSweep()
        {
            string file = _dir + "/sweep.json";
            if (!FileAccess.FileExists(file)) return;
            var json = new Json();
            if (json.Parse(FileAccess.GetFileAsString(file)) != Error.Ok || json.Data.Obj is not Godot.Collections.Dictionary d
                || !d.TryGetValue("variants", out var arr) || arr.Obj is not Godot.Collections.Array list)
            {
                GD.PushWarning($"[Visual] {file} didn't parse; playing the usual run");
                return;
            }
            _sweep = new();
            foreach (var v in list)
            {
                if (v.Obj is not Godot.Collections.Dictionary one) continue;
                var set = new Dictionary<string, float>();
                string name = "variant";
                foreach (var k in one.Keys)
                {
                    if (k.AsString() == "name") name = one[k].AsString();
                    else set[k.AsString()] = (float)one[k].AsDouble();
                }
                _sweep.Add((name, set));
            }
            GD.Print($"[Visual] look sweep: {_sweep.Count} variants from {file}");
        }

        // Only what the battle's own world draws with: other viewports (the crate reveal, menus)
        // bring their own environment and lights
        private void FindLights(Node n, World3D world, List<DirectionalLight3D> into)
        {
            if (n is DirectionalLight3D l && l.GetWorld3D() == world) into.Add(l);
            foreach (var c in n.GetChildren()) FindLights(c, world, into);
        }

        /// <summary>Reads (apply null) or sets the look's numbers. The first light is the sun, the second the fill.</summary>
        private Dictionary<string, float> Look(Dictionary<string, float> apply)
        {
            var now = new Dictionary<string, float>();
            var world = GetViewport().FindWorld3D();
            var env = world?.Environment;
            var lights = new List<DirectionalLight3D>();
            FindLights(GetTree().Root, world, lights);
            ShaderMaterial ground = ServiceLocator.TryGet<VineGrid>(out var grid) ? grid.GroundShaderMat : null;
            ShaderMaterial dome = ServiceLocator.TryGet<ConversionDome>(out var cd) ? cd.FloorMaterial : null;
            void Num(string key, System.Func<float> get, System.Action<float> set)
            {
                now[key] = get();
                if (apply != null && apply.TryGetValue(key, out float v)) set(v);
            }
            void Param(ShaderMaterial m, string key, string param)
            {
                if (m == null) return;
                Num(key, () => (float)m.GetShaderParameter(param).AsDouble(), v => m.SetShaderParameter(param, v));
            }
            if (env != null)
            {
                Num("glow", () => env.GlowEnabled ? 1f : 0f, v => env.GlowEnabled = v > 0.5f);
                Num("glowIntensity", () => env.GlowIntensity, v => env.GlowIntensity = v);
                Num("glowBloom", () => env.GlowBloom, v => env.GlowBloom = v);
                Num("glowThreshold", () => env.GlowHdrThreshold, v => env.GlowHdrThreshold = v);
                Num("glowStrength", () => env.GlowStrength, v => env.GlowStrength = v);
                Num("exposure", () => env.TonemapExposure, v => env.TonemapExposure = v);
                Num("ambient", () => env.AmbientLightEnergy, v => env.AmbientLightEnergy = v);
            }
            if (lights.Count > 0) Num("sun", () => lights[0].LightEnergy, v => lights[0].LightEnergy = v);
            if (lights.Count > 1) Num("fill", () => lights[1].LightEnergy, v => lights[1].LightEnergy = v);
            Param(ground, "bitEmission", "bit_emission_strength");
            Param(ground, "gridEmission", "grid_emission_strength");
            Param(ground, "gridWidth", "grid_width");
            Param(ground, "bitMetallic", "bit_metallic");
            Param(ground, "bitRoughness", "bit_roughness");
            Param(dome, "domeRim", "rim_strength");
            Param(dome, "domeFill", "fill_alpha");
            return now;
        }

        private void Sweep()
        {
            var gm = GameManager.Instance;
            if (_sweepAt == -2)
            {
                // A few seconds in: the Spire is down, the dome is lit, the first towers are up
                if (gm.CurrentPhase is not (GamePhase.Build or GamePhase.Wave) || _clock < 9.0) return;
                if (!ServiceLocator.TryGet<VineGrid>(out var grid) || grid.Harvester == null) return;
                // Three enemies stood on the lit floor, where their markers have to be read
                var theme = PlanetTheme.Current;
                var spire = grid.Harvester.GlobalPosition;
                var kinds = new (string name, VineEnemyFaction f, Color c, Vector3 off)[]
                {
                    ("Scrap Rat", VineEnemyFaction.Scavenger, theme.EnemyScavenger, new Vector3(-3.2f, 0, 2.4f)),
                    ("Rust Hulk", VineEnemyFaction.Brute, theme.EnemyBrute, new Vector3(-1.2f, 0, 4.2f)),
                    ("Buzz Drone", VineEnemyFaction.Swarm, theme.EnemySwarm, new Vector3(3.4f, 0, 3.0f)),
                };
                foreach (var k in kinds)
                {
                    var p = spire + k.off;
                    var e = new VineEnemy();
                    grid.Harvester.GetParent().AddChild(e);
                    e.Initialize(k.name, k.f, 1e6f, 0.01f, 0, k.c, grid.WorldToGrid(p));
                    e.GlobalPosition = new Vector3(p.X, grid.GetWorldHeight(p.X, p.Z), p.Z);
                }
                _sweepAt = -1;
                _sweepFrames = 12;
                RenderingServer.RenderLoopEnabled = true;
                return;
            }
            if (--_sweepFrames > 0) return;
            if (_sweepAt == -1)
            {
                GetTree().Paused = true;
                _sweepBase = Look(null);
                GD.Print("[Visual] sweep base: " + string.Join(", ", System.Linq.Enumerable.Select(_sweepBase, kv => $"{kv.Key} {kv.Value:0.###}")));
            }
            else
            {
                var (name, _) = _sweep[_sweepAt];
                var img = GetViewport().GetTexture().GetImage();
                string file = $"{_dir}/sweep_{_sweepAt + 1:00}_{name}.png";
                img.SavePng(file);
                GD.Print($"[Visual] sweep {_sweepAt + 1:00} {name}");
            }
            _sweepAt++;
            if (_sweepAt >= _sweep.Count)
            {
                Look(_sweepBase);
                GD.Print("[Visual] sweep done");
                GetTree().Paused = false;
                _sweep = null;
                GetTree().Quit();
                return;
            }
            Look(_sweepBase);
            Look(_sweep[_sweepAt].set);
            // Glow and exposure settle over a few frames
            _sweepFrames = 24;
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
