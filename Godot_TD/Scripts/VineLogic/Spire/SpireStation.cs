using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>Tuning for what BIT can do at the Spire (Data/spire_station.json).</summary>
    public class SpireStationData
    {
        public float Reach { get; set; } = 4.5f;
        public StationRepair Repair { get; set; } = new();
        public StationBank Bank { get; set; } = new();
        public StationDock Dock { get; set; } = new();
        public StationMaterialsMode MaterialsMode { get; set; } = new();
        public List<SpireUpgrade> Upgrades { get; set; } = new();
        public List<SpireStrike> Strikes { get; set; } = new();

        private static SpireStationData _cached;
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static SpireStationData Load()
        {
            if (_cached != null) return _cached;
            const string path = "res://Data/spire_station.json";
            if (Godot.FileAccess.FileExists(path))
            {
                using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                try { _cached = JsonSerializer.Deserialize<SpireStationData>(f.GetAsText(), _json); }
                catch (JsonException e) { GD.PushError($"[SpireStation] {path}: {e.Message}"); }
            }
            return _cached ??= new SpireStationData();
        }
    }

    public class StationRepair { public float HpPerSecond { get; set; } = 15f; public float MaterialsPerHp { get; set; } = 0.4f; }
    public class StationBank { public float RefillCost { get; set; } = 25f; public float TrainCost { get; set; } = 40f; public float TrainXp { get; set; } = 60f; }
    public class StationDock { public float Damage { get; set; } = 30f; public float Interval { get; set; } = 0.45f; public float Range { get; set; } = 24f; public float Splash { get; set; } = 1.6f; }
    public class StationMaterialsMode { public float DropShare { get; set; } = 0.5f; public float SpireRateMult { get; set; } = 2f; }

    /// <summary>A one-shot strike bought at the Spire (Data/spire_station.json "strikes").</summary>
    public class SpireStrike
    {
        public string Id { get; set; } = "";
        public int Key { get; set; } = 1;
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public float Cost { get; set; } = 100f;
        public float CostPerWave { get; set; } = 0.08f;
        public float Damage { get; set; }
        public float DamagePerWave { get; set; } = 0.12f;
        public float Radius { get; set; } = 3f;
        public int Shells { get; set; }
        public float Seconds { get; set; }
        public float Stun { get; set; }
        public int CostAt(int wave) => Mathf.Max(1, Mathf.RoundToInt(Cost * (1f + CostPerWave * Mathf.Max(0, wave)) * MetaRun.StrikeCostMult * RoleRun.StrikeCostMult));
        /// <summary>Damage at a wave: grows a share a wave, and with the health ramp past the authored waves.</summary>
        public float DamageAt(int wave) => Damage * (1f + DamagePerWave * Mathf.Max(0, wave)) * VineWaveLoader.RampAt(wave);
    }

    public class SpireUpgrade
    {
        public string Id { get; set; } = "";
        public string Group { get; set; } = "spire";      // spire | bit
        public string Name { get; set; } = "";
        public string Currency { get; set; } = "resources"; // resources | materials
        public string Text { get; set; } = "";
        public int MaxLevel { get; set; } = 5;
        public float Cost { get; set; } = 20f;
        public float CostGrowth { get; set; } = 1.5f;
        public float Step { get; set; } = 0.25f;
        public bool UsesMaterials => Currency == "materials";
        /// <summary>Cost of the next level when <paramref name="level"/> are owned.</summary>
        public int CostAt(int level) => Mathf.RoundToInt(Cost * Mathf.Pow(CostGrowth, level));
    }

    /// <summary>
    /// The Spire as a place BIT goes to. Within reach, F opens the Spire menu (upgrades for the
    /// Spire in Resources, for BIT in banked Materials, refill and training); G climbs in, after
    /// which the mouse aims the Spire's cannon and the left button fires it; standing still at a
    /// Holding F (rather than tapping it) repairs a damaged Spire with BIT's own Materials. It
    /// used to repair whenever BIT stood still beside it, and BIT starts the run there, so an idle
    /// BIT kept the Spire topped up for free. One per battle; all of it lasts the run.
    /// </summary>
    public partial class SpireStation : Node3D
    {
        public static SpireStation Current { get; private set; }
        public SpireStationData Data { get; private set; }

        private readonly Dictionary<string, int> _levels = new();
        private SpirePanel _panel;
        private Label3D _prompt;
        private float _dockCooldown;
        private bool _fireHeld;
        private float _repairFx;
        private bool _fDown;
        private ulong _fDownMs;
        /// <summary>F is held long enough to count as a hold (repairing), not a tap (menu).</summary>
        public bool Repairing { get; private set; }
        public const ulong HoldMs = 300;
        /// <summary>Tests: hold F (repair) without the keyboard.</summary>
        internal bool TestHoldRepair { get; set; }
        private VineHarvester _harvester;
        private MeshInstance3D _reticle;
        private CanvasLayer _dockHint;
        private VinePlayer _player;
        private float _baseHarvesterMax;

        /// <summary>BIT is inside the Spire, gunning.</summary>
        public bool Docked { get; private set; }
        public bool PanelOpen => GodotObject.IsInstanceValid(_panel) && _panel.Visible;
        /// <summary>HP repaired so far this run (tests, debrief).</summary>
        public float Repaired { get; private set; }
        public event Action Changed;

        public override void _Ready()
        {
            Name = "SpireStation";
            Current = this;
            Data = SpireStationData.Load();
            ServiceLocator.Register(this);
            _prompt = new Label3D
            {
                Text = "[F] SPIRE",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 56,
                OutlineSize = 14,
                Modulate = BitPalette.Accent,
                NoDepthTest = true,
                Visible = false,
                PixelSize = 0.006f,
            };
            AddChild(_prompt);

            // Where the cannon (or BIT's aimed fire) will land
            var ring = new TorusMesh { InnerRadius = 0.42f, OuterRadius = 0.55f, Rings = 24, RingSegments = 6 };
            _reticle = new MeshInstance3D
            {
                Name = "AimReticle",
                Mesh = ring,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = BitPalette.Accent,
                    NoDepthTest = true,
                },
            };
            AddChild(_reticle);

            _dockHint = new CanvasLayer { Layer = 20, Visible = false, Name = "DockHint" };
            var hint = new Label
            {
                Name = "Hint",
                Text = DockHintText,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _dockHintLabel = hint;
            hint.AddThemeFontSizeOverride("font_size", 20);
            hint.AddThemeColorOverride("font_color", BitPalette.Accent);
            hint.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
            hint.AddThemeConstantOverride("outline_size", 8);
            hint.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            hint.OffsetTop = -170;
            hint.OffsetBottom = -140;
            hint.OffsetLeft = -600;
            hint.OffsetRight = 600;
            hint.MouseFilter = Control.MouseFilterEnum.Ignore;
            _dockHint.AddChild(hint);
            AddChild(_dockHint);
            BuildCrosshair();
        }

        private const string DockHintText = "IN THE SPIRE (map view)   Hold left mouse to fire the cannon where you aim   [V] first person   [G] or [F] climb out";
        private const string GunnerHintText = "IN THE SPIRE   Move the mouse to aim, hold left mouse to fire   [V] map view   [G] or [F] climb out";
        private Label _dockHintLabel;

        public override void _ExitTree()
        {
            if (Current == this) Current = null;
            // Leaving the battle from gunner view must not leave the mouse captured
            if (GunnerView && Input.MouseMode == Input.MouseModeEnum.Captured) Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        // ── Levels and what they give ──

        public int LevelOf(string id) => _levels.TryGetValue(id, out var l) ? l : 0;
        public SpireUpgrade Upgrade(string id) => Data.Upgrades.FirstOrDefault(u => u.Id == id);
        private float Gain(string id) => (Upgrade(id)?.Step ?? 0f) * LevelOf(id);

        public float BitDamageMult => 1f + Gain("bit_weapon");
        public float BitRateMult => 1f + Gain("bit_trigger");
        public float BitRangeBonus => Gain("bit_reach");
        public float BitAbilityMult => 1f + Gain("bit_core");
        public float SpireDamageMult => 1f + Gain("spire_guns");
        public float SpireRangeBonus => Gain("spire_reach");
        public float MaterialsBanked => _harvester?.MaterialsAccumulated ?? 0f;

        /// <summary>Why <paramref name="id"/> can't be bought now, or null.</summary>
        public string CantBuy(string id)
        {
            var u = Upgrade(id);
            if (u == null) return "Unknown";
            int lv = LevelOf(id);
            if (lv >= u.MaxLevel) return "Maxed";
            int cost = u.CostAt(lv);
            if (u.UsesMaterials) return MaterialsBanked + 0.001f >= cost ? null : "Needs Materials";
            return (GameManager.Instance?.CurrentResources ?? 0) >= cost ? null : "Needs Resources";
        }

        public bool Buy(string id)
        {
            if (CantBuy(id) != null) return false;
            var u = Upgrade(id);
            int cost = u.CostAt(LevelOf(id));
            bool paid = u.UsesMaterials ? _harvester.SpendMaterials(cost) : GameManager.Instance.SpendResources(cost);
            if (!paid) return false;
            _levels[id] = LevelOf(id) + 1;
            Apply(u);
            GD.Print($"[SpireStation] {u.Name} -> level {LevelOf(id)} for {cost} {u.Currency}");
            Changed?.Invoke();
            return true;
        }

        private void Apply(SpireUpgrade u)
        {
            switch (u.Id)
            {
                case "spire_plating":
                    if (_harvester != null) _harvester.IncreaseMaxHP(_baseHarvesterMax * u.Step);
                    _harvester?.SetPlating(LevelOf(u.Id));
                    break;
                case "spire_guns":
                    ShowGuns();
                    break;
                case "spire_reach":
                    // Show the new reach on the ground for a moment
                    if (_harvester != null)
                        VfxFactory.SpawnAbilityRing(GetTree(), _harvester.GlobalPosition, 14f + SpireRangeBonus, new Color(1f, 0.75f, 0.3f));
                    break;
                case "bit_core":
                    if (_player != null)
                    {
                        _player.MaxMaterials += 40f;
                        _player.CurrentMaterials = Mathf.Min(_player.MaxMaterials, _player.CurrentMaterials + 40f);
                        GameEvents.OnPlayerMaterialsChanged?.Invoke(_player.CurrentMaterials, _player.MaxMaterials);
                    }
                    break;
            }
        }

        public bool CanRefill => MaterialsBanked + 0.001f >= Data.Bank.RefillCost && _player != null && _player.CurrentMaterials < _player.MaxMaterials - 0.5f;
        public bool CanTrain => MaterialsBanked + 0.001f >= Data.Bank.TrainCost && _player?.Progression != null;

        /// <summary>Banked Materials fill BIT's own Materials bar.</summary>
        public bool Refill()
        {
            if (!CanRefill || !_harvester.SpendMaterials(Data.Bank.RefillCost)) return false;
            _player.CurrentMaterials = _player.MaxMaterials;
            GameEvents.OnPlayerMaterialsChanged?.Invoke(_player.CurrentMaterials, _player.MaxMaterials);
            VfxFactory.SpawnEnergyBurst(GetTree(), _player.GlobalPosition + Vector3.Up * 0.6f, new Color(0.3f, 0.5f, 1f), 8);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Banked Materials become BIT experience.</summary>
        public bool Train()
        {
            if (!CanTrain || !_harvester.SpendMaterials(Data.Bank.TrainCost)) return false;
            _player.Progression.AddXp(Data.Bank.TrainXp);
            VfxFactory.SpawnEnergyBurst(GetTree(), _player.GlobalPosition + Vector3.Up * 0.6f, BitPalette.Accent, 10);
            Changed?.Invoke();
            return true;
        }

        // ── Where BIT is ──

        public bool InReach
        {
            get
            {
                if (_player == null || _harvester == null || !_player.IsAlive || _harvester.IsDestroyed) return false;
                var d = _player.GlobalPosition - _harvester.GlobalPosition;
                d.Y = 0;
                return d.Length() <= Data.Reach + Constants.VINE_CELL_SIZE * 0.75f;
            }
        }

        public void OpenPanel()
        {
            if (!InReach || Docked) return;
            if (!GodotObject.IsInstanceValid(_panel))
            {
                _panel = new SpirePanel(this);
                GetTree().CurrentScene.AddChild(_panel);
            }
            _panel.Visible = true;
            _panel.Refresh();
        }

        public void ClosePanel()
        {
            if (GodotObject.IsInstanceValid(_panel)) _panel.Visible = false;
        }

        public void Dock()
        {
            if (Docked || !InReach) return;
            ClosePanel();
            Docked = true;
            _player.SetDocked(true);
            _dockCooldown = 0f;
            VfxFactory.SpawnEnergyBurst(GetTree(), _harvester.GlobalPosition + Vector3.Up * 1.5f, BitPalette.Accent, 10);
            Changed?.Invoke();
            // Climbing in is first person (V for the map); the overhead view didn't read as "in" it
            EnterGunnerView();
        }

        public void Undock()
        {
            if (!Docked) return;
            ExitGunnerView();
            Docked = false;
            _fireHeld = false;
            if (_player != null)
            {
                // Step out on the side of the Spire facing the camera
                var away = ServiceLocator.TryGet<TDCamera>(out var cam) ? cam.ScreenToGround(new Vector3(0, 0, 1)) : Vector3.Back;
                var at = _harvester.GlobalPosition + away.Normalized() * (Constants.VINE_CELL_SIZE * 1.2f);
                _player.SetDocked(false, at);
            }
            Changed?.Invoke();
        }

        public override void _Process(double delta)
        {
            long __pt = FrameProfiler.Start();
            try
            {
                VineEnemy.HitSource = "the Spire";
                try { ProcessTick(delta); }
                finally { VineEnemy.HitSource = null; }
        
            }
            finally { FrameProfiler.Stop("spire_station", __pt); }
        }

        private void ProcessTick(double delta)
        {
            float dt = (float)delta;
            _harvester ??= ServiceLocator.TryGet<VineGrid>(out var g) ? g.Harvester : null;
            if (_harvester != null && _baseHarvesterMax <= 0f) _baseHarvesterMax = _harvester.MaxHP;
            if (_harvester != null && !_runStartApplied)
            {
                // The perk tree's head start for the Spire: Gun Foundry mounts guns (and Bruteforge
                // one more), Stockpile fills a charge of every strike
                _runStartApplied = true;
                ShowGuns();
                if (MetaRun.StartStrikes)
                {
                    foreach (var sk in Data.Strikes) _charges[sk.Id] = Charges(sk.Id) + 1;
                    Changed?.Invoke();
                }
            }
            // Bruteforge: another gun every few waves
            if (_harvester != null && GunsWanted != _gunsShown) ShowGuns();
            if (_player == null || !GodotObject.IsInstanceValid(_player))
                _player = ServiceLocator.TryGet<VinePlayer>(out var p) ? p : null;
            if (_harvester == null || _player == null) return;

            bool inReach = InReach;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            bool live = phase is GamePhase.Build or GamePhase.Wave or GamePhase.WaveComplete;

            _prompt.Visible = inReach && !Docked && !PanelOpen && live;
            _prompt.Text = _harvester.CurrentHP < _harvester.MaxHP - 0.5f ? "[F] SPIRE   hold F: repair" : "[F] SPIRE";
            if (_prompt.Visible)
                _prompt.GlobalPosition = _harvester.GlobalPosition + Vector3.Up * (_harvester.ModelTop + 2.6f); // above the Spire's name tag

            if (PanelOpen && !inReach) ClosePanel();
            if (Docked && (_harvester.IsDestroyed || !live && phase != GamePhase.Paused)) Undock();

            // F: a tap opens the menu (on release), a hold repairs
            if (_fDown && !Input.IsKeyPressed(Key.F)) ReleaseF();
            Repairing = !Docked && inReach && live
                && (TestHoldRepair || _fDown && Time.GetTicksMsec() - _fDownMs >= HoldMs);
            if (Docked) UpdateDock(dt);
            else if (Repairing) UpdateRepair(dt);

            // The aim marker shows while gunning from the Spire and while BIT aims by hand
            _dockHint.Visible = Docked;
            if (GunnerView) PlaceGunnerCam();
            bool aiming = Docked || _player.IsAiming;
            var aimAt = aiming ? (TestAimPoint ?? (GunnerView ? GunnerAim() : _player.TestAimPoint ?? CursorGround(GetViewport()))) : null;
            _reticle.Visible = aimAt != null;
            if (aimAt != null) _reticle.GlobalPosition = aimAt.Value + Vector3.Up * 0.08f;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey k && k.Pressed && !k.Echo)
            {
                if (k.Keycode == Key.F)
                {
                    if (Docked) { Undock(); GetViewport().SetInputAsHandled(); }
                    else if (PanelOpen) { ClosePanel(); GetViewport().SetInputAsHandled(); }
                    else if (InReach) { _fDown = true; _fDownMs = Time.GetTicksMsec(); GetViewport().SetInputAsHandled(); }
                }
                else if (k.Keycode == Key.V && Docked)
                {
                    if (GunnerView) ExitGunnerView(); else EnterGunnerView();
                    GetViewport().SetInputAsHandled();
                }
                else if (k.Keycode == Key.G)
                {
                    if (Docked) Undock(); else if (InReach) Dock();
                    if (Docked || InReach) GetViewport().SetInputAsHandled();
                }
                else if (k.Keycode == Key.Escape && PanelOpen)
                {
                    ClosePanel();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventKey kr && !kr.Pressed && kr.Keycode == Key.F && _fDown)
            {
                ReleaseF();
                GetViewport().SetInputAsHandled();
            }
            else if (Docked && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                _fireHeld = mb.Pressed;
                GetViewport().SetInputAsHandled();
            }
        }

        private void ReleaseF()
        {
            bool tap = Time.GetTicksMsec() - _fDownMs < HoldMs;
            _fDown = false;
            Repairing = false;
            if (tap && InReach && !Docked) OpenPanel();
        }

        // ── Repair ──

        private void UpdateRepair(float dt)
        {
            if (_harvester.CurrentHP >= _harvester.MaxHP - 0.01f || _player.CurrentMaterials <= 0.01f) return;
            float hp = Mathf.Min(Data.Repair.HpPerSecond * dt, _harvester.MaxHP - _harvester.CurrentHP);
            float cost = hp * Data.Repair.MaterialsPerHp;
            if (cost > _player.CurrentMaterials) { hp = _player.CurrentMaterials / Data.Repair.MaterialsPerHp; cost = _player.CurrentMaterials; }
            _player.CurrentMaterials -= cost;
            GameEvents.OnPlayerMaterialsChanged?.Invoke(_player.CurrentMaterials, _player.MaxMaterials);
            _harvester.Heal(hp);
            Repaired += hp;

            _repairFx -= dt;
            if (_repairFx <= 0f)
            {
                _repairFx = 0.12f;
                var from = _player.GlobalPosition + Vector3.Up * 0.7f;
                var to = _harvester.GlobalPosition + Vector3.Up * Mathf.Min(2.5f, _harvester.ModelTop * 0.5f + 0.5f);
                VfxFactory.SpawnArc(GetTree(), from, to, new Color(0.35f, 0.95f, 0.55f));
            }
        }

        // ── Docked: the Spire's cannon ──

        /// <summary>Where the mouse points on the ground (null when it points at the sky).</summary>
        public static Vector3? CursorGround(Viewport vp)
        {
            var cam = vp?.GetCamera3D();
            if (cam == null || !ServiceLocator.TryGet<VineGrid>(out var grid)) return null;
            var mouse = vp.GetMousePosition();
            var from = cam.ProjectRayOrigin(mouse);
            var dir = cam.ProjectRayNormal(mouse);
            if (dir.Y > -0.01f) return null;
            // March down the ray to the drawn terrain
            float t = 0f;
            for (int i = 0; i < 200; i++)
            {
                var p = from + dir * t;
                float h = grid.GetWorldHeight(p.X, p.Z);
                if (p.Y <= h) return new Vector3(p.X, h, p.Z);
                t += Mathf.Max(0.25f, (p.Y - h) * 0.5f);
            }
            return null;
        }

        private void UpdateDock(float dt)
        {
            // A release that landed on the menu or outside the window left the trigger held,
            // so the cannon kept firing by itself: trust the button's real state
            if (_fireHeld && !_testFire && !Input.IsMouseButtonPressed(MouseButton.Left)) _fireHeld = false;
            _dockCooldown -= dt;
            if (!_fireHeld || _dockCooldown > 0f) return;
            var aim = TestAimPoint ?? (GunnerView ? GunnerAim() : CursorGround(GetViewport()));
            if (aim == null) return;
            FireCannon(aim.Value);
        }

        // ── Gunner view: first person from the top of the Spire ──
        // V while docked. The mouse turns the view (captured), the left button fires where the
        // crosshair is; V goes back to the map, climbing out or pausing leaves it.

        private Camera3D _gunnerCam;
        private Camera3D _mapCam;
        private CanvasLayer _crosshair;
        private float _gunYaw, _gunPitch;
        private const float GunnerSensitivity = 0.0028f;
        private const float GunnerPitchMin = -1.25f, GunnerPitchMax = 0.2f;

        public bool GunnerView => _gunnerCam != null && GodotObject.IsInstanceValid(_gunnerCam);
        internal Camera3D GunnerCamera => GunnerView ? _gunnerCam : null;
        internal float GunnerYaw { get => _gunYaw; set => _gunYaw = value; }
        internal float GunnerPitch { get => _gunPitch; set => _gunPitch = Mathf.Clamp(value, GunnerPitchMin, GunnerPitchMax); }

        public void EnterGunnerView()
        {
            if (!Docked || GunnerView || _harvester == null) return;
            _mapCam = GetViewport().GetCamera3D();
            _gunnerCam = new Camera3D { Name = "GunnerCam", Fov = 72f, Near = 0.05f, Far = 400f };
            GetTree().CurrentScene.AddChild(_gunnerCam);
            // Start facing the way the map camera looked, a little down
            var fwd = _mapCam != null ? -_mapCam.GlobalBasis.Z : Vector3.Forward;
            fwd.Y = 0;
            if (fwd.LengthSquared() < 0.001f) fwd = Vector3.Forward;
            _gunYaw = Mathf.Atan2(-fwd.X, -fwd.Z);
            _gunPitch = -0.3f;
            PlaceGunnerCam();
            _gunnerCam.Current = true;
            if (DisplayServer.GetName() != "headless") Input.MouseMode = Input.MouseModeEnum.Captured;
            _crosshair.Visible = true;
            if (_dockHintLabel != null) _dockHintLabel.Text = GunnerHintText;
            Changed?.Invoke();
        }

        public void ExitGunnerView()
        {
            if (_crosshair != null) _crosshair.Visible = false;
            if (_dockHintLabel != null) _dockHintLabel.Text = DockHintText;
            if (!GunnerView) return;
            if (Input.MouseMode == Input.MouseModeEnum.Captured) Input.MouseMode = Input.MouseModeEnum.Visible;
            if (_mapCam != null && GodotObject.IsInstanceValid(_mapCam)) _mapCam.Current = true;
            _gunnerCam.QueueFree();
            _gunnerCam = null;
            Changed?.Invoke();
        }

        /// <summary>Above the Spire's top, turned by the mouse.</summary>
        private void PlaceGunnerCam()
        {
            if (!GunnerView || _harvester == null) return;
            // Out past the top of the model on the side it faces, so the Spire's own spike isn't in the view
            var ahead = new Vector3(-Mathf.Sin(_gunYaw), 0f, -Mathf.Cos(_gunYaw));
            _gunnerCam.GlobalPosition = _harvester.GlobalPosition + Vector3.Up * (_harvester.ModelTop + 1.4f) + ahead * 1.6f;
            _gunnerCam.Rotation = new Vector3(_gunPitch, _gunYaw, 0f);
        }

        /// <summary>Where the crosshair points on the ground; at the sky, the cannon's reach straight ahead.</summary>
        internal Vector3? GunnerAim()
        {
            if (!GunnerView || !ServiceLocator.TryGet<VineGrid>(out var grid)) return null;
            var from = _gunnerCam.GlobalPosition;
            var dir = -_gunnerCam.GlobalBasis.Z;
            float range = Data.Dock.Range + SpireRangeBonus;
            if (dir.Y < -0.01f)
            {
                float t = 0f;
                for (int i = 0; i < 300 && t < range * 3f; i++)
                {
                    var p = from + dir * t;
                    float h = grid.GetWorldHeight(p.X, p.Z);
                    if (p.Y <= h) return new Vector3(p.X, h, p.Z);
                    t += Mathf.Max(0.2f, (p.Y - h) * 0.5f);
                }
            }
            var flat = new Vector3(dir.X, 0, dir.Z).Normalized();
            var far = _harvester.GlobalPosition + flat * range;
            return new Vector3(far.X, grid.GetWorldHeight(far.X, far.Z), far.Z);
        }

        public override void _Input(InputEvent @event)
        {
            if (!GunnerView) return;
            if (@event is InputEventMouseMotion mm)
            {
                float sens = GunnerSensitivity * GameSettings.MouseSensitivity;
                _gunYaw -= mm.Relative.X * sens;
                GunnerPitch = _gunPitch - mm.Relative.Y * sens;
                GetViewport().SetInputAsHandled();
            }
            else if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                // The cursor is hidden at the screen centre: clicks must not reach the HUD under it
                _fireHeld = mb.Pressed;
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Notification(int what)
        {
            // The pause menu or a perk pick needs the mouse back
            if (what == NotificationPaused) ExitGunnerView();
        }

        private void BuildCrosshair()
        {
            _crosshair = new CanvasLayer { Layer = 19, Visible = false, Name = "GunnerCrosshair" };
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.Draw += () =>
            {
                var c = root.Size / 2f;
                var col = BitPalette.Accent;
                var dark = new Color(0, 0, 0, 0.6f);
                foreach (var (w, cc) in new[] { (5f, dark), (2f, col) })
                {
                    root.DrawArc(c, 18f, 0f, Mathf.Tau, 48, cc, w, true);
                    root.DrawLine(c + new Vector2(-34, 0), c + new Vector2(-10, 0), cc, w, true);
                    root.DrawLine(c + new Vector2(10, 0), c + new Vector2(34, 0), cc, w, true);
                    root.DrawLine(c + new Vector2(0, -34), c + new Vector2(0, -10), cc, w, true);
                    root.DrawLine(c + new Vector2(0, 10), c + new Vector2(0, 34), cc, w, true);
                }
                root.DrawCircle(c, 2.5f, col);
            };
            root.Resized += root.QueueRedraw;
            _crosshair.AddChild(root);
            AddChild(_crosshair);
        }

        // ── Strikes: one-shot weapons bought with Resources, fired with 1, 2, 3 ──
        private readonly Dictionary<string, int> _charges = new();
        private bool _runStartApplied;
        private int _gunsShown;

        /// <summary>
        /// Extra guns on the Spire: Spire Guns levels, Gun Foundry (perk tree), the role's head
        /// start and, for Bruteforge, one more every few waves.
        /// </summary>
        public int GunsWanted => LevelOf("spire_guns") + MetaRun.StartGuns + RoleRun.StartGuns + RoleRun.WaveGuns(Wave);

        private void ShowGuns()
        {
            if (_harvester == null) return;
            int fromWaves = RoleRun.WaveGuns(Wave);
            if (fromWaves > _waveGunsShown)
            {
                DamageNumbers.Tag(_harvester.GlobalPosition + Vector3.Up * 4.5f, "THE FORGE MOUNTS ANOTHER GUN", new Color(1f, 0.6f, 0.25f));
                GameEvents.OnAnnouncement?.Invoke("BRUTEFORGE: the Spire mounted another gun");
            }
            _waveGunsShown = fromWaves;
            _gunsShown = GunsWanted;
            _harvester.SetExtraGuns(_gunsShown);
        }
        private int _waveGunsShown;
        public int Charges(string id) => _charges.TryGetValue(id, out var n) ? n : 0;
        public int TotalCharges => _charges.Values.Sum();
        public SpireStrike Strike(string id) => Data.Strikes.FirstOrDefault(x => x.Id == id);
        private static int Wave => GameManager.Instance?.CurrentWave ?? 0;
        public int StrikeCost(string id) => Strike(id)?.CostAt(Wave) ?? 0;
        /// <summary>Strikes fired so far (tests) and what the last one hit.</summary>
        public int StrikesFired { get; private set; }
        public int LastStrikeHits { get; private set; }

        public string CantBuyStrike(string id)
        {
            var s = Strike(id);
            if (s == null) return "Unknown";
            return (GameManager.Instance?.CurrentResources ?? 0) >= s.CostAt(Wave) ? null : "Needs Resources";
        }

        public bool BuyStrike(string id)
        {
            if (CantBuyStrike(id) != null) return false;
            var s = Strike(id);
            if (!GameManager.Instance.SpendResources(s.CostAt(Wave))) return false;
            _charges[id] = Charges(id) + 1;
            GD.Print($"[SpireStation] {s.Name} bought ({Charges(id)} ready)");
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Fire a strike at <paramref name="at"/> (the mouse on the ground, or the gunner crosshair;
        /// with neither, the thickest knot of enemies). Uses a charge.
        /// </summary>
        public bool FireStrike(string id, Vector3? at = null)
        {
            var s = Strike(id);
            if (s == null || Charges(id) <= 0) return false;
            var target = at ?? TestAimPoint ?? (GunnerView ? GunnerAim() : CursorGround(GetViewport())) ?? DensestEnemies();
            if (target == null) { GameEvents.OnAnnouncement?.Invoke($"{s.Name}: nothing to aim at"); return false; }
            var p = target.Value;
            if (ServiceLocator.TryGet<VineGrid>(out var grid)) p.Y = grid.GetWorldHeight(p.X, p.Z);
            _charges[id] = Charges(id) - 1;
            StrikesFired++;
            VineEnemy.HitSource = "the Spire";
            try
            {
                switch (id)
                {
                    case "barrage": Barrage(s, p); break;
                    case "emp": Emp(s, p); break;
                    default: Lance(s, p); break;
                }
            }
            finally { VineEnemy.HitSource = null; }
            Changed?.Invoke();
            return true;
        }

        private int HitCircle(Vector3 at, float radius, float damage)
        {
            int hits = 0;
            foreach (var e in Roster.Enemies(GetTree()).ToList())
            {
                if (!IsInstanceValid(e) || !e.IsAlive) continue;
                var d = e.GlobalPosition - at; d.Y = 0;
                if (d.Length() > radius) continue;
                e.TakeDamage(damage * (1f - 0.3f * d.Length() / radius), DamageKind.Heavy);
                hits++;
            }
            return hits;
        }

        private void Lance(SpireStrike s, Vector3 at)
        {
            var col = new Color(1f, 0.9f, 0.55f);
            VfxFactory.SpawnTracer(GetTree(), at + Vector3.Up * 45f, at, col, 140f);
            VfxFactory.SpawnExplosion(GetTree(), at, s.Radius, col);
            VfxFactory.SpawnAbilityRing(GetTree(), at, s.Radius * 1.4f, col);
            VfxFactory.SpawnEnergyBurst(GetTree(), at + Vector3.Up, col, 16);
            LastStrikeHits = HitCircle(at, s.Radius, s.DamageAt(Wave));
            if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(1.6f, 0.5f);
            Announce(s, LastStrikeHits);
        }

        private void Barrage(SpireStrike s, Vector3 at)
        {
            int shells = Mathf.Max(1, s.Shells);
            float gap = Mathf.Max(0.02f, s.Seconds / shells);
            var rng = new RandomNumberGenerator();
            var tree = GetTree();
            LastStrikeHits = 0;
            for (int i = 0; i < shells; i++)
            {
                float a = rng.Randf() * Mathf.Tau, r = Mathf.Sqrt(rng.Randf()) * s.Radius;
                var p = at + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (ServiceLocator.TryGet<VineGrid>(out var grid)) p.Y = grid.GetWorldHeight(p.X, p.Z);
                float dmg = s.DamageAt(Wave);
                void Land()
                {
                    if (!IsInstanceValid(this)) return;
                    VfxFactory.SpawnExplosion(tree, p, 1.6f, new Color(1f, 0.6f, 0.25f));
                    VineEnemy.HitSource = "the Spire";
                    try { LastStrikeHits += HitCircle(p, 1.8f, dmg); }
                    finally { VineEnemy.HitSource = null; }
                    if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.4f, 0.1f);
                }
                if (i == 0) Land();
                else tree.CreateTimer(gap * i).Timeout += Land;
            }
            VfxFactory.SpawnAbilityRing(tree, at, s.Radius, new Color(1f, 0.6f, 0.25f));
            Announce(s, -1);
        }

        private void Emp(SpireStrike s, Vector3 at)
        {
            var col = new Color(0.45f, 0.85f, 1f);
            int hits = 0;
            foreach (var e in Roster.Enemies(GetTree()).ToList())
            {
                if (!IsInstanceValid(e) || !e.IsAlive) continue;
                var d = e.GlobalPosition - at; d.Y = 0;
                if (d.Length() > s.Radius) continue;
                e.ApplyStun(s.Stun);
                e.StripShield();
                hits++;
            }
            VfxFactory.SpawnAbilityRing(GetTree(), at, s.Radius, col);
            VfxFactory.SpawnCorruptionPulse(GetTree(), at + Vector3.Up * 0.5f, col);
            VfxFactory.SpawnEnergyBurst(GetTree(), at + Vector3.Up, col, 20);
            if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.8f, 0.3f);
            LastStrikeHits = hits;
            Announce(s, hits);
        }

        private void Announce(SpireStrike s, int hits)
        {
            string what = hits < 0 ? "incoming" : hits == 0 ? "missed everything" : $"{hits} hit";
            Celebration.Show("Strike", s.Name.ToUpperInvariant(), $"{what}  ·  {Charges(s.Id)} left", new Color(1f, 0.8f, 0.4f), 1.6f);
        }

        /// <summary>The middle of the biggest knot of enemies (a strike with nothing aimed).</summary>
        private Vector3? DensestEnemies()
        {
            var list = Roster.Enemies(GetTree()).Where(e => IsInstanceValid(e) && e.IsAlive).ToList();
            if (list.Count == 0) return null;
            Vector3 best = list[0].GlobalPosition; int bestN = 0;
            foreach (var a in list)
            {
                int n = list.Count(b => b.GlobalPosition.DistanceTo(a.GlobalPosition) < 4f);
                if (n > bestN) { bestN = n; best = a.GlobalPosition; }
            }
            return best;
        }

        public override void _UnhandledKeyInput(InputEvent e)
        {
            if (e is not InputEventKey { Pressed: true, Echo: false } k || VinePerkScreen.IsOverlayOpen) return;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Build;
            if (phase is not (GamePhase.Wave or GamePhase.Build or GamePhase.WaveComplete)) return;
            int key = k.Keycode switch { Key.Key1 or Key.Kp1 => 1, Key.Key2 or Key.Kp2 => 2, Key.Key3 or Key.Kp3 => 3, _ => 0 };
            if (key == 0) return;
            var s = Data.Strikes.FirstOrDefault(x => x.Key == key);
            if (s == null || Charges(s.Id) <= 0) return;
            if (FireStrike(s.Id)) GetViewport().SetInputAsHandled();
        }

        /// <summary>One cannon shot from the Spire's top toward <paramref name="aim"/>.</summary>
        public void FireCannon(Vector3 aim)
        {
            _dockCooldown = Data.Dock.Interval / Mathf.Max(0.1f, _player?.Progression != null ? BitRateMult : 1f);
            var top = _harvester.GlobalPosition + Vector3.Up * Mathf.Max(1.5f, _harvester.ModelTop * 0.9f);
            var flat = aim - _harvester.GlobalPosition;
            flat.Y = 0;
            float range = Data.Dock.Range + SpireRangeBonus;
            if (flat.Length() > range) aim = _harvester.GlobalPosition + flat.Normalized() * range;
            aim.Y = ServiceLocator.TryGet<VineGrid>(out var grid) ? grid.GetWorldHeight(aim.X, aim.Z) + 0.4f : aim.Y;

            float damage = Data.Dock.Damage * SpireDamageMult * BitDamageMult;
            int hits = 0;
            foreach (var n in Roster.Enemies(GetTree()))
            {
                if (n is not VineEnemy e || !e.IsAlive) continue;
                var d = e.GlobalPosition - aim;
                d.Y = 0;
                if (d.Length() > Data.Dock.Splash) continue;
                float prev = e.CurrentHealth;
                e.TakeDamage(damage * (1f - 0.5f * d.Length() / Data.Dock.Splash), DamageKind.Heavy);
                hits++;
                if (!e.IsAlive && prev > 0) _player?.CreditKill();
            }
            var col = BitPalette.Accent;
            VfxFactory.SpawnArc(GetTree(), top, aim, col.Lerp(Colors.White, 0.3f));
            VfxFactory.SpawnExplosion(GetTree(), aim, Data.Dock.Splash, col);
            if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.15f, 0.08f);
            ShotsFired++;
            LastShotHits = hits;
        }

        /// <summary>Tests: where the cannon aims instead of the mouse.</summary>
        internal Vector3? TestAimPoint { get; set; }
        /// <summary>Tests: hold the cannon's trigger.</summary>
        internal bool FireHeld { get => _fireHeld; set { _fireHeld = value; _testFire = value; } }
        private bool _testFire;

        /// <summary>Cannon shots fired and what the last one hit (tests).</summary>
        public int ShotsFired { get; private set; }
        public int LastShotHits { get; private set; }
    }
}
