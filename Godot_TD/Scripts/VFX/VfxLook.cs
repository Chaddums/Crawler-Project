using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Meshes, shaders and generated textures for <see cref="VfxParticles"/>, made once and shared.
    /// Glows, sparks and bolts add light (camera-facing; sparks and bolts stretch along their
    /// direction); smoke blends in dark; rings lie flat on the ground; debris is lit metal.
    /// </summary>
    public static class VfxLook
    {
        private static QuadMesh _quad;
        private static PlaneMesh _plane;
        private static BoxMesh _box;
        private static Shader _billboardAdd, _billboardMix, _flatAdd;
        private static readonly Material[] _materials = new Material[6];
        private static ImageTexture _dot, _puff, _ring;

        public static Mesh MeshFor(VfxParticles.Kind kind) => kind switch
        {
            VfxParticles.Kind.Debris => _box ??= new BoxMesh { Size = new Vector3(1f, 0.55f, 0.8f) },
            VfxParticles.Kind.Ring => _plane ??= new PlaneMesh { Size = Vector2.One },
            _ => _quad ??= new QuadMesh { Size = Vector2.One },
        };

        public static Material MaterialFor(VfxParticles.Kind kind)
        {
            int k = (int)kind;
            if (_materials[k] != null) return _materials[k];
            Material m;
            switch (kind)
            {
                case VfxParticles.Kind.Smoke:
                    m = Shaded(_billboardMix ??= new Shader { Code = BillboardCode("blend_mix") }, Puff());
                    break;
                case VfxParticles.Kind.Ring:
                    m = Shaded(_flatAdd ??= new Shader { Code = FlatCode }, RingTex());
                    break;
                case VfxParticles.Kind.Debris:
                    m = new ShaderMaterial { Shader = new Shader { Code = DebrisCode } };
                    break;
                default:
                    m = Shaded(_billboardAdd ??= new Shader { Code = BillboardCode("blend_add") }, Dot());
                    break;
            }
            _materials[k] = m;
            return m;
        }

        private static ShaderMaterial Shaded(Shader shader, Texture2D tex)
        {
            var m = new ShaderMaterial { Shader = shader };
            m.SetShaderParameter("tex", tex);
            return m;
        }

        // Camera-facing quads. INSTANCE_CUSTOM.xyz is a streak direction and .w its length in
        // widths (0 = round). The vertex is built in world space and handed over in view space.
        private static string BillboardCode(string blend) => @"
shader_type spatial;
render_mode " + blend + @", unshaded, cull_disabled, depth_draw_never, shadows_disabled, skip_vertex_transform, fog_disabled;
uniform sampler2D tex : source_color, filter_linear_mipmap;
varying vec4 v_col;
void vertex() {
    v_col = COLOR;
    vec3 origin = MODEL_MATRIX[3].xyz;
    float size = length(MODEL_MATRIX[0].xyz);
    vec4 cu = INSTANCE_CUSTOM;
    vec3 world;
    if (cu.w > 0.0) {
        vec3 axis = normalize(cu.xyz);
        vec3 to_cam = normalize(CAMERA_POSITION_WORLD - origin);
        vec3 side = cross(axis, to_cam);
        float sl = length(side);
        side = sl > 0.001 ? side / sl : INV_VIEW_MATRIX[0].xyz;
        world = origin + side * VERTEX.x * size + axis * VERTEX.y * size * cu.w;
    } else {
        world = origin + (INV_VIEW_MATRIX[0].xyz * VERTEX.x + INV_VIEW_MATRIX[1].xyz * VERTEX.y) * size;
    }
    VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
    NORMAL = vec3(0.0, 0.0, 1.0);
}
void fragment() {
    vec4 t = texture(tex, UV);
" + (blend == "blend_add"
    ? "    ALBEDO = v_col.rgb * t.rgb * t.a * v_col.a;\n"
    : "    ALBEDO = v_col.rgb * t.rgb;\n    ALPHA = t.a * v_col.a;\n") + "}\n";

        // Flat quads on the ground (rings): ordinary transform, additive
        private const string FlatCode = @"
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
uniform sampler2D tex : source_color, filter_linear_mipmap;
varying vec4 v_col;
void vertex() { v_col = COLOR; }
void fragment() {
    vec4 t = texture(tex, UV);
    ALBEDO = v_col.rgb * t.rgb * t.a * v_col.a;
}
";

        // Lit chunks in their instance colour; INSTANCE_CUSTOM.x is how hot they still glow
        private const string DebrisCode = @"
shader_type spatial;
render_mode depth_prepass_alpha, cull_back;
varying vec4 v_col;
varying float v_heat;
void vertex() { v_col = COLOR; v_heat = INSTANCE_CUSTOM.x; }
void fragment() {
    ALBEDO = v_col.rgb;
    ALPHA = v_col.a;
    METALLIC = 0.7;
    ROUGHNESS = 0.45;
    EMISSION = vec3(1.0, 0.42, 0.1) * v_heat * 0.6;
}
";

        /// <summary>Soft round glow: a hot core and a long falloff.</summary>
        public static ImageTexture Dot()
        {
            if (_dot != null) return _dot;
            const int n = 64;
            var img = Image.CreateEmpty(n, n, true, Image.Format.Rgba8);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp(Mathf.Exp(-d * d * 5f) * 1.1f - 0.05f, 0f, 1f);
                float core = Mathf.Clamp(1f - d * 3f, 0f, 1f);
                img.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp(a + core * 0.5f, 0f, 1f)));
            }
            img.GenerateMipmaps();
            return _dot = ImageTexture.CreateFromImage(img);
        }

        /// <summary>A lumpy smoke puff.</summary>
        public static ImageTexture Puff()
        {
            if (_puff != null) return _puff;
            const int n = 64;
            var img = Image.CreateEmpty(n, n, true, Image.Format.Rgba8);
            var noise = new FastNoiseLite { Frequency = 0.09f, Seed = 3, FractalOctaves = 3 };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float body = Mathf.Clamp(1f - d, 0f, 1f);
                float lump = 0.6f + 0.6f * noise.GetNoise2D(x, y);
                float a = Mathf.Clamp(body * body * 1.6f * lump, 0f, 1f);
                float shade = 0.75f + 0.25f * noise.GetNoise2D(x * 1.7f + 40f, y * 1.7f);
                img.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
            img.GenerateMipmaps();
            return _puff = ImageTexture.CreateFromImage(img);
        }

        /// <summary>A thin bright ring with a soft inner glow, for shockwaves and pulses.</summary>
        public static ImageTexture RingTex()
        {
            if (_ring != null) return _ring;
            const int n = 128;
            var img = Image.CreateEmpty(n, n, true, Image.Format.Rgba8);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                // A crisp band with a faint wash inside (a wide soft band read as a glowing tyre
                // when a range pulse grew to a tower's full range)
                float band = Mathf.Exp(-Mathf.Pow((d - 0.93f) / 0.03f, 2f));
                float inner = d < 0.93f ? Mathf.Pow(d / 0.93f, 6f) * 0.12f : 0f;
                img.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp(band + inner, 0f, 1f)));
            }
            img.GenerateMipmaps();
            return _ring = ImageTexture.CreateFromImage(img);
        }
    }
}
