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
    }
}
