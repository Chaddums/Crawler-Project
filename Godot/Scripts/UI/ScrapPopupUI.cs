using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Displays floating "+X Scrap" notifications that drift upward and fade out.
    /// Batches rapid gains into a single popup to avoid spam.
    /// </summary>
    public partial class ScrapPopupUI : CanvasLayer
    {
        private static readonly Color ScrapColor = new(0.95f, 0.75f, 0.15f);
        private static readonly Color ScrapShadow = new(0.2f, 0.15f, 0.05f, 0.7f);

        private const float POPUP_DURATION = 1.5f;
        private const float DRIFT_SPEED = 60f;
        private const float BATCH_WINDOW = 0.4f;

        private int _batchedAmount;
        private float _batchTimer;
        private readonly List<PopupEntry> _active = new();

        private struct PopupEntry
        {
            public Label Label;
            public Label Shadow;
            public float Elapsed;
            public Vector2 StartPos;
        }

        public override void _Ready()
        {
            Layer = 90;
            GameEvents.OnScrapEarned += OnScrapEarned;
        }

        private void OnScrapEarned(int amount)
        {
            _batchedAmount += amount;
            _batchTimer = BATCH_WINDOW;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Batch timer — spawn popup when batch window expires
            if (_batchedAmount > 0)
            {
                _batchTimer -= dt;
                if (_batchTimer <= 0f)
                {
                    SpawnPopup(_batchedAmount);
                    _batchedAmount = 0;
                }
            }

            // Animate active popups
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var entry = _active[i];
                entry.Elapsed += dt;

                if (entry.Elapsed >= POPUP_DURATION)
                {
                    entry.Label.QueueFree();
                    entry.Shadow.QueueFree();
                    _active.RemoveAt(i);
                    continue;
                }

                float t = entry.Elapsed / POPUP_DURATION;
                float yOffset = -DRIFT_SPEED * entry.Elapsed;
                float alpha = 1f - t * t; // ease-out fade

                var pos = entry.StartPos + new Vector2(0, yOffset);
                entry.Label.Position = pos;
                entry.Shadow.Position = pos + new Vector2(2, 2);

                var col = ScrapColor;
                col.A = alpha;
                entry.Label.AddThemeColorOverride("font_color", col);

                var shCol = ScrapShadow;
                shCol.A = alpha * 0.7f;
                entry.Shadow.AddThemeColorOverride("font_color", shCol);

                // Scale pop on spawn
                float scale = t < 0.1f ? Mathf.Lerp(1.3f, 1f, t / 0.1f) : 1f;
                entry.Label.Scale = new Vector2(scale, scale);
                entry.Shadow.Scale = new Vector2(scale, scale);

                _active[i] = entry;
            }
        }

        private void SpawnPopup(int amount)
        {
            // Stack offset so multiple popups don't overlap
            float yBase = 500f;
            foreach (var existing in _active)
            {
                if (existing.Elapsed < 0.6f)
                    yBase -= 30f;
            }

            string text = $"+{amount} Scrap";
            var startPos = new Vector2(960f - 60f, yBase);

            // Shadow label (drawn first, behind)
            var shadow = new Label();
            shadow.Text = text;
            shadow.Position = startPos + new Vector2(2, 2);
            shadow.AddThemeFontSizeOverride("font_size", 22);
            shadow.AddThemeColorOverride("font_color", ScrapShadow);
            AddChild(shadow);

            // Main label
            var label = new Label();
            label.Text = text;
            label.Position = startPos;
            label.AddThemeFontSizeOverride("font_size", 22);
            label.AddThemeColorOverride("font_color", ScrapColor);
            AddChild(label);

            _active.Add(new PopupEntry
            {
                Label = label,
                Shadow = shadow,
                Elapsed = 0f,
                StartPos = startPos,
            });
        }

        public override void _ExitTree()
        {
            GameEvents.OnScrapEarned -= OnScrapEarned;
        }
    }
}
