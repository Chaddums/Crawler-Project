using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Centralized Tron Legacy palette and material factories for the first planet.
    /// Dark blue-black surfaces with bright cyan emissive grid lines.
    /// Future planets (Rust World, Ice Moon, etc.) will swap palettes via a similar class.
    /// </summary>
    public static class TronTheme
    {
        // ── Palette ──

        public static readonly Color GroundBase = new(0.02f, 0.02f, 0.04f);
        public static readonly Color GridCyan = new(0.0f, 0.85f, 0.95f);
        public static readonly Color WallBase = new(0.04f, 0.04f, 0.06f);
        public static readonly Color Background = new(0.01f, 0.01f, 0.025f);
        public static readonly Color Ambient = new(0.02f, 0.06f, 0.1f);
        public static readonly Color EntryTeal = new(0.0f, 0.9f, 0.7f);
        public static readonly Color ExitRed = new(0.95f, 0.2f, 0.1f);
        public static readonly Color PlanetBase = new(0.02f, 0.02f, 0.05f);
        public static readonly Color PillarBase = new(0.03f, 0.03f, 0.05f);
        public static readonly Color SpaceBackground = new(0.005f, 0.005f, 0.015f);
        public static readonly Color SurfaceSun = new(0.3f, 0.4f, 0.5f);
        public static readonly Color MainLight = new(0.4f, 0.5f, 0.6f);
        public static readonly Color FillLight = new(0.05f, 0.1f, 0.15f);
        public static readonly Color CraterColor = new(0.01f, 0.01f, 0.02f);
        public static readonly Color DebrisDark = new(0.04f, 0.04f, 0.06f);
        public static readonly Color DebrisCyan = new(0.0f, 0.7f, 0.9f);
        public static readonly Color PanelBg = new(0.02f, 0.02f, 0.04f);
        public static readonly Color HelpBorder = new(0.0f, 0.5f, 0.6f);
        public static readonly Color CliffBase = new(0.025f, 0.025f, 0.045f);
        public static readonly Color FogColor = new(0.01f, 0.03f, 0.06f);
        public static readonly Color HorizonSilhouette = new(0.015f, 0.015f, 0.03f);

        // ── Tron Faction Palettes ──
        // Player = blue/cyan program, Enemy = red/orange program

        // Player node tints (by category — 3D meshes only, HUD keeps functional colors)
        public static readonly Color NodeSensor = new(0.15f, 0.55f, 0.7f);    // Teal-blue
        public static readonly Color NodeEffect = new(0.2f, 0.4f, 0.8f);      // Medium blue
        public static readonly Color NodeRoute = new(0.3f, 0.5f, 0.65f);      // Steel blue

        // Enemy faction tints (all red family, vary by faction)
        public static readonly Color EnemyScavenger = new(0.9f, 0.25f, 0.15f);  // Bright red
        public static readonly Color EnemyBrute = new(0.7f, 0.12f, 0.1f);       // Dark crimson
        public static readonly Color EnemySwarm = new(0.95f, 0.4f, 0.1f);       // Orange-red
        public static readonly Color EnemyGhost = new(0.8f, 0.15f, 0.35f);      // Magenta-red

        // ── Material Factories ──

        public static StandardMaterial3D MakeGroundMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = GroundBase;
            mat.Metallic = 0.3f;
            mat.Roughness = 0.85f;
            return mat;
        }

        public static StandardMaterial3D MakeGridLineMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(GridCyan.R, GridCyan.G, GridCyan.B, 0.6f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = GridCyan;
            mat.EmissionEnergyMultiplier = 1.5f;
            return mat;
        }

        public static StandardMaterial3D MakeWallBodyMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = WallBase;
            mat.Metallic = 0.4f;
            mat.Roughness = 0.8f;
            return mat;
        }

        public static StandardMaterial3D MakeWallEdgeMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = GridCyan;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = GridCyan;
            mat.EmissionEnergyMultiplier = 1.2f;
            return mat;
        }

        /// <summary>
        /// Recursively override all mesh materials on a node tree to Tron dark body + cyan edges.
        /// Use this on KitBash/imported assets so they match the Tron planet style.
        /// </summary>
        public static void TronifyNode(Node3D root)
        {
            var bodyMat = new StandardMaterial3D();
            bodyMat.AlbedoColor = WallBase;
            bodyMat.Metallic = 0.5f;
            bodyMat.Roughness = 0.7f;

            TronifyRecursive(root, bodyMat);
        }

        private static void TronifyRecursive(Node node, StandardMaterial3D bodyMat)
        {
            if (node is MeshInstance3D mesh)
            {
                // Only override material — do NOT add wireframe children to imported scenes,
                // as modifying imported scene tree structure causes native crashes.
                mesh.MaterialOverride = bodyMat;
            }

            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    TronifyRecursive(n, bodyMat);
            }
        }

        public static StandardMaterial3D MakeEntryMarkerMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = EntryTeal;
            mat.EmissionEnabled = true;
            mat.Emission = EntryTeal;
            mat.EmissionEnergyMultiplier = 0.5f;
            return mat;
        }

        public static StandardMaterial3D MakeExitMarkerMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = ExitRed;
            mat.EmissionEnabled = true;
            mat.Emission = ExitRed;
            mat.EmissionEnergyMultiplier = 0.8f;
            return mat;
        }

        public static StandardMaterial3D MakePlanetMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = PlanetBase;
            mat.Roughness = 0.9f;
            mat.Metallic = 0.2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            return mat;
        }

        public static StandardMaterial3D MakePlanetGridMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(GridCyan.R, GridCyan.G, GridCyan.B, 0.5f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = GridCyan;
            mat.EmissionEnergyMultiplier = 1.5f;
            return mat;
        }

        public static StandardMaterial3D MakeSurfaceGroundMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = GroundBase;
            mat.Metallic = 0.35f;
            mat.Roughness = 0.8f;
            return mat;
        }

        public static StandardMaterial3D MakePillarBodyMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = PillarBase;
            mat.Metallic = 0.4f;
            mat.Roughness = 0.8f;
            return mat;
        }

        public static StandardMaterial3D MakeCliffMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = CliffBase;
            mat.Metallic = 0.35f;
            mat.Roughness = 0.85f;
            return mat;
        }

        public static StandardMaterial3D MakeHorizonMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = HorizonSilhouette;
            mat.Roughness = 1f;
            mat.Metallic = 0f;
            return mat;
        }

        public static StandardMaterial3D MakeHazeMaterial(float alpha)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(FogColor.R, FogColor.G, FogColor.B, alpha);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            return mat;
        }

        /// <summary>
        /// Extended ground plane for the wider planet surface around the battle grid.
        /// Slightly different shade from the playable area to subtly delineate bounds.
        /// </summary>
        public static StandardMaterial3D MakeExtendedGroundMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.015f, 0.015f, 0.03f);
            mat.Metallic = 0.25f;
            mat.Roughness = 0.9f;
            return mat;
        }

        /// <summary>
        /// Dim grid line material for the outer/background grid (less prominent than battle grid).
        /// </summary>
        public static StandardMaterial3D MakeOuterGridLineMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(GridCyan.R, GridCyan.G, GridCyan.B, 0.15f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = GridCyan;
            mat.EmissionEnergyMultiplier = 0.4f;
            return mat;
        }

        /// <summary>
        /// Flat-topped cylindrical mesa with wireframe ring edges.
        /// </summary>
        public static Node3D MakeMesa(float radius, float height, int sides = 12)
        {
            var root = new Node3D();

            var body = new MeshInstance3D();
            var cyl = new CylinderMesh();
            cyl.TopRadius = radius;
            cyl.BottomRadius = radius * 1.15f;
            cyl.Height = height;
            cyl.RadialSegments = sides;
            body.Mesh = cyl;
            body.Position = new Vector3(0, height / 2f, 0);
            body.MaterialOverride = MakeCliffMaterial();
            root.AddChild(body);

            AddCylinderWireframe(root, radius, radius * 1.15f, height, sides);
            return root;
        }

        /// <summary>
        /// Tall thin hexagonal spire with wireframe edges.
        /// </summary>
        public static Node3D MakeSpire(float height, float radius, int sides = 6)
        {
            var root = new Node3D();

            var body = new MeshInstance3D();
            var cyl = new CylinderMesh();
            cyl.TopRadius = radius * 0.4f;
            cyl.BottomRadius = radius;
            cyl.Height = height;
            cyl.RadialSegments = sides;
            body.Mesh = cyl;
            body.Position = new Vector3(0, height / 2f, 0);
            body.MaterialOverride = MakeCliffMaterial();
            root.AddChild(body);

            AddCylinderWireframe(root, radius * 0.4f, radius, height, sides);
            return root;
        }

        /// <summary>
        /// Stepped mesa — stacked cylinders of decreasing radius for a terraced look.
        /// </summary>
        public static Node3D MakeSteppedMesa(float baseRadius, float totalHeight, int steps)
        {
            var root = new Node3D();
            float stepH = totalHeight / steps;

            for (int i = 0; i < steps; i++)
            {
                float r = baseRadius * (1f - 0.2f * i);
                var body = new MeshInstance3D();
                var cyl = new CylinderMesh();
                cyl.TopRadius = r;
                cyl.BottomRadius = r;
                cyl.Height = stepH;
                cyl.RadialSegments = 8;
                body.Mesh = cyl;
                body.Position = new Vector3(0, stepH * i + stepH / 2f, 0);
                body.MaterialOverride = MakeCliffMaterial();
                root.AddChild(body);

                AddCylinderWireframe(root, r, r, stepH, 8, stepH * i);
            }
            return root;
        }

        /// <summary>
        /// Long narrow ridge — the one shape where a box makes sense (geological ridge).
        /// </summary>
        public static Node3D MakeRidge(float length, float height, float depth)
        {
            var root = new Node3D();
            var size = new Vector3(length, height, depth);

            var body = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = size;
            body.Mesh = box;
            body.Position = new Vector3(0, height / 2f, 0);
            body.MaterialOverride = MakeCliffMaterial();
            root.AddChild(body);

            var wireParent = new Node3D();
            wireParent.Position = new Vector3(0, height / 2f, 0);
            root.AddChild(wireParent);
            AddWireframeEdges(wireParent, size);

            return root;
        }

        /// <summary>
        /// Rounded hill — sphere half-buried in the ground with latitude wireframe rings.
        /// </summary>
        public static Node3D MakeHill(float radius)
        {
            var root = new Node3D();

            var body = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = radius;
            sphere.Height = radius * 2f;
            sphere.RadialSegments = 16;
            sphere.Rings = 8;
            body.Mesh = sphere;
            body.Position = new Vector3(0, radius * 0.3f, 0); // ~70% buried
            body.MaterialOverride = MakeCliffMaterial();
            root.AddChild(body);

            // Wireframe rings at exposed latitudes
            var wire = new MeshInstance3D();
            var im = new ImmediateMesh();
            wire.Mesh = im;
            wire.Position = body.Position;
            wire.MaterialOverride = MakeWallEdgeMaterial();

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            int ringCount = 4;
            int segs = 24;
            for (int r = 1; r <= ringCount; r++)
            {
                float phi = Mathf.Pi * 0.15f * r; // Only upper hemisphere rings
                float y = Mathf.Cos(phi) * radius;
                float ringR = Mathf.Sin(phi) * radius;

                for (int j = 0; j < segs; j++)
                {
                    float t0 = Mathf.Tau * j / segs;
                    float t1 = Mathf.Tau * (j + 1) / segs;
                    im.SurfaceAddVertex(new Vector3(ringR * Mathf.Cos(t0), y, ringR * Mathf.Sin(t0)));
                    im.SurfaceAddVertex(new Vector3(ringR * Mathf.Cos(t1), y, ringR * Mathf.Sin(t1)));
                }
            }
            // Equator ring
            for (int j = 0; j < segs; j++)
            {
                float t0 = Mathf.Tau * j / segs;
                float t1 = Mathf.Tau * (j + 1) / segs;
                im.SurfaceAddVertex(new Vector3(radius * Mathf.Cos(t0), 0, radius * Mathf.Sin(t0)));
                im.SurfaceAddVertex(new Vector3(radius * Mathf.Cos(t1), 0, radius * Mathf.Sin(t1)));
            }
            im.SurfaceEnd();
            root.AddChild(wire);

            return root;
        }

        /// <summary>
        /// Peaked mountain — prism shape (triangular cross-section) with wireframe.
        /// </summary>
        public static Node3D MakePeak(float width, float height, float depth)
        {
            var root = new Node3D();

            var body = new MeshInstance3D();
            var prism = new PrismMesh();
            prism.Size = new Vector3(width, height, depth);
            body.Mesh = prism;
            body.Position = new Vector3(0, height / 2f, 0);
            body.MaterialOverride = MakeCliffMaterial();
            root.AddChild(body);

            // Wireframe for prism: 2 triangular faces + 3 connecting edges
            var wire = new MeshInstance3D();
            var im = new ImmediateMesh();
            wire.Mesh = im;
            wire.Position = body.Position;
            wire.MaterialOverride = MakeWallEdgeMaterial();

            float hx = width / 2f, hy = height / 2f, hz = depth / 2f;
            // Prism vertices: top ridge, bottom-left, bottom-right on both z-faces
            var v = new Vector3[] {
                new(0, hy, -hz),        // 0: top front
                new(-hx, -hy, -hz),     // 1: bottom-left front
                new(hx, -hy, -hz),      // 2: bottom-right front
                new(0, hy, hz),         // 3: top back
                new(-hx, -hy, hz),      // 4: bottom-left back
                new(hx, -hy, hz),       // 5: bottom-right back
            };

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            // Front triangle
            im.SurfaceAddVertex(v[0]); im.SurfaceAddVertex(v[1]);
            im.SurfaceAddVertex(v[1]); im.SurfaceAddVertex(v[2]);
            im.SurfaceAddVertex(v[2]); im.SurfaceAddVertex(v[0]);
            // Back triangle
            im.SurfaceAddVertex(v[3]); im.SurfaceAddVertex(v[4]);
            im.SurfaceAddVertex(v[4]); im.SurfaceAddVertex(v[5]);
            im.SurfaceAddVertex(v[5]); im.SurfaceAddVertex(v[3]);
            // Connecting edges
            im.SurfaceAddVertex(v[0]); im.SurfaceAddVertex(v[3]);
            im.SurfaceAddVertex(v[1]); im.SurfaceAddVertex(v[4]);
            im.SurfaceAddVertex(v[2]); im.SurfaceAddVertex(v[5]);
            im.SurfaceEnd();

            root.AddChild(wire);
            return root;
        }

        /// <summary>
        /// Draw wireframe rings for a cylinder (top ring + bottom ring + vertical struts).
        /// </summary>
        public static void AddCylinderWireframe(Node3D parent, float topR, float botR, float height, int sides, float yOffset = 0f)
        {
            var wire = new MeshInstance3D();
            var im = new ImmediateMesh();
            wire.Mesh = im;
            wire.MaterialOverride = MakeWallEdgeMaterial();

            float halfH = height / 2f;
            float yCenter = yOffset + halfH;
            wire.Position = new Vector3(0, yCenter, 0);

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);

            // Top ring
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.Tau * i / sides;
                float a1 = Mathf.Tau * (i + 1) / sides;
                im.SurfaceAddVertex(new Vector3(topR * Mathf.Cos(a0), halfH, topR * Mathf.Sin(a0)));
                im.SurfaceAddVertex(new Vector3(topR * Mathf.Cos(a1), halfH, topR * Mathf.Sin(a1)));
            }

            // Bottom ring
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.Tau * i / sides;
                float a1 = Mathf.Tau * (i + 1) / sides;
                im.SurfaceAddVertex(new Vector3(botR * Mathf.Cos(a0), -halfH, botR * Mathf.Sin(a0)));
                im.SurfaceAddVertex(new Vector3(botR * Mathf.Cos(a1), -halfH, botR * Mathf.Sin(a1)));
            }

            // Vertical struts
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.Tau * i / sides;
                im.SurfaceAddVertex(new Vector3(topR * Mathf.Cos(a), halfH, topR * Mathf.Sin(a)));
                im.SurfaceAddVertex(new Vector3(botR * Mathf.Cos(a), -halfH, botR * Mathf.Sin(a)));
            }

            im.SurfaceEnd();
            parent.AddChild(wire);
        }

        // Fog body shader — uses INSTANCE_CUSTOM for per-instance phase.
        // Drifts with breeze + fades in/out.
        private const string FogBodyShader = @"
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled;

uniform float drift_speed = 0.4;
uniform float bank_phase = 0.0;

void vertex() {
    float phase = INSTANCE_CUSTOM.x;
    float t = TIME * drift_speed + phase;
    VERTEX.x += t * 0.8 - floor(t * 0.8 / 40.0) * 40.0 - 20.0;
    VERTEX.z += sin(t * 0.3) * 0.5;
    VERTEX.y += sin(t * 0.5) * 0.15;
}

void fragment() {
    float phase = INSTANCE_CUSTOM.x;
    float speed = INSTANCE_CUSTOM.y;
    float wave = sin(TIME * speed + phase) * 0.5 + 0.5;
    ALBEDO = vec3(0.01, 0.01, 0.02);
    ALPHA = mix(0.0, 0.5, wave);
}
";

        // Fog edge shader — uses uniforms (not INSTANCE_CUSTOM) since edges
        // are a single ImmediateMesh, not a MultiMesh.
        private const string FogEdgeShader = @"
shader_type spatial;
render_mode unshaded, blend_mix;

uniform float drift_speed = 0.4;
uniform float bank_phase = 0.0;
uniform float fade_speed = 0.7;

void vertex() {
    float t = TIME * drift_speed + bank_phase;
    VERTEX.x += t * 0.8 - floor(t * 0.8 / 40.0) * 40.0 - 20.0;
    VERTEX.z += sin(t * 0.3) * 0.5;
    VERTEX.y += sin(t * 0.5) * 0.15;
}

void fragment() {
    float wave = sin(TIME * fade_speed + bank_phase) * 0.5 + 0.5;
    vec3 cyan = vec3(0.0, 0.85, 0.95);
    ALBEDO = cyan;
    EMISSION = cyan * 0.8;
    ALPHA = mix(0.05, 0.45, wave);
}
";

        // Cached shader resources (created once, reused)
        private static Shader _fogBodyShader;
        private static Shader _fogEdgeShader;

        private static Shader GetFogBodyShader()
        {
            if (_fogBodyShader == null)
            {
                _fogBodyShader = new Shader();
                _fogBodyShader.Code = FogBodyShader;
            }
            return _fogBodyShader;
        }

        private static Shader GetFogEdgeShader()
        {
            if (_fogEdgeShader == null)
            {
                _fogEdgeShader = new Shader();
                _fogEdgeShader.Code = FogEdgeShader;
            }
            return _fogEdgeShader;
        }

        /// <summary>
        /// Builds a fog cloud using MultiMeshInstance3D (1 draw call for bodies)
        /// + a single ImmediateMesh for all wireframe edges.
        /// Only 3 nodes per bank instead of thousands.
        /// </summary>
        public static Node3D MakeFogBank(RandomNumberGenerator rng, Vector3 center, int cubeCount, float spread, float cubeSize)
        {
            var bank = new Node3D();
            bank.Position = center;
            float bankPhase = rng.RandfRange(0, Mathf.Tau);

            // ── MultiMesh for cube bodies (1 draw call) ──
            var boxMesh = new BoxMesh();
            boxMesh.Size = new Vector3(cubeSize, cubeSize, cubeSize);

            var mm = new MultiMesh();
            mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            mm.UseCustomData = true;
            mm.Mesh = boxMesh;
            mm.InstanceCount = cubeCount;

            // ── Single ImmediateMesh for all wireframe edges ──
            var edgeMesh = new ImmediateMesh();
            edgeMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

            for (int i = 0; i < cubeCount; i++)
            {
                float cubePhase = bankPhase + rng.RandfRange(-1.5f, 1.5f);
                float cubeSpeed = rng.RandfRange(0.5f, 1.0f);

                float s = cubeSize * rng.RandfRange(0.4f, 1.2f);
                float sy = s * rng.RandfRange(0.3f, 0.8f);

                float ox = rng.RandfRange(-spread, spread) * rng.RandfRange(0.2f, 1.0f);
                float oz = rng.RandfRange(-spread, spread) * rng.RandfRange(0.2f, 1.0f);
                float oy = rng.RandfRange(-spread * 0.2f, spread * 0.6f) * rng.RandfRange(0.2f, 1.0f);

                // Set instance transform (position + scale)
                var xform = new Transform3D(
                    new Vector3(s, 0, 0),
                    new Vector3(0, sy, 0),
                    new Vector3(0, 0, s),
                    new Vector3(ox, oy, oz));
                mm.SetInstanceTransform(i, xform);

                // Pack phase + speed into custom data for shader
                mm.SetInstanceCustomData(i, new Color(cubePhase, cubeSpeed, 0, 0));

                // Add wireframe edges for this cube into the shared mesh
                float hx = s / 2, hy = sy / 2, hz = s / 2;
                Vector3 p = new(ox, oy, oz);
                Vector3[] c = {
                    p + new Vector3(-hx, -hy, -hz), p + new Vector3( hx, -hy, -hz),
                    p + new Vector3( hx, -hy,  hz), p + new Vector3(-hx, -hy,  hz),
                    p + new Vector3(-hx,  hy, -hz), p + new Vector3( hx,  hy, -hz),
                    p + new Vector3( hx,  hy,  hz), p + new Vector3(-hx,  hy,  hz)
                };
                int[,] ei = {
                    {0,1},{1,2},{2,3},{3,0},
                    {4,5},{5,6},{6,7},{7,4},
                    {0,4},{1,5},{2,6},{3,7}
                };
                for (int e = 0; e < 12; e++)
                {
                    edgeMesh.SurfaceAddVertex(c[ei[e, 0]]);
                    edgeMesh.SurfaceAddVertex(c[ei[e, 1]]);
                }
            }

            edgeMesh.SurfaceEnd();

            // Body MultiMeshInstance
            var bodyInst = new MultiMeshInstance3D();
            bodyInst.Multimesh = mm;
            var bodyMat = new ShaderMaterial();
            bodyMat.Shader = GetFogBodyShader();
            bodyMat.SetShaderParameter("bank_phase", bankPhase);
            bodyInst.MaterialOverride = bodyMat;
            bank.AddChild(bodyInst);

            // Edge wireframe instance — uses uniforms for phase (not INSTANCE_CUSTOM)
            var edgeInst = new MeshInstance3D();
            edgeInst.Mesh = edgeMesh;
            var edgeMat = new ShaderMaterial();
            edgeMat.Shader = GetFogEdgeShader();
            edgeMat.SetShaderParameter("bank_phase", bankPhase);
            edgeMat.SetShaderParameter("fade_speed", 0.7f);
            edgeInst.MaterialOverride = edgeMat;
            bank.AddChild(edgeInst);

            return bank;
        }

        /// <summary>
        /// Draw the 12 edges of a box as ImmediateMesh lines in cyan emissive.
        /// Adds a MeshInstance3D child to the parent.
        /// </summary>
        public static void AddWireframeEdges(Node3D parent, Vector3 size)
        {
            var wireframe = new MeshInstance3D();
            var im = new ImmediateMesh();
            wireframe.Mesh = im;
            wireframe.MaterialOverride = MakeWallEdgeMaterial();

            float hx = size.X / 2f;
            float hy = size.Y / 2f;
            float hz = size.Z / 2f;

            // 8 corners of the box
            var corners = new Vector3[] {
                new(-hx, -hy, -hz), new( hx, -hy, -hz),
                new( hx, -hy,  hz), new(-hx, -hy,  hz),
                new(-hx,  hy, -hz), new( hx,  hy, -hz),
                new( hx,  hy,  hz), new(-hx,  hy,  hz),
            };

            // 12 edges: bottom 4, top 4, vertical 4
            int[,] edges = {
                {0,1}, {1,2}, {2,3}, {3,0},  // bottom
                {4,5}, {5,6}, {6,7}, {7,4},  // top
                {0,4}, {1,5}, {2,6}, {3,7},  // verticals
            };

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            for (int i = 0; i < 12; i++)
            {
                im.SurfaceAddVertex(corners[edges[i, 0]]);
                im.SurfaceAddVertex(corners[edges[i, 1]]);
            }
            im.SurfaceEnd();

            parent.AddChild(wireframe);
        }

        /// <summary>
        /// Draw latitude/longitude grid on a sphere as ImmediateMesh lines.
        /// Used for the planet-from-orbit Tron globe effect.
        /// </summary>
        public static MeshInstance3D MakeSphereGrid(float radius, int latRings, int lonArcs, int segments)
        {
            var gridMesh = new MeshInstance3D();
            var im = new ImmediateMesh();
            gridMesh.Mesh = im;
            gridMesh.MaterialOverride = MakePlanetGridMaterial();

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);

            // Latitude rings
            for (int i = 1; i < latRings; i++)
            {
                float phi = Mathf.Pi * i / latRings;
                float y = Mathf.Cos(phi) * radius;
                float r = Mathf.Sin(phi) * radius;

                for (int j = 0; j < segments; j++)
                {
                    float theta0 = Mathf.Tau * j / segments;
                    float theta1 = Mathf.Tau * (j + 1) / segments;
                    im.SurfaceAddVertex(new Vector3(r * Mathf.Cos(theta0), y, r * Mathf.Sin(theta0)));
                    im.SurfaceAddVertex(new Vector3(r * Mathf.Cos(theta1), y, r * Mathf.Sin(theta1)));
                }
            }

            // Longitude arcs
            for (int i = 0; i < lonArcs; i++)
            {
                float theta = Mathf.Tau * i / lonArcs;
                for (int j = 0; j < segments; j++)
                {
                    float phi0 = Mathf.Pi * j / segments;
                    float phi1 = Mathf.Pi * (j + 1) / segments;
                    float y0 = Mathf.Cos(phi0) * radius;
                    float r0 = Mathf.Sin(phi0) * radius;
                    float y1 = Mathf.Cos(phi1) * radius;
                    float r1 = Mathf.Sin(phi1) * radius;
                    im.SurfaceAddVertex(new Vector3(r0 * Mathf.Cos(theta), y0, r0 * Mathf.Sin(theta)));
                    im.SurfaceAddVertex(new Vector3(r1 * Mathf.Cos(theta), y1, r1 * Mathf.Sin(theta)));
                }
            }

            im.SurfaceEnd();
            return gridMesh;
        }

        /// <summary>
        /// Build a Tron data pillar: thin dark box with cyan wireframe edges.
        /// </summary>
        public static Node3D MakeDataPillar(float height, float width)
        {
            var pillar = new Node3D();
            var size = new Vector3(width, height, width);

            var body = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = size;
            body.Mesh = box;
            body.Position = new Vector3(0, height / 2f, 0);
            body.MaterialOverride = MakePillarBodyMaterial();
            pillar.AddChild(body);

            // Wireframe edges offset to match body position
            var wireParent = new Node3D();
            wireParent.Position = new Vector3(0, height / 2f, 0);
            pillar.AddChild(wireParent);
            AddWireframeEdges(wireParent, size);

            return pillar;
        }

        /// <summary>
        /// Build surface grid lines (ImmediateMesh) for the planet surface scene.
        /// </summary>
        public static MeshInstance3D MakeSurfaceGrid(float areaSize, float spacing)
        {
            var gridVisual = new MeshInstance3D();
            var im = new ImmediateMesh();
            gridVisual.Mesh = im;
            gridVisual.Position = new Vector3(0, 0.02f, 0);
            gridVisual.MaterialOverride = MakeGridLineMaterial();

            float half = areaSize / 2f;
            int lines = (int)(areaSize / spacing) + 1;

            im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            for (int i = 0; i < lines; i++)
            {
                float offset = -half + i * spacing;
                // X-parallel lines
                im.SurfaceAddVertex(new Vector3(-half, 0, offset));
                im.SurfaceAddVertex(new Vector3(half, 0, offset));
                // Z-parallel lines
                im.SurfaceAddVertex(new Vector3(offset, 0, -half));
                im.SurfaceAddVertex(new Vector3(offset, 0, half));
            }
            im.SurfaceEnd();

            return gridVisual;
        }
    }
}
