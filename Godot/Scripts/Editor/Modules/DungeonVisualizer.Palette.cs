using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    public partial class DungeonVisualizer
    {
        // === Palette state ===
        private VBoxContainer _palettePanel;
        private ScrollContainer _paletteScroll;
        private VBoxContainer _paletteList;
        private HBoxContainer _categoryBar;
        private LineEdit _paletteSearch;
        private string _paletteCategory = "Props";
        private string _selectedAssetId;
        private string _selectedAssetCategory; // ModelLibrary category or "vfx"/"audio"/"trigger"
        private string _selectedAssetType;     // "model", "vfx", "audio", "trigger", "collision"
        private Label _placingLabel;

        // Map collapse
        private bool _mapCollapsed;
        private Control _mapContainer;
        private Button _mapToggleBtn;

        // Category → ModelLibrary category mapping
        private static readonly Dictionary<string, string> _paletteCategoryMap = new()
        {
            { "Props",   "prop" },
            { "Walls",   "wall" },
            { "Floors",  "floor" },
            { "Doors",   "door" },
        };

        // Hardcoded VFX list — key VfxFactory effects
        private static readonly string[] _vfxList = new[]
        {
            "TorchFire", "AmbientParticles", "Portal", "HitParticles",
            "DeathParticles", "LootBurst", "CelebrationBurst", "HealParticles",
            "PoisonCloud", "FreezeBurst", "ElectricSparks", "MuzzleFlash",
            "ArcaneCircle", "ShockwaveRing", "MusicNotes", "DashTrail",
            "AuraRing", "StunIndicator", "GroundSparks", "SadPuff",
        };

        // Hardcoded audio list
        private static readonly string[] _audioList = new[]
        {
            "ambience_hum", "drip", "wind", "torch_crackle",
            "metal_creak", "electricity_buzz", "steam_hiss",
            "distant_rumble", "chain_rattle", "water_flow",
            "alarm_beep", "fan_whir", "pipe_clank",
            "heartbeat", "whisper",
        };

        // Trigger types
        private static readonly string[] _triggerList = new[]
        {
            "Dialogue", "SpawnEnemy", "PlaySound",
        };

        private void BuildPalettePanel(VBoxContainer leftPanel)
        {
            // Map toggle button
            _mapToggleBtn = EditorStyles.MakeButton("Map [collapse]", EditorStyles.FontTiny, EditorStyles.TextSecondary);
            _mapToggleBtn.Pressed += ToggleMapCollapse;
            leftPanel.AddChild(_mapToggleBtn);

            // Map container (collapsible)
            _mapContainer = new VBoxContainer();
            _mapContainer.CustomMinimumSize = new Vector2(0, 200);

            _mapInfo = EditorStyles.MakeLabel("Click Generate to create a dungeon", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            _mapContainer.AddChild(_mapInfo);

            _mapPanel = new Control();
            _mapPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            _mapPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _mapPanel.CustomMinimumSize = new Vector2(240, 200);
            _mapPanel.Draw += DrawMap;
            _mapPanel.GuiInput += OnMapClick;
            _mapPanel.MouseFilter = Control.MouseFilterEnum.Stop;
            _mapContainer.AddChild(_mapPanel);

            _roomInfo = EditorStyles.MakeLabel("", EditorStyles.FontSmall, EditorStyles.TextMuted);
            _mapContainer.AddChild(_roomInfo);

            leftPanel.AddChild(_mapContainer);
            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Palette header
            leftPanel.AddChild(EditorStyles.MakeLabel("Asset Palette", EditorStyles.FontSmall, AccentColor));

            // Category tabs
            _categoryBar = new HBoxContainer();
            _categoryBar.AddThemeConstantOverride("separation", 2);
            var categories = new[] { "Props", "Walls", "Floors", "Doors", "VFX", "Audio", "Triggers" };
            foreach (var cat in categories)
            {
                var btn = EditorStyles.MakeButton(cat, EditorStyles.FontTiny);
                btn.CustomMinimumSize = new Vector2(0, 22);
                btn.ToggleMode = true;
                btn.ButtonPressed = cat == _paletteCategory;
                var captured = cat;
                btn.Pressed += () => SwitchPaletteCategory(captured);
                _categoryBar.AddChild(btn);
            }
            leftPanel.AddChild(_categoryBar);

            // Search
            _paletteSearch = EditorStyles.MakeLineEdit("Search...", EditorStyles.FontSmall);
            _paletteSearch.TextChanged += _ => RefreshPaletteList();
            leftPanel.AddChild(_paletteSearch);

            // Scrollable asset list
            _paletteScroll = new ScrollContainer();
            _paletteScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _paletteScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _paletteList = new VBoxContainer();
            _paletteList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _paletteList.AddThemeConstantOverride("separation", 1);
            _paletteScroll.AddChild(_paletteList);
            leftPanel.AddChild(_paletteScroll);

            RefreshPaletteList();
        }

        private void ToggleMapCollapse()
        {
            _mapCollapsed = !_mapCollapsed;
            _mapContainer.Visible = !_mapCollapsed;
            _mapToggleBtn.Text = _mapCollapsed ? "Map [expand]" : "Map [collapse]";
        }

        private void SwitchPaletteCategory(string category)
        {
            _paletteCategory = category;

            // Update toggle states
            foreach (var child in _categoryBar.GetChildren())
            {
                if (child is Button btn)
                    btn.ButtonPressed = btn.Text == category;
            }

            _paletteSearch.Text = "";
            RefreshPaletteList();
        }

        private void RefreshPaletteList()
        {
            foreach (var child in _paletteList.GetChildren())
                if (child is Node n) n.QueueFree();

            string filter = _paletteSearch?.Text?.ToLower() ?? "";
            string[] items;
            string assetType;
            string assetCategoryKey;

            if (_paletteCategoryMap.TryGetValue(_paletteCategory, out var modelCat))
            {
                items = ModelLibrary.GetCategoryIds(modelCat);
                assetType = "model";
                assetCategoryKey = modelCat;
            }
            else if (_paletteCategory == "VFX")
            {
                items = _vfxList;
                assetType = "vfx";
                assetCategoryKey = "vfx";
            }
            else if (_paletteCategory == "Audio")
            {
                items = _audioList;
                assetType = "audio";
                assetCategoryKey = "audio";
            }
            else if (_paletteCategory == "Triggers")
            {
                items = _triggerList;
                assetType = "trigger";
                assetCategoryKey = "trigger";
            }
            else
            {
                items = Array.Empty<string>();
                assetType = "model";
                assetCategoryKey = "prop";
            }

            foreach (var id in items)
            {
                if (filter.Length > 0 && !id.ToLower().Contains(filter))
                    continue;

                var btn = new Button();
                btn.Text = id;
                btn.Alignment = HorizontalAlignment.Left;
                btn.AddThemeFontSizeOverride("font_size", EditorStyles.FontTiny);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.CustomMinimumSize = new Vector2(0, 20);
                btn.ClipText = true;
                btn.ToggleMode = true;
                btn.ButtonPressed = id == _selectedAssetId && assetType == _selectedAssetType;

                var flatStyle = EditorStyles.MakeFlat(new Color(0, 0, 0, 0));
                btn.AddThemeStyleboxOverride("normal", flatStyle);
                btn.AddThemeStyleboxOverride("hover", EditorStyles.MakeFlat(EditorStyles.BgHover));
                btn.AddThemeStyleboxOverride("pressed", EditorStyles.MakeFlat(EditorStyles.BgSelected));

                var capturedId = id;
                var capturedType = assetType;
                var capturedCat = assetCategoryKey;
                btn.Pressed += () => SelectPaletteAsset(capturedId, capturedType, capturedCat);
                _paletteList.AddChild(btn);
            }

            if (items.Length == 0)
            {
                _paletteList.AddChild(EditorStyles.MakeLabel("(no assets)", EditorStyles.FontTiny, EditorStyles.TextMuted));
            }
        }

        private void SelectPaletteAsset(string assetId, string type, string category)
        {
            _selectedAssetId = assetId;
            _selectedAssetType = type;
            _selectedAssetCategory = category;

            // Auto-switch to Place tool
            SetEditorTool(EditorTool.Place);

            // Update placing label
            if (_placingLabel != null)
                _placingLabel.Text = $"Placing: {assetId}";

            // Update toggle states in palette list
            foreach (var child in _paletteList.GetChildren())
            {
                if (child is Button btn)
                    btn.ButtonPressed = btn.Text == assetId;
            }

            // Create ghost preview
            CreateGhostPreview();
        }
    }
}
