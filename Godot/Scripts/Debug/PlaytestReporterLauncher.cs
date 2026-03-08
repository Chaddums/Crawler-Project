using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Autoload node that launches the playtest bug/feature reporter on game start.
    /// The Python reporter runs in the background with global hotkeys:
    ///   Insert → Bug Report (screenshot + dialog)
    ///   Delete → Feature Request (screenshot + dialog)
    /// </summary>
    public partial class PlaytestReporterLauncher : Node
    {
        private int _pid = -1;

        public override void _Ready()
        {
            string projectRoot = ProjectSettings.GlobalizePath("res://").GetBaseDir();
            string scriptPath = System.IO.Path.Combine(projectRoot, "tools", "playtest-reporter", "reporter.py");

            if (!System.IO.File.Exists(scriptPath))
            {
                GD.Print("[PlaytestReporter] Reporter script not found at: " + scriptPath);
                return;
            }

            var args = new string[] { scriptPath };
            _pid = OS.CreateProcess("python", args, false);

            if (_pid > 0)
                GD.Print($"[PlaytestReporter] Launched reporter (PID {_pid}) — Insert=Bug, Delete=Feature");
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
