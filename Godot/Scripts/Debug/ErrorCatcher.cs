using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JunkbotArena
{
    /// <summary>
    /// Autoload: monitors the Godot log file and C# exceptions for errors,
    /// then writes them as auto-generated bug reports to test-reports/bugs/.
    /// Deduplicates by error hash so repeated errors don't spam reports.
    /// </summary>
    public partial class ErrorCatcher : Node
    {
        private string _logFilePath;
        private long _lastLogPosition;
        private float _pollTimer;
        private const float POLL_INTERVAL = 2f; // check log every 2 seconds
        private string _reportDir;
        private readonly HashSet<string> _seenHashes = new();

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;

            // Resolve paths
            string godotDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/').TrimEnd('\\');
            string repoRoot = Path.GetDirectoryName(godotDir);
            _reportDir = Path.Combine(repoRoot, "test-reports", "bugs");
            Directory.CreateDirectory(_reportDir);

            // Find the Godot log file
            string userDir = ProjectSettings.GlobalizePath("user://").TrimEnd('/').TrimEnd('\\');
            string logsDir = Path.Combine(userDir, "logs");
            _logFilePath = Path.Combine(logsDir, "godot.log");

            // Start reading from end of current log (don't report old errors)
            if (File.Exists(_logFilePath))
            {
                using var fs = new FileStream(_logFilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
                _lastLogPosition = fs.Length;
            }

            // Hook C# unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Load seen hashes from existing auto-reports to avoid duplicates across sessions
            LoadSeenHashes();

            GD.Print($"[ErrorCatcher] Monitoring {_logFilePath}");
        }

        public override void _Process(double delta)
        {
            _pollTimer += (float)delta;
            if (_pollTimer < POLL_INTERVAL) return;
            _pollTimer = 0;

            PollLogFile();
        }

        private void PollLogFile()
        {
            if (!File.Exists(_logFilePath)) return;

            try
            {
                using var fs = new FileStream(_logFilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length <= _lastLogPosition) return;

                fs.Seek(_lastLogPosition, SeekOrigin.Begin);
                using var reader = new StreamReader(fs, Encoding.UTF8);

                string line;
                var errorBuffer = new List<string>();
                bool inError = false;

                while ((line = reader.ReadLine()) != null)
                {
                    // Detect error lines in Godot log
                    bool isError = line.Contains("ERROR:")
                        || line.Contains("SCRIPT ERROR:")
                        || line.Contains("error CS")
                        || line.Contains("System.") && line.Contains("Exception");

                    if (isError)
                    {
                        if (errorBuffer.Count > 0 && !inError)
                            ProcessError(errorBuffer);
                        errorBuffer.Clear();
                        errorBuffer.Add(line);
                        inError = true;
                    }
                    else if (inError)
                    {
                        // Continuation lines (stack trace, indented context)
                        if (line.StartsWith("  ") || line.StartsWith("\t") || line.Contains(" at "))
                        {
                            errorBuffer.Add(line);
                        }
                        else
                        {
                            ProcessError(errorBuffer);
                            errorBuffer.Clear();
                            inError = false;
                        }
                    }
                }

                // Flush remaining error
                if (errorBuffer.Count > 0)
                    ProcessError(errorBuffer);

                _lastLogPosition = fs.Position;
            }
            catch (IOException)
            {
                // Log file locked, try next poll
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception ex)
            {
                var lines = new List<string>
                {
                    $"UNHANDLED EXCEPTION: {ex.GetType().Name}: {ex.Message}",
                };
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    foreach (var stLine in ex.StackTrace.Split('\n'))
                        lines.Add(stLine.TrimEnd());
                }
                ProcessError(lines);
            }
        }

        private void ProcessError(List<string> errorLines)
        {
            if (errorLines.Count == 0) return;

            string firstLine = errorLines[0];

            // Skip noisy non-errors
            if (firstLine.Contains("[ErrorCatcher]")) return;
            if (firstLine.Contains("Condition \"!is_inside_tree()\"")) return; // common Godot warning
            if (firstLine.Contains("ObjectDB instances leaked")) return; // shutdown noise

            // Hash for deduplication
            string hash = ComputeHash(firstLine);
            if (_seenHashes.Contains(hash)) return;
            _seenHashes.Add(hash);

            // Build the report
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string folderName = $"auto-{timestamp}";
            string folderPath = Path.Combine(_reportDir, folderName);
            Directory.CreateDirectory(folderPath);

            // Extract a short title from the error
            string title = ExtractTitle(firstLine);
            string fullError = string.Join("\n", errorLines);

            string sceneContext = "Unable to capture";
            try
            {
                sceneContext = SceneContext.Capture(GetTree());
            }
            catch { /* scene context is best-effort */ }

            string report = $@"# Auto Bug Report: {title}
**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}
**Type:** Auto-detected Error
**Hash:** {hash}

## Error
```
{fullError}
```

## Scene Context
```
{sceneContext}
```

## Notes
This error was automatically captured from the Godot log during gameplay.
";

            File.WriteAllText(Path.Combine(folderPath, "report.md"), report);
            GD.Print($"[ErrorCatcher] Auto-reported: {title}");
        }

        private static string ExtractTitle(string errorLine)
        {
            // Clean up the error line into a readable title
            string title = errorLine;

            // Remove common prefixes
            foreach (var prefix in new[] { "USER ERROR: ", "ERROR: ", "SCRIPT ERROR: ",
                "UNHANDLED EXCEPTION: ", "System.", "Godot." })
            {
                if (title.StartsWith(prefix))
                    title = title.Substring(prefix.Length);
            }

            // Truncate to reasonable length
            if (title.Length > 80)
                title = title.Substring(0, 77) + "...";

            return title;
        }

        private static string ComputeHash(string input)
        {
            // Strip timestamps and line numbers for stable deduplication
            string normalized = System.Text.RegularExpressions.Regex.Replace(
                input, @"\d{4}-\d{2}-\d{2}|\d+:\d+:\d+|line \d+|:\d+", "");

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(bytes).Substring(0, 12);
        }

        private void LoadSeenHashes()
        {
            // Scan existing auto-reports to avoid duplicating them
            if (!Directory.Exists(_reportDir)) return;

            foreach (var dir in Directory.GetDirectories(_reportDir, "auto-*"))
            {
                string reportPath = Path.Combine(dir, "report.md");
                if (!File.Exists(reportPath)) continue;

                try
                {
                    string content = File.ReadAllText(reportPath);
                    int hashIdx = content.IndexOf("**Hash:** ");
                    if (hashIdx >= 0)
                    {
                        int start = hashIdx + 10;
                        int end = content.IndexOf('\n', start);
                        if (end > start)
                        {
                            string hash = content.Substring(start, end - start).Trim();
                            _seenHashes.Add(hash);
                        }
                    }
                }
                catch { /* skip unreadable files */ }
            }

            GD.Print($"[ErrorCatcher] Loaded {_seenHashes.Count} known error hashes");
        }

        public override void _ExitTree()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        }
    }
}
