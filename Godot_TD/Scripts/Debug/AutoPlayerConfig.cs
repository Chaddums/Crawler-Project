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
        /// <summary>Press Send All (Shift+Space) two seconds into each of these waves.</summary>
        public int[] SendAllAtWaves { get; set; } = System.Array.Empty<int>();
        /// <summary>Balance bots: the tower SingleType builds (a VineNodeType name).</summary>
        public string Tower { get; set; } = "DamageTower";
        /// <summary>Balance bots: waves sent on top of each wave (2: "send three waves over and over").</summary>
        public int Stack { get; set; }
        /// <summary>Balance bots: build Salvage Drills.</summary>
        public bool Drills { get; set; } = true;
        /// <summary>Game seconds the bot waits in each build phase before sending, as a player takes.</summary>
        public float BuildSeconds { get; set; }
        /// <summary>Balance bots: where new towers go. "route" (where they cover the most of the
        /// route), "blob" (the free cell nearest the Spire) or "random" (any free cell near the Spire).</summary>
        public string Placement { get; set; } = "route";
        /// <summary>Balance bots: one more drill allowed every this many waves.</summary>
        public int DrillEvery { get; set; } = 8;
        /// <summary>Counters: walls it may place in a run (8: a few early, odd waves to W10) and how many a build past that.</summary>
        public int Walls { get; set; } = 8;
        public int WallsPerBuild { get; set; } = 1;
        /// <summary>Counters: build the drill it's allowed whenever the card doesn't say BREAKING (not only with room to spare).</summary>
        public bool EagerDrills { get; set; }
        /// <summary>Counters: one tar pit for every this many guns.</summary>
        public int TarEvery { get; set; } = 7;

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
            if (dict.ContainsKey("tower")) config.Tower = (string)dict["tower"];
            if (dict.ContainsKey("stack")) config.Stack = (int)dict["stack"];
            if (dict.ContainsKey("drills")) config.Drills = (bool)dict["drills"];
            if (dict.ContainsKey("buildSeconds")) config.BuildSeconds = (float)dict["buildSeconds"];
            if (dict.ContainsKey("placement")) config.Placement = (string)dict["placement"];
            if (dict.ContainsKey("drillEvery")) config.DrillEvery = (int)dict["drillEvery"];
            if (dict.ContainsKey("walls")) config.Walls = (int)dict["walls"];
            if (dict.ContainsKey("wallsPerBuild")) config.WallsPerBuild = (int)dict["wallsPerBuild"];
            if (dict.ContainsKey("eagerDrills")) config.EagerDrills = (bool)dict["eagerDrills"];
            if (dict.ContainsKey("tarEvery")) config.TarEvery = (int)dict["tarEvery"];
            if (dict.ContainsKey("sendAllAtWaves") && dict["sendAllAtWaves"].Obj is Godot.Collections.Array sa)
                config.SendAllAtWaves = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(sa, v => (int)v));

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
                new AutoPlayerConfig { Strategy = "MixedDefense", Role = "Arcanist", MaterialType = "Chaos" },
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
