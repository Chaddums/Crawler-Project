using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Handles saving and loading game state to/from JSON.
    /// Save path: user://junkbot_save.json
    /// </summary>
    public static class SaveManager
    {
        private static readonly string SavePath = $"user://{Constants.SAVE_FILE}";
        private static SaveData _pendingLoad;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static bool SaveFileExists()
        {
            return FileAccess.FileExists(SavePath);
        }

        /// <summary>
        /// Save the current game state.
        /// </summary>
        public static void SaveGame(PlayerController player, int sectorNumber)
        {
            if (player == null) return;

            var data = new SaveData
            {
                CurrentSector = sectorNumber,
                CurrentArea = GameManager.Instance?.CurrentArea ?? 1
            };

            // Player data
            var pd = data.Player;
            pd.ClassName = player.ClassController.CurrentClass ?? BotFrameType.TinCan;
            pd.Level = player.Stats.Level;
            pd.Experience = player.Stats.Experience;
            pd.SkillPoints = player.Stats.AvailableSkillPoints;
            pd.CurrentHealth = player.Health.CurrentHealth;
            pd.CurrentMana = player.Stats.CurrentMana;

            // Base stats
            var statTypes = Enum.GetValues<StatType>();
            foreach (var stat in statTypes)
            {
                float val = player.Stats.Stats.GetBaseStat(stat);
                if (val != 0)
                    pd.BaseStats[stat.ToString()] = val;
            }

            // Inventory items
            foreach (var item in player.Inventory.Items)
                pd.InventoryItems.Add(SerializeItem(item));

            // Equipped items
            foreach (var (slot, item) in player.Inventory.Equipped)
                pd.EquippedItems[slot.ToString()] = SerializeItem(item);

            // Passive tree
            if (player.ClassController.PassiveTree != null)
            {
                foreach (var nodeId in player.ClassController.PassiveTree.AllocatedNodes)
                    pd.AllocatedPassiveNodes.Add(nodeId);
            }

            // Abilities
            for (int i = 0; i < Constants.MAX_ABILITY_SLOTS; i++)
            {
                var slot = player.Combat.GetSlot(i);
                pd.AbilityIds.Add(slot?.Data?.Id ?? "");
            }

            // Achievement data
            if (ServiceLocator.TryGet<AchievementManager>(out var achievementMgr))
                data.Achievements = achievementMgr.GetSaveData();

            // Stairwell timer
            if (ServiceLocator.TryGet<LiftTimer>(out var timer))
                data.TimerRemaining = timer.TimeRemaining;

            // Serialize to JSON
            try
            {
                string json = JsonSerializer.Serialize(data, JsonOptions);

                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreString(json);
                    GD.Print($"[SaveManager] Game saved (Sector {sectorNumber})");
                }
                else
                {
                    GD.PrintErr($"[SaveManager] Failed to open save file: {FileAccess.GetOpenError()}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveManager] Save error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load game state from save file. Call before changing to Sector scene.
        /// </summary>
        public static SaveData LoadGame()
        {
            if (!SaveFileExists())
            {
                GD.PrintErr("[SaveManager] No save file found");
                return null;
            }

            try
            {
                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr($"[SaveManager] Failed to open save file: {FileAccess.GetOpenError()}");
                    return null;
                }

                string json = file.GetAsText();
                var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
                _pendingLoad = data;

                GD.Print($"[SaveManager] Game loaded (Sector {data.CurrentSector} Area {data.CurrentArea}, Lv{data.Player.Level} {data.Player.ClassName})");
                return data;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveManager] Load error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Apply loaded state to the player after spawning.
        /// Called by SectorManager after player is spawned.
        /// </summary>
        public static void ApplyLoadedState(PlayerController player)
        {
            if (_pendingLoad == null || player == null) return;

            var pd = _pendingLoad.Player;

            // Re-select class only if different from what SectorManager already set
            if (player.ClassController.CurrentClass != pd.ClassName)
                player.ClassController.SelectClass(pd.ClassName);

            // Restore level and XP
            player.Stats.SetLevel(pd.Level);
            player.Stats.SetExperience(pd.Experience);
            player.Stats.SetSkillPoints(pd.SkillPoints);

            // Restore base stats
            foreach (var (statName, value) in pd.BaseStats)
            {
                if (Enum.TryParse<StatType>(statName, out var statType))
                    player.Stats.Stats.SetBaseStat(statType, value);
            }

            // Restore health/mana
            float maxHp = player.Stats.GetStat(StatType.MaxHealth);
            player.Health.SetMaxHealth(maxHp, false);
            player.Health.SetCurrentHealth(pd.CurrentHealth);
            player.Stats.SetMana(pd.CurrentMana);

            // Clear starter items before restoring saved inventory
            player.Inventory.ClearAll();
            player.Inventory.SuppressPickupEvents = true;

            // Restore inventory
            foreach (var itemSave in pd.InventoryItems)
            {
                var item = ItemRegistry.Reconstruct(itemSave);
                if (item != null)
                    player.Inventory.TryAddItem(item);
            }

            // Restore equipment
            foreach (var (slotName, itemSave) in pd.EquippedItems)
            {
                if (!Enum.TryParse<EquipmentSlot>(slotName, out _)) continue;

                var item = ItemRegistry.Reconstruct(itemSave);
                if (item != null)
                {
                    player.Inventory.TryAddItem(item);
                    player.Inventory.Equip(item);
                }
            }

            player.Inventory.SuppressPickupEvents = false;

            // Restore passive tree
            if (player.ClassController.PassiveTree != null)
            {
                foreach (var nodeId in pd.AllocatedPassiveNodes)
                {
                    if (nodeId.StartsWith("start_")) continue; // Already allocated
                    player.ClassController.PassiveTree.AllocateNode(
                        nodeId, player.Stats.Stats, 999); // Force allocation
                }
            }

            // Restore achievements
            if (ServiceLocator.TryGet<AchievementManager>(out var achievementMgr))
                achievementMgr.LoadSaveData(_pendingLoad.Achievements);

            // Restore stairwell timer
            if (ServiceLocator.TryGet<LiftTimer>(out var timer))
                timer.SetTimeRemaining(_pendingLoad.TimerRemaining);

            _pendingLoad = null;
            GD.Print("[SaveManager] Loaded state applied to player");
        }

        /// <summary>
        /// Lightweight state restore for normal scene transitions (not full game load).
        /// Restores inventory, equipment, level, XP, abilities, and passive tree
        /// WITHOUT re-selecting class (body mesh and base stats already set).
        /// </summary>
        public static void ApplyTransitionState(PlayerController player)
        {
            var data = LoadGame();
            if (data == null || player == null) return;

            var pd = data.Player;

            // Restore level/XP/skill points
            player.Stats.SetLevel(pd.Level);
            player.Stats.SetExperience(pd.Experience);
            player.Stats.SetSkillPoints(pd.SkillPoints);

            // Restore health/mana
            float maxHp = player.Stats.GetStat(StatType.MaxHealth);
            player.Health.SetMaxHealth(maxHp, false);
            player.Health.SetCurrentHealth(pd.CurrentHealth);
            player.Stats.SetMana(pd.CurrentMana);

            // Clear starter items before restoring saved inventory
            player.Inventory.ClearAll();
            player.Inventory.SuppressPickupEvents = true;

            foreach (var itemSave in pd.InventoryItems)
            {
                var item = ItemRegistry.Reconstruct(itemSave);
                if (item != null)
                    player.Inventory.TryAddItem(item);
            }

            // Restore equipment
            foreach (var (slotName, itemSave) in pd.EquippedItems)
            {
                if (!Enum.TryParse<EquipmentSlot>(slotName, out _)) continue;

                var item = ItemRegistry.Reconstruct(itemSave);
                if (item != null)
                {
                    player.Inventory.TryAddItem(item);
                    player.Inventory.Equip(item);
                }
            }

            player.Inventory.SuppressPickupEvents = false;

            // Restore passive tree
            if (player.ClassController.PassiveTree != null)
            {
                foreach (var nodeId in pd.AllocatedPassiveNodes)
                {
                    if (nodeId.StartsWith("start_")) continue;
                    player.ClassController.PassiveTree.AllocateNode(
                        nodeId, player.Stats.Stats, 999);
                }
            }

            // Restore stairwell timer
            if (ServiceLocator.TryGet<LiftTimer>(out var timer))
                timer.SetTimeRemaining(data.TimerRemaining);

            _pendingLoad = null;
            GD.Print("[SaveManager] Transition state applied to player");
        }

        public static void DeleteSave()
        {
            if (SaveFileExists())
            {
                DirAccess.RemoveAbsolute(SavePath);
                GD.Print("[SaveManager] Save file deleted");
            }
        }

        private static ItemSaveData SerializeItem(ItemInstance item)
        {
            // Register item for future reconstruction
            ItemRegistry.Register(item.BaseData);

            var save = new ItemSaveData
            {
                BaseDataId = item.BaseData.Id,
                Rarity = item.Rarity.ToString(),
                StackCount = item.StackCount
            };

            foreach (var affix in item.Affixes)
            {
                save.Affixes.Add(new AffixSaveData
                {
                    AffixId = affix.Data.Id,
                    RolledValue = affix.RolledValue
                });
            }

            return save;
        }
    }
}
