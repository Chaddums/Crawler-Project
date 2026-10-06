using System;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Clean shutdown for automated runs (test harness, autoplay).
    ///
    /// Quitting with a battle scene alive left thousands of C# wrappers for the engine to finalize
    /// during shutdown, which crashed (SIGILL/SIGABRT) and turned a passing run's exit code 0 into
    /// 134. Free the live scene and stray root-level nodes, unregister the error logger, drop shared
    /// VFX resources, and run the finalizers while the engine is still up.
    /// </summary>
    public static class HeadlessShutdown
    {
        public static async Task TearDown(Node caller)
        {
            try
            {
                var tree = caller.GetTree();
                tree.Paused = false;
                tree.CurrentScene?.QueueFree();
                foreach (var child in tree.Root.GetChildren())
                {
                    // Keep autoloads; free anything a scene parented to the root (enemies, popups)
                    if (child != caller && child.Owner == null && !IsAutoload(child))
                        child.QueueFree();
                }
                for (int i = 0; i < 3; i++)
                    await caller.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

                ErrorCaptureLogger.Uninstall();
                VfxCache.Clear();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await caller.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Shutdown] Teardown error (ignored): {e.Message}");
            }
        }

        /// <summary>Tear down, then quit with the given code.</summary>
        public static async void QuitClean(Node caller, int exitCode)
        {
            await TearDown(caller);
            caller.GetTree().Quit(exitCode);
        }

        private static bool IsAutoload(Node node) => ProjectSettings.HasSetting($"autoload/{node.Name}");
    }
}
