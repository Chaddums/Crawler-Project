using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>One upgrade step or branch from Data/tower_upgrades.json.</summary>
    public class TowerUpgradeStep
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public int Cost { get; set; }
        public float Damage { get; set; }
        public float Rate { get; set; }
        public float Range { get; set; }
        public float Hp { get; set; }
        public float Slow { get; set; }
        public float Buff { get; set; }
        public float Force { get; set; }
        /// <summary>Salvage Drill: a share more paid each wave.</summary>
        public float Income { get; set; }
        public bool AntiAir { get; set; }
        public string Kind { get; set; }
        public string[] Grants { get; set; } = System.Array.Empty<string>();
        /// <summary>Branches that change what the tower is for say so (panel text).</summary>
        public string Role { get; set; }
        public string Best { get; set; }
        public string Weak { get; set; }
    }

    public class TowerUpgradePlan
    {
        public List<TowerUpgradeStep> Levels { get; set; } = new();
        public List<TowerUpgradeStep> Branches { get; set; } = new();

        private static Dictionary<string, TowerUpgradePlan> _plans;

        /// <summary>The plan for a tower id (damage_tower, ...), or null.</summary>
        public static TowerUpgradePlan For(string towerId)
        {
            if (_plans == null) Load();
            return towerId != null && _plans.TryGetValue(towerId, out var p) ? p : null;
        }

        private static void Load()
        {
            _plans = new Dictionary<string, TowerUpgradePlan>();
            const string path = "res://Data/tower_upgrades.json";
            if (!Godot.FileAccess.FileExists(path)) { GD.PushWarning("[Upgrades] No tower_upgrades.json"); return; }
            using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            try
            {
                _plans = JsonSerializer.Deserialize<Dictionary<string, TowerUpgradePlan>>(f.GetAsText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }) ?? new();
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"[Upgrades] tower_upgrades.json: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Per-tower upgrades: two levels, then one of two branches (Data/tower_upgrades.json), plus
    /// what the counter traits need from a tower: whether it reaches flyers and how its hits land.
    /// </summary>
    public partial class VineNode
    {
        /// <summary>1 as built, 2 and 3 after the level upgrades.</summary>
        public int Level { get; private set; } = 1;
        /// <summary>The branch taken at level 3 (null until then).</summary>
        public TowerUpgradeStep Branch { get; private set; }
        /// <summary>Resources spent on upgrades (sold back with the tower).</summary>
        public int UpgradeSpent { get; private set; }

        public TowerUpgradePlan UpgradePlan => TowerUpgradePlan.For(Data?.Id);
        public int MaxLevel => 1 + (UpgradePlan?.Levels.Count ?? 0);

        /// <summary>The next level up, or null at the top (then the branches are offered). Under One
        /// Big Gun a gun with its branch keeps going: Overcharge levels, each dearer than the last.</summary>
        public TowerUpgradeStep NextLevel => UpgradePlan != null && Level - 1 < UpgradePlan.Levels.Count ? UpgradePlan.Levels[Level - 1] : OverchargeStep;

        /// <summary>One Big Gun: Overcharge levels bought past the branch.</summary>
        public int Overcharge { get; private set; }
        public const string OverchargeId = "overcharge";

        /// <summary>The next Overcharge level, or null when it isn't offered (no One Big Gun, not a gun, no branch yet).</summary>
        private TowerUpgradeStep OverchargeStep
        {
            get
            {
                if (Branch == null || MetaRun.GunCap <= 0 || Data == null || !MetaRun.CountsAsGun(Data.Type)) return null;
                int baseCost = Mathf.Max(40, Branch.Cost);
                return new TowerUpgradeStep
                {
                    Id = OverchargeId, Name = $"Overcharge {Overcharge + 1}",
                    Text = $"+{Constants.OVERCHARGE_DAMAGE * 100f:0}% damage and +{Constants.OVERCHARGE_RATE * 100f:0}% fire rate (One Big Gun: a gun keeps levelling, each step dearer)",
                    Cost = Mathf.RoundToInt(baseCost * Mathf.Pow(Constants.OVERCHARGE_COST_GROWTH, Overcharge + 1)),
                    Damage = Constants.OVERCHARGE_DAMAGE, Rate = Constants.OVERCHARGE_RATE,
                };
            }
        }
        /// <summary>The two branches, offered once both levels are bought and none is taken.</summary>
        public IReadOnlyList<TowerUpgradeStep> BranchChoices =>
            UpgradePlan != null && Level >= MaxLevel && Branch == null ? UpgradePlan.Branches : System.Array.Empty<TowerUpgradeStep>();

        private IEnumerable<TowerUpgradeStep> Taken()
        {
            var plan = UpgradePlan;
            if (plan == null) yield break;
            for (int i = 0; i < Level - 1 && i < plan.Levels.Count; i++) yield return plan.Levels[i];
            if (Branch != null) yield return Branch;
        }

        // What the steps taken add up to, kept until the level, branch or Overcharge changes: every
        // shot and every reach check summed them through an iterator and LINQ (a 300-tower field
        // allocated megabytes a second here, and the collector stalled frames for 150 ms)
        private int _sumLevel = -1, _sumOver = -1;
        private TowerUpgradeStep _sumBranch;
        private float _sDamage, _sRate, _sRange, _sSlow, _sBuff, _sForce, _sIncome;

        private void SumTaken()
        {
            if (_sumLevel == Level && _sumOver == Overcharge && ReferenceEquals(_sumBranch, Branch)) return;
            _sumLevel = Level; _sumOver = Overcharge; _sumBranch = Branch;
            _sDamage = _sRate = _sRange = _sSlow = _sBuff = _sForce = _sIncome = 0f;
            var plan = UpgradePlan;
            if (plan == null) return;
            for (int i = 0; i < Level - 1 && i < plan.Levels.Count; i++) Add(plan.Levels[i]);
            if (Branch != null) Add(Branch);
            void Add(TowerUpgradeStep s)
            {
                _sDamage += s.Damage; _sRate += s.Rate; _sRange += s.Range; _sSlow += s.Slow;
                _sBuff += s.Buff; _sForce += s.Force; _sIncome += s.Income;
            }
        }

        public float UpgradeDamageMult { get { SumTaken(); return Mathf.Max(0.1f, 1f + _sDamage + Overcharge * Constants.OVERCHARGE_DAMAGE); } }
        public float UpgradeRateMult { get { SumTaken(); return Mathf.Max(0.1f, 1f + _sRate + Overcharge * Constants.OVERCHARGE_RATE); } }
        public float UpgradeRangeMult { get { SumTaken(); return 1f + _sRange; } }
        public float UpgradeSlowBonus { get { SumTaken(); return _sSlow; } }
        public float UpgradeBuffMult { get { SumTaken(); return 1f + _sBuff; } }
        public float UpgradeForceMult { get { SumTaken(); return 1f + _sForce; } }
        public float UpgradeIncomeMult { get { SumTaken(); return 1f + _sIncome; } }

        /// <summary>A Junk Turret's damage a second as it stands, with every bonus (tests, panels).</summary>
        public float CurrentDps
        {
            get
            {
                if (Data == null) return 0f;
                float iv = DamageTowerBaseInterval;
                return GetEffectiveDamage(iv) / Mathf.Max(0.001f, GetEffectiveFireInterval(iv));
            }
        }

        /// <summary>Reach as it stands (0 for towers without one).</summary>
        public float CurrentRange => Data != null && Data.Range > 0 ? GetEffectiveRange() : 0f;

        /// <summary>A perk's behaviour for this tower: the run has the perk, or this tower's branch grants it.</summary>
        public bool Has(string perkId) => VinePerkRegistry.IsActive(perkId) || (Branch?.Grants?.Contains(perkId) ?? false);
        public bool HasBranch(string id) => Branch?.Id == id;

        /// <summary>Flak and Tesla reach flyers; others only with a branch that says so (Minigun).</summary>
        public bool CanHitAir => Data != null && (TypeHitsAir(Data.Type) || (Branch?.AntiAir ?? false));

        /// <summary>A tower of this type reaches flyers as built (before any branch).</summary>
        public static bool TypeHitsAir(VineNodeType t) => t is VineNodeType.FlakBattery or VineNodeType.TeslaCoil
            or VineNodeType.ArcPrism or VineNodeType.SignalJammer or VineNodeType.StormCapacitor or VineNodeType.Lens or VineNodeType.Beacon;

        /// <summary>A live enemy this tower can shoot: flyers only if it reaches the air.</summary>
        public bool CanTarget(VineEnemy ve) => ve != null && ve.IsAlive && (!ve.IsFlying || CanHitAir);

        /// <summary>How this tower's hits land on armour and shields.</summary>
        public DamageKind HitKind
        {
            get
            {
                if (Branch?.Kind != null && System.Enum.TryParse<DamageKind>(Branch.Kind, true, out var k)) return k;
                return Data?.Type switch
                {
                    VineNodeType.DamageTower or VineNodeType.ScatterCannon or VineNodeType.MortarPit or VineNodeType.RailLance
                        or VineNodeType.AutocannonNest => DamageKind.Heavy,
                    VineNodeType.FlakBattery => DamageKind.Light,
                    VineNodeType.TeslaCoil or VineNodeType.ArcPrism or VineNodeType.SignalJammer or VineNodeType.StormCapacitor => DamageKind.Electric,
                    _ => DamageKind.Normal,
                };
            }
        }

        /// <summary>
        /// What this tower brings to a fight, for the threat readout (ThreatModel): damage a
        /// second to one target, how many it can hit at once (splash, arcs, bursts), how its hits
        /// land, whether it reaches flyers, its reach and any slow it lays on the path.
        /// </summary>
        public struct Firepower
        {
            public float Dps, Targets, Range, Slow;
            public DamageKind Kind;
            public bool Air, Ground;
            /// <summary>Against flyers only (Skyguard doubles them).</summary>
            public float AirMult;
        }

        public Firepower GetFirepower()
        {
            var f = new Firepower { Kind = HitKind, Air = CanHitAir, Ground = true, Targets = 1f, AirMult = 1f, Range = CurrentRange };
            if (Data == null || IsDestroyed) return default;
            switch (Data.Type)
            {
                case VineNodeType.DamageTower:
                {
                    float iv = DamageTowerBaseInterval;
                    f.Dps = GetEffectiveDamage(iv) / Mathf.Max(0.01f, GetEffectiveFireInterval(iv));
                    if (Has("piercing_rail")) f.Targets = 1f + Constants.PERK_PIERCE_TARGETS * Constants.PERK_PIERCE_DAMAGE * 0.5f;
                    break;
                }
                case VineNodeType.ScatterCannon:
                {
                    float iv = Constants.SCATTER_CANNON_INTERVAL;
                    f.Dps = GetEffectiveDamage(iv) / Mathf.Max(0.01f, GetEffectiveFireInterval(iv));
                    f.Targets = Has("cluster_shells") ? 3.2f : 2.4f;
                    break;
                }
                case VineNodeType.TeslaCoil:
                {
                    float iv = Constants.TESLA_COIL_INTERVAL;
                    f.Dps = GetEffectiveDamage(iv) / Mathf.Max(0.01f, GetEffectiveFireInterval(iv));
                    if (HasBranch("overload")) f.Dps *= Constants.OVERLOAD_MULT;
                    bool arc = Has("arc_conductor");
                    int chains = Constants.TESLA_COIL_CHAIN_COUNT + (arc ? Constants.PERK_ARC_EXTRA_CHAINS : 0);
                    f.Targets = 1f + chains * (arc ? Constants.PERK_ARC_CHAIN_FACTOR : Constants.TESLA_COIL_CHAIN_FACTOR) * 0.7f;
                    break;
                }
                case VineNodeType.FlakBattery:
                {
                    float iv = Constants.FLAK_BATTERY_INTERVAL;
                    f.Dps = GetEffectiveDamage(iv) / Mathf.Max(0.01f, GetEffectiveFireInterval(iv));
                    f.Targets = Constants.FLAK_BATTERY_MAX_TARGETS + (Has("saturation_fire") ? Constants.PERK_FLAK_EXTRA_TARGETS : 0);
                    if (HasBranch("skyguard")) f.AirMult = Constants.SKYGUARD_AIR_MULT;
                    break;
                }
                case VineNodeType.SlowField:
                    f.Slow = Mathf.Clamp((Data.SlowAmount + SignalTuningEditor.SlowFieldAmount - Constants.SLOW_FIELD_AMOUNT + UpgradeSlowBonus)
                        * RoleRun.SlowMult * PowerType.SlowMult, 0f, 0.9f);
                    if (HasBranch("napalm")) { f.Dps = Constants.NAPALM_DPS * UpgradeDamageMult; f.Targets = 4f; f.Kind = DamageKind.Normal; }
                    break;
                case VineNodeType.PushPull:
                    // A shove sends enemies back over ground they already walked: like a slow
                    f.Slow = Mathf.Clamp(0.12f * UpgradeForceMult * RoleRun.ForceMult * PowerType.RamMult, 0f, 0.5f);
                    if (HasBranch("repulsor"))
                    {
                        f.Dps = Constants.REPULSOR_DAMAGE * UpgradeDamageMult * PowerType.RamMult / Mathf.Max(0.1f, GetEffectiveFireInterval(Constants.PUSH_PULL_INTERVAL));
                        f.Targets = 3f; f.Kind = DamageKind.Heavy;
                    }
                    break;
                // ── Class towers ──
                case VineNodeType.MortarPit:
                {
                    float iv = ClassTowers.MORTAR_INTERVAL;
                    f.Dps = GetEffectiveDamage(iv) / Mathf.Max(0.01f, GetEffectiveFireInterval(iv)) * 0.8f; // the shell leads, and misses some
                    f.Targets = HasBranch("cluster_bombs") ? 3f : 2.4f;
                    if (HasBranch("incendiary")) f.Dps += ClassTowers.SLAG_DPS * 0.8f * UpgradeDamageMult * 0.5f;
                    break;
                }
                case VineNodeType.RailLance:
                {
                    float iv = ClassTowers.RAIL_INTERVAL;
                    float heavy = HasBranch("heavy_slug") ? 2f / 1.4f : 1f;
                    f.Dps = GetEffectiveDamage(iv) / Mathf.Max(0.01f, GetEffectiveFireInterval(iv)) * heavy;
                    f.Targets = HasBranch("twin_rail") ? 2.4f : 1.8f;
                    break;
                }
                case VineNodeType.SlagChute:
                {
                    // A pool every few seconds that burns what crosses it, a few at a time
                    float share = ClassTowers.SLAG_SECONDS * (HasBranch("magma") ? 2f : 1f) / Mathf.Max(0.1f, GetEffectiveFireInterval(ClassTowers.SLAG_INTERVAL));
                    f.Dps = Data.Damage * UpgradeDamageMult * Mathf.Min(1f, share);
                    f.Targets = 2.5f;
                    f.Slow = ClassTowers.SLAG_SLOW + (HasBranch("clinker") ? 0.3f : 0f);
                    break;
                }
                case VineNodeType.AutocannonNest:
                {
                    float iv = ClassTowers.NEST_INTERVAL;
                    f.Dps = GetEffectiveDamage(iv) / Mathf.Max(0.01f, GetEffectiveFireInterval(iv));
                    f.Targets = 1.6f;
                    break;
                }
                case VineNodeType.ArcPrism:
                {
                    // An echo of the coils near it: about one coil's worth for every coil in earshot
                    float coils = 0f;
                    foreach (var n in Roster.Nodes(GetTree()))
                        if (n?.Data?.Type == VineNodeType.TeslaCoil && !n.IsDestroyed && Flat2(n.At, At) <= ClassTowers.PRISM_LISTEN * ClassTowers.PRISM_LISTEN)
                            coils += n.GetEffectiveDamage(Constants.TESLA_COIL_INTERVAL) / Mathf.Max(0.01f, n.GetEffectiveFireInterval(Constants.TESLA_COIL_INTERVAL));
                    f.Dps = coils * ClassTowers.PRISM_SHARE * UpgradeDamageMult * (HasBranch("focus") ? 2.1f : 1f);
                    f.Targets = HasBranch("focus") ? 1f : ClassTowers.PRISM_ARCS + (HasBranch("spectrum") ? 2 : 0);
                    break;
                }
                case VineNodeType.SignalJammer:
                    f.Dps = GetEffectiveDamage(ClassTowers.JAMMER_TICK) / ClassTowers.JAMMER_TICK + ClassTowers.JAMMER_DRAIN * UpgradeDamageMult * 0.5f;
                    f.Targets = 4f;
                    f.Slow = Mathf.Clamp(ClassTowers.JAMMER_SLOW + UpgradeSlowBonus, 0f, 0.8f);
                    break;
                case VineNodeType.StormCapacitor:
                {
                    // One discharge about every CAP_MIN seconds of charge in a busy fight
                    float rate = 1f / Mathf.Max(0.1f, GetEffectiveFireInterval(1f)) * (HasBranch("bank") ? 1.3f : 1f);
                    f.Dps = GetEffectiveDamage(1f) * rate;
                    f.Targets = 1.8f + (HasBranch("chain") ? 1.2f : 0f);
                    break;
                }
                case VineNodeType.Lens:
                {
                    float tick = ClassTowers.LENS_TICK;
                    float crew = 1f + Mathf.Min(CrewLinks, Constants.CREW_MAX_LINKS) * ClassTowers.LENS_PER_LINK;
                    // Held on one target it ramps up: count it at half way
                    float ramp = 1f + (ClassTowers.LENS_RAMP_MAX - 1f + (HasBranch("burn") ? 1f : 0f)) * 0.5f;
                    f.Dps = GetEffectiveDamage(tick) / Mathf.Max(0.001f, GetEffectiveFireInterval(tick)) * crew * ramp;
                    f.Targets = HasBranch("split") ? 1.6f : 1f;
                    break;
                }
                case VineNodeType.GravityWell:
                    // A knot for the guns: counted like a slow (they walk back over ground already walked)
                    f.Slow = Mathf.Clamp(0.15f * UpgradeForceMult * RoleRun.ForceMult * (HasBranch("singularity") ? 1.6f : 1f), 0f, 0.5f);
                    break;
                case VineNodeType.Beacon:
                    f.Range = CurrentRange;
                    break;
                default:
                    f.Range = 0f;
                    break;
            }
            // Molten Rounds: the burn a hit leaves (a share of the tower's damage a second for a few
            // seconds; one burn on an enemy at a time, so not quite all of it). The card left it out,
            // so a bot with the relic kept buying the Tesla and Flak it weakens and fell 11 waves early
            if (RelicRun.Ignites(Data.Type)) f.Dps *= 1f + 0.8f * RelicRun.T(RelicRun.MoltenRounds, "burnShare", 0.45f);
            return f;
        }

        /// <summary>What selling gives back: the tower and its upgrades at the sell rate (a wreck, WRECK_SELL_SHARE of that).</summary>
        public int SellValue => Mathf.RoundToInt(Invested * SellRate * (IsWrecked ? Constants.WRECK_SELL_SHARE : 1f));

        /// <summary>Everything paid for this tower: placing it and its upgrades.</summary>
        public int Invested => PlacePrice + UpgradeSpent;

        /// <summary>What was paid to place it (free towers from the perk tree paid nothing).</summary>
        public int PaidToPlace { get; set; } = -1;
        private int PlacePrice => PaidToPlace >= 0 ? PaidToPlace : (Data?.ResourceCost ?? 0);

        /// <summary>The sell rate now: everything back between waves with the Free Rebuild perk.</summary>
        public static float SellRate => MetaRun.FreeRebuild && GameManager.Instance?.CurrentPhase is GamePhase.Build or GamePhase.WaveComplete
            ? 1f : SignalTuningEditor.SellRefund;

        /// <summary>What a level or branch costs this run (Surplus Parts makes them cheaper).</summary>
        public static int PriceOf(TowerUpgradeStep step) => step == null ? 0 : MetaRun.UpgradeCost(step.Cost);

        /// <summary>Veteran Crews (perk tree): the first level comes free with the tower.</summary>
        public void GrantFreeLevel()
        {
            var next = NextLevel;
            if (next == null || Level > 1) return;
            Level++;
            ApplyUpgradeStats(next);
            ShowUpgradeLook();
            GameEvents.OnTowerUpgraded?.Invoke(this);
        }

        /// <summary>Why the next level can't be bought now (null: it can).</summary>
        public string CantUpgrade()
        {
            if (IsWrecked) return "Wrecked: repair it first";
            var next = NextLevel;
            if (next == null) return "Top level";
            if ((GameManager.Instance?.CurrentResources ?? 0) < PriceOf(next)) return $"Needs {PriceOf(next)} Resources";
            return null;
        }

        public bool TryUpgrade()
        {
            var next = NextLevel;
            if (next == null || CantUpgrade() != null) return false;
            int price = PriceOf(next);
            if (GameManager.Instance != null && !GameManager.Instance.SpendResources(price)) return false;
            UpgradeSpent += price;
            if (next.Id == OverchargeId) Overcharge++;
            else { Level++; ApplyUpgradeStats(next); }
            ShowUpgradeLook();
            GD.Print($"[Upgrades] {Data.Name} at ({GridPosition.X},{GridPosition.Y}) -> {(next.Id == OverchargeId ? next.Name : $"level {Level}")} for {price}");
            GameEvents.OnTowerUpgraded?.Invoke(this);
            return true;
        }

        public string CantBranch(string id)
        {
            if (IsWrecked) return "Wrecked: repair it first";
            var b = BranchChoices.FirstOrDefault(x => x.Id == id);
            if (b == null) return Branch != null ? "Branch already chosen" : "Reach the top level first";
            if ((GameManager.Instance?.CurrentResources ?? 0) < PriceOf(b)) return $"Needs {PriceOf(b)} Resources";
            return null;
        }

        public bool TryBranch(string id)
        {
            var b = BranchChoices.FirstOrDefault(x => x.Id == id);
            if (b == null || CantBranch(id) != null) return false;
            int price = PriceOf(b);
            if (GameManager.Instance != null && !GameManager.Instance.SpendResources(price)) return false;
            UpgradeSpent += price;
            Branch = b;
            ApplyUpgradeStats(b);
            ShowUpgradeLook();
            foreach (var g in b.Grants ?? System.Array.Empty<string>()) _look?.ShowPerk(g, animate: true);
            GD.Print($"[Upgrades] {Data.Name} at ({GridPosition.X},{GridPosition.Y}) -> {b.Name} for {price}");
            GameEvents.OnTowerUpgraded?.Invoke(this);
            return true;
        }

        /// <summary>Stats that live on the node rather than being read per shot (wall HP).</summary>
        private void ApplyUpgradeStats(TowerUpgradeStep step)
        {
            if (step.Hp > 0f && _hasHealth && NodeMaxHealth > 0f)
            {
                float baseHp = NodeMaxHealth / (1f + Taken().Where(s => s != step).Sum(s => s.Hp));
                float add = baseHp * step.Hp;
                NodeMaxHealth += add;
                NodeCurrentHealth += add;
            }
        }

        // ── Looks: a lit band on the base per level, a brighter one for the branch ──

        private readonly List<Node3D> _levelBands = new();

        private void ShowUpgradeLook()
        {
            foreach (var b in _levelBands) if (IsInstanceValid(b)) b.QueueFree();
            _levelBands.Clear();
            int bands = Level - 1 + (Branch != null ? 1 : 0);
            var color = Branch != null ? new Color(1f, 0.78f, 0.25f) : new Color(0.35f, 0.8f, 1f);
            for (int i = 0; i < bands; i++)
            {
                bool top = Branch != null && i == bands - 1;
                var ring = new MeshInstance3D
                {
                    Name = $"LevelBand{i}",
                    Mesh = new TorusMesh { InnerRadius = 0.66f + i * 0.07f, OuterRadius = 0.7f + i * 0.07f, Rings = 24, RingSegments = 6 },
                    MaterialOverride = VfxCache.Glow(top ? color : new Color(0.35f, 0.8f, 1f), top ? color : new Color(0.3f, 0.7f, 1f), top ? 2.2f : 1.4f, alpha: false),
                    Position = new Vector3(0, 0.04f - Constants.NODE_ORIGIN_HEIGHT + 0.02f * i, 0),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(ring);
                _levelBands.Add(ring);
            }
            // A pop so the upgrade is seen
            VfxFactory.SpawnBuffMotes(GetTree(), GlobalPosition, color);
        }

        /// <summary>The tower's numbers as they stand, for its panel.</summary>
        internal string StatLine()
        {
            if (Data == null) return "";
            float cells(float r) => r / Constants.VINE_CELL_SIZE;
            string range = Data.Range > 0 ? $"  ·  range {cells(GetEffectiveRange()):0.#} cells" : "";
            string air = Data.AutoFires && Data.Type is not (VineNodeType.BuffEmitter or VineNodeType.BarrierWall)
                ? (CanHitAir ? "  ·  hits flyers" : "  ·  ground only") : "";
            switch (Data.Type)
            {
                case VineNodeType.DamageTower:
                {
                    float shot = GetEffectiveDamage(DamageTowerBaseInterval), iv = GetEffectiveFireInterval(DamageTowerBaseInterval);
                    return $"{shot / iv:0} damage a second ({shot:0.#} a shot){range}{air}";
                }
                case VineNodeType.ScatterCannon:
                {
                    float hit = GetEffectiveDamage(Constants.SCATTER_CANNON_INTERVAL), iv = GetEffectiveFireInterval(Constants.SCATTER_CANNON_INTERVAL);
                    return $"{hit:0} to everything in the blast every {iv:0.#} s{range}{air}";
                }
                case VineNodeType.TeslaCoil:
                {
                    float hit = GetEffectiveDamage(Constants.TESLA_COIL_INTERVAL), iv = GetEffectiveFireInterval(Constants.TESLA_COIL_INTERVAL);
                    int chains = Constants.TESLA_COIL_CHAIN_COUNT + (Has("arc_conductor") ? Constants.PERK_ARC_EXTRA_CHAINS : 0);
                    return $"{hit * (HasBranch("overload") ? Constants.OVERLOAD_MULT : 1f):0} then arcs to {chains} more, every {iv:0.#} s{range}{air}";
                }
                case VineNodeType.FlakBattery:
                {
                    float hit = GetEffectiveDamage(Constants.FLAK_BATTERY_INTERVAL), iv = GetEffectiveFireInterval(Constants.FLAK_BATTERY_INTERVAL);
                    int targets = Constants.FLAK_BATTERY_MAX_TARGETS + (Has("saturation_fire") ? Constants.PERK_FLAK_EXTRA_TARGETS : 0);
                    return $"{hit / iv:0} damage a second to each of {targets}{range}{air}";
                }
                case VineNodeType.SlowField:
                {
                    float slow = Mathf.Clamp(Data.SlowAmount + SignalTuningEditor.SlowFieldAmount - Constants.SLOW_FIELD_AMOUNT + UpgradeSlowBonus, 0f, 0.9f);
                    return $"{slow * 100:0}% slow{(HasBranch("napalm") ? $", burns {Constants.NAPALM_DPS * UpgradeDamageMult:0}/s" : "")}{range}{air}";
                }
                case VineNodeType.PushPull:
                    return $"Shove every {GetEffectiveFireInterval(Constants.PUSH_PULL_INTERVAL):0.#} s, {UpgradeForceMult * 100:0}% force{range}{air}";
                case VineNodeType.BuffEmitter:
                    return $"Boost {Constants.BUFF_EMITTER_STRENGTH * UpgradeBuffMult * SignalTuningEditor.BuffDamageBonus * 100:0}% damage and fire rate to towers {(Has("relay_mesh") ? "up to two cells away" : "next door")}";
                case VineNodeType.BarrierWall:
                    return $"{NodeCurrentHealth:0}/{NodeMaxHealth:0} HP";
                case VineNodeType.MortarPit:
                {
                    float hit = GetEffectiveDamage(ClassTowers.MORTAR_INTERVAL), iv = GetEffectiveFireInterval(ClassTowers.MORTAR_INTERVAL);
                    return $"{hit:0} to everything in the blast every {iv:0.#} s, nothing closer than {ClassTowers.MORTAR_MIN_RANGE / Constants.VINE_CELL_SIZE:0} cells{range}{air}";
                }
                case VineNodeType.RailLance:
                {
                    float hit = GetEffectiveDamage(ClassTowers.RAIL_INTERVAL) * (HasBranch("heavy_slug") ? 2f : 1f);
                    float iv = GetEffectiveFireInterval(ClassTowers.RAIL_INTERVAL) * (HasBranch("heavy_slug") ? 1.4f : 1f);
                    return $"{hit:0} to every enemy on the line every {iv:0.#} s{range}{air}";
                }
                case VineNodeType.SlagChute:
                    return $"A pool that burns {Data.Damage * UpgradeDamageMult:0}/s for {ClassTowers.SLAG_SECONDS * (HasBranch("magma") ? 2f : 1f):0} s every {GetEffectiveFireInterval(ClassTowers.SLAG_INTERVAL):0.#} s{range}{air}";
                case VineNodeType.AutocannonNest:
                {
                    float shot = GetEffectiveDamage(ClassTowers.NEST_INTERVAL), iv = GetEffectiveFireInterval(ClassTowers.NEST_INTERVAL);
                    return $"{shot / iv:0} damage a second over two targets ({shot:0.#} a shot){range}{air}";
                }
                case VineNodeType.ArcPrism:
                    return $"Passes on {ClassTowers.PRISM_SHARE * UpgradeDamageMult * 100:0}% of each Tesla arc within {ClassTowers.PRISM_LISTEN / Constants.VINE_CELL_SIZE:0.#} cells to {(HasBranch("focus") ? 1 : ClassTowers.PRISM_ARCS + (HasBranch("spectrum") ? 2 : 0))} more{range}{air}";
                case VineNodeType.SignalJammer:
                    return $"{Mathf.Clamp(ClassTowers.JAMMER_SLOW + UpgradeSlowBonus, 0f, 0.8f) * 100:0}% slow, -{ClassTowers.JAMMER_DRAIN * UpgradeDamageMult:0} shield and {GetEffectiveDamage(1f):0.#} damage a second in the field{range}{air}";
                case VineNodeType.StormCapacitor:
                    return $"Charge {Charge:0.#}/{ClassTowers.CAP_MAX * (HasBranch("bank") ? 1.75f : 1f):0}: {GetEffectiveDamage(1f):0} a second of charge in one blast{range}{air}";
                case VineNodeType.Lens:
                    return $"{GetEffectiveDamage(1f) * (1f + Mathf.Min(CrewLinks, Constants.CREW_MAX_LINKS) * ClassTowers.LENS_PER_LINK):0} damage a second, up to x{ClassTowers.LENS_RAMP_MAX + (HasBranch("burn") ? 1f : 0f):0} held on one target{range}{air}";
                case VineNodeType.GravityWell:
                    return $"Pulls everything in reach together every {GetEffectiveFireInterval(ClassTowers.WELL_INTERVAL * (HasBranch("singularity") ? 0.6f : 1f)):0.#} s{range}{air}";
                case VineNodeType.Beacon:
                    return $"Marks the {(HasBranch("spotter") ? 4 : ClassTowers.BEACON_MARKS)} toughest in reach: +{ClassTowers.BEACON_TAKEN * UpgradeBuffMult * 100:0}% damage taken (+{ClassTowers.BEACON_BIT_TAKEN * UpgradeBuffMult * 100:0}% from BIT){range}{air}";
                case VineNodeType.SalvageDrill:
                {
                    int wave = Mathf.Max(1, (GameManager.Instance?.CurrentWave ?? 0) + 1);
                    return $"Pays {SalvageDrill.PayoutOf(this, wave)} for wave {wave} (+{UpgradeIncomeMult * 100 - 100:0}% from upgrades)  ·  {NodeCurrentHealth:0}/{NodeMaxHealth:0} HP";
                }
                default:
                    return Data.Description;
            }
        }

        /// <summary>What one more level adds, in words.</summary>
        internal static string StepText(TowerUpgradeStep s)
        {
            var parts = new List<string>();
            void Add(float v, string what) { if (v != 0f) parts.Add($"{(v > 0 ? "+" : "")}{v * 100:0}% {what}"); }
            Add(s.Damage, "damage");
            Add(s.Rate, "fire rate");
            Add(s.Range, "range");
            Add(s.Hp, "HP");
            Add(s.Slow, "slow");
            Add(s.Buff, "boost");
            Add(s.Force, "force");
            Add(s.Income, "pay");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// A tower placed from a suit's kit: it arrives at the kit's level and branch, paid for
        /// by nothing (so selling it gives nothing back).
        /// </summary>
        public void ApplyKit(int level, string branch)
        {
            if (level <= 1 && branch == null) return;
            ForceUpgrade(level, branch);
            GameEvents.OnTowerUpgraded?.Invoke(this);
        }

        /// <summary>Tests: set level and branch without paying.</summary>
        internal void ForceUpgrade(int level, string branch = null)
        {
            while (Level < Mathf.Min(level, MaxLevel)) { Level++; ApplyUpgradeStats(UpgradePlan.Levels[Level - 2]); }
            if (branch != null && Level >= MaxLevel)
            {
                Branch = UpgradePlan.Branches.FirstOrDefault(b => b.Id == branch);
                if (Branch != null) { ApplyUpgradeStats(Branch); foreach (var g in Branch.Grants ?? System.Array.Empty<string>()) _look?.ShowPerk(g, animate: false); }
            }
            ShowUpgradeLook();
        }
    }
}
