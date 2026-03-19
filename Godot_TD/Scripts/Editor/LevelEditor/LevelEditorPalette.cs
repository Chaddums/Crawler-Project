using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Unified palette builder for all editor tool modes.
    /// Extracted from LevelEditorUI with consistent API across modes.
    /// </summary>
    public static class LevelEditorPalette
    {
        // ── Shared helpers ──

        public static Label AddSection(VBoxContainer container, string title)
        {
            var label = EditorStyles.MakeLabel(title, 12, EditorStyles.AccentWaves);
            container.AddChild(label);
            return label;
        }

        public static HBoxContainer AddPresetButton(VBoxContainer container, string label, Color swatch, System.Action onClick)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var colorRect = new ColorRect();
            colorRect.Color = swatch;
            colorRect.CustomMinimumSize = new Vector2(20, 20);
            row.AddChild(colorRect);

            var btn = EditorStyles.MakeButton(label, 11);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.CustomMinimumSize = new Vector2(0, 24);
            btn.Pressed += onClick;
            row.AddChild(btn);

            container.AddChild(row);
            return row;
        }

        public static void AddSeparator(VBoxContainer container)
        {
            container.AddChild(EditorStyles.MakeSeparator());
        }

        // ── Per-mode palettes ──

        public static void BuildCellPalette(VBoxContainer container, LevelEditorScene editor)
        {
            var types = new[] { "Wall", "Elevated", "Channel", "DataStream", "Entry", "Exit" };
            var colors = new[] {
                EditorStyles.TextPrimary, EditorStyles.AccentMap,
                EditorStyles.AccentSignals, new Color(0f, 0.8f, 0.9f),
                EditorStyles.StatusOk, EditorStyles.StatusError
            };

            for (int i = 0; i < types.Length; i++)
            {
                int idx = i;
                var btn = EditorStyles.MakeButton(types[i], 12, colors[i]);
                btn.CustomMinimumSize = new Vector2(0, 28);
                btn.Pressed += () =>
                {
                    var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                    if (tools != null) tools.PaintCellType = types[idx];
                };
                container.AddChild(btn);
            }
        }

        public static void BuildHeightPalette(VBoxContainer container, LevelEditorScene editor)
        {
            var heightSpin = EditorStyles.MakeSpinBox(0.5f, 0.1f, 5f, 0.1f);
            heightSpin.ValueChanged += v =>
            {
                var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                if (tools != null) tools.HeightPaintValue = (float)v;
            };

            var row = new HBoxContainer();
            row.AddChild(EditorStyles.MakeLabel("Value:", 12, EditorStyles.TextMuted));
            row.AddChild(heightSpin);
            container.AddChild(row);

            container.AddChild(EditorStyles.MakeLabel("Left: Raise, Right: Lower", 11, EditorStyles.TextMuted));
        }

        public static void BuildAssetPalette(VBoxContainer container, LevelEditorScene editor)
        {
            var categories = new (string name, (string label, string path)[] items)[] {
                ("Buildings", new[] {
                    ("Checkpoint", AssetLibrary.BLDG_CHECKPOINT),
                    ("Barracks", AssetLibrary.BLDG_BARRACKS),
                    ("Fuel Tanks", AssetLibrary.BLDG_FUEL_TANKS),
                    ("Outpost", AssetLibrary.BLDG_OUTPOST),
                    ("Trench", AssetLibrary.BLDG_TRENCH),
                    ("Water Towers", AssetLibrary.BLDG_WATER_TOWERS),
                }),
                ("Turrets", new[] {
                    ("Turret A", AssetLibrary.TURRET_A),
                    ("Turret B", AssetLibrary.TURRET_B),
                    ("Turret C", AssetLibrary.TURRET_C),
                }),
                ("Props", new[] {
                    ("Barrel", AssetLibrary.PROP_BARREL),
                    ("Container A", AssetLibrary.PROP_CONTAINER_A),
                    ("Antenna A", AssetLibrary.PROP_ANTENNA_A),
                }),
            };

            foreach (var (catName, items) in categories)
            {
                AddSection(container, catName);
                foreach (var (label, path) in items)
                {
                    var assetPath = path;
                    var btn = EditorStyles.MakeButton(label, 11);
                    btn.CustomMinimumSize = new Vector2(0, 26);
                    btn.Pressed += () =>
                    {
                        var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                        if (tools != null) tools.PlaceAssetPath = assetPath;
                    };
                    container.AddChild(btn);
                }
            }

            // Assemblies section
            AddSeparator(container);
            BuildAssemblyPaletteSection(container, editor);
        }

        public static void BuildAssemblyPaletteSection(VBoxContainer container, LevelEditorScene editor)
        {
            var assembly = editor?.GetNodeOrNull<LevelEditorAssembly>("LevelEditorAssembly");
            if (assembly == null) return;

            var assemblyIds = assembly.ListAssemblies();
            if (assemblyIds.Length == 0)
            {
                container.AddChild(EditorStyles.MakeLabel("ASSEMBLIES", 12, EditorStyles.TextSecondary));
                container.AddChild(EditorStyles.MakeLabel("None saved", 11, EditorStyles.TextMuted));
                return;
            }

            AddSection(container, "ASSEMBLIES");
            foreach (var id in assemblyIds)
            {
                var asmId = id;
                var btn = EditorStyles.MakeButton(id, 11, EditorStyles.AccentMap);
                btn.CustomMinimumSize = new Vector2(0, 26);
                btn.Pressed += () =>
                {
                    assembly.SelectedAssemblyId = asmId;
                    var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                    if (tools != null) tools.PlaceAssemblyId = asmId;
                };
                container.AddChild(btn);
            }
        }

        public static void BuildLightPalette(VBoxContainer container, LevelEditorScene editor)
        {
            var presets = new[] {
                ("Warm Omni", "Omni", new Color(1f, 0.85f, 0.6f)),
                ("Cool Omni", "Omni", new Color(0.6f, 0.8f, 1f)),
                ("Accent Spot", "Spot", new Color(0f, 0.9f, 0.95f)),
            };

            foreach (var (label, type, color) in presets)
            {
                var lightType = type;
                var btn = EditorStyles.MakeButton(label, 12, color);
                btn.CustomMinimumSize = new Vector2(0, 28);
                btn.Pressed += () =>
                {
                    var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                    if (tools != null) tools.PlaceLightType = lightType;
                };
                container.AddChild(btn);
            }
        }

        public static void BuildFXPalette(VBoxContainer container, LevelEditorScene editor)
        {
            var types = new[] { "Fire", "Sparks", "Smoke", "Fog", "Electric_Arc" };
            var colors = new[] {
                new Color(1f, 0.4f, 0.1f),
                new Color(1f, 0.9f, 0.2f),
                new Color(0.6f, 0.6f, 0.6f),
                new Color(0.5f, 0.5f, 0.7f),
                new Color(0.3f, 0.7f, 1f),
            };

            for (int i = 0; i < types.Length; i++)
            {
                int idx = i;
                var btn = EditorStyles.MakeButton(types[i], 12, colors[i]);
                btn.CustomMinimumSize = new Vector2(0, 28);
                btn.Pressed += () =>
                {
                    var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                    if (tools != null) tools.PlaceFXType = types[idx];
                };
                container.AddChild(btn);
            }
        }

        public static void BuildTexturePalette(VBoxContainer container, LevelEditorScene editor)
        {
            container.AddChild(EditorStyles.MakeLabel(
                "Paint terrain material overrides.\nLeft-click to apply, Right to clear.",
                11, EditorStyles.TextMuted));

            var textures = new (string label, string id, Color swatch)[] {
                ("Ground (Default)", "ground", new Color(0.02f, 0.02f, 0.04f)),
                ("Grid Cyan", "grid_cyan", TronTheme.GridCyan),
                ("Dark Metal", "dark_metal", new Color(0.04f, 0.04f, 0.06f)),
                ("Rust", "rust", new Color(0.35f, 0.18f, 0.08f)),
                ("Concrete", "concrete", new Color(0.25f, 0.24f, 0.22f)),
                ("Sand", "sand", new Color(0.45f, 0.38f, 0.25f)),
                ("Grime", "grime", new Color(0.12f, 0.1f, 0.06f)),
                ("Acid Green", "acid", new Color(0.2f, 0.6f, 0.1f)),
                ("Lava Orange", "lava", new Color(0.8f, 0.25f, 0.02f)),
                ("Ice Blue", "ice", new Color(0.4f, 0.65f, 0.85f)),
                ("Toxic Purple", "toxic", new Color(0.4f, 0.1f, 0.5f)),
                ("Hologram Cyan", "holo", new Color(0f, 0.85f, 0.95f)),
            };

            AddSection(container, "Base Surfaces");
            foreach (var (label, id, swatch) in textures)
            {
                var texId = id;
                AddPresetButton(container, label, swatch, () =>
                {
                    var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                    if (tools != null) tools.PaintTextureId = texId;
                });
            }

            AddSeparator(container);
            AddSection(container, "Emissive");

            var emissives = new (string label, string id, Color swatch)[] {
                ("Cyan Glow", "emit_cyan", new Color(0f, 0.85f, 0.95f)),
                ("Red Glow", "emit_red", new Color(0.9f, 0.15f, 0.1f)),
                ("Green Glow", "emit_green", new Color(0.1f, 0.9f, 0.2f)),
                ("Gold Glow", "emit_gold", new Color(0.9f, 0.7f, 0.2f)),
                ("Magenta Glow", "emit_magenta", new Color(0.8f, 0.1f, 0.6f)),
            };

            foreach (var (label, id, swatch) in emissives)
            {
                var texId = id;
                AddPresetButton(container, label, swatch, () =>
                {
                    var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                    if (tools != null) tools.PaintTextureId = texId;
                });
            }

            AddSeparator(container);
            var brushRow = new HBoxContainer();
            brushRow.AddChild(EditorStyles.MakeLabel("Brush:", 12, EditorStyles.TextMuted));
            var brushSpin = EditorStyles.MakeSpinBox(1, 1, 5, 1);
            brushSpin.ValueChanged += v =>
            {
                var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                if (tools != null) tools.TextureBrushSize = (int)v;
            };
            brushRow.AddChild(brushSpin);
            container.AddChild(brushRow);
        }

        public static void BuildMaterialPaintPalette(VBoxContainer container, LevelEditorScene editor)
        {
            container.AddChild(EditorStyles.MakeLabel(
                "Click objects to apply material.\nWorks with multi-select.",
                11, EditorStyles.TextMuted));

            // Faction presets
            AddSection(container, "FACTION PRESETS");

            var factions = new (string label, int id, Color swatch)[] {
                ("Player (BIT)", 0, BitPalette.Accent),
                ("Scavenger", 1, TronTheme.EnemyScavenger),
                ("Brute", 2, TronTheme.EnemyBrute),
                ("Swarm", 3, TronTheme.EnemySwarm),
                ("Ghost", 4, TronTheme.EnemyGhost),
            };

            foreach (var (label, id, swatch) in factions)
            {
                var factionId = id;
                AddPresetButton(container, label, swatch, () =>
                {
                    var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                    if (tools != null)
                        tools.PaintMaterialOverride = LevelEditorMaterialPainter.MakeFactionPreset(factionId);
                });
            }

            // Original (restore)
            AddPresetButton(container, "Original", new Color(0.3f, 0.3f, 0.3f), () =>
            {
                var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                if (tools != null)
                    tools.PaintMaterialOverride = LevelEditorMaterialPainter.MakeOriginalPreset();
            });

            AddSeparator(container);
            AddSection(container, "CUSTOM");

            // Color pickers
            var colorPicker = new ColorPickerButton();
            colorPicker.Color = new Color(0f, 0.85f, 0.95f);
            colorPicker.CustomMinimumSize = new Vector2(0, 28);
            colorPicker.EditAlpha = false;
            container.AddChild(colorPicker);

            // Material type dropdown
            var typeRow = new HBoxContainer();
            typeRow.AddChild(EditorStyles.MakeLabel("Type:", 11, EditorStyles.TextMuted));
            var typeOption = new OptionButton();
            typeOption.AddItem("Custom Solid", 0);
            typeOption.AddItem("Emissive", 1);
            typeOption.AddItem("Theme Tinted", 2);
            typeOption.CustomMinimumSize = new Vector2(120, 0);
            typeRow.AddChild(typeOption);
            container.AddChild(typeRow);

            // Intensity (for emissive)
            var intensityRow = new HBoxContainer();
            intensityRow.AddChild(EditorStyles.MakeLabel("Intensity:", 11, EditorStyles.TextMuted));
            var intensitySpin = EditorStyles.MakeSpinBox(2f, 0.1f, 10f, 0.1f);
            intensityRow.AddChild(intensitySpin);
            container.AddChild(intensityRow);

            // Outline mode
            var outlineRow = new HBoxContainer();
            outlineRow.AddChild(EditorStyles.MakeLabel("Outline:", 11, EditorStyles.TextMuted));
            var outlineOption = new OptionButton();
            outlineOption.AddItem("Per-Mesh", 0);
            outlineOption.AddItem("Silhouette", 1);
            outlineOption.AddItem("None", 2);
            outlineOption.CustomMinimumSize = new Vector2(100, 0);
            outlineRow.AddChild(outlineOption);
            container.AddChild(outlineRow);

            // Apply button
            var applyBtn = EditorStyles.MakeButton("Set Custom Material", 12, EditorStyles.AccentNodes);
            applyBtn.CustomMinimumSize = new Vector2(0, 30);
            applyBtn.Pressed += () =>
            {
                var tools = editor?.GetNodeOrNull<LevelEditorTools>("LevelEditorTools");
                if (tools == null) return;

                var color = colorPicker.Color;
                int typeIdx = typeOption.Selected;
                float intensity = (float)intensitySpin.Value;
                int outline = outlineOption.Selected;

                MaterialOverrideData data;
                switch (typeIdx)
                {
                    case 1: // Emissive
                        data = LevelEditorMaterialPainter.MakeEmissivePreset(color, intensity);
                        break;
                    case 2: // Theme tinted
                        data = new MaterialOverrideData
                        {
                            MaterialType = "theme",
                            TintR = color.R, TintG = color.G, TintB = color.B,
                            OutlineMode = outline
                        };
                        break;
                    default: // Custom solid
                        data = LevelEditorMaterialPainter.MakeCustomPreset(color);
                        break;
                }

                tools.PaintMaterialOverride = data;
            };
            container.AddChild(applyBtn);
        }
    }
}
