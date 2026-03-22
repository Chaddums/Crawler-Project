using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Loads wave/surge data from JSON files using P# address format.
    /// Falls back to hardcoded VineWaveRegistry if JSON not found.
    /// JSON location: Data/Waves/P{planet}.json (single file per planet)
    /// </summary>
    public static class VineWaveLoader
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// S2: Load all waves for a planet. Tries JSON first, falls back to hardcoded.
        /// </summary>
        public static List<VineWaveData> LoadPlanetWaves(int planet)
        {
            string path = $"res://Data/Waves/P{planet}.json";

            if (Godot.FileAccess.FileExists(path))
            {
                var waves = LoadFromJson(path, planet);
                if (waves != null && waves.Count > 0)
                {
                    GD.Print($"[VineWaveLoader] P{planet}: loaded {waves.Count} waves from JSON");
                    VineWaveRegistry.SetWaves(waves);
                    return waves;
                }
                GD.PushWarning($"[VineWaveLoader] P{planet}: JSON found but failed to parse, using hardcoded fallback");
            }

            // Fallback to hardcoded registry
            var fallback = VineWaveRegistry.GetAll();
            GD.Print($"[VineWaveLoader] P{planet}: using hardcoded fallback ({fallback.Count} waves)");
            return fallback;
        }

        /// <summary>
        /// S2: Load milestone definitions for a planet.
        /// </summary>
        public static List<MilestoneData> LoadMilestones(int planet)
        {
            string path = "res://Data/milestones.json";
            var milestones = new List<MilestoneData>();

            if (!Godot.FileAccess.FileExists(path))
            {
                GD.PushWarning("[VineWaveLoader] No milestones.json found, using defaults");
                // Default milestones every 5 waves
                for (int w = 5; w <= 20; w += 5)
                    milestones.Add(new MilestoneData { Wave = w, Type = "perk_select", Label = $"MILESTONE: Wave {w}" });
                return milestones;
            }

            try
            {
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                string json = file.GetAsText();
                var parsed = Json.ParseString(json);
                if (parsed.VariantType != Variant.Type.Dictionary) return milestones;

                var root = parsed.AsGodotDictionary();
                if (!root.ContainsKey("planets")) return milestones;

                var planets = root["planets"].AsGodotDictionary();
                string planetKey = planet.ToString();
                if (!planets.ContainsKey(planetKey)) return milestones;

                var planetData = planets[planetKey].AsGodotDictionary();
                if (planetData.ContainsKey("milestones"))
                {
                    var arr = planetData["milestones"].AsGodotArray();
                    foreach (var item in arr)
                    {
                        var m = item.AsGodotDictionary();
                        milestones.Add(new MilestoneData
                        {
                            Wave = (int)m["wave"].AsInt64(),
                            Type = m["type"].AsString(),
                            Label = m["label"].AsString()
                        });
                    }
                }

                GD.Print($"[VineWaveLoader] Loaded {milestones.Count} milestones for planet {planet}");
            }
            catch (Exception e)
            {
                GD.PrintErr($"[VineWaveLoader] Failed to parse milestones.json: {e.Message}");
            }

            return milestones;
        }

        /// <summary>
        /// S2: Generate a procedural wave for wave numbers beyond hand-crafted data.
        /// Picks a random earlier wave as template and clones its surges.
        /// </summary>
        public static VineWaveData GenerateWave(int waveNumber, List<VineWaveData> handCrafted, RandomNumberGenerator rng)
        {
            if (handCrafted == null || handCrafted.Count == 0) return null;

            // Pick a random template from hand-crafted waves (avoid boss waves as templates)
            VineWaveData template = null;
            for (int attempts = 0; attempts < 10; attempts++)
            {
                int idx = rng.RandiRange(0, handCrafted.Count - 1);
                if (!handCrafted[idx].IsBossWave || attempts >= 9)
                {
                    template = handCrafted[idx];
                    break;
                }
            }
            if (template == null) template = handCrafted[0];

            var wave = new VineWaveData
            {
                WaveNumber = waveNumber,
                Name = $"Wave {waveNumber}",
                BonusResources = template.BonusResources,
                IsBossWave = false,
                CompletionMode = template.CompletionMode,
                CompletionTimer = template.CompletionTimer,
                CompletionKillCount = template.CompletionKillCount
            };

            foreach (var surge in template.Surges)
            {
                if (surge.IsBoss) continue; // Skip boss surges in procedural waves
                wave.Surges.Add(surge.Clone());
            }

            // Ensure at least one surge exists
            if (wave.Surges.Count == 0 && template.Surges.Count > 0)
                wave.Surges.Add(template.Surges[0].Clone());

            GD.Print($"[VineWaveLoader] Generated procedural wave {waveNumber} from template \"{template.Name}\"");
            return wave;
        }

        private static List<VineWaveData> LoadFromJson(string path, int planet)
        {
            try
            {
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                if (file == null) return null;

                string json = file.GetAsText();
                var planetData = JsonSerializer.Deserialize<JsonPlanetData>(json, _jsonOptions);
                if (planetData?.Waves == null) return null;

                var result = new List<VineWaveData>();
                foreach (var jw in planetData.Waves)
                {
                    var wave = new VineWaveData
                    {
                        WaveNumber = jw.WaveNumber,
                        Name = jw.Name ?? $"Wave {jw.WaveNumber}",
                        BonusResources = jw.BonusResources > 0 ? jw.BonusResources : jw.BonusScrap,
                        IsBossWave = jw.IsBossWave,
                        CompletionMode = ParseCompletionMode(jw.CompletionMode),
                        CompletionTimer = jw.CompletionTimer,
                        CompletionKillCount = jw.CompletionKillCount
                    };

                    if (jw.Surges != null)
                    {
                        foreach (var js in jw.Surges)
                        {
                            var surge = new SurgeData
                            {
                                EnemyName = js.EnemyName,
                                Faction = ParseFaction(js.Faction),
                                Health = js.Health,
                                Speed = js.Speed,
                                ResourceValue = js.ResourceValue > 0 ? js.ResourceValue : js.ScrapValue,
                                Color = GetFactionColor(ParseFaction(js.Faction)),
                                Count = js.Count,
                                SpawnInterval = js.SpawnInterval,
                                StartDelay = js.StartDelay,
                                EntryIndex = js.EntryIndex,
                                IsBoss = js.IsBoss,
                                SpawnJitter = js.SpawnJitter,
                                AttackRange = js.AttackRange,
                                AttackDamage = js.AttackDamage,
                                AttackInterval = js.AttackInterval
                            };

                            // Parse commander if present
                            if (js.Commander != null)
                            {
                                surge.Commander = new CommanderData
                                {
                                    EnemyName = js.Commander.EnemyName,
                                    Faction = ParseFaction(js.Commander.Faction),
                                    Health = js.Commander.Health,
                                    Speed = js.Commander.Speed,
                                    ResourceValue = js.Commander.ResourceValue,
                                    SpawnType = ParseSpawnType(js.Commander.SpawnType),
                                    SpawnChance = js.Commander.SpawnChance,
                                    ReactiveTrigger = js.Commander.ReactiveTrigger,
                                    ReactiveThreshold = js.Commander.ReactiveThreshold,
                                    Behavior = ParseBehavior(js.Commander.Behavior),
                                    AuraRadius = js.Commander.AuraRadius,
                                    AuraSpeedBuff = js.Commander.AuraSpeedBuff,
                                    AuraHPBuff = js.Commander.AuraHPBuff,
                                    TelegraphDuration = js.Commander.TelegraphDuration,
                                    IgnoreThreats = js.Commander.IgnoreThreats
                                };
                            }

                            wave.Surges.Add(surge);
                        }
                    }

                    result.Add(wave);
                }

                return result;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[VineWaveLoader] Failed to parse {path}: {e.Message}");
                return null;
            }
        }

        // ── Enum Parsers ──

        private static VineEnemyFaction ParseFaction(string s)
        {
            if (string.IsNullOrEmpty(s)) return VineEnemyFaction.Scavenger;
            return Enum.TryParse<VineEnemyFaction>(s, true, out var f) ? f : VineEnemyFaction.Scavenger;
        }

        private static WaveCompletionMode ParseCompletionMode(string s)
        {
            if (string.IsNullOrEmpty(s)) return WaveCompletionMode.KillAll;
            return Enum.TryParse<WaveCompletionMode>(s, true, out var m) ? m : WaveCompletionMode.KillAll;
        }

        private static CommanderSpawnType ParseSpawnType(string s)
        {
            if (string.IsNullOrEmpty(s)) return CommanderSpawnType.Scripted;
            return Enum.TryParse<CommanderSpawnType>(s, true, out var t) ? t : CommanderSpawnType.Scripted;
        }

        private static CommanderBehavior ParseBehavior(string s)
        {
            if (string.IsNullOrEmpty(s)) return CommanderBehavior.Elite;
            return Enum.TryParse<CommanderBehavior>(s, true, out var b) ? b : CommanderBehavior.Elite;
        }

        private static Color GetFactionColor(VineEnemyFaction faction)
        {
            return faction switch
            {
                VineEnemyFaction.Scavenger => TronTheme.EnemyScavenger,
                VineEnemyFaction.Brute => TronTheme.EnemyBrute,
                VineEnemyFaction.Ghost => TronTheme.EnemyGhost,
                VineEnemyFaction.Swarm => TronTheme.EnemySwarm,
                _ => TronTheme.EnemyScavenger
            };
        }

        // ── JSON DTOs ──

        private class JsonPlanetData
        {
            public string Address { get; set; }
            public int Planet { get; set; }
            public List<JsonWaveData> Waves { get; set; }
        }

        private class JsonWaveData
        {
            public int WaveNumber { get; set; }
            public string Name { get; set; }
            public int BonusResources { get; set; }
            [JsonPropertyName("bonusScrap")]
            public int BonusScrap { get; set; }  // Backward-compat
            public bool IsBossWave { get; set; }
            public string CompletionMode { get; set; }
            public float CompletionTimer { get; set; }
            public int CompletionKillCount { get; set; }
            public List<JsonSurgeData> Surges { get; set; }
        }

        private class JsonSurgeData
        {
            public string EnemyName { get; set; }
            public string Faction { get; set; }
            public float Health { get; set; }
            public float Speed { get; set; }
            public int ResourceValue { get; set; }
            [JsonPropertyName("scrapValue")]
            public int ScrapValue { get; set; }  // Backward-compat
            public int Count { get; set; }
            public float SpawnInterval { get; set; }
            public float StartDelay { get; set; }
            public int EntryIndex { get; set; }
            public bool IsBoss { get; set; }
            public float SpawnJitter { get; set; }
            public float AttackRange { get; set; }
            public float AttackDamage { get; set; }
            public float AttackInterval { get; set; }
            public JsonCommanderData Commander { get; set; }
        }

        private class JsonCommanderData
        {
            public string EnemyName { get; set; }
            public string Faction { get; set; }
            public float Health { get; set; }
            public float Speed { get; set; }
            public int ResourceValue { get; set; }
            public string SpawnType { get; set; }
            public float SpawnChance { get; set; } = 1f;
            public string ReactiveTrigger { get; set; }
            public string ReactiveThreshold { get; set; }
            public string Behavior { get; set; }
            public float AuraRadius { get; set; } = 15f;
            public float AuraSpeedBuff { get; set; } = 1f;
            public float AuraHPBuff { get; set; } = 1f;
            public float TelegraphDuration { get; set; } = 2.5f;
            public bool IgnoreThreats { get; set; } = true;
        }
    }
}
