using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// When waves start and when the fighting stops (Data/wave_pacing.json). After the first
    /// wave's delay, waves 1 to <see cref="ContinuousUntil"/> run back to back; after that each
    /// clear gives <see cref="LaterGap"/> seconds to build. A safe room after wave
    /// <see cref="SafeRoomFrom"/> and every <see cref="SafeRoomEvery"/> after it stops the
    /// fighting until BIT's crates are opened (VineWaveManager, LootManager). Under the test
    /// harness <see cref="Classic"/> keeps the old pacing (30 s after every clear, crates open
    /// between any waves) so suites written for it don't race the next wave.
    /// </summary>
    public static class WavePacing
    {
        public static float FirstWaveDelay { get; private set; } = 30f;
        public static int ContinuousUntil { get; private set; } = 20;
        public static float ContinuousGap { get; private set; } = 2f;
        public static float LaterGap { get; private set; } = 45f;
        public static int SafeRoomFrom { get; private set; } = 20;
        public static int SafeRoomEvery { get; private set; } = 10;

        /// <summary>The old pacing: every gap the prep time, no safe rooms (the test harness sets it).</summary>
        public static bool Classic { get; set; }

        private static bool _loaded;

        static WavePacing() { Load(); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            const string path = "res://Data/wave_pacing.json";
            if (!FileAccess.FileExists(path)) return;
            var json = new Json();
            if (json.Parse(FileAccess.GetFileAsString(path)) != Error.Ok || json.Data.Obj is not Godot.Collections.Dictionary d)
            {
                GD.PushWarning("[WavePacing] Data/wave_pacing.json didn't parse; using defaults");
                return;
            }
            float F(string k, float def) => d.ContainsKey(k) ? (float)d[k].AsDouble() : def;
            int I(string k, int def) => d.ContainsKey(k) ? d[k].AsInt32() : def;
            FirstWaveDelay = F("firstWaveDelay", FirstWaveDelay);
            ContinuousUntil = I("continuousUntil", ContinuousUntil);
            ContinuousGap = F("continuousGap", ContinuousGap);
            LaterGap = F("laterGap", LaterGap);
            SafeRoomFrom = I("safeRoomFrom", SafeRoomFrom);
            SafeRoomEvery = Mathf.Max(1, I("safeRoomEvery", SafeRoomEvery));
        }

        /// <summary>Seconds before the first wave.</summary>
        public static float FirstDelay { get { Load(); return Classic ? Constants.WAVE_PREP_TIME : FirstWaveDelay; } }

        /// <summary>Seconds from a clear of <paramref name="clearedWave"/> to the next wave starting by itself.</summary>
        public static float GapAfter(int clearedWave)
        {
            Load();
            if (Classic) return Constants.WAVE_PREP_TIME;
            return clearedWave + 1 <= ContinuousUntil ? ContinuousGap : LaterGap;
        }

        /// <summary>Clearing <paramref name="wave"/> opens a safe room.</summary>
        public static bool IsSafeRoom(int wave)
        {
            Load();
            return !Classic && wave >= SafeRoomFrom && (wave - SafeRoomFrom) % SafeRoomEvery == 0;
        }

        /// <summary>The first safe room at or after <paramref name="wave"/> (crates wait for it).</summary>
        public static int NextSafeRoom(int wave)
        {
            Load();
            if (wave <= SafeRoomFrom) return SafeRoomFrom;
            int k = (wave - SafeRoomFrom + SafeRoomEvery - 1) / SafeRoomEvery;
            return SafeRoomFrom + k * SafeRoomEvery;
        }
    }
}
