using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages suit persistence — capture, apply, destroy.
    /// Suits serialize a successful tower build for reuse in boss runs.
    /// Follows MetaPerkSave pattern for load/save.
    /// </summary>
    public static class SuitManager
    {
        private const string SavePath = "user://suits.json";

        private static SuitSaveData[] _suits;

        public static SuitSaveData[] GetAll()
        {
            if (_suits == null)
                _suits = Load();
            return _suits;
        }

        /// <summary>
        /// Returns non-consumed suits.
        /// </summary>
        public static List<SuitSaveData> GetAvailableSuits()
        {
            var all = GetAll();
            var available = new List<SuitSaveData>();
            foreach (var suit in all)
            {
                if (suit != null && !suit.Consumed && suit.Nodes.Count > 0)
                    available.Add(suit);
            }
            return available;
        }

        /// <summary>
        /// Capture the current VineGrid state into a new suit.
        /// Returns the slot index, or -1 if no slots available.
        /// </summary>
        public static int CaptureSuit(VineGrid grid, string name, string role, int planet, MaterialType material)
        {
            var all = GetAll();
            int slot = -1;
            for (int i = 0; i < Constants.MAX_SUIT_SLOTS; i++)
            {
                if (all[i] == null || all[i].Consumed || all[i].Nodes.Count == 0)
                {
                    slot = i;
                    break;
                }
            }

            if (slot == -1)
            {
                GD.PushWarning("[SuitManager] No available suit slots");
                return -1;
            }

            var suit = new SuitSaveData
            {
                Name = name,
                Role = role,
                Planet = planet,
                Material = material,
                CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RelicNames = Array.Empty<string>()
            };

            // Iterate grid and capture all placed nodes
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var node = grid.GetNode(x, y);
                    if (node?.Data == null) continue;

                    var entry = new SuitNodeEntry
                    {
                        GridX = x,
                        GridY = y,
                        NodeType = node.Data.Type,
                        Components = CaptureComponents(node)
                    };
                    suit.Nodes.Add(entry);
                }
            }

            all[slot] = suit;
            Save(all);
            GameEvents.OnSuitSaved?.Invoke(slot);

            GD.Print($"[SuitManager] Captured suit '{name}' in slot {slot} ({suit.Nodes.Count} nodes)");
            return slot;
        }

        /// <summary>
        /// Apply a suit to a VineGrid — places all nodes from the suit onto the grid.
        /// Used at the start of a boss run.
        /// </summary>
        public static void ApplySuit(SuitSaveData suit, VineGrid grid)
        {
            if (suit == null || suit.Nodes.Count == 0) return;

            int placed = 0;
            foreach (var entry in suit.Nodes)
            {
                var pos = new Vector2I(entry.GridX, entry.GridY);
                if (!grid.InBounds(pos.X, pos.Y)) continue;
                if (grid.GetNode(pos) != null) continue; // Already occupied
                if (grid.GetCell(pos.X, pos.Y) == VineCellType.Wall) continue;

                var nodeData = VineNodeRegistry.Get(entry.NodeType);
                if (nodeData == null) continue;

                var vineNode = new VineNode();
                vineNode.Initialize(nodeData);

                grid.PlaceNode(vineNode, pos);
                placed++;
            }

            GameEvents.OnSuitEquipped?.Invoke();
            GD.Print($"[SuitManager] Applied suit '{suit.Name}' — {placed}/{suit.Nodes.Count} nodes placed");
        }

        /// <summary>
        /// Mark a suit as consumed (destroyed in a failed boss run).
        /// </summary>
        public static void DestroySuit(int index)
        {
            var all = GetAll();
            if (index < 0 || index >= all.Length || all[index] == null) return;

            all[index].Consumed = true;
            Save(all);
            GameEvents.OnSuitDestroyed?.Invoke(index);

            GD.Print($"[SuitManager] Suit '{all[index].Name}' destroyed (slot {index})");
        }

        // ── Persistence ──

        public static SuitSaveData[] Load()
        {
            var suits = new SuitSaveData[Constants.MAX_SUIT_SLOTS];

            if (!FileAccess.FileExists(SavePath))
                return suits;

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null) return suits;

            var text = file.GetAsText();
            var json = new Json();
            if (json.Parse(text) != Error.Ok)
            {
                GD.PushWarning($"[SuitManager] Failed to parse save: {json.GetErrorMessage()}");
                return suits;
            }

            var root = json.Data.AsGodotDictionary();
            if (!root.ContainsKey("suits")) return suits;

            var arr = root["suits"].AsGodotArray();
            for (int i = 0; i < arr.Count && i < Constants.MAX_SUIT_SLOTS; i++)
            {
                var sd = arr[i].AsGodotDictionary();
                if (sd == null || !sd.ContainsKey("name")) continue;

                var suit = new SuitSaveData
                {
                    Name = sd.ContainsKey("name") ? sd["name"].AsString() : $"Suit {i + 1}",
                    Role = sd.ContainsKey("role") ? sd["role"].AsString() : "Obelisk",
                    Planet = sd.ContainsKey("planet") ? sd["planet"].AsInt32() : 1,
                    Material = sd.ContainsKey("material") ? (MaterialType)sd["material"].AsInt32() : MaterialType.None,
                    Consumed = sd.ContainsKey("consumed") && sd["consumed"].AsBool(),
                    CreatedTimestamp = sd.ContainsKey("created") ? sd["created"].AsInt64() : 0,
                    RelicNames = Array.Empty<string>()
                };

                if (sd.ContainsKey("relics"))
                {
                    var relics = sd["relics"].AsGodotArray();
                    suit.RelicNames = new string[relics.Count];
                    for (int r = 0; r < relics.Count; r++)
                        suit.RelicNames[r] = relics[r].AsString();
                }

                if (sd.ContainsKey("nodes"))
                {
                    var nodes = sd["nodes"].AsGodotArray();
                    foreach (var nodeVar in nodes)
                    {
                        var nd = nodeVar.AsGodotDictionary();
                        var entry = new SuitNodeEntry
                        {
                            GridX = nd.ContainsKey("x") ? nd["x"].AsInt32() : 0,
                            GridY = nd.ContainsKey("y") ? nd["y"].AsInt32() : 0,
                            NodeType = nd.ContainsKey("type") ? (VineNodeType)nd["type"].AsInt32() : VineNodeType.Extender,
                            Components = Array.Empty<TowerComponentType>()
                        };

                        if (nd.ContainsKey("components"))
                        {
                            var comps = nd["components"].AsGodotArray();
                            entry.Components = new TowerComponentType[comps.Count];
                            for (int c = 0; c < comps.Count; c++)
                                entry.Components[c] = (TowerComponentType)comps[c].AsInt32();
                        }

                        suit.Nodes.Add(entry);
                    }
                }

                suits[i] = suit;
            }

            GD.Print($"[SuitManager] Loaded suits from disk");
            return suits;
        }

        public static void Save(SuitSaveData[] suits)
        {
            var root = new Godot.Collections.Dictionary();
            var arr = new Godot.Collections.Array();

            foreach (var suit in suits)
            {
                if (suit == null)
                {
                    arr.Add(new Godot.Collections.Dictionary());
                    continue;
                }

                var sd = new Godot.Collections.Dictionary();
                sd["name"] = suit.Name ?? "";
                sd["role"] = suit.Role ?? "";
                sd["planet"] = suit.Planet;
                sd["material"] = (int)suit.Material;
                sd["consumed"] = suit.Consumed;
                sd["created"] = suit.CreatedTimestamp;

                var relics = new Godot.Collections.Array();
                if (suit.RelicNames != null)
                    foreach (var r in suit.RelicNames)
                        relics.Add(r ?? "");
                sd["relics"] = relics;

                var nodes = new Godot.Collections.Array();
                foreach (var entry in suit.Nodes)
                {
                    var nd = new Godot.Collections.Dictionary();
                    nd["x"] = entry.GridX;
                    nd["y"] = entry.GridY;
                    nd["type"] = (int)entry.NodeType;

                    var comps = new Godot.Collections.Array();
                    if (entry.Components != null)
                        foreach (var c in entry.Components)
                            comps.Add((int)c);
                    nd["components"] = comps;

                    nodes.Add(nd);
                }
                sd["nodes"] = nodes;

                arr.Add(sd);
            }

            root["suits"] = arr;

            var text = Json.Stringify(root, "  ");
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError("[SuitManager] Failed to open save file for writing");
                return;
            }
            file.StoreString(text);
            GD.Print("[SuitManager] Saved to disk");
        }

        public static void Reset()
        {
            _suits = null;
            Save(new SuitSaveData[Constants.MAX_SUIT_SLOTS]);
        }

        private static TowerComponentType[] CaptureComponents(VineNode node)
        {
            // TowerSlotSystem stores components — check if node has one
            // VineNode doesn't expose SlotSystem directly, so capture empty for now
            // TODO: Wire TowerSlotSystem serialization when slot system is fully integrated
            return Array.Empty<TowerComponentType>();
        }
    }
}
