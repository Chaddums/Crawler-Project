using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Tar Pools perk: a pool of tar where a Tar Sprayer gob lands. A decal, so it lies on
    /// whatever ground is under it; slows every enemy standing in it until it dries up.
    /// </summary>
    public partial class TarPool : Node3D
    {
        private float _delay;      // until the gob lands
        private float _age;
        private float _tick;
        private Decal _decal;

        private static ImageTexture _texture;

        /// <summary>Lay a pool at <paramref name="at"/> once the gob gets there.</summary>
        public static TarPool Spawn(SceneTree tree, Vector3 at, float delay)
        {
            var scene = tree?.CurrentScene;
            if (scene == null) return null;
            var pool = new TarPool { Name = "TarPool", _delay = Mathf.Max(0f, delay), Visible = delay <= 0f };
            scene.AddChild(pool);
            float y = at.Y;
            if (ServiceLocator.TryGet<VineGrid>(out var grid)) y = grid.GetWorldHeight(at.X, at.Z);
            pool.GlobalPosition = new Vector3(at.X, y, at.Z);
            pool.RotationDegrees = new Vector3(0, GD.Randf() * 360f, 0);
            return pool;
        }

        public override void _Ready()
        {
            float r = Constants.PERK_TAR_POOL_RADIUS;
            _decal = new Decal
            {
                Size = new Vector3(r * 2f, 1.6f, r * 2f),
                TextureAlbedo = Texture(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                UpperFade = 0.3f,
                LowerFade = 0.3f,
                CullMask = 1,
            };
            AddChild(_decal);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (_delay > 0f)
            {
                _delay -= dt;
                if (_delay > 0f) return;
                Visible = true;
            }
            _age += dt;
            float life = Constants.PERK_TAR_POOL_DURATION;
            if (_age >= life) { QueueFree(); return; }

            // Splat in, dry out over the last second
            float a = Mathf.Min(1f, _age / 0.15f) * Mathf.Clamp((life - _age) / 1f, 0f, 1f);
            float grow = 0.6f + 0.4f * Mathf.Min(1f, _age / 0.2f);
            _decal.Modulate = new Color(1f, 1f, 1f, a);
            _decal.Scale = new Vector3(grow, 1f, grow);

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = 0.2f;
            float r2 = Constants.PERK_TAR_POOL_RADIUS * Constants.PERK_TAR_POOL_RADIUS;
            foreach (var n in GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY))
            {
                if (n is not VineEnemy e || !e.IsAlive) continue;
                var d = e.GlobalPosition - GlobalPosition;
                if (d.X * d.X + d.Z * d.Z <= r2)
                    e.ApplySlow(Constants.PERK_TAR_POOL_SLOW, 0.35f);
            }
        }

        /// <summary>A glossy blob of tar with a lumpy edge, made once.</summary>
        private static ImageTexture Texture()
        {
            if (_texture != null) return _texture;
            const int size = 128;
            var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            var noise = new FastNoiseLite { Frequency = 0.06f, Seed = 7 };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float ang = Mathf.Atan2(v, u);
                float edge = 0.78f + 0.14f * noise.GetNoise2D(Mathf.Cos(ang) * 40f, Mathf.Sin(ang) * 40f);
                float d = Mathf.Sqrt(u * u + v * v) / edge;
                float alpha = Mathf.Clamp((1f - d) * 6f, 0f, 1f) * 0.92f;
                // Near-black with a purple sheen toward the middle and a lighter rim
                float sheen = Mathf.Clamp(1f - d * 1.4f, 0f, 1f);
                float rim = Mathf.Clamp(1f - Mathf.Abs(d - 0.9f) * 12f, 0f, 1f);
                var c = new Color(0.03f + 0.06f * sheen + 0.05f * rim, 0.025f + 0.03f * sheen + 0.04f * rim,
                                  0.045f + 0.1f * sheen + 0.06f * rim, alpha);
                img.SetPixel(x, y, c);
            }
            _texture = ImageTexture.CreateFromImage(img);
            return _texture;
        }
    }
}
