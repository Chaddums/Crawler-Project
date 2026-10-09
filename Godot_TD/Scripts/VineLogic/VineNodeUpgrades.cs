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

        /// <summary>The next level up, or null at the top (then the branches are offered).</summary>
        public TowerUpgradeStep NextLevel => UpgradePlan != null && Level - 1 < UpgradePlan.Levels.Count ? UpgradePlan.Levels[Level - 1] : null;
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

        public float UpgradeDamageMult => Mathf.Max(0.1f, 1f + Taken().Sum(s => s.Damage));
        public float UpgradeRateMult => Mathf.Max(0.1f, 1f + Taken().Sum(s => s.Rate));
        public float UpgradeRangeMult => 1f + Taken().Sum(s => s.Range);
        public float UpgradeSlowBonus => Taken().Sum(s => s.Slow);
        public float UpgradeBuffMult => 1f + Taken().Sum(s => s.Buff);
        public float UpgradeForceMult => 1f + Taken().Sum(s => s.Force);

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
        public bool CanHitAir => Data != null && (Data.Type is VineNodeType.FlakBattery or VineNodeType.TeslaCoil || (Branch?.AntiAir ?? false));

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
                    VineNodeType.DamageTower or VineNodeType.ScatterCannon => DamageKind.Heavy,
                    VineNodeType.FlakBattery => DamageKind.Light,
                    VineNodeType.TeslaCoil => DamageKind.Electric,
                    _ => DamageKind.Normal,
                };
            }
        }

        /// <summary>What selling gives back: the tower and its upgrades at the sell rate.</summary>
        public int SellValue => Mathf.RoundToInt((PlacePrice + UpgradeSpent) * SellRate);

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
            Level++;
            ApplyUpgradeStats(next);
            ShowUpgradeLook();
            GD.Print($"[Upgrades] {Data.Name} at ({GridPosition.X},{GridPosition.Y}) -> level {Level} for {price}");
            GameEvents.OnTowerUpgraded?.Invoke(this);
            return true;
        }

        public string CantBranch(string id)
        {
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
            return string.Join(", ", parts);
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
