using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The relics: the catalogue of every relic ever found (kept between runs, the Relics
    /// screen shows it) and the run's three slots. Relics are run items: they come out of loot
    /// crates during a run, go in their slot from the crate, and all come off when the run ends
    /// (<see cref="BeginRun"/>, <see cref="EndRun"/>). Before 2026-10 the slots were saved, so
    /// Cryo Fists from one run's crate were still on at the start of the next.
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
        // Found in this run's crates: what can go in a slot, and what a crate gives as a copy
        private static readonly HashSet<string> _thisRun = new();
        private static bool _loaded;
        private static bool _persist = true;

        public static IReadOnlyList<string> Owned { get { EnsureLoaded(); return _owned; } }
        public static IReadOnlyList<string> Equipped { get { EnsureLoaded(); return _equipped; } }

        public static int OwnedCount { get { EnsureLoaded(); return _owned.Count; } }
        public static int EquippedCount { get { EnsureLoaded(); return _equipped.Count; } }
        public static int MaxEquipSlots => RelicManager.MAX_EQUIPPED;

        public static bool OwnsRelic(string relicId) { EnsureLoaded(); return _owned.Contains(relicId); }
        public static bool IsEquipped(string relicId) { EnsureLoaded(); return _equipped.Contains(relicId); }

        /// <summary>Found in this run and not already on: equipping it takes its kind's slot (replacing what was there).</summary>
        public static bool CanEquip(string relicId)
        {
            EnsureLoaded();
            return !_equipped.Contains(relicId) && _thisRun.Contains(relicId) && RelicRegistry.Get(relicId) != null;
        }

        /// <summary>Found in one of this run's crates.</summary>
        public static bool FoundThisRun(string relicId) { EnsureLoaded(); return _thisRun.Contains(relicId); }

        /// <summary>A run starts: nothing in the slots, nothing found yet.</summary>
        public static void BeginRun()
        {
            EnsureLoaded();
            bool had = _equipped.Count > 0;
            _equipped.Clear();
            _thisRun.Clear();
            if (had) { Save(); GameEvents.OnRelicEquipChanged?.Invoke(); }
        }

        /// <summary>The run is over: its relics come off (the catalogue keeps them).</summary>
        public static void EndRun() => BeginRun();

        /// <summary>What's in a kind's slot (bit, towers, spire), or null.</summary>
        public static string EquippedOfKind(string kind)
        {
            EnsureLoaded();
            return _equipped.FirstOrDefault(id => RelicRegistry.Get(id)?.Kind == kind);
        }

        /// <summary>Put a relic in its kind's slot; whatever was there comes off.</summary>
        public static bool Equip(string relicId)
        {
            if (!CanEquip(relicId)) return false;
            string kind = RelicRegistry.Get(relicId)?.Kind;
            _equipped.RemoveAll(id => RelicRegistry.Get(id)?.Kind == kind);
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

        /// <summary>Found one in this run's crates: it can go in a slot, and goes in the catalogue. True if never found before.</summary>
        public static bool Acquire(string relicId)
        {
            EnsureLoaded();
            bool isNew = !_owned.Contains(relicId);
            if (isNew) _owned.Add(relicId);
            _thisRun.Add(relicId);
            Save();
            return isNew;
        }

        /// <summary>Find a relic without saving (testing / debug): catalogued and found this run.</summary>
        public static void ForceOwn(string relicId)
        {
            EnsureLoaded();
            if (!_owned.Contains(relicId)) _owned.Add(relicId);
            _thisRun.Add(relicId);
        }

        /// <summary>
        /// Switch to an empty, non-persisted inventory. Test suites call this so they
        /// neither depend on nor overwrite the player's real save.
        /// </summary>
        public static void UseInMemoryForTests()
        {
            _owned.Clear();
            _equipped.Clear();
            _thisRun.Clear();
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
            _thisRun.Clear();

            var text = SafeFile.ReadAllText(SavePath, IsValidJsonObject);
            if (text == null) return;

            var json = new Json();
            if (json.Parse(text) != Error.Ok || json.Data.Obj is not Godot.Collections.Dictionary dict) return;

            if (dict.ContainsKey("owned"))
                foreach (var id in dict["owned"].AsGodotArray())
                {
                    string s = id.AsString();
                    if (!_owned.Contains(s) && RelicRegistry.Get(s) != null) _owned.Add(s);
                }

            // "equipped" in older saves is ignored: the slots belong to a run, and a run starts empty
        }

        private static void Save()
        {
            if (!_persist) return;
            var dict = new Godot.Collections.Dictionary();
            var owned = new Godot.Collections.Array();
            foreach (var id in _owned) owned.Add(id);
            dict["owned"] = owned;
            SafeFile.WriteAllText(SavePath, Json.Stringify(dict, "  "));
        }

        private static bool IsValidJsonObject(string text)
        {
            var json = new Json();
            return json.Parse(text) == Error.Ok && json.Data.Obj is Godot.Collections.Dictionary;
        }
    }
}
