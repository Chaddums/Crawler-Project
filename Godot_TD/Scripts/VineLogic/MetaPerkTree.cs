using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    public enum MetaPerkLane { Network, Player, Harvester }

    /// <summary>One perk on the permanent tree (Data/perk_tree.json).</summary>
    public class MetaPerkNode
    {
        public string Id;
        public string Name;
        /// <summary>Per-rank text: {v} and {v2} are the value at the next (or current) rank.</summary>
        public string DescTemplate;
        public MetaPerkLane Lane;
        /// <summary>Points spent in its lane before it opens.</summary>
        public int Needs;
        public int MaxRank = 1;
        public int Cost = 1;
        public bool IsKeystone;
        /// <summary>Takes points without end (a lane's Mastery); left out of the tree's total.</summary>
        public bool Repeatable;
        public string Effect;
        public float Value;
        public float Value2;
        public float Value3;
        /// <summary>Pick-one group: only one perk with the same choice can be owned (the doctrine and capstone rows).</summary>
        public string Choice;

        /// <summary>What it does at <paramref name="rank"/> ranks (at least 1).</summary>
        public string Describe(int rank)
        {
            int r = Math.Max(1, rank);
            return DescTemplate
                .Replace("{v+1}", Fmt(Value * r + 1))
                .Replace("{v2}", Fmt(Value2 * r))
                .Replace("{v3}", Fmt(Value3 * r))
                .Replace("{v}", Fmt(Value * r));
        }

        private static string Fmt(float f) => Mathf.IsEqualApprox(f, Mathf.Round(f))
            ? ((int)Mathf.Round(f)).ToString(CultureInfo.InvariantCulture)
            : f.ToString("0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The permanent perk tree. Three lanes, each a ladder: a perk opens once enough points are
    /// spent in its lane, ranked perks take a point a rank, and keystones change how a run plays
    /// (free towers, a full refund between waves, towers built pre-upgraded, BIT starting a level
    /// up, a second life, double bounties, interest, a fourth perk card). Every point can be spent
    /// somewhere until the whole tree is bought.
    /// (Until 2026-10 it was 25 flat stat bumps, one per tier, with 8 tiers: after 8 points every
    /// further point was stuck.)
    /// </summary>
    public static class MetaPerkRegistry
    {
        private const string DataPath = "res://Data/perk_tree.json";
        private static List<MetaPerkNode> _nodes;
        private static readonly List<(MetaPerkLane lane, string name, string blurb)> _lanes = new();

        public static int PointsPerWaves { get; private set; } = 5;
        public static int AscendantKillPoints { get; private set; } = 1;

        public static List<MetaPerkNode> GetAll()
        {
            if (_nodes == null) Load();
            return _nodes;
        }

        public static IReadOnlyList<(MetaPerkLane lane, string name, string blurb)> Lanes
        {
            get { if (_nodes == null) Load(); return _lanes; }
        }

        public static MetaPerkNode Get(string id) => GetAll().FirstOrDefault(n => n.Id == id);

        /// <summary>Every point the whole tree takes (not counting the repeatable Mastery cards).</summary>
        public static int TotalCost => GetAll().Where(n => !n.Repeatable && n.Choice == null).Sum(n => n.Cost * n.MaxRank)
            + GetAll().Where(n => !n.Repeatable && n.Choice != null).GroupBy(n => n.Choice).Sum(g => g.Max(n => n.Cost * n.MaxRank));

        /// <summary>The perk already taken from this one's pick-one group (null: none, or not a choice).</summary>
        public static MetaPerkNode ChosenInGroup(MetaPerkNode node, MetaPerkSaveData save)
            => node?.Choice == null ? null : GetAll().FirstOrDefault(n => n != node && n.Choice == node.Choice && RankOf(save, n.Id) > 0);

        /// <summary>Points spent on the tree's fixed perks (not Mastery).</summary>
        public static int SpentFixed(MetaPerkSaveData save) => GetAll().Where(n => !n.Repeatable).Sum(n => RankOf(save, n.Id) * n.Cost);

        public static int RankOf(MetaPerkSaveData save, string id)
            => save?.Ranks != null && save.Ranks.TryGetValue(id, out int r) ? r : 0;

        /// <summary>Points spent in a lane.</summary>
        public static int LaneSpent(MetaPerkSaveData save, MetaPerkLane lane)
            => GetAll().Where(n => n.Lane == lane).Sum(n => RankOf(save, n.Id) * n.Cost);

        /// <summary>Points spent in a lane on perks that open before <paramref name="needs"/> (the rows above).</summary>
        public static int LowerSpent(MetaPerkSaveData save, MetaPerkLane lane, int needs)
            => GetAll().Where(n => n.Lane == lane && n.Needs < needs).Sum(n => RankOf(save, n.Id) * n.Cost);

        /// <summary>Points spent on the whole tree.</summary>
        public static int Spent(MetaPerkSaveData save) => GetAll().Sum(n => RankOf(save, n.Id) * n.Cost);

        /// <summary>Why the next rank can't be bought (null: it can).</summary>
        public static string WhyNot(MetaPerkNode node, MetaPerkSaveData save)
        {
            if (node == null || save == null) return "Unknown perk";
            int rank = RankOf(save, node.Id);
            if (rank >= node.MaxRank) return node.MaxRank > 1 ? "All ranks taken" : "Owned";
            if (ChosenInGroup(node, save) is { } other) return $"You took {other.Name}: pick one of the two (right-click it to swap)";
            int lane = LowerSpent(save, node.Lane, node.Needs);
            if (lane < node.Needs) return $"Spend {node.Needs - lane} more point{(node.Needs - lane == 1 ? "" : "s")} higher up this lane first";
            if (save.AvailablePoints < node.Cost) return node.Cost == 1 ? "Needs a perk point" : $"Needs {node.Cost} perk points";
            return null;
        }

        /// <summary>Open in its lane (enough spent there), whether or not there are points to pay.</summary>
        public static bool IsOpen(MetaPerkNode node, MetaPerkSaveData save)
            => node != null && LowerSpent(save, node.Lane, node.Needs) >= node.Needs;

        /// <summary>Why a rank can't be taken back (null: it can). Perks further down still need the points above them.</summary>
        public static string WhyNotRefund(MetaPerkNode node, MetaPerkSaveData save)
        {
            if (node == null || RankOf(save, node.Id) <= 0) return "Nothing to take back";
            save.Ranks[node.Id]--;
            var stranded = GetAll().FirstOrDefault(n => n.Lane == node.Lane && RankOf(save, n.Id) > 0
                && LowerSpent(save, n.Lane, n.Needs) < n.Needs);
            save.Ranks[node.Id]++;
            return stranded == null ? null : $"{stranded.Name} needs these points";
        }

        public static bool TryRefund(string id, MetaPerkSaveData save)
        {
            var node = Get(id);
            if (WhyNotRefund(node, save) != null) return false;
            save.Ranks[id]--;
            if (save.Ranks[id] <= 0) save.Ranks.Remove(id);
            save.AvailablePoints += node.Cost;
            return true;
        }

        public static bool TryBuy(string id, MetaPerkSaveData save)
        {
            var node = Get(id);
            if (WhyNot(node, save) != null) return false;
            save.Ranks[id] = RankOf(save, id) + 1;
            save.AvailablePoints -= node.Cost;
            return true;
        }

        /// <summary>
        /// Give back ranks in rows that aren't open any more (the tree changed under a save), so
        /// nothing owned sits in a locked row. Returns the points refunded.
        /// </summary>
        public static int RefundStranded(MetaPerkSaveData save)
        {
            if (save?.Ranks == null) return 0;
            int refund = 0;
            for (bool again = true; again;)
            {
                again = false;
                // Deepest rows first, so a refund higher up can't strand one below after the fact
                foreach (var n in GetAll().OrderByDescending(n => n.Needs))
                {
                    int r = RankOf(save, n.Id);
                    if (r <= 0 || LowerSpent(save, n.Lane, n.Needs) >= n.Needs) continue;
                    save.Ranks.Remove(n.Id);
                    refund += r * n.Cost;
                    again = true;
                }
                // Two of one pick-one group (an older save): keep the first
                foreach (var g in GetAll().Where(n => n.Choice != null).GroupBy(n => n.Choice))
                    foreach (var extra in g.Where(n => RankOf(save, n.Id) > 0).Skip(1).ToList())
                    {
                        refund += RankOf(save, extra.Id) * extra.Cost;
                        save.Ranks.Remove(extra.Id);
                        again = true;
                    }
            }
            save.AvailablePoints += refund;
            return refund;
        }

        /// <summary>Refund everything; returns the points given back.</summary>
        public static int Reset(MetaPerkSaveData save)
        {
            int refund = Spent(save);
            save.Ranks.Clear();
            save.AvailablePoints += refund;
            return refund;
        }

        /// <summary>Set this run's modifiers from the tree (after SignalTuningEditor.ResetToDefaults).</summary>
        public static void Apply(MetaPerkSaveData save)
        {
            MetaRun.Reset();
            if (save == null) return;
            // AXIS answers a stronger BIT (Climb.Threat)
            MetaRun.PointsSpent = Spent(save);
            foreach (var node in GetAll())
            {
                int r = RankOf(save, node.Id);
                if (r <= 0) continue;
                float v = node.Value * r, v2 = node.Value2 * r;
                switch (node.Effect)
                {
                    case "tower_damage": SignalTuningEditor.DamageTowerDPS *= 1f + v / 100f; break;
                    case "tower_range": SignalTuningEditor.DamageTowerRange += v; break;
                    case "wall_masonry": MetaRun.WallCostCut += Mathf.RoundToInt(v); MetaRun.WallHpMult += v2 / 100f; break;
                    case "free_towers": MetaRun.FreeTowers += Mathf.RoundToInt(v); break;
                    case "upgrade_discount": MetaRun.UpgradeCostMult *= 1f - v / 100f; break;
                    case "free_rebuild": MetaRun.FreeRebuild = true; break;
                    case "veteran_towers": MetaRun.VeteranTowers = true; break;
                    case "bit_hp": SignalTuningEditor.PlayerMaxHPBonus += v; break;
                    case "bit_attack_speed": SignalTuningEditor.PlayerAttackSpeedMult *= 1f + v / 100f; break;
                    case "bit_damage": SignalTuningEditor.PlayerAttackDamageMult *= 1f + v / 100f; break;
                    case "bit_start_level": MetaRun.BitStartLevel += Mathf.RoundToInt(v); break;
                    case "ability_cost": MetaRun.AbilityCostMult *= 1f - v / 100f; break;
                    case "bit_materials":
                        SignalTuningEditor.PlayerMaxMaterialsBonus += v;
                        SignalTuningEditor.PlayerMaterialsRegenMult *= 1f + v2 / 100f;
                        break;
                    case "second_wind": MetaRun.SecondWind = true; break;
                    case "starting_resources": SignalTuningEditor.StartingScrap += Mathf.RoundToInt(v); break;
                    case "wave_bonus": SignalTuningEditor.WaveBonus += Mathf.RoundToInt(v); break;
                    case "spire_income": SignalTuningEditor.HarvesterIncomeBonus += Mathf.RoundToInt(v); break;
                    case "bounty": MetaRun.BountyMult = 1f + v; break;
                    case "compound_interest": MetaRun.InterestPct = node.Value; MetaRun.InterestCap = Mathf.RoundToInt(node.Value2); break;
                    case "spire_hp": MetaRun.SpireHpMult += v / 100f; break;
                    case "perk_choices": MetaRun.PerkChoices += Mathf.RoundToInt(v); break;
                    // The rows added once the first tree was bought out (2026-10)
                    case "crew_bonus": MetaRun.CrewBonusAdd += v / 100f; break;
                    case "dome_bonus": MetaRun.DomeBonusAdd += v / 100f; break;
                    case "tower_hp": MetaRun.TowerHpMult += v / 100f; break;
                    case "veteran_kills": MetaRun.VeteranKillMult = Mathf.Max(1, Mathf.RoundToInt(node.Value)); break;
                    case "bit_regen": MetaRun.BitRegenMult += v / 100f; break;
                    case "bit_move": MetaRun.BitMoveMult += v / 100f; break;
                    case "ability_cooldown": MetaRun.AbilityCooldownMult *= 1f - v / 100f; break;
                    case "bit_kill_bounty": MetaRun.BitKillBounty = 1f + node.Value / 100f; break;
                    case "strike_cost": MetaRun.StrikeCostMult *= 1f - v / 100f; break;
                    case "start_guns": MetaRun.StartGuns += Mathf.RoundToInt(v); break;
                    case "drop_bonus": MetaRun.DropMult += v / 100f; break;
                    case "start_strikes": MetaRun.StartStrikes = true; break;
                    case "bit_mastery": SignalTuningEditor.PlayerAttackDamageMult *= 1f + v / 100f; SignalTuningEditor.PlayerMaxHPBonus += v2; break;
                    case "spire_mastery": MetaRun.SpireHpMult += v / 100f; SignalTuningEditor.WaveBonus += Mathf.RoundToInt(v2); break;
                    // Pick-one keystones (2026-10): each bends a rule of the run
                    case "fortress": MetaRun.TowerHpFactor *= node.Value; MetaRun.NoTowerRepair = true; break;
                    case "swarm":
                        MetaRun.TowerCostMult *= node.Value / 100f; MetaRun.TowerHpFactor *= node.Value / 100f;
                        MetaRun.TowerDamageFactor *= 1f - node.Value2 / 100f;
                        // Half a tower counts as half in the supply price, or twice as many at half the
                        // price cost the same in the end (a full tree with it fell 12 waves early)
                        MetaRun.SupplyStepMult *= node.Value / 100f; break;
                    case "big_guns":
                        MetaRun.GunCap = Mathf.RoundToInt(node.Value); MetaRun.TowerDamageFactor *= node.Value2;
                        MetaRun.TowerRangeFactor *= 1f + node.Value3 / 100f; break;
                    case "chain_reaction":
                        MetaRun.ChainShare = node.Value / 100f; MetaRun.TowerDamageFactor *= 1f - node.Value2 / 100f;
                        MetaRun.ChainRadius = node.Value3; break;
                    case "blood_rites": MetaRun.BloodRites = true; MetaRun.AbilityPowerMult *= 1f + node.Value / 100f; break;
                    case "glass_core": MetaRun.BitHpFactor *= node.Value / 100f; MetaRun.BitDamageFactor *= 1f + node.Value2 / 100f; break;
                    case "juggernaut":
                        MetaRun.BitDamageTaken *= 1f - node.Value / 100f; MetaRun.BitDamageFactor *= 1f + node.Value2 / 100f;
                        MetaRun.BitMoveMult *= 1f - node.Value3 / 100f; MetaRun.NoSprint = true; break;
                    case "echo": MetaRun.EchoShare = node.Value / 100f; MetaRun.AbilityCooldownMult *= 1f + node.Value2 / 100f; break;
                    case "austerity": MetaRun.NoSpireIncome = true; MetaRun.DropMult += node.Value / 100f; break;
                    case "fortified": MetaRun.SpireHpFactor *= node.Value; MetaRun.DomeRadiusMult *= 1f - node.Value2 / 100f; break;
                    case "last_stand":
                        MetaRun.LastStandAt = node.Value / 100f; MetaRun.LastStandSeconds = node.Value2; MetaRun.LastStandStun = node.Value3; break;
                    case "requisition": MetaRun.Requisition = true; break;
                    default: GD.PushWarning($"[MetaPerk] {node.Id}: unknown effect '{node.Effect}'"); break;
                }
            }
        }

        private static MetaPerkLane ParseLane(string s) => s switch
        {
            "player" => MetaPerkLane.Player,
            "harvester" => MetaPerkLane.Harvester,
            _ => MetaPerkLane.Network,
        };

        private static void Load()
        {
            _nodes = new List<MetaPerkNode>();
            _lanes.Clear();
            var text = FileAccess.FileExists(DataPath) ? FileAccess.GetFileAsString(DataPath) : null;
            var json = new Json();
            if (text == null || json.Parse(text) != Error.Ok || json.Data.Obj is not Godot.Collections.Dictionary root)
            {
                GD.PushError($"[MetaPerk] Could not read {DataPath}");
                return;
            }
            if (root.ContainsKey("points") && root["points"].Obj is Godot.Collections.Dictionary pts)
            {
                if (pts.ContainsKey("perWaves")) PointsPerWaves = Math.Max(1, pts["perWaves"].AsInt32());
                if (pts.ContainsKey("ascendantKill")) AscendantKillPoints = pts["ascendantKill"].AsInt32();
            }
            if (root.ContainsKey("lanes") && root["lanes"].Obj is Godot.Collections.Array lanes)
                foreach (var l in lanes)
                    if (l.Obj is Godot.Collections.Dictionary ld)
                        _lanes.Add((ParseLane(ld["id"].AsString()), ld["name"].AsString(),
                            ld.ContainsKey("blurb") ? ld["blurb"].AsString() : ""));
            if (root.ContainsKey("perks") && root["perks"].Obj is Godot.Collections.Array perks)
                foreach (var p in perks)
                {
                    if (p.Obj is not Godot.Collections.Dictionary d) continue;
                    _nodes.Add(new MetaPerkNode
                    {
                        Id = d["id"].AsString(),
                        Name = d["name"].AsString(),
                        DescTemplate = d.ContainsKey("desc") ? d["desc"].AsString() : "",
                        Lane = ParseLane(d.ContainsKey("lane") ? d["lane"].AsString() : "network"),
                        Needs = d.ContainsKey("needs") ? d["needs"].AsInt32() : 0,
                        MaxRank = d.ContainsKey("ranks") ? Math.Max(1, d["ranks"].AsInt32()) : 1,
                        Cost = d.ContainsKey("cost") ? Math.Max(1, d["cost"].AsInt32()) : 1,
                        IsKeystone = d.ContainsKey("keystone") && d["keystone"].AsBool(),
                        Repeatable = d.ContainsKey("repeatable") && d["repeatable"].AsBool(),
                        Effect = d.ContainsKey("effect") ? d["effect"].AsString() : "",
                        Value = d.ContainsKey("value") ? (float)d["value"].AsDouble() : 0f,
                        Value2 = d.ContainsKey("value2") ? (float)d["value2"].AsDouble() : 0f,
                        Value3 = d.ContainsKey("value3") ? (float)d["value3"].AsDouble() : 0f,
                        Choice = d.ContainsKey("choice") ? d["choice"].AsString() : null,
                    });
                }
            GD.Print($"[MetaPerk] Loaded {_nodes.Count} perks ({TotalCost} points to buy them all)");
        }
    }

    /// <summary>
    /// This run's modifiers from the permanent tree that don't live on SignalTuningEditor.
    /// Reset and set at the start of every run (MetaPerkRegistry.Apply).
    /// </summary>
    public static class MetaRun
    {
        public static int FreeTowers;
        /// <summary>Free towers left this run (counts down as they're placed).</summary>
        public static int FreeTowersLeft;
        public static int WallCostCut;
        public static float WallHpMult = 1f;
        public static float UpgradeCostMult = 1f;
        public static bool FreeRebuild;
        public static bool VeteranTowers;
        public static int BitStartLevel = 1;
        public static float AbilityCostMult = 1f;
        public static bool SecondWind;
        /// <summary>Second Wind already spent this run.</summary>
        public static bool SecondWindUsed;
        public static float BountyMult = 1f;
        public static float InterestPct;
        public static int InterestCap;
        public static float SpireHpMult = 1f;
        public static int PerkChoices = 3;
        public static float CrewBonusAdd, DomeBonusAdd, TowerHpMult = 1f;
        public static int VeteranKillMult = 1;
        public static float BitRegenMult = 1f, BitMoveMult = 1f, AbilityCooldownMult = 1f, BitKillBounty = 1f;
        public static float StrikeCostMult = 1f, DropMult = 1f;
        public static int StartGuns;
        /// <summary>Perk-tree points owned this run (Climb.Threat: AXIS pushes harder against a stronger BIT).</summary>
        public static int PointsSpent;
        public static bool StartStrikes;
        // Pick-one keystones (2026-10)
        public static float TowerHpFactor = 1f, TowerDamageFactor = 1f, TowerRangeFactor = 1f, TowerCostMult = 1f;
        public static bool NoTowerRepair;
        /// <summary>Swarm Doctrine: each tower standing raises the next one's price this much less.</summary>
        public static float SupplyStepMult = 1f;
        /// <summary>One Big Gun: most towers (not walls or relays) you can have (0: no cap).</summary>
        public static int GunCap;
        /// <summary>Chain Reaction: share of an enemy's health it blows up for when a tower kills it, and the blast's reach.</summary>
        public static float ChainShare, ChainRadius = 3f;
        public static bool BloodRites;
        public static float AbilityPowerMult = 1f;
        public static float BitHpFactor = 1f, BitDamageFactor = 1f, BitDamageTaken = 1f;
        public static bool NoSprint;
        public static float EchoShare, EchoDelay = 0.6f;
        public static bool NoSpireIncome;
        public static float SpireHpFactor = 1f, DomeRadiusMult = 1f;
        /// <summary>Last Stand: the Spire's health share that sets it off (0: not taken), how long the towers' double rate lasts, the stun.</summary>
        public static float LastStandAt, LastStandSeconds, LastStandStun;
        public static bool LastStandUsed;
        /// <summary>Seconds of Last Stand's double fire rate left.</summary>
        public static float LastStandLeft;
        public static bool Requisition;

        public static void Reset()
        {
            CrewBonusAdd = 0f; DomeBonusAdd = 0f; TowerHpMult = 1f; VeteranKillMult = 1;
            BitRegenMult = 1f; BitMoveMult = 1f; AbilityCooldownMult = 1f; BitKillBounty = 1f;
            StrikeCostMult = 1f; DropMult = 1f; StartGuns = 0; StartStrikes = false;
            FreeTowers = 0; FreeTowersLeft = 0; WallCostCut = 0; WallHpMult = 1f; UpgradeCostMult = 1f;
            FreeRebuild = false; VeteranTowers = false; BitStartLevel = 1; AbilityCostMult = 1f;
            SecondWind = false; SecondWindUsed = false; BountyMult = 1f; InterestPct = 0f; InterestCap = 0;
            SpireHpMult = 1f; PerkChoices = 3;
            TowerHpFactor = 1f; TowerDamageFactor = 1f; TowerRangeFactor = 1f; TowerCostMult = 1f; NoTowerRepair = false; SupplyStepMult = 1f;
            GunCap = 0; ChainShare = 0f; ChainRadius = 3f; BloodRites = false; AbilityPowerMult = 1f;
            BitHpFactor = 1f; BitDamageFactor = 1f; BitDamageTaken = 1f; NoSprint = false; EchoShare = 0f;
            NoSpireIncome = false; SpireHpFactor = 1f; DomeRadiusMult = 1f;
            LastStandAt = 0f; LastStandSeconds = 0f; LastStandStun = 0f; LastStandUsed = false; LastStandLeft = 0f;
            Requisition = false;
            PointsSpent = 0;
        }

        /// <summary>Called when the run starts: fills the per-run counters.</summary>
        public static void BeginRun()
        {
            FreeTowersLeft = FreeTowers;
            SecondWindUsed = false;
            LastStandUsed = false;
            LastStandLeft = 0f;
        }

        /// <summary>One Big Gun: towers that count toward the cap (everything but walls and relays).</summary>
        public static bool CountsAsGun(VineNodeType t) => t is not (VineNodeType.BarrierWall or VineNodeType.BuffEmitter or VineNodeType.SalvageDrill);

        /// <summary>What placing this node costs right now (a suit's kit, free towers, cheaper walls).</summary>
        public static int PlaceCost(VineNodeData data)
        {
            if (data == null) return 0;
            if (SuitKit.CountOf(data.Type) > 0) return 0; // a boss fight's suit: its towers are free
            // Drills are never free or halved: each one standing makes the next dearer
            if (data.Type == VineNodeType.SalvageDrill) return SalvageDrill.NextCost();
            if (FreeTowersLeft > 0 && data.Type != VineNodeType.BarrierWall) return 0;
            int cost = data.ResourceCost;
            // Living Walls: double; Environment power type: cheaper
            if (data.Type == VineNodeType.BarrierWall) cost = Math.Max(1, Mathf.RoundToInt((cost - WallCostCut) * RelicRun.WallCostMult * PowerType.WallCostMult));
            // Swarm Doctrine: half; supply: each of the type standing makes the next dearer
            else cost = Climb.SupplyCost(Math.Max(1, Mathf.RoundToInt(cost * TowerCostMult)), Climb.Standing(data.Type), Climb.Fleet());
            return cost;
        }

        /// <summary>A node was placed for <see cref="PlaceCost"/>: use up a free tower if it was one.</summary>
        public static void OnPlaced(VineNodeData data)
        {
            if (data != null && FreeTowersLeft > 0 && data.Type is not (VineNodeType.BarrierWall or VineNodeType.SalvageDrill))
                FreeTowersLeft--;
        }

        public static int UpgradeCost(int cost) => Mathf.Max(1, Mathf.RoundToInt(cost * UpgradeCostMult * RoleRun.UpgradeCostMult));
    }
}
