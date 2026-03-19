using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// FrameBudget - Maintains a minimum frame rate by deferring non-critical work.
    /// Ported from HoldtheLine's autoload. Registers with ServiceLocator.
    ///
    /// How it works: at the start of each frame, the clock resets. Components call
    /// HasBudget() before expensive work (AI targeting, retargeting, path queries).
    /// If the frame's script-time budget is exceeded, HasBudget() returns false and
    /// the work is deferred to the next frame. Movement and rendering are unaffected
    /// so the game stays visually smooth even under heavy load.
    /// </summary>
    public partial class FrameBudget : Node
    {
        /// <summary>Target minimum frame rate.</summary>
        public float MinFps { get; set; } = 30f;

        /// <summary>
        /// Script budget as a fraction of total frame time.
        /// 70% of the frame leaves 30% for rendering + engine overhead.
        /// At 30fps: frame = 33.3ms, script budget = ~23.3ms.
        /// </summary>
        private const float BUDGET_FRACTION = 0.7f;

        private long _frameStartUsec;
        private long _budgetUsec;

        /// <summary>True if this frame's budget was exceeded (cached to avoid repeated checks).</summary>
        public bool BudgetExceeded { get; private set; }

        public override void _Ready()
        {
            // Prevent the physics death spiral: when FPS drops, Godot tries to run
            // extra physics iterations to catch up, which makes the frame even longer.
            // Cap at 4 iterations so physics can fall behind gracefully instead.
            Engine.MaxPhysicsStepsPerFrame = 4;

            _budgetUsec = (long)(1_000_000.0 / MinFps * BUDGET_FRACTION);

            ServiceLocator.Register(this);
        }

        public override void _Process(double delta)
        {
            // Reset at the top of each frame.
            _frameStartUsec = (long)Time.GetTicksUsec();
            BudgetExceeded = false;
        }

        /// <summary>
        /// Returns true if there is still script-time budget remaining this frame.
        /// Cheap to call: one branch, one subtraction, one comparison.
        /// </summary>
        public bool HasBudget()
        {
            if (BudgetExceeded)
                return false;

            if ((long)Time.GetTicksUsec() - _frameStartUsec > _budgetUsec)
            {
                BudgetExceeded = true;
                return false;
            }

            return true;
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<FrameBudget>();
        }
    }
}
