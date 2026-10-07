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

        /// <summary>Cells of sloped ground between the field edge and the yard floor (VineGrid.BuildOuterGround).</summary>
        public const int ApronCells = 5;
        // Props stay off the field and its apron
        private static float ClearMargin => ApronCells * Constants.VINE_CELL_SIZE + 1f;
        private static float _gridW, _gridH;
        private static bool Blocked(float x, float z, float extra = 0f)
            => VineBattleScene.InField(x, z, _gridW, _gridH, ClearMargin + extra);

        // ── PBR Material Paths ──
        private const string TEX_RUSTED_METAL = "res://Materials/Scrapyard/Textures/";
        private const string TEX_INDUSTRIAL_RUBBLE = "res://Materials/Scrapyard/Textures/";
        private const string TEX_DAMAGED_CONCRETE = "res://Materials/Scrapyard/Textures/";
        private const string TEX_CONCRETE_CRACK = "res://Materials/Scrapyard/concrete_crack_sdokhyi_2k/";
        private const string TEX_GARBAGE_PILE = "res://Materials/Scrapyard/garbage_pile_shlr1sh_2k/";

        // Dusk grade: textured props are tinted down so the low sun and the lamps carry the scene
        private static readonly Color DuskTint = new(0.55f, 0.5f, 0.5f);

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
            // Ground031's PNGs were never added (only stale .import files are), so loading them
            // logs engine errors; the rubble set is in the project
            var baseColor = TryLoadTexture(TEX_INDUSTRIAL_RUBBLE + "Industrial_Rubble_slxnyfd_2K_BaseColor.jpg");
            var normal = TryLoadTexture(TEX_INDUSTRIAL_RUBBLE + "Industrial_Rubble_slxnyfd_2K_Normal.jpg");
            var roughness = TryLoadTexture(TEX_INDUSTRIAL_RUBBLE + "Industrial_Rubble_slxnyfd_2K_Roughness.jpg");

            if (baseColor != null)
            {
                _groundMat.AlbedoTexture = baseColor;
                if (normal != null) { _groundMat.NormalEnabled = true; _groundMat.NormalTexture = normal; }
                if (roughness != null) _groundMat.RoughnessTexture = roughness;
                _groundMat.Uv1Scale = new Vector3(8, 8, 8); // Tile the texture
                _groundMat.AlbedoColor = new Color(0.42f, 0.36f, 0.34f); // dusk grade
            }
            else
            {
                // Fallback: procedural brown
                _groundMat.AlbedoColor = new Color(0.14f, 0.11f, 0.11f);
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
                // The set's _Specular map isn't metalness; fully metallic rust reflected a black sky
                _rustedMetalMat.Metallic = 0.3f;
                _rustedMetalMat.AlbedoColor = DuskTint;
                _rustedMetalMat.Uv1Scale = new Vector3(2, 2, 2);
            }
            else
            {
                _rustedMetalMat.AlbedoColor = new Color(0.2f, 0.12f, 0.08f);
                _rustedMetalMat.Roughness = 0.85f;
                _rustedMetalMat.Metallic = 0.3f;
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
                _concreteMat.AlbedoColor = DuskTint;
            }
            else
            {
                _concreteMat.AlbedoColor = new Color(0.13f, 0.12f, 0.12f);
                _concreteMat.Roughness = 0.9f;
            }
            return _concreteMat;
        }

        public static StandardMaterial3D GetDarkMetalMaterial()
        {
            if (_darkMetalMat != null) return _darkMetalMat;

            _darkMetalMat = new StandardMaterial3D();
            // Metal042A's PNGs were never added either; dark metal is the rust plate, darker and shinier
            var baseColor = TryLoadTexture(TEX_RUSTED_METAL + "Rusted_Metal_Plate_smsqo0n_2K_BaseColor.jpg");
            var normal = TryLoadTexture(TEX_RUSTED_METAL + "Rusted_Metal_Plate_smsqo0n_2K_Normal.jpg");
            var roughness = TryLoadTexture(TEX_RUSTED_METAL + "Rusted_Metal_Plate_smsqo0n_2K_Roughness.jpg");

            if (baseColor != null)
            {
                _darkMetalMat.AlbedoTexture = baseColor;
                if (normal != null) { _darkMetalMat.NormalEnabled = true; _darkMetalMat.NormalTexture = normal; }
                if (roughness != null) _darkMetalMat.RoughnessTexture = roughness;
                _darkMetalMat.Metallic = 0.6f;
                _darkMetalMat.AlbedoColor = new Color(0.22f, 0.21f, 0.22f);
                _darkMetalMat.Uv1Scale = new Vector3(3, 3, 3);
            }
            else
            {
                _darkMetalMat.AlbedoColor = new Color(0.08f, 0.07f, 0.07f);
                _darkMetalMat.Roughness = 0.7f;
                _darkMetalMat.Metallic = 0.35f;
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
                _garbagePileMat.AlbedoColor = DuskTint;
            }
            else
            {
                _garbagePileMat.AlbedoColor = new Color(0.1f, 0.09f, 0.07f);
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
            _gridW = gridW;
            _gridH = gridH;

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
            // The yard floor itself is VineGrid.BuildOuterGround (shares the field's material).
            // Scattered debris on the floor around the field, never on it
            for (int i = 0; i < 30; i++)
            {
                float px = 0, pz = 0;
                bool found = false;
                for (int tries = 0; tries < 12 && !found; tries++)
                {
                    float angle = _rng.RandfRange(0, Mathf.Tau);
                    float dist = _rng.RandfRange(30f, 90f);
                    px = cx + Mathf.Cos(angle) * dist;
                    pz = cz + Mathf.Sin(angle) * dist;
                    found = !Blocked(px, pz);
                }
                if (!found) continue;

                var debris = new MeshInstance3D();
                float s = _rng.RandfRange(0.2f, 0.8f);
                debris.Mesh = new BoxMesh { Size = new Vector3(s, s * 0.3f, s * _rng.RandfRange(0.5f, 1.5f)) };
                debris.Position = new Vector3(px, s * 0.1f, pz);
                debris.RotationDegrees = new Vector3(_rng.RandfRange(-10, 10), _rng.RandfRange(0, 360), _rng.RandfRange(-5, 5));
                debris.MaterialOverride = GetRustedMetalMaterial();
                parent.AddChild(debris);
            }
        }

        /// <summary>Point <paramref name="dist"/> outside the field's rectangle along <paramref name="angle"/>.</summary>
        private static Vector2 RingPoint(float cx, float cz, float gridW, float gridH, float angle, float dist)
        {
            float c = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            float tx = Mathf.Abs(c) > 1e-4f ? (gridW / 2f + dist) / Mathf.Abs(c) : float.MaxValue;
            float tz = Mathf.Abs(sn) > 1e-4f ? (gridH / 2f + dist) / Mathf.Abs(sn) : float.MaxValue;
            float t = Mathf.Min(tx, tz);
            return new Vector2(cx + c * t, cz + sn * t);
        }

        private static void BuildScrapRing(Node3D parent, float cx, float cz, float gridW, float gridH)
        {
            // Junk banked up around the field: an even band just past the apron, taller further out.
            // (An ellipse here cut into the field's corners.)
            float margin = ClearMargin + 1f;
            float depth = 45f;

            for (float angle = 0; angle < Mathf.Tau; angle += 0.06f)
            {
                for (float dist = margin; dist < depth; dist += _rng.RandfRange(2.5f, 5f))
                {
                    var p = RingPoint(cx, cz, gridW, gridH, angle, dist);
                    float rawX = p.X + _rng.RandfRange(-2f, 2f);
                    float rawZ = p.Y + _rng.RandfRange(-2f, 2f);
                    if (Blocked(rawX, rawZ)) continue;

                    float d = dist - margin;
                    float scale = 0.5f + d * 0.06f;
                    float height = 0.5f + d * 0.1f;

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
                (AssetLibrary.BLDG_OUTPOST,   new Vector3(cx - 35, 0, cz - 38), 0.25f, 15),
                (AssetLibrary.BLDG_FUEL_TANKS, new Vector3(cx + 56, 0, cz - 28), 0.2f, -20),
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
            // Haze comes from the environment's depth fog (ScrapyardPlanetTheme.ConfigureEnvironment).
            // The old stacked 120x120 haze planes washed the whole field out.

            // Fires and work lamps out in the junk ring, so the yard glows at dusk; none on the field
            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.Tau + _rng.RandfRange(-0.25f, 0.25f);
                var p = RingPoint(cx, cz, _gridW, _gridH, angle, ClearMargin + _rng.RandfRange(3f, 14f));
                if (Blocked(p.X, p.Y)) continue;
                var light = new OmniLight3D();
                light.Position = new Vector3(p.X, _rng.RandfRange(1.5f, 4f), p.Y);
                light.LightColor = new Color(1f, 0.52f, 0.18f);
                light.LightEnergy = 1.6f;
                light.OmniRange = 9f;
                light.OmniAttenuation = 1.6f;
                light.ShadowEnabled = false;
                parent.AddChild(light);

                // A visible ember at the source so the light reads as a fire, not a mystery glow
                var ember = MakeMesh(new SphereMesh { Radius = 0.35f, Height = 0.5f, RadialSegments = 8, Rings = 4 },
                    GetWarmGlowMaterial());
                ember.Position = new Vector3(p.X, 0.25f, p.Y);
                ember.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                parent.AddChild(ember);
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
