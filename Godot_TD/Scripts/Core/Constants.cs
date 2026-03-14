namespace JunkyardTD
{
    public static class Constants
    {
        // Grid
        public const float CELL_SIZE = 2f;       // World units per grid cell
        public const int DEFAULT_MAP_WIDTH = 24;
        public const int DEFAULT_MAP_HEIGHT = 16;

        // Physics layers
        public const int LAYER_DEFAULT = 1;
        public const int LAYER_TOWER = 2;
        public const int LAYER_ENEMY = 3;
        public const int LAYER_PROJECTILE = 4;
        public const int LAYER_SCRAP = 5;
        public const int LAYER_GROUND = 6;
        public const int LAYER_HERO = 7;

        public static readonly uint MASK_GROUND = 1u << (LAYER_GROUND - 1);
        public static readonly uint MASK_ENEMY = 1u << (LAYER_ENEMY - 1);
        public static readonly uint MASK_TOWER = 1u << (LAYER_TOWER - 1);

        // Groups
        public const string GROUP_TOWER = "Tower";
        public const string GROUP_ENEMY = "Enemy";
        public const string GROUP_PROJECTILE = "Projectile";
        public const string GROUP_SCRAP = "Scrap";

        // Economy
        public const int STARTING_SCRAP = 100;
        public const float SCRAP_DECAY_TIME = 15f;   // Seconds before uncollected scrap degrades
        public const float SCRAP_COLLECT_RADIUS = 2.5f;

        // Towers
        public const float TOWER_SELL_REFUND = 0.6f;  // 60% refund

        // Core (player base)
        public const float CORE_MAX_HEALTH = 100f;
        public const int CORE_LIVES = 20;             // Enemies that leak through

        // Camera
        public const float CAMERA_HEIGHT = 25f;
        public const float CAMERA_ANGLE = 55f;        // Top-down-ish
        public const float CAMERA_PAN_SPEED = 20f;
        public const float CAMERA_MIN_ZOOM = 15f;
        public const float CAMERA_MAX_ZOOM = 40f;

        // Waves
        public const float WAVE_PREP_TIME = 30f;      // Seconds between waves
        public const float ENEMY_SPAWN_INTERVAL = 0.8f;

        // Speed control
        public const float SPEED_NORMAL = 1f;
        public const float SPEED_FAST = 2f;
        public const float SPEED_ULTRA = 3f;

        // Terrain costs
        public const int BULLDOZE_COST = 5;
        public const int PILE_COST = 10;

        // Hero Bot
        public const int HERO_DEPLOY_COST = 25;
        public const float HERO_MOVE_SPEED = 8f;
        public const float HERO_REPAIR_AMOUNT = 20f;
        public const float HERO_REPAIR_RANGE = 3f;
        public const float HERO_SLAM_DAMAGE = 15f;
        public const float HERO_SLAM_RADIUS = 3f;
        public const float HERO_SLAM_COOLDOWN = 5f;
        public const float HERO_DEATH_RESPAWN_TIME = 10f;
        public const float HERO_MAX_HEALTH = 80f;
        public const string GROUP_HERO = "Hero";

        // Difficulty scaling per wave
        public const float DIFFICULTY_HP_SCALE = 0.15f;     // +15% HP per wave
        public const float DIFFICULTY_SPEED_SCALE = 0.03f;   // +3% speed per wave
        public const float DIFFICULTY_COUNT_SCALE = 0.1f;    // +10% count per wave

        // Scene paths
        public const string SCENE_MAIN_MENU = "res://Scenes/MainMenu.tscn";
        public const string SCENE_BATTLE = "res://Scenes/Battle.tscn";
        public const string SCENE_MAP_SELECT = "res://Scenes/MapSelect.tscn";
    }
}
