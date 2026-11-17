using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The ending: shown once the last boss fight is won (after its debrief, or from the Command
    /// Center while the campaign stands complete). AXIS and BIT have the last words (Data/story.json
    /// "ending"), the campaign's numbers follow, and one button begins the next campaign, harder,
    /// with the perk tree, the bank and the store's items kept (<see cref="NewCampaign"/>).
    /// Space, Enter or a click shows every line at once; again begins the next campaign.
    /// </summary>
    public partial class EndingScreen : CanvasLayer
    {
        private static readonly Color Gold = new(1f, 0.78f, 0.3f);
        private static readonly Color Dim = new(0.62f, 0.68f, 0.76f);

        private readonly List<Control> _reveal = new();
        private int _shown;
        private float _clock;
        private Button _begin;
        private MetaPerkSaveData _save;

        /// <summary>Seconds between one line and the next.</summary>
        public const float LineEvery = 2.4f;

        /// <summary>Tests: every line and the button are on screen.</summary>
        public bool AllShown => _shown >= _reveal.Count;
        public int LineCount { get; private set; }
        public string ButtonText => _begin?.Text ?? "";

        public override void _Ready()
        {
            Layer = 10;
            _save = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();
            Build();
        }

        private void Build()
        {
            var bg = new ColorRect { Color = new Color(0.015f, 0.02f, 0.04f) };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);
            AddChild(new GridBackdrop { Modulate = new Color(1, 1, 1, 0.35f) });

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(center);
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(860, 0) };
            box.AddThemeConstantOverride("separation", 14);
            center.AddChild(box);

            int number = NewCampaign.Number(_save);
            bool due = NewCampaign.EndingDue(_save);
            box.AddChild(L(due ? $"CAMPAIGN {NewCampaign.Roman(number)} COMPLETE" : NewCampaign.Name(_save).ToUpperInvariant(), 44, Gold, true));
            box.AddChild(L(due ? "Both planets are taken. Every boss is destroyed." : "This campaign is still being fought.", 18, Dim, true));
            box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

            // The last words, one at a time
            foreach (var line in EndingLines(_save))
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 14);
                var who = L(line[0], 15, Comms.ColorOf(line[0] == Comms.Bit ? Comms.Bit : Comms.Axis));
                who.CustomMinimumSize = new Vector2(64, 0);
                who.VerticalAlignment = VerticalAlignment.Top;
                row.AddChild(who);
                var text = L(line[1], 20, new Color(0.9f, 0.93f, 0.98f));
                text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(text);
                Hide(row);
                box.AddChild(row);
                LineCount++;
            }

            // The campaign in numbers
            var stats = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            stats.AddThemeConstantOverride("separation", 34);
            foreach (var (label, value) in NewCampaign.Stats(_save))
            {
                var cell = new VBoxContainer();
                cell.AddThemeConstantOverride("separation", 0);
                cell.AddChild(L(value, 26, new Color(0.4f, 0.99f, 0.9f), true));
                cell.AddChild(L(label.ToUpperInvariant(), 12, Dim, true));
                stats.AddChild(cell);
            }
            var statsPad = new MarginContainer();
            statsPad.AddThemeConstantOverride("margin_top", 16);
            statsPad.AddChild(stats);
            Hide(statsPad);
            box.AddChild(statsPad);

            // What the next campaign keeps, and what it costs
            int harder = NewCampaign.NextStepPercent(_save);
            var next = L(due
                ? $"Campaign {NewCampaign.Roman(number + 1)} starts over: every site and region is given back, and their buffs with them. "
                  + $"Your perk tree, banked Resources, store items, relics and suits stay. Every enemy has {harder}% more health."
                : "Win the last boss fight to end it.", 16, Dim, true);
            next.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Hide(next);
            box.AddChild(next);

            _begin = new Button
            {
                Name = "BeginButton",
                Text = due ? $"BEGIN CAMPAIGN {NewCampaign.Roman(number + 1)}  ▶" : "BACK TO THE COMMAND CENTER  ▶",
                CustomMinimumSize = new Vector2(420, 60), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            };
            _begin.AddThemeFontSizeOverride("font_size", 20);
            _begin.Pressed += BeginNext;
            _begin.Disabled = true; // until it shows: a click where it will be must not begin anything
            Hide(_begin);
            box.AddChild(_begin);

            var hint = L("Space: show everything, then begin", 14, new Color(0.45f, 0.5f, 0.58f), true);
            box.AddChild(hint);
            _clock = 0.6f;
        }

        private void Hide(Control c)
        {
            c.Modulate = new Color(1, 1, 1, 0);
            _reveal.Add(c);
        }

        public override void _Process(double delta)
        {
            if (_shown >= _reveal.Count) return;
            if ((_clock -= (float)delta) > 0f) return;
            ShowNext();
            // The numbers, what follows and the button come quickly once the words are done
            _clock = _shown <= LineCount ? LineEvery : 0.5f;
        }

        private void ShowNext()
        {
            if (_shown >= _reveal.Count) return;
            var c = _reveal[_shown++];
            var tween = CreateTween();
            tween.TweenProperty(c, "modulate:a", 1f, 0.45f);
            if (_shown >= _reveal.Count) { _begin.Disabled = false; _begin.GrabFocus(); }
        }

        /// <summary>Every line, the numbers and the button, now.</summary>
        public void ShowAll()
        {
            while (_shown < _reveal.Count)
            {
                _reveal[_shown++].Modulate = Colors.White;
            }
            _begin.Disabled = false;
            _begin.GrabFocus();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            bool go = @event is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Space or Key.Enter or Key.KpEnter
                || @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
            if (!go) return;
            GetViewport().SetInputAsHandled();
            if (!AllShown) ShowAll();
            else if (@event is InputEventKey) BeginNext();
        }

        /// <summary>The button: begin the next campaign (when this one is finished) and go to the Command Center.</summary>
        public void BeginNext()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.MetaSave ??= MetaPerkSave.Load();
            NewCampaign.Begin(gm.MetaSave);
            gm.ShowMetaHub();
        }

        /// <summary>The ending's words for this save: the first campaign's, or a later one's ({directive}, {deployment}, {campaign} filled in).</summary>
        public static List<string[]> EndingLines(MetaPerkSaveData save)
        {
            var data = StoryDirector.Data.Ending;
            int done = save?.CampaignsCompleted ?? 0;
            var src = data.TryGetValue(done == 0 ? "first" : "again", out var l) && l.Count > 0 ? l
                : data.TryGetValue("first", out var f) ? f : new List<string[]>();
            var list = new List<string[]>();
            foreach (var line in src)
            {
                if (line == null || line.Length < 2) continue;
                list.Add(new[] { line[0], line[1]
                    .Replace("{directive}", (848 + done).ToString())
                    .Replace("{deployment}", StoryDirector.Deployment(save?.RunCount ?? 0).ToString())
                    .Replace("{campaign}", NewCampaign.Roman(done + 1))
                    .Replace("{next}", NewCampaign.Roman(done + 2)) });
            }
            return list;
        }

        private static Label L(string text, int size, Color color, bool centered = false)
        {
            var l = new Label { Text = text, HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            return l;
        }
    }
}
