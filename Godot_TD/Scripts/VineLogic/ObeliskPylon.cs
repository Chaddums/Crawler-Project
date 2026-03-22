using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A buildable pylon sub-structure for the Obelisk spire.
    /// Has a small power radius that extends the build area.
    /// Can be infused with magic to buff towers in its radius.
    /// </summary>
    public partial class ObeliskPylon : Node3D
    {
        public float PowerRadius { get; private set; }
        public MaterialType? Infusion { get; private set; }

        private MeshInstance3D _radiusRing;
        private MeshInstance3D _column;
        private MeshInstance3D _orb;
        private StandardMaterial3D _orbMat;

        public void Initialize(float powerRadius)
        {
            PowerRadius = powerRadius;
            BuildVisual();
            AddToGroup("ObeliskPylons");
        }

        public void Infuse(MaterialType type)
        {
            Infusion = type;
            if (_orbMat != null)
            {
                var color = VineHarvester.GetMaterialColor(type);
                _orbMat.AlbedoColor = color;
                _orbMat.Emission = color;
                _orbMat.EmissionEnergyMultiplier = 1.5f;
            }
            GD.Print($"[Pylon] Infused with {type} at {GlobalPosition}");
        }

        public void SetRadiusVisible(bool visible)
        {
            if (_radiusRing != null)
                _radiusRing.Visible = visible;
        }

        private void BuildVisual()
        {
            // Column
            _column = new MeshInstance3D();
            _column.Mesh = new CylinderMesh
            {
                TopRadius = 0.12f, BottomRadius = 0.18f, Height = 2.5f, RadialSegments = 6
            };
            _column.Position = new Vector3(0, 1.25f, 0);
            var colMat = new StandardMaterial3D();
            colMat.AlbedoColor = new Color(0.08f, 0.08f, 0.1f);
            colMat.Roughness = 0.4f;
            colMat.Metallic = 0.7f;
            _column.MaterialOverride = colMat;
            AddChild(_column);

            // Orb on top
            _orb = new MeshInstance3D();
            _orb.Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f };
            _orb.Position = new Vector3(0, 2.6f, 0);
            _orbMat = new StandardMaterial3D();
            _orbMat.AlbedoColor = new Color(0.4f, 0.6f, 1.0f);
            _orbMat.EmissionEnabled = true;
            _orbMat.Emission = new Color(0.4f, 0.6f, 1.0f);
            _orbMat.EmissionEnergyMultiplier = 0.8f;
            _orb.MaterialOverride = _orbMat;
            AddChild(_orb);

            // Power radius ring
            _radiusRing = new MeshInstance3D();
            var torus = new TorusMesh();
            torus.InnerRadius = PowerRadius - 0.1f;
            torus.OuterRadius = PowerRadius + 0.1f;
            torus.Rings = 32;
            torus.RingSegments = 8;
            _radiusRing.Mesh = torus;
            _radiusRing.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
            _radiusRing.Position = new Vector3(0, 0.05f, 0);

            var ringMat = new StandardMaterial3D();
            ringMat.AlbedoColor = new Color(0.3f, 0.5f, 1.0f, 0.2f);
            ringMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            ringMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            ringMat.EmissionEnabled = true;
            ringMat.Emission = new Color(0.4f, 0.6f, 1.0f);
            ringMat.EmissionEnergyMultiplier = 0.2f;
            _radiusRing.MaterialOverride = ringMat;
            AddChild(_radiusRing);
        }
    }
}
