using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>A crate tier from Data/loot_boxes.json (Scrap Crate up to Ascendant Cache).</summary>
    public class LootTier
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public float[] Color { get; set; } = { 1f, 1f, 1f };
        /// <summary>Data/Towers sheet the crate is built from.</summary>
        public string Sheet { get; set; } = "loot_crate";
        /// <summary>A light pillar over it in the field, seen from across the map.</summary>
        public bool Pillar { get; set; }
        /// <summary>How it opens: pop, rays, shatter or ascend.</summary>
        public string Sequence { get; set; } = "pop";
        public List<LootRewardLine> Rewards { get; set; } = new();
        /// <summary>Order in the file (0 = the lowest tier).</summary>
        public int Rank;

        public Color Tint => new(Color.Length > 0 ? Color[0] : 1f, Color.Length > 1 ? Color[1] : 1f, Color.Length > 2 ? Color[2] : 1f);
    }

    public class LootRewardLine
    {
        public string Kind { get; set; } = "resources";
        public float Min { get; set; }
        public float Max { get; set; }
        public float Chance { get; set; } = 1f;
        public string[] Rarities { get; set; } = System.Array.Empty<string>();
        /// <summary>Relic lines: this many different relics for the same slot, to choose one to wear (1: a plain relic).</summary>
        public int Choices { get; set; } = 1;
    }

    /// <summary>
    /// The first cache: handed to BIT when wave <see cref="Wave"/> clears, opened in a safe room
    /// there (wave_pacing.json firstSafeRoom). It looks and opens like <see cref="Tier"/> and holds
    /// <see cref="Rewards"/>: a choice of relics for one slot, early enough to build a run around.
    /// </summary>
    public class LootStarter
    {
        public int Wave { get; set; } = 5;
        public string Tier { get; set; } = "gold";
        public string Name { get; set; } = "First Cache";
        public List<LootRewardLine> Rewards { get; set; } = new();
    }

    public class LootDropRow
    {
        public int FromWave { get; set; }
        public Dictionary<string, float> Weights { get; set; } = new();
    }

    public class LootDrop
    {
        public float Chance { get; set; } = 1f;
        public int FromWave { get; set; }
        public List<LootDropRow> Table { get; set; } = new();
    }

    public class LootFile
    {
        public float WaveScale { get; set; } = 0.08f;
        /// <summary>Resources (and salvage) grow this much a wave instead: like kill bounties, not faster.</summary>
        public float ResourceWaveScale { get; set; } = 0.02f;
        public float PickupRadius { get; set; } = 2.4f;
        public List<LootTier> Tiers { get; set; } = new();
        public Dictionary<string, LootDrop> Drops { get; set; } = new();
        public Dictionary<string, string[]> Lines { get; set; } = new();
        public LootStarter Starter { get; set; }
    }

    /// <summary>One thing a crate gave: what it is, how much, its name and colour for the reveal.</summary>
    public class LootReward
    {
        public string Kind = "";
        public int Amount;
        public string RelicId;
        public string StrikeId;
        public string Name = "";
        public string Detail = "";
        /// <summary>common, uncommon, rare, epic or legendary: the reveal's colour and weight.</summary>
        public string Rarity = "common";

        public int RarityRank => Rarity switch { "uncommon" => 1, "rare" => 2, "epic" => 3, "legendary" => 4, _ => 0 };

        public static Color RarityColor(string rarity) => rarity switch
        {
            "uncommon" => new Color(0.3f, 0.95f, 0.75f),
            "rare" => new Color(0.45f, 0.62f, 1f),
            "epic" => new Color(0.78f, 0.45f, 1f),
            "legendary" => new Color(1f, 0.66f, 0.15f),
            _ => new Color(0.82f, 0.84f, 0.88f),
        };
    }

    /// <summary>
    /// Data/loot_boxes.json: crate tiers, what they hold and what drops them. Rolls a tier for a
    /// drop and a crate's contents. Tests set <see cref="ForceDropChance"/> to make drops certain.
    /// </summary>
    public static class LootTable
    {
        private static LootFile _file;
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>Tests: every drop happens (null: the file's chances).</summary>
        public static float? ForceDropChance;

        public static LootFile File
        {
            get
            {
                if (_file != null) return _file;
                _file = new LootFile();
                const string path = "res://Data/loot_boxes.json";
                if (Godot.FileAccess.FileExists(path))
                {
                    using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                    try { _file = JsonSerializer.Deserialize<LootFile>(f.GetAsText(), _json) ?? new LootFile(); }
                    catch (JsonException e) { GD.PushError($"[Loot] {path}: {e.Message}"); }
                }
                for (int i = 0; i < _file.Tiers.Count; i++) _file.Tiers[i].Rank = i;
                return _file;
            }
        }

        public static bool Loaded => File.Tiers.Count > 0;
        public static IReadOnlyList<LootTier> Tiers => File.Tiers;
        public static LootTier Tier(string id) => File.Tiers.FirstOrDefault(t => t.Id == id);

        private static LootTier _starterTier;
        /// <summary>The first cache as a crate (its tier's look and sequence, its own name and contents), or null.</summary>
        public static LootTier StarterTier
        {
            get
            {
                var st = File.Starter;
                if (st == null || st.Rewards.Count == 0) return null;
                if (_starterTier != null) return _starterTier;
                var like = Tier(st.Tier) ?? File.Tiers.LastOrDefault();
                if (like == null) return null;
                return _starterTier = new LootTier
                {
                    Id = like.Id, Name = st.Name, Color = like.Color, Sheet = like.Sheet, Pillar = like.Pillar,
                    Sequence = like.Sequence, Rank = like.Rank, Rewards = st.Rewards,
                };
            }
        }

        /// <summary>A tier for a drop from <paramref name="source"/> (commander, boss, ascendant, supply) at this wave, or null when it doesn't drop.</summary>
        public static LootTier RollDrop(string source, int wave, RandomNumberGenerator rng)
        {
            if (!File.Drops.TryGetValue(source, out var drop) || drop.Table.Count == 0) return null;
            if (wave < drop.FromWave) return null;
            float chance = ForceDropChance ?? drop.Chance;
            if (rng.Randf() >= chance) return null;
            var row = drop.Table.Where(r => wave >= r.FromWave).OrderBy(r => r.FromWave).LastOrDefault() ?? drop.Table[0];
            return Pick(row.Weights, rng);
        }

        private static LootTier Pick(Dictionary<string, float> weights, RandomNumberGenerator rng)
        {
            float total = weights.Values.Where(w => w > 0f).Sum();
            if (total <= 0f) return null;
            float roll = rng.Randf() * total, acc = 0f;
            foreach (var kv in weights)
            {
                if (kv.Value <= 0f) continue;
                acc += kv.Value;
                if (roll <= acc) return Tier(kv.Key);
            }
            return Tier(weights.Keys.Last());
        }

        /// <summary>What a crate of this tier holds, opened at this wave. Best things last.</summary>
        /// <param name="rolled">Relics already rolled in crates opened alongside this one (shared across them, so one relic never comes twice).</param>
        public static List<LootReward> RollContents(LootTier tier, int wave, RandomNumberGenerator rng, HashSet<string> rolled = null)
        {
            var list = new List<LootReward>();
            if (tier == null) return list;
            float scale = 1f + File.WaveScale * Mathf.Max(0, wave);
            float moneyScale = 1f + File.ResourceWaveScale * Mathf.Max(0, wave);
            var strikes = SpireStation.Current?.Data?.Strikes;
            foreach (var line in tier.Rewards)
            {
                if (line.Chance < 1f && rng.Randf() >= line.Chance) continue;
                float roll = rng.RandfRange(line.Min, Mathf.Max(line.Min, line.Max));
                // Resources and XP grow like kill bounties: XP growing 8% a wave piled three levels
                // on BIT at the first safe room
                int amount = Mathf.RoundToInt(roll * (line.Kind is "resources" or "xp" ? moneyScale : scale));
                switch (line.Kind)
                {
                    case "resources":
                        list.Add(new LootReward { Kind = "resources", Amount = amount, Name = $"{amount} Resources", Detail = "For towers and upgrades this run", Rarity = "common" });
                        break;
                    case "materials":
                        list.Add(new LootReward { Kind = "materials", Amount = amount, Name = $"{amount} Materials", Detail = "Banked at the Spire for BIT's upgrades", Rarity = "uncommon" });
                        break;
                    case "xp":
                        list.Add(new LootReward { Kind = "xp", Amount = amount, Name = $"{amount} BIT XP", Detail = "Training data", Rarity = "uncommon" });
                        break;
                    case "strike":
                        if (strikes == null || strikes.Count == 0) break;
                        var sk = strikes[rng.RandiRange(0, strikes.Count - 1)];
                        list.Add(new LootReward { Kind = "strike", Amount = 1, StrikeId = sk.Id, Name = $"{sk.Name} charge", Detail = $"Fire it with {sk.Key}", Rarity = "rare" });
                        break;
                    case "relic":
                        var relic = PickRelic(line.Rarities, rng, rolled);
                        if (line.Choices > 1)
                        {
                            // A choice is of relics not yet found this run, from a slot with enough of them
                            var fresh = RelicRegistry.All.Where(r => (line.Rarities.Length == 0 || line.Rarities.Contains(r.Rarity)) && RelicRegistry.Usable(r)
                                && !RelicInventory.FoundThisRun(r.Id) && rolled?.Contains(r.Id) != true).ToList();
                            var roomy = fresh.Where(r => fresh.Count(o => o.Kind == r.Kind) >= line.Choices).ToList();
                            if (roomy.Count > 0) relic = roomy[rng.RandiRange(0, roomy.Count - 1)];
                            else if (fresh.Count > 0) relic = fresh[rng.RandiRange(0, fresh.Count - 1)];
                        }
                        if (relic == null) break;
                        string rr = relic.Value.Rarity == "legendary" ? "legendary" : relic.Value.Rarity == "rare" ? "epic" : "rare";
                        if (RelicInventory.FoundThisRun(relic.Value.Id) || list.Any(x => x.RelicId == relic.Value.Id) || rolled?.Contains(relic.Value.Id) == true)
                        {
                            // A relic BIT already has is salvaged for Resources
                            int worth = Mathf.RoundToInt((relic.Value.Rarity switch { "legendary" => 250f, "rare" => 120f, _ => 60f }) * moneyScale);
                            list.Add(new LootReward { Kind = "salvage", Amount = worth, RelicId = relic.Value.Id, Name = $"{relic.Value.Name} (a copy)",
                                Detail = $"Already catalogued: salvaged for {worth} Resources", Rarity = rr == "legendary" ? "epic" : "rare" });
                            break;
                        }
                        rolled?.Add(relic.Value.Id);
                        // A choice: more relics for the same slot. All are catalogued; one is worn.
                        var others = line.Choices > 1
                            ? RelicRegistry.OfKind(relic.Value.Kind).Where(r => r.Id != relic.Value.Id && RelicRegistry.Usable(r) && !RelicInventory.FoundThisRun(r.Id) && rolled?.Contains(r.Id) != true
                                && (line.Rarities.Length == 0 || line.Rarities.Contains(r.Rarity))).ToList()
                            : new List<RelicRegistry.Relic>();
                        string choose = others.Count > 0 ? "CHOOSE ONE to wear. " : "";
                        // A choice shows what each costs: that's what's being chosen between
                        // (and any relic in a reveal: several for one slot turn up in a safe room's haul)
                        string Cost(RelicRegistry.Relic r) => string.IsNullOrEmpty(r.Tradeoff) ? "" : $"\nCost: {r.Tradeoff}";
                        list.Add(new LootReward { Kind = "relic", Amount = 1, RelicId = relic.Value.Id, Name = relic.Value.Name,
                            Detail = $"{choose}Relic ({RelicRegistry.SlotName(relic.Value.Kind)}): {RelicRegistry.Short(relic.Value)}{Cost(relic.Value)}", Rarity = rr });
                        for (int c = 1; c < line.Choices && others.Count > 0; c++)
                        {
                            var o = others[rng.RandiRange(0, others.Count - 1)];
                            others.Remove(o);
                            rolled?.Add(o.Id);
                            list.Add(new LootReward { Kind = "relic", Amount = 1, RelicId = o.Id, Name = o.Name,
                                Detail = $"{choose}Relic ({RelicRegistry.SlotName(o.Kind)}): {RelicRegistry.Short(o)}{Cost(o)}",
                                Rarity = o.Rarity == "legendary" ? "legendary" : o.Rarity == "rare" ? "epic" : "rare" });
                        }
                        break;
                    case "perk_point":
                        list.Add(new LootReward { Kind = "perk_point", Amount = 1, Name = "Perk point", Detail = "Spend it on the Perk Tree in the Command Center", Rarity = "legendary" });
                        break;
                }
            }
            return list.OrderBy(r => r.RarityRank).ToList();
        }

        /// <summary>A relic of one of these rarities, new ones far likelier than copies.</summary>
        private static RelicRegistry.Relic? PickRelic(string[] rarities, RandomNumberGenerator rng, HashSet<string> rolled)
        {
            // Only relics the class being played can use (no Gravity Rams for a class without rams)
            var pool = RelicRegistry.All.Where(r => (rarities.Length == 0 || rarities.Contains(r.Rarity)) && RelicRegistry.Usable(r)).ToList();
            if (pool.Count == 0) return null;
            var weights = pool.Select(r => RelicInventory.FoundThisRun(r.Id) || rolled?.Contains(r.Id) == true ? 0.2f : 1f).ToList();
            float roll = rng.Randf() * weights.Sum(), acc = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += weights[i];
                if (roll <= acc) return pool[i];
            }
            return pool[^1];
        }

        /// <summary>A line for this sequence, or "".</summary>
        public static string Line(string sequence, RandomNumberGenerator rng)
            => File.Lines.TryGetValue(sequence ?? "", out var lines) && lines.Length > 0 ? lines[rng.RandiRange(0, lines.Length - 1)] : "";

        /// <summary>Whether <paramref name="text"/> is one of a sequence's lines (tests).</summary>
        internal static bool IsLine(string sequence, string text)
            => File.Lines.TryGetValue(sequence ?? "", out var lines) && System.Array.IndexOf(lines, text) >= 0;

        public static void Reload() { _file = null; _starterTier = null; }
    }
}
