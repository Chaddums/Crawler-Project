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
                Text = "IN THE SPIRE   Hold left mouse to fire the cannon where you aim   [G] or [F] climb out",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
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
        }

        public override void _ExitTree()
        {
            if (Current == this) Current = null;
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
        }

        public void Undock()
        {
            if (!Docked) return;
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
            float dt = (float)delta;
            _harvester ??= ServiceLocator.TryGet<VineGrid>(out var g) ? g.Harvester : null;
            if (_harvester != null && _baseHarvesterMax <= 0f) _baseHarvesterMax = _harvester.MaxHP;
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
            bool aiming = Docked || _player.IsAiming;
            var aimAt = aiming ? (TestAimPoint ?? _player.TestAimPoint ?? CursorGround(GetViewport())) : null;
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
            _dockCooldown -= dt;
            if (!_fireHeld || _dockCooldown > 0f) return;
            var aim = TestAimPoint ?? CursorGround(GetViewport());
            if (aim == null) return;
            FireCannon(aim.Value);
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
            foreach (var n in GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
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
        internal bool FireHeld { get => _fireHeld; set => _fireHeld = value; }

        /// <summary>Cannon shots fired and what the last one hit (tests).</summary>
        public int ShotsFired { get; private set; }
        public int LastShotHits { get; private set; }
    }
}
