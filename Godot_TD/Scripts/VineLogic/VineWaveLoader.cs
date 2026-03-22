using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Loads wave/surge data from JSON files using P#-F# address format.
    /// Falls back to hardcoded VineWaveRegistry if JSON not found.
    /// JSON location: Data/Waves/P{planet}-F{floor}.json
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
        /// Load waves for a specific planet+floor. Tries JSON first, falls back to hardcoded.
        /// </summary>
        public static List<VineWaveData> LoadFloorWaves(int planet, int floor)
        {
            string address = $"P{planet}-F{floor}";
            string path = $"res://Data/Waves/{address}.json";

            if (Godot.FileAccess.FileExists(path))
            {
                var waves = LoadFromJson(path, planet, floor);
                if (waves != null && waves.Count > 0)
                {
                    GD.Print($"[VineWaveLoader] {address}: loaded {waves.Count} waves from JSON");
                    return waves;
                }
                GD.PushWarning($"[VineWaveLoader] {address}: JSON found but failed to parse, using hardcoded fallback");
            }

            // Fallback to hardcoded registry
            var fallback = VineWaveRegistry.GetFloorWaves(floor);
            GD.Print($"[VineWaveLoader] {address}: using hardcoded fallback ({fallback.Count} waves)");
            return fallback;
        }

        private static List<VineWaveData> LoadFromJson(string path, int planet, int floor)
        {
            try
            {
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                if (file == null) return null;

                string json = file.GetAsText();
                var floorData = JsonSerializer.Deserialize<JsonFloorData>(json, _jsonOptions);
                if (floorData?.Waves == null) return null;

                var result = new List<VineWaveData>();
                foreach (var jw in floorData.Waves)
                {
                    var wave = new VineWaveData
                    {
                        WaveNumber = jw.WaveNumber,
                        Name = jw.Name ?? $"Wave {jw.WaveNumber}",
                        BonusResources = jw.BonusResources,
                        Floor = floor,
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
                                ResourceValue = js.ResourceValue,
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

                    // Apply floor scaler if present
                    if (floorData.FloorScaler != null)
                        ApplyFloorScaler(wave, floorData.FloorScaler);

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

        private static void ApplyFloorScaler(VineWaveData wave, JsonFloorScaler scaler)
        {
            foreach (var surge in wave.Surges)
            {
                if (scaler.EnemyHP != 1f)
                    surge.Health *= scaler.EnemyHP;
                if (scaler.EnemySpeed != 1f)
                    surge.Speed *= scaler.EnemySpeed;
                if (scaler.EnemyCount != 1f)
                    surge.Count = Mathf.RoundToInt(surge.Count * scaler.EnemyCount);
                if (scaler.ResourceValue != 1f)
                    surge.ResourceValue = Mathf.RoundToInt(surge.ResourceValue * scaler.ResourceValue);
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

        private class JsonFloorData
        {
            public string Address { get; set; }
            public int Planet { get; set; }
            public int Floor { get; set; }
            public JsonFloorScaler FloorScaler { get; set; }
            public List<JsonWaveData> Waves { get; set; }
        }

        private class JsonFloorScaler
        {
            public float EnemyHP { get; set; } = 1f;
            public float EnemySpeed { get; set; } = 1f;
            public float EnemyCount { get; set; } = 1f;
            public float ResourceValue { get; set; } = 1f;
        }

        private class JsonWaveData
        {
            public int WaveNumber { get; set; }
            public string Name { get; set; }
            public int BonusResources { get; set; }
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
