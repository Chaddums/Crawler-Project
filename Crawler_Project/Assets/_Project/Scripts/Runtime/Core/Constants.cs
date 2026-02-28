namespace DungeonCrawlerCarl
{
    public static class Constants
    {
        // Layers
        public const int LAYER_DEFAULT = 0;
        public const int LAYER_PLAYER = 6;
        public const int LAYER_COMPANION = 7;
        public const int LAYER_ENEMY = 8;
        public const int LAYER_PLAYER_PROJECTILE = 9;
        public const int LAYER_ENEMY_PROJECTILE = 10;
        public const int LAYER_INTERACTABLE = 11;
        public const int LAYER_GROUND = 12;
        public const int LAYER_UI = 13;

        // Layer Masks
        public static readonly int MASK_GROUND = 1 << LAYER_GROUND;
        public static readonly int MASK_ENEMY = 1 << LAYER_ENEMY;
        public static readonly int MASK_PLAYER = 1 << LAYER_PLAYER;
        public static readonly int MASK_INTERACTABLE = 1 << LAYER_INTERACTABLE;
        public static readonly int MASK_DAMAGEABLE = (1 << LAYER_PLAYER) | (1 << LAYER_COMPANION) | (1 << LAYER_ENEMY);

        // Tags
        public const string TAG_PLAYER = "Player";
        public const string TAG_COMPANION = "Companion";
        public const string TAG_ENEMY = "Enemy";
        public const string TAG_INTERACTABLE = "Interactable";

        // Save
        public const string SAVE_FILE = "crawlercarl_save.json";

        // Gameplay defaults
        public const float DEFAULT_MOVE_SPEED = 6f;
        public const float DEFAULT_ATTACK_RANGE = 2f;
        public const float DEFAULT_AGGRO_RANGE = 10f;
        public const int DEFAULT_INVENTORY_SIZE = 30;
        public const int MAX_ABILITY_SLOTS = 6;
        public const float MIN_COMMENTARY_INTERVAL = 3f;

        // Camera defaults
        public const float CAMERA_ANGLE_X = 35f;
        public const float CAMERA_ANGLE_Y = 45f;
        public const float CAMERA_DISTANCE = 20f;
        public const float CAMERA_MIN_ZOOM = 12f;
        public const float CAMERA_MAX_ZOOM = 30f;

        // Scene names
        public const string SCENE_BOOT = "Boot";
        public const string SCENE_MAIN_MENU = "MainMenu";
        public const string SCENE_CHARACTER_CREATION = "CharacterCreation";
        public const string SCENE_STAIRWELL = "Stairwell";
        public const string SCENE_TEST_COMBAT = "TestCombat";
    }
}
