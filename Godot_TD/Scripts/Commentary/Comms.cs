using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The one place AXIS and BIT speak: a comms panel at the top centre, under the top bar, one
    /// line at a time with the speaker's name in their colour. Story lines (StoryDirector, chaos,
    /// Ascendants) always get their turn; chatter (a tower placed, a leak) is dropped when the
    /// feed is busy and goes stale after a few seconds in the queue.
    ///
    /// Before 2026-10 AXIS spoke in a box at the top centre and BIT in another at the top left,
    /// BIT queued a line for a quarter of every kill (a wave left minutes of them to play out, out
    /// of context), the chaos lines went to an event nothing showed, and the memory lines BIT was
    /// written with were never used. The playtest's verdict was that the story was gone.
    /// </summary>
    public partial class Comms : Node
    {
        public const string Axis = "AXIS", Bit = "BIT";
        private const int MaxStory = 12;
        private const float ChatterStale = 6f, Gap = 0.5f;

        private static Comms _current;

        private struct Line
        {
            public string Speaker, Text;
            public bool Story;
            public double At;
        }

        private readonly List<Line> _queue = new();
        private PanelContainer _panel;
        private StyleBoxFlat _style;
        private Label _name, _text;
        private float _showFor, _gap;
        private double _clock;

        /// <summary>Lines shown since the scene began, and the last one (tests).</summary>
        public static int Shown { get; private set; }
        public static string LastSpeaker { get; private set; }
        public static string LastText { get; private set; }
        /// <summary>What is on the panel now (null when it's hidden).</summary>
        public static string Showing => _current != null && IsInstanceValid(_current) && _current._panel.Visible ? _current._text.Text : null;
        public static int Queued => _current != null && IsInstanceValid(_current) ? _current._queue.Count : 0;

        public static Color ColorOf(string speaker) => speaker == Axis ? new Color(1f, 0.58f, 0.28f) : new Color(0.72f, 0.86f, 0.98f);

        /// <summary>Say a line. <paramref name="story"/>: it waits its turn however long that takes.</summary>
        public static void Say(string speaker, string text, bool story = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var c = _current;
            if (c == null || !IsInstanceValid(c)) return;
            if (!story)
            {
                // Chatter only into a quiet feed: a story line or two waiting means not now
                if (c._queue.Count >= 1 || (c._panel.Visible && c._showFor > 1.5f && c._showingStory)) return;
            }
            else if (c._queue.Count >= MaxStory) return;
            c._queue.Add(new Line { Speaker = speaker, Text = text, Story = story, At = c._clock });
        }

        private bool _showingStory;

        public override void _Ready()
        {
            _current = this;
            Shown = 0;
            // Keeps talking over the end-of-run screen (the run-end lines come with it)
            ProcessMode = ProcessModeEnum.Always;

            var canvas = new CanvasLayer { Layer = 10 };
            AddChild(canvas);
            _panel = new PanelContainer { Name = "Comms" };
            // Top centre under the top bar: the getting-started list has the top left
            _panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _panel.OffsetLeft = -245;
            _panel.OffsetRight = 245;
            _panel.OffsetTop = 60;
            _panel.GrowHorizontal = Control.GrowDirection.Both;
            _style = new StyleBoxFlat { BgColor = new Color(0.04f, 0.045f, 0.07f, 0.78f) };
            _style.SetCornerRadiusAll(6);
            _style.BorderWidthLeft = 3;
            _style.ContentMarginLeft = 12;
            _style.ContentMarginRight = 14;
            _style.ContentMarginTop = 6;
            _style.ContentMarginBottom = 8;
            _panel.AddThemeStyleboxOverride("panel", _style);
            _panel.MouseFilter = Control.MouseFilterEnum.Ignore;
            canvas.AddChild(_panel);

            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 1);
            box.MouseFilter = Control.MouseFilterEnum.Ignore;
            _panel.AddChild(box);
            _name = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
            _name.AddThemeFontSizeOverride("font_size", 14);
            box.AddChild(_name);
            _text = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(462, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _text.AddThemeFontSizeOverride("font_size", 15);
            box.AddChild(_text);
            _panel.Visible = false;

            GameEvents.OnCommentary += OnCommentary;
        }

        public override void _ExitTree()
        {
            GameEvents.OnCommentary -= OnCommentary;
            if (_current == this) _current = null;
        }

        // Chaos and corruption speak through the event
        private void OnCommentary(string speaker, string text) => Say(speaker == Bit ? Bit : Axis, text, story: true);

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _clock += dt;
            if (_showFor > 0f)
            {
                _showFor -= dt;
                if (_showFor <= 0f) { _panel.Visible = false; _gap = Gap; }
                return;
            }
            if (_gap > 0f) { _gap -= dt; return; }

            // Drop chatter that waited too long: it was about a moment that has passed
            _queue.RemoveAll(l => !l.Story && _clock - l.At > ChatterStale);
            if (_queue.Count == 0) return;
            var line = _queue[0];
            _queue.RemoveAt(0);
            Show(line);
        }

        private void Show(Line line)
        {
            var col = ColorOf(line.Speaker);
            _name.Text = line.Speaker;
            _name.AddThemeColorOverride("font_color", col);
            _text.Text = line.Text;
            _text.AddThemeColorOverride("font_color", line.Speaker == Axis ? new Color(0.95f, 0.92f, 0.88f) : new Color(0.74f, 0.77f, 0.8f));
            _style.BorderColor = col;
            _panel.Visible = true;
            _showingStory = line.Story;
            // BIT lingers longer: it says less and means more
            _showFor = (2.6f + line.Text.Length * 0.045f) * (line.Speaker == Bit ? 1.25f : 1f);
            Shown++;
            LastSpeaker = line.Speaker;
            LastText = line.Text;
            if (ServiceLocator.TryGet<AudioManager>(out var audio)) audio.PlaySFXByName("commentary_ping");
        }
    }
}
