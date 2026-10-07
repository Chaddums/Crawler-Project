using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Perk selection (pick 1 of 3) shown at perk_select wave milestones.
    /// Runs as an overlay inside the battle scene: the tree is paused while it is
    /// open and the run resumes untouched when a perk is chosen. (Loading it as a
    /// separate scene rebuilt the battle and wiped towers, resources and wave count.)
    /// Code-built UI (same pattern as VineDraftScreen).
    /// </summary>
    public partial class VinePerkScreen : CanvasLayer
    {
        private List<PerkData> _choices;
        private bool _resolved;

        /// <summary>True while an in-battle perk overlay is open. PauseMenu checks this.</summary>
        public static bool IsOverlayOpen { get; private set; }

        /// <summary>
        /// True when shown over a live battle (pauses the tree, frees itself on pick).
        /// False for the legacy standalone scene (VinePerkSelect.tscn).
        /// </summary>
        public bool InBattleOverlay { get; set; }

        /// <summary>Fired after a perk has been applied and the overlay is closing.</summary>
        public event System.Action Closed;

        public override void _Ready()
        {
            Layer = 10;

            if (InBattleOverlay)
            {
                ProcessMode = ProcessModeEnum.Always;
                IsOverlayOpen = true;
                GetTree().Paused = true;
            }

            // Pick 3 random perks excluding already-selected ones
            var gm = GameManager.Instance;
            _choices = VinePerkRegistry.PickRandom(3, gm?.ActivePerks);

            if (_choices.Count == 0 && InBattleOverlay)
            {
                // Pool exhausted — nothing to offer, resume immediately
                GD.Print("[VinePerk] No perks left to offer — skipping milestone");
                CallDeferred(nameof(Close));
                return;
            }

            BuildUI();
        }

        public override void _ExitTree()
        {
            if (InBattleOverlay)
                IsOverlayOpen = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    GetViewport().SetInputAsHandled();
                    // Overlay: a pick is required to continue — swallow ESC so the
                    // pause menu can't unpause the run underneath the overlay.
                    if (!InBattleOverlay)
                        GameManager.Instance?.ReturnToMainMenu();
                    return;
                }

                // 1 / 2 / 3 pick a card
                int pick = key.Keycode switch
                {
                    Key.Key1 or Key.Kp1 => 0,
                    Key.Key2 or Key.Kp2 => 1,
                    Key.Key3 or Key.Kp3 => 2,
                    _ => -1,
                };
                if (pick >= 0 && _choices != null && pick < _choices.Count)
                {
                    GetViewport().SetInputAsHandled();
                    OnPerkSelected(pick);
                }
            }
        }

        /// <summary>Wave that triggered this pick (shown in the title).</summary>
        public int Wave { get; set; }

        private readonly List<Control> _cardControls = new();

        private void BuildUI()
        {
            var gm = GameManager.Instance;
            // Translucent over a live battle so the field stays visible behind the pick
            var root = MetaUiStyle.Backdrop(InBattleOverlay ? 0.86f : 1f);
            root.Modulate = new Color(1, 1, 1, 0);
            AddChild(root);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(center);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 14);
            center.AddChild(vbox);

            int wave = Wave > 0 ? Wave : gm?.CurrentWave ?? 0;
            vbox.AddChild(MetaUiStyle.Label(wave > 0 ? $"WAVE {wave} MILESTONE" : "MILESTONE REACHED",
                34, MetaUiStyle.Go, HorizontalAlignment.Center));
            vbox.AddChild(MetaUiStyle.Label("Pick one upgrade. It lasts for the rest of this run.",
                17, MetaUiStyle.TextDim, HorizontalAlignment.Center));

            if (gm != null && wave > 0 && gm.MetaPointsByWave.TryGetValue(wave, out int banked) && banked > 0)
                vbox.AddChild(MetaUiStyle.Label($"+{banked} perk point{(banked == 1 ? "" : "s")} banked for the Perk Tree",
                    15, MetaUiStyle.Currency, HorizontalAlignment.Center));

            var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
            vbox.AddChild(spacer);

            var cardRow = new HBoxContainer();
            cardRow.AddThemeConstantOverride("separation", 22);
            cardRow.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(cardRow);

            for (int i = 0; i < _choices.Count; i++)
                BuildPerkCard(cardRow, i);

            // What's already running this run, so picks can stack deliberately
            var active = gm?.ActivePerks;
            if (active != null && active.Count > 0)
            {
                var names = new List<string>();
                foreach (var p in active) names.Add(p.Name);
                var activeLbl = MetaUiStyle.Label("Active this run: " + string.Join(", ", names),
                    14, MetaUiStyle.TextFaint, HorizontalAlignment.Center);
                activeLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                activeLbl.CustomMinimumSize = new Vector2(900, 0);
                vbox.AddChild(activeLbl);
            }

            vbox.AddChild(MetaUiStyle.Label(InBattleOverlay ? "Click a card or press 1, 2 or 3" : "ESC to go back",
                13, MetaUiStyle.TextFaint, HorizontalAlignment.Center));

            // Fade in, then the cards one after another (runs while the tree is paused)
            var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
            tween.TweenProperty(root, "modulate:a", 1f, 0.18f);
            foreach (var c in _cardControls)
                tween.TweenProperty(c, "modulate:a", 1f, 0.14f);
        }

        private void BuildPerkCard(HBoxContainer parent, int index)
        {
            var perk = _choices[index];
            var col = perk.Color;

            // The whole card is the button
            var card = new Button
            {
                CustomMinimumSize = new Vector2(300, 250),
                FocusMode = Control.FocusModeEnum.All,
                Modulate = new Color(1, 1, 1, 0),
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            };
            var normal = MetaUiStyle.Box(new Color(MetaUiStyle.Panel.R, MetaUiStyle.Panel.G, MetaUiStyle.Panel.B, 0.96f),
                new Color(col.R, col.G, col.B, 0.45f), 2, 6, 0);
            var hover = MetaUiStyle.Box(new Color(col.R * 0.14f + 0.04f, col.G * 0.14f + 0.05f, col.B * 0.14f + 0.08f, 1f),
                col, 3, 6, 0);
            hover.ShadowColor = new Color(col.R, col.G, col.B, 0.25f);
            hover.ShadowSize = 14;
            card.AddThemeStyleboxOverride("normal", normal);
            card.AddThemeStyleboxOverride("hover", hover);
            card.AddThemeStyleboxOverride("pressed", hover);
            card.AddThemeStyleboxOverride("focus", hover);
            parent.AddChild(card);
            _cardControls.Add(card);

            // Colour band across the top
            var band = new ColorRect { Color = col, MouseFilter = Control.MouseFilterEnum.Ignore };
            band.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            band.OffsetLeft = 2; band.OffsetRight = -2; band.OffsetTop = 2; band.OffsetBottom = 8;
            card.AddChild(band);

            var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            v.OffsetLeft = 22; v.OffsetRight = -22; v.OffsetTop = 26; v.OffsetBottom = -20;
            v.AddThemeConstantOverride("separation", 12);
            card.AddChild(v);

            var key = MetaUiStyle.Label($"{index + 1}", 14, new Color(col.R, col.G, col.B, 0.7f));
            key.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(key);

            var nameLabel = MetaUiStyle.Label(perk.Name.ToUpper(), 23, col, HorizontalAlignment.Center);
            nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(nameLabel);

            var rule = new ColorRect
            {
                Color = new Color(col.R, col.G, col.B, 0.3f),
                CustomMinimumSize = new Vector2(0, 1),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            v.AddChild(rule);

            var desc = MetaUiStyle.Label(perk.Description, 18, MetaUiStyle.Text, HorizontalAlignment.Center);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(desc);

            var fill = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
            v.AddChild(fill);

            var take = MetaUiStyle.Label("TAKE", 15, new Color(col.R, col.G, col.B, 0.85f), HorizontalAlignment.Center);
            take.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(take);

            int capturedIndex = index;
            card.Pressed += () => OnPerkSelected(capturedIndex);
            if (index == 0) card.CallDeferred(Control.MethodName.GrabFocus);
        }

        private void OnPerkSelected(int index)
        {
            if (_resolved) return; // Guard against double-click
            var perk = _choices[index];
            var gm = GameManager.Instance;
            if (gm == null) return;

            GD.Print($"[VinePerk] P{gm.CurrentPlanet}-W{gm.CurrentWave} selected: {perk.Name}");
            gm.AddPerk(perk);

            if (InBattleOverlay)
            {
                Close();
                return;
            }

            // Legacy standalone scene path (only reachable via MetaPerkTreeScreen)
            _resolved = true;
            gm.SetPhase(GamePhase.Build);
            GetTree().ChangeSceneToFile(Constants.SCENE_VINE_BATTLE);
        }

        private void Close()
        {
            if (_resolved) return;
            _resolved = true;
            IsOverlayOpen = false;
            if (InBattleOverlay)
                GetTree().Paused = false;
            Closed?.Invoke();
            QueueFree();
        }
    }
}
