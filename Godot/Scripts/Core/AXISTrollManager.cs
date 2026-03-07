using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Timer-based service that fires cosmetic troll events from AXIS (the arena AI).
    /// Randomly triggers every 60-120 seconds during gameplay.
    /// </summary>
    public partial class AXISTrollManager : Node
    {
        private float _nextTrollTime;
        private float _elapsed;
        private bool _active;

        private const float MIN_INTERVAL = 60f;
        private const float MAX_INTERVAL = 120f;
        private const int TROLL_COUNT = 5;

        public override void _Ready()
        {
            ServiceLocator.Register(this);
            _active = true;
            ScheduleNext();
            GD.Print("[AXISTrollManager] AXIS is watching...");
        }

        public override void _Process(double delta)
        {
            if (!_active) return;
            if (GameManager.Instance?.CurrentState != GameState.InSector) return;

            _elapsed += (float)delta;
            if (_elapsed >= _nextTrollTime)
            {
                ExecuteRandomTroll();
                ScheduleNext();
            }
        }

        private void ScheduleNext()
        {
            _elapsed = 0f;
            _nextTrollTime = (float)GD.RandRange(MIN_INTERVAL, MAX_INTERVAL);
        }

        private void ExecuteRandomTroll()
        {
            int roll = (int)(GD.Randi() % TROLL_COUNT);
            switch (roll)
            {
                case 0: TrollInventoryShuffle(); break;
                case 1: TrollUIDodge(); break;
                case 2: TrollEnemySwap(); break;
                case 3: TrollFakeLevelUp(); break;
                case 4: TrollTimerGlitch(); break;
            }
        }

        // --- Troll 1: Inventory Shuffle ---
        private void TrollInventoryShuffle()
        {
            if (!ServiceLocator.TryGet<PlayerController>(out var player)) return;
            var inv = player.Inventory;
            if (inv.ItemCount < 2) return;

            int a = (int)(GD.Randi() % (uint)inv.ItemCount);
            int b = (int)(GD.Randi() % (uint)inv.ItemCount);
            while (b == a && inv.ItemCount > 1)
                b = (int)(GD.Randi() % (uint)inv.ItemCount);

            inv.SwapSlots(a, b);

            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                commentary.QueueLine("AXIS", StringLoader.Get("trollEvents.inventoryShuffle"),
                    CommentaryPriority.Medium, CommentaryCategory.LootReaction);

            GD.Print("[AXISTrollManager] Inventory shuffle triggered");
        }

        // --- Troll 2: UI Dodge ---
        private void TrollUIDodge()
        {
            if (!ServiceLocator.TryGet<LiftTimerUI>(out var timerUI)) return;

            float offset = (float)GD.RandRange(20.0, 50.0) * (GD.Randf() > 0.5f ? 1f : -1f);
            timerUI.SetVisualOffset(offset);

            // Revert after 1.5 seconds
            GetTree().CreateTimer(1.5).Timeout += () => timerUI.ClearVisualOffset();

            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                commentary.QueueLine("AXIS", StringLoader.Get("trollEvents.uiDodge"),
                    CommentaryPriority.Low, CommentaryCategory.LootReaction);

            GD.Print("[AXISTrollManager] UI dodge triggered");
        }

        // --- Troll 3: Enemy Swap ---
        private void TrollEnemySwap()
        {
            var enemies = GetTree().GetNodesInGroup(Constants.GROUP_ENEMY);
            if (enemies.Count < 2) return;

            int a = (int)(GD.Randi() % (uint)enemies.Count);
            int b = (int)(GD.Randi() % (uint)enemies.Count);
            while (b == a)
                b = (int)(GD.Randi() % (uint)enemies.Count);

            if (enemies[a] is Node3D enemyA && enemies[b] is Node3D enemyB)
            {
                (enemyA.GlobalPosition, enemyB.GlobalPosition) = (enemyB.GlobalPosition, enemyA.GlobalPosition);
            }

            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                commentary.QueueLine("AXIS", StringLoader.Get("trollEvents.enemySwap"),
                    CommentaryPriority.Medium, CommentaryCategory.CombatReaction);

            GD.Print("[AXISTrollManager] Enemy swap triggered");
        }

        // --- Troll 4: Fake Level Up ---
        private void TrollFakeLevelUp()
        {
            var canvas = new CanvasLayer();
            canvas.Layer = 80;
            AddChild(canvas);

            var label = new Label();
            label.Text = StringLoader.Get("trollEvents.fakeLevelUp");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.AddThemeFontSizeOverride("font_size", 52);
            label.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
            label.OffsetTop = -100;
            canvas.AddChild(label);

            // Flash in
            label.Modulate = new Color(1, 1, 1, 0);
            var tween = CreateTween();
            tween.TweenProperty(label, "modulate:a", 1f, 0.2f);
            tween.TweenInterval(1.5f);

            // Retract — replace with "JUST KIDDING"
            tween.TweenCallback(Callable.From(() =>
            {
                label.Text = StringLoader.Get("trollEvents.fakeLevelUpReveal");
                label.AddThemeColorOverride("font_color", new Color(0.8f, 0.3f, 0.3f));
            }));
            tween.TweenInterval(1.0f);

            // Fade out and clean up
            tween.TweenProperty(label, "modulate:a", 0f, 0.3f);
            tween.TweenCallback(Callable.From(() => canvas.QueueFree()));

            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                commentary.QueueLine("AXIS", StringLoader.Get("trollEvents.fakeLevelUpCommentary"),
                    CommentaryPriority.High, CommentaryCategory.CombatReaction);

            GD.Print("[AXISTrollManager] Fake level up triggered");
        }

        // --- Troll 5: Timer Glitch ---
        private void TrollTimerGlitch()
        {
            if (!ServiceLocator.TryGet<LiftTimerUI>(out var timerUI)) return;

            float fakeOffset = (float)GD.RandRange(-45.0, -15.0);
            timerUI.SetVisualOffset(fakeOffset);

            // Revert after 3 seconds
            GetTree().CreateTimer(3.0).Timeout += () => timerUI.ClearVisualOffset();

            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                commentary.QueueLine("AXIS", StringLoader.Get("trollEvents.timerGlitch"),
                    CommentaryPriority.Medium, CommentaryCategory.SectorIntro);

            GD.Print("[AXISTrollManager] Timer glitch triggered");
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<AXISTrollManager>();
        }
    }
}
