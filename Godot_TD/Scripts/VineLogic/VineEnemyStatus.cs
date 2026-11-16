using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What relics do to enemies: chill and freeze (Cryo Fists: three chills freeze an enemy solid
    /// in a block of ice, and a frozen enemy that dies shatters into shards that hit everything
    /// round it), burning (Molten Rounds: damage over time, and a burning enemy that dies spreads
    /// the fire) and crushing (Gravity Rams: takes more from every hit for a while).
    /// </summary>
    public partial class VineEnemy
    {
        // Chill and freeze
        private int _chillStacks;
        private float _chillTimer, _frozenTimer;
        private float _shatterHit, _shatterShare, _shatterRadius;
        private MeshInstance3D _iceBlock;
        // Burning
        private float _burnDps, _burnTimer, _burnTick, _emberTimer;
        private float _burnSpread;
        // Crushed
        private float _crushTimer, _crushTaken = 1f, _crushMoteTimer;
        // Corroded (Chaos power type)
        private int _corrodeStacks;
        private float _corrodeTimer, _corrodeMoteTimer;
        // Hits that shouldn't flash the model (burn ticks)
        private bool _quietHit;

        public int ChillStacks => _chillTimer > 0f ? _chillStacks : 0;
        public bool IsFrozen => _frozenTimer > 0f;
        /// <summary>What its hits are worth now: frozen, nothing; each chill takes a share off.</summary>
        public float StatusDamageMult => IsFrozen ? 0f : Mathf.Max(0.3f, 1f - Constants.CHILL_DAMAGE_CUT * ChillStacks);
        public bool IsBurning => _burnTimer > 0f;
        public bool IsCrushed => _crushTimer > 0f;
        /// <summary>Corrode stacks on it now (Chaos power type).</summary>
        public int CorrodeStacks => _corrodeTimer > 0f ? _corrodeStacks : 0;
        /// <summary>Each corrode stack makes every hit land a share harder.</summary>
        public float CorrodeTakenMult => _corrodeTimer > 0f ? 1f + _corrodeStacks * PowerType.CorrodePerStack : 1f;
        public static readonly Color CorrodeColor = new(0.62f, 1f, 0.25f);

        /// <summary>
        /// One corrode stack (Chaos: every tower hit), or <paramref name="stacks"/> at once, up to
        /// the tier's cap; each refreshes the timer. Nothing happens without the Chaos type.
        /// </summary>
        public void Corrode(int stacks = 1)
        {
            int cap = PowerType.CorrodeStacks;
            if (!IsAlive || cap <= 0 || stacks <= 0) return;
            _corrodeStacks = Mathf.Min(cap, (_corrodeTimer > 0f ? _corrodeStacks : 0) + stacks);
            _corrodeTimer = PowerType.CorrodeSeconds;
        }
        internal MeshInstance3D IceBlock => _iceBlock;

        /// <summary>What every hit is multiplied by from statuses: crushed (relic), corroded (Chaos power), berserk (AXIS chaos).</summary>
        public float StatusTakenMult => (_crushTimer > 0f ? _crushTaken : 1f) * CorrodeTakenMult * ChaosTakenMult * MarkTakenMult;

        /// <summary>AXIS chaos berserk: takes this share of every hit (CorruptionManager.CHAOS_TAKEN).</summary>
        public float ChaosTakenMult { get; set; } = 1f;

        public static readonly Color IceColor = new(0.55f, 0.9f, 1f);
        public static readonly Color FireColor = new(1f, 0.45f, 0.12f);
        public static readonly Color CrushColor = new(0.78f, 0.45f, 1f);

        /// <summary>
        /// One chill (Cryo Fists): slows a little more each time; at <paramref name="freezeAt"/> it
        /// freezes solid for <paramref name="freezeTime"/> (bosses and commanders much less). A
        /// frozen enemy that dies shatters for the smaller of <paramref name="shatterShare"/> of its
        /// health and <paramref name="shatterHit"/> on everything within <paramref name="shatterRadius"/>.
        /// </summary>
        public void Chill(float slowPerStack, float chillTime, int freezeAt, float freezeTime,
            float shatterHit, float shatterShare, float shatterRadius)
        {
            if (!IsAlive) return;
            _shatterHit = Mathf.Max(_shatterHit, shatterHit);
            _shatterShare = shatterShare;
            _shatterRadius = shatterRadius;
            if (IsFrozen) { _frozenTimer = Mathf.Max(_frozenTimer, 0.3f); return; }
            _chillStacks = _chillTimer > 0f ? _chillStacks + 1 : 1;
            _chillTimer = chillTime;
            ApplySlow(Mathf.Min(0.75f, slowPerStack * _chillStacks), chillTime);
            if (_chillStacks < Mathf.Max(1, freezeAt)) return;
            // Frozen solid
            _chillStacks = 0;
            _chillTimer = 0f;
            float t = freezeTime * (IsBoss || IsCommander ? Constants.STUN_BOSS_FACTOR : 1f);
            _frozenTimer = t;
            ApplyStun(freezeTime);
            ShowIce(true);
            if (IsInsideTree())
            {
                VfxFactory.SpawnFrostBurst(GetTree(), BodyCentre, Mathf.Max(0.6f, _bodyRadius * 2f));
            }
        }

        /// <summary>Set it burning (Molten Rounds): <paramref name="dps"/> for <paramref name="seconds"/>; the strongest burn holds.</summary>
        public void Ignite(float dps, float seconds, float spreadRadius)
        {
            if (!IsAlive || dps <= 0f) return;
            _burnDps = _burnTimer > 0f ? Mathf.Max(_burnDps, dps) : dps;
            _burnTimer = Mathf.Max(_burnTimer, seconds);
            _burnSpread = spreadRadius;
        }

        /// <summary>Crushed (Gravity Rams): every hit lands <paramref name="taken"/> times over for <paramref name="seconds"/>.</summary>
        public void Crush(float taken, float seconds)
        {
            if (!IsAlive) return;
            _crushTaken = Mathf.Max(_crushTimer > 0f ? _crushTaken : 1f, taken);
            _crushTimer = Mathf.Max(_crushTimer, seconds);
        }

        private void TickStatus(float dt)
        {
            TickMarks(dt);
            if (_chillTimer > 0f && (_chillTimer -= dt) <= 0f) _chillStacks = 0;
            if (_frozenTimer > 0f && (_frozenTimer -= dt) <= 0f) ShowIce(false);
            if (_iceBlock != null && IsInstanceValid(_iceBlock))
            {
                // A little shiver while it holds, a crack as it lets go
                float k = Mathf.Clamp(_frozenTimer / 0.25f, 0f, 1f);
                _iceBlock.Scale = new Vector3(1f, 0.85f + 0.15f * k, 1f);
            }
            if (_burnTimer > 0f)
            {
                _burnTimer -= dt;
                _burnTick += dt;
                if (_burnTick >= 0.25f)
                {
                    float hit = _burnDps * _burnTick;
                    _burnTick = 0f;
                    string was = HitSource;
                    HitSource ??= "your towers";
                    _quietHit = true;
                    try { TakeDamage(hit, DamageKind.Normal); }
                    finally { _quietHit = false; HitSource = was; }
                }
                if ((_emberTimer -= dt) <= 0f && IsInsideTree())
                {
                    _emberTimer = 0.12f;
                    VfxFactory.SpawnEmbers(GetTree(), BodyCentre, _bodyRadius);
                }
                if (_burnTimer <= 0f) { _burnDps = 0f; _burnTick = 0f; }
            }
            if (_corrodeTimer > 0f)
            {
                _corrodeTimer -= dt;
                // Green motes, more of them the deeper it is corroded
                if (_corrodeStacks >= 2 && (_corrodeMoteTimer -= dt) <= 0f && IsInsideTree())
                {
                    _corrodeMoteTimer = Mathf.Max(0.18f, 0.7f - 0.08f * _corrodeStacks);
                    VfxFactory.SpawnBuffMotes(GetTree(), GlobalPosition, CorrodeColor);
                }
                if (_corrodeTimer <= 0f) _corrodeStacks = 0;
            }
            if (_crushTimer > 0f)
            {
                _crushTimer -= dt;
                if ((_crushMoteTimer -= dt) <= 0f && IsInsideTree())
                {
                    _crushMoteTimer = 0.3f;
                    VfxFactory.SpawnBuffMotes(GetTree(), GlobalPosition, CrushColor);
                }
                if (_crushTimer <= 0f) _crushTaken = 1f;
            }
        }

        /// <summary>It died: a frozen one shatters, a burning one spreads the fire.</summary>
        private void OnStatusDeath()
        {
            if (!IsInsideTree()) return;
            var tree = GetTree();
            var at = GlobalPosition;
            if (IsFrozen || _iceBlock != null)
            {
                float hit = Mathf.Min(MaxHealth * _shatterShare, _shatterHit);
                ShowIce(false);
                _frozenTimer = 0f;
                VfxFactory.SpawnIceShatter(tree, BodyCentre, _shatterRadius);
                ShatterCount++;
                if (hit > 0f)
                    foreach (var e in Roster.Enemies(tree).ToList())
                    {
                        if (e == this || !IsInstanceValid(e) || !e.IsAlive) continue;
                        var d = e.GlobalPosition - at; d.Y = 0;
                        if (d.Length() > _shatterRadius) continue;
                        e.TakeDamage(hit * (1f - 0.4f * d.Length() / Mathf.Max(0.1f, _shatterRadius)), DamageKind.Heavy);
                    }
            }
            if (_burnTimer > 0f && _burnSpread > 0f)
            {
                float dps = _burnDps;
                foreach (var e in Roster.Enemies(tree).ToList())
                {
                    if (e == this || !IsInstanceValid(e) || !e.IsAlive) continue;
                    var d = e.GlobalPosition - at; d.Y = 0;
                    if (d.Length() > _burnSpread) continue;
                    e.Ignite(dps, Mathf.Max(1.5f, _burnTimer + 1f), _burnSpread);
                    VfxFactory.SpawnTracer(tree, BodyCentre, e.BodyCentre, FireColor, 18f);
                }
                VfxFactory.SpawnExplosion(tree, at + Vector3.Up * 0.3f, Mathf.Min(_burnSpread * 0.6f, 1.6f), FireColor);
                _burnTimer = 0f;
            }
        }

        /// <summary>Shatters so far this run (tests).</summary>
        public static int ShatterCount;

        // ── The block of ice ──

        private static ShaderMaterial _iceMat;

        private void ShowIce(bool on)
        {
            if (!on)
            {
                if (_iceBlock != null && IsInstanceValid(_iceBlock))
                {
                    if (IsInsideTree()) VfxFactory.SpawnFrostBurst(GetTree(), BodyCentre, Mathf.Max(0.5f, _bodyRadius * 1.6f));
                    _iceBlock.QueueFree();
                }
                _iceBlock = null;
                return;
            }
            if (_iceBlock != null && IsInstanceValid(_iceBlock)) return;
            _iceMat ??= new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque, specular_schlick_ggx;
uniform vec4 tint : source_color = vec4(0.55, 0.9, 1.0, 1.0);
void fragment() {
    float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
    ALBEDO = mix(tint.rgb * 0.55, vec3(0.92, 0.98, 1.0), fres);
    EMISSION = tint.rgb * (0.25 + fres * 1.6);
    ROUGHNESS = 0.08;
    METALLIC = 0.0;
    SPECULAR = 0.9;
    ALPHA = 0.42 + fres * 0.45;
}",
                },
            };
            float w = Mathf.Clamp(_bodyRadius * 2.6f, 0.7f, 3.2f), h = Mathf.Clamp(_bodyHeight * 1.15f, 0.7f, 3.6f);
            float hover = Faction == VineEnemyFaction.Swarm ? Constants.SWARM_HOVER_HEIGHT : 0f;
            _iceBlock = new MeshInstance3D
            {
                Name = "IceBlock",
                // A cluster of crystals grown up round it. It was a plain box: from the game's
                // camera a frozen enemy read as an enemy with a box drawn round it
                Mesh = IceCrystals(w, h, (int)(GD.Randi() % IceVariants)),
                MaterialOverride = _iceMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, hover, 0),
                Rotation = new Vector3(0, (float)GD.RandRange(0, Mathf.Tau), 0),
            };
            AddChild(_iceBlock);
        }

        private const int IceVariants = 3;
        private static readonly System.Collections.Generic.Dictionary<(int, int, int), ArrayMesh> _iceMeshes = new();

        /// <summary>
        /// Ice round a body <paramref name="w"/> wide and <paramref name="h"/> tall, standing on
        /// the ground at its feet: one tall six-sided crystal over the body and five or six
        /// shorter ones leaning out round it, flat-faced so each facet catches the light.
        /// Built once for a size and a variant.
        /// </summary>
        internal static ArrayMesh IceCrystals(float w, float h, int variant)
        {
            var key = (Mathf.RoundToInt(w * 10f), Mathf.RoundToInt(h * 10f), variant);
            if (_iceMeshes.TryGetValue(key, out var cached)) return cached;
            var rng = new RandomNumberGenerator { Seed = (ulong)(key.Item1 * 7919 + key.Item2 * 104729 + variant * 31 + 17) };
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            // The one the enemy stands in
            Crystal(st, Vector3.Zero, w * 0.46f, h * 1.12f, 0f, 0f, rng.RandfRange(0f, 1f));
            int outer = 5 + variant % 2;
            for (int i = 0; i < outer; i++)
            {
                float a = Mathf.Tau * (i + rng.RandfRange(-0.25f, 0.25f)) / outer;
                float out_ = w * rng.RandfRange(0.3f, 0.42f);
                Crystal(st, new Vector3(Mathf.Cos(a) * out_, 0f, Mathf.Sin(a) * out_), w * rng.RandfRange(0.16f, 0.26f), h * rng.RandfRange(0.4f, 0.85f),
                    a, rng.RandfRange(0.18f, 0.42f), rng.RandfRange(0f, 1f));
            }
            st.GenerateNormals();
            var mesh = st.Commit();
            _iceMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>One six-sided crystal: a prism to three quarters of its height, then a point; leaning <paramref name="lean"/> radians away from the middle along <paramref name="heading"/>.</summary>
        private static void Crystal(SurfaceTool st, Vector3 foot, float radius, float height, float heading, float lean, float twist)
        {
            var tilt = new Basis(new Vector3(-Mathf.Sin(heading), 0f, Mathf.Cos(heading)), -lean);
            Vector3 P(float ang, float r, float y) => foot + tilt * new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
            float shoulder = height * 0.72f;
            var tip = foot + tilt * new Vector3(radius * 0.12f, height, 0f);
            for (int i = 0; i < 6; i++)
            {
                float a0 = twist + Mathf.Tau * i / 6f, a1 = twist + Mathf.Tau * (i + 1) / 6f;
                Vector3 b0 = P(a0, radius, -0.05f), b1 = P(a1, radius, -0.05f), s0 = P(a0, radius * 0.86f, shoulder), s1 = P(a1, radius * 0.86f, shoulder);
                // Unshared corners: every face flat (Godot's front faces wind clockwise)
                st.AddVertex(b0); st.AddVertex(s0); st.AddVertex(s1);
                st.AddVertex(b0); st.AddVertex(s1); st.AddVertex(b1);
                st.AddVertex(s0); st.AddVertex(tip); st.AddVertex(s1);
            }
        }
    }
}
