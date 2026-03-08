using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Sound Designer — preview and tune procedural sound parameters.
    /// Lists all sound names, plays them on click, shows waveform info.
    /// Sector ambience preview with sector/ascension sliders.
    /// </summary>
    public partial class SoundDesigner : EditorPanel
    {
        public override string PanelName => "Sound";
        public override Color AccentColor => EditorStyles.AccentSound;

        private VBoxContainer _soundList;
        private Label _nowPlaying;
        private HSlider _sectorSlider;
        private HSlider _ascensionSlider;
        private Label _sectorLabel;
        private Label _ascensionLabel;

        private static readonly string[] SfxNames =
        {
            "hit", "crit_hit", "enemy_death", "swing", "pickup", "level_up",
            "projectile", "heal", "achievement", "box_shake", "box_open",
            "item_reveal", "heartbeat", "epic_drop",
            "celebration_fanfare", "celebration_confetti",
            "rifle", "shotgun_blast", "launcher_fire", "reload"
        };

        protected override void BuildUI(VBoxContainer content)
        {
            // Now playing indicator
            _nowPlaying = EditorStyles.MakeLabel("Click a sound to preview", EditorStyles.FontBody, EditorStyles.TextSecondary);
            content.AddChild(_nowPlaying);
            content.AddChild(EditorStyles.MakeSeparator());

            // Split: sound list (left) | ambience controls (right)
            var split = new HBoxContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.AddThemeConstantOverride("separation", 16);

            // Left: SFX list
            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

            var sfxTitle = EditorStyles.MakeLabel("Sound Effects", EditorStyles.FontHeader, AccentColor);
            leftPanel.AddChild(sfxTitle);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _soundList = new VBoxContainer();
            _soundList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _soundList.AddThemeConstantOverride("separation", 2);

            foreach (var name in SfxNames)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                var playBtn = EditorStyles.MakeButton("Play", EditorStyles.FontSmall, AccentColor);
                playBtn.CustomMinimumSize = new Vector2(50, 24);
                var captured = name;
                playBtn.Pressed += () => PlaySound(captured);
                row.AddChild(playBtn);

                var label = EditorStyles.MakeLabel(name, EditorStyles.FontSmall);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(label);

                _soundList.AddChild(row);
            }

            scroll.AddChild(_soundList);
            leftPanel.AddChild(scroll);
            split.AddChild(leftPanel);

            // Right: Ambience controls
            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightPanel.CustomMinimumSize = new Vector2(300, 0);

            var ambTitle = EditorStyles.MakeLabel("Sector Ambience", EditorStyles.FontHeader, AccentColor);
            rightPanel.AddChild(ambTitle);
            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Sector slider
            var sectorRow = new HBoxContainer();
            sectorRow.AddThemeConstantOverride("separation", 8);
            sectorRow.AddChild(EditorStyles.MakeLabel("Sector:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _sectorLabel = EditorStyles.MakeLabel("1", EditorStyles.FontSmall, AccentColor);
            _sectorLabel.CustomMinimumSize = new Vector2(20, 0);

            _sectorSlider = new HSlider();
            _sectorSlider.MinValue = 1;
            _sectorSlider.MaxValue = 8;
            _sectorSlider.Step = 1;
            _sectorSlider.Value = 1;
            _sectorSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _sectorSlider.ValueChanged += v => _sectorLabel.Text = ((int)v).ToString();
            sectorRow.AddChild(_sectorSlider);
            sectorRow.AddChild(_sectorLabel);
            rightPanel.AddChild(sectorRow);

            // Ascension slider
            var ascRow = new HBoxContainer();
            ascRow.AddThemeConstantOverride("separation", 8);
            ascRow.AddChild(EditorStyles.MakeLabel("Ascension:", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _ascensionLabel = EditorStyles.MakeLabel("0", EditorStyles.FontSmall, AccentColor);
            _ascensionLabel.CustomMinimumSize = new Vector2(20, 0);

            _ascensionSlider = new HSlider();
            _ascensionSlider.MinValue = 0;
            _ascensionSlider.MaxValue = 10;
            _ascensionSlider.Step = 1;
            _ascensionSlider.Value = 0;
            _ascensionSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _ascensionSlider.ValueChanged += v => _ascensionLabel.Text = ((int)v).ToString();
            ascRow.AddChild(_ascensionSlider);
            ascRow.AddChild(_ascensionLabel);
            rightPanel.AddChild(ascRow);

            // Play ambience button
            var playAmb = EditorStyles.MakeButton("Play Sector Ambience", EditorStyles.FontBody, AccentColor);
            playAmb.CustomMinimumSize = new Vector2(0, 32);
            playAmb.Pressed += PlayAmbience;
            rightPanel.AddChild(playAmb);

            var stopAmb = EditorStyles.MakeButton("Stop Music", EditorStyles.FontSmall, EditorStyles.StatusError);
            stopAmb.CustomMinimumSize = new Vector2(0, 28);
            stopAmb.Pressed += () =>
            {
                GetAudioManager()?.StopMusic();
                _nowPlaying.Text = "Stopped";
            };
            rightPanel.AddChild(stopAmb);

            rightPanel.AddChild(EditorStyles.MakeSeparator());

            // Sector frequency reference
            var freqTitle = EditorStyles.MakeLabel("Base Drone Frequencies", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            rightPanel.AddChild(freqTitle);
            var freqInfo = new string[]
            {
                "Sector 1: 55 Hz (A1 industrial)",
                "Sector 2: 49 Hz (G1 toxic)",
                "Sector 3: 41 Hz (E1 military)",
                "Sector 4: 46.25 Hz (Bb1 lab)",
                "Sector 5: 36.7 Hz (D1 core)",
                "Sector 6+: Scaled from base"
            };
            foreach (var info in freqInfo)
            {
                rightPanel.AddChild(EditorStyles.MakeLabel(info, EditorStyles.FontTiny, EditorStyles.TextMuted));
            }

            split.AddChild(rightPanel);
            content.AddChild(split);
        }

        private static AudioManager GetAudioManager()
        {
            return ServiceLocator.TryGet<AudioManager>(out var am) ? am : null;
        }

        private void PlaySound(string name)
        {
            _nowPlaying.Text = $"Playing: {name}";
            _nowPlaying.AddThemeColorOverride("font_color", AccentColor);
            GetAudioManager()?.PlaySFXByName(name);
        }

        private void PlayAmbience()
        {
            int sector = (int)_sectorSlider.Value;
            int ascension = (int)_ascensionSlider.Value;
            _nowPlaying.Text = $"Ambience: Sector {sector}, Ascension {ascension}";
            _nowPlaying.AddThemeColorOverride("font_color", AccentColor);
            GetAudioManager()?.PlayAscensionAmbience(sector, ascension);
        }

        protected override void Reload()
        {
            SetStatus("Sound preview ready", EditorStyles.StatusSaved);
        }

        protected override void Save()
        {
            SetStatus("No data to save (preview only)", EditorStyles.TextMuted);
        }

        protected override void RestoreSnapshot(string jsonSnapshot) { }
    }
}
