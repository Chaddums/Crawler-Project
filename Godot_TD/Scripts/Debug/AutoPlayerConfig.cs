using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Serializable run configuration for the AutoPlayer.
    /// Load from JSON or construct programmatically for batch runs.
    /// </summary>
    public class AutoPlayerConfig
    {
        public int Planet { get; set; } = 1;
        public string Role { get; set; } = "Bruteforge";
        public string Strategy { get; set; } = "TurretSpam";
        public string MaterialType { get; set; } = "Power";
        public float GameSpeed { get; set; } = 3f;
        public int MaxWaves { get; set; } = 20;
        public bool ScreenshotOnDeath { get; set; } = true;
        public int ScreenshotEveryNWaves { get; set; } = 5;

        /// <summary>
        /// Load config from JSON file path. Returns default if file missing/invalid.
        /// </summary>
        public static AutoPlayerConfig LoadFromJson(string path)
        {
            if (!FileAccess.FileExists(path))
            {
                GD.PrintErr($"[AutoPlayerConfig] File not found: {path}");
                return new AutoPlayerConfig();
            }

            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return new AutoPlayerConfig();

            string text = file.GetAsText();
            file.Close();

            var json = new Json();
            if (json.Parse(text) != Error.Ok)
            {
                GD.PrintErr($"[AutoPlayerConfig] Parse error: {json.GetErrorMessage()}");
                return new AutoPlayerConfig();
            }

            var dict = json.Data.AsGodotDictionary();
            var config = new AutoPlayerConfig();

            if (dict.ContainsKey("planet")) config.Planet = (int)dict["planet"];
            if (dict.ContainsKey("role")) config.Role = (string)dict["role"];
            if (dict.ContainsKey("strategy")) config.Strategy = (string)dict["strategy"];
            if (dict.ContainsKey("materialType")) config.MaterialType = (string)dict["materialType"];
            if (dict.ContainsKey("gameSpeed")) config.GameSpeed = (float)dict["gameSpeed"];
            if (dict.ContainsKey("maxWaves")) config.MaxWaves = (int)dict["maxWaves"];
            if (dict.ContainsKey("screenshotOnDeath")) config.ScreenshotOnDeath = (bool)dict["screenshotOnDeath"];
            if (dict.ContainsKey("screenshotEveryNWaves")) config.ScreenshotEveryNWaves = (int)dict["screenshotEveryNWaves"];

            return config;
        }

        /// <summary>
        /// Generate all default strategy configs for batch mode.
        /// </summary>
        public static AutoPlayerConfig[] GetDefaultBatch()
        {
            return new[]
            {
                new AutoPlayerConfig { Strategy = "TurretSpam", Role = "Bruteforge", MaterialType = "Power" },
                new AutoPlayerConfig { Strategy = "MazeBuilder", Role = "Obelisk", MaterialType = "Environment" },
                new AutoPlayerConfig { Strategy = "SensorNet", Role = "Arcanist", MaterialType = "Chaos" },
                new AutoPlayerConfig { Strategy = "RandomPlacement", Role = "Bruteforge", MaterialType = "Power" },
                new AutoPlayerConfig { Strategy = "EconomyFocus", Role = "Arcanist", MaterialType = "Power" },
                new AutoPlayerConfig { Strategy = "RushDefense", Role = "Bruteforge", MaterialType = "Chaos" },
                new AutoPlayerConfig { Strategy = "SlotExplorer", Role = "Bruteforge", MaterialType = "Power" },
                new AutoPlayerConfig { Strategy = "DoNothing", Role = "Obelisk", MaterialType = "Environment" },
            };
        }

        public override string ToString() => $"{Strategy}/{Role}/P{Planet}/{MaterialType}";
    }
}
