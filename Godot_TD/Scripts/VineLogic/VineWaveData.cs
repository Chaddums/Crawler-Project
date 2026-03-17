using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Wave definitions for Vine Logic TD.
    /// Early waves test core mechanics; later waves introduce faction-specific pressure.
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
    }

    public class VineWaveData
    {
        public int WaveNumber;
        public string Name;
        public List<VineSpawnGroup> Groups = new();
        public int BonusGold;
    }

    public static class VineWaveRegistry
    {
        private static List<VineWaveData> _waves;

        public static int WaveCount => GetAll().Count;

        public static VineWaveData Get(int waveNumber)
        {
            var all = GetAll();
            if (waveNumber < 1 || waveNumber > all.Count) return null;
            return all[waveNumber - 1];
        }

        public static List<VineWaveData> GetAll()
        {
            if (_waves != null) return _waves;
            _waves = BuildWaves();
            return _waves;
        }

        private static List<VineWaveData> BuildWaves()
        {
            return new List<VineWaveData>
            {
                // Wave 1: Intro — basic scavengers, test that signals reach towers
                new VineWaveData {
                    WaveNumber = 1, Name = "First Contact", BonusGold = 10,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 20, Speed = 2.5f, ScrapValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 6, SpawnInterval = 1.2f, StartDelay = 0, EntryIndex = 0
                        }
                    }
                },

                // Wave 2: Two entry points — test routing
                new VineWaveData {
                    WaveNumber = 2, Name = "Split Path", BonusGold = 12,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 25, Speed = 2.5f, ScrapValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 25, Speed = 2.5f, ScrapValue = 3,
                            Color = TronTheme.EnemyScavenger,
                            Count = 5, SpawnInterval = 1f, StartDelay = 3f, EntryIndex = 1
                        }
                    }
                },

                // Wave 3: Introduce swarm — triggers count sensors early
                new VineWaveData {
                    WaveNumber = 3, Name = "Swarm Protocol", BonusGold = 15,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 10, Speed = 4f, ScrapValue = 1,
                            Color = TronTheme.EnemySwarm,
                            Count = 15, SpawnInterval = 0.4f, StartDelay = 0, EntryIndex = 0
                        }
                    }
                },

                // Wave 4: Mixed — scavengers + a few brutes that break switches
                new VineWaveData {
                    WaveNumber = 4, Name = "Heavy Hitters", BonusGold = 15,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 30, Speed = 2.5f, ScrapValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 1f, StartDelay = 0, EntryIndex = -1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 80, Speed = 1.5f, ScrapValue = 10,
                            Color = TronTheme.EnemyBrute,
                            Count = 2, SpawnInterval = 3f, StartDelay = 5f, EntryIndex = 0
                        }
                    }
                },

                // Wave 5: Ghosts — ignore gate routing, test direct defense
                new VineWaveData {
                    WaveNumber = 5, Name = "Phase Shift", BonusGold = 18,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 35, Speed = 3f, ScrapValue = 8,
                            Color = TronTheme.EnemyGhost,
                            Count = 6, SpawnInterval = 1.5f, StartDelay = 0, EntryIndex = -1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 35, Speed = 3f, ScrapValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 8, SpawnInterval = 0.8f, StartDelay = 4f, EntryIndex = 0
                        }
                    }
                },

                // Wave 6: All factions — the real test
                new VineWaveData {
                    WaveNumber = 6, Name = "Full Spectrum", BonusGold = 20,
                    Groups = {
                        new VineSpawnGroup {
                            EnemyName = "Scrap Rat", Faction = VineEnemyFaction.Scavenger,
                            Health = 40, Speed = 3f, ScrapValue = 4,
                            Color = TronTheme.EnemyScavenger,
                            Count = 10, SpawnInterval = 0.8f, StartDelay = 0, EntryIndex = 0
                        },
                        new VineSpawnGroup {
                            EnemyName = "Rust Hulk", Faction = VineEnemyFaction.Brute,
                            Health = 120, Speed = 1.5f, ScrapValue = 12,
                            Color = TronTheme.EnemyBrute,
                            Count = 3, SpawnInterval = 4f, StartDelay = 3f, EntryIndex = 1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Buzz Drone", Faction = VineEnemyFaction.Swarm,
                            Health = 15, Speed = 4.5f, ScrapValue = 1,
                            Color = TronTheme.EnemySwarm,
                            Count = 20, SpawnInterval = 0.3f, StartDelay = 8f, EntryIndex = -1
                        },
                        new VineSpawnGroup {
                            EnemyName = "Phase Crawler", Faction = VineEnemyFaction.Ghost,
                            Health = 45, Speed = 3f, ScrapValue = 8,
                            Color = TronTheme.EnemyGhost,
                            Count = 4, SpawnInterval = 2f, StartDelay = 12f, EntryIndex = 0
                        }
                    }
                }
            };
        }
    }
}
