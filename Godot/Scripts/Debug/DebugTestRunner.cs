using Godot;
using System.Linq;
using System.Threading.Tasks;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Autoload that runs an automated screenshot test sequence when launched with --test.
    /// Usage: Godot.exe --path "project_dir" -- --test
    /// Screenshots saved to {project_dir}/.cache/screenshots/
    /// </summary>
    public partial class DebugTestRunner : Node
    {
        private string _screenshotDir;
        private bool _testMode;

        public override void _Ready()
        {
            var userArgs = OS.GetCmdlineUserArgs();
            _testMode = userArgs.Contains("--test");

            if (!_testMode)
                return;

            GD.Print("[TestRunner] Test mode activated");

            // Run windowed so it doesn't take over the screen
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1280, 720));
            DisplayServer.WindowSetPosition(new Vector2I(100, 100));

            _screenshotDir = ProjectSettings.GlobalizePath("res://") + ".cache/screenshots/";
            DirAccess.MakeDirRecursiveAbsolute(_screenshotDir);

            // Defer so MainMenu has time to initialize
            CallDeferred(nameof(StartTestSequence));
        }

        private async void StartTestSequence()
        {
            GD.Print("[TestRunner] Starting test sequence...");

            // Step 1: Capture the main menu
            await Wait(1.5);
            CaptureScreenshot("01_main_menu");
            GD.Print("[TestRunner] Captured: 01_main_menu");

            // Step 2: Click "New Game" programmatically
            if (GameManager.Instance != null)
            {
                GD.Print("[TestRunner] Starting new game...");
                GameManager.Instance.StartNewGame();
            }
            else
            {
                GD.PrintErr("[TestRunner] GameManager not found, aborting");
                GetTree().Quit(1);
                return;
            }

            // Step 3: Wait for floor to load + player to spawn
            await Wait(2.5);
            CaptureScreenshot("02_floor_spawn");
            GD.Print("[TestRunner] Captured: 02_floor_spawn");

            // Step 4: Move forward (W) for 1 second
            Input.ActionPress("move_up");
            await Wait(1.0);
            Input.ActionRelease("move_up");

            // Strafe left (A) for 0.5 seconds
            Input.ActionPress("move_left");
            await Wait(0.5);
            Input.ActionRelease("move_left");

            await Wait(0.3);
            CaptureScreenshot("03_after_movement");
            GD.Print("[TestRunner] Captured: 03_after_movement");

            // Step 5: Zoom out several notches (must inject InputEvent for _UnhandledInput)
            for (int i = 0; i < 5; i++)
            {
                var ev = new InputEventAction { Action = "zoom_out", Pressed = true };
                Input.ParseInputEvent(ev);
                await Wait(0.15);
            }

            await Wait(0.5);
            CaptureScreenshot("04_zoomed_out");
            GD.Print("[TestRunner] Captured: 04_zoomed_out");

            // Done
            GD.Print("[TestRunner] Test sequence complete. Exiting.");
            await Wait(0.3);
            GetTree().Quit();
        }

        private void CaptureScreenshot(string name)
        {
            var image = GetViewport().GetTexture().GetImage();
            var path = _screenshotDir + name + ".png";
            var error = image.SavePng(path);

            if (error != Error.Ok)
                GD.PrintErr($"[TestRunner] Failed to save screenshot: {path} (Error: {error})");
        }

        private async Task Wait(double seconds)
        {
            await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        }
    }
}
