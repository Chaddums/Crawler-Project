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
        // Memory/object peaks sampled every frame (FPS fields above were never populated:
        // they came from an AutoBugger autoload that isn't registered).
        public long PeakObjectCount { get; set; }
        public long PeakNodeCount { get; set; }
        public long PeakStaticMemoryMB { get; set; }
        public long PeakManagedMemoryMB { get; set; }
        public long FinalObjectCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
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
            ErrorCount = 0; WarningCount = 0;
            Screenshots.Clear();
            PeakObjectCount = PeakNodeCount = PeakStaticMemoryMB = PeakManagedMemoryMB = FinalObjectCount = 0;
            PeakFPS = AvgFPS = 0; MinFPS = float.MaxValue;
            _fpsSum = 0; _fpsSamples = 0; _logTimer = 0;

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
            else if (_fpsSamples > 0)
            {
                AvgFPS = (float)(_fpsSum / _fpsSamples);
            }
            if (MinFPS == float.MaxValue) MinFPS = 0;
            FinalObjectCount = (long)Performance.GetMonitor(Performance.Monitor.ObjectCount);
        }

        private double _fpsSum;
        private int _fpsSamples;
        private float _logTimer;

        /// <summary>
        /// Per-frame sampling of FPS, engine object counts and memory. Logs a line every
        /// <paramref name="logInterval"/> seconds of real time so leaks show up in the log
        /// before they become an out-of-memory kill.
        /// </summary>
        public void SamplePerf(float realDelta, int wave, float logInterval = 5f)
        {
            long objects = (long)Performance.GetMonitor(Performance.Monitor.ObjectCount);
            long nodes = (long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            long staticMb = (long)(OS.GetStaticMemoryUsage() / 1048576);
            long managedMb = GC.GetTotalMemory(false) / 1048576;
            if (objects > PeakObjectCount) PeakObjectCount = objects;
            if (nodes > PeakNodeCount) PeakNodeCount = nodes;
            if (staticMb > PeakStaticMemoryMB) PeakStaticMemoryMB = staticMb;
            if (managedMb > PeakManagedMemoryMB) PeakManagedMemoryMB = managedMb;

            float fps = (float)Engine.GetFramesPerSecond();
            if (fps > 0)
            {
                if (fps > PeakFPS) PeakFPS = fps;
                if (fps < MinFPS) MinFPS = fps;
                _fpsSum += fps; _fpsSamples++;
            }

            _logTimer += realDelta;
            if (_logTimer >= logInterval)
            {
                _logTimer = 0;
                GD.Print($"[AutoPlayer/perf] P{Config?.Planet}-W{wave} objects={objects} nodes={nodes} " +
                         $"static={staticMb}MB managed={managedMb}MB fps={fps:F0}");
            }
        }

        /// <summary>
        /// Capture and save a screenshot. Returns the filename.
        /// </summary>
        public string CaptureScreenshot(string label, Viewport viewport)
        {
            if (OS.HasFeature("headless") || DisplayServer.GetName() == "headless") return null;
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

            // Invariant culture: on comma-decimal locales "{3.0:F1}" formats as "3,0" (invalid JSON)
            string json = string.Create(System.Globalization.CultureInfo.InvariantCulture, $@"{{
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
  ""synergiesActivated"": [{string.Join(", ", SynergiesActivated.ConvertAll(s => $"\"{EscapeJson(s)}\""))}],
  ""enemiesKilled"": {EnemiesKilled},
  ""coreLivesRemaining"": {CoreLivesRemaining},
  ""peakFPS"": {PeakFPS:F0},
  ""minFPS"": {MinFPS:F0},
  ""avgFPS"": {AvgFPS:F0},
  ""peakObjectCount"": {PeakObjectCount},
  ""finalObjectCount"": {FinalObjectCount},
  ""peakNodeCount"": {PeakNodeCount},
  ""peakStaticMemoryMB"": {PeakStaticMemoryMB},
  ""peakManagedMemoryMB"": {PeakManagedMemoryMB},
  ""errorCount"": {ErrorCount},
  ""warningCount"": {WarningCount},
  ""errors"": [{string.Join(", ", Errors.ConvertAll(s => $"\"{EscapeJson(s)}\""))}],
  ""warnings"": [{string.Join(", ", Warnings.ConvertAll(s => $"\"{EscapeJson(s)}\""))}],
  ""screenshots"": [{string.Join(", ", Screenshots.ConvertAll(s => $"\"{EscapeJson(s)}\""))}]
}}");

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

        // Errors now carry engine/exception text (\r\n on Windows, tabs in backtraces); raw
        // control characters are invalid JSON and broke the QA parser exactly when there was
        // something to report.
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
