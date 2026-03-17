using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Wave definitions for Vine Logic TD.
    /// Organized by floor with escalating difficulty across 3 floors + boss.
    /// </summary>
    public class VineSpawnGroup
    {
        public string EnemyName;
        public VineEnemyFaction Faction;
        public float Health;
        public float Speed;
        public int ScrapValue;
        public Color Color;
        public int Count;
        public float SpawnInterval;
        public float StartDelay;
        public int EntryIndex;  // -1 = random entry point
        public bool IsBoss;
    }

    public class VineWaveData
    {
        public int WaveNumber;
        public string Name;
        public List<VineSpawnGroup> Groups = new();
        public int BonusGold;
        public int Floor;
        public bool IsBossWave;
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

        // Backward-compat: get by global wave number (1-based)
        public static VineWaveData Get(int waveNumber)
        {
            var all = GetFloorMap();
            int idx = 0;
            for (int floor = 1; floor <= Constants.VINE_FLOOR_COUNT; floor++)
            {
                if (!all.ContainsKey(floor)) continue;
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
            for (int floor = 1; floor <= Constants.VINE_FLOOR_COUNT; floor++)
            {
                if (all.ContainsKey(floor))
                    result.AddRange(all[floor]);
            }
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
                    Floor = 1, WaveNumber = 1, Name = "First Contact", BonusGold = 10,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 20, Speed = 2.5f, ScrapValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 1.2f, StartDelay = 0, EntryIndex = 0
                        }
                    }
                },
                new VineWaveData {
                    Floor = 1, WaveNumber = 2, Name = "Swarm Alert", BonusGold = 12,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 10, Speed = 4f, ScrapValue = 1,
                            Color = TronTheme.EnemySwarm,
                            Count = 12, SpawnInterval = 0.4f, StartDelay = 0, EntryIndex = 0
                        }
                    }
                },
                new VineWaveData {
                    Floor = 1, WaveNumber = 3, Name = "Mixed Signals", BonusGold = 15,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 25, Speed = 2.5f, ScrapValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 1f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 80, Speed = 1.5f, ScrapValue = 10,
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
                    Floor = 2, WaveNumber = 1, Name = "Split Path", BonusGold = 12,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 30, Speed = 2.5f, ScrapValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 30, Speed = 2.5f, ScrapValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 3f, EntryIndex = 1
                        }
                    }
                },
                new VineWaveData {
                    Floor = 2, WaveNumber = 2, Name = "Phase Shift", BonusGold = 15,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 35, Speed = 3f, ScrapValue = 8,
                            Color = TronTheme.EnemyGhost,
                            Count = 6, SpawnInterval = 1.5f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 35, Speed = 3f, ScrapValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 0.8f, StartDelay = 4f, EntryIndex = 1
                        }
                    }
                },
                new VineWaveData {
                    Floor = 2, WaveNumber = 3, Name = "Full Spectrum", BonusGold = 18,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 40, Speed = 3f, ScrapValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 0.8f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 100, Speed = 1.5f, ScrapValue = 12,
                            Color = TronTheme.EnemyBrute,
                            Count = 3, SpawnInterval = 3f, StartDelay = 3f, EntryIndex = 1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 15, Speed = 4.5f, ScrapValue = 1,
                            Color = TronTheme.EnemySwarm,
                            Count = 15, SpawnInterval = 0.3f, StartDelay = 8f, EntryIndex = -1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 40, Speed = 3f, ScrapValue = 8,
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
                    Floor = 3, WaveNumber = 1, Name = "Three-Front", BonusGold = 15,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 45, Speed = 3f, ScrapValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 1f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 45, Speed = 3f, ScrapValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 2f, EntryIndex = 1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 45, Speed = 3f, ScrapValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 4f, EntryIndex = 2
                        }
                    }
                },
                new VineWaveData {
                    Floor = 3, WaveNumber = 2, Name = "Storm Protocol", BonusGold = 18,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 20, Speed = 4.5f, ScrapValue = 2,
                            Color = TronTheme.EnemySwarm,
                            Count = 10, SpawnInterval = 0.3f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 120, Speed = 1.5f, ScrapValue = 12,
                            Color = TronTheme.EnemyBrute,
                            Count = 3, SpawnInterval = 3f, StartDelay = 2f, EntryIndex = 1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 50, Speed = 3f, ScrapValue = 8,
                            Color = TronTheme.EnemyGhost,
                            Count = 4, SpawnInterval = 1.5f, StartDelay = 5f, EntryIndex = 2
                        }
                    }
                },
                new VineWaveData {
                    Floor = 3, WaveNumber = 3, Name = "Siege Breaker", BonusGold = 20,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 55, Speed = 3.5f, ScrapValue = 5,
                            Color = TronTheme.EnemyScavenger,
                            Count = 10, SpawnInterval = 0.6f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 150, Speed = 1.8f, ScrapValue = 15,
                            Color = TronTheme.EnemyBrute,
                            Count = 4, SpawnInterval = 3f, StartDelay = 3f, EntryIndex = 1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 60, Speed = 3.5f, ScrapValue = 10,
                            Color = TronTheme.EnemyGhost,
                            Count = 5, SpawnInterval = 1.5f, StartDelay = 6f, EntryIndex = 2
                        },
                        new VineSpawnGroup {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 25, Speed = 5f, ScrapValue = 2,
                            Color = TronTheme.EnemySwarm,
                            Count = 15, SpawnInterval = 0.25f, StartDelay = 10f, EntryIndex = -1
                        }
                    }
                },
                // ── BOSS WAVE ──
                new VineWaveData {
                    Floor = 3, WaveNumber = 4, Name = "APEX PROTOCOL", BonusGold = 50,
                    IsBossWave = true,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Apex Construct", Faction = VineEnemyFaction.Brute,
                            Health = 600, Speed = 1.2f, ScrapValue = Constants.BOSS_SCRAP_VALUE,
                            Color = TronTheme.BossGlow,
                            Count = 1, SpawnInterval = 0, StartDelay = 0, EntryIndex = 0,
                            IsBoss = true
                        },
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 35, Speed = 3f, ScrapValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 0.8f, StartDelay = 3f, EntryIndex = 1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 15, Speed = 4f, ScrapValue = 1,
                            Color = TronTheme.EnemySwarm,
                            Count = 8, SpawnInterval = 0.3f, StartDelay = 5f, EntryIndex = 2
                        }
                    }
                }
            };

            return map;
        }
    }
}
