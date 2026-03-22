using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Floating damage number that rises and fades.
    /// Crits are larger and colored differently.
    /// </summary>
    public partial class DamageNumber : Node3D
    {
        private Label3D _label;
        private float _lifetime = 0.8f;
        private float _maxLifetime = 0.8f;
        private Vector3 _velocity;

        private static readonly RandomNumberGenerator _rng = new();

        public static void Spawn(SceneTree tree, Vector3 position, float damage, DamageType type, bool isCrit = false)
        {
            var dn = new DamageNumber();
            tree.CurrentScene.AddChild(dn);
            dn.GlobalPosition = position + new Vector3(
                _rng.RandfRange(-0.3f, 0.3f),
                0.5f,
                _rng.RandfRange(-0.3f, 0.3f)
            );
            dn.Initialize(damage, type, isCrit);
        }

        private void Initialize(float damage, DamageType type, bool isCrit)
        {
            _label = new Label3D();
            _label.Text = Mathf.RoundToInt(damage).ToString();
            _label.FontSize = isCrit ? 48 : 32;
            _label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _label.NoDepthTest = true;
            _label.FixedSize = true;
            _label.PixelSize = 0.005f;

            _label.Modulate = type switch
            {
                DamageType.Fire => isCrit ? new Color(1f, 0.3f, 0f) : new Color(1f, 0.6f, 0.2f),
                DamageType.Ice => new Color(0.4f, 0.8f, 1f),
                DamageType.Lightning => new Color(0.7f, 0.7f, 1f),
                DamageType.Poison => new Color(0.4f, 0.9f, 0.2f),
                _ => isCrit ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 1f, 1f)
            };

            if (isCrit)
                _label.Text += "!";

            _label.OutlineSize = 8;
            _label.OutlineModulate = new Color(0, 0, 0, 0.8f);

            AddChild(_label);

            _velocity = new Vector3(
                _rng.RandfRange(-0.5f, 0.5f),
                2.5f,
                _rng.RandfRange(-0.5f, 0.5f)
            );
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _lifetime -= dt;

            _velocity.Y -= 3f * dt; // Gentle gravity
            GlobalPosition += _velocity * dt;

            float t = _lifetime / _maxLifetime;
            _label.Modulate = new Color(
                _label.Modulate.R,
                _label.Modulate.G,
                _label.Modulate.B,
                t
            );

            if (_lifetime <= 0)
                QueueFree();
        }
    }
}
