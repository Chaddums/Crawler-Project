using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Player settings, saved to user://settings.cfg and applied at start-up and on every change.
    /// Opened from the title screen's SETTINGS and the pause menu (<see cref="SettingsScreen"/>).
    /// </summary>
    public static class GameSettings
    {
        private const string Path = "user://settings.cfg";

        // Audio (0..1)
        public static float MasterVolume = 0.9f;
        public static float SfxVolume = 0.8f;
        public static float AmbientVolume = 0.7f;
        public static float VoiceVolume = 0.8f;
        // Display
        public static bool Fullscreen;
        public static bool VSync = true;
        /// <summary>0 = no cap.</summary>
        public static int MaxFps;
        /// <summary>
        /// 0 Low, 1 Balanced, 2 High: the sun's shadow cascades (one, two or four) and how soon
        /// distant models drop to their simpler versions. With 40 towers and 120 enemies, four
        /// cascades drew everything five times over (about 8,000 draws and 19M triangles a frame).
        /// </summary>
        public static int Quality = 1;
        public static readonly string[] QualityNames = { "Low", "Balanced", "High" };
        // Gameplay
        /// <summary>How hard the camera shakes (0 off, 1 full).</summary>
        public static float ScreenShake = 1f;
        /// <summary>Mouse look speed in the Spire's gunner view.</summary>
        public static float MouseSensitivity = 1f;
        /// <summary>Damage numbers over hit enemies and BIT.</summary>
        public static bool DamageNumbers = true;
        /// <summary>The enemy route drawn on the ground.</summary>
        public static bool RoutePreview = true;
        /// <summary>BIT shoots the nearest enemy by itself when the mouse isn't held.</summary>
        public static bool BitAutoFire = true;

        public static readonly int[] FpsChoices = { 0, 30, 60, 120, 144 };

        public static void Load()
        {
            var cfg = new ConfigFile();
            if (cfg.Load(Path) != Error.Ok) return;
            MasterVolume = (float)cfg.GetValue("audio", "master", MasterVolume).AsDouble();
            SfxVolume = (float)cfg.GetValue("audio", "sfx", SfxVolume).AsDouble();
            AmbientVolume = (float)cfg.GetValue("audio", "ambient", AmbientVolume).AsDouble();
            VoiceVolume = (float)cfg.GetValue("audio", "voice", VoiceVolume).AsDouble();
            Fullscreen = cfg.GetValue("display", "fullscreen", Fullscreen).AsBool();
            VSync = cfg.GetValue("display", "vsync", VSync).AsBool();
            MaxFps = cfg.GetValue("display", "max_fps", MaxFps).AsInt32();
            Quality = Mathf.Clamp(cfg.GetValue("display", "quality", Quality).AsInt32(), 0, 2);
            ScreenShake = (float)cfg.GetValue("gameplay", "screen_shake", ScreenShake).AsDouble();
            MouseSensitivity = (float)cfg.GetValue("gameplay", "mouse_sensitivity", MouseSensitivity).AsDouble();
            DamageNumbers = cfg.GetValue("gameplay", "damage_numbers", DamageNumbers).AsBool();
            RoutePreview = cfg.GetValue("gameplay", "route_preview", RoutePreview).AsBool();
            BitAutoFire = cfg.GetValue("gameplay", "bit_auto_fire", BitAutoFire).AsBool();
        }

        public static void Save()
        {
            if (SafeFile.IsSandboxed) return; // tests never write the player's settings
            var cfg = new ConfigFile();
            cfg.SetValue("audio", "master", MasterVolume);
            cfg.SetValue("audio", "sfx", SfxVolume);
            cfg.SetValue("audio", "ambient", AmbientVolume);
            cfg.SetValue("audio", "voice", VoiceVolume);
            cfg.SetValue("display", "fullscreen", Fullscreen);
            cfg.SetValue("display", "vsync", VSync);
            cfg.SetValue("display", "max_fps", MaxFps);
            cfg.SetValue("display", "quality", Quality);
            cfg.SetValue("gameplay", "screen_shake", ScreenShake);
            cfg.SetValue("gameplay", "mouse_sensitivity", MouseSensitivity);
            cfg.SetValue("gameplay", "damage_numbers", DamageNumbers);
            cfg.SetValue("gameplay", "route_preview", RoutePreview);
            cfg.SetValue("gameplay", "bit_auto_fire", BitAutoFire);
            cfg.Save(Path);
        }

        /// <summary>Push the audio and display settings to the engine.</summary>
        public static void Apply()
        {
            SetBus("Master", MasterVolume);
            SetBus("SFX", SfxVolume);
            SetBus("Music", AmbientVolume);
            SetBus("Voice", VoiceVolume);
            Engine.MaxFps = MaxFps;
            if (DisplayServer.GetName() == "headless") return;
            if (Engine.GetMainLoop() is SceneTree tree)
                tree.Root.MeshLodThreshold = LodThreshold;
            if (GodotObject.IsInstanceValid(BattleSun)) ApplyShadows(BattleSun);
            DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
            if (!Engine.IsEmbeddedInEditor())
            {
                var want = Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed;
                var now = DisplayServer.WindowGetMode();
                bool isFull = now is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
                if (isFull != Fullscreen) DisplayServer.WindowSetMode(want);
            }
        }

        /// <summary>Pixels of error allowed before a model switches to a simpler version.</summary>
        public static float LodThreshold => Quality switch { 0 => 6f, 1 => 3f, _ => 1f };

        /// <summary>The battle's sun (set by the battle scene) so a change applies at once.</summary>
        public static DirectionalLight3D BattleSun;

        /// <summary>Shadow cascades for the quality level.</summary>
        public static void ApplyShadows(DirectionalLight3D sun)
        {
            if (sun == null) return;
            sun.DirectionalShadowMode = Quality switch
            {
                0 => DirectionalLight3D.ShadowMode.Orthogonal,
                1 => DirectionalLight3D.ShadowMode.Parallel2Splits,
                _ => DirectionalLight3D.ShadowMode.Parallel4Splits,
            };
            sun.DirectionalShadowBlendSplits = Quality >= 2;
        }

        private static void SetBus(string name, float linear)
        {
            int idx = AudioServer.GetBusIndex(name);
            if (idx < 0 && name != "Master")
            {
                idx = AudioServer.BusCount;
                AudioServer.AddBus();
                AudioServer.SetBusName(idx, name);
                AudioServer.SetBusSend(idx, "Master");
            }
            if (idx < 0) return;
            AudioServer.SetBusMute(idx, linear <= 0.001f);
            AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(Mathf.Max(0.0001f, linear)));
        }
    }
}
