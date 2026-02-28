using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Small billboard health bar above enemy heads using quad meshes.
    /// Hidden at full health, shown on first damage.
    /// </summary>
    public partial class EnemyHealthBar3D : Node3D
    {
        private const float BAR_WIDTH = 1.2f;
        private const float BAR_HEIGHT = 0.12f;

        private MeshInstance3D _bgMesh;
        private MeshInstance3D _fillMesh;
        private StandardMaterial3D _fillMat;
        private float _currentPercent = 1f;
        private bool _hasBeenDamaged;

        private static readonly Color ColorHigh = new(0.2f, 0.8f, 0.2f);
        private static readonly Color ColorMid = new(0.9f, 0.7f, 0.1f);
        private static readonly Color ColorLow = new(0.8f, 0.15f, 0.15f);
        private static readonly Color BgColor = new(0.1f, 0.1f, 0.1f, 0.8f);

        public override void _Ready()
        {
            // Background quad
            _bgMesh = CreateQuad(BAR_WIDTH, BAR_HEIGHT, BgColor, 0f);
            AddChild(_bgMesh);

            // Fill quad (slightly in front of bg)
            _fillMat = CreateBillboardMaterial(ColorHigh);
            _fillMesh = new MeshInstance3D();
            var fillQuad = new QuadMesh();
            fillQuad.Size = new Vector2(BAR_WIDTH, BAR_HEIGHT);
            _fillMesh.Mesh = fillQuad;
            _fillMesh.MaterialOverride = _fillMat;
            _fillMesh.Position = new Vector3(0, 0, 0.001f);
            AddChild(_fillMesh);

            Visible = false;
        }

        public void UpdateHealth(float current, float max)
        {
            if (max <= 0f) return;
            float pct = Mathf.Clamp(current / max, 0f, 1f);
            _currentPercent = pct;

            if (pct < 1f && !_hasBeenDamaged)
            {
                _hasBeenDamaged = true;
                Visible = true;
            }

            // Scale fill on X axis, shift to keep left-aligned
            float fillWidth = BAR_WIDTH * pct;
            float offset = (BAR_WIDTH - fillWidth) * -0.5f;
            _fillMesh.Scale = new Vector3(pct, 1f, 1f);
            _fillMesh.Position = new Vector3(offset, 0, 0.001f);

            // Color shift
            if (pct <= 0.3f)
                _fillMat.AlbedoColor = ColorLow;
            else if (pct <= 0.6f)
                _fillMat.AlbedoColor = ColorMid;
            else
                _fillMat.AlbedoColor = ColorHigh;

            // Hide when dead
            if (pct <= 0f)
                Visible = false;
        }

        private static MeshInstance3D CreateQuad(float width, float height, Color color, float zOffset)
        {
            var mesh = new MeshInstance3D();
            var quad = new QuadMesh();
            quad.Size = new Vector2(width, height);
            mesh.Mesh = quad;
            mesh.MaterialOverride = CreateBillboardMaterial(color);
            mesh.Position = new Vector3(0, 0, zOffset);
            return mesh;
        }

        private static StandardMaterial3D CreateBillboardMaterial(Color color)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.NoDepthTest = true;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.RenderPriority = 10;
            return mat;
        }
    }
}
