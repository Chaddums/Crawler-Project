using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A loot crate in the field. It is tossed out of the enemy that dropped it (or falls from
    /// the sky as a supply drop), then floats, turns and glows in its tier's colour, with a
    /// light pillar over Gold and up so it can be seen across the map. BIT walking within
    /// <see cref="LootFile.PickupRadius"/> takes it: it flies into BIT and goes to
    /// <see cref="LootManager"/> to be opened between waves. The look is built from its tier's
    /// Data/Towers sheet (kit crate plus lit parts); the idle motion is ported from the dungeon
    /// game's LootBoxPresenter.
    /// </summary>
    public partial class LootCrate : Node3D
    {
        public const string GROUP = "LootCrate";
        /// <summary>Crates are drawn larger in the field than in the opening, so they read at play distance.</summary>
        public const float FieldScale = 1.35f;

        public LootTier Tier { get; private set; }
        /// <summary>BIT has it (flying in or gone).</summary>
        public bool Taken { get; private set; }
        /// <summary>Landed and waiting to be picked up.</summary>
        public bool Waiting => _state == State.Idle;
        public Node3D Look => _look;
        internal MeshInstance3D Pillar => _pillar;

        private enum State { Arriving, Idle, Flying }
        private State _state = State.Arriving;
        private Node3D _look, _hover;
        private Label3D _label;
        private MeshInstance3D _pillar;
        private OmniLight3D _light;
        private Vector3 _from, _to;
        private float _t, _dur, _arc, _clock, _fxTimer, _spinRate, _bob, _wobble;
        private bool _sky;
        private float _lightEnergy;

        /// <summary>
        /// Put a crate in the field: tossed from <paramref name="from"/> to land at
        /// <paramref name="to"/>, or dropped from high above it when <paramref name="fromSky"/>.
        /// </summary>
        public static LootCrate Spawn(Node parent, LootTier tier, Vector3 from, Vector3 to, bool fromSky)
        {
            var crate = new LootCrate { Name = $"LootCrate_{tier.Id}", Tier = tier, _sky = fromSky };
            parent.AddChild(crate);
            crate._to = to;
            crate._from = fromSky ? to + Vector3.Up * 26f : from + Vector3.Up * 1.2f;
            crate._dur = fromSky ? 1.1f : 0.75f;
            crate._arc = fromSky ? 0f : 2.2f;
            crate.GlobalPosition = crate._from;
            return crate;
        }

        public override void _Ready()
        {
            AddToGroup(GROUP);
            int rank = Tier.Rank;
            // How lively it is by tier (the dungeon presenter's numbers)
            _spinRate = Mathf.Tau / (6f - rank * 0.8f);
            _bob = 0.15f + rank * 0.04f;
            _wobble = 0.03f + rank * 0.012f;

            _hover = new Node3D { Name = "Hover" };
            AddChild(_hover);
            var sheet = TowerSheet.Load(Tier.Sheet);
            _look = sheet != null ? TowerLook.Build(sheet, Tier.Tint, 0f) : new Node3D();
            _look.Name = "CrateModel";
            _look.Position = new Vector3(0, 0.25f, 0);
            _look.Scale = Vector3.One * FieldScale;
            _hover.AddChild(_look);
            // A rim in its own colour: the kit crates are dark metal and vanished on Grid Prime's floor
            BitPalette.ApplyAccentRim(_look, Tier.Tint, 1.2f + rank * 0.2f);
            foreach (var mi in _look.FindChildren("*", "MeshInstance3D", true, false))
                if (mi is GeometryInstance3D g) g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

            _label = new Label3D
            {
                Name = "TierLabel",
                // Say how to take it: a playtester saw a Gold Cache between two bosses and
                // couldn't tell what it was
                Text = $"{Tier.Name.ToUpper()}\nloot: walk BIT over it",
                FontSize = 40,
                PixelSize = 0.008f,
                OutlineSize = 10,
                Modulate = Tier.Tint.Lightened(0.25f),
                OutlineModulate = new Color(0, 0, 0, 0.9f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                Position = new Vector3(0, 2.0f, 0),
                Visible = false,
            };
            AddChild(_label);

            if (Tier.Pillar)
            {
                _pillar = new MeshInstance3D
                {
                    Name = "LightPillar",
                    Mesh = PillarMesh,
                    MaterialOverride = PillarMaterial(Tier),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(0, PillarHeight * 0.5f, 0),
                    Visible = false,
                };
                AddChild(_pillar);
                _lightEnergy = 0.8f + rank * 0.5f;
                _light = new OmniLight3D
                {
                    Name = "Glow",
                    LightColor = Tier.Tint,
                    LightEnergy = _lightEnergy,
                    OmniRange = 2.5f + rank * 0.4f,
                    ShadowEnabled = false,
                    Position = new Vector3(0, 0.6f, 0),
                };
                AddChild(_light);
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _clock += dt;
            switch (_state)
            {
                case State.Arriving: Arrive(dt); break;
                case State.Idle: Idle(dt); break;
                case State.Flying: Fly(dt); break;
            }
        }

        private void Arrive(float dt)
        {
            _t = Mathf.Min(1f, _t + dt / _dur);
            float k = _sky ? _t * _t : _t;
            var p = _from.Lerp(_to, k);
            p.Y += _arc * 4f * _t * (1f - _t);
            GlobalPosition = p;
            _hover.Rotation = new Vector3(0, _clock * 7f, _sky ? 0f : Mathf.Sin(_clock * 9f) * 0.4f);
            if (_sky && GetTree() != null && (_fxTimer -= dt) <= 0f)
            {
                _fxTimer = 0.06f;
                VfxFactory.SpawnBuffMotes(GetTree(), GlobalPosition + Vector3.Up * 0.4f, Tier.Tint);
            }
            if (_t < 1f) return;
            _state = State.Idle;
            _label.Visible = true;
            if (_pillar != null) _pillar.Visible = true;
            var tree = GetTree();
            VfxFactory.SpawnEnergyBurst(tree, GlobalPosition + Vector3.Up * 0.5f, Tier.Tint, 8 + Tier.Rank * 4);
            VfxFactory.SpawnAreaPulse(tree, GlobalPosition, 1.6f + Tier.Rank * 0.3f, Tier.Tint, 0.6f, 0.9f);
            if (_sky && ServiceLocator.TryGet<TDCamera>(out var cam)) cam.Shake(0.35f, 0.2f);
        }

        private void Idle(float dt)
        {
            // Float, turn and sway (LootBoxPresenter's motion, by tier)
            _hover.Position = new Vector3(0, _bob + Mathf.Sin(_clock * Mathf.Tau / (3f + Tier.Rank * 0.2f)) * _bob, 0);
            _hover.Rotation = new Vector3(0, _clock * _spinRate, Mathf.Sin(_clock * 2.1f) * _wobble);
            if (_light != null) _light.LightEnergy = _lightEnergy * (0.7f + 0.3f * Mathf.Sin(_clock * 3f));
            if (Tier.Rank >= 2 && (_fxTimer -= dt) <= 0f)
            {
                _fxTimer = 0.55f - Tier.Rank * 0.06f;
                var tree = GetTree();
                VfxFactory.SpawnBuffMotes(tree, GlobalPosition + Vector3.Up * 0.7f, Tier.Tint);
                if (Mathf.PosMod(_clock, 1.8f) < _fxTimer) VfxFactory.SpawnAreaPulse(tree, GlobalPosition, 1.1f, Tier.Tint, 0.9f, 0.5f);
            }
            // BIT takes it by walking over it
            if (ServiceLocator.TryGet<VinePlayer>(out var bit) && IsInstanceValid(bit) && bit.IsAlive && !bit.IsDocked && bit.Visible)
            {
                var d = bit.GlobalPosition - GlobalPosition;
                d.Y = 0f;
                if (d.LengthSquared() <= LootTable.File.PickupRadius * LootTable.File.PickupRadius) Take();
            }
        }

        /// <summary>BIT takes the crate: it flies into BIT and goes to the loot manager.</summary>
        public void Take()
        {
            if (Taken) return;
            Taken = true;
            _state = State.Flying;
            _t = 0f;
            _from = GlobalPosition;
            _label.Visible = false;
            if (_pillar != null) _pillar.Visible = false;
            if (ServiceLocator.TryGet<LootManager>(out var loot)) loot.Collect(this);
        }

        private void Fly(float dt)
        {
            _t = Mathf.Min(1f, _t + dt / 0.35f);
            var to = ServiceLocator.TryGet<VinePlayer>(out var bit) && IsInstanceValid(bit) ? bit.GlobalPosition + Vector3.Up * 1.0f : _from;
            var p = _from.Lerp(to, _t * _t);
            p.Y += 1.2f * 4f * _t * (1f - _t);
            GlobalPosition = p;
            _hover.Scale = Vector3.One * Mathf.Max(0.05f, 1f - _t * 0.9f);
            _hover.Rotation = new Vector3(0, _clock * 14f, 0);
            if (_t < 1f) return;
            var tree = GetTree();
            VfxFactory.SpawnEnergyBurst(tree, GlobalPosition, Tier.Tint, 10 + Tier.Rank * 3);
            DamageNumbers.Tag(GlobalPosition + Vector3.Up * 1.2f, $"+{Tier.Name.ToUpper()}", Tier.Tint.Lightened(0.3f));
            QueueFree();
        }

        // ── The light pillar (shared mesh, a material per tier) ──

        private const float PillarHeight = 16f;
        private static Mesh _pillarMesh;
        private static Shader _pillarShader;
        private static readonly Dictionary<string, ShaderMaterial> _pillarMats = new();

        private static Mesh PillarMesh => _pillarMesh ??= new CylinderMesh
        {
            TopRadius = 0.22f, BottomRadius = 0.42f, Height = PillarHeight, RadialSegments = 16, Rings = 1, CapTop = false, CapBottom = false,
        };

        private static ShaderMaterial PillarMaterial(LootTier tier)
        {
            if (_pillarMats.TryGetValue(tier.Id, out var m)) return m;
            _pillarShader ??= new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 tint : source_color = vec4(1.0);
uniform float strength = 0.6;
uniform float height = 16.0;
varying float local_y;
void vertex() { local_y = VERTEX.y; }
void fragment() {
    float up = clamp(local_y / height + 0.5, 0.0, 1.0);
    float fade = pow(1.0 - up, 1.8);
    float core = pow(abs(dot(NORMAL, VIEW)), 1.4);
    float pulse = 0.8 + 0.2 * sin(TIME * 3.0 - up * 12.0);
    ALBEDO = tint.rgb;
    ALPHA = clamp(fade * core * strength * pulse, 0.0, 1.0);
}",
            };
            m = new ShaderMaterial { Shader = _pillarShader };
            m.SetShaderParameter("tint", tier.Tint);
            m.SetShaderParameter("strength", 0.45f + tier.Rank * 0.1f);
            m.SetShaderParameter("height", PillarHeight);
            _pillarMats[tier.Id] = m;
            return m;
        }
    }
}
