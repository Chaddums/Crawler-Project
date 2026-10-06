using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Persistent relic inventory: what the player owns and what BIT has equipped.
    ///
    /// Lives outside RelicManager (a battle-scene node) so the meta layer can use it —
    /// the Relic Inventory screen and Meta Hub run with no battle loaded. Before this,
    /// those screens found no RelicManager: every relic showed as owned, equip/unequip
    /// did nothing, and equipped relics weren't saved, so they never applied in a run.
    /// </summary>
    public static class RelicInventory
    {
        private const string SavePath = "user://relic_inventory.json";

        private static readonly List<string> _owned = new();
        private static readonly List<string> _equipped = new();
        private static bool _loaded;
        private static bool _persist = true;

        public static IReadOnlyList<string> Owned { get { EnsureLoaded(); return _owned; } }
        public static IReadOnlyList<string> Equipped { get { EnsureLoaded(); return _equipped; } }

        public static int OwnedCount { get { EnsureLoaded(); return _owned.Count; } }
        public static int EquippedCount { get { EnsureLoaded(); return _equipped.Count; } }
        public static int MaxEquipSlots => RelicManager.MAX_EQUIPPED;

        public static bool OwnsRelic(string relicId) { EnsureLoaded(); return _owned.Contains(relicId); }
        public static bool IsEquipped(string relicId) { EnsureLoaded(); return _equipped.Contains(relicId); }

        public static bool CanEquip(string relicId)
        {
            EnsureLoaded();
            return _equipped.Count < MaxEquipSlots && !_equipped.Contains(relicId) && _owned.Contains(relicId);
        }

        public static bool Equip(string relicId)
        {
            if (!CanEquip(relicId)) return false;
            _equipped.Add(relicId);
            Save();
            GameEvents.OnRelicEquipChanged?.Invoke();
            return true;
        }

        public static bool Unequip(string relicId)
        {
            EnsureLoaded();
            if (!_equipped.Remove(relicId)) return false;
            Save();
            GameEvents.OnRelicEquipChanged?.Invoke();
            return true;
        }

        /// <summary>Add a relic to the inventory. Returns true if it was new.</summary>
        public static bool Acquire(string relicId)
        {
            EnsureLoaded();
            bool isNew = !_owned.Contains(relicId);
            if (isNew) _owned.Add(relicId);
            Save();
            return isNew;
        }

        /// <summary>Own a relic without saving (testing / debug).</summary>
        public static void ForceOwn(string relicId)
        {
            EnsureLoaded();
            if (!_owned.Contains(relicId)) _owned.Add(relicId);
        }

        /// <summary>
        /// Switch to an empty, non-persisted inventory. Test suites call this so they
        /// neither depend on nor overwrite the player's real save.
        /// </summary>
        public static void UseInMemoryForTests()
        {
            _owned.Clear();
            _equipped.Clear();
            _loaded = true;
            _persist = false;
        }

        /// <summary>Leave test mode and reload the real save from disk.</summary>
        public static void RestoreFromDisk()
        {
            _persist = true;
            _loaded = false;
            EnsureLoaded();
        }

        // ── Persistence ──

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _owned.Clear();
            _equipped.Clear();

            var text = SafeFile.ReadAllText(SavePath, IsValidJsonObject);
            if (text == null) return;

            var json = new Json();
            if (json.Parse(text) != Error.Ok || json.Data.Obj is not Godot.Collections.Dictionary dict) return;

            if (dict.ContainsKey("owned"))
                foreach (var id in dict["owned"].AsGodotArray())
                {
                    string s = id.AsString();
                    if (!_owned.Contains(s)) _owned.Add(s);
                }

            if (dict.ContainsKey("equipped"))
                foreach (var id in dict["equipped"].AsGodotArray())
                {
                    string s = id.AsString();
                    // Only keep equips that are still valid
                    if (_owned.Contains(s) && !_equipped.Contains(s) && _equipped.Count < MaxEquipSlots
                        && RelicManager.GetRelicById(s) != null)
                        _equipped.Add(s);
                }
        }

        private static void Save()
        {
            if (!_persist) return;
            var dict = new Godot.Collections.Dictionary();
            var owned = new Godot.Collections.Array();
            foreach (var id in _owned) owned.Add(id);
            var equipped = new Godot.Collections.Array();
            foreach (var id in _equipped) equipped.Add(id);
            dict["owned"] = owned;
            dict["equipped"] = equipped;
            SafeFile.WriteAllText(SavePath, Json.Stringify(dict, "  "));
        }

        private static bool IsValidJsonObject(string text)
        {
            var json = new Json();
            return json.Parse(text) == Error.Ok && json.Data.Obj is Godot.Collections.Dictionary;
        }
    }
}
