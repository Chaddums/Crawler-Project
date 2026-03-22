using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Canonical hierarchy: Run > Planet > Floor > Wave > Surge > Enemy
    /// Address format: P#-F#-W#-S#
    /// </summary>

    // ── Completion Modes ──

    public enum WaveCompletionMode
    {
        KillAll,         // Default — all enemies dead
        Timer,           // Survive X seconds
        KillThreshold,   // Kill N of M enemies
        Hybrid           // Timer OR kill count, whichever fires first
    }

    // ── Commander System ──

    public enum CommanderSpawnType
    {
        Scripted,        // Always spawns with this surge
        Random,          // Chance-based per surge
        Reactive         // Triggered by game state (e.g. fast clear time)
    }

    public enum CommanderBehavior
    {
        Elite,           // Hard enemy, no special mechanic
        AuraBuffer,      // Passively buffs nearby units
        Rally,           // Calls reinforcements, changes aggro
        Assassin         // Ignores threats, b-lines to player
    }

    /// <summary>
    /// Commander definition — optional special enemy attached to a Surge.
    /// Not required for wave/surge completion. Spawn condition + behavior are independent.
    /// </summary>
    public class CommanderData
    {
        public string EnemyName;
        public VineEnemyFaction Faction;
        public float Health;
        public float Speed;
        public int ResourceValue;

        // Spawn condition
        public CommanderSpawnType SpawnType = CommanderSpawnType.Scripted;
        public float SpawnChance = 1f;           // For Random type (0-1)
        public string ReactiveTrigger;            // For Reactive type (e.g. "WaveClearTime")
        public string ReactiveThreshold;          // e.g. "under_30s"

        // Behavior
        public CommanderBehavior Behavior = CommanderBehavior.Elite;

        // AuraBuffer specifics
        public float AuraRadius = 15f;
        public float AuraSpeedBuff = 1f;          // Multiplier (1 = no buff)
        public float AuraHPBuff = 1f;

        // Assassin specifics
        public float TelegraphDuration = 2.5f;    // Warning before charge
        public bool IgnoreThreats = true;
    }

    // ── Floor Scaler ──

    /// <summary>
    /// Multipliers applied to all surges on a floor. Layers on top of base surge values.
    /// JSON key: "floorScaler" on the floor data file.
    /// </summary>
    public class FloorScaler
    {
        public float EnemyHP = 1f;
        public float EnemySpeed = 1f;
        public float EnemyCount = 1f;     // Multiplied then rounded
        public float ResourceValue = 1f;
    }

    // ── Surge Data ──

    /// <summary>
    /// A surge is a group of enemies within a wave. Dynamic count — never hardcode surge count.
    /// </summary>
    public class SurgeData
    {
        public string EnemyName;
        public VineEnemyFaction Faction;
        public float Health;
        public float Speed;
        public int ResourceValue;
        public Color Color;
        public int Count;
        public float SpawnInterval;
        public float StartDelay;
        public int EntryIndex;  // -1 = random entry point
        public bool IsBoss;
        public float SpawnJitter;  // Random +/- seconds on spawn interval

        // Ranged attack stats (0 = no ranged attack)
        public float AttackRange;
        public float AttackDamage;
        public float AttackInterval;

        // Accumulator-based spawning — when true, uses fractional spawn accumulation
        // instead of discrete timers. Produces smoother spawn pacing at high rates.
        public bool UseAccumulator;

        // Optional commander attached to this surge
        public CommanderData Commander;
    }

    // ── Wave Data ──

    public class VineWaveData
    {
        public int WaveNumber;
        public string Name;
        public List<SurgeData> Surges = new();
        public int BonusResources;
        public int Floor;
        public bool IsBossWave;
        public WaveCompletionMode CompletionMode = WaveCompletionMode.KillAll;

        // Timer/KillThreshold completion params
        public float CompletionTimer;       // Seconds for Timer/Hybrid mode
        public int CompletionKillCount;     // Kill count for KillThreshold/Hybrid mode
    }

    public static class VineWaveRegistry
    {
        private static Dictionary<int, List<VineWaveData>> _floorWaves;

        // Backward-compat: total wave count across all floors
        public static int WaveCount
        {
            get
            {
                var all = GetFloorMap();
                int total = 0;
                foreach (var waves in all.Values) total += waves.Count;
                return total;
            }
        }

        // S1: floors removed — iterate all entries in floor map
        public static VineWaveData Get(int waveNumber)
        {
            var all = GetFloorMap();
            int idx = 0;
            foreach (var floor in all.Keys)
            {
                foreach (var wave in all[floor])
                {
                    idx++;
                    if (idx == waveNumber) return wave;
                }
            }
            return null;
        }

        public static List<VineWaveData> GetAll()
        {
            var result = new List<VineWaveData>();
            var all = GetFloorMap();
            foreach (var waves in all.Values)
                result.AddRange(waves);
            return result;
        }

        public static int GetFloorWaveCount(int floor)
        {
            var all = GetFloorMap();
            return all.ContainsKey(floor) ? all[floor].Count : 0;
        }

        public static List<VineWaveData> GetFloorWaves(int floor)
        {
            var all = GetFloorMap();
            return all.ContainsKey(floor) ? all[floor] : new List<VineWaveData>();
        }

        public static VineWaveData GetFloorWave(int floor, int waveInFloor)
        {
            var waves = GetFloorWaves(floor);
            if (waveInFloor < 1 || waveInFloor > waves.Count) return null;
            return waves[waveInFloor - 1];
        }

        private static Dictionary<int, List<VineWaveData>> GetFloorMap()
        {
            if (_floorWaves != null) return _floorWaves;
            _floorWaves = BuildFloorWaves();
            return _floorWaves;
        }

        private static Dictionary<int, List<VineWaveData>> BuildFloorWaves()
        {
            var map = new Dictionary<int, List<VineWaveData>>();

            // ── Floor 1: Gateway (1 entry) ──
            map[1] = new List<VineWaveData>
            {
                new VineWaveData {
                    Floor = 1, WaveNumber = 1, Name = "First Contact", BonusResources = 10,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 20, Speed = 2.5f, ResourceValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 1.2f, StartDelay = 0, EntryIndex = 0
                        }
                    }
                },
                new VineWaveData {
                    Floor = 1, WaveNumber = 2, Name = "Swarm Alert", BonusResources = 12,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 10, Speed = 4f, ResourceValue = 1,
                            Color = TronTheme.EnemySwarm,
                            Count = 12, SpawnInterval = 0.4f, StartDelay = 0, EntryIndex = 0
                        }
                    }
                },
                new VineWaveData {
                    Floor = 1, WaveNumber = 3, Name = "Mixed Signals", BonusResources = 15,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 25, Speed = 2.5f, ResourceValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 1f, StartDelay = 0, EntryIndex = 0
                        },
                        new SurgeData {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 80, Speed = 1.5f, ResourceValue = 10,
                            Color = TronTheme.EnemyBrute,
                            Count = 2, SpawnInterval = 3f, StartDelay = 5f, EntryIndex = 0
                        }
                    }
                }
            };

            // ── Floor 2: Conduit (2 entries) ──
            map[2] = new List<VineWaveData>
            {
                new VineWaveData {
                    Floor = 2, WaveNumber = 1, Name = "Split Path", BonusResources = 12,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 30, Speed = 2.5f, ResourceValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 0, EntryIndex = 0
                        },
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 30, Speed = 2.5f, ResourceValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 3f, EntryIndex = 1
                        }
                    }
                },
                new VineWaveData {
                    Floor = 2, WaveNumber = 2, Name = "Phase Shift", BonusResources = 15,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 35, Speed = 3f, ResourceValue = 8,
                            Color = TronTheme.EnemyGhost,
                            Count = 6, SpawnInterval = 1.5f, StartDelay = 0, EntryIndex = 0
                        },
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 35, Speed = 3f, ResourceValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 0.8f, StartDelay = 4f, EntryIndex = 1
                        }
                    }
                },
                new VineWaveData {
                    Floor = 2, WaveNumber = 3, Name = "Full Spectrum", BonusResources = 18,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 40, Speed = 3f, ResourceValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 0.8f, StartDelay = 0, EntryIndex = 0
                        },
                        new SurgeData {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 100, Speed = 1.5f, ResourceValue = 12,
                            Color = TronTheme.EnemyBrute,
                            Count = 3, SpawnInterval = 3f, StartDelay = 3f, EntryIndex = 1
                        },
                        new SurgeData {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 15, Speed = 4.5f, ResourceValue = 1,
                            Color = TronTheme.EnemySwarm,
                            Count = 15, SpawnInterval = 0.3f, StartDelay = 8f, EntryIndex = -1
                        },
                        new SurgeData {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 40, Speed = 3f, ResourceValue = 8,
                            Color = TronTheme.EnemyGhost,
                            Count = 3, SpawnInterval = 2f, StartDelay = 12f, EntryIndex = 0
                        }
                    }
                }
            };

            // ── Floor 3: Arena (3 entries) ──
            map[3] = new List<VineWaveData>
            {
                new VineWaveData {
                    Floor = 3, WaveNumber = 1, Name = "Three-Front", BonusResources = 15,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 45, Speed = 3f, ResourceValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 1f, StartDelay = 0, EntryIndex = 0
                        },
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 45, Speed = 3f, ResourceValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 2f, EntryIndex = 1
                        },
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 45, Speed = 3f, ResourceValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 4f, EntryIndex = 2
                        }
                    }
                },
                new VineWaveData {
                    Floor = 3, WaveNumber = 2, Name = "Storm Protocol", BonusResources = 18,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 20, Speed = 4.5f, ResourceValue = 2,
                            Color = TronTheme.EnemySwarm,
                            Count = 10, SpawnInterval = 0.3f, StartDelay = 0, EntryIndex = 0
                        },
                        new SurgeData {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 120, Speed = 1.5f, ResourceValue = 12,
                            Color = TronTheme.EnemyBrute,
                            Count = 3, SpawnInterval = 3f, StartDelay = 2f, EntryIndex = 1
                        },
                        new SurgeData {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 50, Speed = 3f, ResourceValue = 8,
                            Color = TronTheme.EnemyGhost,
                            Count = 4, SpawnInterval = 1.5f, StartDelay = 5f, EntryIndex = 2
                        }
                    }
                },
                new VineWaveData {
                    Floor = 3, WaveNumber = 3, Name = "Siege Breaker", BonusResources = 20,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 55, Speed = 3.5f, ResourceValue = 5,
                            Color = TronTheme.EnemyScavenger,
                            Count = 10, SpawnInterval = 0.6f, StartDelay = 0, EntryIndex = 0
                        },
                        new SurgeData {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 150, Speed = 1.8f, ResourceValue = 15,
                            Color = TronTheme.EnemyBrute,
                            Count = 4, SpawnInterval = 3f, StartDelay = 3f, EntryIndex = 1
                        },
                        new SurgeData {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 60, Speed = 3.5f, ResourceValue = 10,
                            Color = TronTheme.EnemyGhost,
                            Count = 5, SpawnInterval = 1.5f, StartDelay = 6f, EntryIndex = 2
                        },
                        new SurgeData {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 25, Speed = 5f, ResourceValue = 2,
                            Color = TronTheme.EnemySwarm,
                            Count = 15, SpawnInterval = 0.25f, StartDelay = 10f, EntryIndex = -1
                        }
                    }
                },
                // ── BOSS WAVE ──
                new VineWaveData {
                    Floor = 3, WaveNumber = 4, Name = "APEX PROTOCOL", BonusResources = 50,
                    IsBossWave = true,
                    Surges = {
                        new SurgeData {
                            EnemyName = "Apex Construct", Faction = VineEnemyFaction.Brute,
                            Health = 600, Speed = 1.2f, ResourceValue = Constants.BOSS_RESOURCE_VALUE,
                            Color = TronTheme.BossGlow,
                            Count = 1, SpawnInterval = 0, StartDelay = 0, EntryIndex = 0,
                            IsBoss = true
                        },
                        new SurgeData {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 35, Speed = 3f, ResourceValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 0.8f, StartDelay = 3f, EntryIndex = 1
                        },
                        new SurgeData {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 15, Speed = 4f, ResourceValue = 1,
                            Color = TronTheme.EnemySwarm,
                            Count = 8, SpawnInterval = 0.3f, StartDelay = 5f, EntryIndex = 2
                        }
                    }
                }
            };

            // ── Floor 4: Forge (2 entries, breather) ──
            map[4] = new List<VineWaveData>
            {
                new VineWaveData { Floor = 4, WaveNumber = 1, Name = "Forgefire", BonusResources = 18,
                    Surges = {
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 55, Speed = 2f, ResourceValue = 6, Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 1.2f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 55, Speed = 2f, ResourceValue = 6, Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 5f, EntryIndex = 1 },
                    }
                },
                new VineWaveData { Floor = 4, WaveNumber = 2, Name = "Molten Rush", BonusResources = 20,
                    Surges = {
                        new SurgeData { EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 28, Speed = 3f, ResourceValue = 5, Color = TronTheme.EnemySwarm,
                            Count = 8, SpawnInterval = 0.4f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 90, Speed = 1.1f, ResourceValue = 10, Color = TronTheme.EnemyBrute,
                            Count = 3, SpawnInterval = 3f, StartDelay = 6f, EntryIndex = 1 },
                    }
                },
                new VineWaveData { Floor = 4, WaveNumber = 3, Name = "Tempered Steel", BonusResources = 22,
                    Surges = {
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 60, Speed = 2.2f, ResourceValue = 7, Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 0.8f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 55, Speed = 2.2f, ResourceValue = 9, Color = TronTheme.EnemyGhost,
                            Count = 4, SpawnInterval = 1.5f, StartDelay = 8f, EntryIndex = 1 },
                        new SurgeData { EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 30, Speed = 3.2f, ResourceValue = 5, Color = TronTheme.EnemySwarm,
                            Count = 10, SpawnInterval = 0.3f, StartDelay = 12f, EntryIndex = -1 },
                    }
                },
            };

            // ── Floor 5: Labyrinth (3 entries, dense) ──
            map[5] = new List<VineWaveData>
            {
                new VineWaveData { Floor = 5, WaveNumber = 1, Name = "Maze Runners", BonusResources = 20,
                    Surges = {
                        new SurgeData { EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 70, Speed = 2.2f, ResourceValue = 10, Color = TronTheme.EnemyGhost,
                            Count = 5, SpawnInterval = 1.5f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 75, Speed = 2.2f, ResourceValue = 8, Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 1f, StartDelay = 5f, EntryIndex = 1 },
                        new SurgeData { EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 35, Speed = 3f, ResourceValue = 6, Color = TronTheme.EnemySwarm,
                            Count = 8, SpawnInterval = 0.4f, StartDelay = 10f, EntryIndex = 2 },
                    }
                },
                new VineWaveData { Floor = 5, WaveNumber = 2, Name = "Dead Ends", BonusResources = 25,
                    Surges = {
                        new SurgeData { EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 120, Speed = 1.2f, ResourceValue = 14, Color = TronTheme.EnemyBrute,
                            Count = 4, SpawnInterval = 3f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 80, Speed = 2.3f, ResourceValue = 9, Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 0.8f, StartDelay = 4f, EntryIndex = 1 },
                        new SurgeData { EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 75, Speed = 2.3f, ResourceValue = 11, Color = TronTheme.EnemyGhost,
                            Count = 5, SpawnInterval = 1.5f, StartDelay = 10f, EntryIndex = 2 },
                    }
                },
                new VineWaveData { Floor = 5, WaveNumber = 3, Name = "No Escape", BonusResources = 28,
                    Surges = {
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 85, Speed = 2.5f, ResourceValue = 9, Color = TronTheme.EnemyScavenger,
                            Count = 10, SpawnInterval = 0.6f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 140, Speed = 1.3f, ResourceValue = 15, Color = TronTheme.EnemyBrute,
                            Count = 4, SpawnInterval = 3f, StartDelay = 5f, EntryIndex = 1 },
                        new SurgeData { EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 80, Speed = 2.5f, ResourceValue = 12, Color = TronTheme.EnemyGhost,
                            Count = 6, SpawnInterval = 1.2f, StartDelay = 9f, EntryIndex = 2 },
                        new SurgeData { EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 40, Speed = 3.5f, ResourceValue = 6, Color = TronTheme.EnemySwarm,
                            Count = 12, SpawnInterval = 0.25f, StartDelay = 14f, EntryIndex = -1 },
                    }
                },
            };

            // ── Floor 6: Crucible (4 entries, major boss) ──
            map[6] = new List<VineWaveData>
            {
                new VineWaveData { Floor = 6, WaveNumber = 1, Name = "Cardinal Assault", BonusResources = 25,
                    Surges = {
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 90, Speed = 2.2f, ResourceValue = 8, Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 1f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 90, Speed = 2.2f, ResourceValue = 8, Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 3f, EntryIndex = 1 },
                        new SurgeData { EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 150, Speed = 1.2f, ResourceValue = 14, Color = TronTheme.EnemyBrute,
                            Count = 3, SpawnInterval = 3f, StartDelay = 7f, EntryIndex = 2 },
                        new SurgeData { EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 42, Speed = 3.2f, ResourceValue = 6, Color = TronTheme.EnemySwarm,
                            Count = 8, SpawnInterval = 0.3f, StartDelay = 12f, EntryIndex = 3 },
                    }
                },
                new VineWaveData { Floor = 6, WaveNumber = 2, Name = "Final Approach", BonusResources = 28,
                    Surges = {
                        new SurgeData { EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 160, Speed = 1.3f, ResourceValue = 15, Color = TronTheme.EnemyBrute,
                            Count = 4, SpawnInterval = 2.5f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 90, Speed = 2.5f, ResourceValue = 13, Color = TronTheme.EnemyGhost,
                            Count = 5, SpawnInterval = 1.5f, StartDelay = 4f, EntryIndex = 1 },
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 100, Speed = 2.5f, ResourceValue = 10, Color = TronTheme.EnemyScavenger,
                            Count = 10, SpawnInterval = 0.6f, StartDelay = 8f, EntryIndex = 2 },
                        new SurgeData { EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 45, Speed = 3.5f, ResourceValue = 7, Color = TronTheme.EnemySwarm,
                            Count = 12, SpawnInterval = 0.25f, StartDelay = 13f, EntryIndex = 3 },
                    }
                },
                new VineWaveData { Floor = 6, WaveNumber = 3, Name = "Last Stand", BonusResources = 30,
                    Surges = {
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 110, Speed = 2.5f, ResourceValue = 10, Color = TronTheme.EnemyScavenger,
                            Count = 10, SpawnInterval = 0.5f, StartDelay = 0, EntryIndex = 0 },
                        new SurgeData { EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 180, Speed = 1.3f, ResourceValue = 16, Color = TronTheme.EnemyBrute,
                            Count = 5, SpawnInterval = 2.5f, StartDelay = 4f, EntryIndex = 1 },
                        new SurgeData { EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 100, Speed = 2.5f, ResourceValue = 14, Color = TronTheme.EnemyGhost,
                            Count = 6, SpawnInterval = 1.2f, StartDelay = 8f, EntryIndex = 2 },
                        new SurgeData { EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 50, Speed = 3.5f, ResourceValue = 7, Color = TronTheme.EnemySwarm,
                            Count = 15, SpawnInterval = 0.2f, StartDelay = 13f, EntryIndex = 3 },
                    }
                },
                // ── MAJOR BOSS WAVE ──
                new VineWaveData { Floor = 6, WaveNumber = 4, Name = "APEX PROTOCOL", BonusResources = 50,
                    IsBossWave = true,
                    Surges = {
                        new SurgeData { EnemyName = "Apex Construct", Faction = VineEnemyFaction.Brute,
                            Health = 1200, Speed = 0.7f, ResourceValue = 100, Color = TronTheme.BossGlow,
                            Count = 1, SpawnInterval = 0, StartDelay = 0, EntryIndex = 0, IsBoss = true },
                        new SurgeData { EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 80, Speed = 2.5f, ResourceValue = 8, Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 0.6f, StartDelay = 4f, EntryIndex = 1 },
                        new SurgeData { EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 120, Speed = 1.2f, ResourceValue = 12, Color = TronTheme.EnemyBrute,
                            Count = 3, SpawnInterval = 3f, StartDelay = 8f, EntryIndex = 2 },
                        new SurgeData { EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 40, Speed = 3.5f, ResourceValue = 6, Color = TronTheme.EnemySwarm,
                            Count = 10, SpawnInterval = 0.3f, StartDelay = 12f, EntryIndex = 3 },
                    }
                },
            };

            return map;
        }
    }
}
