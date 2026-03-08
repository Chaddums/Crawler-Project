using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Balance Editor — view and tune all game data: abilities, enemies, equipment, consumables, etc.
    /// Sub-tabs let you switch between data types. Each sub-tab shows a DataTable (left) + PropertyInspector (right).
    /// Changes save to JSON in Data/ directory. Registries load JSON on next init if present.
    /// </summary>
    public partial class BalanceEditor : EditorPanel
    {
        public override string PanelName => "Balance";
        public override Color AccentColor => EditorStyles.AccentBalance;

        private HBoxContainer _subTabBar;
        private HSplitContainer _split;
        private DataTable _table;
        private SearchFilter _search;
        private PropertyInspector _inspector;
        private Label _inspectorTitle;

        private string _activeSubTab = "Abilities";
        private readonly List<Button> _subTabButtons = new();

        // Current data store for each sub-tab (loaded from registries)
        private Dictionary<string, Dictionary<string, object>> _currentData;

        private static readonly string[] SubTabs = { "Abilities", "Enemies", "Equipment", "Consumables", "Relics" };

        protected override void BuildUI(VBoxContainer content)
        {
            // Sub-tab bar
            _subTabBar = new HBoxContainer();
            _subTabBar.AddThemeConstantOverride("separation", 2);
            foreach (var tab in SubTabs)
            {
                var btn = EditorStyles.MakeButton(tab, EditorStyles.FontSmall);
                btn.CustomMinimumSize = new Vector2(90, 28);
                var captured = tab;
                btn.Pressed += () => SwitchSubTab(captured);
                _subTabBar.AddChild(btn);
                _subTabButtons.Add(btn);
            }
            content.AddChild(_subTabBar);

            // Search filter
            _search = new SearchFilter();
            content.AddChild(_search);
            // Wire up after _Ready

            // Main split: table (left) | inspector (right)
            _split = new HSplitContainer();
            _split.SizeFlagsVertical = SizeFlags.ExpandFill;
            _split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
#pragma warning disable CS0618
            _split.SplitOffset = 500;
#pragma warning restore CS0618

            // Left panel - data table
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.CustomMinimumSize = new Vector2(400, 0);

            _table = new DataTable();
            _table.SizeFlagsVertical = SizeFlags.ExpandFill;
            _table.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.AddChild(_table);
            _split.AddChild(leftPanel);

            // Right panel - property inspector
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(300, 0);

            _inspectorTitle = EditorStyles.MakeLabel("Select an item", EditorStyles.FontHeader, EditorStyles.TextSecondary);
            rightPanel.AddChild(_inspectorTitle);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            var inspectorScroll = new ScrollContainer();
            inspectorScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            inspectorScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _inspector = new PropertyInspector();
            _inspector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inspectorScroll.AddChild(_inspector);
            rightPanel.AddChild(inspectorScroll);

            _split.AddChild(rightPanel);
            content.AddChild(_split);
        }

        public override void _Ready()
        {
            base._Ready();

            // Deferred wiring (children need _Ready first)
            CallDeferred(nameof(WireEvents));
        }

        private void WireEvents()
        {
            _search.OnFilterChanged += text => _table?.Filter(text);
            _table.OnRowSelected += OnRowSelected;
            _inspector.OnValueChanged += OnPropertyChanged;
            SwitchSubTab("Abilities");
        }

        private void SwitchSubTab(string tab)
        {
            _activeSubTab = tab;

            // Update button styles
            for (int i = 0; i < SubTabs.Length; i++)
            {
                bool active = SubTabs[i] == tab;
                _subTabButtons[i].AddThemeStyleboxOverride("normal",
                    EditorStyles.MakeFlat(active ? EditorStyles.BgField : EditorStyles.BgDark));
                if (active)
                    _subTabButtons[i].AddThemeColorOverride("font_color", AccentColor);
                else
                    _subTabButtons[i].RemoveThemeColorOverride("font_color");
            }

            LoadSubTabData();
        }

        private void LoadSubTabData()
        {
            _currentData = _activeSubTab switch
            {
                "Abilities" => LoadAbilities(),
                "Enemies" => LoadEnemies(),
                "Equipment" => LoadEquipment(),
                "Consumables" => LoadConsumables(),
                "Relics" => LoadRelics(),
                _ => new()
            };

            string[] columns = _activeSubTab switch
            {
                "Abilities" => new[] { "Type", "ManaCost", "BaseDamage", "Cooldown", "Range", "ScalingStat" },
                "Enemies" => new[] { "Tier", "Health", "Damage", "Speed", "Armor", "Behavior" },
                "Equipment" => new[] { "Slot", "Stat", "Value", "WeaponType" },
                "Consumables" => new[] { "Rarity", "MaxStack", "HealAmount", "ManaRestore" },
                "Relics" => new[] { "Slot", "Rarity", "StatBonuses" },
                _ => Array.Empty<string>()
            };

            _table.SetData(columns, _currentData);
            _inspectorTitle.Text = $"Select a {_activeSubTab.TrimEnd('s')}";
        }

        private void OnRowSelected(string key, Dictionary<string, object> rowData)
        {
            _inspectorTitle.Text = key;
            _inspectorTitle.AddThemeColorOverride("font_color", AccentColor);

            // Build detailed data for inspector (more fields than the table summary)
            var detailData = GetDetailData(key);
            var hints = GetHints();
            _inspector.Build(detailData, hints);

            // Push undo snapshot
            PushUndo(MiniJsonWriter.Serialize(_currentData));
        }

        private void OnPropertyChanged(string property, object value)
        {
            var selectedKey = _table.SelectedKey;
            if (selectedKey == null || _currentData == null) return;

            // Update in-memory data
            if (_currentData.TryGetValue(selectedKey, out var row))
            {
                row[property] = value;
                _table.UpdateRow(selectedKey, row);
            }

            MarkDirty();
            ApplyToRegistry(selectedKey);
        }

        // ===== DATA LOADING =====

        private Dictionary<string, Dictionary<string, object>> LoadAbilities()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();

            // Try JSON first
            var json = LoadJson("res://Data/abilities.json");
            if (json != null)
            {
                foreach (var kvp in json)
                {
                    if (kvp.Value is Dictionary<string, object> entry)
                        result[kvp.Key] = entry;
                }
                return result;
            }

            // Fall back to registry
            foreach (var id in GetAbilityIds())
            {
                var a = AbilityRegistry.Get(id);
                if (a == null) continue;
                result[id] = new Dictionary<string, object>
                {
                    ["Type"] = a.Type.ToString(),
                    ["ManaCost"] = (double)a.ManaCost,
                    ["BaseDamage"] = (double)a.BaseDamage,
                    ["Cooldown"] = (double)a.Cooldown,
                    ["Range"] = (double)a.Range,
                    ["ScalingStat"] = a.ScalingStat.ToString(),
                    ["ScalingRatio"] = (double)a.ScalingRatio,
                    ["AoERadius"] = (double)a.AoERadius,
                    ["KnockbackForce"] = (double)a.KnockbackForce,
                    ["StunDuration"] = (double)a.StunDuration,
                    ["BurstCount"] = (double)a.BurstCount,
                    ["BurstDelay"] = (double)a.BurstDelay,
                    ["BurstSpread"] = (double)a.BurstSpread,
                    ["DamageType"] = a.DamageType.ToString()
                };
            }
            return result;
        }

        private Dictionary<string, Dictionary<string, object>> LoadEnemies()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();

            var json = LoadJson("res://Data/enemies.json");
            if (json != null)
            {
                foreach (var kvp in json)
                {
                    if (kvp.Value is Dictionary<string, object> entry)
                        result[kvp.Key] = entry;
                }
                return result;
            }

            foreach (var kvp in EnemyRegistry.Enemies)
            {
                var e = kvp.Value;
                result[kvp.Key] = new Dictionary<string, object>
                {
                    ["Tier"] = e.Tier.ToString(),
                    ["Health"] = (double)e.BaseHealth,
                    ["Damage"] = (double)e.BaseDamage,
                    ["Speed"] = (double)e.MoveSpeed,
                    ["Armor"] = (double)e.Armor,
                    ["Behavior"] = e.Behavior.ToString(),
                    ["AttackRange"] = (double)e.AttackRange,
                    ["AttackCooldown"] = (double)e.AttackCooldown,
                    ["AggroRange"] = (double)e.AggroRange,
                    ["XpReward"] = (double)e.XpReward,
                    ["SignatureDropChance"] = (double)e.SignatureDropChance,
                    ["LootBoxDropChance"] = (double)e.LootBoxDropChance
                };
            }
            return result;
        }

        private Dictionary<string, Dictionary<string, object>> LoadEquipment()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();

            var json = LoadJson("res://Data/equipment.json");
            if (json != null)
            {
                foreach (var kvp in json)
                {
                    if (kvp.Value is Dictionary<string, object> entry)
                        result[kvp.Key] = entry;
                }
                return result;
            }

            foreach (var equip in BaseItemPool.Equipment)
            {
                var stats = equip.BaseStatBonuses;
                var statName = stats.Count > 0 ? stats[0].StatType.ToString() : "--";
                var statVal = stats.Count > 0 ? stats[0].Value : 0;

                result[equip.Id] = new Dictionary<string, object>
                {
                    ["Slot"] = equip.Slot.ToString(),
                    ["Stat"] = statName,
                    ["Value"] = (double)statVal,
                    ["WeaponType"] = equip.WeaponType.ToString()
                };
            }
            return result;
        }

        private Dictionary<string, Dictionary<string, object>> LoadConsumables()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();

            var json = LoadJson("res://Data/consumables.json");
            if (json != null)
            {
                foreach (var kvp in json)
                {
                    if (kvp.Value is Dictionary<string, object> entry)
                        result[kvp.Key] = entry;
                }
                return result;
            }

            // Load from ConsumableRegistry if it exposes data
            // For now return empty if no JSON
            return result;
        }

        private Dictionary<string, Dictionary<string, object>> LoadRelics()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();

            var json = LoadJson("res://Data/relics.json");
            if (json != null)
            {
                foreach (var kvp in json)
                {
                    if (kvp.Value is Dictionary<string, object> entry)
                        result[kvp.Key] = entry;
                }
                return result;
            }

            return result;
        }

        // ===== DETAIL DATA (full field set for PropertyInspector) =====

        private Dictionary<string, object> GetDetailData(string key)
        {
            if (_currentData.TryGetValue(key, out var data))
            {
                // Return a copy so inspector edits go through OnPropertyChanged
                return new Dictionary<string, object>(data);
            }
            return new();
        }

        // ===== PROPERTY HINTS =====

        private Dictionary<string, PropertyInspector.PropertyHint> GetHints()
        {
            return _activeSubTab switch
            {
                "Abilities" => new()
                {
                    ["Type"] = new() { EnumOptions = new[] { "Melee", "Projectile", "AoE", "Summon", "Buff", "Debuff" } },
                    ["ScalingStat"] = new() { EnumOptions = new[] { "Strength", "Dexterity", "Intelligence", "Vitality" } },
                    ["DamageType"] = new() { EnumOptions = new[] { "Physical", "Fire", "Ice", "Lightning", "Dark", "Poison" } },
                    ["ManaCost"] = new() { Min = 0, Max = 200, Step = 1 },
                    ["BaseDamage"] = new() { Min = 0, Max = 500, Step = 1 },
                    ["Cooldown"] = new() { Min = 0, Max = 60, Step = 0.1f },
                    ["Range"] = new() { Min = 0, Max = 50, Step = 0.5f },
                    ["AoERadius"] = new() { Min = 0, Max = 30, Step = 0.5f },
                    ["KnockbackForce"] = new() { Min = 0, Max = 20, Step = 0.5f },
                    ["StunDuration"] = new() { Min = 0, Max = 10, Step = 0.1f },
                    ["BurstCount"] = new() { Min = 1, Max = 20, Step = 1 },
                    ["ScalingRatio"] = new() { Min = 0, Max = 5, Step = 0.05f },
                },
                "Enemies" => new()
                {
                    ["Tier"] = new() { EnumOptions = new[] { "Normal", "Elite", "MiniBoss", "Boss" } },
                    ["Behavior"] = new() { EnumOptions = new[] { "Melee", "Ranged", "Charger", "Flanker", "Healer", "Tank", "Swarm" } },
                    ["Health"] = new() { Min = 1, Max = 2000, Step = 5 },
                    ["Damage"] = new() { Min = 0, Max = 200, Step = 1 },
                    ["Speed"] = new() { Min = 0, Max = 15, Step = 0.5f },
                    ["Armor"] = new() { Min = 0, Max = 50, Step = 1 },
                    ["AttackRange"] = new() { Min = 0, Max = 30, Step = 0.5f },
                    ["AttackCooldown"] = new() { Min = 0.1f, Max = 10, Step = 0.1f },
                    ["AggroRange"] = new() { Min = 0, Max = 30, Step = 0.5f },
                    ["XpReward"] = new() { Min = 0, Max = 500, Step = 5 },
                    ["SignatureDropChance"] = new() { Min = 0, Max = 1, Step = 0.05f },
                    ["LootBoxDropChance"] = new() { Min = 0, Max = 1, Step = 0.05f },
                },
                "Equipment" => new()
                {
                    ["Slot"] = new() { EnumOptions = new[] { "MainHand", "OffHand", "Head", "Chest", "Legs", "Feet", "Hands", "Amulet", "Ring1", "Ring2", "Back" } },
                    ["WeaponType"] = new() { EnumOptions = new[] { "None", "Pistol", "Rifle", "Shotgun", "Launcher", "Repeater", "BladeRing", "FlailChain", "ShockCoil", "FlameThrower" } },
                    ["Stat"] = new() { EnumOptions = new[] { "Strength", "Dexterity", "Intelligence", "Vitality", "Armor", "MaxHealth", "MaxMana", "MoveSpeed", "AttackSpeed", "CritChance", "CritDamage", "CooldownReduction" } },
                    ["Value"] = new() { Min = 0, Max = 100, Step = 0.5f },
                },
                _ => new()
            };
        }

        // ===== APPLY CHANGES TO LIVE REGISTRIES =====

        private void ApplyToRegistry(string key)
        {
            if (_currentData == null || !_currentData.TryGetValue(key, out var data)) return;

            switch (_activeSubTab)
            {
                case "Abilities":
                    ApplyAbilityChange(key, data);
                    break;
                case "Enemies":
                    ApplyEnemyChange(key, data);
                    break;
            }
        }

        private void ApplyAbilityChange(string id, Dictionary<string, object> data)
        {
            var ability = AbilityRegistry.Get(id);
            if (ability == null) return;

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
            if (data.TryGetValue("Type", out var t) && Enum.TryParse<AbilityType>(t.ToString(), out var abilType))
                ability.Type = abilType;
        }

        private void ApplyEnemyChange(string id, Dictionary<string, object> data)
        {
            var enemy = EnemyRegistry.GetEnemy(id);
            if (enemy == null) return;

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
        }

        // ===== SAVE / RELOAD =====

        protected override void Reload()
        {
            if (_table == null) return; // Not yet ready
            LoadSubTabData();
            MarkClean();
            SetStatus("Loaded from registry", EditorStyles.StatusSaved);
        }

        protected override void Save()
        {
            if (_currentData == null) return;

            string path = _activeSubTab switch
            {
                "Abilities" => "res://Data/abilities.json",
                "Enemies" => "res://Data/enemies.json",
                "Equipment" => "res://Data/equipment.json",
                "Consumables" => "res://Data/consumables.json",
                "Relics" => "res://Data/relics.json",
                _ => null
            };

            if (path != null && SaveJson(path, _currentData))
            {
                MarkClean();
                SetStatus($"Saved {_activeSubTab} to {path}", EditorStyles.StatusSaved);
                GD.Print($"[BalanceEditor] Saved {_activeSubTab} → {path}");
            }
            else
            {
                SetStatus("Save failed!", EditorStyles.StatusError);
            }
        }

        protected override void RestoreSnapshot(string jsonSnapshot)
        {
            var parsed = MiniJson.Deserialize(jsonSnapshot) as Dictionary<string, object>;
            if (parsed == null) return;

            _currentData = new();
            foreach (var kvp in parsed)
            {
                if (kvp.Value is Dictionary<string, object> entry)
                    _currentData[kvp.Key] = entry;
            }

            // Re-populate table
            string[] columns = _activeSubTab switch
            {
                "Abilities" => new[] { "Type", "ManaCost", "BaseDamage", "Cooldown", "Range", "ScalingStat" },
                "Enemies" => new[] { "Tier", "Health", "Damage", "Speed", "Armor", "Behavior" },
                "Equipment" => new[] { "Slot", "Stat", "Value", "WeaponType" },
                "Consumables" => new[] { "Rarity", "MaxStack", "HealAmount", "ManaRestore" },
                "Relics" => new[] { "Slot", "Rarity", "StatBonuses" },
                _ => Array.Empty<string>()
            };
            _table.SetData(columns, _currentData);
        }

        // ===== HELPERS =====

        private static string[] GetAbilityIds()
        {
            return new[]
            {
                "ability_burst_fire", "ability_strike", "ability_shield_bash", "ability_whirlwind",
                "ability_cannon_blast", "ability_slam", "ability_feral_roar", "ability_earthquake",
                "ability_arcane_bolt", "ability_frost_nova", "ability_meteor",
                "ability_snipe_shot", "ability_backstab", "ability_smoke_bomb", "ability_assassinate",
                "ability_dark_chord", "ability_raise_dead", "ability_death_ballad",
                "ability_rivet_burst", "ability_flurry", "ability_uppercut", "ability_hundred_fists"
            };
        }
    }
}
