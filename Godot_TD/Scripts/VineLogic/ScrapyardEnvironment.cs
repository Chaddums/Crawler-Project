using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Scrapyard planet environment — industrial junkyard with real PBR textures.
    /// No wireframes, no outlines. Solid opaque rusted metal, concrete, debris.
    /// Smokestacks, scrap piles, welded metal structures.
    /// </summary>
    public static class ScrapyardEnvironment
    {
        private static readonly RandomNumberGenerator _rng = new();

        // ── PBR Material Paths ──
        private const string TEX_RUSTED_METAL = "res://Materials/Scrapyard/rusted_metal_plate_smsqo0n_2k/";
        private const string TEX_INDUSTRIAL_RUBBLE = "res://Materials/Scrapyard/industrial_rubble_slxnyfd_2k/";
        private const string TEX_DAMAGED_CONCRETE = "res://Materials/Scrapyard/damaged_concrete_tbqmedor_2k/";
        private const string TEX_CONCRETE_CRACK = "res://Materials/Scrapyard/concrete_crack_sdokhyi_2k/";
        private const string TEX_GARBAGE_PILE = "res://Materials/Scrapyard/garbage_pile_shlr1sh_2k/";
        private const string TEX_GROUND = "res://Materials/Scrapyard/Textures/";
        private const string TEX_METAL = "res://Materials/Scrapyard/Textures/";

        // ── Cached Materials ──
        private static StandardMaterial3D _groundMat;
        private static StandardMaterial3D _rustedMetalMat;
        private static StandardMaterial3D _concreteMat;
        private static StandardMaterial3D _darkMetalMat;
        private static StandardMaterial3D _warmGlowMat;
        private static StandardMaterial3D _garbagePileMat;

        /// <summary>
        /// Build the full scrapyard ground plane with industrial texture.
        /// </summary>
        public static StandardMaterial3D GetGroundMaterial()
        {
            if (_groundMat != null) return _groundMat;

            _groundMat = new StandardMaterial3D();
            // Try loading PBR textures
            var baseColor = TryLoadTexture(TEX_GROUND + "Ground031_2K-PNG_Color.png");
            var normal = TryLoadTexture(TEX_GROUND + "Ground031_2K-PNG_NormalGL.png");
            var roughness = TryLoadTexture(TEX_GROUND + "Ground031_2K-PNG_Roughness.png");

            if (baseColor != null)
            {
                _groundMat.AlbedoTexture = baseColor;
                if (normal != null) { _groundMat.NormalEnabled = true; _groundMat.NormalTexture = normal; }
                if (roughness != null) _groundMat.RoughnessTexture = roughness;
                _groundMat.Uv1Scale = new Vector3(8, 8, 8); // Tile the texture
            }
            else
            {
                // Fallback: procedural brown
                _groundMat.AlbedoColor = new Color(0.12f, 0.09f, 0.07f);
                _groundMat.Roughness = 0.95f;
            }
            return _groundMat;
        }

        public static StandardMaterial3D GetRustedMetalMaterial()
        {
            if (_rustedMetalMat != null) return _rustedMetalMat;

            _rustedMetalMat = new StandardMaterial3D();
            var baseColor = TryLoadTexture(TEX_RUSTED_METAL + "Rusted_Metal_Plate_smsqo0n_2K_BaseColor.jpg");
            var normal = TryLoadTexture(TEX_RUSTED_METAL + "Rusted_Metal_Plate_smsqo0n_2K_Normal.jpg");
            var roughness = TryLoadTexture(TEX_RUSTED_METAL + "Rusted_Metal_Plate_smsqo0n_2K_Roughness.jpg");
            var metallic = TryLoadTexture(TEX_RUSTED_METAL + "Rusted_Metal_Plate_smsqo0n_2K_Specular.jpg");
            var ao = TryLoadTexture(TEX_RUSTED_METAL + "Rusted_Metal_Plate_smsqo0n_2K_AO.jpg");

            if (baseColor != null)
            {
                _rustedMetalMat.AlbedoTexture = baseColor;
                if (normal != null) { _rustedMetalMat.NormalEnabled = true; _rustedMetalMat.NormalTexture = normal; }
                if (roughness != null) _rustedMetalMat.RoughnessTexture = roughness;
                if (metallic != null) { _rustedMetalMat.MetallicTexture = metallic; _rustedMetalMat.Metallic = 1f; }
                // AO handled via ORM texture in Godot 4.6
                _rustedMetalMat.Uv1Scale = new Vector3(2, 2, 2);
            }
            else
            {
                _rustedMetalMat.AlbedoColor = new Color(0.35f, 0.2f, 0.12f);
                _rustedMetalMat.Roughness = 0.85f;
                _rustedMetalMat.Metallic = 0.5f;
            }
            return _rustedMetalMat;
        }

        public static StandardMaterial3D GetConcreteMaterial()
        {
            if (_concreteMat != null) return _concreteMat;

            _concreteMat = new StandardMaterial3D();
            var baseColor = TryLoadTexture(TEX_DAMAGED_CONCRETE + "Damaged_Concrete_tbqmedor_2K_BaseColor.jpg");
            var normal = TryLoadTexture(TEX_DAMAGED_CONCRETE + "Damaged_Concrete_tbqmedor_2K_Normal.jpg");
            var roughness = TryLoadTexture(TEX_DAMAGED_CONCRETE + "Damaged_Concrete_tbqmedor_2K_Roughness.jpg");

            if (baseColor != null)
            {
                _concreteMat.AlbedoTexture = baseColor;
                if (normal != null) { _concreteMat.NormalEnabled = true; _concreteMat.NormalTexture = normal; }
                if (roughness != null) _concreteMat.RoughnessTexture = roughness;
                _concreteMat.Uv1Scale = new Vector3(3, 3, 3);
            }
            else
            {
                _concreteMat.AlbedoColor = new Color(0.25f, 0.22f, 0.2f);
                _concreteMat.Roughness = 0.9f;
            }
            return _concreteMat;
        }

        public static StandardMaterial3D GetDarkMetalMaterial()
        {
            if (_darkMetalMat != null) return _darkMetalMat;

            _darkMetalMat = new StandardMaterial3D();
            var baseColor = TryLoadTexture(TEX_METAL + "Metal042A_2K-PNG_Color.png");  // AmbientCG uses .png
            var normal = TryLoadTexture(TEX_METAL + "Metal042A_2K-PNG_NormalGL.png");
            var roughness = TryLoadTexture(TEX_METAL + "Metal042A_2K-PNG_Roughness.png");
            var metallic = TryLoadTexture(TEX_METAL + "Metal042A_2K-PNG_Metalness.png");

            if (baseColor != null)
            {
                _darkMetalMat.AlbedoTexture = baseColor;
                if (normal != null) { _darkMetalMat.NormalEnabled = true; _darkMetalMat.NormalTexture = normal; }
                if (roughness != null) _darkMetalMat.RoughnessTexture = roughness;
                if (metallic != null) { _darkMetalMat.MetallicTexture = metallic; _darkMetalMat.Metallic = 1f; }
                _darkMetalMat.Uv1Scale = new Vector3(2, 2, 2);
            }
            else
            {
                _darkMetalMat.AlbedoColor = new Color(0.15f, 0.13f, 0.12f);
                _darkMetalMat.Roughness = 0.7f;
                _darkMetalMat.Metallic = 0.6f;
            }
            return _darkMetalMat;
        }

        /// <summary>
        /// Warm amber glow material for lights, vents, hot spots.
        /// </summary>
        public static StandardMaterial3D GetWarmGlowMaterial()
        {
            if (_warmGlowMat != null) return _warmGlowMat;

            _warmGlowMat = new StandardMaterial3D();
            _warmGlowMat.AlbedoColor = new Color(0.9f, 0.5f, 0.1f);
            _warmGlowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _warmGlowMat.EmissionEnabled = true;
            _warmGlowMat.Emission = new Color(0.9f, 0.5f, 0.1f);
            _warmGlowMat.EmissionEnergyMultiplier = 0.6f;
            return _warmGlowMat;
        }

        public static StandardMaterial3D GetGarbagePileMaterial()
        {
            if (_garbagePileMat != null) return _garbagePileMat;

            _garbagePileMat = new StandardMaterial3D();
            var baseColor = TryLoadTexture(TEX_GARBAGE_PILE + "Garbage_Pile_shlr1sh_2K_BaseColor.jpg");
            var normal = TryLoadTexture(TEX_GARBAGE_PILE + "Garbage_Pile_shlr1sh_2K_Normal.jpg");
            var roughness = TryLoadTexture(TEX_GARBAGE_PILE + "Garbage_Pile_shlr1sh_2K_Roughness.jpg");
            var ao = TryLoadTexture(TEX_GARBAGE_PILE + "Garbage_Pile_shlr1sh_2K_AO.jpg");

            if (baseColor != null)
            {
                _garbagePileMat.AlbedoTexture = baseColor;
                if (normal != null) { _garbagePileMat.NormalEnabled = true; _garbagePileMat.NormalTexture = normal; }
                if (roughness != null) _garbagePileMat.RoughnessTexture = roughness;
                _garbagePileMat.Uv1Scale = new Vector3(2, 2, 2);
            }
            else
            {
                _garbagePileMat.AlbedoColor = new Color(0.2f, 0.18f, 0.12f);
                _garbagePileMat.Roughness = 0.95f;
            }
            return _garbagePileMat;
        }

        // ── Environment Building ──

        /// <summary>
        /// Build the complete scrapyard environment around the battle grid.
        /// </summary>
        public static void BuildEnvironment(Node3D parent, float gridW, float gridH)
        {
            float cx = gridW / 2f;
            float cz = gridH / 2f;

            GD.Print("[Scrapyard] Building ground...");
            BuildGround(parent, cx, cz);

            GD.Print("[Scrapyard] Building scrap ring...");
            BuildScrapRing(parent, cx, cz, gridW, gridH);

            GD.Print("[Scrapyard] Building structures...");
            BuildStructures(parent, cx, cz, gridW, gridH);

            GD.Print("[Scrapyard] Building atmosphere...");
            BuildAtmosphere(parent, cx, cz);

            GD.Print("[Scrapyard] Environment complete.");
        }

        private static void BuildGround(Node3D parent, float cx, float cz)
        {
            // Main ground — large textured plane
            var ground = new MeshInstance3D();
            var groundMesh = new PlaneMesh();
            groundMesh.Size = new Vector2(200, 200);
            ground.Mesh = groundMesh;
            ground.Position = new Vector3(cx, -0.05f, cz);
            ground.MaterialOverride = GetGroundMaterial();
            parent.AddChild(ground);

            // Scattered debris on the ground around the grid
            for (int i = 0; i < 30; i++)
            {
                float angle = _rng.RandfRange(0, Mathf.Tau);
                float dist = _rng.RandfRange(5f, 80f);
                float px = cx + Mathf.Cos(angle) * dist;
                float pz = cz + Mathf.Sin(angle) * dist;

                var debris = new MeshInstance3D();
                float s = _rng.RandfRange(0.2f, 0.8f);
                debris.Mesh = new BoxMesh { Size = new Vector3(s, s * 0.3f, s * _rng.RandfRange(0.5f, 1.5f)) };
                debris.Position = new Vector3(px, s * 0.1f, pz);
                debris.RotationDegrees = new Vector3(_rng.RandfRange(-10, 10), _rng.RandfRange(0, 360), _rng.RandfRange(-5, 5));
                debris.MaterialOverride = GetRustedMetalMaterial();
                parent.AddChild(debris);
            }
        }

        private static void BuildScrapRing(Node3D parent, float cx, float cz, float gridW, float gridH)
        {
            float margin = 5f;
            float depth = 45f;

            for (float angle = 0; angle < Mathf.Tau; angle += 0.06f)
            {
                for (float dist = margin; dist < depth; dist += _rng.RandfRange(2.5f, 5f))
                {
                    float rawX = cx + Mathf.Cos(angle) * (gridW / 2f + dist);
                    float rawZ = cz + Mathf.Sin(angle) * (gridH / 2f + dist);

                    // Jitter
                    rawX += _rng.RandfRange(-2f, 2f);
                    rawZ += _rng.RandfRange(-2f, 2f);

                    float scale = 0.5f + dist * 0.06f;
                    float height = 0.5f + dist * 0.1f;

                    BuildScrapPiece(parent, rawX, rawZ, scale, height);
                }
            }
        }

        private static void BuildScrapPiece(Node3D parent, float x, float z, float scale, float height)
        {
            int variant = _rng.RandiRange(0, 7);
            switch (variant)
            {
                case 0: // Shipping container
                {
                    float h = _rng.RandfRange(1.5f, 3f) * height;
                    float w = _rng.RandfRange(1.5f, 3f) * scale;
                    float d = _rng.RandfRange(3f, 6f) * scale;
                    var container = MakeMesh(new BoxMesh { Size = new Vector3(w, h, d) }, GetRustedMetalMaterial());
                    container.Position = new Vector3(x, h / 2f, z);
                    container.RotationDegrees = new Vector3(_rng.RandfRange(-3, 3), _rng.RandfRange(0, 360), _rng.RandfRange(-3, 3));
                    parent.AddChild(container);
                    break;
                }

                case 1: // Pipe stack — cylinders bundled together
                {
                    int count = _rng.RandiRange(2, 4);
                    for (int i = 0; i < count; i++)
                    {
                        float r = _rng.RandfRange(0.2f, 0.5f) * scale;
                        float len = _rng.RandfRange(2f, 5f) * scale;
                        var pipe = MakeMesh(new CylinderMesh {
                            TopRadius = r, BottomRadius = r, Height = len },
                            GetDarkMetalMaterial());
                        pipe.Position = new Vector3(
                            x + _rng.RandfRange(-0.5f, 0.5f) * scale,
                            r + i * r * 1.5f,
                            z + _rng.RandfRange(-0.5f, 0.5f) * scale);
                        pipe.RotationDegrees = new Vector3(90, _rng.RandfRange(0, 30), 0);
                        parent.AddChild(pipe);
                    }
                    break;
                }

                case 2: // Metal scrap pile — multiple angled plates
                {
                    int count = _rng.RandiRange(3, 6);
                    for (int i = 0; i < count; i++)
                    {
                        float s = _rng.RandfRange(0.3f, 1f) * scale;
                        var plate = MakeMesh(new BoxMesh {
                            Size = new Vector3(s, _rng.RandfRange(0.05f, 0.15f), s * _rng.RandfRange(0.7f, 1.5f)) },
                            i % 2 == 0 ? GetRustedMetalMaterial() : GetDarkMetalMaterial());
                        plate.Position = new Vector3(
                            x + _rng.RandfRange(-1f, 1f) * scale,
                            _rng.RandfRange(0.1f, 0.8f) * height,
                            z + _rng.RandfRange(-1f, 1f) * scale);
                        plate.RotationDegrees = new Vector3(
                            _rng.RandfRange(-30, 30), _rng.RandfRange(0, 180), _rng.RandfRange(-20, 20));
                        parent.AddChild(plate);
                    }
                    break;
                }

                case 3: // Smokestack — tall cylinder with warm glow at top
                {
                    float h = _rng.RandfRange(3f, 8f) * height;
                    float r = _rng.RandfRange(0.3f, 0.6f) * scale;
                    var stack = MakeMesh(new CylinderMesh {
                        TopRadius = r * 0.8f, BottomRadius = r, Height = h },
                        GetDarkMetalMaterial());
                    stack.Position = new Vector3(x, h / 2f, z);
                    parent.AddChild(stack);

                    // Small fire glow at top + omni light
                    var glow = MakeMesh(new CylinderMesh {
                        TopRadius = r * 0.6f, BottomRadius = r * 0.8f, Height = 0.15f },
                        GetWarmGlowMaterial());
                    glow.Position = new Vector3(x, h, z);
                    parent.AddChild(glow);
                    var fireLight = new OmniLight3D();
                    fireLight.Position = new Vector3(x, h + 0.5f, z);
                    fireLight.LightColor = new Color(0.95f, 0.5f, 0.1f);
                    fireLight.LightEnergy = 0.4f;
                    fireLight.OmniRange = 3f;
                    fireLight.ShadowEnabled = false;
                    parent.AddChild(fireLight);
                    break;
                }

                case 4: // Concrete block — damaged foundation
                {
                    float h = _rng.RandfRange(0.5f, 2f) * height;
                    float w = _rng.RandfRange(1f, 3f) * scale;
                    var block = MakeMesh(new BoxMesh { Size = new Vector3(w, h, w * _rng.RandfRange(0.7f, 1.3f)) },
                        GetConcreteMaterial());
                    block.Position = new Vector3(x, h / 2f, z);
                    block.RotationDegrees = new Vector3(_rng.RandfRange(-5, 5), _rng.RandfRange(0, 90), _rng.RandfRange(-3, 3));
                    parent.AddChild(block);
                    break;
                }

                case 5: // Barrel cluster
                {
                    int count = _rng.RandiRange(2, 5);
                    for (int i = 0; i < count; i++)
                    {
                        float r = _rng.RandfRange(0.2f, 0.4f) * scale;
                        float h = _rng.RandfRange(0.6f, 1.2f) * scale;
                        var barrel = MakeMesh(new CylinderMesh {
                            TopRadius = r, BottomRadius = r, Height = h },
                            _rng.Randf() > 0.3f ? GetRustedMetalMaterial() : GetDarkMetalMaterial());
                        barrel.Position = new Vector3(
                            x + _rng.RandfRange(-1f, 1f) * scale,
                            h / 2f,
                            z + _rng.RandfRange(-1f, 1f) * scale);
                        if (_rng.Randf() > 0.7f) // Some barrels tipped over
                            barrel.RotationDegrees = new Vector3(85, _rng.RandfRange(0, 360), 0);
                        parent.AddChild(barrel);
                    }
                    break;
                }

                case 6: // Scaffolding frame — thin boxes forming a frame
                {
                    float h = _rng.RandfRange(2f, 5f) * height;
                    float w = _rng.RandfRange(1.5f, 3f) * scale;
                    float beamThick = 0.08f * scale;

                    // Vertical posts
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float ox = (corner % 2 == 0 ? -1 : 1) * w / 2f;
                        float oz = (corner < 2 ? -1 : 1) * w / 2f;
                        var post = MakeMesh(new BoxMesh { Size = new Vector3(beamThick, h, beamThick) },
                            GetDarkMetalMaterial());
                        post.Position = new Vector3(x + ox, h / 2f, z + oz);
                        parent.AddChild(post);
                    }
                    // Cross beam
                    var crossBeam = MakeMesh(new BoxMesh { Size = new Vector3(w, beamThick, beamThick) },
                        GetDarkMetalMaterial());
                    crossBeam.Position = new Vector3(x, h * 0.7f, z);
                    parent.AddChild(crossBeam);
                    break;
                }

                default: // Junk heap — mixed small boxes
                {
                    int count = _rng.RandiRange(4, 8);
                    for (int i = 0; i < count; i++)
                    {
                        float s = _rng.RandfRange(0.15f, 0.6f) * scale;
                        float pick = _rng.Randf();
                        var mat = pick < 0.33f ? GetRustedMetalMaterial()
                                : pick < 0.66f ? GetConcreteMaterial()
                                : GetGarbagePileMaterial();
                        var junk = MakeMesh(new BoxMesh {
                            Size = new Vector3(s, s * _rng.RandfRange(0.4f, 1.2f), s * _rng.RandfRange(0.5f, 1.3f)) },
                            mat);
                        junk.Position = new Vector3(
                            x + _rng.RandfRange(-1.5f, 1.5f) * scale,
                            s * 0.3f + i * 0.1f,
                            z + _rng.RandfRange(-1.5f, 1.5f) * scale);
                        junk.RotationDegrees = new Vector3(
                            _rng.RandfRange(-25, 25), _rng.RandfRange(0, 360), _rng.RandfRange(-15, 15));
                        parent.AddChild(junk);
                    }
                    break;
                }
            }
        }

        private static void BuildStructures(Node3D parent, float cx, float cz, float gridW, float gridH)
        {
            // Place KitBash buildings in the mid-distance with scrapyard materials
            var structures = new (string asset, Vector3 pos, float scale, float rotY)[] {
                (AssetLibrary.BLDG_OUTPOST,   new Vector3(cx - 35, 0, cz - 30), 0.25f, 15),
                (AssetLibrary.BLDG_FUEL_TANKS, new Vector3(cx + 38, 0, cz - 25), 0.2f, -20),
                (AssetLibrary.BLDG_BARRACKS,   new Vector3(cx - 30, 0, cz + 35), 0.22f, 40),
                (AssetLibrary.BLDG_TRENCH,     new Vector3(cx + 10, 0, cz - 38), 0.2f, 0),
            };

            foreach (var (asset, pos, scale, rotY) in structures)
            {
                var instance = AssetLibrary.Instantiate(asset);
                if (instance == null) continue;
                instance.Position = pos;
                instance.Scale = Vector3.One * scale;
                instance.RotationDegrees = new Vector3(0, rotY, 0);
                // Apply scrapyard theme — rust shader, not Tron outline
                PlanetTheme.Current.ApplyToNode(instance);
                parent.AddChild(instance);
            }
        }

        private static void BuildAtmosphere(Node3D parent, float cx, float cz)
        {
            // Warm amber haze planes at various heights
            for (int i = 0; i < 4; i++)
            {
                var haze = new MeshInstance3D();
                var plane = new PlaneMesh();
                plane.Size = new Vector2(120, 120);
                haze.Mesh = plane;
                haze.Position = new Vector3(cx, 8f + i * 5f, cz);

                var hazeMat = new StandardMaterial3D();
                hazeMat.AlbedoColor = new Color(0.15f, 0.08f, 0.03f, 0.06f);
                hazeMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                hazeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                hazeMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                haze.MaterialOverride = hazeMat;
                parent.AddChild(haze);
            }

            // Warm point lights scattered around — invisible light sources (no mesh)
            for (int i = 0; i < 8; i++)
            {
                float angle = _rng.RandfRange(0, Mathf.Tau);
                float dist = _rng.RandfRange(15f, 45f);
                var light = new OmniLight3D();
                light.Position = new Vector3(
                    cx + Mathf.Cos(angle) * dist,
                    _rng.RandfRange(2f, 6f),
                    cz + Mathf.Sin(angle) * dist);
                light.LightColor = new Color(0.9f, 0.5f, 0.15f);
                light.LightEnergy = 0.6f;
                light.OmniRange = 12f;
                light.OmniAttenuation = 1.5f;
                light.ShadowEnabled = false;
                parent.AddChild(light);
            }
        }

        // ── Helpers ──

        private static MeshInstance3D MakeMesh(Mesh mesh, StandardMaterial3D material)
        {
            var inst = new MeshInstance3D();
            inst.Mesh = mesh;
            inst.MaterialOverride = material;
            return inst;
        }

        private static Texture2D TryLoadTexture(string path)
        {
            if (!ResourceLoader.Exists(path)) return null;
            return GD.Load<Texture2D>(path);
        }
    }
}
