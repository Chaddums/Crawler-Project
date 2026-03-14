using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Batch-renders every combination of bot frame, weapon, growth tier, and weapon mount
    /// to PNG screenshots and generates an HTML gallery for visual review.
    /// Uses a dedicated SubViewport with three-point lighting, cycling through all categories.
    ///
    /// Output goes to user://character_catalog/ with an index.html for browsing.
    /// </summary>
    public partial class CharacterCatalogExporter : Node
    {
        private SubViewport _viewport;
        private Camera3D _camera;
        private Node3D _previewRoot;

        private readonly List<(string category, string id)> _queue = new();
        private int _currentIndex;
        private int _frameWait;
        private string _outputDir;
        private bool _exporting;
        private readonly Dictionary<string, List<string>> _capturedByCategory = new();

        public event Action<string> OnProgress;
        public event Action OnComplete;

        // Ranged weapon IDs mapped from WeaponType
        private static readonly Dictionary<WeaponType, string> RangedWeaponIds = new()
        {
            { WeaponType.Pistol, "base_pistol" },
            { WeaponType.Rifle, "base_rifle" },
            { WeaponType.Shotgun, "base_shotgun" },
            { WeaponType.Launcher, "base_launcher" },
            { WeaponType.Repeater, "base_repeater" },
        };

        private static readonly HashSet<WeaponType> AoeWeaponTypes = new()
        {
            WeaponType.BladeRing,
            WeaponType.FlailChain,
            WeaponType.ShockCoil,
            WeaponType.FlameThrower,
        };

        public override void _Ready()
        {
            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(512, 512);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;
            _viewport.TransparentBg = false;
            AddChild(_viewport);

            _camera = new Camera3D();
            _camera.Position = new Vector3(3, 2.5f, 3);
            _camera.LookAt(Vector3.Zero);
            _viewport.AddChild(_camera);

            _previewRoot = new Node3D();
            _viewport.AddChild(_previewRoot);

            // Three-point lighting for good asset visibility
            var keyLight = new DirectionalLight3D();
            keyLight.RotationDegrees = new Vector3(-55, 40, 0);
            keyLight.LightEnergy = 2.0f;
            keyLight.LightColor = new Color(1.0f, 0.98f, 0.95f);
            _viewport.AddChild(keyLight);

            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-40, -60, 0);
            fillLight.LightEnergy = 0.8f;
            fillLight.LightColor = new Color(0.85f, 0.9f, 1.0f);
            _viewport.AddChild(fillLight);

            var rimLight = new DirectionalLight3D();
            rimLight.RotationDegrees = new Vector3(-20, 180, 0);
            rimLight.LightEnergy = 0.6f;
            _viewport.AddChild(rimLight);

            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.08f, 0.08f, 0.1f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.4f, 0.4f, 0.45f);
            envRes.AmbientLightEnergy = 1.0f;
            envRes.TonemapMode = Godot.Environment.ToneMapper.Aces;
            envRes.TonemapWhite = 6.0f;
            envRes.SsaoEnabled = true;
            envRes.SsaoIntensity = 1.0f;
            env.Environment = envRes;
            _viewport.AddChild(env);

            // Reflection probe so metallic PBR materials reflect something
            var probe = new ReflectionProbe();
            probe.BoxProjection = true;
            probe.Size = new Vector3(30, 30, 30);
            probe.Interior = true;
            probe.AmbientMode = ReflectionProbe.AmbientModeEnum.Color;
            probe.AmbientColor = new Color(0.3f, 0.3f, 0.35f);
            _viewport.AddChild(probe);

            // Grid floor
            var grid = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(20, 20);
            grid.Mesh = planeMesh;
            grid.Position = new Vector3(0, -0.01f, 0);
            var gridMat = new StandardMaterial3D();
            gridMat.AlbedoColor = new Color(0.1f, 0.1f, 0.12f, 0.4f);
            gridMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            gridMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            grid.MaterialOverride = gridMat;
            _viewport.AddChild(grid);
        }

        public void StartExport()
        {
            if (_exporting) return;

            _outputDir = ProjectSettings.GlobalizePath("user://character_catalog");
            DirAccess.MakeDirRecursiveAbsolute(_outputDir);

            _queue.Clear();
            _capturedByCategory.Clear();

            // Category 1: Frames — each bot frame at base tier, no weapon
            foreach (var frame in Enum.GetValues(typeof(BotFrameType)))
                _queue.Add(("frame", ((BotFrameType)frame).ToString().ToLower()));

            // Category 2: Weapons — each weapon type on TinCan
            foreach (var wt in Enum.GetValues(typeof(WeaponType)))
            {
                var weaponType = (WeaponType)wt;
                if (weaponType == WeaponType.None) continue;
                _queue.Add(("weapon", weaponType.ToString().ToLower()));
            }

            // Category 3: Growth tiers — each frame x each tier
            foreach (var frame in Enum.GetValues(typeof(BotFrameType)))
            {
                foreach (var tier in Enum.GetValues(typeof(GrowthTier)))
                {
                    var frameName = ((BotFrameType)frame).ToString().ToLower();
                    var tierName = ((GrowthTier)tier).ToString().ToLower();
                    _queue.Add(("growth", $"{frameName}_{tierName}"));
                }
            }

            // Category 4: Weapon mounts — each mount type on TinCan with Pistol
            foreach (var mt in Enum.GetValues(typeof(WeaponMountType)))
                _queue.Add(("mount", ((WeaponMountType)mt).ToString().ToLower()));

            // Category 5: Enemies — all registered enemy types
            EnemyRegistry.Initialize(); // ensure registry is populated
            foreach (var kvp in EnemyRegistry.Enemies)
                _queue.Add(("enemy", kvp.Key));

            // Category 6: Bosses — all registered boss types
            var bossIds = new[] { "corrupted_sentry", "rust_titan", "scrap_hydra", "null_warden", "axis_avatar" };
            foreach (var bossId in bossIds)
                _queue.Add(("boss", bossId));

            _currentIndex = 0;
            _frameWait = 0;
            _exporting = true;

            var seenCats = new HashSet<string>();
            foreach (var (cat, _) in _queue)
            {
                if (seenCats.Add(cat))
                    DirAccess.MakeDirRecursiveAbsolute($"{_outputDir}/{cat}");
            }

            GD.Print($"[CharacterCatalog] Starting export of {_queue.Count} characters to {_outputDir}");
            OnProgress?.Invoke($"Exporting 0/{_queue.Count}...");
        }

        public override void _Process(double delta)
        {
            if (!_exporting) return;

            if (_frameWait > 0)
            {
                _frameWait--;
                if (_frameWait == 0)
                    CaptureCurrentAsset();
                return;
            }

            if (_currentIndex >= _queue.Count)
            {
                FinishExport();
                return;
            }

            LoadNextAsset();
        }

        private void LoadNextAsset()
        {
            var (category, id) = _queue[_currentIndex];

            // Clear preview — remove and free all children
            foreach (var child in _previewRoot.GetChildren())
            {
                _previewRoot.RemoveChild(child);
                ((Node)child).QueueFree();
            }

            Node3D model = null;

            try
            {
                switch (category)
                {
                    case "frame":
                        model = BuildFrameModel(id);
                        break;
                    case "weapon":
                        model = BuildWeaponModel(id);
                        break;
                    case "growth":
                        model = BuildGrowthModel(id);
                        break;
                    case "mount":
                        model = BuildMountModel(id);
                        break;
                    case "enemy":
                        model = CharacterMeshBuilder.BuildEnemyBody(id);
                        break;
                    case "boss":
                        model = BuildBossModel(id);
                        break;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CharacterCatalog] Error building {category}/{id}: {ex.Message}");
                _currentIndex++;
                return;
            }

            if (model == null)
            {
                GD.Print($"[CharacterCatalog] Skipped {category}/{id} (no model)");
                _currentIndex++;
                return;
            }

            // Add to scene first so transforms are valid
            _previewRoot.AddChild(model);

            // Fit camera using full recursive transform-aware AABB
            FitCamera(model);

            // Wait frames for viewport to render
            _frameWait = 3;
        }

        /// <summary>
        /// Build a bare bot frame body (base tier, no weapon).
        /// </summary>
        private Node3D BuildFrameModel(string id)
        {
            if (!Enum.TryParse<BotFrameType>(id, true, out var frame))
                return null;

            return CharacterMeshBuilder.BuildPlayerBody(frame);
        }

        /// <summary>
        /// Build TinCan body with specified weapon type attached.
        /// </summary>
        private Node3D BuildWeaponModel(string id)
        {
            if (!Enum.TryParse<WeaponType>(id, true, out var weaponType))
                return null;

            var body = CharacterMeshBuilder.BuildPlayerBody(BotFrameType.TinCan);
            if (body == null) return null;

            // We need a container so the body is in the scene tree before we add weapon children
            var container = new Node3D();
            container.Name = $"WeaponPreview_{id}";
            container.AddChild(body);

            if (RangedWeaponIds.TryGetValue(weaponType, out var weaponId))
            {
                // Ranged weapon — build from item registry
                var itemData = ItemRegistry.GetItem(weaponId);
                if (itemData == null)
                {
                    GD.Print($"[CharacterCatalog] Item '{weaponId}' not found in registry, skipping weapon");
                    return container;
                }

                Node3D weaponModel;
                try
                {
                    weaponModel = CharacterMeshBuilder.BuildItemModel(new ItemInstance(itemData, ItemRarity.Common));
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[CharacterCatalog] Failed to build item model for {weaponId}: {ex.Message}");
                    return container;
                }

                if (weaponModel == null) return container;

                var mount = FbxPivotMapper.FindNodeRecursive(body, "WeaponMount");
                if (mount != null)
                {
                    weaponModel.Position = Vector3.Zero;
                    mount.AddChild(weaponModel);
                    CharacterMeshBuilder.ScaleWeaponToWorldSize(weaponModel, 0.6f);
                }
                else
                {
                    weaponModel.Position = new Vector3(0, 1, 0);
                    body.AddChild(weaponModel);
                    CharacterMeshBuilder.ScaleWeaponToWorldSize(weaponModel, 0.6f);
                }
            }
            else if (AoeWeaponTypes.Contains(weaponType))
            {
                // AoE weapon — build via dedicated builder method
                Node3D aoeModel = weaponType switch
                {
                    WeaponType.BladeRing => CharacterMeshBuilder.BuildBladeRing(),
                    WeaponType.FlailChain => CharacterMeshBuilder.BuildFlailChain(),
                    WeaponType.ShockCoil => CharacterMeshBuilder.BuildShockCoil(),
                    WeaponType.FlameThrower => CharacterMeshBuilder.BuildFlameThrower(),
                    _ => null
                };

                if (aoeModel != null)
                {
                    aoeModel.Position = new Vector3(0, 1, 0);
                    body.AddChild(aoeModel);
                }
            }

            return container;
        }

        /// <summary>
        /// Build a bot frame at a specific growth tier.
        /// </summary>
        private Node3D BuildGrowthModel(string id)
        {
            // id format: "{frame}_{tier}" e.g. "tincan_plated"
            var lastUnderscore = id.LastIndexOf('_');
            if (lastUnderscore < 0) return null;

            var framePart = id.Substring(0, lastUnderscore);
            var tierPart = id.Substring(lastUnderscore + 1);

            if (!Enum.TryParse<BotFrameType>(framePart, true, out var frame))
                return null;
            if (!Enum.TryParse<GrowthTier>(tierPart, true, out var tier))
                return null;

            var body = CharacterMeshBuilder.BuildPlayerBody(frame);
            if (body == null) return null;

            if (tier != GrowthTier.Base)
            {
                try
                {
                    var growthPieces = CharacterMeshBuilder.BuildGrowthPieces(frame, tier, body);
                    if (growthPieces != null)
                        body.AddChild(growthPieces);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[CharacterCatalog] Failed to build growth pieces for {id}: {ex.Message}");
                }

                // Apply tier scale
                float tierScale = tier switch
                {
                    GrowthTier.Plated => 1.08f,
                    GrowthTier.Armored => 1.15f,
                    GrowthTier.Heavy => 1.22f,
                    GrowthTier.Evolved => 1.3f,
                    _ => 1.0f
                };
                body.Scale *= tierScale;
            }

            return body;
        }

        /// <summary>
        /// Build TinCan body with Pistol using a specific weapon mount type.
        /// </summary>
        private Node3D BuildMountModel(string id)
        {
            if (!Enum.TryParse<WeaponMountType>(id, true, out var mountType))
                return null;

            var body = CharacterMeshBuilder.BuildPlayerBody(BotFrameType.TinCan);
            if (body == null) return null;

            var container = new Node3D();
            container.Name = $"MountPreview_{id}";
            container.AddChild(body);

            // Build pistol weapon model
            var itemData = ItemRegistry.GetItem("base_pistol");
            if (itemData == null)
            {
                GD.Print("[CharacterCatalog] Item 'base_pistol' not found in registry, skipping mount preview");
                return container;
            }

            Node3D weaponModel;
            try
            {
                weaponModel = CharacterMeshBuilder.BuildItemModel(new ItemInstance(itemData, ItemRarity.Common));
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CharacterCatalog] Failed to build pistol model for mount: {ex.Message}");
                return container;
            }

            if (weaponModel == null) return container;

            if (mountType == WeaponMountType.HandHeld)
            {
                // Default mount: use WeaponMount marker
                var mount = FbxPivotMapper.FindNodeRecursive(body, "WeaponMount");
                if (mount != null)
                {
                    weaponModel.Position = Vector3.Zero;
                    mount.AddChild(weaponModel);
                    CharacterMeshBuilder.ScaleWeaponToWorldSize(weaponModel, 0.6f);
                }
                else
                {
                    weaponModel.Position = new Vector3(0, 1, 0);
                    body.AddChild(weaponModel);
                    CharacterMeshBuilder.ScaleWeaponToWorldSize(weaponModel, 0.6f);
                }
            }
            else
            {
                // Custom mount position/rotation
                var customMount = new Marker3D();
                customMount.Name = $"Mount_{mountType}";
                customMount.Position = CharacterMeshBuilder.GetMountPosition(BotFrameType.TinCan, mountType);
                body.AddChild(customMount);

                weaponModel.RotationDegrees = CharacterMeshBuilder.GetMountRotation(mountType);
                customMount.AddChild(weaponModel);
                CharacterMeshBuilder.ScaleWeaponToWorldSize(weaponModel, 0.6f);
                float mountScale = CharacterMeshBuilder.GetMountScale(mountType);
                weaponModel.Scale *= mountScale;
            }

            return container;
        }

        /// <summary>
        /// Build a boss model. AXIS Avatar uses its custom body; others use BuildEnemyBody.
        /// </summary>
        private Node3D BuildBossModel(string id)
        {
            if (id == "axis_avatar")
                return AxisBossBody.Build();
            return CharacterMeshBuilder.BuildEnemyBody(id);
        }

        private void FitCamera(Node3D model)
        {
            // Use recursive global-transform-aware AABB calculation
            var aabb = GetGlobalAabb(model);
            var center = aabb.GetCenter();
            float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));

            // Clamp to reasonable values
            if (maxDim < 0.01f) maxDim = 2f;
            if (maxDim > 100f) maxDim = 100f;

            float distance = Mathf.Max(2f, maxDim * 1.8f);
            float height = center.Y + maxDim * 0.3f;

            _camera.Position = new Vector3(
                center.X + Mathf.Cos(0.7f) * distance,
                Mathf.Max(height, 0.5f),
                center.Z + Mathf.Sin(0.7f) * distance
            );
            _camera.LookAt(center);
        }

        /// <summary>
        /// Calculate AABB using GlobalTransform — handles arbitrarily nested Node3D hierarchies
        /// correctly, including FBX axis-correction nodes and GLB group wrappers.
        /// </summary>
        private Aabb GetGlobalAabb(Node3D root)
        {
            var combined = new Aabb();
            bool first = true;
            CollectGlobalAabb(root, ref combined, ref first);
            if (first) return new Aabb(Vector3.Zero, Vector3.One);
            return combined;
        }

        private void CollectGlobalAabb(Node node, ref Aabb aabb, ref bool first)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var meshAabb = mi.Mesh.GetAabb();

                // Transform all 8 corners of the mesh AABB through the global transform
                var gt = mi.GlobalTransform;
                var corners = new Vector3[8];
                var pos = meshAabb.Position;
                var end = meshAabb.End;
                corners[0] = gt * new Vector3(pos.X, pos.Y, pos.Z);
                corners[1] = gt * new Vector3(end.X, pos.Y, pos.Z);
                corners[2] = gt * new Vector3(pos.X, end.Y, pos.Z);
                corners[3] = gt * new Vector3(end.X, end.Y, pos.Z);
                corners[4] = gt * new Vector3(pos.X, pos.Y, end.Z);
                corners[5] = gt * new Vector3(end.X, pos.Y, end.Z);
                corners[6] = gt * new Vector3(pos.X, end.Y, end.Z);
                corners[7] = gt * new Vector3(end.X, end.Y, end.Z);

                // Build AABB from transformed corners
                var min = corners[0];
                var max = corners[0];
                for (int i = 1; i < 8; i++)
                {
                    min = new Vector3(
                        Mathf.Min(min.X, corners[i].X),
                        Mathf.Min(min.Y, corners[i].Y),
                        Mathf.Min(min.Z, corners[i].Z));
                    max = new Vector3(
                        Mathf.Max(max.X, corners[i].X),
                        Mathf.Max(max.Y, corners[i].Y),
                        Mathf.Max(max.Z, corners[i].Z));
                }

                var transformedAabb = new Aabb(min, max - min);
                if (first) { aabb = transformedAabb; first = false; }
                else aabb = aabb.Merge(transformedAabb);
            }

            foreach (var child in node.GetChildren())
                if (child is Node n) CollectGlobalAabb(n, ref aabb, ref first);
        }

        private void CaptureCurrentAsset()
        {
            if (_currentIndex >= _queue.Count) return;

            var (category, id) = _queue[_currentIndex];
            string filename = $"{id}.png";
            string filepath = $"{_outputDir}/{category}/{filename}";

            var img = _viewport.GetTexture().GetImage();
            if (img != null)
            {
                img.SavePng(filepath);

                if (!_capturedByCategory.ContainsKey(category))
                    _capturedByCategory[category] = new List<string>();
                _capturedByCategory[category].Add(id);
            }
            else
            {
                GD.PrintErr($"[CharacterCatalog] Failed to capture {category}/{id}");
            }

            _currentIndex++;
            int pct = (int)((float)_currentIndex / _queue.Count * 100);
            OnProgress?.Invoke($"Exporting {_currentIndex}/{_queue.Count} ({pct}%) — {category}/{id}");
        }

        private void FinishExport()
        {
            _exporting = false;
            GenerateHtml();

            int total = _currentIndex;
            GD.Print($"[CharacterCatalog] Export complete: {total} characters to {_outputDir}");
            OnProgress?.Invoke($"Done! {total} characters exported.");
            OnComplete?.Invoke();
        }

        private string GetCardDescription(string category, string id)
        {
            return category switch
            {
                "frame" => $"Bot Frame: {id}",
                "weapon" => $"Weapon: {id} on TinCan",
                "growth" => FormatGrowthDescription(id),
                "mount" => $"Mount: {id} (Pistol on TinCan)",
                "enemy" => $"Enemy: {id}",
                "boss" => $"Boss: {id}",
                _ => id
            };
        }

        private static string FormatGrowthDescription(string id)
        {
            var lastUnderscore = id.LastIndexOf('_');
            if (lastUnderscore < 0) return id;
            var frame = id.Substring(0, lastUnderscore);
            var tier = id.Substring(lastUnderscore + 1);
            return $"{frame} @ {tier} tier";
        }

        private void GenerateHtml()
        {
            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            html.AppendLine("<title>Junkbot Arena — Character Catalog</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { background: #1a1a2e; color: #e0e0e0; font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #00d4ff; border-bottom: 2px solid #00d4ff; padding-bottom: 8px; }");
            html.AppendLine("h2 { color: #ffa500; margin-top: 30px; cursor: pointer; }");
            html.AppendLine("h2:hover { color: #ffcc00; }");
            html.AppendLine(".category { margin-bottom: 20px; }");
            html.AppendLine(".grid { display: flex; flex-wrap: wrap; gap: 12px; }");
            html.AppendLine(".card { background: #16213e; border: 1px solid #333; border-radius: 8px; padding: 8px; width: 200px; text-align: center; transition: border-color 0.2s; position: relative; }");
            html.AppendLine(".card:hover { border-color: #00d4ff; }");
            html.AppendLine(".card.flagged { border-color: #ff4444; border-width: 2px; background: #1e1428; }");
            html.AppendLine(".card img { width: 180px; height: 180px; object-fit: contain; border-radius: 4px; background: #0a0a1a; cursor: pointer; }");
            html.AppendLine(".card .name { font-size: 11px; color: #aaa; margin-top: 4px; word-break: break-all; }");
            html.AppendLine(".card .id { font-size: 13px; color: #fff; font-weight: bold; margin-top: 2px; }");
            html.AppendLine(".card .flag-row { display:flex; align-items:center; gap:4px; margin-top:6px; justify-content:center; }");
            html.AppendLine(".card .flag-row input[type=checkbox] { accent-color: #ff4444; cursor:pointer; width:16px; height:16px; }");
            html.AppendLine(".card .flag-row label { font-size:11px; color:#ff8888; cursor:pointer; }");
            html.AppendLine(".card .note-box { display:none; margin-top:4px; }");
            html.AppendLine(".card.flagged .note-box { display:block; }");
            html.AppendLine(".card .note-box textarea { width:100%; height:40px; background:#0a0a1a; border:1px solid #444; color:#ddd; font-size:11px; border-radius:4px; padding:4px; resize:vertical; font-family:inherit; }");
            html.AppendLine(".stats { color: #888; font-size: 13px; margin: 10px 0; }");
            html.AppendLine(".toolbar { display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin:10px 0; }");
            html.AppendLine(".filter { padding: 8px 12px; background: #16213e; border: 1px solid #444; color: #fff; font-size: 14px; border-radius: 4px; width: 300px; }");
            html.AppendLine(".toolbar button { padding:8px 16px; border:1px solid #444; border-radius:4px; cursor:pointer; font-size:13px; }");
            html.AppendLine(".btn-export { background:#ff4444; color:#fff; border-color:#ff4444; }");
            html.AppendLine(".btn-export:hover { background:#ff6666; }");
            html.AppendLine(".btn-filter { background:#16213e; color:#ddd; }");
            html.AppendLine(".btn-filter:hover { background:#1e2a4a; }");
            html.AppendLine(".btn-filter.active { background:#3a1a1a; border-color:#ff4444; color:#ff8888; }");
            html.AppendLine(".issue-count { color:#ff8888; font-weight:bold; font-size:14px; }");
            html.AppendLine(".toast { position:fixed; bottom:20px; right:20px; background:#2a5a2a; color:#fff; padding:12px 20px; border-radius:8px; z-index:2000; display:none; font-size:14px; }");
            html.AppendLine(".hidden { display: none !important; }");
            // Lightbox
            html.AppendLine(".lightbox { display:none; position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,0.92); z-index:1000; justify-content:center; align-items:center; flex-direction:column; cursor:pointer; }");
            html.AppendLine(".lightbox.active { display:flex; }");
            html.AppendLine(".lightbox img { max-width:80vw; max-height:75vh; border:2px solid #00d4ff; border-radius:8px; image-rendering: auto; }");
            html.AppendLine(".lightbox .lb-title { color:#fff; font-size:22px; font-weight:bold; margin-top:12px; }");
            html.AppendLine(".lightbox .lb-cat { color:#888; font-size:14px; margin-top:4px; }");
            html.AppendLine(".lightbox .lb-hint { color:#555; font-size:12px; margin-top:8px; }");
            html.AppendLine("</style></head><body>");
            html.AppendLine("<h1>Junkbot Arena — Character Catalog</h1>");
            html.AppendLine($"<p class='stats'>Generated: {DateTime.Now:yyyy-MM-dd HH:mm} | Total characters: {_currentIndex}</p>");
            html.AppendLine("<div class='toolbar'>");
            html.AppendLine("<input class='filter' type='text' placeholder='Filter characters...' oninput='filterAssets(this.value)'>");
            html.AppendLine("<button class='btn-filter' id='flaggedOnlyBtn' onclick='toggleFlaggedOnly()'>Show Flagged Only</button>");
            html.AppendLine("<button class='btn-export' onclick='exportIssues()'>Export Issues</button>");
            html.AppendLine("<button class='btn-filter' onclick='clearAllFlags()'>Clear All</button>");
            html.AppendLine("<span class='issue-count' id='issueCount'>0 issues</span>");
            html.AppendLine("</div>");
            // Lightbox element
            html.AppendLine("<div class='lightbox' id='lightbox' onclick='closeLightbox()'>");
            html.AppendLine("<img id='lb-img' src=''><div class='lb-title' id='lb-title'></div><div class='lb-cat' id='lb-cat'></div>");
            html.AppendLine("<div class='lb-hint'>Click anywhere to close | Arrow keys to navigate | Esc to exit</div>");
            html.AppendLine("</div>");

            // Table of contents
            html.AppendLine("<p>");
            foreach (var kvp in _capturedByCategory)
                html.AppendLine($"<a href='#{kvp.Key}' style='color:#00d4ff; margin-right:12px;'>{kvp.Key} ({kvp.Value.Count})</a>");
            html.AppendLine("</p>");

            foreach (var kvp in _capturedByCategory)
            {
                string cat = kvp.Key;
                var ids = kvp.Value;

                html.AppendLine($"<div class='category' id='{cat}'>");
                html.AppendLine($"<h2 onclick=\"this.nextElementSibling.classList.toggle('hidden')\">{cat.ToUpper()} ({ids.Count})</h2>");
                html.AppendLine("<div class='grid'>");

                foreach (var id in ids)
                {
                    string key = $"{cat}/{id}";
                    string desc = GetCardDescription(cat, id);
                    html.AppendLine($"<div class='card' data-name='{key}' id='card-{cat}-{id}'>");
                    html.AppendLine($"<img src='{cat}/{id}.png' alt='{id}' loading='lazy' onclick=\"openLightbox('{cat}/{id}.png','{id}','{cat}')\">");
                    html.AppendLine($"<div class='id'>{id}</div>");
                    html.AppendLine($"<div class='name'>{desc}</div>");
                    html.AppendLine($"<div class='flag-row'>");
                    html.AppendLine($"<input type='checkbox' id='flag-{cat}-{id}' onchange=\"toggleFlag('{key}', this.checked)\">");
                    html.AppendLine($"<label for='flag-{cat}-{id}'>Flag Issue</label>");
                    html.AppendLine($"</div>");
                    html.AppendLine($"<div class='note-box'>");
                    html.AppendLine($"<textarea placeholder='Describe the issue...' oninput=\"saveNote('{key}', this.value)\"></textarea>");
                    html.AppendLine($"</div>");
                    html.AppendLine("</div>");
                }

                html.AppendLine("</div></div>");
            }

            // Scripts
            html.AppendLine("<script>");
            // State
            html.AppendLine("let allCards = [], currentIdx = -1, flaggedOnly = false;");
            html.AppendLine("let issues = JSON.parse(localStorage.getItem('characterIssues') || '{}');");
            // Init — restore saved flags/notes on load
            html.AppendLine("document.addEventListener('DOMContentLoaded', () => {");
            html.AppendLine("  allCards = [...document.querySelectorAll('.card')];");
            html.AppendLine("  for (let [key, data] of Object.entries(issues)) {");
            html.AppendLine("    let card = allCards.find(c => c.dataset.name === key);");
            html.AppendLine("    if (!card) continue;");
            html.AppendLine("    if (data.flagged) {");
            html.AppendLine("      card.classList.add('flagged');");
            html.AppendLine("      card.querySelector('input[type=checkbox]').checked = true;");
            html.AppendLine("    }");
            html.AppendLine("    if (data.note) card.querySelector('textarea').value = data.note;");
            html.AppendLine("  }");
            html.AppendLine("  updateIssueCount();");
            html.AppendLine("});");
            // Save helper
            html.AppendLine("function saveIssues() { localStorage.setItem('characterIssues', JSON.stringify(issues)); }");
            // Toggle flag on a card
            html.AppendLine("function toggleFlag(key, checked) {");
            html.AppendLine("  if (!issues[key]) issues[key] = {};");
            html.AppendLine("  issues[key].flagged = checked;");
            html.AppendLine("  let card = allCards.find(c => c.dataset.name === key);");
            html.AppendLine("  if (card) card.classList.toggle('flagged', checked);");
            html.AppendLine("  if (!checked && !issues[key].note) delete issues[key];");
            html.AppendLine("  saveIssues(); updateIssueCount();");
            html.AppendLine("}");
            // Save note text
            html.AppendLine("function saveNote(key, text) {");
            html.AppendLine("  if (!issues[key]) issues[key] = {};");
            html.AppendLine("  issues[key].note = text;");
            html.AppendLine("  saveIssues();");
            html.AppendLine("}");
            // Count display
            html.AppendLine("function updateIssueCount() {");
            html.AppendLine("  let count = Object.values(issues).filter(i => i.flagged).length;");
            html.AppendLine("  document.getElementById('issueCount').textContent = count + ' issue' + (count !== 1 ? 's' : '');");
            html.AppendLine("}");
            // Show flagged only toggle
            html.AppendLine("function toggleFlaggedOnly() {");
            html.AppendLine("  flaggedOnly = !flaggedOnly;");
            html.AppendLine("  let btn = document.getElementById('flaggedOnlyBtn');");
            html.AppendLine("  btn.classList.toggle('active', flaggedOnly);");
            html.AppendLine("  allCards.forEach(c => {");
            html.AppendLine("    if (flaggedOnly && !c.classList.contains('flagged')) c.style.display = 'none';");
            html.AppendLine("    else c.style.display = '';");
            html.AppendLine("  });");
            html.AppendLine("}");
            // Export issues to clipboard as formatted list
            html.AppendLine("function exportIssues() {");
            html.AppendLine("  let lines = ['# Character Review Issues', ''];");
            html.AppendLine("  let flagged = Object.entries(issues).filter(([k,v]) => v.flagged).sort((a,b) => a[0].localeCompare(b[0]));");
            html.AppendLine("  if (flagged.length === 0) { showToast('No flagged issues'); return; }");
            html.AppendLine("  let currentCat = '';");
            html.AppendLine("  flagged.forEach(([key, data]) => {");
            html.AppendLine("    let [cat, id] = key.split('/');");
            html.AppendLine("    if (cat !== currentCat) { currentCat = cat; lines.push('## ' + cat.toUpperCase()); }");
            html.AppendLine("    let note = data.note ? data.note.trim() : '(no description)';");
            html.AppendLine("    lines.push('- **' + id + '**: ' + note);");
            html.AppendLine("  });");
            html.AppendLine("  lines.push('', '---', 'Total: ' + flagged.length + ' issues');");
            html.AppendLine("  let text = lines.join('\\n');");
            html.AppendLine("  navigator.clipboard.writeText(text).then(() => showToast('Copied ' + flagged.length + ' issues to clipboard'));");
            html.AppendLine("}");
            // Clear all flags
            html.AppendLine("function clearAllFlags() {");
            html.AppendLine("  if (!confirm('Clear all flagged issues?')) return;");
            html.AppendLine("  issues = {};");
            html.AppendLine("  saveIssues();");
            html.AppendLine("  allCards.forEach(c => { c.classList.remove('flagged'); c.querySelector('input[type=checkbox]').checked = false; c.querySelector('textarea').value = ''; });");
            html.AppendLine("  updateIssueCount();");
            html.AppendLine("}");
            // Toast notification
            html.AppendLine("function showToast(msg) {");
            html.AppendLine("  let t = document.getElementById('toast'); t.textContent = msg; t.style.display = 'block';");
            html.AppendLine("  setTimeout(() => t.style.display = 'none', 3000);");
            html.AppendLine("}");
            // Text filter
            html.AppendLine("function filterAssets(text) {");
            html.AppendLine("  text = text.toLowerCase();");
            html.AppendLine("  allCards.forEach(c => {");
            html.AppendLine("    let match = c.dataset.name.toLowerCase().includes(text);");
            html.AppendLine("    if (flaggedOnly && !c.classList.contains('flagged')) match = false;");
            html.AppendLine("    c.style.display = match ? '' : 'none';");
            html.AppendLine("  });");
            html.AppendLine("}");
            // Lightbox
            html.AppendLine("function openLightbox(src, title, cat) {");
            html.AppendLine("  document.getElementById('lb-img').src = src;");
            html.AppendLine("  document.getElementById('lb-title').textContent = title;");
            html.AppendLine("  document.getElementById('lb-cat').textContent = cat;");
            html.AppendLine("  document.getElementById('lightbox').classList.add('active');");
            html.AppendLine("  currentIdx = allCards.findIndex(c => c.dataset.name === cat + '/' + title);");
            html.AppendLine("}");
            html.AppendLine("function closeLightbox() { document.getElementById('lightbox').classList.remove('active'); }");
            html.AppendLine("document.addEventListener('keydown', e => {");
            html.AppendLine("  if (!document.getElementById('lightbox').classList.contains('active')) return;");
            html.AppendLine("  if (e.key === 'Escape') closeLightbox();");
            html.AppendLine("  if (e.key === 'ArrowRight' || e.key === 'ArrowDown') { navigateLB(1); e.preventDefault(); }");
            html.AppendLine("  if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') { navigateLB(-1); e.preventDefault(); }");
            html.AppendLine("});");
            html.AppendLine("function navigateLB(dir) {");
            html.AppendLine("  if (allCards.length === 0) return;");
            html.AppendLine("  currentIdx = (currentIdx + dir + allCards.length) % allCards.length;");
            html.AppendLine("  let c = allCards[currentIdx];");
            html.AppendLine("  let parts = c.dataset.name.split('/');");
            html.AppendLine("  openLightbox(c.dataset.name + '.png', parts[1], parts[0]);");
            html.AppendLine("}");
            html.AppendLine("</script>");

            html.AppendLine("<div class='toast' id='toast'></div>");
            html.AppendLine("</body></html>");

            using var file = FileAccess.Open("user://character_catalog/index.html", FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(html.ToString());
                GD.Print($"[CharacterCatalog] Gallery saved to {_outputDir}/index.html");
            }
        }
    }
}
