using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Base class for planet visual themes. Each planet implements one of these.
    /// Defines the full palette + material factories so any asset can be "themed"
    /// to look native to that planet.
    ///
    /// TronTheme is the first planet. Future planets (RustWorld, IceMoon, etc.)
    /// will be additional implementations with different palettes.
    /// </summary>
    public abstract class PlanetTheme
    {
        // ── Identity ──
        public abstract string PlanetName { get; }
        public abstract string PlanetDescription { get; }

        // ── Base Palette ──
        public abstract Color GroundColor { get; }
        public abstract Color GridLineColor { get; }
        public abstract Color WallColor { get; }
        public abstract Color BackgroundColor { get; }
        public abstract Color AmbientColor { get; }
        public abstract Color FogColor { get; }

        // ── Faction Colors ──
        public abstract Color PlayerPrimary { get; }       // Player node tint
        public abstract Color PlayerSensor { get; }
        public abstract Color PlayerEffect { get; }
        public abstract Color PlayerRoute { get; }

        public abstract Color EnemyScavenger { get; }
        public abstract Color EnemyBrute { get; }
        public abstract Color EnemySwarm { get; }
        public abstract Color EnemyGhost { get; }

        // ── VFX Colors ──
        public abstract Color ProjectileColor { get; }
        public abstract Color SignalPulseColor { get; }
        public abstract Color DeathBurstColor { get; }
        public abstract Color ImpactFlashColor { get; }

        // ── UI Colors ──
        public abstract Color EntryMarkerColor { get; }
        public abstract Color ExitMarkerColor { get; }
        public abstract Color PanelBgColor { get; }

        // ── Lighting ──
        public abstract Color MainLightColor { get; }
        public abstract Color FillLightColor { get; }

        // ── Material Factories ──

        /// <summary>
        /// Apply this planet's theme to any Node3D.
        /// Replaces all materials on MeshInstance3D children with themed versions.
        /// The tint parameter allows per-faction or per-role color variation.
        /// </summary>
        public virtual void ApplyToNode(Node3D node, Color? tint = null)
        {
            var mat = MakeThemedMaterial(tint ?? PlayerPrimary);
            ApplyMaterialRecursive(node, mat);
        }

        /// <summary>
        /// Apply enemy faction theming to a node.
        /// </summary>
        public virtual void ApplyEnemyTheme(Node3D node, VineEnemyFaction faction)
        {
            Color factionColor = faction switch {
                VineEnemyFaction.Scavenger => EnemyScavenger,
                VineEnemyFaction.Brute => EnemyBrute,
                VineEnemyFaction.Swarm => EnemySwarm,
                VineEnemyFaction.Ghost => EnemyGhost,
                _ => EnemyScavenger
            };
            var mat = MakeEnemyMaterial(factionColor);
            ApplyMaterialRecursive(node, mat);
        }

        /// <summary>
        /// Create a material that fits this planet's aesthetic.
        /// Override per planet for different looks (Tron = dark+emissive, Rust = rough+metallic, etc.)
        /// </summary>
        public abstract StandardMaterial3D MakeThemedMaterial(Color tint);

        /// <summary>
        /// Create an enemy material for this planet.
        /// </summary>
        public abstract StandardMaterial3D MakeEnemyMaterial(Color tint);

        /// <summary>
        /// Create a projectile material.
        /// </summary>
        public abstract StandardMaterial3D MakeProjectileMaterial();

        /// <summary>
        /// Create the ground plane material.
        /// </summary>
        public abstract StandardMaterial3D MakeGroundMaterial();

        /// <summary>
        /// Create a wall material.
        /// </summary>
        public abstract StandardMaterial3D MakeWallMaterial();

        // ── Utility ──

        protected static void ApplyMaterialRecursive(Node node, StandardMaterial3D material)
        {
            if (node is MeshInstance3D mesh)
                mesh.MaterialOverride = material;
            foreach (var child in node.GetChildren())
                ApplyMaterialRecursive(child, material);
        }

        /// <summary>
        /// Get the current active planet theme.
        /// For alpha, this is always TronPlanetTheme.
        /// Later, GameManager will track which planet the player is on.
        /// </summary>
        public static PlanetTheme Current { get; set; } = new TronPlanetTheme();
    }

    /// <summary>
    /// Tron Legacy planet — first planet in the game.
    /// Dark blue-black surfaces, bright cyan emissive grid lines,
    /// blue player programs, red enemy programs.
    /// </summary>
    public class TronPlanetTheme : PlanetTheme
    {
        public override string PlanetName => "Grid Prime";
        public override string PlanetDescription => "A digital construct. Data flows through luminous pathways across an endless dark plane.";

        // Base palette — pull from existing TronTheme constants
        public override Color GroundColor => TronTheme.GroundBase;
        public override Color GridLineColor => TronTheme.GridCyan;
        public override Color WallColor => TronTheme.WallBase;
        public override Color BackgroundColor => TronTheme.Background;
        public override Color AmbientColor => TronTheme.Ambient;
        public override Color FogColor => TronTheme.FogColor;

        // Factions
        public override Color PlayerPrimary => new(0.0f, 0.8f, 0.9f);
        public override Color PlayerSensor => TronTheme.NodeSensor;
        public override Color PlayerEffect => TronTheme.NodeEffect;
        public override Color PlayerRoute => TronTheme.NodeRoute;

        public override Color EnemyScavenger => TronTheme.EnemyScavenger;
        public override Color EnemyBrute => TronTheme.EnemyBrute;
        public override Color EnemySwarm => TronTheme.EnemySwarm;
        public override Color EnemyGhost => TronTheme.EnemyGhost;

        // VFX
        public override Color ProjectileColor => new(0.0f, 0.85f, 0.95f);
        public override Color SignalPulseColor => new(0.0f, 0.9f, 0.8f);
        public override Color DeathBurstColor => new(0.9f, 0.2f, 0.1f);
        public override Color ImpactFlashColor => new(1f, 1f, 1f);

        // UI
        public override Color EntryMarkerColor => TronTheme.EntryTeal;
        public override Color ExitMarkerColor => TronTheme.ExitRed;
        public override Color PanelBgColor => TronTheme.PanelBg;

        // Lighting
        public override Color MainLightColor => TronTheme.MainLight;
        public override Color FillLightColor => TronTheme.FillLight;

        // Cached Tron rim shader
        private static Shader _tronRimShader;

        private static Shader GetTronRimShader()
        {
            if (_tronRimShader != null) return _tronRimShader;
            _tronRimShader = new Shader();
            _tronRimShader.Code = @"
shader_type spatial;
uniform vec3 base_color : source_color = vec3(0.02, 0.02, 0.04);
uniform vec3 rim_color : source_color = vec3(0.0, 0.85, 0.95);
uniform float rim_power : hint_range(0.5, 12.0) = 5.0;
uniform float rim_intensity : hint_range(0.0, 3.0) = 0.8;

void fragment() {
    ALBEDO = base_color;
    METALLIC = 0.5;
    ROUGHNESS = 0.7;

    // Tight Fresnel rim — only fires at true silhouette edges
    // Higher power = tighter edge, smoothstep kills internal mesh junction noise
    float ndv = dot(NORMAL, VIEW);
    float raw_rim = pow(max(1.0 - ndv, 0.0), rim_power);
    // Smooth threshold — kill anything below 0.15 to eliminate internal seam glow
    float rim = smoothstep(0.1, 0.6, raw_rim) * rim_intensity;
    EMISSION = rim_color * rim;

    // Subtle ambient glow so the whole model isn't pitch black
    EMISSION += rim_color * 0.03;
}
";
            return _tronRimShader;
        }

        public override StandardMaterial3D MakeThemedMaterial(Color tint)
        {
            // Fallback for non-shader contexts — but ApplyToNode uses the shader version
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = TronTheme.WallBase;
            mat.Roughness = 0.7f;
            mat.Metallic = 0.5f;
            return mat;
        }

        /// <summary>
        /// 0 = Per-mesh outline (each mesh gets own outline — shows internal edges)
        /// 1 = Silhouette only (one outline clone — clean outer edge only)
        /// 2 = No outline (dark body only)
        /// </summary>
        public int OutlineMode { get; set; }

        public override void ApplyToNode(Node3D node, Color? tint = null)
        {
            Color rimColor = tint ?? PlayerPrimary;
            ApplyWithMode(node, rimColor);
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
            ApplyWithMode(node, factionColor);
        }

        private void ApplyWithMode(Node3D node, Color accentColor)
        {
            GD.Print($"[TronTheme] ApplyWithMode: outline={OutlineMode} ({OutlineModes[OutlineMode]}), color=({accentColor.R:F2},{accentColor.G:F2},{accentColor.B:F2})");

            // Clean up any previous silhouette clones
            var parent = node.GetParent();
            if (parent != null)
            {
                for (int i = parent.GetChildCount() - 1; i >= 0; i--)
                {
                    var child = parent.GetChild(i);
                    if (child is Node3D n3d && n3d.HasMeta("silhouette_clone"))
                        n3d.QueueFree();
                }
            }

            switch (OutlineMode)
            {
                case 1: // Silhouette — accent body + black outline clone
                    ApplyDarkBodyRecursive(node, accentColor);
                    AddSilhouetteOutline(node, accentColor);
                    break;
                case 2: // No outline — accent body only
                    ApplyDarkBodyRecursive(node, accentColor);
                    break;
                default: // Per-mesh outline
                    ApplyTronFlatRecursive(node, accentColor);
                    break;
            }
        }

        private static readonly string[] OutlineModes = { "Per-Mesh Outline", "Silhouette Only", "No Outline" };

        private static void ApplyDarkBodyRecursive(Node node, Color? accentColor = null)
        {
            if (node is MeshInstance3D mesh)
            {
                // Body is the ACCENT color (teal) — the silhouette clone behind is black
                // So: teal body, black enlarged clone peeking out = black outline on teal shape
                var color = accentColor ?? new Color(0.01f, 0.01f, 0.02f);
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = color;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.EmissionEnabled = true;
                mat.Emission = color;
                mat.EmissionEnergyMultiplier = 0.4f;
                mesh.MaterialOverride = mat;
            }
            foreach (var child in node.GetChildren())
                ApplyDarkBodyRecursive(child, accentColor);
        }

        /// <summary>
        /// Clone the entire model, apply outline shader to all meshes in the clone,
        /// and add it as a sibling. One outline for the whole model = clean silhouette.
        /// </summary>
        private void AddSilhouetteOutline(Node3D original, Color outlineColor)
        {
            // Deep duplicate — flag 15 = all (signals, groups, scripts, subresources)
            var clone = original.Duplicate(15) as Node3D;
            if (clone == null)
            {
                GD.PrintErr("[TronTheme] Failed to duplicate model for silhouette");
                return;
            }

            // Use silhouette shader — renders solid enlarged shape
            // The original's dark body sits in front and occludes the interior,
            // so you only see the outline color peeking around the outer edges
            // Silhouette clone is BLACK — body is the accent color
            // Black clone peeks out around edges = dark outline on bright shape
            GetOutlineShader(); // Ensures _silhouetteShader is initialized too
            // Scale-compensate outline_width for consistent world-space thickness
            float modelScale = original.Scale.X;
            float silWidth = 0.12f / Mathf.Max(modelScale, 0.01f);

            var silMat = new ShaderMaterial();
            silMat.Shader = _silhouetteShader;
            silMat.SetShaderParameter("outline_color", new Vector3(0.01f, 0.01f, 0.02f));
            silMat.SetShaderParameter("outline_width", Mathf.Clamp(silWidth, 0.02f, 0.5f));
            silMat.RenderPriority = -1;

            ApplyMaterialToAllMeshes(clone, silMat);

            // Mark as silhouette clone for cleanup
            clone.SetMeta("silhouette_clone", true);

            // Add clone as sibling (same parent as original)
            var parent = original.GetParent();
            if (parent != null)
            {
                parent.AddChild(clone);
                clone.Position = original.Position;
                clone.Scale = original.Scale;
                clone.Rotation = original.Rotation;
            }

            int cloneMeshes = CountMeshInstances(clone);
            int origMeshes = CountMeshInstances(original);
            GD.Print($"[TronTheme] Silhouette clone: {cloneMeshes} meshes (original had {origMeshes})");
        }

        private static void ApplyMaterialToAllMeshes(Node node, ShaderMaterial mat)
        {
            if (node is MeshInstance3D mesh)
                mesh.MaterialOverride = mat;
            foreach (var child in node.GetChildren())
                ApplyMaterialToAllMeshes(child, mat);
        }

        // Cached shaders
        private static Shader _outlineShader;
        private static Shader _silhouetteShader;

        private static Shader GetOutlineShader()
        {
            if (_outlineShader != null) return _outlineShader;
            _outlineShader = new Shader();
            _outlineShader.Code = @"
shader_type spatial;
render_mode unshaded, cull_front;

uniform vec3 outline_color : source_color = vec3(0.0, 0.85, 0.95);
uniform float outline_width : hint_range(0.0, 0.3) = 0.025;

void vertex() {
    VERTEX += NORMAL * outline_width;
}

void fragment() {
    ALBEDO = outline_color;
    ALPHA = 0.9;
}
";
            // Silhouette shader — renders solid enlarged shape, original body occludes interior
            _silhouetteShader = new Shader();
            _silhouetteShader.Code = @"
shader_type spatial;
render_mode unshaded, depth_draw_always;

uniform vec3 outline_color : source_color = vec3(0.0, 0.85, 0.95);
uniform float outline_width : hint_range(0.0, 0.5) = 0.12;

void vertex() {
    // Enlarge in all directions along normal
    VERTEX += NORMAL * outline_width;
}

void fragment() {
    ALBEDO = outline_color;
}
";
            return _outlineShader;
        }

        private static void ApplyTronFlatRecursive(Node node, Color accentColor)
        {
            if (node is MeshInstance3D mesh)
            {
                // Pass 1: solid black body — unshaded so internal surfaces are invisible
                var bodyMat = new StandardMaterial3D();
                bodyMat.AlbedoColor = new Color(0.01f, 0.01f, 0.02f);
                bodyMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

                // Pass 2: inverted hull outline in accent color
                // Scale-compensate outline_width for consistent world-space thickness
                float modelScale = mesh.GetParent() is Node3D parent ? parent.Scale.X : 1f;
                float outlineWidth = 0.06f / Mathf.Max(modelScale, 0.01f);

                var outlineMat = new ShaderMaterial();
                outlineMat.Shader = GetOutlineShader();
                outlineMat.SetShaderParameter("outline_color",
                    new Vector3(accentColor.R, accentColor.G, accentColor.B));
                outlineMat.SetShaderParameter("outline_width", Mathf.Clamp(outlineWidth, 0.01f, 0.3f));
                outlineMat.RenderPriority = -1;

                // Chain: body renders first, outline renders second
                bodyMat.NextPass = outlineMat;
                mesh.MaterialOverride = bodyMat;
            }
            foreach (var child in node.GetChildren())
                ApplyTronFlatRecursive(child, accentColor);
        }

        private static int CountMeshInstances(Node node)
        {
            int count = 0;
            if (node is MeshInstance3D) count++;
            foreach (var child in node.GetChildren())
                count += CountMeshInstances(child);
            return count;
        }

        private static void ApplyTronShaderRecursive(Node node, Shader shader, Color rimColor)
        {
            if (node is MeshInstance3D mesh)
            {
                var mat = new ShaderMaterial();
                mat.Shader = shader;
                mat.SetShaderParameter("base_color", new Vector3(
                    TronTheme.WallBase.R, TronTheme.WallBase.G, TronTheme.WallBase.B));
                mat.SetShaderParameter("rim_color", new Vector3(rimColor.R, rimColor.G, rimColor.B));
                mat.SetShaderParameter("rim_power", 3.0f);
                mat.SetShaderParameter("rim_intensity", 1.2f);
                mesh.MaterialOverride = mat;
            }
            foreach (var child in node.GetChildren())
                ApplyTronShaderRecursive(child, shader, rimColor);
        }

        public override StandardMaterial3D MakeEnemyMaterial(Color tint)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = TronTheme.WallBase;
            mat.Roughness = 0.7f;
            mat.Metallic = 0.5f;
            return mat;
        }

        public override StandardMaterial3D MakeProjectileMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = ProjectileColor;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = ProjectileColor;
            mat.EmissionEnergyMultiplier = 1.2f;
            return mat;
        }

        public override StandardMaterial3D MakeGroundMaterial() => TronTheme.MakeGroundMaterial();
        public override StandardMaterial3D MakeWallMaterial() => TronTheme.MakeWallBodyMaterial();
    }
}
