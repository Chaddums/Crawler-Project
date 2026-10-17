using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Suits: a kit of the best towers from a run (up to <see cref="Constants.SUIT_KIT_SIZE"/>, by
    /// level, then branch, then kills; no walls), saved at the debrief into one of
    /// <see cref="Constants.MAX_SUIT_SLOTS"/> slots and taken into a boss fight, where
    /// <see cref="SuitKit"/> puts its towers on the build bar free to place anywhere at their
    /// levels. Losing the boss fight loses the suit. Before 2026-10 a suit was the exact layout,
    /// pre-placed at the same cells on the boss map (which skipped any that didn't fit), and the
    /// Suits screen drew it as a map.
    /// </summary>
    public static class SuitManager
    {
        private const string SavePath = "user://suits.json";

        private static SuitSaveData[] _suits;
        private static bool _persist = true;

        public static SuitSaveData[] GetAll()
        {
            if (_suits == null)
                _suits = Load();
            return _suits;
        }

        /// <summary>Suits that can go into a boss fight.</summary>
        public static List<SuitSaveData> GetAvailableSuits() => GetAll().Where(s => s != null && s.Usable).ToList();

        /// <summary>Slots free for a new suit (empty or lost).</summary>
        public static int FreeSlots => GetAll().Count(s => s == null || !s.Usable);

        /// <summary>Towers that can go in a kit: every class's towers but the Barricade and the drill.</summary>
        public static bool IsKitTower(VineNodeType type) =>
            type is not (VineNodeType.BarrierWall or VineNodeType.SalvageDrill) && VineDraftScreen.AllTowers.Contains(type);

        /// <summary>
        /// Capture the current VineGrid state into a new suit.
        /// Returns the slot index, or -1 if no slots available.
        /// </summary>
        public static int CaptureSuit(VineGrid grid, string name, string role, int planet, MaterialType material)
            => SaveSnapshot(CreateSnapshot(grid, role, planet, material), name);

        /// <summary>
        /// Build an unsaved suit from the towers on the grid: the best
        /// <see cref="Constants.SUIT_KIT_SIZE"/>. Take it while the battle scene is still alive (the
        /// grid is freed on scene change), so the debrief saves the snapshot later.
        /// </summary>
        public static SuitSaveData CreateSnapshot(VineGrid grid, string role, int planet, MaterialType material,
            string site = "", int wave = 0)
        {
            var suit = new SuitSaveData
            {
                Role = role,
                Planet = planet,
                Material = material,
                RelicNames = Array.Empty<string>(),
                Site = site ?? "",
                Wave = wave,
            };
            var towers = new List<(SuitNodeEntry entry, int kills)>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var node = grid.GetNode(x, y);
                    if (node?.Data == null || !IsKitTower(node.Data.Type)) continue;
                    towers.Add((new SuitNodeEntry
                    {
                        GridX = x,
                        GridY = y,
                        NodeType = node.Data.Type,
                        Components = Array.Empty<TowerComponentType>(),
                        Level = node.Level,
                        Branch = node.Branch?.Id,
                    }, node.Kills));
                }
            suit.Nodes = towers
                .OrderByDescending(t => t.entry.Level)
                .ThenByDescending(t => t.entry.Branch != null)
                .ThenByDescending(t => t.kills)
                .Take(Constants.SUIT_KIT_SIZE)
                .Select(t => t.entry)
                .ToList();
            return suit;
        }

        /// <summary>The best <see cref="Constants.SUIT_KIT_SIZE"/> towers of a list, best first (older saves).</summary>
        public static List<SuitNodeEntry> ToKit(IEnumerable<SuitNodeEntry> entries) => entries
            .Where(e => IsKitTower(e.NodeType))
            .OrderByDescending(e => e.Level)
            .ThenByDescending(e => e.Branch != null)
            .Take(Constants.SUIT_KIT_SIZE)
            .ToList();

        /// <summary>
        /// Save a snapshot into the first free slot under the given name.
        /// Returns the slot index, or -1 if no slots are available or the build is empty.
        /// </summary>
        public static int SaveSnapshot(SuitSaveData snapshot, string name)
        {
            if (snapshot == null || snapshot.Nodes.Count == 0)
            {
                GD.PushWarning("[SuitManager] Nothing to save: no towers in the build");
                return -1;
            }

            var all = GetAll();
            int slot = -1;
            for (int i = 0; i < Constants.MAX_SUIT_SLOTS; i++)
                if (all[i] == null || !all[i].Usable) { slot = i; break; }
            if (slot == -1)
            {
                GD.PushWarning("[SuitManager] No available suit slots");
                return -1;
            }

            var suit = new SuitSaveData
            {
                Name = CleanName(name, slot),
                Role = snapshot.Role,
                Planet = snapshot.Planet,
                Material = snapshot.Material,
                Nodes = ToKit(snapshot.Nodes),
                CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RelicNames = snapshot.RelicNames ?? Array.Empty<string>(),
                Site = snapshot.Site ?? "",
                Wave = snapshot.Wave,
            };

            all[slot] = suit;
            Save(all);
            GameEvents.OnSuitSaved?.Invoke(slot);
            GD.Print($"[SuitManager] Saved suit '{suit.Name}' in slot {slot} ({suit.Nodes.Count} towers)");
            return slot;
        }

        private static string CleanName(string name, int slot)
        {
            name = (name ?? "").Trim();
            if (name.Length > 28) name = name[..28];
            return name.Length == 0 ? $"Suit {slot + 1}" : name;
        }

        /// <summary>Rename a suit. False if the slot is empty.</summary>
        public static bool Rename(int index, string name)
        {
            var all = GetAll();
            if (index < 0 || index >= all.Length || all[index] == null || all[index].Nodes.Count == 0) return false;
            all[index].Name = CleanName(name, index);
            Save(all);
            return true;
        }

        /// <summary>Empty a slot.</summary>
        public static bool Salvage(int index)
        {
            var all = GetAll();
            if (index < 0 || index >= all.Length || all[index] == null) return false;
            all[index] = null;
            Save(all);
            return true;
        }

        /// <summary>Mark a suit as lost (a failed boss fight).</summary>
        public static void DestroySuit(int index)
        {
            var all = GetAll();
            if (index < 0 || index >= all.Length || all[index] == null) return;
            all[index].Consumed = true;
            Save(all);
            GameEvents.OnSuitDestroyed?.Invoke(index);
            GD.Print($"[SuitManager] Suit '{all[index].Name}' lost (slot {index})");
        }

        // ── What a suit holds, for the screens ──

        public record KitLine(VineNodeType Type, string Name, int Level, int MaxLevel, string BranchName, Color Color, int Value);

        public static List<KitLine> Describe(SuitSaveData suit)
        {
            var lines = new List<KitLine>();
            if (suit == null) return lines;
            foreach (var e in suit.Nodes)
            {
                var data = VineNodeRegistry.Get(e.NodeType);
                if (data == null) continue;
                var plan = TowerUpgradePlan.For(data.Id);
                int maxLevel = 1 + (plan?.Levels.Count ?? 0);
                int level = Mathf.Clamp(e.Level, 1, maxLevel);
                var branch = e.Branch != null ? plan?.Branches.FirstOrDefault(b => b.Id == e.Branch) : null;
                int value = data.ResourceCost;
                if (plan != null)
                    for (int i = 0; i < level - 1 && i < plan.Levels.Count; i++) value += plan.Levels[i].Cost;
                if (branch != null) value += branch.Cost;
                lines.Add(new KitLine(e.NodeType, data.Name, level, maxLevel, branch?.Name, data.TintColor, value));
            }
            return lines;
        }

        /// <summary>What the kit would cost to build and upgrade.</summary>
        public static int KitValue(SuitSaveData suit) => Describe(suit).Sum(l => l.Value);

        /// <summary>One line naming the kit's towers: "2 Junk Turret L3 Railgun, Tesla Coil L2, ...".</summary>
        public static string KitSummary(SuitSaveData suit)
        {
            var groups = Describe(suit)
                .GroupBy(l => (l.Name, l.Level, l.BranchName))
                .Select(g => (g.Count() > 1 ? $"{g.Count()} " : "") + LineLabel(g.First()));
            return string.Join(", ", groups);
        }

        /// <summary>"Junk Turret L3 Railgun".</summary>
        public static string LineLabel(KitLine l) => $"{l.Name} L{l.Level}{(l.BranchName != null ? " " + l.BranchName : "")}";

        /// <summary>Where a suit came from: "Relay Station Alpha, wave 23".</summary>
        public static string Origin(SuitSaveData suit)
        {
            if (suit == null) return "";
            string where = !string.IsNullOrEmpty(suit.Site) ? suit.Site : $"Planet {suit.Planet}";
            return suit.Wave > 0 ? $"{where}, wave {suit.Wave}" : where;
        }

        // ── Persistence ──

        /// <summary>Tests: an empty set of suits that is never written to disk.</summary>
        public static void UseInMemoryForTests()
        {
            _persist = false;
            _suits = new SuitSaveData[Constants.MAX_SUIT_SLOTS];
        }

        /// <summary>Leave test mode and read the real save again.</summary>
        public static void RestoreFromDisk()
        {
            _persist = true;
            _suits = null;
        }

        public static SuitSaveData[] Load()
        {
            var suits = new SuitSaveData[Constants.MAX_SUIT_SLOTS];
            var text = SafeFile.ReadAllText(SavePath, t =>
            {
                var j = new Json();
                return j.Parse(t) == Error.Ok && j.Data.Obj is Godot.Collections.Dictionary;
            });
            return text == null ? suits : Parse(text);
        }

        /// <summary>Read suits from save text. An older save's layout becomes its best towers as a kit.</summary>
        public static SuitSaveData[] Parse(string text)
        {
            var suits = new SuitSaveData[Constants.MAX_SUIT_SLOTS];
            var json = new Json();
            if (json.Parse(text) != Error.Ok)
            {
                GD.PushWarning($"[SuitManager] Failed to parse save: {json.GetErrorMessage()}");
                return suits;
            }
            var root = json.Data.AsGodotDictionary();
            if (root == null || !root.ContainsKey("suits")) return suits;

            var arr = root["suits"].AsGodotArray();
            for (int i = 0; i < arr.Count && i < Constants.MAX_SUIT_SLOTS; i++)
            {
                var sd = arr[i].AsGodotDictionary();
                if (sd == null || !sd.ContainsKey("name")) continue;

                var suit = new SuitSaveData
                {
                    Name = sd["name"].AsString(),
                    Role = sd.ContainsKey("role") ? sd["role"].AsString() : "Obelisk",
                    Planet = sd.ContainsKey("planet") ? sd["planet"].AsInt32() : 1,
                    Material = sd.ContainsKey("material") ? (MaterialType)sd["material"].AsInt32() : MaterialType.None,
                    Consumed = sd.ContainsKey("consumed") && sd["consumed"].AsBool(),
                    CreatedTimestamp = sd.ContainsKey("created") ? sd["created"].AsInt64() : 0,
                    RelicNames = Array.Empty<string>(),
                    Site = sd.ContainsKey("site") ? sd["site"].AsString() : "",
                    Wave = sd.ContainsKey("wave") ? sd["wave"].AsInt32() : 0,
                };
                if (string.IsNullOrEmpty(suit.Role)) suit.Role = "Obelisk";

                if (sd.ContainsKey("relics"))
                    suit.RelicNames = sd["relics"].AsGodotArray().Select(r => r.AsString()).ToArray();

                var entries = new List<SuitNodeEntry>();
                if (sd.ContainsKey("nodes"))
                    foreach (var nodeVar in sd["nodes"].AsGodotArray())
                    {
                        var nd = nodeVar.AsGodotDictionary();
                        if (nd == null) continue;
                        entries.Add(new SuitNodeEntry
                        {
                            GridX = nd.ContainsKey("x") ? nd["x"].AsInt32() : 0,
                            GridY = nd.ContainsKey("y") ? nd["y"].AsInt32() : 0,
                            NodeType = nd.ContainsKey("type") ? (VineNodeType)nd["type"].AsInt32() : VineNodeType.Extender,
                            Components = Array.Empty<TowerComponentType>(),
                            Level = nd.ContainsKey("level") ? Mathf.Max(1, nd["level"].AsInt32()) : 1,
                            Branch = nd.ContainsKey("branch") && nd["branch"].AsString().Length > 0 ? nd["branch"].AsString() : null,
                        });
                    }
                suit.Nodes = ToKit(entries);
                suits[i] = suit;
            }
            return suits;
        }

        public static void Save(SuitSaveData[] suits)
        {
            if (!_persist) return;
            var text = Serialize(suits);
            if (!SafeFile.WriteAllText(SavePath, text))
            {
                GD.PushError("[SuitManager] Failed to write save file");
                return;
            }
            GD.Print("[SuitManager] Saved to disk");
        }

        public static string Serialize(SuitSaveData[] suits)
        {
            var arr = new Godot.Collections.Array();
            foreach (var suit in suits)
            {
                if (suit == null)
                {
                    arr.Add(new Godot.Collections.Dictionary());
                    continue;
                }
                var relics = new Godot.Collections.Array();
                foreach (var r in suit.RelicNames ?? Array.Empty<string>()) relics.Add(r ?? "");
                var nodes = new Godot.Collections.Array();
                foreach (var e in suit.Nodes)
                    nodes.Add(new Godot.Collections.Dictionary
                    {
                        ["x"] = e.GridX, ["y"] = e.GridY, ["type"] = (int)e.NodeType,
                        ["level"] = e.Level, ["branch"] = e.Branch ?? "",
                        ["components"] = new Godot.Collections.Array(),
                    });
                arr.Add(new Godot.Collections.Dictionary
                {
                    ["name"] = suit.Name ?? "",
                    ["role"] = suit.Role ?? "",
                    ["planet"] = suit.Planet,
                    ["material"] = (int)suit.Material,
                    ["consumed"] = suit.Consumed,
                    ["created"] = suit.CreatedTimestamp,
                    ["site"] = suit.Site ?? "",
                    ["wave"] = suit.Wave,
                    ["relics"] = relics,
                    ["nodes"] = nodes,
                });
            }
            return Json.Stringify(new Godot.Collections.Dictionary { ["suits"] = arr }, "  ");
        }

        public static void Reset()
        {
            _suits = new SuitSaveData[Constants.MAX_SUIT_SLOTS];
            Save(_suits);
        }
    }

    /// <summary>
    /// The suit taken into this boss fight: its towers wait on the build bar (marked KIT), each
    /// placed for nothing and arriving at its saved level and branch. Best first within a type.
    /// Cleared at every battle start; set by <see cref="VineBattleScene"/> for a boss run with a suit.
    /// </summary>
    public static class SuitKit
    {
        private static readonly List<SuitNodeEntry> _left = new();
        public static string SuitName { get; private set; } = "";
        public static int Total { get; private set; }
        public static int Left => _left.Count;
        public static bool Active => Total > 0;

        public static void Begin(SuitSaveData suit)
        {
            Clear();
            if (suit == null) return;
            _left.AddRange(SuitManager.ToKit(suit.Nodes).Select(e => new SuitNodeEntry
            {
                NodeType = e.NodeType, Level = e.Level, Branch = e.Branch, Components = Array.Empty<TowerComponentType>(),
            }));
            SuitName = suit.Name ?? "";
            Total = _left.Count;
        }

        public static void Clear()
        {
            _left.Clear();
            SuitName = "";
            Total = 0;
        }

        public static int CountOf(VineNodeType type)
        {
            int n = 0;
            foreach (var e in _left) if (e.NodeType == type) n++;
            return n;
        }

        /// <summary>The best kit tower of this type still waiting, taken off the kit (null: none).</summary>
        public static SuitNodeEntry Take(VineNodeType type)
        {
            int i = _left.FindIndex(e => e.NodeType == type);
            if (i < 0) return null;
            var e = _left[i];
            _left.RemoveAt(i);
            return e;
        }
    }
}
