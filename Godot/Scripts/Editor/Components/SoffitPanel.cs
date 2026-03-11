using Godot;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Panel control with chamfered (soffit) corners — diagonal cuts instead of rounded.
    /// Uses DrawColoredPolygon for the background and DrawPolyline for the border.
    /// Set BevelSize per-corner (TL, TR, BL, BR). Zero = sharp corner.
    /// </summary>
    public partial class SoffitPanel : Control
    {
        public Color BgColor { get; set; } = new(0.08f, 0.08f, 0.12f, 0.95f);
        public Color BorderColor { get; set; } = new(0.3f, 0.3f, 0.35f);
        public float BorderWidth { get; set; } = 2f;

        public float BevelTL { get; set; } = 12f;
        public float BevelTR { get; set; } = 12f;
        public float BevelBL { get; set; } = 12f;
        public float BevelBR { get; set; } = 12f;

        public void SetBevelAll(float bevel)
        {
            BevelTL = BevelTR = BevelBL = BevelBR = bevel;
        }

        public override void _Draw()
        {
            var s = Size;
            float tl = Mathf.Min(BevelTL, Mathf.Min(s.X, s.Y) * 0.4f);
            float tr = Mathf.Min(BevelTR, Mathf.Min(s.X, s.Y) * 0.4f);
            float bl = Mathf.Min(BevelBL, Mathf.Min(s.X, s.Y) * 0.4f);
            float br = Mathf.Min(BevelBR, Mathf.Min(s.X, s.Y) * 0.4f);

            // Build polygon clockwise from top-left bevel
            var pts = new Vector2[]
            {
                new(tl, 0),            // top-left → right of bevel
                new(s.X - tr, 0),      // top-right → left of bevel
                new(s.X, tr),           // top-right → below bevel
                new(s.X, s.Y - br),    // bottom-right → above bevel
                new(s.X - br, s.Y),    // bottom-right → left of bevel
                new(bl, s.Y),           // bottom-left → right of bevel
                new(0, s.Y - bl),       // bottom-left → above bevel
                new(0, tl),             // top-left → below bevel
            };

            DrawColoredPolygon(pts, BgColor);

            if (BorderWidth > 0)
            {
                // Close the loop for polyline
                var border = new Vector2[pts.Length + 1];
                for (int i = 0; i < pts.Length; i++) border[i] = pts[i];
                border[pts.Length] = pts[0];
                DrawPolyline(border, BorderColor, BorderWidth, true);
            }
        }
    }
}
