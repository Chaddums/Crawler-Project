namespace JunkyardTD
{
    public static class Constants
    {
        // Grid
        public const float CELL_SIZE = 2f;
        public const int DEFAULT_MAP_WIDTH = 24;
        public const int DEFAULT_MAP_HEIGHT = 16;

        // Physics layers
        public const int LAYER_DEFAULT = 1;
        public const int LAYER_TOWER = 2;
        public const int LAYER_ENEMY = 3;
        public const int LAYER_PROJECTILE = 4;
        public const int LAYER_RESOURCE = 5;  // S1: renamed from LAYER_SCRAP
        public const int LAYER_GROUND = 6;
        public const int LAYER_HERO = 7;

        public static readonly uint MASK_GROUND = 1u << (LAYER_GROUND - 1);
        public static readonly uint MASK_ENEMY = 1u << (LAYER_ENEMY - 1);
        public static readonly uint MASK_TOWER = 1u << (LAYER_TOWER - 1);

        // Groups
        public const string GROUP_TOWER = "Tower";
        public const string GROUP_ENEMY = "Enemy";
        public const string GROUP_PROJECTILE = "Projectile";
        public const string GROUP_RESOURCE = "Resource";  // S1: renamed from GROUP_SCRAP

        // Economy — S1: renamed scrap→resources
        public const int STARTING_RESOURCES = 100;
        public const float RESOURCE_DECAY_TIME = 15f;
        public const float RESOURCE_COLLECT_RADIUS = 2.5f;

        // Towers
        public const float TOWER_SELL_REFUND = 0.6f;

        // Core (player base)
        public const float CORE_MAX_HEALTH = 100f;
        public const int CORE_LIVES = 20;

        // Camera
        public const float CAMERA_HEIGHT = 25f;
        public const float CAMERA_ANGLE = 55f;
        public const float CAMERA_PAN_SPEED = 20f;
        public const float CAMERA_MIN_ZOOM = 8f;
        public const float CAMERA_MAX_ZOOM = 100f;

        // Waves
        public const float WAVE_PREP_TIME = 30f;
        public const float ENEMY_SPAWN_INTERVAL = 0.8f;

        // Speed control
        public const float SPEED_NORMAL = 1f;
        public const float SPEED_FAST = 2f;
        public const float SPEED_ULTRA = 3f;

        // Terrain costs
        public const int BULLDOZE_COST = 5;
        public const int PILE_COST = 10;

        // S1: removed HERO_* constants (HeroBotController deleted)

        // Difficulty scaling per wave
        public const float DIFFICULTY_HP_SCALE = 0.15f;
        public const float DIFFICULTY_SPEED_SCALE = 0.02f;
        public const float DIFFICULTY_COUNT_SCALE = 0.1f;

        // Scene paths
        public const string SCENE_MAIN_MENU = "res://Scenes/MainMenu.tscn";
        // S1: removed SCENE_BATTLE, SCENE_MAP_SELECT (Classic TD deleted)
        public const string SCENE_VINE_BATTLE = "res://Scenes/VineBattle.tscn";
        public const string SCENE_INTRO_CINEMATIC = "res://Scenes/IntroCinematic.tscn";
        public const string SCENE_VINE_DRAFT = "res://Scenes/VineDraft.tscn";

        // ── Vine Logic TD ──

        // Grid
        public const int VINE_MAP_WIDTH = 40;
        public const int VINE_MAP_HEIGHT = 24;
        public const float VINE_CELL_SIZE = 2f;

        // Heightmap terrain
        public const float HEIGHTMAP_MAX_AMPLITUDE = 3.5f;
        public const float HEIGHTMAP_NOISE_FREQ = 0.08f;
        public const float SLOPE_COST_FACTOR = 0.5f;
        public const float STEEP_THRESHOLD = 1.5f;
        public const int PROP_SCATTER_BASE = 8;
        public const int PROP_SCATTER_PER_LEVEL = 4;  // S1: renamed from PER_FLOOR

        // Signals
        public const float SIGNAL_TRAVEL_SPEED = 6f;
        public const float SIGNAL_PULSE_DURATION = 0.3f;
        public const float SIGNAL_BUFF_DECAY = 0.15f;

        // Nodes
        public const float SENSOR_RANGE = 7f;
        public const float DAMAGE_TOWER_RANGE = 8f;
        public const float DAMAGE_TOWER_DPS = 12f;
        public const float SLOW_FIELD_RANGE = 5f;
        public const float SLOW_FIELD_AMOUNT = 0.4f;
        public const float DELAY_DURATION = 2f;
        public const float TIMER_INTERVAL = 3f;
        public const float GATE_INPUT_WINDOW = 0.5f;
        public const float SWITCH_TOGGLE_TIME = 1.5f;
        public const float PUSH_PULL_FORCE = 5f;
        public const float BUFF_DAMAGE_BONUS = 0.25f;
        public const float BUFF_SPEED_BONUS = 0.15f;

        // Economy (vine mode) — S1: renamed scrap→resources
        public const int VINE_STARTING_RESOURCES = 90;
        public const int VINE_WAVE_BONUS = 15;

        // Enemies (vine mode)
        public const float VINE_ENEMY_BASE_SPEED = 2f;
        public const int VINE_CORE_LIVES = 10;

        // S1: removed VINE_FLOOR_COUNT, VINE_WAVES_PER_FLOOR (floors removed)

        // Spawn positioning
        public const float VINE_SPAWN_OFFSET = 12f;

        // Character model target heights
        public const float ENEMY_HEIGHT_STANDARD = 1.2f;
        public const float ENEMY_HEIGHT_SMALL = 0.8f;
        public const float ENEMY_HEIGHT_LARGE = 1.6f;
        public const float PLAYER_HEIGHT = 1.4f;
        public const float NODE_MODEL_HEIGHT = 1.5f;

        // Boss
        public const float BOSS_HP_MULTIPLIER = 4f;
        public const float BOSS_SCALE = 2f;
        public const float BOSS_SPEED_MULT = 0.5f;
        public const int BOSS_RESOURCE_VALUE = 50;  // S1: renamed from BOSS_SCRAP_VALUE

        // Scene paths (vine)
        public const string SCENE_VINE_PERK = "res://Scenes/VinePerkSelect.tscn";
        public const string SCENE_META_PERK = "res://Scenes/MetaPerkTree.tscn";
        public const string SCENE_LEVEL_EDITOR = "res://Scenes/LevelEditor.tscn";

        // Harvester
        public const float VINE_HARVESTER_MAX_HP = 200f;
        public const int VINE_HARVESTER_INCOME = 3;
        public const float VINE_HARVESTER_INCOME_INTERVAL = 5f;

        // Player — S1: renamed mana→materials
        public const float VINE_PLAYER_MAX_HP = 100f;
        public const float VINE_PLAYER_MAX_MATERIALS = 100f;
        public const float VINE_PLAYER_MOVE_SPEED = 3.2f;
        public const float VINE_PLAYER_ATTACK_RANGE = 6f;
        public const float VINE_PLAYER_ATTACK_DAMAGE = 8f;
        public const float VINE_PLAYER_ATTACK_SPEED = 1.5f;
        public const float VINE_PLAYER_MATERIALS_REGEN = 3f;
        public const float VINE_PLAYER_DEATH_PENALTY = 20f;
        public const float VINE_PLAYER_RESPAWN_TIME = 5f;
        public const string GROUP_VINE_PLAYER = "VinePlayer";

        // Enemy ranged attacks
        public const float ENEMY_ATTACK_RANGE = 5f;
        public const float ENEMY_ATTACK_DAMAGE = 4f;
        public const float ENEMY_FIRE_INTERVAL = 1.5f;

        // Tower health (effect nodes only)
        public const float VINE_NODE_BASE_HEALTH = 50f;

        // Physics layers (vine mode reuses existing)
        public const string GROUP_VINE_NODE = "VineNode";
        public const string GROUP_VINE_ENEMY = "VineEnemy";
    }
}
