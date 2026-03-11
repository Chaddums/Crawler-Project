using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    public class CraftRecipe
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int ComponentCost { get; set; }
        public ItemRarity OutputRarity { get; set; }
        public bool IsWeapon { get; set; }

        public string RarityLabel => OutputRarity switch
        {
            ItemRarity.Common    => "Common",
            ItemRarity.Uncommon  => "Uncommon",
            ItemRarity.Rare      => "Rare",
            ItemRarity.Epic      => "Epic",
            _                    => OutputRarity.ToString()
        };
    }

    /// <summary>
    /// Manages the in-run crafting system.
    /// Players salvage unwanted equipment to gain Scrap Components,
    /// then spend components at the safe room crafting bench to craft new gear.
    /// </summary>
    public static class CraftingSystem
    {
        public const string COMPONENT_ID = "mat_scrap_component";

        private static ItemData _componentData;
        private static bool _initialized;
        private static readonly Random _rng = new();

        public static IReadOnlyList<CraftRecipe> Recipes { get; private set; }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _componentData = new ItemData(
                COMPONENT_ID,
                "Scrap Components",
                ItemRarity.Common,
                ItemType.Crafting);
            _componentData.MaxStack = 99;
            _componentData.Description = "Salvaged parts from disassembled gear. Used at the crafting bench in the safe room.";
            ItemRegistry.Register(_componentData);

            Recipes = new List<CraftRecipe>
            {
                new() { Id = "craft_weapon_common",   DisplayName = "Random Weapon",  ComponentCost = 3,  OutputRarity = ItemRarity.Common,   IsWeapon = true  },
                new() { Id = "craft_armor_common",    DisplayName = "Random Armor",   ComponentCost = 3,  OutputRarity = ItemRarity.Common,   IsWeapon = false },
                new() { Id = "craft_weapon_uncommon", DisplayName = "Random Weapon",  ComponentCost = 6,  OutputRarity = ItemRarity.Uncommon, IsWeapon = true  },
                new() { Id = "craft_armor_uncommon",  DisplayName = "Random Armor",   ComponentCost = 6,  OutputRarity = ItemRarity.Uncommon, IsWeapon = false },
                new() { Id = "craft_weapon_rare",     DisplayName = "Random Weapon",  ComponentCost = 12, OutputRarity = ItemRarity.Rare,     IsWeapon = true  },
                new() { Id = "craft_armor_rare",      DisplayName = "Random Armor",   ComponentCost = 12, OutputRarity = ItemRarity.Rare,     IsWeapon = false },
            };

            GD.Print($"[CraftingSystem] Initialized ({Recipes.Count} recipes)");
        }

        /// <summary>How many Scrap Components this rarity yields when salvaged.</summary>
        public static int SalvageYield(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common    => 1,
            ItemRarity.Uncommon  => 2,
            ItemRarity.Rare      => 4,
            ItemRarity.Epic      => 8,
            ItemRarity.Legendary => 15,
            ItemRarity.Absurd    => 25,
            _                    => 1
        };

        /// <summary>Total Scrap Components in the player's inventory across all stacks.</summary>
        public static int GetComponentCount(PlayerInventory inv)
        {
            return inv.Items
                .Where(i => i.BaseData.Id == COMPONENT_ID)
                .Sum(i => i.StackCount);
        }

        /// <summary>
        /// Salvage an equipment item: remove it from inventory and grant Scrap Components.
        /// </summary>
        public static void SalvageItem(ItemInstance item, PlayerInventory inv)
        {
            int yield = SalvageYield(item.Rarity);
            inv.RemoveItem(item);

            // Try to top up an existing stack first
            var existing = inv.Items.FirstOrDefault(i => i.BaseData.Id == COMPONENT_ID && i.StackCount < _componentData.MaxStack);
            if (existing != null)
            {
                int space = _componentData.MaxStack - existing.StackCount;
                int adding = Math.Min(yield, space);
                existing.StackCount += adding;
                yield -= adding;
            }

            // Remainder (or full yield if no stack existed) goes into a new stack
            if (yield > 0)
            {
                var mat = new ItemInstance(_componentData);
                mat.StackCount = yield;
                inv.TryAddItem(mat);
            }

            GD.Print($"[CraftingSystem] Salvaged {item.GetDisplayName()} → +{SalvageYield(item.Rarity)} components (total: {GetComponentCount(inv)})");
        }

        /// <summary>
        /// Attempt to craft an item from the given recipe.
        /// Returns the crafted ItemInstance, or null if insufficient components.
        /// </summary>
        public static ItemInstance CraftItem(CraftRecipe recipe, PlayerInventory inv)
        {
            if (GetComponentCount(inv) < recipe.ComponentCost)
                return null;

            DeductComponents(recipe.ComponentCost, inv);

            var pool = BaseItemPool.Equipment
                .Where(e => recipe.IsWeapon
                    ? e.Slot == EquipmentSlot.MainHand
                    : e.Slot != EquipmentSlot.MainHand)
                .ToList();

            if (pool.Count == 0) return null;

            var template = pool[_rng.Next(pool.Count)];
            var crafted = new ItemInstance(template, recipe.OutputRarity);

            GD.Print($"[CraftingSystem] Crafted: {crafted.GetDisplayName()} ({crafted.Rarity}) via {recipe.Id}");
            return crafted;
        }

        private static void DeductComponents(int amount, PlayerInventory inv)
        {
            int remaining = amount;
            var stacks = inv.Items.Where(i => i.BaseData.Id == COMPONENT_ID).ToList();

            foreach (var stack in stacks)
            {
                if (remaining <= 0) break;

                if (stack.StackCount <= remaining)
                {
                    remaining -= stack.StackCount;
                    inv.RemoveItem(stack);
                }
                else
                {
                    stack.StackCount -= remaining;
                    remaining = 0;
                }
            }
        }
    }
}
