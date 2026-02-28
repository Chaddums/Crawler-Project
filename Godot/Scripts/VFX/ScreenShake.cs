using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Trauma-based screen shake. Attach as child of camera.
    /// Call AddTrauma() to trigger shake; offset decays via trauma^2 * noise.
    /// </summary>
    public partial class ScreenShake : Node
    {
        private float _trauma;
        private FastNoiseLite _noise;
        private float _noiseY;

        public Vector3 Offset { get; private set; }

        private const float MAX_OFFSET_X = 0.4f;
        private const float MAX_OFFSET_Y = 0.3f;
        private const float DECAY_RATE = 2.5f;

        public override void _Ready()
        {
            _noise = new FastNoiseLite();
            _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            _noise.Frequency = 3f;
        }

        public override void _Process(double delta)
        {
            if (_trauma <= 0)
            {
                Offset = Vector3.Zero;
                return;
            }

            _trauma = Mathf.Max(0, _trauma - (float)delta * DECAY_RATE);
            float shake = _trauma * _trauma;

            _noiseY += (float)delta * 60f;
            float offsetX = MAX_OFFSET_X * shake * _noise.GetNoise2D(0, _noiseY);
            float offsetY = MAX_OFFSET_Y * shake * _noise.GetNoise2D(100, _noiseY);

            Offset = new Vector3(offsetX, offsetY, 0);
        }

        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Min(1f, _trauma + amount);
        }
    }
}
