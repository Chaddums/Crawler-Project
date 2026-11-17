using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Runs a boss fight's boss (Data/bosses.json). In a boss fight the boss comes with the site's
    /// boss wave, walks the route to the Spire and stays there breaking it; the fight is won the
    /// moment it dies. On the way it plays its trick: The Silencer silences every tower near it,
    /// The Overloader charges and bursts, taking a share of the health of every tower in reach.
    /// A bar at the top of the screen carries its name, health and what it is doing.
    /// (Until 2026-11 a boss fight was the ordinary waves held to the boss wave. No boss existed.)
    /// </summary>
    public partial class SiteBossManager : Node
    {
        public static SiteBossManager Current { get; private set; }

        /// <summary>This fight's boss, or null outside a boss fight.</summary>
        public SiteBossProfile Profile { get; private set; }
        public int BossWave { get; private set; }
        /// <summary>The boss on the field, or null (not here yet, or dead).</summary>
        public VineEnemy Boss => _boss != null && IsInstanceValid(_boss) && _boss.IsAlive ? _boss : null;
        public bool Spawned { get; private set; }
        public bool Defeated { get; private set; }
        /// <summary>Towers silenced right now.</summary>
        public int SilencedNow { get; private set; }
        /// <summary>The Overloader is charging; the burst lands when this reaches 0.</summary>
        public float ChargeLeft { get; private set; }
        /// <summary>Seconds to the next charge (The Overloader).</summary>
        public float NextCharge { get; private set; }
        public int Bursts { get; private set; }
        public int TowersBurned { get; private set; }
        public int TowersWrecked { get; private set; }
        /// <summary>Seconds the boss has been on the field.</summary>
        public float FightSeconds { get; private set; }
        /// <summary>Tower-seconds of silence dealt (tuning).</summary>
        public float SilencedSeconds { get; private set; }

        private VineEnemy _boss;
        private VineGrid _grid;
        private float _jamTick;
        private MeshInstance3D _ring;
        private ImmediateMesh _ringMesh;
        private StandardMaterial3D _ringMat;
        private float _ringPhase;
        private bool _warned, _warnedLast;

        // The bar
        private CanvasLayer _canvas;
        private Control _barRoot;
        private Label _nameLabel, _stateLabel;
        private ColorRect _barFill;
        private const float BarWidth = 620f;

        public override void _Ready()
        {
            Current = this;
            ServiceLocator.TryGet(out _grid);
            Profile = SiteBosses.Current;
            var site = Profile != null ? TerritoryManager.GetSite(GameManager.Instance?.BossSectionId ?? "") : null;
            BossWave = site?.BossWave ?? 0;
            if (Profile == null || BossWave <= 0) { Profile = null; return; }
            RunRecord.BossName = Profile.Name;
            GameEvents.OnWaveStarted += OnWaveStarted;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GD.Print($"[SiteBoss] {Profile.Name} comes with wave {BossWave}: {Profile.WhatWords}");
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveStarted -= OnWaveStarted;
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            if (Current == this) Current = null;
        }

        /// <summary>
        /// The boss's health at the wave it comes with: its own times the climb there, the AXIS
        /// threat, the planet and the campaign (not the fight's wave_hp: that is for its waves).
        /// </summary>
        public static float HealthFor(SiteBossProfile p, int wave)
            => p == null ? 0f : p.Hp * Mathf.Max(1f, Climb.Hp(wave) * Climb.Threat * Climb.PlanetHp * Climb.CampaignHp);

        private void OnWaveStarted(int wave)
        {
            if (Profile == null || Spawned) return;
            if (wave >= BossWave) { Spawn(); return; }
            int left = BossWave - wave;
            if (left <= Profile.WarnWaves && !_warned)
            {
                _warned = true;
                GameEvents.OnAnnouncement?.Invoke($"{Profile.Name} comes with wave {BossWave}. {Profile.WhatWords}");
                Comms.Say(Comms.Axis, Profile.AxisLine.Replace("{name}", Profile.Name).Replace("{waves}", left == 1 ? "one wave" : $"{left} waves"), story: true);
            }
            else if (left == 1 && !_warnedLast)
            {
                _warnedLast = true;
                GameEvents.OnAnnouncement?.Invoke($"{Profile.Name} comes with the next wave");
            }
        }

        /// <summary>Put the boss on the field now (the boss wave does it; tests call it).</summary>
        public VineEnemy Spawn()
        {
            if (Profile == null || Spawned) return Boss;
            if (!ServiceLocator.TryGet<VineWaveManager>(out var waves)) return null;
            int wave = Mathf.Max(1, GameManager.Instance?.CurrentWave ?? BossWave);
            _boss = waves.SpawnSiteBoss(Profile, HealthFor(Profile, wave));
            if (_boss == null) return null;
            Spawned = true;
            NextCharge = Profile.Interval * 0.6f;
            FightSeconds = 0f;
            BuildRing();
            BuildBar();
            Celebration.Show("Boss", Profile.Name.ToUpperInvariant(), $"{Profile.WhatWords} {Profile.AnswerWords}", Profile.Color, 5f);
            GameEvents.OnAnnouncement?.Invoke($"{Profile.Name}: {Profile.Arrives}");
            Comms.Say(Comms.Bit, Profile.BitLine, story: true);
            GD.Print($"[SiteBoss] {Profile.Name} on the field at P{GameManager.Instance?.CurrentPlanet}-W{wave} with {_boss.MaxHealth:F0} health");
            FlightRecorder.Note($"Site boss {Profile.Name} ({_boss.MaxHealth:F0} hp)");
            return _boss;
        }

        private void OnEnemyKilled(Node enemy)
        {
            if (Defeated || _boss == null || enemy != _boss) return;
            // Removed some other way (never as stuck, but a scene teardown passes through here too)
            if (_boss.CurrentHealth > 0f) return;
            Defeated = true;
            HideFight();
            string by = string.IsNullOrEmpty(_boss.LastHitBy) ? "" : $", the last hit from {_boss.LastHitBy}";
            GD.Print($"[SiteBoss] {Profile.Name} destroyed after {FightSeconds:F0} s on the field{by}; "
                + $"silenced {SilencedSeconds:F0} tower-seconds, {Bursts} bursts burned {TowersBurned} towers and wrecked {TowersWrecked}, "
                + $"Spire took {_boss.SpireDamageDone:F0} from it{(_boss.AtSpire ? " (it reached the Spire)" : "")}");
            GameEvents.OnBossDefeated?.Invoke();
            GameManager.Instance?.OnBossRunComplete();
        }

        public override void _PhysicsProcess(double delta)
        {
            var boss = Boss;
            if (Profile == null || boss == null)
            {
                if (Spawned && !Defeated && _boss != null && !IsInstanceValid(_boss)) HideFight();
                return;
            }
            float dt = (float)delta;
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.Wave;
            if (phase is GamePhase.Victory or GamePhase.Defeat) { HideFight(); return; }
            FightSeconds += dt;
            _ringPhase += dt;
            if (Profile.Jams) TickJam(boss, dt);
            else if (Profile.Overloads) TickOverload(boss, dt);
            DrawRing(boss);
            UpdateBar(boss);
        }

        // ── The Silencer ──

        private void TickJam(VineEnemy boss, float dt)
        {
            if ((_jamTick -= dt) > 0f) return;
            const float step = 0.2f;
            _jamTick = step;
            // Stunned or frozen, the field drops: an EMP or a freeze buys the towers a moment
            if (boss.IsStunned || boss.IsFrozen) { SilencedNow = 0; return; }
            int n = 0;
            var at = boss.GlobalPosition;
            float r2 = Profile.Radius * Profile.Radius;
            foreach (var node in Roster.Nodes(GetTree()))
            {
                if (!IsInstanceValid(node) || !node.CanBeSilenced) continue;
                var p = node.GlobalPosition;
                float dx = p.X - at.X, dz = p.Z - at.Z;
                if (dx * dx + dz * dz > r2) continue;
                node.Silence(step * 2.5f, Profile.Color);
                n++;
            }
            SilencedNow = n;
            SilencedSeconds += n * step;
        }

        // ── The Overloader ──

        private void TickOverload(VineEnemy boss, float dt)
        {
            if (ChargeLeft > 0f)
            {
                // A stun holds the charge where it is
                if (boss.IsStunned || boss.IsFrozen) return;
                ChargeLeft -= dt;
                if (ChargeLeft <= 0f) Burst(boss);
                return;
            }
            NextCharge -= dt;
            if (NextCharge > 0f) return;
            ChargeLeft = Profile.Charge;
            GameEvents.OnAnnouncement?.Invoke($"{Profile.Name} is charging: towers in the ring burn in {Profile.Charge:0} s");
        }

        /// <summary>The burst: every tower in reach loses a share of its health, and BIT a share of its own.</summary>
        internal void Burst(VineEnemy boss)
        {
            ChargeLeft = 0f;
            NextCharge = Profile.Interval - Profile.Charge;
            Bursts++;
            var at = boss.GlobalPosition;
            float r2 = Profile.Radius * Profile.Radius;
            var from = at + Vector3.Up * (Profile.RigHeight + Profile.RigScale);
            int hit = 0, wrecked = 0;
            var list = new List<VineNode>();
            foreach (var node in Roster.Nodes(GetTree()))
            {
                if (!IsInstanceValid(node) || node.IsDestroyed || node.Data == null || node.Data.Category != VineNodeCategory.Effect) continue;
                var p = node.GlobalPosition;
                float dx = p.X - at.X, dz = p.Z - at.Z;
                if (dx * dx + dz * dz <= r2) list.Add(node);
            }
            foreach (var node in list)
            {
                if (hit < 24) VfxFactory.SpawnArc(GetTree(), from, node.GlobalPosition + Vector3.Up * 0.8f, Profile.Color);
                hit++;
                if (node.Overload(Profile.TowerShare)) wrecked++;
            }
            if (ServiceLocator.TryGet<VinePlayer>(out var bit) && bit.IsAlive && !bit.IsDocked)
            {
                var p = bit.GlobalPosition;
                float dx = p.X - at.X, dz = p.Z - at.Z;
                if (dx * dx + dz * dz <= r2)
                {
                    VfxFactory.SpawnArc(GetTree(), from, p + Vector3.Up * 0.8f, Profile.Color);
                    bit.TakeDamage(bit.MaxHP * Profile.BitShare);
                }
            }
            TowersBurned += hit;
            TowersWrecked += wrecked;
            VfxFactory.SpawnExplosion(GetTree(), from, 2.4f, Profile.Color);
            VfxFactory.SpawnAbilityRing(GetTree(), at + Vector3.Up * 0.2f, Profile.Radius, Profile.Color);
            if (ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(1.2f, 0.5f);
            string burned = hit == 1 ? "1 tower burned" : $"{hit} towers burned";
            if (hit > 0)
                GameEvents.OnAnnouncement?.Invoke(wrecked > 0
                    ? $"Overload: {burned}, {wrecked} wrecked. Repair {(wrecked == 1 ? "it" : "them")}"
                    : $"Overload: {burned}");
            GD.Print($"[SiteBoss] burst {Bursts}: {burned}, {wrecked} wrecked");
        }

        // ── The ring on the ground ──

        private void BuildRing()
        {
            _ringMesh = new ImmediateMesh();
            _ringMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add, // lit on the dark ground, not a darker band of it
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor = Colors.White,
                VertexColorUseAsAlbedo = true,
            };
            _ring = new MeshInstance3D
            {
                Name = "BossRing", Mesh = _ringMesh, MaterialOverride = _ringMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true,
            };
            AddChild(_ring);
        }

        /// <summary>
        /// The trick's reach, draped over the ground (wall tops included: that is where the towers
        /// stand). The Silencer's is always on; The Overloader's closes in from its reach to the
        /// boss while it charges.
        /// </summary>
        private void DrawRing(VineEnemy boss)
        {
            if (_ring == null || _grid == null) return;
            _ringMesh.ClearSurfaces();
            var at = boss.GlobalPosition;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_ringPhase * 3.2f);
            if (Profile.Jams)
            {
                bool off = boss.IsStunned || boss.IsFrozen;
                Band(at, Profile.Radius, 0.3f, new Color(Profile.Color, off ? 0.15f : 0.6f + 0.35f * pulse));
            }
            else
            {
                bool charging = ChargeLeft > 0f;
                Band(at, Profile.Radius, charging ? 0.36f : 0.2f, new Color(Profile.Color, charging ? 1f : 0.4f));
                if (charging)
                {
                    float t = 1f - Mathf.Clamp(ChargeLeft / Mathf.Max(0.01f, Profile.Charge), 0f, 1f);
                    Band(at, Mathf.Lerp(Profile.Radius, 1.2f, t), 0.5f, new Color(1f, 0.9f, 0.6f, 0.9f));
                }
            }
        }

        private void Band(Vector3 at, float radius, float width, Color color)
        {
            const int n = 72;
            _ringMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Tau * i / n;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                for (int k = 0; k < 2; k++)
                {
                    float r = radius + (k == 0 ? -width : width) * 0.5f;
                    float x = at.X + cx * r, z = at.Z + cz * r;
                    _ringMesh.SurfaceSetColor(color);
                    _ringMesh.SurfaceAddVertex(new Vector3(x, _grid.GetWorldHeight(x, z) + 0.14f, z));
                }
            }
            _ringMesh.SurfaceEnd();
        }

        // ── The bar ──

        private void BuildBar()
        {
            _canvas = new CanvasLayer { Layer = 11, Name = "BossBar" };
            AddChild(_canvas);
            _barRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            _barRoot.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _barRoot.OffsetLeft = -BarWidth / 2f;
            _barRoot.OffsetRight = BarWidth / 2f;
            // Under the comms panel (from 60) and the announcement line (from 140, two rows at most)
            _barRoot.OffsetTop = 236;
            _barRoot.OffsetBottom = 304;
            _canvas.AddChild(_barRoot);

            var plate = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
            plate.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            plate.OffsetLeft = -14; plate.OffsetRight = 14; plate.OffsetTop = -8; plate.OffsetBottom = 8;
            var style = new StyleBoxFlat { BgColor = new Color(0.03f, 0.03f, 0.05f, 0.95f) }; // labels in the world behind it stay behind it
            style.SetCornerRadiusAll(6);
            style.BorderWidthBottom = 2;
            style.BorderColor = new Color(Profile.Color, 0.8f);
            plate.AddThemeStyleboxOverride("panel", style);
            _barRoot.AddChild(plate);

            _nameLabel = new Label
            {
                Text = Profile.Name.ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _nameLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _nameLabel.OffsetBottom = 24;
            _nameLabel.AddThemeFontSizeOverride("font_size", 20);
            _nameLabel.AddThemeColorOverride("font_color", Profile.Color.Lightened(0.4f));
            _barRoot.AddChild(_nameLabel);

            var back = new ColorRect { Color = new Color(0.12f, 0.1f, 0.14f, 0.95f), MouseFilter = Control.MouseFilterEnum.Ignore };
            back.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            back.OffsetTop = 28; back.OffsetBottom = 42;
            _barRoot.AddChild(back);
            _barFill = new ColorRect { Color = Profile.Color, MouseFilter = Control.MouseFilterEnum.Ignore };
            _barFill.Position = new Vector2(0, 28);
            _barFill.Size = new Vector2(BarWidth, 14);
            _barRoot.AddChild(_barFill);

            _stateLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            _stateLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _stateLabel.OffsetTop = 45; _stateLabel.OffsetBottom = 68;
            _stateLabel.AddThemeFontSizeOverride("font_size", 15);
            _stateLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.9f, 0.95f));
            _barRoot.AddChild(_stateLabel);
        }

        private void UpdateBar(VineEnemy boss)
        {
            if (_barRoot == null) return;
            float pct = Mathf.Clamp(boss.HealthPercent, 0f, 1f);
            _barFill.Size = new Vector2(BarWidth * pct, 14);
            _stateLabel.Text = StateLine(boss);
        }

        /// <summary>What the boss is doing, for the bar.</summary>
        public string StateLine(VineEnemy boss)
        {
            if (boss == null || Profile == null) return "";
            string hp = $"{Mathf.CeilToInt(boss.HealthPercent * 100f)}%";
            if (boss.AtSpire) return $"{hp}  ·  AT THE SPIRE: kill it before the Spire falls";
            if (Profile.Jams)
                return boss.IsStunned || boss.IsFrozen ? $"{hp}  ·  stunned: its field is down"
                    : SilencedNow > 0 ? $"{hp}  ·  {SilencedNow} tower{(SilencedNow == 1 ? "" : "s")} silenced" : $"{hp}  ·  no tower in its field";
            if (ChargeLeft > 0f) return $"{hp}  ·  OVERLOAD in {Mathf.CeilToInt(ChargeLeft)}: get BIT out of the ring";
            return $"{hp}  ·  next overload in {Mathf.CeilToInt(Mathf.Max(0f, NextCharge))} s";
        }

        /// <summary>Tests: the bar's parts.</summary>
        internal Control Bar => _barRoot;
        internal string BarState => _stateLabel?.Text ?? "";

        private void HideFight()
        {
            if (_ring != null && IsInstanceValid(_ring)) _ring.Visible = false;
            if (_canvas != null && IsInstanceValid(_canvas)) _canvas.Visible = false;
            SilencedNow = 0;
            ChargeLeft = 0f;
        }

        // ── Words for the wave card, the HUD and the screens before the fight ──

        /// <summary>The wave card's line when the boss is near or here, or null.</summary>
        public static string CardLine(int nextWave)
        {
            var m = Current;
            if (m?.Profile == null || m.Defeated) return null;
            if (m.Boss != null) return $"{m.Profile.Name} is on the field. {m.Profile.AnswerWords}";
            if (m.Spawned) return null;
            int left = m.BossWave - nextWave;
            if (left > m.Profile.WarnWaves) return null;
            return left <= 0 ? $"{m.Profile.Name} comes with this wave. {m.Profile.WhatWords}"
                : $"{m.Profile.Name} comes with wave {m.BossWave}. {m.Profile.WhatWords}";
        }
    }
}
