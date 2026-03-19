using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Simple Node3D that applies spin/bob/flicker/pulse animation via _Process().
    /// </summary>
    public partial class AnimatedPropController : Node3D
    {
        public string AnimType { get; set; } = "Spin";
        public float AnimSpeed { get; set; } = 45f;
        public float AnimAmplitude { get; set; } = 0.5f;

        private float _time;
        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private float _baseEmission = 1f;

        public override void _Ready()
        {
            _basePosition = Position;
            _baseScale = Scale;
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;

            switch (AnimType)
            {
                case "Spin":
                    RotationDegrees = new Vector3(
                        RotationDegrees.X,
                        RotationDegrees.Y + AnimSpeed * (float)delta,
                        RotationDegrees.Z);
                    break;

                case "Bob":
                    float bobY = Mathf.Sin(_time * AnimSpeed * Mathf.Tau) * AnimAmplitude;
                    Position = new Vector3(_basePosition.X, _basePosition.Y + bobY, _basePosition.Z);
                    break;

                case "Flicker":
                    // Random emission toggling
                    if (_time % 0.15f < (float)delta)
                    {
                        float flicker = GD.Randf() > 0.3f ? 1f : 0.1f;
                        ApplyEmission(flicker);
                    }
                    break;

                case "Pulse":
                    float pulse = 1f + Mathf.Sin(_time * AnimSpeed * Mathf.Tau) * AnimAmplitude * 0.2f;
                    Scale = _baseScale * pulse;
                    break;
            }
        }

        private void ApplyEmission(float multiplier)
        {
            foreach (var child in GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
                {
                    if (mat.EmissionEnabled)
                        mat.EmissionEnergyMultiplier = _baseEmission * multiplier;
                }
            }
        }
    }
}
