using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Autoload node that launches the playtest bug reporter tool on game start.
    /// Runs the Python reporter script in the background, which captures
    /// screenshots and logs for bug reports.
    /// </summary>
    public partial class PlaytestReporterLauncher : Node
    {
        private int _pid = -1;

        public override void _Ready()
        {
            // Only run if --playtest flag is passed or if running from editor
            bool forceRun = OS.HasFeature("editor");
            bool hasFlag = false;
            foreach (var arg in OS.GetCmdlineArgs())
            {
                if (arg == "--playtest")
                {
                    hasFlag = true;
                    break;
                }
            }

            if (!forceRun && !hasFlag) return;

            // Try to launch the reporter script
            string projectRoot = ProjectSettings.GlobalizePath("res://").GetBaseDir();
            string scriptPath = System.IO.Path.Combine(projectRoot, "tools", "playtest_reporter.py");

            if (!System.IO.File.Exists(scriptPath))
            {
                GD.Print("[PlaytestReporter] Reporter script not found at: " + scriptPath);
                return;
            }

            // Launch Python script in background
            var args = new string[] { scriptPath };
            _pid = OS.CreateProcess("python", args, false);

            if (_pid > 0)
                GD.Print($"[PlaytestReporter] Launched reporter (PID {_pid})");
            else
                GD.Print("[PlaytestReporter] Failed to launch reporter script");
        }

        public override void _ExitTree()
        {
            if (_pid > 0)
            {
                OS.Kill(_pid);
                GD.Print("[PlaytestReporter] Killed reporter process");
            }
        }
    }
}
