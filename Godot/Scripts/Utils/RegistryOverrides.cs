using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Loads JSON override files from Data/ and applies them to live registry objects.
    /// Called at the end of each registry's Initialize() so editor changes persist
    /// across game restarts.
    /// </summary>
    public static class RegistryOverrides
    {
        private static Dictionary<string, object> LoadJson(string path)
        {
            if (!FileAccess.FileExists(path)) return null;
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            var text = file.GetAsText();
            if (string.IsNullOrWhiteSpace(text)) return null;
            return MiniJson.Deserialize(text) as Dictionary<string, object>;
        }

        /// <summary>Apply ability overrides from abilities.json.</summary>
        public static void ApplyAbilities()
        {
            var json = LoadJson("res://Data/abilities.json");
            if (json == null) return;

            int count = 0;
            foreach (var kvp in json)
            {
                if (kvp.Value is not Dictionary<string, object> data) continue;
                var ability = AbilityRegistry.Get(kvp.Key);
                if (ability == null) continue;

                if (data.TryGetValue("ManaCost", out var mc)) ability.ManaCost = Convert.ToSingle(mc);
                if (data.TryGetValue("BaseDamage", out var bd)) ability.BaseDamage = Convert.ToSingle(bd);
                if (data.TryGetValue("Cooldown", out var cd)) ability.Cooldown = Convert.ToSingle(cd);
                if (data.TryGetValue("Range", out var rng)) ability.Range = Convert.ToSingle(rng);
                if (data.TryGetValue("AoERadius", out var aoe)) ability.AoERadius = Convert.ToSingle(aoe);
                if (data.TryGetValue("KnockbackForce", out var kb)) ability.KnockbackForce = Convert.ToSingle(kb);
                if (data.TryGetValue("StunDuration", out var st)) ability.StunDuration = Convert.ToSingle(st);
                if (data.TryGetValue("BurstCount", out var bc)) ability.BurstCount = Convert.ToInt32(bc);
                if (data.TryGetValue("BurstDelay", out var bdel)) ability.BurstDelay = Convert.ToSingle(bdel);
                if (data.TryGetValue("BurstSpread", out var bs)) ability.BurstSpread = Convert.ToSingle(bs);
                if (data.TryGetValue("ScalingRatio", out var sr)) ability.ScalingRatio = Convert.ToSingle(sr);
                if (data.TryGetValue("DamageType", out var dt) && Enum.TryParse<DamageType>(dt.ToString(), out var dmgType))
                    ability.DamageType = dmgType;
                if (data.TryGetValue("ScalingStat", out var ss) && Enum.TryParse<StatType>(ss.ToString(), out var statType))
                    ability.ScalingStat = statType;
                count++;
            }
            if (count > 0)
                GD.Print($"[RegistryOverrides] Applied {count} ability overrides from abilities.json");
        }

        /// <summary>Apply enemy overrides from enemies.json.</summary>
        public static void ApplyEnemies()
        {
            var json = LoadJson("res://Data/enemies.json");
            if (json == null) return;

            int count = 0;
            foreach (var kvp in json)
            {
                if (kvp.Value is not Dictionary<string, object> data) continue;
                var enemy = EnemyRegistry.GetEnemy(kvp.Key);
                if (enemy == null) continue;

                if (data.TryGetValue("Health", out var hp)) enemy.BaseHealth = Convert.ToSingle(hp);
                if (data.TryGetValue("Damage", out var dmg)) enemy.BaseDamage = Convert.ToSingle(dmg);
                if (data.TryGetValue("Speed", out var spd)) enemy.MoveSpeed = Convert.ToSingle(spd);
                if (data.TryGetValue("Armor", out var arm)) enemy.Armor = Convert.ToSingle(arm);
                if (data.TryGetValue("AttackRange", out var ar)) enemy.AttackRange = Convert.ToSingle(ar);
                if (data.TryGetValue("AttackCooldown", out var ac)) enemy.AttackCooldown = Convert.ToSingle(ac);
                if (data.TryGetValue("AggroRange", out var agr)) enemy.AggroRange = Convert.ToSingle(agr);
                if (data.TryGetValue("XpReward", out var xp)) enemy.XpReward = Convert.ToInt32(xp);
                if (data.TryGetValue("SignatureDropChance", out var sdc)) enemy.SignatureDropChance = Convert.ToSingle(sdc);
                if (data.TryGetValue("LootBoxDropChance", out var ldc)) enemy.LootBoxDropChance = Convert.ToSingle(ldc);
                if (data.TryGetValue("Tier", out var tier) && Enum.TryParse<EnemyTier>(tier.ToString(), out var t))
                    enemy.Tier = t;
                if (data.TryGetValue("Behavior", out var beh) && Enum.TryParse<EnemyBehavior>(beh.ToString(), out var b))
                    enemy.Behavior = b;
                count++;
            }
            if (count > 0)
                GD.Print($"[RegistryOverrides] Applied {count} enemy overrides from enemies.json");
        }

        /// <summary>Apply relic overrides from relics.json.</summary>
        public static void ApplyRelics()
        {
            var json = LoadJson("res://Data/relics.json");
            if (json == null) return;

            int count = 0;
            foreach (var kvp in json)
            {
                if (kvp.Value is not Dictionary<string, object> data) continue;
                var relic = RelicRegistry.Get(kvp.Key);
                if (relic == null) continue;

                if (data.TryGetValue("Name", out var name)) relic.ItemName = name.ToString();
                if (data.TryGetValue("Description", out var desc)) relic.Description = desc.ToString();
                if (data.TryGetValue("FlavorText", out var flavor)) relic.FlavorText = flavor.ToString();
                if (data.TryGetValue("AxisQuote", out var axis)) relic.AxisQuote = axis.ToString();
                if (data.TryGetValue("Slot", out var slot) && Enum.TryParse<EquipmentSlot>(slot.ToString(), out var s))
                    relic.Slot = s;

                // Parse stat bonuses: "CritDamage % 40, CritChance % 15"
                if (data.TryGetValue("StatBonuses", out var bonusStr))
                    ApplyStatBonusString(relic, bonusStr.ToString());

                count++;
            }
            if (count > 0)
                GD.Print($"[RegistryOverrides] Applied {count} relic overrides from relics.json");
        }

        /// <summary>Apply consumable overrides from consumables.json.</summary>
        public static void ApplyConsumables()
        {
            var json = LoadJson("res://Data/consumables.json");
            if (json == null) return;

            int count = 0;
            foreach (var kvp in json)
            {
                if (kvp.Value is not Dictionary<string, object> data) continue;
                var con = ConsumableRegistry.Get(kvp.Key);
                if (con == null) continue;

                if (data.TryGetValue("Name", out var name)) con.ItemName = name.ToString();
                if (data.TryGetValue("Description", out var desc)) con.Description = desc.ToString();
                if (data.TryGetValue("HealAmount", out var ha)) con.HealAmount = Convert.ToSingle(ha);
                if (data.TryGetValue("ManaAmount", out var ma)) con.ManaRestoreAmount = Convert.ToSingle(ma);
                if (data.TryGetValue("Duration", out var dur)) con.BuffDuration = Convert.ToSingle(dur);
                if (data.TryGetValue("MaxStack", out var ms)) con.MaxStack = Convert.ToInt32(ms);
                count++;
            }
            if (count > 0)
                GD.Print($"[RegistryOverrides] Applied {count} consumable overrides from consumables.json");
        }

        /// <summary>Apply all registry overrides. Call during game startup.</summary>
        public static void ApplyAll()
        {
            ApplyAbilities();
            ApplyEnemies();
            ApplyRelics();
            ApplyConsumables();
        }

        /// <summary>Parse and apply stat bonus string to equipment. Called by BalanceEditor too.</summary>
        public static void ApplyStatBonusString(EquipmentData equip, string bonusString)
        {
            if (string.IsNullOrWhiteSpace(bonusString)) return;

            // Clear existing and re-parse
            equip.BaseStatBonuses.Clear();

            // Format: "CritDamage % 40, CritChance % 15"
            var parts = bonusString.Split(',', StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;

                var tokens = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3) continue;

                if (!Enum.TryParse<StatType>(tokens[0], out var stat)) continue;
                var modType = tokens[1] == "%" ? ModifierType.Percent : ModifierType.Flat;
                if (!float.TryParse(tokens[2], out var value)) continue;

                equip.AddBaseStat(stat, modType, value);
            }
        }
    }
}
