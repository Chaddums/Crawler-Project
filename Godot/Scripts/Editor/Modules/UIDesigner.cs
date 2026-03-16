using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// UI/UX Designer — editor module for viewing, editing, and creating UI screens.
    /// Left panel: screen/element tree. Center: live preview. Right: property inspector.
    /// Saves to res://Data/ui_config.json.
    /// </summary>
    public partial class UIDesigner : EditorPanel
    {
        private const string CONFIG_PATH = "res://Data/ui_config.json";

        public override string PanelName => "UI/UX";
        public override Color AccentColor => EditorStyles.AccentUI;

        // State
        private Dictionary<string, object> _config;
        private string _selectedScreen;
        private string _selectedElement;

        // Left panel
        private VBoxContainer _screenList;
        private VBoxContainer _elementList;
        private LineEdit _searchFilter;

        // Center panel — preview
        private SubViewportContainer _previewContainer;
        private SubViewport _previewViewport;
        private Control _previewRoot;

        // Layout mode
        private bool _layoutMode;
        private Button _layoutToggleBtn;
        private CheckButton _gridSnapCheck;
        private SpinBox _gridSizeSpin;
        private readonly Dictionary<string, DraggableHUDElement> _draggables = new();

        // Right panel — inspector
        private Label _inspectorTitle;
        private VBoxContainer _inspectorFields;
        private ScrollContainer _inspectorScroll;

        // New element creation
        private OptionButton _newElementType;
        private LineEdit _newElementName;

        // ═══════════════════════════════════════════════════════════════
        //  SCREEN DEFINITIONS — all editable UI screens and their elements
        // ═══════════════════════════════════════════════════════════════

        private static readonly Dictionary<string, ScreenDef> ScreenDefinitions = new()
        {
            ["HUD"] = new ScreenDef("HUD", "In-game heads-up display", new[]
            {
                El("HealthBar", "Health/Scrap bar", new PropDef[]
                {
                    Prop("AccentColor", PropType.Color, "0.7,0.45,0.15"),
                    Prop("FillColorHigh", PropType.Color, "0.2,0.8,0.2"),
                    Prop("FillColorMid", PropType.Color, "0.9,0.7,0.1"),
                    Prop("FillColorLow", PropType.Color, "0.8,0.15,0.15"),
                    Prop("BgColor", PropType.Color, "0.04,0.04,0.06"),
                    Prop("Width", PropType.Float, "150", 50, 400),
                    Prop("Height", PropType.Float, "220", 100, 500),
                    Prop("EdgeMargin", PropType.Float, "30", 0, 100),
                    Prop("BottomMargin", PropType.Float, "28", 0, 100),
                    Prop("BorderWidth", PropType.Int, "3", 0, 10),
                    Prop("HeaderText", PropType.String, "SCRAP"),
                    Prop("FontSize", PropType.Int, "11", 8, 24),
                    Prop("BgOpacity", PropType.Float, "0.93", 0, 1),
                    Prop("PositionX", PropType.Float, "30", 0, 1920),
                    Prop("PositionY", PropType.Float, "832", 0, 1080),
                }),
                El("BatteryBar", "Mana/Battery bar", new PropDef[]
                {
                    Prop("AccentColor", PropType.Color, "0.15,0.45,0.95"),
                    Prop("FillColor", PropType.Color, "0.2,0.4,0.9"),
                    Prop("BgColor", PropType.Color, "0.04,0.04,0.06"),
                    Prop("Width", PropType.Float, "150", 50, 400),
                    Prop("Height", PropType.Float, "220", 100, 500),
                    Prop("EdgeMargin", PropType.Float, "30", 0, 100),
                    Prop("BottomMargin", PropType.Float, "28", 0, 100),
                    Prop("BorderWidth", PropType.Int, "3", 0, 10),
                    Prop("HeaderText", PropType.String, "BATTERY"),
                    Prop("FontSize", PropType.Int, "11", 8, 24),
                    Prop("BgOpacity", PropType.Float, "0.93", 0, 1),
                    Prop("PositionX", PropType.Float, "1740", 0, 1920),
                    Prop("PositionY", PropType.Float, "832", 0, 1080),
                }),
                El("XPBar", "Experience bar", new PropDef[]
                {
                    Prop("FillColor", PropType.Color, "0.6,0.3,0.8"),
                    Prop("BgColor", PropType.Color, "0.15,0.1,0.2"),
                    Prop("Height", PropType.Float, "8", 4, 30),
                    Prop("BottomMargin", PropType.Float, "6", 0, 50),
                    Prop("FontSize", PropType.Int, "12", 8, 20),
                    Prop("LevelPrefix", PropType.String, "LVL"),
                    Prop("PositionX", PropType.Float, "190", 0, 1920),
                    Prop("PositionY", PropType.Float, "1068", 0, 1080),
                }),
                El("BuffStrip", "Status effect indicator strip", new PropDef[]
                {
                    Prop("IconSize", PropType.Float, "32", 16, 64),
                    Prop("MaxVisible", PropType.Int, "8", 4, 16),
                    Prop("Spacing", PropType.Float, "4", 0, 16),
                    Prop("BgColor", PropType.Color, "0.1,0.1,0.12"),
                    Prop("PositionX", PropType.Float, "30", 0, 1920),
                    Prop("PositionY", PropType.Float, "780", 0, 1080),
                }),
                El("DashIndicator", "Dash charge pips", new PropDef[]
                {
                    Prop("ActiveColor", PropType.Color, "0.3,0.9,1.0"),
                    Prop("InactiveColor", PropType.Color, "0.3,0.3,0.35"),
                    Prop("LabelText", PropType.String, "DASH"),
                    Prop("FontSize", PropType.Int, "10", 8, 16),
                    Prop("PositionX", PropType.Float, "880", 0, 1920),
                    Prop("PositionY", PropType.Float, "1056", 0, 1080),
                }),
                El("SectorLabel", "Sector/Area text in top-right", new PropDef[]
                {
                    Prop("TextColor", PropType.Color, "0.7,0.65,0.5"),
                    Prop("FontSize", PropType.Int, "18", 10, 28),
                    Prop("PositionX", PropType.Float, "1600", 0, 1920),
                    Prop("PositionY", PropType.Float, "20", 0, 200),
                }),
                El("Minimap", "Top-right minimap", new PropDef[]
                {
                    Prop("Width", PropType.Float, "200", 100, 400),
                    Prop("Height", PropType.Float, "200", 100, 400),
                    Prop("PositionX", PropType.Float, "1700", 0, 1920),
                    Prop("PositionY", PropType.Float, "60", 0, 400),
                    Prop("RoomColor", PropType.Color, "0.3,0.3,0.35"),
                    Prop("PlayerColor", PropType.Color, "0.2,0.8,0.3"),
                    Prop("EnemyColor", PropType.Color, "0.9,0.2,0.2"),
                    Prop("BossColor", PropType.Color, "1.0,0.4,0.1"),
                    Prop("BgColor", PropType.Color, "0.05,0.05,0.08"),
                    Prop("BgOpacity", PropType.Float, "0.85", 0, 1),
                }),
                El("LiftTimer", "Sector countdown timer", new PropDef[]
                {
                    Prop("TextColor", PropType.Color, "0.9,0.85,0.7"),
                    Prop("WarningColor", PropType.Color, "1.0,0.3,0.2"),
                    Prop("WarningThreshold", PropType.Float, "60", 10, 300),
                    Prop("FontSize", PropType.Int, "20", 12, 32),
                    Prop("PositionX", PropType.Float, "20", 0, 200),
                    Prop("PositionY", PropType.Float, "20", 0, 200),
                }),
                El("ConsumableList", "Health/Mana potion indicators", new PropDef[]
                {
                    Prop("FontSize", PropType.Int, "12", 8, 18),
                    Prop("HealthKeyLabel", PropType.String, "[Q]"),
                    Prop("ManaKeyLabel", PropType.String, "[F]"),
                    Prop("TextColor", PropType.Color, "0.8,0.8,0.8"),
                    Prop("PositionX", PropType.Float, "30", 0, 1920),
                    Prop("PositionY", PropType.Float, "730", 0, 1080),
                }),
                El("LootBoxTracker", "Collected loot box counter", new PropDef[]
                {
                    Prop("PositionX", PropType.Float, "20", 0, 200),
                    Prop("PositionY", PropType.Float, "95", 0, 300),
                    Prop("FontSize", PropType.Int, "12", 8, 18),
                    Prop("IconColor", PropType.Color, "0.9,0.7,0.2"),
                }),
            }),

            ["Inventory"] = new ScreenDef("Inventory", "Bag + equipment overlay (I key)", new[]
            {
                El("Panel", "Main inventory window", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.06,0.06,0.1"),
                    Prop("BorderColor", PropType.Color, "0.6,0.5,0.2"),
                    Prop("BorderWidth", PropType.Int, "2", 0, 6),
                    Prop("PositionX", PropType.Float, "360", 0, 1000),
                    Prop("PositionY", PropType.Float, "140", 0, 600),
                    Prop("Width", PropType.Float, "1200", 400, 1920),
                    Prop("Height", PropType.Float, "750", 300, 1080),
                    Prop("BgOpacity", PropType.Float, "0.97", 0, 1),
                    Prop("Title", PropType.String, "SALVAGE INVENTORY"),
                    Prop("TitleFontSize", PropType.Int, "20", 12, 32),
                    Prop("TitleColor", PropType.Color, "0.9,0.7,0.2"),
                }),
                El("DimOverlay", "Background dim when open", new PropDef[]
                {
                    Prop("Color", PropType.Color, "0,0,0"),
                    Prop("Opacity", PropType.Float, "0.4", 0, 1),
                }),
                El("BagSlot", "Individual bag grid slot", new PropDef[]
                {
                    Prop("Size", PropType.Float, "64", 32, 128),
                    Prop("Spacing", PropType.Float, "4", 0, 16),
                    Prop("Columns", PropType.Int, "6", 4, 10),
                    Prop("BgColor", PropType.Color, "0.12,0.12,0.16"),
                    Prop("BorderColor", PropType.Color, "0.3,0.3,0.35"),
                    Prop("HoverColor", PropType.Color, "0.4,0.35,0.15"),
                }),
                El("EquipSlot", "Equipment slot", new PropDef[]
                {
                    Prop("Size", PropType.Float, "72", 48, 128),
                    Prop("BgColor", PropType.Color, "0.08,0.1,0.16"),
                    Prop("BorderColor", PropType.Color, "0.4,0.5,0.7"),
                    Prop("LabelFontSize", PropType.Int, "9", 7, 14),
                    Prop("LabelColor", PropType.Color, "0.5,0.5,0.55"),
                }),
                El("Tooltip", "Item tooltip popup", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.05,0.05,0.08"),
                    Prop("BorderColor", PropType.Color, "0.5,0.5,0.55"),
                    Prop("Width", PropType.Float, "280", 180, 500),
                    Prop("NameFontSize", PropType.Int, "16", 10, 24),
                    Prop("DescFontSize", PropType.Int, "12", 8, 18),
                    Prop("BgOpacity", PropType.Float, "0.97", 0, 1),
                }),
            }),

            ["PassiveTree"] = new ScreenDef("PassiveTree", "POE-style hex passive tree (P key)", new[]
            {
                El("Panel", "Passive tree overlay", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.04,0.04,0.06"),
                    Prop("BgOpacity", PropType.Float, "0.95", 0, 1),
                    Prop("Title", PropType.String, "UPGRADE MATRIX"),
                    Prop("TitleFontSize", PropType.Int, "22", 12, 36),
                    Prop("TitleColor", PropType.Color, "0.4,0.75,1.0"),
                }),
                El("Node", "Passive tree node", new PropDef[]
                {
                    Prop("Radius", PropType.Float, "18", 8, 40),
                    Prop("LockedColor", PropType.Color, "0.2,0.2,0.25"),
                    Prop("AvailableColor", PropType.Color, "0.3,0.5,0.7"),
                    Prop("AllocatedColor", PropType.Color, "0.4,0.75,1.0"),
                    Prop("KeystoneRadius", PropType.Float, "24", 12, 48),
                    Prop("KeystoneColor", PropType.Color, "0.9,0.6,0.2"),
                    Prop("BorderWidth", PropType.Float, "2", 1, 6),
                }),
                El("Connection", "Lines between nodes", new PropDef[]
                {
                    Prop("LineWidth", PropType.Float, "2", 1, 6),
                    Prop("InactiveColor", PropType.Color, "0.15,0.15,0.2"),
                    Prop("ActiveColor", PropType.Color, "0.4,0.75,1.0"),
                }),
                El("PointsDisplay", "Available points counter", new PropDef[]
                {
                    Prop("FontSize", PropType.Int, "16", 10, 24),
                    Prop("TextColor", PropType.Color, "0.9,0.85,0.5"),
                    Prop("Label", PropType.String, "POINTS AVAILABLE:"),
                }),
                El("DimOverlay", "Background dim", new PropDef[]
                {
                    Prop("Color", PropType.Color, "0,0,0"),
                    Prop("Opacity", PropType.Float, "0.7", 0, 1),
                }),
            }),

            ["PauseMenu"] = new ScreenDef("PauseMenu", "Pause menu overlay (ESC)", new[]
            {
                El("Panel", "Main pause panel", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.06,0.06,0.1"),
                    Prop("BgOpacity", PropType.Float, "0.95", 0, 1),
                    Prop("BorderColor", PropType.Color, "0.5,0.4,0.2"),
                    Prop("BorderWidth", PropType.Int, "2", 0, 6),
                    Prop("Width", PropType.Float, "400", 200, 800),
                    Prop("Title", PropType.String, "PAUSED"),
                    Prop("TitleFontSize", PropType.Int, "28", 16, 40),
                    Prop("TitleColor", PropType.Color, "0.9,0.7,0.2"),
                }),
                El("Button", "Menu buttons (Resume, Save, etc.)", new PropDef[]
                {
                    Prop("FontSize", PropType.Int, "18", 12, 28),
                    Prop("Height", PropType.Float, "48", 30, 80),
                    Prop("Spacing", PropType.Float, "8", 0, 20),
                    Prop("NormalColor", PropType.Color, "0.8,0.8,0.82"),
                    Prop("HoverColor", PropType.Color, "0.9,0.7,0.2"),
                    Prop("QuitColor", PropType.Color, "0.8,0.3,0.3"),
                }),
                El("DimOverlay", "Background dim", new PropDef[]
                {
                    Prop("Color", PropType.Color, "0,0,0"),
                    Prop("Opacity", PropType.Float, "0.6", 0, 1),
                }),
            }),

            ["CharacterCreation"] = new ScreenDef("CharacterCreation", "Frame selection at game start", new[]
            {
                El("Panel", "Main selection panel", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.05,0.05,0.08"),
                    Prop("Title", PropType.String, "CHOOSE YOUR FRAME"),
                    Prop("TitleFontSize", PropType.Int, "28", 16, 40),
                    Prop("TitleColor", PropType.Color, "0.9,0.7,0.2"),
                }),
                El("FrameCard", "Individual bot frame selection card", new PropDef[]
                {
                    Prop("Width", PropType.Float, "160", 100, 300),
                    Prop("Height", PropType.Float, "200", 120, 400),
                    Prop("BgColor", PropType.Color, "0.08,0.08,0.12"),
                    Prop("SelectedBorderColor", PropType.Color, "0.9,0.7,0.2"),
                    Prop("HoverBorderColor", PropType.Color, "0.5,0.5,0.6"),
                    Prop("BorderWidth", PropType.Int, "2", 1, 6),
                    Prop("NameFontSize", PropType.Int, "14", 10, 22),
                    Prop("DescFontSize", PropType.Int, "11", 8, 16),
                }),
                El("StatsGrid", "Attribute display grid", new PropDef[]
                {
                    Prop("FontSize", PropType.Int, "12", 8, 18),
                    Prop("LabelColor", PropType.Color, "0.6,0.6,0.65"),
                    Prop("ValueColor", PropType.Color, "0.9,0.9,0.92"),
                }),
                El("EnterButton", "Enter Arena button", new PropDef[]
                {
                    Prop("Text", PropType.String, "ENTER THE ARENA"),
                    Prop("FontSize", PropType.Int, "22", 14, 36),
                    Prop("Color", PropType.Color, "0.9,0.7,0.2"),
                }),
            }),

            ["MainMenu"] = new ScreenDef("MainMenu", "Title screen", new[]
            {
                El("Background", "Background panel", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.03,0.03,0.05"),
                }),
                El("Title", "Game title text", new PropDef[]
                {
                    Prop("Text", PropType.String, "JUNKBOT ARENA"),
                    Prop("FontSize", PropType.Int, "48", 24, 72),
                    Prop("Color", PropType.Color, "0.9,0.7,0.2"),
                    Prop("Subtitle", PropType.String, "Scrap. Fight. Survive."),
                    Prop("SubtitleFontSize", PropType.Int, "18", 10, 28),
                    Prop("SubtitleColor", PropType.Color, "0.6,0.55,0.4"),
                }),
                El("Button", "Menu buttons", new PropDef[]
                {
                    Prop("FontSize", PropType.Int, "20", 12, 32),
                    Prop("Width", PropType.Float, "300", 150, 600),
                    Prop("Height", PropType.Float, "50", 30, 80),
                    Prop("Spacing", PropType.Float, "12", 0, 30),
                    Prop("NormalColor", PropType.Color, "0.8,0.8,0.82"),
                    Prop("HoverColor", PropType.Color, "0.9,0.7,0.2"),
                }),
            }),

            ["BossHealthBar"] = new ScreenDef("BossHealthBar", "Full-width boss health bar", new[]
            {
                El("Bar", "Health bar", new PropDef[]
                {
                    Prop("FillColor", PropType.Color, "0.8,0.15,0.15"),
                    Prop("BgColor", PropType.Color, "0.1,0.1,0.12"),
                    Prop("BorderColor", PropType.Color, "0.6,0.2,0.2"),
                    Prop("Height", PropType.Float, "24", 12, 48),
                    Prop("TopMargin", PropType.Float, "40", 10, 100),
                    Prop("SideMargin", PropType.Float, "200", 50, 500),
                }),
                El("NameLabel", "Boss name text", new PropDef[]
                {
                    Prop("FontSize", PropType.Int, "18", 12, 28),
                    Prop("TextColor", PropType.Color, "0.9,0.3,0.2"),
                }),
                El("PhaseIndicator", "Phase dots", new PropDef[]
                {
                    Prop("ActiveColor", PropType.Color, "1.0,0.4,0.1"),
                    Prop("InactiveColor", PropType.Color, "0.3,0.3,0.35"),
                    Prop("DotSize", PropType.Float, "8", 4, 16),
                }),
            }),

            ["LootBoxCeremony"] = new ScreenDef("LootBoxCeremony", "Loot box opening animation", new[]
            {
                El("Panel", "Ceremony overlay", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.02,0.02,0.04"),
                    Prop("BgOpacity", PropType.Float, "0.95", 0, 1),
                }),
                El("Box", "Loot box visual", new PropDef[]
                {
                    Prop("Size", PropType.Float, "120", 60, 200),
                    Prop("ShakeIntensity", PropType.Float, "8", 0, 30),
                    Prop("ShakeDuration", PropType.Float, "1.5", 0.5f, 5),
                }),
                El("ItemReveal", "Revealed item cards", new PropDef[]
                {
                    Prop("CardWidth", PropType.Float, "160", 80, 300),
                    Prop("CardHeight", PropType.Float, "200", 100, 400),
                    Prop("RevealDelay", PropType.Float, "0.3", 0.1f, 1),
                    Prop("BgColor", PropType.Color, "0.08,0.08,0.12"),
                    Prop("BorderWidth", PropType.Int, "2", 0, 6),
                }),
                El("TierColors", "Rarity tier colors", new PropDef[]
                {
                    Prop("Common", PropType.Color, "0.6,0.6,0.6"),
                    Prop("Uncommon", PropType.Color, "0.3,0.8,0.3"),
                    Prop("Rare", PropType.Color, "0.3,0.5,1.0"),
                    Prop("Epic", PropType.Color, "0.7,0.3,0.9"),
                    Prop("Legendary", PropType.Color, "1.0,0.6,0.1"),
                }),
            }),

            ["DamageNumbers"] = new ScreenDef("DamageNumbers", "Floating combat numbers", new[]
            {
                El("Text", "Damage/heal number text", new PropDef[]
                {
                    Prop("DamageColor", PropType.Color, "1.0,0.3,0.2"),
                    Prop("HealColor", PropType.Color, "0.2,0.9,0.3"),
                    Prop("CritColor", PropType.Color, "1.0,0.8,0.1"),
                    Prop("FontSize", PropType.Int, "18", 10, 32),
                    Prop("CritFontSize", PropType.Int, "26", 14, 40),
                    Prop("FloatSpeed", PropType.Float, "60", 20, 150),
                    Prop("Duration", PropType.Float, "1.0", 0.3f, 3),
                    Prop("SpreadX", PropType.Float, "30", 0, 80),
                }),
            }),

            ["SectorTransition"] = new ScreenDef("SectorTransition", "Sector change screen", new[]
            {
                El("Panel", "Transition overlay", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.02,0.02,0.04"),
                    Prop("FadeDuration", PropType.Float, "0.5", 0.1f, 2),
                }),
                El("Text", "Sector announce text", new PropDef[]
                {
                    Prop("FontSize", PropType.Int, "36", 20, 56),
                    Prop("Color", PropType.Color, "0.9,0.7,0.2"),
                    Prop("SubtextFontSize", PropType.Int, "16", 10, 24),
                    Prop("SubtextColor", PropType.Color, "0.6,0.6,0.65"),
                }),
            }),

            ["CharacterSheet"] = new ScreenDef("CharacterSheet", "Player stats overlay (C key)", new[]
            {
                El("Panel", "Stats panel", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.06,0.06,0.1"),
                    Prop("BgOpacity", PropType.Float, "0.97", 0, 1),
                    Prop("BorderColor", PropType.Color, "0.4,0.6,0.9"),
                    Prop("Title", PropType.String, "BOT DIAGNOSTICS"),
                    Prop("TitleFontSize", PropType.Int, "20", 12, 32),
                    Prop("TitleColor", PropType.Color, "0.5,0.7,1.0"),
                    Prop("Width", PropType.Float, "600", 300, 1000),
                    Prop("Height", PropType.Float, "700", 300, 1000),
                }),
                El("StatRow", "Individual stat line", new PropDef[]
                {
                    Prop("LabelFontSize", PropType.Int, "13", 8, 20),
                    Prop("ValueFontSize", PropType.Int, "13", 8, 20),
                    Prop("LabelColor", PropType.Color, "0.6,0.6,0.65"),
                    Prop("ValueColor", PropType.Color, "0.9,0.9,0.92"),
                    Prop("ModifierColor", PropType.Color, "0.3,0.8,0.4"),
                    Prop("NegativeColor", PropType.Color, "0.9,0.3,0.3"),
                }),
            }),

            ["Achievements"] = new ScreenDef("Achievements", "Achievement unlock notifications", new[]
            {
                El("Toast", "Notification toast popup", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.08,0.08,0.12"),
                    Prop("BorderColor", PropType.Color, "0.9,0.7,0.2"),
                    Prop("Width", PropType.Float, "350", 200, 600),
                    Prop("Height", PropType.Float, "80", 50, 150),
                    Prop("TitleFontSize", PropType.Int, "14", 10, 22),
                    Prop("TitleColor", PropType.Color, "0.9,0.7,0.2"),
                    Prop("DescFontSize", PropType.Int, "11", 8, 16),
                    Prop("DescColor", PropType.Color, "0.7,0.7,0.72"),
                    Prop("Duration", PropType.Float, "4.0", 1, 10),
                    Prop("SlideInDuration", PropType.Float, "0.3", 0.1f, 1),
                }),
            }),

            ["AbilityBar"] = new ScreenDef("AbilityBar", "Bottom ability slot bar", new[]
            {
                El("Bar", "Ability bar layout", new PropDef[]
                {
                    Prop("SlotCount", PropType.Int, "6", 4, 10),
                    Prop("SlotSize", PropType.Float, "56", 32, 96),
                    Prop("Spacing", PropType.Float, "6", 0, 20),
                    Prop("BottomMargin", PropType.Float, "12", 0, 50),
                }),
                El("Slot", "Individual ability slot", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.08,0.08,0.12"),
                    Prop("BorderColor", PropType.Color, "0.3,0.3,0.38"),
                    Prop("ReadyBorderColor", PropType.Color, "0.4,0.75,1.0"),
                    Prop("CooldownColor", PropType.Color, "0.15,0.15,0.2"),
                    Prop("CooldownTextSize", PropType.Int, "14", 8, 22),
                    Prop("KeyLabelSize", PropType.Int, "10", 8, 16),
                    Prop("ManaCostColor", PropType.Color, "0.3,0.5,0.9"),
                }),
            }),

            ["VictoryScreen"] = new ScreenDef("VictoryScreen", "Post-boss victory screen", new[]
            {
                El("Panel", "Victory overlay", new PropDef[]
                {
                    Prop("BgColor", PropType.Color, "0.02,0.02,0.04"),
                    Prop("BgOpacity", PropType.Float, "0.95", 0, 1),
                }),
                El("Title", "Victory text", new PropDef[]
                {
                    Prop("Text", PropType.String, "SECTOR CLEAR"),
                    Prop("FontSize", PropType.Int, "40", 20, 60),
                    Prop("Color", PropType.Color, "0.9,0.7,0.2"),
                }),
                El("Stats", "Run statistics display", new PropDef[]
                {
                    Prop("FontSize", PropType.Int, "14", 10, 22),
                    Prop("LabelColor", PropType.Color, "0.6,0.6,0.65"),
                    Prop("ValueColor", PropType.Color, "0.9,0.9,0.92"),
                }),
            }),
        };

        // ═══════════════════════════════════════════════════════════════
        //  BUILD UI
        // ═══════════════════════════════════════════════════════════════

        protected override void BuildUI(VBoxContainer content)
        {
            var split = new HSplitContainer();
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // ═══ LEFT PANEL: Screen & Element tree ═══
            var leftScroll = new ScrollContainer();
            leftScroll.CustomMinimumSize = new Vector2(260, 0);
            leftScroll.SizeFlagsVertical = SizeFlags.ExpandFill;

            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            leftPanel.AddChild(EditorStyles.MakeLabel("UI Screens", EditorStyles.FontHeader, AccentColor));

            _searchFilter = EditorStyles.MakeLineEdit("Filter...");
            _searchFilter.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            _searchFilter.TextChanged += _ => RebuildScreenList();
            leftPanel.AddChild(_searchFilter);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            _screenList = new VBoxContainer();
            _screenList.AddThemeConstantOverride("separation", 2);
            leftPanel.AddChild(_screenList);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // Element list (shown when a screen is selected)
            leftPanel.AddChild(EditorStyles.MakeLabel("Elements", EditorStyles.FontHeader, new Color(0.7f, 0.8f, 1f)));
            _elementList = new VBoxContainer();
            _elementList.AddThemeConstantOverride("separation", 2);
            leftPanel.AddChild(_elementList);

            leftPanel.AddChild(EditorStyles.MakeSeparator());

            // ── New Element Creation ──
            leftPanel.AddChild(EditorStyles.MakeLabel("Add Custom Element", EditorStyles.FontSmall, EditorStyles.TextSecondary));
            _newElementName = EditorStyles.MakeLineEdit("Element name...");
            _newElementName.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            leftPanel.AddChild(_newElementName);

            _newElementType = new OptionButton();
            _newElementType.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
            _newElementType.AddItem("Panel");
            _newElementType.AddItem("Label");
            _newElementType.AddItem("Button");
            _newElementType.AddItem("Bar");
            _newElementType.AddItem("Container");
            _newElementType.AddItem("Icon");
            _newElementType.Selected = 0;
            leftPanel.AddChild(_newElementType);

            var addBtn = EditorStyles.MakeButton("+ Add Element", EditorStyles.FontSmall, EditorStyles.StatusSaved);
            addBtn.Pressed += AddCustomElement;
            leftPanel.AddChild(addBtn);

            leftScroll.AddChild(leftPanel);
            split.AddChild(leftScroll);

            // ═══ CENTER PANEL: Preview ═══
            var centerPanel = new VBoxContainer();
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            centerPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

            // Preview header with layout mode controls
            var previewHeader = new HBoxContainer();
            previewHeader.AddThemeConstantOverride("separation", 8);
            previewHeader.AddChild(EditorStyles.MakeLabel("Preview", EditorStyles.FontHeader, EditorStyles.TextSecondary));

            _layoutToggleBtn = EditorStyles.MakeButton("Layout Mode", EditorStyles.FontSmall);
            _layoutToggleBtn.ToggleMode = true;
            _layoutToggleBtn.CustomMinimumSize = new Vector2(0, 24);
            _layoutToggleBtn.Pressed += () =>
            {
                _layoutMode = _layoutToggleBtn.ButtonPressed;
                _layoutToggleBtn.Text = _layoutMode ? "Layout Mode [ON]" : "Layout Mode";
                _layoutToggleBtn.AddThemeColorOverride("font_color",
                    _layoutMode ? new Color(0.9f, 0.7f, 0.2f) : EditorStyles.TextSecondary);
                RebuildPreview();
            };
            previewHeader.AddChild(_layoutToggleBtn);

            _gridSnapCheck = new CheckButton();
            _gridSnapCheck.Text = "Snap";
            _gridSnapCheck.ButtonPressed = true;
            _gridSnapCheck.AddThemeFontSizeOverride("font_size", EditorStyles.FontTiny);
            previewHeader.AddChild(_gridSnapCheck);

            previewHeader.AddChild(EditorStyles.MakeLabel("Grid:", EditorStyles.FontTiny, EditorStyles.TextMuted));
            _gridSizeSpin = new SpinBox();
            _gridSizeSpin.MinValue = 5;
            _gridSizeSpin.MaxValue = 50;
            _gridSizeSpin.Step = 5;
            _gridSizeSpin.Value = 10;
            _gridSizeSpin.CustomMinimumSize = new Vector2(60, 0);
            _gridSizeSpin.AddThemeFontSizeOverride("font_size", EditorStyles.FontTiny);
            previewHeader.AddChild(_gridSizeSpin);

            centerPanel.AddChild(previewHeader);

            _previewContainer = new SubViewportContainer();
            _previewContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _previewContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _previewContainer.Stretch = true;

            _previewViewport = new SubViewport();
            _previewViewport.Size = new Vector2I(960, 540);
            _previewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _previewViewport.TransparentBg = true;

            _previewRoot = new Control();
            _previewRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _previewViewport.AddChild(_previewRoot);

            _previewContainer.AddChild(_previewViewport);
            centerPanel.AddChild(_previewContainer);

            split.AddChild(centerPanel);

            // ═══ RIGHT PANEL: Property Inspector ═══
            _inspectorScroll = new ScrollContainer();
            _inspectorScroll.CustomMinimumSize = new Vector2(320, 0);
            _inspectorScroll.SizeFlagsVertical = SizeFlags.ExpandFill;

            var rightPanel = new VBoxContainer();
            rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _inspectorTitle = EditorStyles.MakeLabel("(select an element)", EditorStyles.FontHeader, AccentColor);
            rightPanel.AddChild(_inspectorTitle);

            _inspectorFields = new VBoxContainer();
            _inspectorFields.AddThemeConstantOverride("separation", 4);
            rightPanel.AddChild(_inspectorFields);

            _inspectorScroll.AddChild(rightPanel);
            split.AddChild(_inspectorScroll);

            content.AddChild(split);
        }

        // ═══════════════════════════════════════════════════════════════
        //  SCREEN & ELEMENT LIST
        // ═══════════════════════════════════════════════════════════════

        private void RebuildScreenList()
        {
            foreach (var child in _screenList.GetChildren())
                if (child is Node n) n.QueueFree();

            string filter = _searchFilter?.Text?.ToLower() ?? "";

            foreach (var kvp in ScreenDefinitions)
            {
                string name = kvp.Key;
                var def = kvp.Value;

                if (!string.IsNullOrEmpty(filter) && !name.ToLower().Contains(filter)
                    && !def.Description.ToLower().Contains(filter))
                    continue;

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);

                bool hasOverrides = _config != null && _config.ContainsKey(name);
                Color labelColor = hasOverrides ? AccentColor : EditorStyles.TextPrimary;

                var btn = EditorStyles.MakeButton(name, EditorStyles.FontBody, labelColor);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                string screenName = name;
                btn.Pressed += () => SelectScreen(screenName);
                row.AddChild(btn);

                // Indicator for custom elements
                if (_config != null && _config.TryGetValue(name, out var screenObj)
                    && screenObj is Dictionary<string, object> screenData
                    && screenData.ContainsKey("_custom"))
                {
                    var customLabel = EditorStyles.MakeLabel("+", EditorStyles.FontSmall, EditorStyles.StatusSaved);
                    row.AddChild(customLabel);
                }

                _screenList.AddChild(row);
            }

            // Also show custom screens from config that aren't in definitions
            if (_config != null)
            {
                foreach (var kvp in _config)
                {
                    if (ScreenDefinitions.ContainsKey(kvp.Key)) continue;
                    if (!string.IsNullOrEmpty(filter) && !kvp.Key.ToLower().Contains(filter)) continue;

                    var btn = EditorStyles.MakeButton($"[+] {kvp.Key}", EditorStyles.FontBody, EditorStyles.StatusSaved);
                    btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    string screenName = kvp.Key;
                    btn.Pressed += () => SelectScreen(screenName);
                    _screenList.AddChild(btn);
                }
            }
        }

        private void SelectScreen(string screenName)
        {
            _selectedScreen = screenName;
            _selectedElement = null;
            RebuildElementList();
            RebuildPreview();
            ClearInspector();
        }

        private void RebuildElementList()
        {
            foreach (var child in _elementList.GetChildren())
                if (child is Node n) n.QueueFree();

            if (_selectedScreen == null) return;

            // Get elements from definition
            if (ScreenDefinitions.TryGetValue(_selectedScreen, out var def))
            {
                foreach (var el in def.Elements)
                {
                    bool hasOverride = HasElementOverrides(_selectedScreen, el.Name);
                    Color color = hasOverride ? AccentColor : EditorStyles.TextSecondary;

                    var btn = EditorStyles.MakeButton($"  {el.Name}", EditorStyles.FontSmall, color);
                    btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    btn.Alignment = HorizontalAlignment.Left;
                    string elName = el.Name;
                    btn.Pressed += () => SelectElement(elName);
                    _elementList.AddChild(btn);

                    // Description tooltip
                    btn.TooltipText = el.Description;
                }
            }

            // Also show custom elements from config
            if (_config != null && _config.TryGetValue(_selectedScreen, out var screenObj)
                && screenObj is Dictionary<string, object> screenData)
            {
                foreach (var kvp in screenData)
                {
                    if (kvp.Key.StartsWith("_")) continue; // skip metadata
                    // Skip if already in definition
                    if (ScreenDefinitions.TryGetValue(_selectedScreen, out var screenDef)
                        && screenDef.Elements.Any(e => e.Name == kvp.Key))
                        continue;

                    var btn = EditorStyles.MakeButton($"  [+] {kvp.Key}", EditorStyles.FontSmall, EditorStyles.StatusSaved);
                    btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    btn.Alignment = HorizontalAlignment.Left;
                    string elName = kvp.Key;
                    btn.Pressed += () => SelectElement(elName);
                    _elementList.AddChild(btn);
                }
            }
        }

        private void SelectElement(string elementName)
        {
            _selectedElement = elementName;
            RebuildInspector();
            RebuildPreview();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PROPERTY INSPECTOR
        // ═══════════════════════════════════════════════════════════════

        private void ClearInspector()
        {
            foreach (var child in _inspectorFields.GetChildren())
                if (child is Node n) n.QueueFree();
            _inspectorTitle.Text = _selectedScreen ?? "(select a screen)";
        }

        private void RebuildInspector()
        {
            foreach (var child in _inspectorFields.GetChildren())
                if (child is Node n) n.QueueFree();

            if (_selectedScreen == null || _selectedElement == null)
            {
                _inspectorTitle.Text = _selectedScreen ?? "(select a screen)";
                return;
            }

            _inspectorTitle.Text = $"{_selectedScreen} > {_selectedElement}";

            // Get properties from definition
            ElementDef elDef = null;
            if (ScreenDefinitions.TryGetValue(_selectedScreen, out var def))
                elDef = def.Elements.FirstOrDefault(e => e.Name == _selectedElement);

            if (elDef != null)
            {
                // Description
                _inspectorFields.AddChild(EditorStyles.MakeLabel(elDef.Description, EditorStyles.FontTiny, EditorStyles.TextMuted));
                _inspectorFields.AddChild(EditorStyles.MakeSeparator());

                foreach (var prop in elDef.Properties)
                {
                    string currentValue = GetPropertyValue(_selectedScreen, _selectedElement, prop.Name, prop.DefaultValue);
                    AddPropertyEditor(prop, currentValue);
                }
            }

            // Show raw overrides for custom elements
            if (elDef == null && _config != null)
            {
                if (GetScreenData(_selectedScreen)?.TryGetValue(_selectedElement, out var elObj) == true
                    && elObj is Dictionary<string, object> elData)
                {
                    _inspectorFields.AddChild(EditorStyles.MakeLabel("Custom Element", EditorStyles.FontSmall, EditorStyles.StatusSaved));
                    _inspectorFields.AddChild(EditorStyles.MakeSeparator());

                    foreach (var kvp in elData)
                    {
                        var prop = new PropDef { Name = kvp.Key, Type = PropType.String, DefaultValue = kvp.Value?.ToString() ?? "" };
                        AddPropertyEditor(prop, kvp.Value?.ToString() ?? "");
                    }
                }
            }

            // Reset button
            _inspectorFields.AddChild(EditorStyles.MakeSeparator());
            var resetBtn = EditorStyles.MakeButton("Reset to Defaults", EditorStyles.FontSmall, EditorStyles.StatusError);
            resetBtn.Pressed += () =>
            {
                ResetElementToDefaults(_selectedScreen, _selectedElement);
                RebuildInspector();
                RebuildPreview();
                RebuildElementList();
            };
            _inspectorFields.AddChild(resetBtn);
        }

        private void AddPropertyEditor(PropDef prop, string currentValue)
        {
            var row = new VBoxContainer();
            row.AddThemeConstantOverride("separation", 2);

            // Label
            row.AddChild(EditorStyles.MakeLabel(prop.Name, EditorStyles.FontSmall, EditorStyles.TextSecondary));

            switch (prop.Type)
            {
                case PropType.Color:
                {
                    var colorBtn = new ColorPickerButton();
                    colorBtn.CustomMinimumSize = new Vector2(0, 28);
                    colorBtn.Color = ParseColor(currentValue);
                    colorBtn.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
                    string pName = prop.Name;
                    colorBtn.ColorChanged += c =>
                    {
                        string val = $"{c.R:F3},{c.G:F3},{c.B:F3}";
                        SetPropertyValue(_selectedScreen, _selectedElement, pName, val);
                        RebuildPreview();
                    };
                    row.AddChild(colorBtn);
                    break;
                }
                case PropType.Float:
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", 4);
                    var spin = new SpinBox();
                    spin.MinValue = prop.Min;
                    spin.MaxValue = prop.Max;
                    spin.Step = prop.Max > 10 ? 1 : 0.01;
                    spin.Value = ParseFloat(currentValue);
                    spin.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
                    spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    string pName = prop.Name;
                    spin.ValueChanged += v =>
                    {
                        SetPropertyValue(_selectedScreen, _selectedElement, pName, v.ToString("F3"));
                        RebuildPreview();
                    };
                    hbox.AddChild(spin);

                    var defaultLabel = EditorStyles.MakeLabel($"({prop.DefaultValue})", EditorStyles.FontTiny, EditorStyles.TextMuted);
                    hbox.AddChild(defaultLabel);

                    row.AddChild(hbox);
                    break;
                }
                case PropType.Int:
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", 4);
                    var spin = new SpinBox();
                    spin.MinValue = prop.Min;
                    spin.MaxValue = prop.Max;
                    spin.Step = 1;
                    spin.Value = ParseFloat(currentValue);
                    spin.Rounded = true;
                    spin.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
                    spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    string pName = prop.Name;
                    spin.ValueChanged += v =>
                    {
                        SetPropertyValue(_selectedScreen, _selectedElement, pName, ((int)v).ToString());
                        RebuildPreview();
                    };
                    hbox.AddChild(spin);

                    var defaultLabel = EditorStyles.MakeLabel($"({prop.DefaultValue})", EditorStyles.FontTiny, EditorStyles.TextMuted);
                    hbox.AddChild(defaultLabel);

                    row.AddChild(hbox);
                    break;
                }
                case PropType.String:
                {
                    var edit = EditorStyles.MakeLineEdit(prop.DefaultValue);
                    edit.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
                    edit.Text = currentValue;
                    string pName = prop.Name;
                    edit.TextChanged += t =>
                    {
                        SetPropertyValue(_selectedScreen, _selectedElement, pName, t);
                        RebuildPreview();
                    };
                    row.AddChild(edit);
                    break;
                }
                case PropType.Bool:
                {
                    var check = new CheckButton();
                    check.ButtonPressed = currentValue == "true";
                    check.Text = prop.Name;
                    check.AddThemeFontSizeOverride("font_size", EditorStyles.FontSmall);
                    string pName = prop.Name;
                    check.Toggled += v =>
                    {
                        SetPropertyValue(_selectedScreen, _selectedElement, pName, v ? "true" : "false");
                        RebuildPreview();
                    };
                    row.AddChild(check);
                    break;
                }
            }

            _inspectorFields.AddChild(row);
        }

        // ═══════════════════════════════════════════════════════════════
        //  LIVE WYSIWYG PREVIEW
        // ═══════════════════════════════════════════════════════════════

        // Mapping from preview controls back to element names for click-selection
        private readonly Dictionary<Control, string> _previewClickMap = new();

        private void RebuildPreview()
        {
            // Clear
            foreach (var child in _previewRoot.GetChildren())
                if (child is Node n) n.QueueFree();
            _previewClickMap.Clear();
            _draggables.Clear();

            if (_selectedScreen == null) return;

            // Dark game background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(0.05f, 0.05f, 0.08f);
            _previewRoot.AddChild(bg);

            // Layout Mode — interactive draggable elements
            if (_layoutMode && _selectedScreen == "HUD")
            {
                RenderHUDLayoutMode();
                return;
            }

            // Dispatch to screen-specific renderer
            switch (_selectedScreen)
            {
                case "HUD": RenderHUDPreview(); break;
                case "Inventory": RenderInventoryPreview(); break;
                case "PassiveTree": RenderPassiveTreePreview(); break;
                case "PauseMenu": RenderPauseMenuPreview(); break;
                case "CharacterCreation": RenderCharacterCreationPreview(); break;
                case "MainMenu": RenderMainMenuPreview(); break;
                case "BossHealthBar": RenderBossHealthBarPreview(); break;
                case "LootBoxCeremony": RenderLootBoxPreview(); break;
                case "DamageNumbers": RenderDamageNumbersPreview(); break;
                case "SectorTransition": RenderSectorTransitionPreview(); break;
                case "CharacterSheet": RenderCharacterSheetPreview(); break;
                case "Achievements": RenderAchievementsPreview(); break;
                case "AbilityBar": RenderAbilityBarPreview(); break;
                case "VictoryScreen": RenderVictoryScreenPreview(); break;
                default: RenderGenericPreview(); break;
            }

            // Add click-to-select input handling
            var clickCatcher = new Control();
            clickCatcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            clickCatcher.GuiInput += OnPreviewClick;
            _previewRoot.AddChild(clickCatcher);
        }

        private void OnPreviewClick(InputEvent ev)
        {
            if (ev is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
            var pos = mb.Position;
            // Find the deepest matching element
            string best = null;
            float bestArea = float.MaxValue;
            foreach (var kvp in _previewClickMap)
            {
                var ctrl = kvp.Key;
                if (!IsInstanceValid(ctrl)) continue;
                var rect = ctrl.GetGlobalRect();
                // Convert to preview-local coords
                if (rect.HasPoint(pos + _previewRoot.GlobalPosition))
                {
                    float area = rect.Size.X * rect.Size.Y;
                    if (area < bestArea) { bestArea = area; best = kvp.Value; }
                }
            }
            if (best != null && best != _selectedElement)
            {
                SelectElement(best);
            }
        }

        /// <summary>Register a control as clickable for an element. Also adds selection highlight.</summary>
        private void RegisterPreviewElement(Control ctrl, string elementName)
        {
            _previewClickMap[ctrl] = elementName;
            if (elementName == _selectedElement)
            {
                // Selection outline
                var outline = new ReferenceRect();
                outline.Position = ctrl.Position - new Vector2(2, 2);
                outline.Size = ctrl.Size + new Vector2(4, 4);
                outline.BorderColor = AccentColor;
                outline.BorderWidth = 2f;
                outline.EditorOnly = false;
                ctrl.GetParent()?.AddChild(outline);

                // Element name tag
                var tag = new Label();
                tag.Text = elementName;
                tag.AddThemeFontSizeOverride("font_size", 10);
                tag.AddThemeColorOverride("font_color", AccentColor);
                tag.Position = ctrl.Position + new Vector2(0, -14);
                ctrl.GetParent()?.AddChild(tag);
            }
        }

        /// <summary>Get property value for current screen, with fallback to schema default.</summary>
        private string PV(string element, string prop)
        {
            string def = "";
            if (ScreenDefinitions.TryGetValue(_selectedScreen, out var sDef))
            {
                var elDef = sDef.Elements.FirstOrDefault(e => e.Name == element);
                var pDef = elDef?.Properties.FirstOrDefault(p => p.Name == prop);
                if (pDef != null) def = pDef.DefaultValue;
            }
            return GetPropertyValue(_selectedScreen, element, prop, def);
        }

        private Color PVColor(string el, string prop) => ParseColor(PV(el, prop));
        private float PVFloat(string el, string prop) => ParseFloat(PV(el, prop));
        private int PVInt(string el, string prop) => (int)ParseFloat(PV(el, prop));

        // ── Helper: build a styled bar (ProgressBar) ──
        private ProgressBar MakePreviewBar(float width, float height, Color fill, Color barBg, float value = 70f, int fillMode = 0)
        {
            var bar = new ProgressBar();
            bar.CustomMinimumSize = new Vector2(width, height);
            bar.MinValue = 0; bar.MaxValue = 100; bar.Value = value;
            bar.ShowPercentage = false;
            bar.FillMode = fillMode;
            var bgStyle = new StyleBoxFlat { BgColor = barBg };
            bar.AddThemeStyleboxOverride("background", bgStyle);
            var fillStyle = new StyleBoxFlat { BgColor = fill };
            bar.AddThemeStyleboxOverride("fill", fillStyle);
            return bar;
        }

        // ── Helper: bordered panel ──
        private PanelContainer MakePreviewPanel(float w, float h, Color bgCol, Color borderCol, int borderW, float opacity = 1f)
        {
            var pc = new PanelContainer();
            pc.CustomMinimumSize = new Vector2(w, h);
            var s = new StyleBoxFlat();
            s.BgColor = new Color(bgCol.R, bgCol.G, bgCol.B, opacity);
            s.BorderColor = borderCol;
            s.SetBorderWidthAll(borderW);
            s.SetContentMarginAll(8);
            pc.AddThemeStyleboxOverride("panel", s);
            return pc;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SCREEN RENDERERS
        // ═══════════════════════════════════════════════════════════════

        private void RenderHUDPreview()
        {
            // Scale factor: preview is 960x540, game is 1920x1080
            const float S = 0.5f;

            // ── Health bar (bottom-left) ──
            {
                float w = PVFloat("HealthBar", "Width") * S;
                float h = PVFloat("HealthBar", "Height") * S;
                var accent = PVColor("HealthBar", "AccentColor");
                var fillHigh = PVColor("HealthBar", "FillColorHigh");
                var bgCol = PVColor("HealthBar", "BgColor");
                float opacity = PVFloat("HealthBar", "BgOpacity");
                int border = PVInt("HealthBar", "BorderWidth");
                string header = PV("HealthBar", "HeaderText");
                int fontSize = PVInt("HealthBar", "FontSize");

                // Use PositionX/Y config values (matching in-game behavior)
                float px = PVFloat("HealthBar", "PositionX") * S;
                float py = PVFloat("HealthBar", "PositionY") * S;
                var panel = MakePreviewPanel(w, h, bgCol, accent * new Color(1,1,1,0.5f), border, opacity);
                panel.Position = new Vector2(px, py);
                _previewRoot.AddChild(panel);

                var vbox = new VBoxContainer();
                vbox.AddThemeConstantOverride("separation", 2);
                var lbl = new Label { Text = header, HorizontalAlignment = HorizontalAlignment.Center };
                lbl.AddThemeFontSizeOverride("font_size", (int)(fontSize * S + 0.5f));
                lbl.AddThemeColorOverride("font_color", accent);
                vbox.AddChild(lbl);
                var sep = new ColorRect { Color = accent * new Color(1,1,1,0.3f), CustomMinimumSize = new Vector2(0, 1) };
                vbox.AddChild(sep);
                var bar = MakePreviewBar(w - 20, 0, fillHigh, new Color(0.02f,0.02f,0.03f), 72f, 3);
                bar.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(bar);
                var valLbl = new Label { Text = "72 / 100", HorizontalAlignment = HorizontalAlignment.Center };
                valLbl.AddThemeFontSizeOverride("font_size", 7);
                valLbl.AddThemeColorOverride("font_color", Colors.White);
                vbox.AddChild(valLbl);
                panel.AddChild(vbox);
                RegisterPreviewElement(panel, "HealthBar");
            }

            // ── Battery bar (bottom-right) ──
            {
                float w = PVFloat("BatteryBar", "Width") * S;
                float h = PVFloat("BatteryBar", "Height") * S;
                var accent = PVColor("BatteryBar", "AccentColor");
                var fill = PVColor("BatteryBar", "FillColor");
                var bgCol = PVColor("BatteryBar", "BgColor");
                float opacity = PVFloat("BatteryBar", "BgOpacity");
                int border = PVInt("BatteryBar", "BorderWidth");
                string header = PV("BatteryBar", "HeaderText");

                float bpx = PVFloat("BatteryBar", "PositionX") * S;
                float bpy = PVFloat("BatteryBar", "PositionY") * S;
                var panel = MakePreviewPanel(w, h, bgCol, accent * new Color(1,1,1,0.5f), border, opacity);
                panel.Position = new Vector2(bpx, bpy);
                _previewRoot.AddChild(panel);

                var vbox = new VBoxContainer();
                vbox.AddThemeConstantOverride("separation", 2);
                var lbl = new Label { Text = header, HorizontalAlignment = HorizontalAlignment.Center };
                lbl.AddThemeFontSizeOverride("font_size", 6);
                lbl.AddThemeColorOverride("font_color", accent);
                vbox.AddChild(lbl);
                var sep = new ColorRect { Color = accent * new Color(1,1,1,0.3f), CustomMinimumSize = new Vector2(0, 1) };
                vbox.AddChild(sep);
                var bar = MakePreviewBar(w - 20, 0, fill, new Color(0.02f,0.02f,0.03f), 85f, 3);
                bar.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(bar);
                var valLbl = new Label { Text = "85 / 100", HorizontalAlignment = HorizontalAlignment.Center };
                valLbl.AddThemeFontSizeOverride("font_size", 7);
                valLbl.AddThemeColorOverride("font_color", Colors.White);
                vbox.AddChild(valLbl);
                panel.AddChild(vbox);
                RegisterPreviewElement(panel, "BatteryBar");
            }

            // ── XP Bar (bottom-center strip) ──
            {
                var fill = PVColor("XPBar", "FillColor");
                var xpBg = PVColor("XPBar", "BgColor");
                float h = PVFloat("XPBar", "Height") * S;
                string lvlPrefix = PV("XPBar", "LevelPrefix");

                float xpPosX = PVFloat("XPBar", "PositionX") * S;
                float xpPosY = PVFloat("XPBar", "PositionY") * S;
                float xpW = 960 - xpPosX * 2f;

                var xpContainer = new Control();
                xpContainer.Position = new Vector2(xpPosX, xpPosY);
                xpContainer.Size = new Vector2(xpW, h + 16);
                _previewRoot.AddChild(xpContainer);

                var xpBar = MakePreviewBar(xpW, h, fill, xpBg, 45f);
                xpContainer.AddChild(xpBar);

                var xpLabel = new Label { Text = $"{lvlPrefix} 3", HorizontalAlignment = HorizontalAlignment.Center };
                xpLabel.AddThemeFontSizeOverride("font_size", 6);
                xpLabel.AddThemeColorOverride("font_color", fill);
                xpLabel.Position = new Vector2(0, h + 1);
                xpLabel.Size = new Vector2(xpW, 14);
                xpContainer.AddChild(xpLabel);
                RegisterPreviewElement(xpContainer, "XPBar");
            }

            // ── Buff Strip (below health bar) ──
            {
                float iconSize = PVFloat("BuffStrip", "IconSize") * S;
                int maxVis = PVInt("BuffStrip", "MaxVisible");
                float spacing = PVFloat("BuffStrip", "Spacing") * S;
                var buffBg = PVColor("BuffStrip", "BgColor");

                var buffRow = new HBoxContainer();
                buffRow.AddThemeConstantOverride("separation", (int)spacing);
                float bx = PVFloat("BuffStrip", "PositionX") * S;
                float by = PVFloat("BuffStrip", "PositionY") * S;
                buffRow.Position = new Vector2(bx, by);
                _previewRoot.AddChild(buffRow);

                for (int i = 0; i < Mathf.Min(3, maxVis); i++)
                {
                    var icon = new ColorRect { CustomMinimumSize = new Vector2(iconSize, iconSize) };
                    icon.Color = i == 0 ? new Color(0.3f,0.8f,0.4f) : i == 1 ? new Color(0.8f,0.3f,0.3f) : new Color(0.3f,0.5f,0.9f);
                    buffRow.AddChild(icon);
                }
                RegisterPreviewElement(buffRow, "BuffStrip");
            }

            // ── Dash Indicator ──
            {
                var active = PVColor("DashIndicator", "ActiveColor");
                var inactive = PVColor("DashIndicator", "InactiveColor");
                string label = PV("DashIndicator", "LabelText");

                var dashRow = new HBoxContainer();
                dashRow.AddThemeConstantOverride("separation", 3);
                dashRow.Position = new Vector2(PVFloat("DashIndicator", "PositionX") * S, PVFloat("DashIndicator", "PositionY") * S);
                _previewRoot.AddChild(dashRow);

                var dashLbl = new Label { Text = label };
                dashLbl.AddThemeFontSizeOverride("font_size", 5);
                dashLbl.AddThemeColorOverride("font_color", active);
                dashRow.AddChild(dashLbl);
                for (int i = 0; i < 3; i++)
                {
                    var pip = new ColorRect { CustomMinimumSize = new Vector2(8, 4) };
                    pip.Color = i < 2 ? active : inactive;
                    dashRow.AddChild(pip);
                }
                RegisterPreviewElement(dashRow, "DashIndicator");
            }

            // ── Sector Label (top-right) ──
            {
                var col = PVColor("SectorLabel", "TextColor");
                int fs = PVInt("SectorLabel", "FontSize");
                float px = PVFloat("SectorLabel", "PositionX") * S;
                float py = PVFloat("SectorLabel", "PositionY") * S;

                var sectorLbl = new Label { Text = "SECTOR 2 — FURNACE DECK" };
                sectorLbl.AddThemeFontSizeOverride("font_size", (int)(fs * S + 0.5f));
                sectorLbl.AddThemeColorOverride("font_color", col);
                sectorLbl.Position = new Vector2(px, py);
                _previewRoot.AddChild(sectorLbl);
                RegisterPreviewElement(sectorLbl, "SectorLabel");
            }

            // ── Minimap (top-right) ──
            {
                float mw = PVFloat("Minimap", "Width") * S;
                float mh = PVFloat("Minimap", "Height") * S;
                float mx = PVFloat("Minimap", "PositionX") * S;
                float my = PVFloat("Minimap", "PositionY") * S;
                var roomCol = PVColor("Minimap", "RoomColor");
                var playerCol = PVColor("Minimap", "PlayerColor");
                var bgCol = PVColor("Minimap", "BgColor");
                float opacity = PVFloat("Minimap", "BgOpacity");

                var mapBg = new ColorRect { CustomMinimumSize = new Vector2(mw, mh) };
                mapBg.Color = new Color(bgCol.R, bgCol.G, bgCol.B, opacity);
                mapBg.Position = new Vector2(mx, my);
                _previewRoot.AddChild(mapBg);

                // Fake room grid
                float cellSize = 9f;
                float cx = mx + mw / 2f, cy = my + mh / 2f;
                int[,] rooms = { {0,1,0}, {1,1,1}, {0,1,0}, {0,1,0} };
                for (int ry = 0; ry < 4; ry++)
                    for (int rx = 0; rx < 3; rx++)
                        if (rooms[ry, rx] == 1)
                        {
                            var cell = new ColorRect { CustomMinimumSize = new Vector2(cellSize, cellSize) };
                            cell.Color = (ry == 1 && rx == 1) ? playerCol : roomCol;
                            cell.Position = new Vector2(cx + (rx - 1) * (cellSize + 2) - cellSize/2, cy + (ry - 1.5f) * (cellSize + 2) - cellSize/2);
                            _previewRoot.AddChild(cell);
                        }
                RegisterPreviewElement(mapBg, "Minimap");
            }

            // ── Lift Timer (top-left) ──
            {
                var col = PVColor("LiftTimer", "TextColor");
                int fs = PVInt("LiftTimer", "FontSize");
                float px = PVFloat("LiftTimer", "PositionX") * S;
                float py = PVFloat("LiftTimer", "PositionY") * S;

                var timerLbl = new Label { Text = "4:32" };
                timerLbl.AddThemeFontSizeOverride("font_size", (int)(fs * S + 0.5f));
                timerLbl.AddThemeColorOverride("font_color", col);
                timerLbl.Position = new Vector2(px, py);
                _previewRoot.AddChild(timerLbl);
                RegisterPreviewElement(timerLbl, "LiftTimer");
            }

            // ── Consumable List ──
            {
                int fs = PVInt("ConsumableList", "FontSize");
                var col = PVColor("ConsumableList", "TextColor");
                string hKey = PV("ConsumableList", "HealthKeyLabel");
                string mKey = PV("ConsumableList", "ManaKeyLabel");

                var conVbox = new VBoxContainer();
                conVbox.Position = new Vector2(PVFloat("ConsumableList", "PositionX") * S, PVFloat("ConsumableList", "PositionY") * S);
                _previewRoot.AddChild(conVbox);

                var hLbl = new Label { Text = $"{hKey} Repair Kit x3" };
                hLbl.AddThemeFontSizeOverride("font_size", (int)(fs * S + 0.5f));
                hLbl.AddThemeColorOverride("font_color", col);
                conVbox.AddChild(hLbl);
                var mLbl = new Label { Text = $"{mKey} Charge Cell x2" };
                mLbl.AddThemeFontSizeOverride("font_size", (int)(fs * S + 0.5f));
                mLbl.AddThemeColorOverride("font_color", col);
                conVbox.AddChild(mLbl);
                RegisterPreviewElement(conVbox, "ConsumableList");
            }

            // ── Loot Box Tracker ──
            {
                float px = PVFloat("LootBoxTracker", "PositionX") * S;
                float py = PVFloat("LootBoxTracker", "PositionY") * S;
                int fs = PVInt("LootBoxTracker", "FontSize");
                var iconCol = PVColor("LootBoxTracker", "IconColor");

                var trackerVbox = new VBoxContainer();
                trackerVbox.Position = new Vector2(px, py);
                trackerVbox.AddThemeConstantOverride("separation", (int)(4 * S));

                // Header label
                var headerLbl = new Label { Text = "LOOT" };
                headerLbl.AddThemeFontSizeOverride("font_size", (int)(11 * S + 0.5f));
                headerLbl.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.4f));
                trackerVbox.AddChild(headerLbl);

                // Tier slots row (B S G D L C)
                var tierRow = new HBoxContainer();
                tierRow.AddThemeConstantOverride("separation", (int)(3 * S));

                string[] labels = { "J", "B", "S", "G", "D", "L", "C" };
                Color[] colors = {
                    new(0.5f, 0.5f, 0.5f), new(0.8f, 0.5f, 0.2f), new(0.8f, 0.8f, 0.9f),
                    new(1f, 0.84f, 0f), new(0.4f, 0.9f, 1f), new(0.7f, 0.3f, 0.9f),
                    new(1f, 0.95f, 0.7f)
                };
                int[] sampleCounts = { 5, 3, 1, 2, 0, 0, 0 };
                for (int t = 0; t < 7; t++)
                {
                    var slot = new PanelContainer();
                    slot.CustomMinimumSize = new Vector2(32 * S, 32 * S);
                    var style = new StyleBoxFlat();
                    style.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.8f);
                    style.BorderColor = colors[t] * new Color(1, 1, 1, 0.4f);
                    style.SetBorderWidthAll(1);
                    style.SetCornerRadiusAll(4);
                    slot.AddThemeStyleboxOverride("panel", style);

                    var slotVbox = new VBoxContainer();
                    slotVbox.AddThemeConstantOverride("separation", 0);
                    slot.AddChild(slotVbox);

                    var tierLbl = new Label { Text = labels[t] };
                    tierLbl.AddThemeFontSizeOverride("font_size", (int)(10 * S + 0.5f));
                    tierLbl.AddThemeColorOverride("font_color", colors[t]);
                    tierLbl.HorizontalAlignment = HorizontalAlignment.Center;
                    slotVbox.AddChild(tierLbl);

                    var countLbl = new Label { Text = sampleCounts[t].ToString() };
                    countLbl.AddThemeFontSizeOverride("font_size", (int)(fs * S + 0.5f));
                    countLbl.AddThemeColorOverride("font_color", sampleCounts[t] > 0 ? colors[t] : new Color(0.6f, 0.6f, 0.6f));
                    countLbl.HorizontalAlignment = HorizontalAlignment.Center;
                    slotVbox.AddChild(countLbl);

                    tierRow.AddChild(slot);
                }
                trackerVbox.AddChild(tierRow);

                _previewRoot.AddChild(trackerVbox);
                RegisterPreviewElement(trackerVbox, "LootBoxTracker");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HUD LAYOUT MODE — draggable elements for repositioning
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Defines which HUD elements can be dragged, their position/size properties,
        /// and default dimensions for the draggable box.
        /// </summary>
        private static readonly (string Name, string PosXProp, string PosYProp,
            string WidthProp, string HeightProp, float DefW, float DefH)[] HUDLayoutElements =
        {
            ("HealthBar",      "PositionX", "PositionY", "Width",  "Height", 150, 220),
            ("BatteryBar",     "PositionX", "PositionY", "Width",  "Height", 150, 220),
            ("XPBar",          "PositionX", "PositionY", null,     "Height", 1540, 8),
            ("BuffStrip",      "PositionX", "PositionY", null,     null,     200,  32),
            ("DashIndicator",  "PositionX", "PositionY", null,     null,     160,  20),
            ("SectorLabel",    "PositionX", "PositionY", null,     null,     300,  30),
            ("Minimap",        "PositionX", "PositionY", "Width",  "Height", 200,  200),
            ("LiftTimer",      "PositionX", "PositionY", null,     null,     100,  30),
            ("ConsumableList", "PositionX", "PositionY", null,     null,     180,  50),
            ("LootBoxTracker", "PositionX", "PositionY", null,     null,     260,  60),
        };

        private void RenderHUDLayoutMode()
        {
            _draggables.Clear();
            const float S = 0.5f;

            // Grid overlay
            bool snap = _gridSnapCheck?.ButtonPressed ?? true;
            float gridSize = (float)(_gridSizeSpin?.Value ?? 10);
            if (snap && gridSize > 0)
            {
                float gs = gridSize * S;
                var gridColor = new Color(0.25f, 0.25f, 0.30f, 0.15f);
                for (float x = 0; x < 960; x += gs)
                {
                    var line = new ColorRect { Size = new Vector2(1, 540), Position = new Vector2(x, 0) };
                    line.Color = gridColor;
                    line.MouseFilter = Control.MouseFilterEnum.Ignore;
                    _previewRoot.AddChild(line);
                }
                for (float y = 0; y < 540; y += gs)
                {
                    var line = new ColorRect { Size = new Vector2(960, 1), Position = new Vector2(0, y) };
                    line.Color = gridColor;
                    line.MouseFilter = Control.MouseFilterEnum.Ignore;
                    _previewRoot.AddChild(line);
                }
            }

            // Create draggable elements
            foreach (var el in HUDLayoutElements)
            {
                // Get position from config
                float px = PVFloat(el.Name, el.PosXProp) * S;
                float py = PVFloat(el.Name, el.PosYProp) * S;
                float w = el.WidthProp != null ? PVFloat(el.Name, el.WidthProp) * S : el.DefW * S;
                float h = el.HeightProp != null ? PVFloat(el.Name, el.HeightProp) * S : el.DefH * S;

                // Color-coded SoffitPanel background
                var (bgColor, borderColor) = GetLayoutElementColors(el.Name);

                var soffit = new SoffitPanel();
                soffit.BgColor = bgColor;
                soffit.BorderColor = borderColor;
                soffit.BorderWidth = 1.5f;
                soffit.SetBevelAll(6f);
                soffit.Size = new Vector2(w, h);
                soffit.MouseFilter = Control.MouseFilterEnum.Ignore;

                // Element name label
                var label = new Label { Text = el.Name, HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeFontSizeOverride("font_size", 8);
                label.AddThemeColorOverride("font_color", borderColor);
                label.Position = new Vector2(4, 4);
                label.MouseFilter = Control.MouseFilterEnum.Ignore;
                soffit.AddChild(label);

                // Wrap in draggable
                var draggable = new DraggableHUDElement();
                draggable.ElementName = el.Name;
                draggable.SnapToGrid = snap;
                draggable.GridSize = gridSize;
                draggable.Position = new Vector2(px, py);
                draggable.Size = new Vector2(w, h);
                draggable.MouseFilter = Control.MouseFilterEnum.Stop;

                draggable.AddChild(soffit);

                var capturedEl = el;
                draggable.OnSelected += OnLayoutElementSelected;
                draggable.OnMoved += (name, gamePos) => OnLayoutElementMoved(capturedEl, gamePos);
                draggable.OnResized += (name, gameSize) => OnLayoutElementResized(capturedEl, gameSize);

                _previewRoot.AddChild(draggable);
                _draggables[el.Name] = draggable;
            }

            // Select the currently selected element
            if (_selectedElement != null && _draggables.TryGetValue(_selectedElement, out var sel))
            {
                sel.Selected = true;
                sel.QueueRedraw();
            }
        }

        private void OnLayoutElementSelected(string name)
        {
            // Deselect all
            foreach (var kvp in _draggables)
            {
                kvp.Value.Selected = kvp.Key == name;
                kvp.Value.QueueRedraw();
            }

            if (name != _selectedElement)
                SelectElement(name);
        }

        private void OnLayoutElementMoved(
            (string Name, string PosXProp, string PosYProp, string WidthProp, string HeightProp, float DefW, float DefH) el,
            Vector2 gamePos)
        {
            SetPropertyValue("HUD", el.Name, el.PosXProp, ((int)gamePos.X).ToString());
            SetPropertyValue("HUD", el.Name, el.PosYProp, ((int)gamePos.Y).ToString());
            RebuildInspector(); // Update the inspector to reflect new position
        }

        private void OnLayoutElementResized(
            (string Name, string PosXProp, string PosYProp, string WidthProp, string HeightProp, float DefW, float DefH) el,
            Vector2 gameSize)
        {
            if (el.WidthProp != null)
                SetPropertyValue("HUD", el.Name, el.WidthProp, ((int)gameSize.X).ToString());
            if (el.HeightProp != null)
                SetPropertyValue("HUD", el.Name, el.HeightProp, ((int)gameSize.Y).ToString());
            RebuildInspector();
        }

        private static (Color bg, Color border) GetLayoutElementColors(string name)
        {
            return name switch
            {
                "HealthBar"      => (new Color(0.15f, 0.35f, 0.15f, 0.6f), new Color(0.3f, 0.8f, 0.3f)),
                "BatteryBar"     => (new Color(0.15f, 0.20f, 0.40f, 0.6f), new Color(0.3f, 0.5f, 0.9f)),
                "XPBar"          => (new Color(0.25f, 0.15f, 0.35f, 0.6f), new Color(0.6f, 0.3f, 0.8f)),
                "BuffStrip"      => (new Color(0.20f, 0.30f, 0.20f, 0.6f), new Color(0.4f, 0.7f, 0.4f)),
                "DashIndicator"  => (new Color(0.15f, 0.30f, 0.35f, 0.6f), new Color(0.3f, 0.9f, 1.0f)),
                "SectorLabel"    => (new Color(0.30f, 0.25f, 0.15f, 0.6f), new Color(0.7f, 0.65f, 0.5f)),
                "Minimap"        => (new Color(0.15f, 0.30f, 0.30f, 0.6f), new Color(0.3f, 0.8f, 0.8f)),
                "LiftTimer"      => (new Color(0.35f, 0.30f, 0.15f, 0.6f), new Color(0.9f, 0.7f, 0.2f)),
                "ConsumableList" => (new Color(0.25f, 0.25f, 0.25f, 0.6f), new Color(0.7f, 0.7f, 0.7f)),
                "LootBoxTracker" => (new Color(0.35f, 0.25f, 0.15f, 0.6f), new Color(0.9f, 0.6f, 0.2f)),
                _                => (new Color(0.20f, 0.20f, 0.20f, 0.6f), new Color(0.5f, 0.5f, 0.5f)),
            };
        }

        private void RenderInventoryPreview()
        {
            // Dim overlay
            var dimCol = PVColor("DimOverlay", "Color");
            float dimOp = PVFloat("DimOverlay", "Opacity");
            var dim = new ColorRect();
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            dim.Color = new Color(dimCol.R, dimCol.G, dimCol.B, dimOp);
            _previewRoot.AddChild(dim);
            RegisterPreviewElement(dim, "DimOverlay");

            const float S = 0.5f;
            float pw = PVFloat("Panel", "Width") * S;
            float ph = PVFloat("Panel", "Height") * S;
            float px = PVFloat("Panel", "PositionX") * S;
            float py = PVFloat("Panel", "PositionY") * S;
            var bgCol = PVColor("Panel", "BgColor");
            var borderCol = PVColor("Panel", "BorderColor");
            int borderW = PVInt("Panel", "BorderWidth");
            float opacity = PVFloat("Panel", "BgOpacity");
            string title = PV("Panel", "Title");
            var titleCol = PVColor("Panel", "TitleColor");
            int titleFs = PVInt("Panel", "TitleFontSize");

            var panel = MakePreviewPanel(pw, ph, bgCol, borderCol, borderW, opacity);
            panel.Position = new Vector2(px, py);
            _previewRoot.AddChild(panel);
            RegisterPreviewElement(panel, "Panel");

            var mainVbox = new VBoxContainer();
            mainVbox.AddThemeConstantOverride("separation", 6);

            var titleLbl = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
            titleLbl.AddThemeFontSizeOverride("font_size", (int)(titleFs * S + 0.5f));
            titleLbl.AddThemeColorOverride("font_color", titleCol);
            mainVbox.AddChild(titleLbl);

            // Bag grid + equipment split
            var splitH = new HBoxContainer();
            splitH.AddThemeConstantOverride("separation", 12);

            // Bag grid
            float slotSize = PVFloat("BagSlot", "Size") * S;
            float slotSpacing = PVFloat("BagSlot", "Spacing") * S;
            int cols = PVInt("BagSlot", "Columns");
            var slotBg = PVColor("BagSlot", "BgColor");
            var slotBorder = PVColor("BagSlot", "BorderColor");

            var bagGrid = new GridContainer();
            bagGrid.Columns = cols;
            bagGrid.AddThemeConstantOverride("h_separation", (int)slotSpacing);
            bagGrid.AddThemeConstantOverride("v_separation", (int)slotSpacing);

            for (int i = 0; i < cols * 5; i++) // 5 rows
            {
                var slot = new ColorRect { CustomMinimumSize = new Vector2(slotSize, slotSize) };
                slot.Color = slotBg;
                bagGrid.AddChild(slot);
            }
            splitH.AddChild(bagGrid);
            RegisterPreviewElement(bagGrid, "BagSlot");

            // Equipment slots
            float eqSize = PVFloat("EquipSlot", "Size") * S;
            var eqBg = PVColor("EquipSlot", "BgColor");
            var eqBorder = PVColor("EquipSlot", "BorderColor");
            int eqLabelFs = PVInt("EquipSlot", "LabelFontSize");
            var eqLabelCol = PVColor("EquipSlot", "LabelColor");

            var eqVbox = new VBoxContainer();
            eqVbox.AddThemeConstantOverride("separation", 4);
            string[] eqNames = { "Helm", "Chest", "Legs", "Weapon", "Shield" };
            foreach (var eName in eqNames)
            {
                var eqRow = new HBoxContainer();
                eqRow.AddThemeConstantOverride("separation", 4);
                var eqSlot = new ColorRect { CustomMinimumSize = new Vector2(eqSize, eqSize), Color = eqBg };
                eqRow.AddChild(eqSlot);
                var eqLbl = new Label { Text = eName };
                eqLbl.AddThemeFontSizeOverride("font_size", (int)(eqLabelFs * S + 0.5f));
                eqLbl.AddThemeColorOverride("font_color", eqLabelCol);
                eqRow.AddChild(eqLbl);
                eqVbox.AddChild(eqRow);
            }
            splitH.AddChild(eqVbox);
            RegisterPreviewElement(eqVbox, "EquipSlot");

            mainVbox.AddChild(splitH);
            panel.AddChild(mainVbox);

            // Tooltip preview
            float ttW = PVFloat("Tooltip", "Width") * S;
            var ttBg = PVColor("Tooltip", "BgColor");
            var ttBorder = PVColor("Tooltip", "BorderColor");
            int ttNameFs = PVInt("Tooltip", "NameFontSize");
            int ttDescFs = PVInt("Tooltip", "DescFontSize");

            var tooltip = MakePreviewPanel(ttW, 80, ttBg, ttBorder, 1, PVFloat("Tooltip", "BgOpacity"));
            tooltip.Position = new Vector2(px + pw + 8, py + 60);
            _previewRoot.AddChild(tooltip);

            var ttVbox = new VBoxContainer();
            var ttName = new Label { Text = "Rusted Blade" };
            ttName.AddThemeFontSizeOverride("font_size", (int)(ttNameFs * S));
            ttName.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.3f));
            ttVbox.AddChild(ttName);
            var ttDesc = new Label { Text = "+12 Attack Power\n+5% Crit Chance" };
            ttDesc.AddThemeFontSizeOverride("font_size", (int)(ttDescFs * S));
            ttDesc.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.72f));
            ttVbox.AddChild(ttDesc);
            tooltip.AddChild(ttVbox);
            RegisterPreviewElement(tooltip, "Tooltip");
        }

        private void RenderPassiveTreePreview()
        {
            var bgCol = PVColor("Panel", "BgColor");
            float opacity = PVFloat("Panel", "BgOpacity");
            string title = PV("Panel", "Title");
            var titleCol = PVColor("Panel", "TitleColor");
            int titleFs = PVInt("Panel", "TitleFontSize");

            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(bgCol.R, bgCol.G, bgCol.B, opacity);
            _previewRoot.AddChild(bg);
            RegisterPreviewElement(bg, "Panel");

            var titleLbl = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
            titleLbl.AddThemeFontSizeOverride("font_size", (int)(titleFs * 0.5f));
            titleLbl.AddThemeColorOverride("font_color", titleCol);
            titleLbl.Position = new Vector2(380, 15);
            _previewRoot.AddChild(titleLbl);

            // Draw some hex nodes
            float radius = PVFloat("Node", "Radius") * 0.5f;
            var locked = PVColor("Node", "LockedColor");
            var available = PVColor("Node", "AvailableColor");
            var allocated = PVColor("Node", "AllocatedColor");
            var keystoneCol = PVColor("Node", "KeystoneColor");
            float keystoneR = PVFloat("Node", "KeystoneRadius") * 0.5f;
            var lineInactive = PVColor("Connection", "InactiveColor");
            var lineActive = PVColor("Connection", "ActiveColor");

            Vector2 center = new Vector2(480, 270);
            Vector2[] offsets = { new(0,0), new(50,-30), new(-50,-30), new(50,30), new(-50,30), new(0,-60), new(100,-30) };
            Color[] cols = { allocated, allocated, available, locked, locked, allocated, keystoneCol };

            // Lines
            int[,] connections = { {0,1}, {0,2}, {0,3}, {0,4}, {1,5}, {1,6} };
            for (int c = 0; c < connections.GetLength(0); c++)
            {
                int a = connections[c, 0], b = connections[c, 1];
                var from = center + offsets[a]; var to = center + offsets[b];
                bool active = cols[a] == allocated && cols[b] == allocated;
                var line = new ColorRect();
                var diff = to - from;
                line.Position = new Vector2(Mathf.Min(from.X, to.X), Mathf.Min(from.Y, to.Y));
                line.Size = new Vector2(Mathf.Max(Mathf.Abs(diff.X), 2), Mathf.Max(Mathf.Abs(diff.Y), 2));
                line.Color = active ? lineActive : lineInactive;
                _previewRoot.AddChild(line);
            }
            RegisterPreviewElement(new Control { Position = center - new Vector2(80, 80), Size = new Vector2(160, 160) }, "Connection");

            for (int i = 0; i < offsets.Length; i++)
            {
                float r = i == 6 ? keystoneR : radius;
                var node = new ColorRect { CustomMinimumSize = new Vector2(r*2, r*2) };
                node.Color = cols[i];
                node.Position = center + offsets[i] - new Vector2(r, r);
                _previewRoot.AddChild(node);
            }
            RegisterPreviewElement(new Control { Position = center - new Vector2(60, 70), Size = new Vector2(170, 110) }, "Node");

            // Points display
            var ptCol = PVColor("PointsDisplay", "TextColor");
            string ptLabel = PV("PointsDisplay", "Label");
            int ptFs = PVInt("PointsDisplay", "FontSize");
            var ptLbl = new Label { Text = $"{ptLabel} 5" };
            ptLbl.AddThemeFontSizeOverride("font_size", (int)(ptFs * 0.5f));
            ptLbl.AddThemeColorOverride("font_color", ptCol);
            ptLbl.Position = new Vector2(380, 500);
            _previewRoot.AddChild(ptLbl);
            RegisterPreviewElement(ptLbl, "PointsDisplay");
        }

        private void RenderPauseMenuPreview()
        {
            // Dim
            var dimCol = PVColor("DimOverlay", "Color");
            float dimOp = PVFloat("DimOverlay", "Opacity");
            var dim = new ColorRect();
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            dim.Color = new Color(dimCol.R, dimCol.G, dimCol.B, dimOp);
            _previewRoot.AddChild(dim);
            RegisterPreviewElement(dim, "DimOverlay");

            float pw = PVFloat("Panel", "Width") * 0.5f;
            var bgCol = PVColor("Panel", "BgColor");
            var borderCol = PVColor("Panel", "BorderColor");
            int borderW = PVInt("Panel", "BorderWidth");
            float opacity = PVFloat("Panel", "BgOpacity");
            string title = PV("Panel", "Title");
            var titleCol = PVColor("Panel", "TitleColor");
            int titleFs = PVInt("Panel", "TitleFontSize");

            var panel = MakePreviewPanel(pw, 250, bgCol, borderCol, borderW, opacity);
            panel.Position = new Vector2(480 - pw/2, 145);
            _previewRoot.AddChild(panel);
            RegisterPreviewElement(panel, "Panel");

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            var titleLbl = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
            titleLbl.AddThemeFontSizeOverride("font_size", (int)(titleFs * 0.5f));
            titleLbl.AddThemeColorOverride("font_color", titleCol);
            vbox.AddChild(titleLbl);

            int btnFs = PVInt("Button", "FontSize");
            float btnH = PVFloat("Button", "Height") * 0.5f;
            var normalCol = PVColor("Button", "NormalColor");
            var hoverCol = PVColor("Button", "HoverColor");
            var quitCol = PVColor("Button", "QuitColor");

            string[] btns = { "Resume", "Save Game", "Settings", "Quit" };
            for (int i = 0; i < btns.Length; i++)
            {
                var btn = new Button { Text = btns[i] };
                btn.CustomMinimumSize = new Vector2(0, btnH);
                btn.AddThemeFontSizeOverride("font_size", (int)(btnFs * 0.5f));
                btn.AddThemeColorOverride("font_color", i == 3 ? quitCol : i == 0 ? hoverCol : normalCol);
                vbox.AddChild(btn);
            }
            panel.AddChild(vbox);
            RegisterPreviewElement(vbox, "Button");
        }

        private void RenderMainMenuPreview()
        {
            var bgCol = PVColor("Background", "BgColor");
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = bgCol;
            _previewRoot.AddChild(bg);
            RegisterPreviewElement(bg, "Background");

            string titleText = PV("Title", "Text");
            int titleFs = PVInt("Title", "FontSize");
            var titleCol = PVColor("Title", "Color");
            string subtitle = PV("Title", "Subtitle");
            int subFs = PVInt("Title", "SubtitleFontSize");
            var subCol = PVColor("Title", "SubtitleColor");

            var titleLbl = new Label { Text = titleText, HorizontalAlignment = HorizontalAlignment.Center };
            titleLbl.AddThemeFontSizeOverride("font_size", (int)(titleFs * 0.5f));
            titleLbl.AddThemeColorOverride("font_color", titleCol);
            titleLbl.Position = new Vector2(280, 80);
            titleLbl.Size = new Vector2(400, 50);
            _previewRoot.AddChild(titleLbl);

            var subLbl = new Label { Text = subtitle, HorizontalAlignment = HorizontalAlignment.Center };
            subLbl.AddThemeFontSizeOverride("font_size", (int)(subFs * 0.5f));
            subLbl.AddThemeColorOverride("font_color", subCol);
            subLbl.Position = new Vector2(330, 120);
            subLbl.Size = new Vector2(300, 20);
            _previewRoot.AddChild(subLbl);
            RegisterPreviewElement(titleLbl, "Title");

            float btnW = PVFloat("Button", "Width") * 0.5f;
            float btnH = PVFloat("Button", "Height") * 0.5f;
            float btnSpacing = PVFloat("Button", "Spacing") * 0.5f;
            int btnFs = PVInt("Button", "FontSize");
            var normalCol = PVColor("Button", "NormalColor");

            var btnVbox = new VBoxContainer();
            btnVbox.AddThemeConstantOverride("separation", (int)btnSpacing);
            btnVbox.Position = new Vector2(480 - btnW/2, 180);

            string[] btns = { "New Run", "Continue", "Achievements", "Settings", "Quit" };
            foreach (var b in btns)
            {
                var btn = new Button { Text = b };
                btn.CustomMinimumSize = new Vector2(btnW, btnH);
                btn.AddThemeFontSizeOverride("font_size", (int)(btnFs * 0.5f));
                btn.AddThemeColorOverride("font_color", normalCol);
                btnVbox.AddChild(btn);
            }
            _previewRoot.AddChild(btnVbox);
            RegisterPreviewElement(btnVbox, "Button");
        }

        private void RenderBossHealthBarPreview()
        {
            var fill = PVColor("Bar", "FillColor");
            var barBg = PVColor("Bar", "BgColor");
            var borderCol = PVColor("Bar", "BorderColor");
            float h = PVFloat("Bar", "Height") * 0.5f;
            float topM = PVFloat("Bar", "TopMargin") * 0.5f;
            float sideM = PVFloat("Bar", "SideMargin") * 0.5f;
            float barW = 960 - sideM * 2;

            int nameFs = PVInt("NameLabel", "FontSize");
            var nameCol = PVColor("NameLabel", "TextColor");

            var container = new VBoxContainer();
            container.AddThemeConstantOverride("separation", 4);
            container.Position = new Vector2(sideM, topM);

            var nameLbl = new Label { Text = "CORRUPTED SENTRY", HorizontalAlignment = HorizontalAlignment.Center };
            nameLbl.AddThemeFontSizeOverride("font_size", (int)(nameFs * 0.5f));
            nameLbl.AddThemeColorOverride("font_color", nameCol);
            container.AddChild(nameLbl);
            RegisterPreviewElement(nameLbl, "NameLabel");

            var bar = MakePreviewBar(barW, h, fill, barBg, 65f);
            container.AddChild(bar);
            _previewRoot.AddChild(container);
            RegisterPreviewElement(bar, "Bar");

            // Phase dots
            var activeCol = PVColor("PhaseIndicator", "ActiveColor");
            var inactiveCol = PVColor("PhaseIndicator", "InactiveColor");
            float dotSize = PVFloat("PhaseIndicator", "DotSize") * 0.5f;

            var dotsRow = new HBoxContainer();
            dotsRow.AddThemeConstantOverride("separation", 6);
            dotsRow.Position = new Vector2(480 - dotSize * 2, topM + h + 30);
            for (int i = 0; i < 3; i++)
            {
                var dot = new ColorRect { CustomMinimumSize = new Vector2(dotSize, dotSize) };
                dot.Color = i == 0 ? activeCol : inactiveCol;
                dotsRow.AddChild(dot);
            }
            _previewRoot.AddChild(dotsRow);
            RegisterPreviewElement(dotsRow, "PhaseIndicator");
        }

        private void RenderLootBoxPreview()
        {
            var bgCol = PVColor("Panel", "BgColor");
            float opacity = PVFloat("Panel", "BgOpacity");
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(bgCol.R, bgCol.G, bgCol.B, opacity);
            _previewRoot.AddChild(bg);
            RegisterPreviewElement(bg, "Panel");

            float boxSize = PVFloat("Box", "Size") * 0.5f;
            var box = new ColorRect { CustomMinimumSize = new Vector2(boxSize, boxSize) };
            box.Color = new Color(0.6f, 0.5f, 0.2f);
            box.Position = new Vector2(480 - boxSize/2, 100);
            _previewRoot.AddChild(box);
            RegisterPreviewElement(box, "Box");

            // Item cards
            float cardW = PVFloat("ItemReveal", "CardWidth") * 0.5f;
            float cardH = PVFloat("ItemReveal", "CardHeight") * 0.5f;
            var cardBg = PVColor("ItemReveal", "BgColor");
            int cardBorder = PVInt("ItemReveal", "BorderWidth");

            var common = PVColor("TierColors", "Common");
            var uncommon = PVColor("TierColors", "Uncommon");
            var rare = PVColor("TierColors", "Rare");
            var epic = PVColor("TierColors", "Epic");
            Color[] tierCols = { uncommon, rare, common };
            string[] names = { "Servo Arm", "Flux Core", "Bolt Pack" };

            var cardRow = new HBoxContainer();
            cardRow.AddThemeConstantOverride("separation", 12);
            cardRow.Position = new Vector2(480 - (cardW * 3 + 24) / 2, 220);
            for (int i = 0; i < 3; i++)
            {
                var card = MakePreviewPanel(cardW, cardH, cardBg, tierCols[i], cardBorder);
                var lbl = new Label { Text = names[i], HorizontalAlignment = HorizontalAlignment.Center };
                lbl.AddThemeFontSizeOverride("font_size", 7);
                lbl.AddThemeColorOverride("font_color", tierCols[i]);
                card.AddChild(lbl);
                cardRow.AddChild(card);
            }
            _previewRoot.AddChild(cardRow);
            RegisterPreviewElement(cardRow, "ItemReveal");

            // Tier color swatches
            var swatchRow = new HBoxContainer();
            swatchRow.AddThemeConstantOverride("separation", 8);
            swatchRow.Position = new Vector2(300, 430);
            string[] tierNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
            Color[] allTiers = { common, uncommon, rare, epic, PVColor("TierColors", "Legendary") };
            for (int i = 0; i < 5; i++)
            {
                var sw = new ColorRect { CustomMinimumSize = new Vector2(20, 20), Color = allTiers[i] };
                swatchRow.AddChild(sw);
                var tl = new Label { Text = tierNames[i] };
                tl.AddThemeFontSizeOverride("font_size", 6);
                tl.AddThemeColorOverride("font_color", allTiers[i]);
                swatchRow.AddChild(tl);
            }
            _previewRoot.AddChild(swatchRow);
            RegisterPreviewElement(swatchRow, "TierColors");
        }

        private void RenderDamageNumbersPreview()
        {
            var dmgCol = PVColor("Text", "DamageColor");
            var healCol = PVColor("Text", "HealColor");
            var critCol = PVColor("Text", "CritColor");
            int fs = PVInt("Text", "FontSize");
            int critFs = PVInt("Text", "CritFontSize");
            float spread = PVFloat("Text", "SpreadX") * 0.5f;

            // Simulated scene background hint
            var hint = new Label { Text = "(floating above enemies)" };
            hint.AddThemeFontSizeOverride("font_size", 8);
            hint.AddThemeColorOverride("font_color", EditorStyles.TextMuted);
            hint.Position = new Vector2(380, 480);
            _previewRoot.AddChild(hint);

            // Damage numbers scattered
            string[] nums = { "-24", "-18", "+12", "-47!", "-8", "+5" };
            Color[] cols = { dmgCol, dmgCol, healCol, critCol, dmgCol, healCol };
            int[] sizes = { fs, fs, fs, critFs, fs, fs };
            Vector2[] positions = { new(300,180), new(420,220), new(200,150), new(500,130), new(350,260), new(280,300) };

            for (int i = 0; i < nums.Length; i++)
            {
                var lbl = new Label { Text = nums[i] };
                lbl.AddThemeFontSizeOverride("font_size", (int)(sizes[i] * 0.5f));
                lbl.AddThemeColorOverride("font_color", cols[i]);
                lbl.Position = positions[i] + new Vector2(GD.Randf() * spread - spread/2, 0);
                _previewRoot.AddChild(lbl);
            }
            RegisterPreviewElement(new Control { Position = new Vector2(180, 120), Size = new Vector2(400, 200) }, "Text");
        }

        private void RenderSectorTransitionPreview()
        {
            var bgCol = PVColor("Panel", "BgColor");
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = bgCol;
            _previewRoot.AddChild(bg);
            RegisterPreviewElement(bg, "Panel");

            int fs = PVInt("Text", "FontSize");
            var col = PVColor("Text", "Color");
            int subFs = PVInt("Text", "SubtextFontSize");
            var subCol = PVColor("Text", "SubtextColor");

            var titleLbl = new Label { Text = "SECTOR 3", HorizontalAlignment = HorizontalAlignment.Center };
            titleLbl.AddThemeFontSizeOverride("font_size", (int)(fs * 0.5f));
            titleLbl.AddThemeColorOverride("font_color", col);
            titleLbl.Position = new Vector2(330, 220);
            titleLbl.Size = new Vector2(300, 40);
            _previewRoot.AddChild(titleLbl);

            var subLbl = new Label { Text = "REACTOR CORE", HorizontalAlignment = HorizontalAlignment.Center };
            subLbl.AddThemeFontSizeOverride("font_size", (int)(subFs * 0.5f));
            subLbl.AddThemeColorOverride("font_color", subCol);
            subLbl.Position = new Vector2(370, 260);
            subLbl.Size = new Vector2(220, 20);
            _previewRoot.AddChild(subLbl);
            RegisterPreviewElement(titleLbl, "Text");
        }

        private void RenderCharacterCreationPreview()
        {
            var bgCol = PVColor("Panel", "BgColor");
            string title = PV("Panel", "Title");
            var titleCol = PVColor("Panel", "TitleColor");
            int titleFs = PVInt("Panel", "TitleFontSize");

            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = bgCol;
            _previewRoot.AddChild(bg);
            RegisterPreviewElement(bg, "Panel");

            var titleLbl = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
            titleLbl.AddThemeFontSizeOverride("font_size", (int)(titleFs * 0.5f));
            titleLbl.AddThemeColorOverride("font_color", titleCol);
            titleLbl.Position = new Vector2(280, 20);
            titleLbl.Size = new Vector2(400, 30);
            _previewRoot.AddChild(titleLbl);

            // Frame cards
            float cw = PVFloat("FrameCard", "Width") * 0.5f;
            float ch = PVFloat("FrameCard", "Height") * 0.5f;
            var cardBg = PVColor("FrameCard", "BgColor");
            var selBorder = PVColor("FrameCard", "SelectedBorderColor");
            var hoverBorder = PVColor("FrameCard", "HoverBorderColor");
            int cardBorderW = PVInt("FrameCard", "BorderWidth");
            int nameFs = PVInt("FrameCard", "NameFontSize");

            string[] frames = { "Scrapheap", "TinCan", "SparkPlug", "RustBucket", "NoiseBox", "Clunker" };
            var cardRow = new HBoxContainer();
            cardRow.AddThemeConstantOverride("separation", 8);
            float totalW = frames.Length * cw + (frames.Length - 1) * 8;
            cardRow.Position = new Vector2(480 - totalW/2, 70);

            for (int i = 0; i < frames.Length; i++)
            {
                var border = i == 2 ? selBorder : hoverBorder;
                var card = MakePreviewPanel(cw, ch, cardBg, border, cardBorderW);
                var lbl = new Label { Text = frames[i], HorizontalAlignment = HorizontalAlignment.Center };
                lbl.AddThemeFontSizeOverride("font_size", (int)(nameFs * 0.5f));
                lbl.AddThemeColorOverride("font_color", i == 2 ? selBorder : Colors.White);
                card.AddChild(lbl);
                cardRow.AddChild(card);
            }
            _previewRoot.AddChild(cardRow);
            RegisterPreviewElement(cardRow, "FrameCard");

            // Stats grid
            int statFs = PVInt("StatsGrid", "FontSize");
            var labelCol = PVColor("StatsGrid", "LabelColor");
            var valueCol = PVColor("StatsGrid", "ValueColor");

            var statsGrid = new GridContainer { Columns = 2 };
            statsGrid.AddThemeConstantOverride("h_separation", 20);
            statsGrid.AddThemeConstantOverride("v_separation", 4);
            statsGrid.Position = new Vector2(360, ch + 90);

            string[,] stats = { {"HP", "120"}, {"ATK", "15"}, {"DEF", "8"}, {"SPD", "1.2"} };
            for (int i = 0; i < stats.GetLength(0); i++)
            {
                var l = new Label { Text = stats[i,0] };
                l.AddThemeFontSizeOverride("font_size", (int)(statFs * 0.5f));
                l.AddThemeColorOverride("font_color", labelCol);
                statsGrid.AddChild(l);
                var v = new Label { Text = stats[i,1] };
                v.AddThemeFontSizeOverride("font_size", (int)(statFs * 0.5f));
                v.AddThemeColorOverride("font_color", valueCol);
                statsGrid.AddChild(v);
            }
            _previewRoot.AddChild(statsGrid);
            RegisterPreviewElement(statsGrid, "StatsGrid");

            // Enter button
            string enterText = PV("EnterButton", "Text");
            int enterFs = PVInt("EnterButton", "FontSize");
            var enterCol = PVColor("EnterButton", "Color");
            var enterBtn = new Button { Text = enterText };
            enterBtn.AddThemeFontSizeOverride("font_size", (int)(enterFs * 0.5f));
            enterBtn.AddThemeColorOverride("font_color", enterCol);
            enterBtn.Position = new Vector2(380, 460);
            enterBtn.CustomMinimumSize = new Vector2(200, 30);
            _previewRoot.AddChild(enterBtn);
            RegisterPreviewElement(enterBtn, "EnterButton");
        }

        private void RenderCharacterSheetPreview()
        {
            const float S = 0.5f;
            float pw = PVFloat("Panel", "Width") * S;
            float ph = PVFloat("Panel", "Height") * S;
            var bgCol = PVColor("Panel", "BgColor");
            var borderCol = PVColor("Panel", "BorderColor");
            float opacity = PVFloat("Panel", "BgOpacity");
            string title = PV("Panel", "Title");
            var titleCol = PVColor("Panel", "TitleColor");
            int titleFs = PVInt("Panel", "TitleFontSize");

            var panel = MakePreviewPanel(pw, ph, bgCol, borderCol, 2, opacity);
            panel.Position = new Vector2(480 - pw/2, 270 - ph/2);
            _previewRoot.AddChild(panel);
            RegisterPreviewElement(panel, "Panel");

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);

            var tLbl = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
            tLbl.AddThemeFontSizeOverride("font_size", (int)(titleFs * S));
            tLbl.AddThemeColorOverride("font_color", titleCol);
            vbox.AddChild(tLbl);

            int labelFs = PVInt("StatRow", "LabelFontSize");
            int valueFs = PVInt("StatRow", "ValueFontSize");
            var labelCol = PVColor("StatRow", "LabelColor");
            var valueCol = PVColor("StatRow", "ValueColor");
            var modCol = PVColor("StatRow", "ModifierColor");
            var negCol = PVColor("StatRow", "NegativeColor");

            string[,] rows = { {"Attack Power", "24", "+3"}, {"Defense", "18", ""}, {"Crit Chance", "12%", "+5%"},
                {"Move Speed", "1.2", "-0.1"}, {"Max HP", "120", "+20"}, {"Cooldown Reduction", "8%", ""} };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                var l = new Label { Text = rows[i,0] };
                l.AddThemeFontSizeOverride("font_size", (int)(labelFs * S));
                l.AddThemeColorOverride("font_color", labelCol);
                l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(l);
                var v = new Label { Text = rows[i,1] };
                v.AddThemeFontSizeOverride("font_size", (int)(valueFs * S));
                v.AddThemeColorOverride("font_color", valueCol);
                row.AddChild(v);
                if (!string.IsNullOrEmpty(rows[i,2]))
                {
                    var m = new Label { Text = rows[i,2] };
                    m.AddThemeFontSizeOverride("font_size", (int)(valueFs * S));
                    m.AddThemeColorOverride("font_color", rows[i,2].StartsWith("-") ? negCol : modCol);
                    row.AddChild(m);
                }
                vbox.AddChild(row);
            }
            panel.AddChild(vbox);
            RegisterPreviewElement(vbox, "StatRow");
        }

        private void RenderAchievementsPreview()
        {
            var bgCol = PVColor("Toast", "BgColor");
            var borderCol = PVColor("Toast", "BorderColor");
            float tw = PVFloat("Toast", "Width") * 0.5f;
            float th = PVFloat("Toast", "Height") * 0.5f;
            int titleFs = PVInt("Toast", "TitleFontSize");
            var titleCol = PVColor("Toast", "TitleColor");
            int descFs = PVInt("Toast", "DescFontSize");
            var descCol = PVColor("Toast", "DescColor");

            // Show 2 stacked toasts sliding in from right
            for (int i = 0; i < 2; i++)
            {
                var toast = MakePreviewPanel(tw, th, bgCol, borderCol, 2);
                toast.Position = new Vector2(960 - tw - 10, 20 + i * (th + 8));
                _previewRoot.AddChild(toast);

                var vbox = new VBoxContainer();
                vbox.AddThemeConstantOverride("separation", 2);
                var tLbl = new Label { Text = i == 0 ? "ACHIEVEMENT UNLOCKED" : "FIRST BLOOD" };
                tLbl.AddThemeFontSizeOverride("font_size", (int)(titleFs * 0.5f));
                tLbl.AddThemeColorOverride("font_color", titleCol);
                vbox.AddChild(tLbl);
                var dLbl = new Label { Text = i == 0 ? "Survived 5 sectors" : "Defeat your first enemy" };
                dLbl.AddThemeFontSizeOverride("font_size", (int)(descFs * 0.5f));
                dLbl.AddThemeColorOverride("font_color", descCol);
                vbox.AddChild(dLbl);
                toast.AddChild(vbox);

                if (i == 0) RegisterPreviewElement(toast, "Toast");
            }
        }

        private void RenderAbilityBarPreview()
        {
            int slotCount = PVInt("Bar", "SlotCount");
            float slotSize = PVFloat("Bar", "SlotSize") * 0.5f;
            float spacing = PVFloat("Bar", "Spacing") * 0.5f;
            float botM = PVFloat("Bar", "BottomMargin") * 0.5f;

            var slotBg = PVColor("Slot", "BgColor");
            var slotBorder = PVColor("Slot", "BorderColor");
            var readyBorder = PVColor("Slot", "ReadyBorderColor");
            var cdCol = PVColor("Slot", "CooldownColor");
            int cdFs = PVInt("Slot", "CooldownTextSize");
            int keyFs = PVInt("Slot", "KeyLabelSize");

            float totalW = slotCount * slotSize + (slotCount - 1) * spacing;
            var barRow = new HBoxContainer();
            barRow.AddThemeConstantOverride("separation", (int)spacing);
            barRow.Position = new Vector2(480 - totalW/2, 540 - slotSize - botM - 16);
            _previewRoot.AddChild(barRow);
            RegisterPreviewElement(barRow, "Bar");

            for (int i = 0; i < slotCount; i++)
            {
                bool onCd = i == 2 || i == 4;
                bool ready = !onCd;
                var slot = MakePreviewPanel(slotSize, slotSize, onCd ? cdCol : slotBg, ready ? readyBorder : slotBorder, 1);

                var slotVbox = new VBoxContainer();
                if (onCd)
                {
                    var cdLbl = new Label { Text = i == 2 ? "3.2" : "1.1", HorizontalAlignment = HorizontalAlignment.Center };
                    cdLbl.AddThemeFontSizeOverride("font_size", (int)(cdFs * 0.5f));
                    cdLbl.AddThemeColorOverride("font_color", Colors.White);
                    slotVbox.AddChild(cdLbl);
                }
                var keyLbl = new Label { Text = $"{i+1}", HorizontalAlignment = HorizontalAlignment.Center };
                keyLbl.AddThemeFontSizeOverride("font_size", (int)(keyFs * 0.5f));
                keyLbl.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
                slotVbox.AddChild(keyLbl);
                slot.AddChild(slotVbox);
                barRow.AddChild(slot);
            }
            RegisterPreviewElement(barRow, "Slot");
        }

        private void RenderVictoryScreenPreview()
        {
            var bgCol = PVColor("Panel", "BgColor");
            float opacity = PVFloat("Panel", "BgOpacity");
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(bgCol.R, bgCol.G, bgCol.B, opacity);
            _previewRoot.AddChild(bg);
            RegisterPreviewElement(bg, "Panel");

            string titleText = PV("Title", "Text");
            int titleFs = PVInt("Title", "FontSize");
            var titleCol = PVColor("Title", "Color");
            var titleLbl = new Label { Text = titleText, HorizontalAlignment = HorizontalAlignment.Center };
            titleLbl.AddThemeFontSizeOverride("font_size", (int)(titleFs * 0.5f));
            titleLbl.AddThemeColorOverride("font_color", titleCol);
            titleLbl.Position = new Vector2(280, 140);
            titleLbl.Size = new Vector2(400, 40);
            _previewRoot.AddChild(titleLbl);
            RegisterPreviewElement(titleLbl, "Title");

            int statFs = PVInt("Stats", "FontSize");
            var labelCol = PVColor("Stats", "LabelColor");
            var valueCol = PVColor("Stats", "ValueColor");

            var statsVbox = new VBoxContainer();
            statsVbox.AddThemeConstantOverride("separation", 6);
            statsVbox.Position = new Vector2(360, 220);
            string[,] stats = { {"Time", "12:34"}, {"Enemies Killed", "47"}, {"Damage Dealt", "8,421"}, {"Loot Boxes", "3"} };
            for (int i = 0; i < stats.GetLength(0); i++)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 20);
                var l = new Label { Text = stats[i,0] };
                l.AddThemeFontSizeOverride("font_size", (int)(statFs * 0.5f));
                l.AddThemeColorOverride("font_color", labelCol);
                l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(l);
                var v = new Label { Text = stats[i,1] };
                v.AddThemeFontSizeOverride("font_size", (int)(statFs * 0.5f));
                v.AddThemeColorOverride("font_color", valueCol);
                row.AddChild(v);
                statsVbox.AddChild(row);
            }
            _previewRoot.AddChild(statsVbox);
            RegisterPreviewElement(statsVbox, "Stats");
        }

        /// <summary>Fallback for screens without a custom renderer (custom screens, etc.)</summary>
        private void RenderGenericPreview()
        {
            if (!ScreenDefinitions.TryGetValue(_selectedScreen, out var def))
            {
                var lbl = new Label { Text = $"Custom screen: {_selectedScreen}" };
                lbl.AddThemeFontSizeOverride("font_size", 12);
                lbl.AddThemeColorOverride("font_color", AccentColor);
                lbl.Position = new Vector2(20, 20);
                _previewRoot.AddChild(lbl);
                return;
            }

            var screenLabel = EditorStyles.MakeLabel(_selectedScreen, EditorStyles.FontHeader, AccentColor);
            screenLabel.Position = new Vector2(10, 10);
            _previewRoot.AddChild(screenLabel);

            float yOffset = 40;
            foreach (var el in def.Elements)
            {
                bool isSelected = el.Name == _selectedElement;
                var container = new PanelContainer();
                var style = EditorStyles.MakePanel(
                    isSelected ? new Color(0.15f, 0.2f, 0.3f) : new Color(0.08f, 0.08f, 0.1f),
                    isSelected ? AccentColor : EditorStyles.BorderColor,
                    isSelected ? 2 : 1, 4, 8);
                container.AddThemeStyleboxOverride("panel", style);
                container.CustomMinimumSize = new Vector2(880, 0);
                container.Position = new Vector2(20, yOffset);

                var vbox = new VBoxContainer();
                vbox.AddThemeConstantOverride("separation", 4);
                vbox.AddChild(EditorStyles.MakeLabel(el.Name, EditorStyles.FontBody,
                    isSelected ? AccentColor : EditorStyles.TextPrimary));

                var propsRow = new HBoxContainer();
                propsRow.AddThemeConstantOverride("separation", 12);
                int shown = 0;
                foreach (var prop in el.Properties)
                {
                    if (shown >= 6) break;
                    string val = GetPropertyValue(_selectedScreen, el.Name, prop.Name, prop.DefaultValue);
                    if (prop.Type == PropType.Color)
                    {
                        var colorRow = new HBoxContainer();
                        colorRow.AddThemeConstantOverride("separation", 4);
                        var swatch = new ColorRect { CustomMinimumSize = new Vector2(16, 16), Color = ParseColor(val) };
                        colorRow.AddChild(swatch);
                        colorRow.AddChild(EditorStyles.MakeLabel(prop.Name, EditorStyles.FontTiny, EditorStyles.TextMuted));
                        propsRow.AddChild(colorRow);
                    }
                    else
                    {
                        propsRow.AddChild(EditorStyles.MakeLabel($"{prop.Name}: {val}", EditorStyles.FontTiny, EditorStyles.TextMuted));
                    }
                    shown++;
                }
                vbox.AddChild(propsRow);
                container.AddChild(vbox);
                _previewRoot.AddChild(container);
                RegisterPreviewElement(container, el.Name);

                yOffset += 50;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CUSTOM ELEMENT CREATION
        // ═══════════════════════════════════════════════════════════════

        private void AddCustomElement()
        {
            if (_selectedScreen == null)
            {
                SetStatus("Select a screen first", EditorStyles.StatusError);
                return;
            }

            string name = _newElementName?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Enter an element name", EditorStyles.StatusError);
                return;
            }

            string type = _newElementType.GetItemText(_newElementType.Selected);

            // Create default properties based on type
            var elData = new Dictionary<string, object>();
            switch (type)
            {
                case "Panel":
                    elData["BgColor"] = "0.08,0.08,0.12";
                    elData["BorderColor"] = "0.3,0.3,0.35";
                    elData["BorderWidth"] = "2";
                    elData["Width"] = "400";
                    elData["Height"] = "300";
                    elData["PositionX"] = "0";
                    elData["PositionY"] = "0";
                    elData["BgOpacity"] = "0.95";
                    break;
                case "Label":
                    elData["Text"] = name;
                    elData["FontSize"] = "16";
                    elData["Color"] = "0.9,0.9,0.92";
                    elData["PositionX"] = "0";
                    elData["PositionY"] = "0";
                    break;
                case "Button":
                    elData["Text"] = name;
                    elData["FontSize"] = "16";
                    elData["Width"] = "200";
                    elData["Height"] = "40";
                    elData["NormalColor"] = "0.8,0.8,0.82";
                    elData["HoverColor"] = "0.9,0.7,0.2";
                    break;
                case "Bar":
                    elData["FillColor"] = "0.3,0.8,0.4";
                    elData["BgColor"] = "0.1,0.1,0.12";
                    elData["Width"] = "300";
                    elData["Height"] = "20";
                    elData["BorderWidth"] = "1";
                    break;
                case "Container":
                    elData["Spacing"] = "8";
                    elData["Width"] = "400";
                    elData["Height"] = "300";
                    elData["Direction"] = "Vertical";
                    break;
                case "Icon":
                    elData["Size"] = "32";
                    elData["Color"] = "0.9,0.9,0.92";
                    elData["BgColor"] = "0.1,0.1,0.12";
                    break;
            }

            // Save to config
            EnsureConfig();
            var screenData = GetOrCreateScreenData(_selectedScreen);
            screenData[name] = elData;

            // Mark as custom
            if (!screenData.ContainsKey("_custom"))
                screenData["_custom"] = new List<object>();
            if (screenData["_custom"] is List<object> customList)
                customList.Add(name);

            PushUndo(MiniJsonWriter.Serialize(_config));
            MarkDirty();

            _newElementName.Text = "";
            RebuildElementList();
            SelectElement(name);

            SetStatus($"Added {type}: {name}", EditorStyles.StatusSaved);
        }

        // ═══════════════════════════════════════════════════════════════
        //  DATA ACCESS
        // ═══════════════════════════════════════════════════════════════

        private void EnsureConfig()
        {
            _config ??= new Dictionary<string, object>();
        }

        private Dictionary<string, object> GetScreenData(string screen)
        {
            if (_config == null) return null;
            if (_config.TryGetValue(screen, out var obj) && obj is Dictionary<string, object> data)
                return data;
            return null;
        }

        private Dictionary<string, object> GetOrCreateScreenData(string screen)
        {
            EnsureConfig();
            if (!_config.TryGetValue(screen, out var obj) || obj is not Dictionary<string, object> data)
            {
                data = new Dictionary<string, object>();
                _config[screen] = data;
            }
            return data;
        }

        private Dictionary<string, object> GetOrCreateElementData(string screen, string element)
        {
            var screenData = GetOrCreateScreenData(screen);
            if (!screenData.TryGetValue(element, out var obj) || obj is not Dictionary<string, object> elData)
            {
                elData = new Dictionary<string, object>();
                screenData[element] = elData;
            }
            return elData;
        }

        private string GetPropertyValue(string screen, string element, string prop, string defaultValue)
        {
            var screenData = GetScreenData(screen);
            if (screenData == null) return defaultValue;
            if (!screenData.TryGetValue(element, out var elObj) || elObj is not Dictionary<string, object> elData)
                return defaultValue;
            if (!elData.TryGetValue(prop, out var val)) return defaultValue;
            return val?.ToString() ?? defaultValue;
        }

        private void SetPropertyValue(string screen, string element, string prop, string value)
        {
            var elData = GetOrCreateElementData(screen, element);
            elData[prop] = value;
            PushUndo(MiniJsonWriter.Serialize(_config));
            MarkDirty();
        }

        private bool HasElementOverrides(string screen, string element)
        {
            var screenData = GetScreenData(screen);
            return screenData != null && screenData.ContainsKey(element);
        }

        private bool HasPropertyOverride(string screen, string element, string prop)
        {
            var screenData = GetScreenData(screen);
            if (screenData == null) return false;
            if (!screenData.TryGetValue(element, out var elObj) || elObj is not Dictionary<string, object> elData)
                return false;
            return elData.ContainsKey(prop);
        }

        private void ResetElementToDefaults(string screen, string element)
        {
            var screenData = GetScreenData(screen);
            if (screenData == null) return;
            screenData.Remove(element);

            // Clean up empty screen
            if (screenData.Count == 0 || (screenData.Count == 1 && screenData.ContainsKey("_custom")))
                _config.Remove(screen);

            PushUndo(MiniJsonWriter.Serialize(_config));
            MarkDirty();
        }

        // ═══════════════════════════════════════════════════════════════
        //  SAVE / LOAD / UNDO
        // ═══════════════════════════════════════════════════════════════

        protected override void Reload()
        {
            _config = LoadJson(CONFIG_PATH) ?? new Dictionary<string, object>();
            RebuildScreenList();
            if (_selectedScreen != null)
            {
                RebuildElementList();
                RebuildPreview();
                if (_selectedElement != null)
                    RebuildInspector();
            }
            MarkClean();
            PushInitialState(MiniJsonWriter.Serialize(_config));
        }

        protected override void Save()
        {
            EnsureConfig();
            SaveJson(CONFIG_PATH, _config);
            MarkClean();
            SetStatus("Saved", EditorStyles.StatusSaved);
        }

        protected override void RestoreSnapshot(string jsonSnapshot)
        {
            _config = MiniJson.Deserialize(jsonSnapshot) as Dictionary<string, object>
                      ?? new Dictionary<string, object>();
            RebuildScreenList();
            if (_selectedScreen != null)
            {
                RebuildElementList();
                RebuildPreview();
                if (_selectedElement != null)
                    RebuildInspector();
            }
            MarkDirty();
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static Color ParseColor(string value)
        {
            if (string.IsNullOrEmpty(value)) return Colors.White;
            var parts = value.Split(',');
            if (parts.Length < 3) return Colors.White;
            float r = ParseFloat(parts[0].Trim());
            float g = ParseFloat(parts[1].Trim());
            float b = ParseFloat(parts[2].Trim());
            return new Color(r, g, b);
        }

        private static float ParseFloat(string value)
        {
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f))
                return f;
            return 0f;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SCHEMA TYPES
        // ═══════════════════════════════════════════════════════════════

        private enum PropType { String, Float, Int, Bool, Color }

        private class PropDef
        {
            public string Name;
            public PropType Type;
            public string DefaultValue;
            public float Min, Max;
        }

        private class ElementDef
        {
            public string Name;
            public string Description;
            public PropDef[] Properties;
        }

        private class ScreenDef
        {
            public string Name;
            public string Description;
            public ElementDef[] Elements;

            public ScreenDef(string name, string desc, ElementDef[] elements)
            {
                Name = name;
                Description = desc;
                Elements = elements;
            }
        }

        // Factory helpers for concise schema definitions
        private static PropDef Prop(string name, PropType type, string defaultValue, float min = 0, float max = 0)
            => new() { Name = name, Type = type, DefaultValue = defaultValue, Min = min, Max = max };

        private static ElementDef El(string name, string desc, PropDef[] props)
            => new() { Name = name, Description = desc, Properties = props };

        // ═══════════════════════════════════════════════════════════════
        //  TEST API
        // ═══════════════════════════════════════════════════════════════

        private int _testScreenIndex;

        public override SubViewport TestGetViewport() => _previewViewport;

        public override void TestCycleNext(string property)
        {
            switch (property)
            {
                case "screen":
                    var screenNames = ScreenDefinitions.Keys.ToList();
                    if (screenNames.Count == 0) break;
                    _testScreenIndex = (_testScreenIndex + 1) % screenNames.Count;
                    SelectScreen(screenNames[_testScreenIndex]);
                    break;
                case "layout":
                    _layoutMode = !_layoutMode;
                    if (_layoutToggleBtn != null)
                    {
                        _layoutToggleBtn.ButtonPressed = _layoutMode;
                        _layoutToggleBtn.Text = _layoutMode ? "Layout Mode [ON]" : "Layout Mode";
                        _layoutToggleBtn.AddThemeColorOverride("font_color",
                            _layoutMode ? new Color(0.9f, 0.7f, 0.2f) : EditorStyles.TextSecondary);
                    }
                    RebuildPreview();
                    break;
            }
        }
    }
}
