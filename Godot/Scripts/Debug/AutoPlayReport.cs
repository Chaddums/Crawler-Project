using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Generates manifest.json and run_report.md when an autoplay run ends.
    /// </summary>
    public static class AutoPlayReport
    {
        public static void Generate(AutoPlayScreenshotter screenshotter, AutoPlayRunStats stats)
        {
            var runDir = screenshotter.RunDir;
            var entries = screenshotter.Entries;
            var anomalies = screenshotter.Anomalies;

            // -- manifest.json --
            var categoryCount = new Dictionary<string, int>();
            foreach (var e in entries)
            {
                if (!categoryCount.ContainsKey(e.Category))
                    categoryCount[e.Category] = 0;
                categoryCount[e.Category]++;
            }

            var manifest = new
            {
                run_id = screenshotter.RunId,
                duration_seconds = stats.Duration,
                bot_frame = stats.BotFrame,
                final_level = stats.FinalLevel,
                sectors_cleared = stats.SectorsCleared,
                rooms_entered = stats.RoomsEntered,
                enemies_killed = stats.EnemiesKilled,
                deaths = stats.Deaths,
                outcome = stats.Outcome,
                total_screenshots = entries.Count,
                screenshots_by_category = categoryCount,
                anomalies = anomalies.ToArray()
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            using (var f = FileAccess.Open(runDir + "/manifest.json", FileAccess.ModeFlags.Write))
                f?.StoreString(manifestJson);

            // -- run_report.md --
            var md = new System.Text.StringBuilder();
            md.AppendLine($"# AutoPlay Run Report — {screenshotter.RunId}");
            md.AppendLine();
            md.AppendLine("## Summary");
            md.AppendLine($"- **Bot Frame**: {stats.BotFrame}");
            md.AppendLine($"- **Duration**: {FormatDuration(stats.Duration)}");
            md.AppendLine($"- **Final Level**: {stats.FinalLevel}");
            md.AppendLine($"- **Outcome**: {stats.Outcome}");
            md.AppendLine($"- **Sectors Cleared**: {stats.SectorsCleared}");
            md.AppendLine($"- **Rooms Entered**: {stats.RoomsEntered}");
            md.AppendLine($"- **Enemies Killed**: {stats.EnemiesKilled}");
            md.AppendLine($"- **Deaths**: {stats.Deaths}");
            md.AppendLine($"- **Screenshots**: {entries.Count}");
            md.AppendLine();

            // Category breakdown
            md.AppendLine("## Screenshots by Category");
            foreach (var kvp in categoryCount.OrderByDescending(x => x.Value))
                md.AppendLine($"- {kvp.Key}: {kvp.Value}");
            md.AppendLine();

            // Anomalies
            if (anomalies.Count > 0)
            {
                md.AppendLine("## Anomalies Detected");
                foreach (var a in anomalies)
                    md.AppendLine($"- {a}");
                md.AppendLine();
            }

            // Recommended screenshots to examine
            md.AppendLine("## Recommended Screenshots to Examine");
            var important = entries
                .Where(e => e.Category is "death" or "anomaly_hp_spike" or "anomaly_fall"
                    or "anomaly_ui_stuck" or "boss_spawn" or "boss_killed")
                .ToList();

            if (important.Count > 0)
            {
                foreach (var e in important)
                    md.AppendLine($"- `{e.PngFile}` — {e.Description} (HP: {e.PlayerHp:F0}/{e.PlayerMaxHp:F0})");
            }
            else
            {
                md.AppendLine("- No critical events captured. Check periodic screenshots for visual issues.");
            }
            md.AppendLine();

            // Timeline
            md.AppendLine("## Full Timeline");
            foreach (var e in entries)
                md.AppendLine($"| {e.Index:D3} | {e.Category,-20} | {e.Description,-40} | {e.SceneContext} |");
            md.AppendLine();

            using (var f = FileAccess.Open(runDir + "/run_report.md", FileAccess.ModeFlags.Write))
                f?.StoreString(md.ToString());

            GD.Print($"[AutoPlayReport] Generated report at {runDir}/run_report.md ({entries.Count} screenshots, {anomalies.Count} anomalies)");
        }

        private static string FormatDuration(float seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalMinutes >= 1 ? $"{ts.Minutes}m {ts.Seconds}s" : $"{ts.Seconds}s";
        }
    }

    public class AutoPlayRunStats
    {
        public float Duration { get; set; }
        public string BotFrame { get; set; } = "Unknown";
        public int FinalLevel { get; set; }
        public int SectorsCleared { get; set; }
        public int RoomsEntered { get; set; }
        public int EnemiesKilled { get; set; }
        public int Deaths { get; set; }
        public string Outcome { get; set; } = "Unknown";
    }
}
