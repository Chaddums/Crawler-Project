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
                }),
                El("XPBar", "Experience bar", new PropDef[]
                {
                    Prop("FillColor", PropType.Color, "0.6,0.3,0.8"),
                    Prop("BgColor", PropType.Color, "0.15,0.1,0.2"),
                    Prop("Height", PropType.Float, "8", 4, 30),
                    Prop("BottomMargin", PropType.Float, "6", 0, 50),
                    Prop("FontSize", PropType.Int, "12", 8, 20),
                    Prop("LevelPrefix", PropType.String, "LVL"),
                }),
                El("BuffStrip", "Status effect indicator strip", new PropDef[]
                {
                    Prop("IconSize", PropType.Float, "32", 16, 64),
                    Prop("MaxVisible", PropType.Int, "8", 4, 16),
                    Prop("Spacing", PropType.Float, "4", 0, 16),
                    Prop("BgColor", PropType.Color, "0.1,0.1,0.12"),
                }),
                El("DashIndicator", "Dash charge pips", new PropDef[]
                {
                    Prop("ActiveColor", PropType.Color, "0.3,0.9,1.0"),
                    Prop("InactiveColor", PropType.Color, "0.3,0.3,0.35"),
                    Prop("LabelText", PropType.String, "DASH"),
                    Prop("FontSize", PropType.Int, "10", 8, 16),
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

            centerPanel.AddChild(EditorStyles.MakeLabel("Preview", EditorStyles.FontHeader, EditorStyles.TextSecondary));

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
        //  PREVIEW
        // ═══════════════════════════════════════════════════════════════

        private void RebuildPreview()
        {
            // Clear
            foreach (var child in _previewRoot.GetChildren())
                if (child is Node n) n.QueueFree();

            if (_selectedScreen == null) return;

            // Dark background
            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(0.05f, 0.05f, 0.08f);
            _previewRoot.AddChild(bg);

            // Screen label
            var screenLabel = EditorStyles.MakeLabel(_selectedScreen, EditorStyles.FontHeader, AccentColor);
            screenLabel.Position = new Vector2(10, 10);
            _previewRoot.AddChild(screenLabel);

            // Build a preview representation based on the screen type
            if (!ScreenDefinitions.TryGetValue(_selectedScreen, out var def)) return;

            float yOffset = 40;

            foreach (var el in def.Elements)
            {
                bool isSelected = el.Name == _selectedElement;
                var elPreview = BuildElementPreview(el, isSelected);
                elPreview.Position = new Vector2(20, yOffset);
                _previewRoot.AddChild(elPreview);
                yOffset += elPreview.Size.Y + 8;
            }
        }

        private Control BuildElementPreview(ElementDef el, bool isSelected)
        {
            var container = new PanelContainer();
            var style = EditorStyles.MakePanel(
                isSelected ? new Color(0.15f, 0.2f, 0.3f) : new Color(0.08f, 0.08f, 0.1f),
                isSelected ? AccentColor : EditorStyles.BorderColor,
                isSelected ? 2 : 1, 4, 8);
            container.AddThemeStyleboxOverride("panel", style);
            container.CustomMinimumSize = new Vector2(880, 0);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);

            // Element name
            vbox.AddChild(EditorStyles.MakeLabel(el.Name, EditorStyles.FontBody,
                isSelected ? AccentColor : EditorStyles.TextPrimary));

            // Show current property values as a preview row
            var propsRow = new HBoxContainer();
            propsRow.AddThemeConstantOverride("separation", 12);

            int shown = 0;
            foreach (var prop in el.Properties)
            {
                if (shown >= 6) break; // Show max 6 props inline

                string val = GetPropertyValue(_selectedScreen, el.Name, prop.Name, prop.DefaultValue);
                bool isOverridden = HasPropertyOverride(_selectedScreen, el.Name, prop.Name);

                if (prop.Type == PropType.Color)
                {
                    var colorRow = new HBoxContainer();
                    colorRow.AddThemeConstantOverride("separation", 4);

                    var swatch = new ColorRect();
                    swatch.CustomMinimumSize = new Vector2(16, 16);
                    swatch.Color = ParseColor(val);
                    colorRow.AddChild(swatch);

                    Color labelCol = isOverridden ? AccentColor : EditorStyles.TextMuted;
                    colorRow.AddChild(EditorStyles.MakeLabel(prop.Name, EditorStyles.FontTiny, labelCol));
                    propsRow.AddChild(colorRow);
                }
                else
                {
                    Color labelCol = isOverridden ? AccentColor : EditorStyles.TextMuted;
                    propsRow.AddChild(EditorStyles.MakeLabel($"{prop.Name}: {val}", EditorStyles.FontTiny, labelCol));
                }
                shown++;
            }

            vbox.AddChild(propsRow);
            container.AddChild(vbox);

            return container;
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
    }
}
