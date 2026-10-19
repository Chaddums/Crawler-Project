using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Opening loot crates, ported from the dungeon game's LootBoxCeremonyUI and built up: the
    /// real crate (its Data/Towers sheet) turns in a 3D view over a dimmed battle, the sequence
    /// builds by tier, the crate gives way, and what it held slides in one line at a time
    /// (Epic and better pulse, Legendary pops). The battle is paused throughout.
    ///   pop (Scrap, Supply; several open together): drops in, hops twice, pops in sparks.
    ///   rays (Gold): rises turning, light fans out behind it while it shakes harder, then bangs
    ///     with a ring and a flash.
    ///   shatter (Prism): floats in and slows, cracks of light spread over it, then it breaks
    ///     into pieces that fly at the screen.
    ///   ascend (Ascendant): the screen goes dark, a beam comes down and the cache descends in
    ///     it, three rings lock round it one after another, it implodes, then a white flash.
    /// A click, Space or Enter skips to the contents; once they are all in, the same collects
    /// (<see cref="Collected"/>) and the next opening starts, or the battle carries on
    /// (<see cref="Finished"/>).
    /// </summary>
    public partial class LootCeremony : CanvasLayer
    {
        /// <summary>One opening: <paramref name="Mass"/> is a safe room's haul, every crate at once.</summary>
        public record Opening(List<LootTier> Tiers, List<LootReward> Rewards, string Line, bool Mass = false);

        public event Action<Opening> Collected;
        public event Action Finished;
        /// <summary>EQUIP pressed on a relic in the reveal: equip it now (true when it's on).</summary>
        public event Func<string, bool> EquipRequested;

        /// <summary>A ceremony is on screen (the pause menu leaves Escape to it).</summary>
        public static bool IsOpen => _active != null && IsInstanceValid(_active) && !_active._done;
        private static LootCeremony _active;
        /// <summary>When the last ceremony closed (ms): Space doesn't send a wave for a moment after.</summary>
        public static ulong ClosedAtMs { get; private set; }
        /// <summary>
        /// Space is still the player skipping through an opening: for 2 s after it closes, and for
        /// as long as Space keeps coming less than a second apart (hammered through a slow reveal,
        /// a fixed 2 s ran out mid-hammer and the rest sent waves).
        /// </summary>
        public static bool JustClosed
        {
            get
            {
                ulong now = Time.GetTicksMsec(), frame = Engine.GetProcessFrames();
                // In frames as well, so a hitch (or a slow machine) between presses doesn't let one through
                return ClosedAtMs > 0 && (now - ClosedAtMs < 2000 || frame - _closedFrame < 30)
                    || _lastSkipMs > 0 && (now - _lastSkipMs < 1000 || frame - _lastSkipFrame < 15);
            }
        }
        private static ulong _lastSkipMs, _lastSkipFrame, _closedFrame;
        /// <summary>A Space press spent on skipping (in a ceremony, or held off after one).</summary>
        public static void NoteSkipPress() { _lastSkipMs = Time.GetTicksMsec(); _lastSkipFrame = Engine.GetProcessFrames(); }

        // A safe room's haul: the other crates round the best one, popping in turn, then fireworks
        private readonly List<(Node3D node, Vector3 home, float popAt, LootTier tier)> _satellites = new();
        private Camera3D _cam;
        private float _party;
        /// <summary>Tests: crates on stage in a mass opening (the centre one included) and how many have popped.</summary>
        internal int MassCrates => _satellites.Count + (Current?.Mass == true ? 1 : 0);
        internal int MassPopped { get; private set; }

        // ── State (tests read these) ──
        public Opening Current { get; private set; }
        public string Sequence => _tier?.Sequence ?? "";
        public bool Burst { get; private set; }
        public int Revealed { get; private set; }
        public bool AllRevealed => _panels.Count > 0 && Revealed >= _panels.Count || _panels.Count == 0 && Burst && _t > _revealAt;
        public int Remaining => _queue.Count;
        /// <summary>Lines on the reveal (grouped: Resources together, Materials together, ...).</summary>
        public IReadOnlyList<(string name, string detail, string rarity)> Lines => _lines;
        internal float Clock => _t;
        internal float BurstAt => _burstAt;

        private readonly Queue<Opening> _queue = new();
        private LootTier _tier;
        private bool _wasPaused, _collecting, _skipped, _done;
        private float _t, _burstAt, _revealAt, _stagger, _fade = 1f;
        private readonly RandomNumberGenerator _rng = new();

        // UI
        private Control _root;
        private ColorRect _dim, _flash;
        private LootFx _back, _front;
        private SubViewportContainer _viewBox;
        private SubViewport _view;
        private Node3D _stage, _crate, _debris;
        private OmniLight3D _tierLight;
        private Label _title, _subtitle, _lineLabel, _prompt;
        private VBoxContainer _list;
        private readonly List<(PanelContainer panel, float at, string rarity)> _panels = new();
        private readonly List<(string name, string detail, string rarity)> _lines = new();
        private readonly List<(StandardMaterial3D mat, float energy)> _glows = new();
        private readonly List<(Node3D node, Vector3 vel, Vector3 spin)> _pieces = new();
        private readonly List<(MeshInstance3D ring, float at)> _locks = new();

        private static readonly Vector2 Canvas = new(1920, 1080);
        private static readonly Vector2 CrateCentre = new(960, 430);

        public override void _Ready()
        {
            Layer = 60;
            ProcessMode = ProcessModeEnum.Always;
            _rng.Randomize();
        }

        public void Play(List<Opening> openings)
        {
            foreach (var o in openings) _queue.Enqueue(o);
            _active = this;
            _wasPaused = GetTree().Paused;
            GetTree().Paused = true;
            HideBattleLayers();
            Next();
        }

        private void Next()
        {
            ClearStage();
            if (_queue.Count == 0)
            {
                _done = true;
                ClosedAtMs = Time.GetTicksMsec();
                _closedFrame = Engine.GetProcessFrames();
                RestoreLayers();
                GetTree().Paused = _wasPaused;
                Finished?.Invoke();
                QueueFree();
                return;
            }
            Current = _queue.Dequeue();
            _tier = Current.Tiers.OrderByDescending(t => t.Rank).First();
            _t = 0f;
            Burst = false;
            Revealed = 0;
            _skipped = false;
            _collecting = false;
            _fade = 1f;
            _crateYaw = 0f;
            _kick = 0f;
            (_burstAt, _revealAt, _stagger) = _tier.Sequence switch
            {
                "rays" => (2.2f, 2.55f, 0.32f),
                "shatter" => (2.4f, 2.95f, 0.4f),
                "ascend" => (3.4f, 3.9f, 0.5f),
                _ => (1.0f, 1.25f, 0.16f),
            };
            // The haul takes its time: the other crates pop first, the best one last
            if (Current.Mass) { _burstAt += 1.2f; _revealAt += 1.6f; _stagger = Mathf.Min(_stagger, 0.22f); }
            _party = 0f;
            MassPopped = 0;
            Build();
        }

        // ── Building the screen ──

        private void Build()
        {
            _root = new Control { Name = "Ceremony", MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            _dim = new ColorRect { Name = "Dim", Color = new Color(0, 0, 0, 0f), MouseFilter = Control.MouseFilterEnum.Stop };
            _dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _dim.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) Advance();
            };
            _root.AddChild(_dim);

            var add = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            _back = new LootFx { Name = "FxBack", Material = add, MouseFilter = Control.MouseFilterEnum.Ignore, Centre = CrateCentre, Tint = _tier.Tint, Manual = _manual };
            _back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(_back);

            // The crate in its own little 3D world
            bool mass = Current.Mass;
            var viewSize = mass ? new Vector2I(1200, 640) : new Vector2I(600, 600);
            _viewBox = new SubViewportContainer { Name = "CrateView", Stretch = true, MouseFilter = Control.MouseFilterEnum.Ignore,
                Size = viewSize, Position = CrateCentre - (Vector2)viewSize / 2f };
            _root.AddChild(_viewBox);
            _view = new SubViewport { Size = viewSize, TransparentBg = true, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            _viewBox.AddChild(_view);
            _stage = new Node3D { Name = "Stage" };
            _view.AddChild(_stage);
            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.55f, 0.58f, 0.66f),
                AmbientLightEnergy = 0.7f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            };
            _stage.AddChild(new WorldEnvironment { Environment = env });
            // A haul is seen from higher up, so the horseshoe of crates reads as one round the best
            var cam = new Camera3D { Fov = 36f, Position = mass ? new Vector3(0, 3.3f, 6.0f) : new Vector3(0, 1.35f, 3.4f), Current = true };
            _stage.AddChild(cam);
            cam.LookAtFromPosition(cam.Position, mass ? new Vector3(0, 0.3f, -0.6f) : new Vector3(0, 0.38f, 0), Vector3.Up);
            _cam = cam;
            _stage.AddChild(new DirectionalLight3D { LightEnergy = 1.3f, RotationDegrees = new Vector3(-48, 30, 0) });
            _tierLight = new OmniLight3D { LightColor = _tier.Tint, LightEnergy = 1.5f, OmniRange = 4f, Position = new Vector3(0, 0.6f, 1.1f) };
            _stage.AddChild(_tierLight);

            var sheet = TowerSheet.Load(_tier.Sheet);
            _crate = sheet != null ? TowerLook.Build(sheet, _tier.Tint, 25f) : new Node3D();
            _crate.Name = "Crate";
            _stage.AddChild(_crate);
            _glows.Clear();
            foreach (var node in _crate.FindChildren("*", "MeshInstance3D", true, false))
                if (node is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D m && m.EmissionEnabled)
                    _glows.Add((m, m.EmissionEnergyMultiplier));
            _debris = new Node3D { Name = "Debris" };
            _stage.AddChild(_debris);

            // The rest of the haul on an arc behind the best crate, popping one after another
            _satellites.Clear();
            if (mass)
            {
                var rest = Current.Tiers.ToList();
                rest.Remove(_tier);
                rest = rest.OrderBy(t => t.Rank).Take(12).ToList();
                int count = rest.Count;
                for (int i = 0; i < count; i++)
                {
                    var t = rest[i];
                    var sh = TowerSheet.Load(t.Sheet);
                    var c = sh != null ? TowerLook.Build(sh, t.Tint, 25f) : new Node3D();
                    c.Name = $"Haul{i}";
                    float a = count == 1 ? Mathf.Pi * 0.5f : Mathf.Lerp(Mathf.DegToRad(15f), Mathf.DegToRad(165f), (float)i / (count - 1));
                    // A horseshoe round the back of the stage, so the best crate sits in the middle of it
                    var home = new Vector3(Mathf.Cos(a) * 2.5f, 0f, -0.1f - Mathf.Sin(a) * 2.1f);
                    c.Position = home + Vector3.Up * 2.5f;
                    c.Scale = Vector3.One * 0.62f;
                    _stage.AddChild(c);
                    float popAt = Mathf.Lerp(_burstAt * 0.42f, _burstAt * 0.9f, count == 1 ? 0.5f : (float)i / (count - 1));
                    _satellites.Add((c, home, popAt, t));
                }
            }

            _front = new LootFx { Name = "FxFront", Material = add, MouseFilter = Control.MouseFilterEnum.Ignore, Centre = CrateCentre, Tint = _tier.Tint, Manual = _manual };
            _front.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(_front);

            _flash = new ColorRect { Name = "Flash", Color = new Color(1, 1, 1, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
            _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(_flash);

            int n = Current.Tiers.Count;
            _title = Text(mass ? $"SAFE ROOM: {n} CRATE{(n == 1 ? "" : "S")}" : n > 1 ? $"{n} CRATES" : _tier.Name.ToUpper(), mass ? 54 : 44, _tier.Tint.Lightened(0.2f), new Vector2(0, mass ? 52 : 70), 1920);
            _subtitle = Text(n > 1 ? string.Join(", ", Current.Tiers.GroupBy(t => t.Name).Select(g => g.Count() > 1 ? $"{g.Count()} {g.Key}s" : g.Key)) : TierBlurb(_tier),
                18, new Color(0.75f, 0.78f, 0.85f), new Vector2(0, 128), 1920);
            _lineLabel = Text(Current.Line ?? "", 20, new Color(0.62f, 0.95f, 0.85f), new Vector2(0, 920), 1920);
            _lineLabel.Modulate = new Color(1, 1, 1, 0);
            _prompt = Text("Click or Space to skip", 17, new Color(0.6f, 0.62f, 0.68f), new Vector2(0, 1000), 1920);

            BuildList();
            Animate(0f);
        }

        private static string TierBlurb(LootTier t) => t.Sequence switch
        {
            "rays" => "Something heavy is rattling in there",
            "shatter" => "Light is leaking out of the seams",
            "ascend" => "It belonged to an Ascendant",
            _ => "Scrap, mostly. Mostly.",
        };

        private Label Text(string text, int size, Color color, Vector2 pos, float width)
        {
            var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, Position = pos, Size = new Vector2(width, size * 1.6f),
                MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            l.AddThemeConstantOverride("outline_size", 6);
            l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
            _root.AddChild(l);
            return l;
        }

        /// <summary>The reveal: like rewards together (Resources added up, charges of one strike counted).</summary>
        private void BuildList()
        {
            _lines.Clear();
            _lineRelics.Clear();
            _equipButtons.Clear();
            foreach (var g in Current.Rewards.GroupBy(r => r.Kind + "|" + (r.StrikeId ?? r.RelicId ?? "")))
            {
                var first = g.First();
                _lineRelics.Add(first.Kind == "relic" ? first.RelicId : null);
                int sum = g.Sum(r => r.Amount), count = g.Count();
                string name = first.Kind switch
                {
                    "resources" => $"{sum} Resources",
                    "materials" => $"{sum} Materials",
                    "xp" => $"{sum} BIT XP",
                    "perk_point" => sum == 1 ? "Perk point" : $"{sum} perk points",
                    _ => count > 1 ? $"{count}x {first.Name}" : first.Name,
                };
                _lines.Add((name, first.Detail, g.Max(r => r.RarityRank) is var rank ? g.First(r => r.RarityRank == rank).Rarity : first.Rarity));
            }
            // Best last, keeping each line's relic beside it
            var order = Enumerable.Range(0, _lines.Count).OrderBy(i => Rank(_lines[i].rarity)).ThenBy(i => i).ToList();
            var sortedLines = order.Select(i => _lines[i]).ToList();
            var sortedRelics = order.Select(i => _lineRelics[i]).ToList();
            _lines.Clear(); _lines.AddRange(sortedLines);
            _lineRelics.Clear(); _lineRelics.AddRange(sortedRelics);

            _list = new VBoxContainer { Name = "Rewards", MouseFilter = Control.MouseFilterEnum.Ignore };
            _list.AddThemeConstantOverride("separation", 8);
            float h = _lines.Count * 66f;
            _list.Position = new Vector2(960 - 320, Mathf.Clamp(560 - h / 2f, 220, 860 - h));
            _list.Size = new Vector2(640, h);
            _root.AddChild(_list);
            _panels.Clear();
            for (int i = 0; i < _lines.Count; i++)
            {
                var (name, detail, rarity) = _lines[i];
                var col = LootReward.RarityColor(rarity);
                var panel = new PanelContainer { Name = $"Reward{i}", CustomMinimumSize = new Vector2(640, 58), Modulate = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
                var st = new StyleBoxFlat { BgColor = new Color(0.05f, 0.06f, 0.1f, 0.94f), BorderColor = col, ContentMarginLeft = 18, ContentMarginRight = 14, ContentMarginTop = 6, ContentMarginBottom = 6 };
                st.BorderWidthLeft = 5;
                st.SetCornerRadiusAll(3);
                panel.AddThemeStyleboxOverride("panel", st);
                var v = new VBoxContainer();
                v.AddThemeConstantOverride("separation", 0);
                panel.AddChild(v);
                var row = new HBoxContainer();
                var nl = new Label { Text = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
                nl.AddThemeFontSizeOverride("font_size", 21);
                nl.AddThemeColorOverride("font_color", col);
                row.AddChild(nl);
                // A relic shows its own rarity (the Relics screen's word), not the reveal's step up
                var rl = new Label { Text = _lineRelics[i] is { } rid && RelicRegistry.Get(rid) is { } own ? $"{own.Rarity} relic".ToUpper() : rarity.ToUpper() };
                rl.AddThemeFontSizeOverride("font_size", 14);
                rl.AddThemeColorOverride("font_color", col.Darkened(0.25f));
                row.AddChild(rl);
                // A relic can go on right now: it takes its kind's slot
                if (_lineRelics[i] is { } relicId && RelicRegistry.Get(relicId) is { } relic)
                {
                    string inSlot = RelicInventory.EquippedOfKind(relic.Kind);
                    bool on = inSlot == relicId;
                    var eq = new Button
                    {
                        Name = "Equip",
                        Text = on ? "EQUIPPED" : inSlot != null ? $"SWAP IN  [E]" : "EQUIP  [E]",
                        Disabled = on,
                        FocusMode = Control.FocusModeEnum.None,
                        CustomMinimumSize = new Vector2(150, 30),
                        TooltipText = $"Goes in {RelicRegistry.TheSlot(relic.Kind)}" + (inSlot != null && !on ? $", replacing {RelicRegistry.Get(inSlot)?.Name}" : ""),
                    };
                    eq.AddThemeFontSizeOverride("font_size", 14);
                    var es = new StyleBoxFlat { BgColor = new Color(col.R * 0.25f, col.G * 0.25f, col.B * 0.25f, 0.95f), BorderColor = col };
                    es.SetBorderWidthAll(1);
                    es.SetCornerRadiusAll(3);
                    eq.AddThemeStyleboxOverride("normal", es);
                    var eh = (StyleBoxFlat)es.Duplicate();
                    eh.BgColor = new Color(col.R * 0.42f, col.G * 0.42f, col.B * 0.42f, 1f);
                    eq.AddThemeStyleboxOverride("hover", eh);
                    eq.AddThemeStyleboxOverride("pressed", eh);
                    eq.AddThemeStyleboxOverride("disabled", es);
                    eq.AddThemeColorOverride("font_color", col.Lightened(0.4f));
                    eq.AddThemeColorOverride("font_disabled_color", col);
                    eq.Pressed += () => Equip(relicId);
                    row.AddChild(eq);
                    _equipButtons.Add((eq, relicId));
                }
                v.AddChild(row);
                var dl = new Label { Text = detail ?? "", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
                dl.AddThemeFontSizeOverride("font_size", 15);
                dl.AddThemeColorOverride("font_color", new Color(0.66f, 0.7f, 0.78f));
                v.AddChild(dl);
                _list.AddChild(panel);
                float extra = Rank(rarity) >= 3 && _tier.Sequence != "pop" ? 0.25f : 0f;
                float at = _revealAt + i * _stagger + extra * i;
                _panels.Add((panel, at, rarity));
            }
        }

        private readonly List<string> _lineRelics = new();
        private readonly List<(Button btn, string relic)> _equipButtons = new();
        /// <summary>Relics equipped from this ceremony (tests).</summary>
        public int Equipped { get; private set; }

        /// <summary>Equip a relic shown in the reveal (its button, or E for the first one).</summary>
        public bool Equip(string relicId)
        {
            if (relicId == null || EquipRequested == null) return false;
            if (!EquipRequested(relicId)) return false;
            Equipped++;
            foreach (var (btn, id) in _equipButtons)
            {
                if (!IsInstanceValid(btn)) continue;
                bool on = id == relicId;
                // Another relic of the same kind in this reveal now swaps out this one
                if (on) { btn.Text = "EQUIPPED"; btn.Disabled = true; }
                else if (RelicRegistry.Get(id)?.Kind == RelicRegistry.Get(relicId)?.Kind) { btn.Text = "SWAP IN"; btn.Disabled = false; }
            }
            var c = RelicRegistry.Get(relicId)?.Tint ?? Colors.White;
            _flash.Color = new Color(c.R, c.G, c.B, 0.3f);
            _front.Sparks(new Vector2(960, 540), 30, c, 600f);
            return true;
        }

        /// <summary>The first relic in the reveal that isn't on yet (E).</summary>
        public bool EquipFirst()
        {
            if (!AllRevealed) return false;
            var next = _equipButtons.FirstOrDefault(b => IsInstanceValid(b.btn) && !b.btn.Disabled);
            return next.relic != null && Equip(next.relic);
        }

        /// <summary>Relics in this reveal that can be equipped (tests).</summary>
        public int EquipOffers => _equipButtons.Count;

        private static int Rank(string r) => r switch { "uncommon" => 1, "rare" => 2, "epic" => 3, "legendary" => 4, _ => 0 };

        private void ClearStage()
        {
            // Out of the tree at once (the next opening builds its own in the same frame)
            if (_root != null && IsInstanceValid(_root)) { if (_root.GetParent() == this) RemoveChild(_root); _root.QueueFree(); }
            _root = null;
            _panels.Clear();
            _pieces.Clear();
            _locks.Clear();
            _glows.Clear();
            _satellites.Clear();
            _cam = null;
        }

        // ── Running ──

        /// <summary>Tests: the clock only moves through <see cref="Step"/> (the light effects too), so a moment can be held for a screenshot.</summary>
        internal bool Manual
        {
            get => _manual;
            set { _manual = value; if (_back != null) _back.Manual = value; if (_front != null) _front.Manual = value; }
        }
        private bool _manual;

        public override void _Process(double delta)
        {
            if (_manual) return;
            float dt = (float)delta / (float)Mathf.Max(0.05, Engine.TimeScale);
            Tick(Mathf.Min(dt, 0.1f));
        }

        /// <summary>Step the sequence by hand (with <see cref="Manual"/> set).</summary>
        internal void Step(float dt) => Tick(dt);

        private void Tick(float dt)
        {
            if (_root == null) return;
            if (_manual) { _back.Advance(dt); _front.Advance(dt); }
            if (_collecting)
            {
                _fade -= dt / 0.3f;
                _root.Modulate = new Color(1, 1, 1, Mathf.Max(0f, _fade));
                if (_fade <= 0f) Next();
                return;
            }
            _t += dt;
            Animate(dt);
        }

        private void Animate(float dt)
        {
            string seq = _tier.Sequence;
            float t = _t;
            float dimTo = seq switch { "ascend" => 0.94f, "shatter" => 0.86f, "rays" => 0.82f, _ => 0.74f };
            _dim.Color = new Color(0.01f, 0.01f, 0.03f, dimTo * Mathf.Clamp(t / 0.35f, 0f, 1f));
            _flash.Color = new Color(1, 1, 1, Mathf.Max(0f, _flash.Color.A - dt * 2.4f));
            if (_satellites.Count > 0) Haul(t);
            if (!Burst)
            {
                // A haul: the best crate's own sequence starts once the others are popping
                float own = Current.Mass ? Mathf.Max(0f, t - 1.2f) : t;
                switch (seq)
                {
                    case "rays": Rays(own); break;
                    case "shatter": Shatter(own, dt); break;
                    case "ascend": Ascend(own, dt); break;
                    default: Pop(own); break;
                }
                if (t >= _burstAt) DoBurst();
            }
            else AfterBurst(dt);

            // The line comes in a little before the burst (at the start for the big ones)
            float lineAt = seq is "ascend" or "shatter" ? 0.6f : _burstAt * 0.5f;
            _lineLabel.Modulate = new Color(1, 1, 1, Mathf.Clamp((t - lineAt) / 0.4f, 0f, 1f));

            // Rewards slide in from the right, one at a time
            int shown = 0;
            for (int i = 0; i < _panels.Count; i++)
            {
                var (panel, at, rarity) = _panels[i];
                float k = _skipped ? 1f : Mathf.Clamp((t - at) / 0.32f, 0f, 1f);
                if (k > 0f) shown++;
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                float back = k < 1f ? Mathf.Sin(k * Mathf.Pi) * 0.08f : 0f;
                panel.Position = new Vector2((1f - ease) * 520f - back * 80f, panel.Position.Y);
                panel.Modulate = new Color(1, 1, 1, Mathf.Clamp(k * 1.6f, 0f, 1f));
                int r = Rank(rarity);
                if (r >= 3 && k >= 1f)
                {
                    float since = _skipped ? 1f : t - at - 0.32f;
                    if (since >= 0f && since < 0.5f)
                    {
                        if (panel.GetThemeStylebox("panel") is StyleBoxFlat st) st.BorderWidthLeft = 5 + Mathf.RoundToInt(6f * Mathf.Sin(since / 0.5f * Mathf.Pi));
                        if (r >= 4)
                        {
                            panel.PivotOffset = panel.Size / 2f;
                            panel.Scale = Vector2.One * (1f + 0.07f * Mathf.Sin(since / 0.5f * Mathf.Pi));
                        }
                        if (since < dt + 0.001f && !_skipped) _front.Sparks(new Vector2(960 + 200, panel.GlobalPosition.Y + 29), 18, LootReward.RarityColor(rarity), 260f);
                    }
                    else panel.Scale = Vector2.One;
                }
            }
            Revealed = shown;
            bool canEquip = _equipButtons.Any(b => IsInstanceValid(b.btn) && !b.btn.Disabled);
            _prompt.Text = AllRevealed ? (canEquip ? "COLLECT   [Space]        EQUIP RELIC   [E]" : "COLLECT   [Space]") : "Click or Space to skip";
            _prompt.AddThemeColorOverride("font_color", AllRevealed ? new Color(1f, 0.85f, 0.35f) : new Color(0.6f, 0.62f, 0.68f));
            _back.QueueRedraw();
            _front.QueueRedraw();
        }

        /// <summary>The haul's other crates drop in, bob, and pop one after another (all of them by the big burst).</summary>
        private void Haul(float t)
        {
            for (int i = 0; i < _satellites.Count; i++)
            {
                var (node, home, popAt, tier) = _satellites[i];
                if (!IsInstanceValid(node) || !node.Visible) continue;
                float drop = Mathf.Clamp((t - i * 0.06f) / 0.45f, 0f, 1f);
                float ease = 1f - (1f - drop) * (1f - drop);
                float shake = t > popAt - 0.35f ? 0.03f : 0f;
                node.Position = home + Vector3.Up * ((1f - ease) * 2.5f + Mathf.Sin(t * 3f + i) * 0.04f)
                    + new Vector3(_rng.RandfRange(-shake, shake), 0, _rng.RandfRange(-shake, shake));
                node.Rotation = new Vector3(0, Mathf.DegToRad(25f) + t * (0.8f + i * 0.07f), 0);
                if (t >= popAt || Burst)
                {
                    node.Visible = false;
                    MassPopped++;
                    var at = _cam != null ? _viewBox.Position + _cam.UnprojectPosition(node.GlobalPosition + Vector3.Up * 0.3f) : CrateCentre;
                    _front.Sparks(at, 18 + tier.Rank * 8, tier.Tint, 380f + tier.Rank * 80f);
                    _front.Ring(at, tier.Tint, 120f + tier.Rank * 40f, 0.4f, 4f);
                    _flash.Color = new Color(tier.Tint.R, tier.Tint.G, tier.Tint.B, Mathf.Max(_flash.Color.A, 0.12f + tier.Rank * 0.04f));
                    _kick = Mathf.Max(_kick, 2f + tier.Rank);
                }
            }
        }

        /// <summary>After a haul's big burst: fireworks across the screen for a few seconds.</summary>
        private void Party(float dt)
        {
            if (!Current.Mass || _party > 3.6f) return;
            float before = _party;
            _party += dt;
            // A burst every 0.12 s, in the haul's colours and white, thinning out at the end
            float every = _party < 2.4f ? 0.12f : 0.24f;
            int was = (int)(before / every), now = (int)(_party / every);
            var tints = Current.Tiers.Select(x => x.Tint).Distinct().ToList();
            for (int k = was; k < now; k++)
            {
                var c = _rng.Randf() < 0.25f ? Colors.White : tints[_rng.RandiRange(0, tints.Count - 1)];
                var at = new Vector2(_rng.RandfRange(220f, 1700f), _rng.RandfRange(120f, 560f));
                _back.Sparks(at, 24, c.Darkened(0.2f), _rng.RandfRange(200f, 320f));
                _front.Sparks(at, 56, c, _rng.RandfRange(420f, 720f));
                _front.Ring(at, c, _rng.RandfRange(120f, 260f), 0.55f, 3.5f);
            }
        }

        private void SetGlow(float mult)
        {
            foreach (var (m, e) in _glows) m.EmissionEnergyMultiplier = e * mult;
            _tierLight.LightEnergy = 1.5f * Mathf.Max(1f, mult * 0.8f);
        }

        // pop: drops in, hops twice, pops
        private void Pop(float t)
        {
            float k = Mathf.Clamp(t / 0.3f, 0f, 1f);
            float y = (1f - k) * 2.2f;
            float hop = 0f, squash = 1f;
            if (t > 0.3f)
            {
                float h = (t - 0.3f) / 0.35f;
                int which = (int)h;
                float f = h - which;
                if (which < 2) { hop = Mathf.Sin(f * Mathf.Pi) * (0.22f + which * 0.1f); squash = 1f + Mathf.Sin(f * Mathf.Tau) * 0.08f; }
            }
            _crate.Position = new Vector3(0, y + hop, 0);
            _crate.Scale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
            _crate.Rotation = new Vector3(0, Mathf.DegToRad(25f) + t * 0.6f, Mathf.Sin(t * 30f) * 0.03f * Mathf.Clamp(t - 0.3f, 0f, 1f));
            SetGlow(1f + t * 1.5f);
        }

        // rays: rises, light fans out behind while it shakes harder, then bangs
        private void Rays(float t)
        {
            float k = Mathf.Clamp(t / 0.5f, 0f, 1f);
            float build = Mathf.Clamp((t - 0.4f) / 1.8f, 0f, 1f);
            float shake = build * build * 0.07f;
            _crate.Position = new Vector3(_rng.RandfRange(-shake, shake), (k - 1f) * 1.6f + _rng.RandfRange(-shake, shake), 0);
            _crate.Rotation = new Vector3(_rng.RandfRange(-shake, shake) * 2f, Mathf.DegToRad(25f) + (1f - k) * 6f + t * 0.4f, _rng.RandfRange(-shake, shake) * 2f);
            _back.RaysAlpha = build * 0.55f;
            _back.RaysLength = 260f + build * 340f;
            _back.RaysTurn = t * 0.35f;
            _back.RaysCount = 14;
            SetGlow(1f + build * 4f);
        }

        // shatter: floats in and slows, cracks of light spread, breaks into pieces
        private void Shatter(float t, float dt)
        {
            float k = Mathf.Clamp(t / 0.6f, 0f, 1f);
            float build = Mathf.Clamp((t - 0.5f) / 1.9f, 0f, 1f);
            float spin = Mathf.Lerp(2.2f, 0f, Mathf.Clamp(t / 2.3f, 0f, 1f));
            _crateYaw += spin * dt;
            _crate.Position = new Vector3(0, (1f - k) * -0.8f + Mathf.Sin(t * 2f) * 0.05f, (1f - k) * -1.5f);
            _crate.Rotation = new Vector3(0, Mathf.DegToRad(25f) + _crateYaw, 0);
            float shake = build > 0.7f ? (build - 0.7f) * 0.08f : 0f;
            _crate.Position += new Vector3(_rng.RandfRange(-shake, shake), _rng.RandfRange(-shake, shake), 0);
            _front.CrackProgress = build;
            _front.CrackAlpha = Mathf.Clamp(build * 1.4f, 0f, 1f);
            _back.GlowRadius = 140f + build * 160f;
            _back.GlowAlpha = build * 0.35f;
            SetGlow(1f + build * 6f);
        }
        private float _crateYaw;

        // ascend: dark, a beam comes down, the cache descends, rings lock on, implosion
        private void Ascend(float t, float dt)
        {
            _back.BeamAlpha = Mathf.Clamp(t / 0.6f, 0f, 1f) * 0.8f;
            _back.BeamWidth = 90f + Mathf.Sin(t * 3f) * 6f;
            float down = Mathf.Clamp((t - 0.6f) / 1.0f, 0f, 1f);
            float ease = 1f - (1f - down) * (1f - down);
            float implode = Mathf.Clamp((t - 2.8f) / 0.6f, 0f, 1f);
            _crate.Position = new Vector3(0, (1f - ease) * 3.2f, 0);
            _crateYaw += (0.5f + implode * implode * 14f) * dt;
            _crate.Rotation = new Vector3(0, Mathf.DegToRad(25f) + _crateYaw, 0);
            _crate.Scale = Vector3.One * Mathf.Max(0.04f, 1f - implode * implode * 0.96f);
            _crate.Visible = t > 0.55f;
            // Three rings lock on, one after another
            float[] at = { 1.6f, 2.0f, 2.4f };
            for (int i = 0; i < at.Length; i++)
            {
                if (t >= at[i] && _locks.Count == i) AddLock(i);
            }
            for (int i = 0; i < _locks.Count; i++)
            {
                var (ring, since) = _locks[i];
                float k = Mathf.Clamp((t - since) / 0.25f, 0f, 1f);
                float s = Mathf.Lerp(2.4f, 1f, 1f - (1f - k) * (1f - k)) * Mathf.Max(0.05f, 1f - implode);
                ring.Scale = Vector3.One * s;
                ring.RotateY(dt * (1.5f + i));
            }
            if (implode > 0f) _front.Converge(implode);
            SetGlow(1f + down * 2f + implode * 6f);
            // A little tremor while it locks
            float tremor = t > 1.6f && t < 3.4f ? 3f : 0f;
            _root.Position = new Vector2(_rng.RandfRange(-tremor, tremor), _rng.RandfRange(-tremor, tremor));
        }

        private void AddLock(int i)
        {
            var mat = new StandardMaterial3D { AlbedoColor = _tier.Tint, EmissionEnabled = true, Emission = _tier.Tint, EmissionEnergyMultiplier = 3f };
            var ring = new MeshInstance3D
            {
                Name = $"Lock{i}",
                Mesh = new TorusMesh { InnerRadius = 0.92f + i * 0.12f, OuterRadius = 0.98f + i * 0.12f, Rings = 32, RingSegments = 6 },
                MaterialOverride = mat,
                Position = new Vector3(0, 0.35f, 0),
                RotationDegrees = new Vector3(i == 1 ? 70 : i == 2 ? -25 : 10, i * 40, i == 2 ? 60 : 0),
            };
            _stage.AddChild(ring);
            _locks.Add((ring, _t));
            _flash.Color = new Color(_tier.Tint.R, _tier.Tint.G, _tier.Tint.B, 0.25f);
            _front.Sparks(CrateCentre, 14, _tier.Tint, 360f);
        }

        // ── The burst ──

        private void DoBurst()
        {
            if (Burst) return;
            Burst = true;
            string seq = _tier.Sequence;
            var c = _tier.Tint;
            _back.RaysAlpha = 0f;
            _front.CrackAlpha = 0f;
            _root.Position = Vector2.Zero;
            switch (seq)
            {
                case "rays":
                    _flash.Color = new Color(1f, 0.95f, 0.8f, 0.55f);
                    _front.Ring(CrateCentre, c, 900f, 0.7f, 10f);
                    _front.Sparks(CrateCentre, 46, c, 700f);
                    _back.RayBurst = 1f;
                    _crate.Visible = false;
                    break;
                case "shatter":
                    _flash.Color = new Color(c.R, c.G, c.B, 0.55f);
                    _front.Shards(CrateCentre, 36, c);
                    BreakCrate();
                    break;
                case "ascend":
                    _flash.Color = new Color(1, 1, 1, 0.95f);
                    _front.Ring(CrateCentre, c, 1400f, 1.1f, 16f);
                    _front.Ring(CrateCentre, Colors.White, 900f, 0.8f, 6f);
                    _front.Sparks(CrateCentre, 70, c, 900f);
                    _back.BeamBurst = 1f;
                    _crate.Visible = false;
                    foreach (var (ring, _) in _locks) ring.Visible = false;
                    break;
                default:
                    _flash.Color = new Color(1, 1, 1, 0.35f);
                    _front.Sparks(CrateCentre, 26 + Current.Tiers.Count * 4, c, 520f);
                    _front.Ring(CrateCentre, c, 420f, 0.45f, 6f);
                    _crate.Visible = false;
                    break;
            }
            // The screen kicks (the battle camera is paused under us: shaking it would play out afterwards)
            _kick = seq switch { "ascend" => 16f, "shatter" => 11f, "rays" => 9f, _ => 4f };
            if (Current.Mass)
            {
                _kick += 6f;
                _flash.Color = new Color(1f, 0.95f, 0.85f, 0.75f);
                _front.Ring(CrateCentre, Colors.White, 1500f, 1.0f, 12f);
                _front.Sparks(CrateCentre, 90, c.Lerp(Colors.White, 0.3f), 1000f);
            }
        }
        private float _kick;

        /// <summary>Prism: the crate's parts fly apart, with chips of its colour.</summary>
        private void BreakCrate()
        {
            var meshes = _crate.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
            foreach (var mi in meshes)
            {
                var gt = mi.GlobalTransform;
                mi.GetParent().RemoveChild(mi);
                _debris.AddChild(mi);
                mi.GlobalTransform = gt;
                var dir = (gt.Origin - new Vector3(0, 0.3f, 0)).Normalized();
                if (dir.LengthSquared() < 0.01f) dir = Vector3.Up;
                _pieces.Add((mi, dir * _rng.RandfRange(2.5f, 4.5f) + new Vector3(0, 1.5f, 1.8f),
                    new Vector3(_rng.RandfRange(-6, 6), _rng.RandfRange(-6, 6), _rng.RandfRange(-6, 6))));
            }
            var chip = new StandardMaterial3D { AlbedoColor = _tier.Tint, EmissionEnabled = true, Emission = _tier.Tint, EmissionEnergyMultiplier = 2.5f, Metallic = 0.6f, Roughness = 0.2f };
            for (int i = 0; i < 16; i++)
            {
                var piece = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.03f, 0.09f) * _rng.RandfRange(0.7f, 1.6f) }, MaterialOverride = chip,
                    Position = new Vector3(_rng.RandfRange(-0.4f, 0.4f), _rng.RandfRange(0.1f, 0.6f), _rng.RandfRange(-0.3f, 0.3f)) };
                _debris.AddChild(piece);
                var dir = new Vector3(_rng.RandfRange(-1, 1), _rng.RandfRange(-0.2f, 1), _rng.RandfRange(0.2f, 1)).Normalized();
                _pieces.Add((piece, dir * _rng.RandfRange(2.5f, 5f), new Vector3(_rng.RandfRange(-9, 9), _rng.RandfRange(-9, 9), _rng.RandfRange(-9, 9))));
            }
            _crate.Visible = false;
        }

        private void AfterBurst(float dt)
        {
            Party(dt);
            for (int i = 0; i < _pieces.Count; i++)
            {
                var (n, v, s) = _pieces[i];
                if (!IsInstanceValid(n)) continue;
                v += Vector3.Down * 6f * dt;
                n.Position += v * dt;
                n.Rotation += s * dt;
                n.Scale = n.Scale * Mathf.Max(0f, 1f - dt * 0.9f);
                _pieces[i] = (n, v, s);
            }
            _kick = Mathf.Max(0f, _kick - dt * 45f);
            _root.Position = _kick > 0f ? new Vector2(_rng.RandfRange(-_kick, _kick), _rng.RandfRange(-_kick, _kick)) : Vector2.Zero;
            _back.RayBurst = Mathf.Max(0f, _back.RayBurst - dt * 1.8f);
            _back.BeamBurst = Mathf.Max(0f, _back.BeamBurst - dt * 1.2f);
            _back.BeamAlpha = Mathf.Max(0f, _back.BeamAlpha - dt * 1.2f);
            _back.GlowAlpha = Mathf.Max(0f, _back.GlowAlpha - dt * 0.8f);
            if (_tierLight != null) _tierLight.LightEnergy = Mathf.Max(0.5f, _tierLight.LightEnergy - dt * 3f);
        }

        // ── Input ──

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_root == null) return;
            if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.E)
            {
                // E equips the relic on show (and does nothing else, so it never collects by accident)
                EquipFirst();
                GetViewport().SetInputAsHandled();
            }
            else if (@event is InputEventKey k2 && k2.Pressed && !k2.Echo && k2.Keycode is Key.Space or Key.Enter or Key.KpEnter or Key.Escape or Key.O)
            {
                if (k2.Keycode == Key.Space) NoteSkipPress();
                Advance();
                GetViewport().SetInputAsHandled();
            }
        }

        /// <summary>Skip to the contents, or collect them once they're all in.</summary>
        public void Advance()
        {
            if (_root == null || _collecting) return;
            if (!AllRevealed) Skip();
            else Collect();
        }

        public void Skip()
        {
            if (!Burst) DoBurst();
            _skipped = true;
            if (_t < _revealAt) _t = _revealAt;
            Animate(0f);
        }

        public void Collect()
        {
            if (_collecting || _root == null) return;
            if (!AllRevealed) Skip();
            _collecting = true;
            Collected?.Invoke(Current);
        }

        /// <summary>
        /// The run ended with the ceremony up: hand back every opening not yet collected (the
        /// one on screen too) and close without a sequence.
        /// </summary>
        public List<Opening> TakeRest()
        {
            var rest = new List<Opening>();
            if (Current != null && !_collecting && !_done) rest.Add(Current);
            rest.AddRange(_queue);
            _queue.Clear();
            _collecting = true;
            if (!_done && IsInsideTree()) GetTree().Paused = _wasPaused;
            _done = true;
            RestoreLayers();
            QueueFree();
            return rest;
        }

        public override void _ExitTree()
        {
            // Never leave the battle paused behind us
            if (!_done && IsInsideTree()) GetTree().Paused = _wasPaused;
            _done = true;
            RestoreLayers();
        }

        // ── The battle's own screens step aside (HUD, commentary, celebrations) ──

        private readonly List<CanvasLayer> _hidden = new();

        private void HideBattleLayers()
        {
            var scene = GetTree().CurrentScene;
            if (scene != null)
                foreach (var n in scene.FindChildren("*", "CanvasLayer", true, false))
                    if (n is CanvasLayer cl && cl != this && cl.Visible && cl.Layer < Layer) { cl.Visible = false; _hidden.Add(cl); }
            // A level-up or relic card waits for the ceremony to end
            Celebration.Held = true;
        }

        private void RestoreLayers()
        {
            foreach (var cl in _hidden) if (IsInstanceValid(cl)) cl.Visible = true;
            _hidden.Clear();
            Celebration.Held = false;
        }

        /// <summary>Tests: the screens hidden while it's up.</summary>
        internal int HiddenLayers => _hidden.Count;
    }

    /// <summary>
    /// The sequences' 2D light (drawn additively): rays fanning behind the crate, a beam, a soft
    /// glow, rings, sparks, shards, cracks spreading over it and sparks drawn inward.
    /// </summary>
    public partial class LootFx : Control
    {
        public Vector2 Centre;
        public Color Tint = Colors.White;
        public float RaysAlpha, RaysLength = 300f, RaysTurn, RayBurst;
        public int RaysCount = 12;
        public float BeamAlpha, BeamWidth = 90f, BeamBurst;
        public float GlowAlpha, GlowRadius = 160f;
        public float CrackProgress, CrackAlpha;
        private readonly List<(Vector2 p, Vector2 v, float life, float max, float size, Color c, bool shard, float rot)> _bits = new();
        private readonly List<(Vector2 c, Color col, float max, float life, float dur, float width)> _rings = new();
        private List<Vector2[]> _cracks;
        private readonly RandomNumberGenerator _rng = new();
        private float _converge, _clock;
        public int Particles => _bits.Count;

        public LootFx() { _rng.Randomize(); }

        public void Sparks(Vector2 at, int n, Color c, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                float a = _rng.Randf() * Mathf.Tau, s = _rng.RandfRange(0.3f, 1f) * speed;
                _bits.Add((at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, 0f, _rng.RandfRange(0.5f, 1.1f), _rng.RandfRange(2f, 5f), c.Lightened(_rng.RandfRange(0f, 0.5f)), false, 0f));
            }
        }

        public void Shards(Vector2 at, int n, Color c)
        {
            for (int i = 0; i < n; i++)
            {
                float a = _rng.Randf() * Mathf.Tau, s = _rng.RandfRange(250f, 800f);
                _bits.Add((at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, 0f, _rng.RandfRange(0.7f, 1.3f), _rng.RandfRange(10f, 26f), c.Lightened(_rng.RandfRange(0.1f, 0.6f)), true, _rng.Randf() * Mathf.Tau));
            }
        }

        public void Ring(Vector2 at, Color c, float radius, float dur, float width) => _rings.Add((at, c, radius, 0f, dur, width));

        /// <summary>Ascend: sparks drawn into the crate as it implodes.</summary>
        public void Converge(float k)
        {
            _converge = k;
            if (_rng.Randf() < 0.8f)
            {
                float a = _rng.Randf() * Mathf.Tau, r = _rng.RandfRange(300f, 520f);
                var from = Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                _bits.Add((from, (Centre - from) * 2.2f, 0f, 0.45f, _rng.RandfRange(2f, 4f), Tint.Lightened(0.4f), false, 0f));
            }
        }

        /// <summary>Moved only by the ceremony's <see cref="LootCeremony.Step"/> (tests).</summary>
        public bool Manual;

        public override void _Process(double delta)
        {
            if (!Manual) Advance(Mathf.Min((float)delta / (float)Mathf.Max(0.05, Engine.TimeScale), 0.1f));
        }

        public void Advance(float dt)
        {
            _clock += dt;
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var b = _bits[i];
                b.life += dt;
                if (b.life >= b.max) { _bits.RemoveAt(i); continue; }
                b.p += b.v * dt;
                b.v *= 1f - dt * (b.shard ? 0.6f : 1.8f);
                if (b.shard) b.v.Y += 500f * dt;
                b.rot += dt * 5f;
                _bits[i] = b;
            }
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var r = _rings[i];
                r.life += dt;
                if (r.life >= r.dur) { _rings.RemoveAt(i); continue; }
                _rings[i] = r;
            }
            if (_bits.Count > 0 || _rings.Count > 0 || RaysAlpha > 0f || BeamAlpha > 0f || CrackAlpha > 0f || GlowAlpha > 0f || RayBurst > 0f || BeamBurst > 0f) QueueRedraw();
        }

        public override void _Draw()
        {
            // Soft glow
            if (GlowAlpha > 0.001f)
                for (int i = 6; i >= 1; i--)
                    DrawCircle(Centre, GlowRadius * i / 6f, new Color(Tint.R, Tint.G, Tint.B, GlowAlpha * 0.12f));
            // The beam (ascend)
            if (BeamAlpha > 0.001f || BeamBurst > 0.001f)
            {
                float w = BeamWidth * (1f + BeamBurst * 5f);
                float a = Mathf.Max(BeamAlpha, BeamBurst);
                for (int i = 0; i < 5; i++)
                {
                    float ww = w * (1f - i * 0.18f);
                    DrawRect(new Rect2(Centre.X - ww / 2f, 0, ww, Centre.Y + 120f), new Color(Tint.R, Tint.G, Tint.B, a * 0.14f));
                }
                DrawRect(new Rect2(Centre.X - w * 0.08f, 0, w * 0.16f, Centre.Y + 120f), new Color(1, 1, 1, a * 0.5f));
            }
            // Rays (rays)
            float rayA = Mathf.Max(RaysAlpha, RayBurst);
            if (rayA > 0.001f)
            {
                float len = RaysLength * (1f + RayBurst * 1.6f);
                for (int i = 0; i < RaysCount; i++)
                {
                    float a0 = RaysTurn + i * Mathf.Tau / RaysCount;
                    float half = Mathf.Tau / RaysCount * 0.18f;
                    var p1 = Centre + new Vector2(Mathf.Cos(a0 - half), Mathf.Sin(a0 - half)) * len;
                    var p2 = Centre + new Vector2(Mathf.Cos(a0 + half), Mathf.Sin(a0 + half)) * len;
                    DrawColoredPolygon(new[] { Centre, p1, p2 }, new Color(Tint.R, Tint.G, Tint.B, rayA * 0.5f));
                }
            }
            // Cracks (shatter)
            if (CrackAlpha > 0.001f)
            {
                _cracks ??= MakeCracks();
                foreach (var line in _cracks)
                {
                    int n = Mathf.Clamp(Mathf.CeilToInt(line.Length * CrackProgress), 0, line.Length);
                    if (n < 2) continue;
                    var pts = line.Take(n).ToArray();
                    DrawPolyline(pts, new Color(Tint.R, Tint.G, Tint.B, CrackAlpha * 0.6f), 7f);
                    DrawPolyline(pts, new Color(1, 1, 1, CrackAlpha), 2.5f);
                }
            }
            // Rings
            foreach (var (c, col, max, life, dur, width) in _rings)
            {
                float k = life / dur;
                float r = max * (1f - (1f - k) * (1f - k));
                DrawArc(c, r, 0, Mathf.Tau, 96, new Color(col.R, col.G, col.B, (1f - k) * 0.9f), width * (1f - k * 0.6f));
            }
            // Sparks and shards
            foreach (var b in _bits)
            {
                float k = 1f - b.life / b.max;
                var col = new Color(b.c.R, b.c.G, b.c.B, k);
                if (b.shard)
                {
                    var d = new Vector2(Mathf.Cos(b.rot), Mathf.Sin(b.rot)) * b.size;
                    var e = new Vector2(-d.Y, d.X) * 0.35f;
                    DrawColoredPolygon(new[] { b.p + d, b.p + e, b.p - d * 0.6f }, col);
                }
                else
                {
                    DrawLine(b.p, b.p - b.v * 0.03f, col, b.size);
                    DrawCircle(b.p, b.size * 0.8f, col);
                }
            }
        }

        private List<Vector2[]> MakeCracks()
        {
            var list = new List<Vector2[]>();
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.Tau / 9f + _rng.RandfRange(-0.25f, 0.25f);
                var pts = new List<Vector2> { Centre + new Vector2(_rng.RandfRange(-12, 12), _rng.RandfRange(-12, 12)) };
                var p = pts[0];
                for (int s = 0; s < 8; s++)
                {
                    a += _rng.RandfRange(-0.5f, 0.5f);
                    p += new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * _rng.RandfRange(18f, 34f);
                    pts.Add(p);
                }
                list.Add(pts.ToArray());
            }
            return list;
        }
    }
}
