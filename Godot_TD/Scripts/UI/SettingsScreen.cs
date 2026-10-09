using System;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The settings overlay (title screen and pause menu): audio levels, display, and gameplay
    /// options. Every change applies at once; Done (or Esc) saves and closes. Works while the
    /// game is paused. Code-built in the Command Center's style.
    /// </summary>
    public partial class SettingsScreen : CanvasLayer
    {
        public static SettingsScreen Current { get; private set; }
        public event Action Closed;

        /// <summary>Open the overlay over <paramref name="parent"/> (one at a time).</summary>
        public static SettingsScreen Open(Node parent)
        {
            if (Current != null && IsInstanceValid(Current)) return Current;
            var s = new SettingsScreen();
            parent.AddChild(s);
            return s;
        }

        private VBoxContainer _rows;

        public override void _Ready()
        {
            Current = this;
            Layer = 90;
            Name = "SettingsScreen";
            ProcessMode = ProcessModeEnum.Always;

            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.65f), MouseFilter = Control.MouseFilterEnum.Stop };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(dim);

            var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(center);

            var panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 0) };
            panel.AddThemeStyleboxOverride("panel", MetaUiStyle.Box(MetaUiStyle.Panel, MetaUiStyle.Frame, 2, 6, 24));
            center.AddChild(panel);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 10);
            panel.AddChild(col);
            col.AddChild(MetaUiStyle.Label("SETTINGS", 28, MetaUiStyle.Frame));

            _rows = new VBoxContainer();
            _rows.AddThemeConstantOverride("separation", 8);
            col.AddChild(_rows);

            Section("AUDIO");
            Slider("Master volume", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, 0, 1, true);
            Slider("Effects", () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, 0, 1, true);
            Slider("Ambience", () => GameSettings.AmbientVolume, v => GameSettings.AmbientVolume = v, 0, 1, true);
            Slider("Voices", () => GameSettings.VoiceVolume, v => GameSettings.VoiceVolume = v, 0, 1, true);
            Section("DISPLAY");
            Toggle("Fullscreen", () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);
            Toggle("VSync", () => GameSettings.VSync, v => GameSettings.VSync = v);
            FpsChoice();
            QualityChoice();
            Section("GAMEPLAY");
            Slider("Screen shake", () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v, 0, 1, true);
            Slider("Gunner view mouse speed", () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, 0.3f, 2.5f, false);
            Toggle("Damage numbers", () => GameSettings.DamageNumbers, v => GameSettings.DamageNumbers = v);
            Toggle("Show the enemy route", () => GameSettings.RoutePreview, v => GameSettings.RoutePreview = v);
            Toggle("BIT shoots by itself", () => GameSettings.BitAutoFire, v => GameSettings.BitAutoFire = v);

            var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
            col.AddChild(foot);
            var done = MetaUiStyle.Button("DONE [Esc]", MetaUiStyle.Go, new Vector2(200, 44));
            done.Name = "Done";
            done.Pressed += Close;
            foot.AddChild(done);
            done.CallDeferred(Control.MethodName.GrabFocus);
        }

        public override void _ExitTree()
        {
            if (Current == this) Current = null;
        }

        public override void _Input(InputEvent e)
        {
            if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            {
                GetViewport().SetInputAsHandled();
                Close();
            }
        }

        public void Close()
        {
            GameSettings.Save();
            Closed?.Invoke();
            QueueFree();
        }

        private void Section(string title)
        {
            var l = MetaUiStyle.Label(title, 15, MetaUiStyle.Currency);
            l.AddThemeConstantOverride("line_spacing", 0);
            _rows.AddChild(l);
        }

        private HBoxContainer Row(string label)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 16);
            var l = MetaUiStyle.Label(label, 16, MetaUiStyle.Text);
            l.CustomMinimumSize = new Vector2(280, 0);
            row.AddChild(l);
            _rows.AddChild(row);
            return row;
        }

        private void Slider(string label, Func<float> get, Action<float> set, float min, float max, bool percent)
        {
            var row = Row(label);
            var s = new HSlider { Name = label, MinValue = min, MaxValue = max, Step = (max - min) / 100f, Value = get(), CustomMinimumSize = new Vector2(300, 24), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            var val = MetaUiStyle.Label("", 15, MetaUiStyle.TextDim);
            val.CustomMinimumSize = new Vector2(60, 0);
            void Show(double v) => val.Text = percent ? $"{v * 100:0}%" : $"{v:0.0}x";
            Show(s.Value);
            s.ValueChanged += v => { set((float)v); Show(v); GameSettings.Apply(); };
            row.AddChild(s);
            row.AddChild(val);
        }

        private void Toggle(string label, Func<bool> get, Action<bool> set)
        {
            var row = Row(label);
            var b = new CheckButton { Name = label, ButtonPressed = get(), FocusMode = Control.FocusModeEnum.All };
            b.Toggled += on => { set(on); GameSettings.Apply(); };
            row.AddChild(b);
        }

        private void QualityChoice()
        {
            var row = Row("Graphics");
            var opt = new OptionButton { Name = "Graphics", CustomMinimumSize = new Vector2(160, 0) };
            for (int i = 0; i < GameSettings.QualityNames.Length; i++)
                opt.AddItem(GameSettings.QualityNames[i], i);
            opt.Select(GameSettings.Quality);
            opt.ItemSelected += i => { GameSettings.Quality = (int)i; GameSettings.Apply(); };
            row.AddChild(opt);
            var hint = MetaUiStyle.Label("shadows and model detail", 13, MetaUiStyle.TextDim);
            row.AddChild(hint);
        }

        private void FpsChoice()
        {
            var row = Row("Frame rate cap");
            var opt = new OptionButton { Name = "Frame rate cap", CustomMinimumSize = new Vector2(160, 0) };
            for (int i = 0; i < GameSettings.FpsChoices.Length; i++)
            {
                int f = GameSettings.FpsChoices[i];
                opt.AddItem(f == 0 ? "No cap" : $"{f} fps", i);
                if (f == GameSettings.MaxFps) opt.Select(i);
            }
            opt.ItemSelected += i => { GameSettings.MaxFps = GameSettings.FpsChoices[(int)i]; GameSettings.Apply(); };
            row.AddChild(opt);
        }
    }
}
