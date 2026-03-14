using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// Defines a single spawn group within a wave.
    /// </summary>
    public class SpawnGroup
    {
        public EnemyType EnemyType;
        public int Count;
        public float SpawnInterval;   // Seconds between each spawn
        public float StartDelay;      // Delay before this group starts
        public int SpawnPointIndex;   // Which spawn point to use (-1 = random)
        public EnemyTier Tier;
    }

    /// <summary>
    /// Defines a complete wave — one or more spawn groups.
    /// </summary>
    public class WaveData
    {
        public int WaveNumber;
        public string Name;
        public List<SpawnGroup> Groups = new();
        public int BonusScrap;        // Awarded on wave clear
    }

    /// <summary>
    /// Pre-built wave definitions for testing. Will eventually load from JSON.
    /// </summary>
    public static class WaveRegistry
    {
        public static List<WaveData> BuildDefaultWaves()
        {
            var waves = new List<WaveData>();

            // Wave 1: Intro — just scrap rats
            waves.Add(new WaveData {
                WaveNumber = 1, Name = "Incoming Scrap",
                BonusScrap = 10,
                Groups = { new SpawnGroup {
                    EnemyType = EnemyType.ScrapRat, Count = 8,
                    SpawnInterval = 1f, StartDelay = 0f, SpawnPointIndex = 0
                }}
            });

            // Wave 2: Two lanes
            waves.Add(new WaveData {
                WaveNumber = 2, Name = "Two Front Assault",
                BonusScrap = 15,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.ScrapRat, Count = 6,
                        SpawnInterval = 0.8f, StartDelay = 0f, SpawnPointIndex = 0
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.ScrapRat, Count = 6,
                        SpawnInterval = 0.8f, StartDelay = 2f, SpawnPointIndex = 1
                    }
                }
            });

            // Wave 3: Introduce wire worms
            waves.Add(new WaveData {
                WaveNumber = 3, Name = "Burrowers",
                BonusScrap = 20,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.ScrapRat, Count = 10,
                        SpawnInterval = 0.6f, StartDelay = 0f, SpawnPointIndex = -1
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.WireWorm, Count = 3,
                        SpawnInterval = 2f, StartDelay = 3f, SpawnPointIndex = 0
                    }
                }
            });

            // Wave 4: First tanky enemy
            waves.Add(new WaveData {
                WaveNumber = 4, Name = "Heavy Metal",
                BonusScrap = 25,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.ScrapRat, Count = 8,
                        SpawnInterval = 0.5f, StartDelay = 0f, SpawnPointIndex = 0
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.RustHulk, Count = 2,
                        SpawnInterval = 3f, StartDelay = 1f, SpawnPointIndex = 1
                    }
                }
            });

            // Wave 5: Air wave
            waves.Add(new WaveData {
                WaveNumber = 5, Name = "Sky Junk",
                BonusScrap = 30,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.SparkDrone, Count = 8,
                        SpawnInterval = 0.7f, StartDelay = 0f, SpawnPointIndex = -1
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.WireWorm, Count = 4,
                        SpawnInterval = 1.5f, StartDelay = 2f, SpawnPointIndex = 0
                    }
                }
            });

            // Wave 6: Thief wave — steals your scrap piles
            waves.Add(new WaveData {
                WaveNumber = 6, Name = "Scavenger Run",
                BonusScrap = 25,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.ScrapThief, Count = 6,
                        SpawnInterval = 1f, StartDelay = 0f, SpawnPointIndex = -1
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.ScrapRat, Count = 12,
                        SpawnInterval = 0.4f, StartDelay = 3f, SpawnPointIndex = -1
                    }
                }
            });

            // Wave 7: Shield wall + DPS behind it
            waves.Add(new WaveData {
                WaveNumber = 7, Name = "Shield Wall",
                BonusScrap = 35,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.ShieldBearer, Count = 3,
                        SpawnInterval = 2f, StartDelay = 0f, SpawnPointIndex = 0
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.ScrapRat, Count = 15,
                        SpawnInterval = 0.3f, StartDelay = 4f, SpawnPointIndex = 0
                    }
                }
            });

            // Wave 8: Bombers — threaten your towers
            waves.Add(new WaveData {
                WaveNumber = 8, Name = "Demolition Crew",
                BonusScrap = 35,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.Bomber, Count = 5,
                        SpawnInterval = 2f, StartDelay = 0f, SpawnPointIndex = -1
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.RustHulk, Count = 3,
                        SpawnInterval = 3f, StartDelay = 2f, SpawnPointIndex = 0
                    }
                }
            });

            // Wave 9: Fabricator — Pillar #5 in action
            waves.Add(new WaveData {
                WaveNumber = 9, Name = "Adaptive Swarm",
                BonusScrap = 40,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.Fabricator, Count = 2,
                        SpawnInterval = 5f, StartDelay = 0f, SpawnPointIndex = 0
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.ScrapRat, Count = 20,
                        SpawnInterval = 0.3f, StartDelay = 1f, SpawnPointIndex = -1
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.WireWorm, Count = 5,
                        SpawnInterval = 1.5f, StartDelay = 5f, SpawnPointIndex = 1
                    }
                }
            });

            // Wave 10: Everything at once
            waves.Add(new WaveData {
                WaveNumber = 10, Name = "Junkyard Blitz",
                BonusScrap = 50,
                Groups = {
                    new SpawnGroup {
                        EnemyType = EnemyType.RustHulk, Count = 4,
                        SpawnInterval = 2f, StartDelay = 0f, SpawnPointIndex = 0
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.SparkDrone, Count = 10,
                        SpawnInterval = 0.5f, StartDelay = 2f, SpawnPointIndex = -1
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.ShieldBearer, Count = 3,
                        SpawnInterval = 3f, StartDelay = 0f, SpawnPointIndex = 1
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.Fabricator, Count = 2,
                        SpawnInterval = 4f, StartDelay = 5f, SpawnPointIndex = 0
                    },
                    new SpawnGroup {
                        EnemyType = EnemyType.Bomber, Count = 4,
                        SpawnInterval = 2f, StartDelay = 8f, SpawnPointIndex = -1
                    }
                }
            });

            return waves;
        }
    }
}
