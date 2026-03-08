using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Autoload stub — reporter is now launched on-demand via the debug console.
    /// Kept as autoload so existing project.godot config doesn't break.
    /// </summary>
    public partial class PlaytestReporterLauncher : Node
    {
        public override void _Ready()
        {
            GD.Print("[PlaytestReporter] Reporter available via ~ console (type 'bug' or 'feature')");
        }
    }
}
