using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A Slag Chute's pour: a glowing pool on the canyon floor that burns and slows whatever walks
    /// through it until it cools. A decal, so it lies on the ground under it.
    /// </summary>
    public partial class SlagPool : Node3D
    {
        private float _delay, _age, _tick, _dps, _slow, _radius, _life;
        private VineNode _from;
        private Decal _decal;
        private OmniLight3D _glow;
        private static ImageTexture _texture;

        /// <summary>Pools on the field now (tests).</summary>
        public static int Live { get; private set; }

        public static SlagPool Spawn(SceneTree tree, Vector3 at, float delay, VineNode from, float dps, float slow, float radius, float seconds)
        {
            var scene = tree?.CurrentScene;
            if (scene == null) return null;
            var pool = new SlagPool
            {
                Name = "SlagPool", _delay = Mathf.Max(0f, delay), Visible = delay <= 0f, _from = from,
                _dps = dps, _slow = slow, _radius = radius, _life = seconds,
            };
            scene.AddChild(pool);
            float y = at.Y;
            if (ServiceLocator.TryGet<VineGrid>(out var grid)) y = grid.GetWorldHeight(at.X, at.Z);
            pool.GlobalPosition = new Vector3(at.X, y, at.Z);
            pool.RotationDegrees = new Vector3(0, GD.Randf() * 360f, 0);
            return pool;
        }

        public override void _EnterTree() => Live++;
        public override void _ExitTree() => Live--;

        public override void _Ready()
        {
            _decal = new Decal
            {
                Size = new Vector3(_radius * 2f, 1.6f, _radius * 2f),
                TextureAlbedo = Texture(),
                TextureEmission = Texture(),
                EmissionEnergy = 2.2f,
                Modulate = new Color(1f, 1f, 1f, 0f),
                UpperFade = 0.3f,
                LowerFade = 0.3f,
                CullMask = 1,
            };
            AddChild(_decal);
            _glow = new OmniLight3D { LightColor = ClassTowers.SlagColor, LightEnergy = 0f, OmniRange = _radius * 1.8f, ShadowEnabled = false, Position = Vector3.Up * 0.5f };
            AddChild(_glow);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (_delay > 0f)
            {
                _delay -= dt;
                if (_delay > 0f) return;
                Visible = true;
                VfxFactory.SpawnEmbers(GetTree(), GlobalPosition, _radius * 0.7f);
            }
            _age += dt;
            if (_age >= _life) { QueueFree(); return; }
            // Splash in, cool over the last second and a half
            float a = Mathf.Min(1f, _age / 0.15f) * Mathf.Clamp((_life - _age) / 1.5f, 0f, 1f);
            _decal.Modulate = new Color(1f, 1f, 1f, a);
            _decal.EmissionEnergy = 2.2f * a;
            _glow.LightEnergy = 1.4f * a;

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = 0.25f;
            float r2 = _radius * _radius;
            VineEnemy.HitSource = "your towers";
            VineEnemy.HitTower = IsInstanceValid(_from) ? _from : null;
            try
            {
                foreach (var e in Roster.Enemies(GetTree()))
                {
                    if (!IsInstanceValid(e) || !e.IsAlive || e.IsFlying) continue;
                    var d = e.GlobalPosition - GlobalPosition;
                    if (d.X * d.X + d.Z * d.Z > r2) continue;
                    e.TakeDamage(_dps * 0.25f, DamageKind.Normal);
                    if (_slow > 0f) e.ApplySlow(_slow, 0.4f);
                }
            }
            finally { VineEnemy.HitSource = null; VineEnemy.HitTower = null; }
        }

        /// <summary>A molten splash: white-hot middle, orange, a dark crusted rim. Made once.</summary>
        private static ImageTexture Texture()
        {
            if (_texture != null) return _texture;
            const int size = 128;
            var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            var noise = new FastNoiseLite { Frequency = 0.07f, Seed = 11 };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float ang = Mathf.Atan2(v, u);
                float edge = 0.8f + 0.12f * noise.GetNoise2D(Mathf.Cos(ang) * 40f, Mathf.Sin(ang) * 40f);
                float d = Mathf.Sqrt(u * u + v * v) / edge;
                float alpha = Mathf.Clamp((1f - d) * 6f, 0f, 1f) * 0.95f;
                float heat = Mathf.Clamp(1f - d * 1.2f + 0.25f * noise.GetNoise2D(x * 2f, y * 2f), 0f, 1f);
                var c = new Color(0.25f + 0.75f * heat, 0.05f + 0.55f * heat * heat, 0.01f + 0.2f * heat * heat * heat, alpha);
                img.SetPixel(x, y, c);
            }
            _texture = ImageTexture.CreateFromImage(img);
            return _texture;
        }
    }
}
