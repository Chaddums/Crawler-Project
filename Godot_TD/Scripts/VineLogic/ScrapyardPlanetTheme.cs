using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Planet 2: Scrapyard — rusted metal, industrial grime, mechanical debris.
    /// The Junkbot Arena aesthetic. Warm browns, oranges, corroded steel.
    /// Opposite feel to Tron's clean digital lines.
    ///
    /// Visual approach: rough textured surfaces with warm emission glow,
    /// not clean outlines. Things look heat-damaged and corroded.
    /// </summary>
    public class ScrapyardPlanetTheme : PlanetTheme
    {
        public override string PlanetName => "Scrapyard";
        public override string PlanetDescription => "A dying industrial world. Rusted hulks and corroded machinery litter the surface. Everything here was built to last — and failed.";

        // Base palette — warm industrial
        public override Color GroundColor => new(0.12f, 0.09f, 0.07f);
        public override Color GridLineColor => new(0.6f, 0.35f, 0.1f);
        public override Color WallColor => new(0.15f, 0.12f, 0.1f);
        public override Color BackgroundColor => new(0.06f, 0.04f, 0.03f);
        public override Color AmbientColor => new(0.12f, 0.08f, 0.05f);
        public override Color FogColor => new(0.08f, 0.06f, 0.04f);

        // Factions — warm tones
        public override Color PlayerPrimary => new(0.9f, 0.6f, 0.15f);
        public override Color PlayerSensor => new(0.8f, 0.55f, 0.2f);
        public override Color PlayerEffect => new(0.9f, 0.45f, 0.1f);
        public override Color PlayerRoute => new(0.7f, 0.5f, 0.25f);

        public override Color EnemyScavenger => new(0.4f, 0.6f, 0.3f);
        public override Color EnemyBrute => new(0.3f, 0.3f, 0.35f);
        public override Color EnemySwarm => new(0.5f, 0.7f, 0.2f);
        public override Color EnemyGhost => new(0.35f, 0.5f, 0.45f);

        // VFX
        public override Color ProjectileColor => new(0.95f, 0.6f, 0.1f);
        public override Color SignalPulseColor => new(0.9f, 0.5f, 0.15f);
        public override Color DeathBurstColor => new(0.6f, 0.4f, 0.2f);
        public override Color ImpactFlashColor => new(1f, 0.8f, 0.4f);

        // UI
        public override Color EntryMarkerColor => new(0.9f, 0.7f, 0.2f);
        public override Color ExitMarkerColor => new(0.8f, 0.2f, 0.15f);
        public override Color PanelBgColor => new(0.08f, 0.06f, 0.05f);

        // Lighting
        public override Color MainLightColor => new(0.9f, 0.75f, 0.55f);
        public override Color FillLightColor => new(0.15f, 0.1f, 0.08f);

        // ── Scrapyard-specific shader ──
        private static Shader _rustShader;

        private static Shader GetRustShader()
        {
            if (_rustShader != null) return _rustShader;
            _rustShader = new Shader();
            _rustShader.Code = @"
shader_type spatial;

uniform vec3 base_color : source_color = vec3(0.12, 0.08, 0.06);
uniform vec3 rust_color : source_color = vec3(0.5, 0.25, 0.08);
uniform vec3 accent_color : source_color = vec3(0.9, 0.6, 0.15);
uniform float rust_amount : hint_range(0.0, 1.0) = 0.4;
uniform float accent_intensity : hint_range(0.0, 1.0) = 0.15;

void fragment() {
    // Procedural rust pattern using vertex position
    float noise = fract(sin(dot(VERTEX.xz * 3.0, vec2(12.9898, 78.233))) * 43758.5453);
    float rust_mask = smoothstep(0.3, 0.7, noise) * rust_amount;

    // Mix base dark with rust patches
    vec3 surface = mix(base_color, rust_color, rust_mask);

    ALBEDO = surface;
    METALLIC = mix(0.6, 0.3, rust_mask); // Rust is less metallic
    ROUGHNESS = mix(0.7, 0.95, rust_mask); // Rust is rougher

    // Faint accent glow from heat/energy — stronger on non-rusted areas
    float rim = pow(1.0 - dot(NORMAL, VIEW), 2.5);
    EMISSION = accent_color * rim * accent_intensity * (1.0 - rust_mask * 0.5);
}
";
            return _rustShader;
        }

        public override StandardMaterial3D MakeThemedMaterial(Color tint)
        {
            // Fallback for non-shader contexts
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = tint.Darkened(0.5f);
            mat.Roughness = 0.85f;
            mat.Metallic = 0.6f;
            mat.EmissionEnabled = true;
            mat.Emission = tint;
            mat.EmissionEnergyMultiplier = 0.25f;
            return mat;
        }

        public override void ApplyToNode(Node3D node, Color? tint = null)
        {
            Color accent = tint ?? PlayerPrimary;
            ApplyRustShaderRecursive(node, accent, false);
        }

        public override void ApplyEnemyTheme(Node3D node, VineEnemyFaction faction)
        {
            Color factionColor = faction switch {
                VineEnemyFaction.Scavenger => EnemyScavenger,
                VineEnemyFaction.Brute => EnemyBrute,
                VineEnemyFaction.Swarm => EnemySwarm,
                VineEnemyFaction.Ghost => EnemyGhost,
                _ => EnemyScavenger
            };
            ApplyRustShaderRecursive(node, factionColor, true);
        }

        private static void ApplyRustShaderRecursive(Node node, Color accent, bool isEnemy)
        {
            if (node is MeshInstance3D mesh)
            {
                var shader = GetRustShader();
                var mat = new ShaderMaterial();
                mat.Shader = shader;

                if (isEnemy)
                {
                    // Enemies: darker base, more rust, toxic accent
                    mat.SetShaderParameter("base_color", new Vector3(0.08f, 0.06f, 0.05f));
                    mat.SetShaderParameter("rust_color", new Vector3(0.3f, 0.2f, 0.1f));
                    mat.SetShaderParameter("rust_amount", 0.6f);
                    mat.SetShaderParameter("accent_intensity", 0.25f);
                }
                else
                {
                    // Player: warmer base, less rust, amber accent
                    mat.SetShaderParameter("base_color", new Vector3(0.1f, 0.07f, 0.05f));
                    mat.SetShaderParameter("rust_color", new Vector3(0.5f, 0.25f, 0.08f));
                    mat.SetShaderParameter("rust_amount", 0.35f);
                    mat.SetShaderParameter("accent_intensity", 0.2f);
                }

                mat.SetShaderParameter("accent_color",
                    new Vector3(accent.R, accent.G, accent.B));

                mesh.MaterialOverride = mat;
            }
            foreach (var child in node.GetChildren())
                ApplyRustShaderRecursive(child, accent, isEnemy);
        }

        public override StandardMaterial3D MakeEnemyMaterial(Color tint)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = tint.Darkened(0.3f);
            mat.Roughness = 0.9f;
            mat.Metallic = 0.5f;
            mat.EmissionEnabled = true;
            mat.Emission = tint;
            mat.EmissionEnergyMultiplier = 0.4f;
            return mat;
        }

        public override StandardMaterial3D MakeProjectileMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = ProjectileColor;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = ProjectileColor;
            mat.EmissionEnergyMultiplier = 2.5f;
            return mat;
        }

        public override StandardMaterial3D MakeGroundMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = GroundColor;
            mat.Roughness = 0.95f;
            mat.Metallic = 0.1f;
            return mat;
        }

        public override StandardMaterial3D MakeWallMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = WallColor;
            mat.Roughness = 0.9f;
            mat.Metallic = 0.5f;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.5f, 0.25f, 0.08f);
            mat.EmissionEnergyMultiplier = 0.15f;
            return mat;
        }

        // ── Scrapyard-specific material factories ──

        /// <summary>
        /// Grid lines for scrapyard — rusty orange instead of cyan.
        /// </summary>
        public static StandardMaterial3D MakeScrapGridLineMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.6f, 0.35f, 0.1f, 0.4f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.6f, 0.35f, 0.1f);
            mat.EmissionEnergyMultiplier = 0.8f;
            return mat;
        }

        /// <summary>
        /// Scrapyard terrain body — dark corroded metal.
        /// </summary>
        public static StandardMaterial3D MakeScrapTerrainMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.1f, 0.08f, 0.06f);
            mat.Roughness = 0.9f;
            mat.Metallic = 0.5f;
            return mat;
        }

        /// <summary>
        /// Scrapyard terrain outline — warm orange instead of cyan.
        /// </summary>
        public static void ApplyScrapOutline(MeshInstance3D mesh, float outlineWidth = 0.03f)
        {
            var bodyMat = MakeScrapTerrainMaterial();
            bodyMat.AlbedoColor = new Color(0.04f, 0.03f, 0.025f);
            bodyMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            bodyMat.EmissionEnabled = false;

            var outlineShader = new Shader();
            outlineShader.Code = @"
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 outline_color : source_color = vec3(0.6, 0.35, 0.1);
uniform float outline_width : hint_range(0.0, 0.3) = 0.03;
void vertex() { VERTEX += NORMAL * outline_width; }
void fragment() { ALBEDO = outline_color; ALPHA = 0.6; }
";
            var outlineMat = new ShaderMaterial();
            outlineMat.Shader = outlineShader;
            outlineMat.SetShaderParameter("outline_color", new Vector3(0.6f, 0.35f, 0.1f));
            outlineMat.SetShaderParameter("outline_width", outlineWidth);

            bodyMat.NextPass = outlineMat;
            mesh.MaterialOverride = bodyMat;
        }
    }
}
