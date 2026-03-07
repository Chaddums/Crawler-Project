using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Persistent meta-progression data that survives death and run resets.
    /// Saved to user://junkbot_meta.json — never deleted.
    /// </summary>
    public class MetaSaveData
    {
        public string Version { get; set; } = "1.0";

        // Meta-currency earned across all runs
        public int Scrap { get; set; }
        public int LifetimeScrap { get; set; }

        // Permanent perk upgrades (perkId -> current rank)
        public Dictionary<string, int> UnlockedPerks { get; set; } = new();

        // Gear codex: set of item base IDs found at least once
        public HashSet<string> GearCodex { get; set; } = new();

        // Unlocked bot frames (all start locked except TinCan)
        public HashSet<string> UnlockedFrames { get; set; } = new() { "TinCan" };

        // Ascension (NG+ rank, unlocked after first AXIS defeat)
        public int AscensionRank { get; set; }
        public int TimesAxisDefeated { get; set; }

        // Run history
        public RunHistoryData History { get; set; } = new();
    }

    public class RunHistoryData
    {
        public int TotalRuns { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }
        public int BestSector { get; set; }
        public int BestArea { get; set; }
        public float BestTime { get; set; }
        public int HighestLevel { get; set; }

        // Per-class stats
        public Dictionary<string, int> RunsPerClass { get; set; } = new();
        public Dictionary<string, int> BestSectorPerClass { get; set; } = new();
    }
}
