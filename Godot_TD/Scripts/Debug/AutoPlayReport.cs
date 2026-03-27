using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Collects per-run metrics during AutoPlayer execution.
    /// Outputs JSON report on run completion.
    /// </summary>
    public class AutoPlayReport
    {
        public AutoPlayerConfig Config { get; set; }
        public string Result { get; set; } = "unknown"; // "defeat", "victory", "timeout", "error"
        public int WaveReached { get; set; }
        public int TotalExtracted { get; set; }
        public float DurationSeconds { get; set; }
        public int NodesPlaced { get; set; }
        public int TowersPlaced { get; set; }
        public int ComponentsSlotted { get; set; }
        public List<string> SynergiesActivated { get; set; } = new();
        public int EnemiesKilled { get; set; }
        public int CoreLivesRemaining { get; set; }
        public float PeakFPS { get; set; }
        public float MinFPS { get; set; }
        public float AvgFPS { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Screenshots { get; set; } = new();

        private DateTime _startTime;
        private string _reportDir;

        public void StartRun(AutoPlayerConfig config)
        {
            Config = config;
            _startTime = DateTime.Now;
            Result = "unknown";
            WaveReached = 0;
            TotalExtracted = 0;
            NodesPlaced = 0;
            TowersPlaced = 0;
            ComponentsSlotted = 0;
            SynergiesActivated.Clear();
            EnemiesKilled = 0;
            CoreLivesRemaining = 0;
            Errors.Clear();
            Warnings.Clear();
            Screenshots.Clear();

            // Create report directory
            string timestamp = _startTime.ToString("yyyyMMdd_HHmmss");
            string folderName = $"{timestamp}_{config.Strategy}_{config.Role}";
            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
            _reportDir = Path.Combine(godotDir, "autoplay-reports", folderName);
            Directory.CreateDirectory(_reportDir);
        }

        public void EndRun(string result)
        {
            Result = result;
            DurationSeconds = (float)(DateTime.Now - _startTime).TotalSeconds;

            // Get FPS stats from AutoBugger
            if (AutoBugger.Instance != null)
            {
                var (peak, min, avg) = AutoBugger.Instance.GetFpsStats();
                PeakFPS = peak;
                MinFPS = min;
                AvgFPS = avg;
            }
        }

        /// <summary>
        /// Capture and save a screenshot. Returns the filename.
        /// </summary>
        public string CaptureScreenshot(string label, Viewport viewport)
        {
            if (OS.HasFeature("headless")) return null;
            if (_reportDir == null || viewport == null) return null;
            try
            {
                var image = viewport.GetTexture().GetImage();
                string filename = $"{label}.png";
                string fullPath = Path.Combine(_reportDir, filename);
                image.SavePng(fullPath);
                Screenshots.Add(filename);
                return filename;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[AutoPlayReport] Screenshot failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Write the JSON report to disk.
        /// </summary>
        public void WriteReport()
        {
            if (_reportDir == null) return;

            string json = $@"{{
  ""config"": {{
    ""planet"": {Config.Planet},
    ""role"": ""{Config.Role}"",
    ""strategy"": ""{Config.Strategy}"",
    ""materialType"": ""{Config.MaterialType}"",
    ""gameSpeed"": {Config.GameSpeed:F1},
    ""maxWaves"": {Config.MaxWaves}
  }},
  ""result"": ""{Result}"",
  ""waveReached"": {WaveReached},
  ""totalExtracted"": {TotalExtracted},
  ""duration_seconds"": {DurationSeconds:F1},
  ""nodesPlaced"": {NodesPlaced},
  ""towersPlaced"": {TowersPlaced},
  ""componentsSlotted"": {ComponentsSlotted},
  ""synergiesActivated"": [{string.Join(", ", SynergiesActivated.ConvertAll(s => $"\"{s}\""))}],
  ""enemiesKilled"": {EnemiesKilled},
  ""coreLivesRemaining"": {CoreLivesRemaining},
  ""peakFPS"": {PeakFPS:F0},
  ""minFPS"": {MinFPS:F0},
  ""avgFPS"": {AvgFPS:F0},
  ""errors"": [{string.Join(", ", Errors.ConvertAll(s => $"\"{EscapeJson(s)}\""))}],
  ""warnings"": [{string.Join(", ", Warnings.ConvertAll(s => $"\"{EscapeJson(s)}\""))}],
  ""screenshots"": [{string.Join(", ", Screenshots.ConvertAll(s => $"\"{s}\""))}]
}}";

            try
            {
                File.WriteAllText(Path.Combine(_reportDir, "report.json"), json);
                GD.Print($"[AutoPlayReport] Saved: {_reportDir}/report.json");
                GD.Print($"[AutoPlayReport] {Result}: wave {WaveReached}, {TotalExtracted} extracted, {DurationSeconds:F0}s, {EnemiesKilled} kills");
            }
            catch (Exception e)
            {
                GD.PrintErr($"[AutoPlayReport] Save failed: {e.Message}");
            }
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
