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
        private HBoxContainer _idInputRow;
        private LineEdit _idInput;
        private Label _idInputLabel;
        private Label _refResultLabel;
        private string _pendingAction; // "add" or "rename"

        private string _activeSubTab = "Abilities";
        private readonly List<Button> _subTabButtons = new();

        // Current data store for each sub-tab (loaded from registries)
        private Dictionary<string, Dictionary<string, object>> _currentData;

        private static readonly string[] SubTabs = { "Abilities", "Enemies", "Equipment", "BotFrames", "Consumables", "Relics" };

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

            // Inline ID input row (hidden by default)
            _idInputRow = new HBoxContainer();
            _idInputRow.AddThemeConstantOverride("separation", 4);
            _idInputRow.Visible = false;
            _idInputLabel = EditorStyles.MakeLabel("New ID:", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            _idInputRow.AddChild(_idInputLabel);
            _idInput = EditorStyles.MakeLineEdit("Enter ID...", EditorStyles.FontSmall);
            _idInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _idInput.TextSubmitted += OnIdInputSubmitted;
            _idInputRow.AddChild(_idInput);
            var cancelBtn = EditorStyles.MakeButton("X", EditorStyles.FontSmall, EditorStyles.StatusError);
            cancelBtn.CustomMinimumSize = new Vector2(24, 24);
            cancelBtn.Pressed += () => _idInputRow.Visible = false;
            _idInputRow.AddChild(cancelBtn);
            rightPanel.AddChild(_idInputRow);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Find References button
            var findRefsBtn = EditorStyles.MakeButton("Find Refs", EditorStyles.FontSmall, EditorStyles.TextAccent);
            findRefsBtn.CustomMinimumSize = new Vector2(0, 24);
            findRefsBtn.Pressed += FindReferences;
            rightPanel.AddChild(findRefsBtn);

            _refResultLabel = EditorStyles.MakeLabel("", EditorStyles.FontTiny, EditorStyles.TextMuted);
            _refResultLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _refResultLabel.CustomMinimumSize = new Vector2(0, 0);
            rightPanel.AddChild(_refResultLabel);

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
            _table.OnAddRequested += OnAddEntry;
            _table.OnRenameRequested += OnRenameEntry;
            _table.OnDeleteRequested += OnDeleteEntry;
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
                "BotFrames" => LoadBotFrames(),
                "Consumables" => LoadConsumables(),
                "Relics" => LoadRelics(),
                _ => new()
            };

            string[] columns = _activeSubTab switch
            {
                "Abilities" => new[] { "Name", "Type", "ManaCost", "BaseDamage", "Cooldown", "ScalingStat" },
                "Enemies" => new[] { "Name", "Tier", "Health", "Damage", "Speed", "Behavior" },
                "Equipment" => new[] { "Name", "Slot", "Stat", "Value", "WeaponType" },
                "BotFrames" => new[] { "PrimaryStat", "HP", "Mana", "HpPerLvl", "ManaPerLvl", "Armor" },
                "Consumables" => new[] { "Name", "Rarity", "MaxStack", "HealAmount", "ManaRestore" },
                "Relics" => new[] { "Name", "Slot", "Rarity", "StatBonuses" },
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

        // ===== ENTRY MANAGEMENT =====

        private void OnAddEntry()
        {
            _pendingAction = "add";
            _idInputLabel.Text = "New ID:";
            _idInput.Text = "";
            _idInput.PlaceholderText = $"new_{_activeSubTab.ToLowerInvariant().TrimEnd('s')}_id";
            _idInputRow.Visible = true;
            _idInput.GrabFocus();
        }

        private void OnRenameEntry(string key)
        {
            _pendingAction = "rename";
            _idInputLabel.Text = $"Rename '{key}':";
            _idInput.Text = key;
            _idInputRow.Visible = true;
            _idInput.GrabFocus();
            _idInput.SelectAll();
        }

        private void OnDeleteEntry(string key)
        {
            if (_currentData == null || !_currentData.ContainsKey(key)) return;
            PushUndo(MiniJsonWriter.Serialize(_currentData));
            _currentData.Remove(key);
            _table.RemoveRow(key);
            _inspectorTitle.Text = $"Select a {_activeSubTab.TrimEnd('s')}";
            MarkDirty();
            SetStatus($"Deleted '{key}'", EditorStyles.StatusError);
        }

        private void OnIdInputSubmitted(string newId)
        {
            _idInputRow.Visible = false;
            if (string.IsNullOrWhiteSpace(newId) || _currentData == null) return;

            if (_pendingAction == "add")
            {
                if (_currentData.ContainsKey(newId))
                {
                    SetStatus($"ID '{newId}' already exists!", EditorStyles.StatusError);
                    return;
                }
                PushUndo(MiniJsonWriter.Serialize(_currentData));
                var defaults = GetDefaultEntry();
                _currentData[newId] = defaults;
                _table.AddRow(newId, defaults);
                _table.Select(newId);
                MarkDirty();
                SetStatus($"Added '{newId}'", EditorStyles.StatusSaved);
            }
            else if (_pendingAction == "rename")
            {
                var oldKey = _table.SelectedKey;
                if (oldKey == null || oldKey == newId) return;
                if (_currentData.ContainsKey(newId))
                {
                    SetStatus($"ID '{newId}' already exists!", EditorStyles.StatusError);
                    return;
                }
                PushUndo(MiniJsonWriter.Serialize(_currentData));
                _currentData[newId] = _currentData[oldKey];
                _currentData.Remove(oldKey);
                _table.RenameRow(oldKey, newId);
                _table.Select(newId);
                MarkDirty();
                SetStatus($"Renamed '{oldKey}' -> '{newId}'", EditorStyles.StatusSaved);
            }
        }

        private Dictionary<string, object> GetDefaultEntry()
        {
            return _activeSubTab switch
            {
                "Abilities" => new()
                {
                    ["Name"] = "New Ability", ["Type"] = "Melee", ["ManaCost"] = 0.0, ["BaseDamage"] = 10.0, ["Cooldown"] = 1.0,
                    ["Range"] = 2.0, ["ScalingStat"] = "Strength", ["ScalingRatio"] = 1.0,
                    ["AoERadius"] = 0.0, ["KnockbackForce"] = 0.0, ["StunDuration"] = 0.0,
                    ["BurstCount"] = 1.0, ["BurstDelay"] = 0.0, ["BurstSpread"] = 0.0, ["DamageType"] = "Physical"
                },
                "Enemies" => new()
                {
                    ["Name"] = "New Enemy", ["Tier"] = "Normal", ["Health"] = 50.0, ["Damage"] = 5.0, ["Speed"] = 3.0,
                    ["Armor"] = 0.0, ["Behavior"] = "Melee", ["AttackRange"] = 2.0,
                    ["AttackCooldown"] = 1.0, ["AggroRange"] = 10.0, ["XpReward"] = 10.0,
                    ["SignatureDropChance"] = 0.0, ["LootBoxDropChance"] = 0.1
                },
                "Equipment" => new()
                {
                    ["Name"] = "New Equipment", ["Slot"] = "MainHand", ["Stat"] = "Strength", ["Value"] = 1.0, ["WeaponType"] = "None"
                },
                "BotFrames" => new()
                {
                    ["PrimaryStat"] = "Strength", ["SecondaryStat"] = "Dexterity",
                    ["HP"] = 100.0, ["Mana"] = 50.0, ["HpPerLvl"] = 5.0, ["ManaPerLvl"] = 2.0,
                    ["Armor"] = 5.0, ["MoveSpeed"] = 5.0,
                    ["Strength"] = 10.0, ["Dexterity"] = 10.0, ["Constitution"] = 10.0,
                    ["Intelligence"] = 10.0, ["Charisma"] = 10.0, ["Luck"] = 10.0,
                    ["PrimaryPerLvl"] = 2.0, ["SecondaryPerLvl"] = 1.0, ["UnlockCost"] = 500.0
                },
                "Consumables" => new()
                {
                    ["Name"] = "New Consumable", ["Rarity"] = "Common", ["MaxStack"] = 5.0, ["HealAmount"] = 0.0,
                    ["ManaRestore"] = 0.0, ["BuffId"] = "", ["BuffDuration"] = 0.0, ["BaseValue"] = 10.0
                },
                "Relics" => new()
                {
                    ["Name"] = "New Relic", ["Slot"] = "Amulet", ["Rarity"] = "Absurd", ["StatBonuses"] = "",
                    ["Description"] = "", ["FlavorText"] = "", ["AxisQuote"] = ""
                },
                _ => new()
            };
        }

        private void FindReferences()
        {
            var key = _table.SelectedKey;
            if (string.IsNullOrEmpty(key))
            {
                _refResultLabel.Text = "Select an entry first.";
                return;
            }

            var matches = new List<string>();
            var dataDir = ProjectSettings.GlobalizePath("res://Data");
            var scriptDir = ProjectSettings.GlobalizePath("res://Scripts");

            SearchDirectory(dataDir, key, "*.json", matches);
            SearchDirectory(scriptDir, key, "*.cs", matches);

            if (matches.Count == 0)
                _refResultLabel.Text = $"No references to '{key}' found.";
            else
                _refResultLabel.Text = $"Refs for '{key}':\n" + string.Join("\n", matches);
        }

        private static void SearchDirectory(string dir, string searchTerm, string pattern, List<string> results)
        {
            var da = DirAccess.Open(dir);
            if (da == null) return;

            da.ListDirBegin();
            string name = da.GetNext();
            while (!string.IsNullOrEmpty(name))
            {
                var fullPath = System.IO.Path.Combine(dir, name);
                if (da.CurrentIsDir())
                {
                    if (!name.StartsWith("."))
                        SearchDirectory(fullPath, searchTerm, pattern, results);
                }
                else if (MatchesPattern(name, pattern))
                {
                    using var file = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
                    if (file != null)
                    {
                        var text = file.GetAsText();
                        if (text.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                            results.Add(name);
                    }
                }
                name = da.GetNext();
            }
            da.ListDirEnd();
        }

        private static bool MatchesPattern(string fileName, string pattern)
        {
            // Simple *.ext pattern match
            if (pattern.StartsWith("*"))
                return fileName.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
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
                    ["Name"] = a.AbilityName ?? id,
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
                    ["Name"] = e.EnemyName ?? kvp.Key,
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
                    ["Name"] = equip.ItemName ?? equip.Id,
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

            foreach (var kvp in ConsumableRegistry.Consumables)
            {
                var c = kvp.Value;
                result[kvp.Key] = new Dictionary<string, object>
                {
                    ["Name"] = c.ItemName ?? kvp.Key,
                    ["Rarity"] = c.Rarity.ToString(),
                    ["MaxStack"] = (double)c.MaxStack,
                    ["HealAmount"] = (double)c.HealAmount,
                    ["ManaRestore"] = (double)c.ManaRestoreAmount,
                    ["BuffId"] = c.BuffId ?? "",
                    ["BuffDuration"] = (double)c.BuffDuration,
                    ["BaseValue"] = (double)c.BaseValue
                };
            }
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

            foreach (var id in RelicRegistry.AllIds)
            {
                var r = RelicRegistry.Get(id);
                if (r == null) continue;
                var bonuses = new List<string>();
                foreach (var s in r.BaseStatBonuses)
                    bonuses.Add($"{s.StatType} {(s.ModType == ModifierType.Percent ? "%" : "+")} {s.Value}");

                result[id] = new Dictionary<string, object>
                {
                    ["Name"] = r.ItemName ?? id,
                    ["Slot"] = r.Slot.ToString(),
                    ["Rarity"] = r.Rarity.ToString(),
                    ["StatBonuses"] = string.Join(", ", bonuses),
                    ["Description"] = r.Description ?? "",
                    ["FlavorText"] = r.FlavorText ?? "",
                    ["AxisQuote"] = r.AxisQuote ?? ""
                };
            }
            return result;
        }

        private Dictionary<string, Dictionary<string, object>> LoadBotFrames()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();

            var json = LoadJson("res://Data/bot_frames.json");
            if (json != null)
            {
                foreach (var kvp in json)
                {
                    if (kvp.Value is Dictionary<string, object> entry)
                        result[kvp.Key] = entry;
                }
                return result;
            }

            foreach (var kvp in BotFrameRegistry.Classes)
            {
                var f = kvp.Value;
                result[kvp.Key.ToString()] = new Dictionary<string, object>
                {
                    ["PrimaryStat"] = f.PrimaryStat.ToString(),
                    ["SecondaryStat"] = f.SecondaryStat.ToString(),
                    ["HP"] = (double)f.BaseStats.GetBaseStat(StatType.MaxHealth),
                    ["Mana"] = (double)f.BaseStats.GetBaseStat(StatType.MaxMana),
                    ["HpPerLvl"] = (double)f.HpPerLevel,
                    ["ManaPerLvl"] = (double)f.ManaPerLevel,
                    ["Armor"] = (double)f.BaseStats.GetBaseStat(StatType.Armor),
                    ["MoveSpeed"] = (double)f.BaseStats.GetBaseStat(StatType.MoveSpeed),
                    ["Strength"] = (double)f.BaseStats.GetBaseStat(StatType.Strength),
                    ["Dexterity"] = (double)f.BaseStats.GetBaseStat(StatType.Dexterity),
                    ["Constitution"] = (double)f.BaseStats.GetBaseStat(StatType.Constitution),
                    ["Intelligence"] = (double)f.BaseStats.GetBaseStat(StatType.Intelligence),
                    ["Charisma"] = (double)f.BaseStats.GetBaseStat(StatType.Charisma),
                    ["Luck"] = (double)f.BaseStats.GetBaseStat(StatType.Luck),
                    ["PrimaryPerLvl"] = (double)f.PrimaryStatPerLevel,
                    ["SecondaryPerLvl"] = (double)f.SecondaryStatPerLevel,
                    ["UnlockCost"] = (double)f.UnlockCost
                };
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

        // Shared enum option arrays (match actual enums in Enums.cs)
        private static readonly string[] RarityOptions = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Absurd" };
        private static readonly string[] SlotOptions = { "Head", "Chest", "Legs", "Feet", "Hands", "MainHand", "OffHand", "Ring1", "Ring2", "Amulet", "Back" };
        private static readonly string[] StatOptions = { "Strength", "Dexterity", "Constitution", "Intelligence", "Charisma", "Luck", "MaxHealth", "MaxMana", "Armor", "CritChance", "CritDamage", "AttackSpeed", "MoveSpeed", "CooldownReduction" };
        private static readonly string[] CoreStatOptions = { "Strength", "Dexterity", "Constitution", "Intelligence", "Charisma", "Luck" };
        private static readonly string[] WeaponOptions = { "None", "Pistol", "Rifle", "Shotgun", "Launcher", "Repeater", "BladeRing", "FlailChain", "ShockCoil", "FlameThrower" };
        private static readonly string[] DamageOptions = { "Physical", "Fire", "Ice", "Lightning", "Poison", "Dark", "Holy" };
        private static readonly string[] AbilityOptions = { "Melee", "Projectile", "AoE", "Buff", "Summon", "Movement" };
        private static readonly string[] TierOptions = { "Normal", "Elite", "MiniBoss", "Boss" };
        private static readonly string[] BehaviorOptions = { "Melee", "Ranged", "Flanker", "Healer", "Charger", "Swarm", "Tank" };

        private Dictionary<string, PropertyInspector.PropertyHint> GetHints()
        {
            return _activeSubTab switch
            {
                "Abilities" => new()
                {
                    ["Type"] = new() { EnumOptions = AbilityOptions },
                    ["ScalingStat"] = new() { EnumOptions = StatOptions },
                    ["DamageType"] = new() { EnumOptions = DamageOptions },
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
                    ["Tier"] = new() { EnumOptions = TierOptions },
                    ["Behavior"] = new() { EnumOptions = BehaviorOptions },
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
                    ["Slot"] = new() { EnumOptions = SlotOptions },
                    ["WeaponType"] = new() { EnumOptions = WeaponOptions },
                    ["Stat"] = new() { EnumOptions = StatOptions },
                    ["Value"] = new() { Min = 0, Max = 100, Step = 0.5f },
                },
                "BotFrames" => new()
                {
                    ["PrimaryStat"] = new() { EnumOptions = CoreStatOptions },
                    ["SecondaryStat"] = new() { EnumOptions = CoreStatOptions },
                    ["HP"] = new() { Min = 10, Max = 500, Step = 5 },
                    ["Mana"] = new() { Min = 0, Max = 200, Step = 5 },
                    ["HpPerLvl"] = new() { Min = 0, Max = 30, Step = 0.5f },
                    ["ManaPerLvl"] = new() { Min = 0, Max = 15, Step = 0.5f },
                    ["Armor"] = new() { Min = 0, Max = 30, Step = 1 },
                    ["MoveSpeed"] = new() { Min = 1, Max = 15, Step = 0.5f },
                    ["Strength"] = new() { Min = 0, Max = 30, Step = 1 },
                    ["Dexterity"] = new() { Min = 0, Max = 30, Step = 1 },
                    ["Constitution"] = new() { Min = 0, Max = 30, Step = 1 },
                    ["Intelligence"] = new() { Min = 0, Max = 30, Step = 1 },
                    ["Charisma"] = new() { Min = 0, Max = 30, Step = 1 },
                    ["Luck"] = new() { Min = 0, Max = 30, Step = 1 },
                    ["PrimaryPerLvl"] = new() { Min = 0, Max = 10, Step = 0.5f },
                    ["SecondaryPerLvl"] = new() { Min = 0, Max = 10, Step = 0.5f },
                    ["UnlockCost"] = new() { Min = 0, Max = 2000, Step = 25 },
                },
                "Consumables" => new()
                {
                    ["Rarity"] = new() { EnumOptions = RarityOptions },
                    ["MaxStack"] = new() { Min = 1, Max = 99, Step = 1 },
                    ["HealAmount"] = new() { Min = 0, Max = 500, Step = 5 },
                    ["ManaRestore"] = new() { Min = 0, Max = 200, Step = 5 },
                    ["BuffDuration"] = new() { Min = 0, Max = 60, Step = 0.5f },
                    ["BaseValue"] = new() { Min = 0, Max = 1000, Step = 5 },
                },
                "Relics" => new()
                {
                    ["Slot"] = new() { EnumOptions = SlotOptions },
                    ["Rarity"] = new() { EnumOptions = RarityOptions },
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
                "BotFrames" => "res://Data/bot_frames.json",
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
                "Abilities" => new[] { "Name", "Type", "ManaCost", "BaseDamage", "Cooldown", "ScalingStat" },
                "Enemies" => new[] { "Name", "Tier", "Health", "Damage", "Speed", "Behavior" },
                "Equipment" => new[] { "Name", "Slot", "Stat", "Value", "WeaponType" },
                "BotFrames" => new[] { "PrimaryStat", "HP", "Mana", "HpPerLvl", "ManaPerLvl", "Armor" },
                "Consumables" => new[] { "Name", "Rarity", "MaxStack", "HealAmount", "ManaRestore" },
                "Relics" => new[] { "Name", "Slot", "Rarity", "StatBonuses" },
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
