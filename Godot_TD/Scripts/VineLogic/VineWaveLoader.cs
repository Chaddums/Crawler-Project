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
                // Planets without their own milestones use planet 1's (Scrapyard had none at all,
                // so its runs never offered a perk)
                if (!planets.ContainsKey(planetKey)) planetKey = "1";
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
                            Label = m["label"].AsString(),
                            MetaPoints = m.ContainsKey("metaPoints") ? (int)m["metaPoints"].AsInt64() : 0
                        });
                    }
                }

                // Repeating milestones for continuous play. repeatAfter/repeatInterval/repeatType
                // were in the JSON but ignored, so there were no perks after wave 20.
                if (planetData.ContainsKey("repeatAfter") && planetData.ContainsKey("repeatInterval"))
                {
                    int after = (int)planetData["repeatAfter"].AsInt64();
                    int interval = (int)planetData["repeatInterval"].AsInt64();
                    string type = planetData.ContainsKey("repeatType") ? planetData["repeatType"].AsString() : "perk_select";
                    if (interval > 0)
                    {
                        for (int w = after + interval; w <= MaxRepeatMilestoneWave; w += interval)
                            milestones.Add(new MilestoneData { Wave = w, Type = type, Label = $"MILESTONE: Wave {w}" });
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

        /// <summary>Repeating milestones are generated up to this wave.</summary>
        private const int MaxRepeatMilestoneWave = 500;

        /// <summary>How many of the last authored waves procedural waves are templated from.</summary>
        private const int ProceduralTemplatePool = 5;

        /// <summary>
        /// S2: Generate a procedural wave for wave numbers beyond hand-crafted data.
        /// Templates from the last few authored waves; counts and speed grow by the linear
        /// wave scaling in difficulty_scaling.json (counts capped, the excess folded into
        /// health), and deep waves gain traits and commanders. Health itself is the climb's
        /// (Climb.Hp), applied to every wave when it is sent, so it is not scaled here.
        /// </summary>
        public static VineWaveData GenerateWave(int waveNumber, List<VineWaveData> handCrafted, RandomNumberGenerator rng)
        {
            if (handCrafted == null || handCrafted.Count == 0) return null;

            // Pick a random template from the last few authored waves (avoid boss waves)
            int first = Math.Max(0, handCrafted.Count - ProceduralTemplatePool);
            VineWaveData template = null;
            int templateIdx = handCrafted.Count - 1;
            for (int attempts = 0; attempts < 10; attempts++)
            {
                int idx = rng.RandiRange(first, handCrafted.Count - 1);
                if (!handCrafted[idx].IsBossWave || attempts >= 9)
                {
                    template = handCrafted[idx];
                    templateIdx = idx;
                    break;
                }
            }
            if (template == null) template = handCrafted[templateIdx];

            int templateWave = template.WaveNumber > 0 ? template.WaveNumber : templateIdx + 1;
            float hpMult = 1f;
            // Every template carries the pool's mean health budget (count x health over its
            // surges): templates differed by up to four times, so the wave after a gentle one
            // could be four times as hard and a defence went from no leaks to twenty in a wave
            float mean = 0f; int pooled = 0;
            for (int i = first; i < handCrafted.Count; i++)
                if (!handCrafted[i].IsBossWave) { mean += Budget(handCrafted[i]); pooled++; }
            float own = Budget(template);
            if (pooled > 0 && own > 0f) hpMult *= Mathf.Clamp(mean / pooled / own, 0.5f, 2f);
            float speedMult = Mathf.Min(WaveScaling.Speed(waveNumber) / WaveScaling.Speed(templateWave), WaveScaling.SpeedCap);
            float countMult = WaveScaling.Count(waveNumber) / WaveScaling.Count(templateWave);
            // Counts stop at the cap; the rest of the curve goes into health, so a deep wave is
            // a few hard enemies rather than two hundred that sink the frame rate
            if (countMult > WaveScaling.CountCap)
            {
                hpMult *= countMult / WaveScaling.CountCap;
                countMult = WaveScaling.CountCap;
            }
            float dmgMult = 1f;
            int past = Math.Max(0, waveNumber - WaveScaling.RampStart);
            float traitChance = Math.Min(WaveScaling.TraitChanceMax, past * WaveScaling.TraitChancePerWave);
            float commanderChance = Math.Min(WaveScaling.CommanderChanceMax, past * WaveScaling.CommanderChancePerWave);

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
                wave.Surges.Add(Deepen(ScaleSurge(surge.Clone(), hpMult, speedMult, countMult, dmgMult), traitChance, rng));
            }
            // Commanders: the expected number for the depth, every wave (a roll per surge gave
            // one wave none and the next four, and four elites with rallied packs are a wall)
            int commanders = Mathf.RoundToInt(commanderChance * wave.Surges.Count);
            var open = wave.Surges.FindAll(x => x.Commander == null);
            for (int i = 0; i < commanders && open.Count > 0; i++)
            {
                var sg = open[rng.RandiRange(0, open.Count - 1)];
                open.Remove(sg);
                GiveCommander(sg, rng);
            }

            // Ensure at least one surge exists
            if (wave.Surges.Count == 0 && template.Surges.Count > 0)
                wave.Surges.Add(ScaleSurge(template.Surges[0].Clone(), hpMult, speedMult, countMult, dmgMult));

            // A deep wave always needs more than one answer: at least two of armour, shields and
            // flight among its surges, so no single tower type covers it
            EnsureMixed(wave, rng);

            GD.Print($"[VineWaveLoader] P?-W{waveNumber} procedural from \"{template.Name}\" (W{templateWave}): " +
                     $"hp x{hpMult:F2}, damage x{dmgMult:F2}, speed x{speedMult:F2}, count x{countMult:F2}");
            return wave;
        }

        private static SurgeData ScaleSurge(SurgeData s, float hp, float speed, float count, float damage = 1f)
        {
            s.Health *= hp;
            s.Speed *= speed;
            s.Count = Math.Max(1, (int)Math.Round(s.Count * count));
            // A siege surge stops at a few guns: the rest of its growth goes into their health
            if ((s.Traits & EnemyTraits.Siege) != 0 && s.Count > Siege.MaxPerSurge)
            {
                s.Health *= (float)s.Count / Siege.MaxPerSurge;
                s.Count = Siege.MaxPerSurge;
            }
            s.AttackDamage *= damage;
            if (s.Commander != null)
            {
                // Commanders kept their authored health (200 at W140): scale a copy of theirs too
                var c = (CommanderData)s.Commander.MemberwiseCloneCommander();
                c.Health *= hp;
                c.Speed *= Mathf.Min(speed, 1.2f);
                s.Commander = c;
            }
            return s;
        }

        private static void EnsureMixed(VineWaveData wave, RandomNumberGenerator rng)
        {
            var have = EnemyTraits.None;
            foreach (var s in wave.Surges) have |= s.Traits & (EnemyTraits.Armoured | EnemyTraits.Shielded | EnemyTraits.Flying);
            int kinds = 0;
            foreach (var t in new[] { EnemyTraits.Armoured, EnemyTraits.Shielded, EnemyTraits.Flying }) if ((have & t) != 0) kinds++;
            for (int guard = 0; kinds < 2 && guard < 6; guard++)
            {
                // Give a plain surge the trait the wave lacks (flight only for the light ones)
                var plain = wave.Surges.FindAll(s => (s.Traits & (EnemyTraits.Armoured | EnemyTraits.Shielded | EnemyTraits.Flying)) == 0);
                if (plain.Count == 0) plain = wave.Surges;
                var s = plain[rng.RandiRange(0, plain.Count - 1)];
                var options = new List<EnemyTraits>();
                // Flyers stay plain: every anti-air hit is light or electric, so armoured flyers were
                // the one group nothing answered (the wall at W33 in autoplay), and shielded flyers
                // stood at 0.4 against 28 Tesla Coils while the rest of the wave read 1.0
                bool flies = (s.Traits & EnemyTraits.Flying) != 0;
                bool covered = (s.Traits & (EnemyTraits.Armoured | EnemyTraits.Shielded)) != 0;
                if ((have & EnemyTraits.Armoured) == 0 && !flies) options.Add(EnemyTraits.Armoured);
                if ((have & EnemyTraits.Shielded) == 0 && !flies) options.Add(EnemyTraits.Shielded);
                if ((have & EnemyTraits.Flying) == 0 && !covered && s.Faction is VineEnemyFaction.Swarm or VineEnemyFaction.Scavenger) options.Add(EnemyTraits.Flying);
                if (options.Count == 0) continue;
                var add = options[rng.RandiRange(0, options.Count - 1)];
                s.Traits |= add;
                have |= add;
                kinds++;
            }
        }

        private static readonly string[] CommanderNames = { "Siege Marshal", "Iron Warden", "Rust Tyrant", "Grid Enforcer" };

        /// <summary>A wave's health budget: count x health over its surges (bosses aside).</summary>
        private static float Budget(VineWaveData w)
        {
            float b = 0f;
            foreach (var sg in w.Surges) if (!sg.IsBoss) b += sg.Count * sg.Health;
            return b;
        }

        /// <summary>
        /// Deep waves mix their counters: a surge may gain a trait it didn't have (armour, a
        /// shield, or flight for the light ones).
        /// </summary>
        private static SurgeData Deepen(SurgeData s, float traitChance, RandomNumberGenerator rng)
        {
            if (traitChance > 0f && rng.Randf() < traitChance)
            {
                var options = new List<EnemyTraits>();
                // Flyers stay plain (see EnsureMixed)
                bool flies = (s.Traits & EnemyTraits.Flying) != 0;
                bool armoured = (s.Traits & EnemyTraits.Armoured) != 0, shielded = (s.Traits & EnemyTraits.Shielded) != 0;
                if (!armoured && !flies) options.Add(EnemyTraits.Armoured);
                if (!shielded && !flies) options.Add(EnemyTraits.Shielded);
                if (!flies && !armoured && !shielded && s.Faction is VineEnemyFaction.Swarm or VineEnemyFaction.Scavenger)
                    options.Add(EnemyTraits.Flying);
                if (options.Count > 0) s.Traits |= options[rng.RandiRange(0, options.Count - 1)];
            }
            return s;
        }

        /// <summary>An elite commander for a surge (six times a member's health, rallies its pack).</summary>
        private static void GiveCommander(SurgeData s, RandomNumberGenerator rng)
        {
            s.Commander = new CommanderData
            {
                EnemyName = CommanderNames[rng.RandiRange(0, CommanderNames.Length - 1)],
                Faction = s.Faction == VineEnemyFaction.Swarm ? VineEnemyFaction.Brute : s.Faction,
                Health = s.Health * 6f,
                Speed = s.Speed * 0.9f,
                ResourceValue = Math.Max(10, s.ResourceValue * 3),
                SpawnType = CommanderSpawnType.Scripted,
                Behavior = CommanderBehavior.Elite,
            };
        }

        /// <summary>
        /// Linear per-wave scaling from difficulty_scaling.json (wave_hp/speed/count_scale),
        /// read directly so procedural waves scale without a DifficultyScaler node.
        /// </summary>
        /// <summary>The climb's health multiplier at a wave (1 at wave 1): strikes and thorns grow with it.</summary>
        public static float RampAt(int wave) => Climb.Hp(Math.Max(1, wave));

        private static class WaveScaling
        {
            private static bool _loaded;
            private static float _hp = 0.04f, _speed = 0.005f, _count = 0.03f;
            // The steep ramp past the authored waves ("ramp" in difficulty_scaling.json)
            public static int RampStart = 20;
            public static float CountCap = 1.6f, SpeedCap = 1.35f;
            public static float TraitChancePerWave = 0.04f, TraitChanceMax = 0.7f;
            public static float CommanderChancePerWave = 0.03f, CommanderChanceMax = 0.5f;

            public static float Hp(int wave) { Load(); return 1f + (wave - 1) * _hp; }
            public static float Speed(int wave) { Load(); return 1f + (wave - 1) * _speed; }
            public static float Count(int wave) { Load(); return 1f + (wave - 1) * _count; }

            private static void Load()
            {
                if (_loaded) return;
                _loaded = true;
                const string path = "res://Data/difficulty_scaling.json";
                if (!Godot.FileAccess.FileExists(path)) return;
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                var parsed = Json.ParseString(file.GetAsText());
                if (parsed.VariantType != Variant.Type.Dictionary) return;
                var d = parsed.AsGodotDictionary();
                if (d.ContainsKey("wave_hp_scale")) _hp = (float)d["wave_hp_scale"].AsDouble();
                if (d.ContainsKey("wave_speed_scale")) _speed = (float)d["wave_speed_scale"].AsDouble();
                if (d.ContainsKey("wave_count_scale")) _count = (float)d["wave_count_scale"].AsDouble();
                if (d.ContainsKey("ramp") && d["ramp"].VariantType == Variant.Type.Dictionary)
                {
                    var r = d["ramp"].AsGodotDictionary();
                    float F(string k, float v) => r.ContainsKey(k) ? (float)r[k].AsDouble() : v;
                    RampStart = (int)F("start_wave", RampStart);
                    CountCap = F("count_cap", CountCap);
                    SpeedCap = F("speed_cap", SpeedCap);
                    TraitChancePerWave = F("extra_trait_chance_per_wave", TraitChancePerWave);
                    TraitChanceMax = F("extra_trait_chance_max", TraitChanceMax);
                    CommanderChancePerWave = F("commander_chance_per_wave", CommanderChancePerWave);
                    CommanderChanceMax = F("commander_chance_max", CommanderChanceMax);
                }
            }
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
                                AttackInterval = js.AttackInterval,
                                Traits = VineEnemy.ParseTraits(js.Traits)
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
            public string[] Traits { get; set; }
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
