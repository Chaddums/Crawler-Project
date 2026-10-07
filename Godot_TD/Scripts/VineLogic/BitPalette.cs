using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// BIT/AXIS virus palette — the player's visual identity.
    /// CONSISTENT ACROSS ALL PLANETS. The probe is a foreign parasite;
    /// its infrastructure (harvester, towers, projectiles) must look alien
    /// to every environment it invades.
    ///
    /// Aesthetic: Clean white spaceship tech. Think orbital probe that just
    /// punched through atmosphere and deployed on hostile terrain. Whites,
    /// silvers, cool metallics. Faint cool-white glow on energy parts.
    /// NOT cyan (that's Tron planet), NOT amber (that's Scrapyard planet).
    ///
    /// BIT's true form is silver-white. Player nodes/harvester always use
    /// this palette regardless of planet. Only enemies and terrain adapt
    /// to the planet theme.
    /// </summary>
    public static class BitPalette
    {
        // ── Core Identity ──
        public static readonly Color DefaultAccent = new(0.9f, 0.93f, 1.0f);  // Cool white, BIT's signature
        /// <summary>Accent for player-owned things; each planet's look picks it (Grid Prime orange, Scrapyard cyan).</summary>
        public static Color Accent => PlanetTheme.Current?.PlayerAccent ?? DefaultAccent;
        public static readonly Color AccentBright = new(0.95f, 0.97f, 1.0f);  // Bright white for energy
        public static readonly Color AccentDim = new(0.5f, 0.52f, 0.58f);     // Dim silver
        public static readonly Color Body = new(0.82f, 0.84f, 0.88f);         // Silver-white hull plating
        public static readonly Color BodyDark = new(0.08f, 0.09f, 0.12f);     // Dark hull (outline body)

        // ── Node Category Tints (subtle variation within white/silver family) ──
        public static readonly Color SensorTint = new(0.7f, 0.82f, 0.95f);    // Ice-white (cool)
        public static readonly Color EffectTint = new(0.95f, 0.88f, 0.8f);    // Warm-white (hot)
        public static readonly Color RouteTint  = new(0.8f, 0.82f, 0.85f);    // Neutral silver

        // ── VFX ──
        public static readonly Color DirtColor = new(0.1f, 0.1f, 0.12f);      // Churned soil
        public static readonly Color CraterColor = new(0.05f, 0.05f, 0.07f);  // Ground disturbance

        // ── Outline Shader (cached) ──
        private static Shader _outlineShader;

        private static Shader GetOutlineShader()
        {
            if (_outlineShader != null) return _outlineShader;
            _outlineShader = new Shader();
            _outlineShader.Code = @"
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 outline_color : source_color = vec3(0.9, 0.93, 1.0);
uniform float outline_width : hint_range(0.0, 0.3) = 0.025;
void vertex() { VERTEX += NORMAL * outline_width; }
void fragment() { ALBEDO = outline_color; ALPHA = 0.85; }
";
            return _outlineShader;
        }

        /// <summary>
        /// Apply BIT virus theme to a 3D model node (imported FBX).
        /// Dark hull body + white emissive outline — clean spaceship tech look.
        /// </summary>
        public static void ApplyToNode(Node3D node, Color? tint = null)
        {
            Color accent = tint ?? Accent;
            ApplyBitRecursive(node, accent);
        }

        private static void ApplyBitRecursive(Node node, Color accent)
        {
            if (node is MeshInstance3D mesh)
            {
                // Dark hull body — unshaded so internal mesh surfaces disappear
                var bodyMat = new StandardMaterial3D();
                bodyMat.AlbedoColor = BodyDark;
                bodyMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

                // White outline as NextPass (inverted hull)
                float modelScale = mesh.GetParent() is Node3D parent ? parent.Scale.X : 1f;
                float outlineWidth = 0.06f / Mathf.Max(modelScale, 0.01f);

                var outlineMat = new ShaderMaterial();
                outlineMat.Shader = GetOutlineShader();
                outlineMat.SetShaderParameter("outline_color",
                    new Vector3(accent.R, accent.G, accent.B));
                outlineMat.SetShaderParameter("outline_width", Mathf.Clamp(outlineWidth, 0.01f, 0.3f));
                outlineMat.RenderPriority = -1;

                bodyMat.NextPass = outlineMat;
                mesh.MaterialOverride = bodyMat;
            }
            foreach (var child in node.GetChildren())
                ApplyBitRecursive(child, accent);
        }

        // ── Cached PBR textures ──
        private static Texture2D _hullAlbedo, _hullNormal, _hullRoughness, _hullMetallic;
        private static Texture2D _panelAlbedo, _panelNormal, _panelRoughness, _panelMetallic;
        private static bool _texturesLoaded;

        private static void EnsureTextures()
        {
            if (_texturesLoaded) return;
            _texturesLoaded = true;

            // Metal040 — grey hull plating (for dome floor, large surfaces)
            _hullAlbedo = GD.Load<Texture2D>("res://Materials/Hull/Metal040_2K-PNG_Color.png");
            _hullNormal = GD.Load<Texture2D>("res://Materials/Hull/Metal040_2K-PNG_NormalGL.png");
            _hullRoughness = GD.Load<Texture2D>("res://Materials/Hull/Metal040_2K-PNG_Roughness.png");
            _hullMetallic = GD.Load<Texture2D>("res://Materials/Hull/Metal040_2K-PNG_Metalness.png");

            // Metal055C — brushed steel (for structures, panels)
            _panelAlbedo = GD.Load<Texture2D>("res://Materials/Hull/Metal055C_2K-PNG_Color.png");
            _panelNormal = GD.Load<Texture2D>("res://Materials/Hull/Metal055C_2K-PNG_NormalGL.png");
            _panelRoughness = GD.Load<Texture2D>("res://Materials/Hull/Metal055C_2K-PNG_Roughness.png");
            _panelMetallic = GD.Load<Texture2D>("res://Materials/Hull/Metal055C_2K-PNG_Metalness.png");
        }

        /// <summary>
        /// Solid material for procedural BIT meshes (harvester body, struts, rings).
        /// Silver-white hull plating with subtle cool-white emission.
        /// </summary>
        public static StandardMaterial3D MakeSolidMaterial(float emissionStrength = 0.3f)
        {
            EnsureTextures();
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = Body;
            mat.Roughness = 0.25f;
            mat.Metallic = 0.75f;
            mat.EmissionEnabled = true;
            mat.Emission = Accent;
            mat.EmissionEnergyMultiplier = emissionStrength;

            // PBR hull textures — tinted silver-white
            if (_panelAlbedo != null)
            {
                mat.AlbedoTexture = _panelAlbedo;
                mat.AlbedoColor = new Color(0.9f, 0.92f, 0.95f); // Tint toward silver-white
            }
            if (_panelNormal != null)
            {
                mat.NormalEnabled = true;
                mat.NormalTexture = _panelNormal;
                mat.NormalScale = 0.6f; // Subtle surface detail
            }
            if (_panelRoughness != null)
                mat.RoughnessTexture = _panelRoughness;
            if (_panelMetallic != null)
            {
                mat.MetallicTexture = _panelMetallic;
                mat.MetallicTextureChannel = BaseMaterial3D.TextureChannel.Red;
            }

            return mat;
        }

        /// <summary>
        /// Hull plating material for large surfaces (dome floor, ground).
        /// Uses Metal040 grey plate texture with BIT tint overlay.
        /// </summary>
        public static StandardMaterial3D MakeHullMaterial(float emissionStrength = 0.08f)
        {
            EnsureTextures();
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.85f, 0.87f, 0.92f); // Cool silver
            mat.Roughness = 0.3f;
            mat.Metallic = 0.8f;
            mat.EmissionEnabled = true;
            mat.Emission = Accent;
            mat.EmissionEnergyMultiplier = emissionStrength;

            if (_hullAlbedo != null)
            {
                mat.AlbedoTexture = _hullAlbedo;
                mat.Uv1Scale = new Vector3(3f, 3f, 1f); // Tile for large surfaces
            }
            if (_hullNormal != null)
            {
                mat.NormalEnabled = true;
                mat.NormalTexture = _hullNormal;
                mat.NormalScale = 0.5f;
                mat.Uv1Scale = new Vector3(3f, 3f, 1f);
            }
            if (_hullRoughness != null)
                mat.RoughnessTexture = _hullRoughness;
            if (_hullMetallic != null)
            {
                mat.MetallicTexture = _hullMetallic;
                mat.MetallicTextureChannel = BaseMaterial3D.TextureChannel.Red;
            }

            return mat;
        }

        /// <summary>
        /// Unshaded emissive material for energy beams, glows, orbs.
        /// Clean white energy — spaceship extraction beam feel.
        /// </summary>
        public static StandardMaterial3D MakeGlowMaterial(float alpha = 1f, float emissionStrength = 0.6f)
        {
            var mat = new StandardMaterial3D();
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            if (alpha < 1f)
            {
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.AlbedoColor = new Color(AccentBright.R, AccentBright.G, AccentBright.B, alpha);
            }
            else
            {
                mat.AlbedoColor = AccentBright;
            }
            mat.EmissionEnabled = true;
            mat.Emission = AccentBright;
            mat.EmissionEnergyMultiplier = emissionStrength;
            return mat;
        }

        // ── Dome-aware ground shader ──
        // Blends planet terrain to BIT white inside the conversion dome radius.
        // Updated each frame by ConversionDome pushing dome_center / dome_radius uniforms.

        private static Shader _groundShader;

        private static Shader GetGroundShader()
        {
            if (_groundShader != null) return _groundShader;
            _groundShader = new Shader();
            _groundShader.Code = @"
shader_type spatial;

// Planet surface
uniform vec3 planet_color : source_color = vec3(0.02, 0.02, 0.04);
uniform sampler2D planet_texture : source_color, hint_default_white;
uniform bool use_texture = false;
uniform vec3 tex_scale = vec3(8.0, 8.0, 1.0);
// > 0: texture is laid out in world space (field size), so the outer ground continues it seamlessly
uniform vec2 tex_world_size = vec2(0.0, 0.0);
uniform sampler2D planet_normal : hint_normal;
uniform bool use_normal = false;
uniform float planet_roughness = 0.85;
uniform float planet_metallic = 0.3;

// BIT takeover surface
uniform vec3 bit_color : source_color = vec3(0.08, 0.08, 0.12);
uniform float bit_roughness = 0.25;
uniform float bit_metallic = 0.75;
uniform vec3 bit_emission : source_color = vec3(0.9, 0.93, 1.0);
uniform float bit_emission_strength = 0.12;

// Grid lines inside dome (Tron only — disabled on Scrapyard)
uniform bool show_grid = true;
uniform vec3 grid_color : source_color = vec3(0.7, 0.75, 0.85);
uniform float grid_spacing = 2.0;
uniform float grid_width = 0.04;
uniform float grid_emission_strength = 0.35;

// Dome boundary (updated each frame)
uniform vec3 dome_center = vec3(0.0, 0.0, 0.0);
uniform float dome_radius = 0.0;
uniform float dome_falloff = 2.5;

varying vec3 world_pos;
void vertex() { world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

void fragment() {
    // World position comes from vertex(); in fragment() VERTEX is view space, which made the
    // dome blend depend on the camera instead of the dome
    vec3 wp = world_pos;
    float dist = length(wp.xz - dome_center.xz);

    // 1 = fully inside dome, 0 = fully outside, smooth transition
    float blend = 1.0 - smoothstep(dome_radius - dome_falloff, dome_radius, dist);

    // Planet base
    vec3 p_col = planet_color;
    vec2 tuv = (tex_world_size.x > 0.0 ? wp.xz / tex_world_size : UV) * tex_scale.xy;
    if (use_texture) {
        // planet_color tints the texture (the dusk grade)
        p_col = texture(planet_texture, tuv).rgb * planet_color;
        if (use_normal) {
            NORMAL_MAP = texture(planet_normal, tuv).rgb;
            NORMAL_MAP_DEPTH = 1.0 - blend;
        }
    }

    // Blend planet → BIT surface
    vec3 base_col = mix(p_col, bit_color, blend);
    ROUGHNESS = mix(planet_roughness, bit_roughness, blend);
    METALLIC = mix(planet_metallic, bit_metallic, blend);

    if (show_grid) {
        // Grid lines (world-space, only inside dome) — Tron aesthetic
        vec2 grid_uv = wp.xz * grid_spacing;
        vec2 grid = abs(fract(grid_uv - 0.5) - 0.5);
        float line = min(grid.x, grid.y);
        float grid_mask = (1.0 - smoothstep(grid_width, grid_width + 0.02, line)) * blend;
        ALBEDO = mix(base_col, grid_color, grid_mask * 0.6);
        EMISSION = grid_color * grid_emission_strength * grid_mask
                 + bit_emission * bit_emission_strength * blend;
    } else {
        // No grid — solid BIT palette blend (Scrapyard)
        ALBEDO = base_col;
        EMISSION = bit_emission * bit_emission_strength * blend;
    }
}
";
            return _groundShader;
        }

        /// <summary>
        /// Create a dome-aware ground material that blends planet terrain to BIT white
        /// inside the conversion dome. Pass the planet's ground properties.
        /// Call UpdateGroundDome() each frame to push dome position/radius.
        /// </summary>
        public static ShaderMaterial MakeDomeGroundMaterial(
            Color planetColor, float planetRoughness, float planetMetallic,
            Texture2D planetTexture = null, Vector3? texScale = null,
            bool showGrid = true, Texture2D planetNormal = null)
        {
            var mat = new ShaderMaterial();
            mat.Shader = GetGroundShader();

            mat.SetShaderParameter("planet_color",
                new Vector3(planetColor.R, planetColor.G, planetColor.B));
            mat.SetShaderParameter("planet_roughness", planetRoughness);
            mat.SetShaderParameter("planet_metallic", planetMetallic);

            if (planetTexture != null)
            {
                mat.SetShaderParameter("use_texture", true);
                mat.SetShaderParameter("planet_texture", planetTexture);
                var scale = texScale ?? new Vector3(8, 8, 1);
                mat.SetShaderParameter("tex_scale", scale);
                mat.SetShaderParameter("use_normal", planetNormal != null);
                if (planetNormal != null) mat.SetShaderParameter("planet_normal", planetNormal);
            }
            else
            {
                mat.SetShaderParameter("use_texture", false);
            }

            // Ground inside the dome: the planet's converted-ground colour, with a faint accent glow
            var conv = PlanetTheme.Current.ConvertedGroundColor;
            mat.SetShaderParameter("bit_color", new Vector3(conv.R, conv.G, conv.B));
            mat.SetShaderParameter("bit_emission_strength", showGrid ? 0.12f : 0.04f);
            mat.SetShaderParameter("bit_roughness", 0.25f);
            mat.SetShaderParameter("bit_metallic", 0.75f);
            mat.SetShaderParameter("bit_emission", new Vector3(Accent.R, Accent.G, Accent.B));

            // Grid lines inside dome (Tron only)
            mat.SetShaderParameter("show_grid", showGrid);
            // Inside the dome the grid takes the player's accent (Grid Prime: orange on cyan)
            mat.SetShaderParameter("grid_color", new Vector3(Accent.R, Accent.G, Accent.B));
            mat.SetShaderParameter("grid_spacing", 2.0f);
            mat.SetShaderParameter("grid_width", 0.04f);
            mat.SetShaderParameter("grid_emission_strength", 0.25f);

            // Start with no dome (radius 0)
            mat.SetShaderParameter("dome_center", Vector3.Zero);
            mat.SetShaderParameter("dome_radius", 0f);
            mat.SetShaderParameter("dome_falloff", 2.5f);

            return mat;
        }

        // ── Player-object accent rim ──

        private static Shader _rimOverlayShader;
        public static readonly StringName MetaAccentRim = "accent_rim";
        private static readonly Dictionary<(Color, float), ShaderMaterial> _rimOverlays = new();

        /// <summary>
        /// Overlay a fresnel rim in the planet's player accent on a model, keeping its own
        /// materials. Towers' textured PBR read as black blobs under Grid Prime's low light;
        /// the rim says "yours" (orange on Grid Prime, cyan on Scrapyard). Per instance
        /// (MaterialOverlay), so shared imported materials are untouched.
        /// </summary>
        public static void ApplyAccentRim(Node3D root, Color color, float strength)
        {
            if (root == null || strength <= 0f) return;
            var mat = GetAccentRimMaterial(color, strength);
            foreach (var n in root.FindChildren("*", "MeshInstance3D", true, false))
                if (n is MeshInstance3D mi) mi.MaterialOverlay = mat;
        }

        /// <summary>The shared accent-rim overlay material for a colour and strength.</summary>
        public static ShaderMaterial GetAccentRimMaterial(Color color, float strength)
        {
            var key = (color, strength);
            if (!_rimOverlays.TryGetValue(key, out var mat) || !GodotObject.IsInstanceValid(mat))
            {
                _rimOverlayShader ??= new Shader { Code = @"
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_back, shadows_disabled;
uniform vec3 rim_color : source_color = vec3(1.0, 0.58, 0.18);
uniform float strength = 1.0;
void fragment() {
    float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    float rim = pow(1.0 - ndv, 4.0);
    ALBEDO = rim_color * (rim * strength + 0.015 * strength);
}
" };
                mat = new ShaderMaterial { Shader = _rimOverlayShader };
                mat.SetMeta(MetaAccentRim, true);
                mat.SetShaderParameter("rim_color", new Vector3(color.R, color.G, color.B));
                mat.SetShaderParameter("strength", strength);
                _rimOverlays[key] = mat;
            }
            return mat;
        }

        /// <summary>
        /// Push dome center and radius to a ground shader material each frame.
        /// </summary>
        public static void UpdateGroundDome(ShaderMaterial groundMat, Vector3 center, float radius)
        {
            groundMat?.SetShaderParameter("dome_center", center);
            groundMat?.SetShaderParameter("dome_radius", radius);
        }
    }
}

