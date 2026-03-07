using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Static registry of all item definitions (needed to reconstruct ItemInstance on load).
    /// Items are registered as they're created or encountered.
    /// </summary>
    public static class ItemRegistry
    {
        private static readonly Dictionary<string, ItemData> _items = new();
        private static readonly Dictionary<string, AffixData> _affixes = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Index all affixes by ID
            foreach (var affix in AffixRegistry.AllAffixes)
                _affixes[affix.Id] = affix;

            GD.Print($"[ItemRegistry] Initialized ({_affixes.Count} affixes indexed)");
        }

        /// <summary>
        /// Register an item data so it can be looked up by ID later (for save/load).
        /// </summary>
        public static void Register(ItemData data)
        {
            if (!string.IsNullOrEmpty(data.Id))
                _items[data.Id] = data;
        }

        public static ItemData GetItem(string id)
        {
            return _items.TryGetValue(id, out var data) ? data : null;
        }

        public static AffixData GetAffix(string id)
        {
            return _affixes.TryGetValue(id, out var data) ? data : null;
        }

        /// <summary>
        /// Reconstruct an ItemInstance from save data.
        /// </summary>
        public static ItemInstance Reconstruct(ItemSaveData saveData)
        {
            var baseData = GetItem(saveData.BaseDataId);
            if (baseData == null)
            {
                GD.PrintErr($"[ItemRegistry] Unknown item ID: {saveData.BaseDataId}");
                return null;
            }

            var rarity = System.Enum.TryParse<ItemRarity>(saveData.Rarity, out var r) ? r : ItemRarity.Common;

            // Create instance without auto-rolling affixes
            var instance = new ItemInstance(baseData, rarity);
            instance.StackCount = saveData.StackCount;

            // Override rolled affixes with saved ones
            instance.Affixes.Clear();
            foreach (var affixSave in saveData.Affixes)
            {
                var affixData = GetAffix(affixSave.AffixId);
                if (affixData != null)
                    instance.Affixes.Add(new RolledAffix(affixData, affixSave.RolledValue));
            }

            return instance;
        }
    }
}
