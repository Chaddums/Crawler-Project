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

        // ── Look (art direction) ──
        // One place per planet for lighting, environment, the dome and the accent that marks
        // everything the player owns. VineBattleScene and ConversionDome read these.

        /// <summary>Accent for what belongs to the player: towers' trim, BIT, the Spire, the dome edge.</summary>
        public virtual Color PlayerAccent => new(0.9f, 0.93f, 1.0f);
        /// <summary>The Conversion Dome's edge ring.</summary>
        public virtual Color DomeRimColor => PlayerAccent;
        public virtual float DomeRimStrength => 1.4f;
        /// <summary>Faint tint inside the dome ring; 0 alpha leaves the ground as it is.</summary>
        public virtual Color DomeFillColor => new(0.9f, 0.9f, 0.92f);
        public virtual float DomeFillAlpha => 0.12f;
        /// <summary>Multiplier on the dome's soft halo rings (1 = the original bright halo).</summary>
        public virtual float DomeHaloAlpha => 0.35f;
        /// <summary>Ground colour the dome blends to inside its radius.</summary>
        public virtual Color ConvertedGroundColor => new(0.08f, 0.08f, 0.12f);
        /// <summary>Shield walls' opacity (1 = solid glow).</summary>
        public virtual float ShieldWallOpacity => 1f;
        /// <summary>Strength of the player-accent rim on towers (0 = none).</summary>
        public virtual float TowerRimStrength => 0f;

        /// <summary>Material for terrain decor and props the dome has taken over.</summary>
        public virtual StandardMaterial3D MakeConvertedMaterial()
        {
            // Dark metal in the dome's colour, lit so its faces read. It was unshaded near-black:
            // seen from above (where the rim doesn't show) converted blocks on Grid Prime's orange
            // floor looked like black holes in the ground.
            var m = new StandardMaterial3D();
            m.AlbedoColor = DomeRimColor.Darkened(0.78f);
            m.Roughness = 0.45f;
            m.Metallic = 0.5f;
            m.EmissionEnabled = true;
            m.Emission = DomeRimColor;
            m.EmissionEnergyMultiplier = 0.22f;
            return m;
        }

        /// <summary>Sky, ambient, fog, tonemap and post effects for the battle.</summary>
        public virtual void ConfigureEnvironment(Godot.Environment env) { }

        /// <summary>Key and fill light direction, colour and strength.</summary>
        public virtual void ConfigureLights(DirectionalLight3D sun, DirectionalLight3D fill) { }

        protected static void SetLight(DirectionalLight3D l, float pitch, float yaw, Color c, float energy)
        {
            if (l == null) return;
            l.RotationDegrees = new Vector3(pitch, yaw, 0);
            l.LightColor = c;
            l.LightEnergy = energy;
        }

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
        // Entries wear the enemy colour: "they come in here" (teal vanished into the cyan grid)
        public override Color EntryMarkerColor => TronTheme.EnemyScavenger;
        public override Color ExitMarkerColor => TronTheme.ExitRed;
        public override Color PanelBgColor => TronTheme.PanelBg;

        // Lighting
        public override Color MainLightColor => TronTheme.MainLight;
        public override Color FillLightColor => TronTheme.FillLight;

        // ── Look: two-tone. The world stays cyan; everything the player owns glows orange. ──
        public override Color PlayerAccent => new(1.0f, 0.58f, 0.18f);
        public override float DomeRimStrength => 2.0f;
        public override Color DomeFillColor => new(0.01f, 0.015f, 0.03f);
        public override float DomeFillAlpha => 0f;
        public override float DomeHaloAlpha => 0.15f;
        public override Color ConvertedGroundColor => new(0.02f, 0.02f, 0.035f);
        public override float ShieldWallOpacity => 0.6f;
        public override float TowerRimStrength => 0.9f;

        public override void ConfigureEnvironment(Godot.Environment env)
        {
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = new Color(0.005f, 0.008f, 0.015f);
            env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            env.AmbientLightColor = new Color(0.08f, 0.12f, 0.18f);
            env.AmbientLightEnergy = 0.4f;
            env.TonemapMode = Godot.Environment.ToneMapper.Aces;
            env.TonemapExposure = 1.1f;
            env.GlowEnabled = true;
            env.GlowHdrThreshold = 0.9f;
            env.GlowIntensity = 0.8f;
            env.GlowBloom = 0.06f;
            env.FogEnabled = true;
            env.FogLightColor = new Color(0f, 0.12f, 0.18f);
            env.FogDensity = 0.007f;
            env.FogAerialPerspective = 0.5f;
        }

        public override void ConfigureLights(DirectionalLight3D sun, DirectionalLight3D fill)
        {
            SetLight(sun, -60, -30, new Color(0.55f, 0.75f, 1f), 0.35f);
            SetLight(fill, -30, 150, new Color(1f, 0.55f, 0.2f), 0.15f);
        }

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
