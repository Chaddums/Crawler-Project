using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Central check for CEF availability. Returns false in headless mode
    /// to prevent CEF from blocking the frame loop during automated testing.
    /// All UI screens should use CefHelper.Available instead of ClassDB.ClassExists("CefTexture").
    /// </summary>
    public static class CefHelper
    {
        private static bool? _available;

        public static bool Available
        {
            get
            {
                // OS.HasFeature("headless") misses the --headless flag in Godot 4.6 —
                // DisplayServer's name is the reliable check (see AutoPlayReport).
                _available ??= !OS.HasFeature("headless")
                    && DisplayServer.GetName() != "headless"
                    && ClassDB.ClassExists("CefTexture");
                return _available.Value;
            }
        }

        /// <summary>
        /// Project setting that turns GPU texture sharing back on for the web views. Off by
        /// default: on Vulkan the plugin shares Godot's graphics queue ("may have sync issues under
        /// load"), and on the dev laptop (RTX 3050, Vulkan) pages came up as a strip of noise and
        /// the game froze after a few screen changes. CPU rendering copies each frame instead.
        /// </summary>
        public const string AcceleratedSetting = "junkyard/ui/cef_accelerated_osr";

        /// <summary>Settings every web view gets before it enters the tree.</summary>
        public static void Configure(GodotObject cefTexture)
        {
            if (cefTexture == null) return;
            bool accelerated = ProjectSettings.HasSetting(AcceleratedSetting)
                && ProjectSettings.GetSetting(AcceleratedSetting).AsBool();
            cefTexture.Set("enable_accelerated_osr", accelerated);
        }
    }
}
