namespace JunkbotArena
{
    public static class Constants
    {
        // Physics layers (Godot uses 1-based layer numbers)
        public const int LAYER_DEFAULT = 1;
        public const int LAYER_PLAYER = 2;
        public const int LAYER_COMPANION = 3;
        public const int LAYER_ENEMY = 4;
        public const int LAYER_PLAYER_PROJECTILE = 5;
        public const int LAYER_ENEMY_PROJECTILE = 6;
        public const int LAYER_INTERACTABLE = 7;
        public const int LAYER_GROUND = 8;

        // Layer Masks (Godot uses bit flags, layer N = bit N-1)
        public static readonly uint MASK_GROUND = 1u << (LAYER_GROUND - 1);
        public static readonly uint MASK_ENEMY = 1u << (LAYER_ENEMY - 1);
        public static readonly uint MASK_PLAYER = 1u << (LAYER_PLAYER - 1);
        public static readonly uint MASK_INTERACTABLE = 1u << (LAYER_INTERACTABLE - 1);
        public static readonly uint MASK_DAMAGEABLE =
            (1u << (LAYER_PLAYER - 1)) |
            (1u << (LAYER_COMPANION - 1)) |
            (1u << (LAYER_ENEMY - 1));

        // Groups (Godot equivalent of tags)
        public const string GROUP_PLAYER = "Player";
        public const string GROUP_COMPANION = "Companion";
        public const string GROUP_ENEMY = "Enemy";
        public const string GROUP_INTERACTABLE = "Interactable";

        // Save
        public const string SAVE_FILE = "junkbot_save.json";

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
        public const float CAMERA_DISTANCE = 18f;
        public const float CAMERA_MIN_ZOOM = 10f;
        public const float CAMERA_MAX_ZOOM = 30f;

        // Affix limits
        public const int MAX_AFFIXES = 3;
        public const int MAX_PREFIXES = 2;
        public const int MAX_SUFFIXES = 1;

        // Dungeon generation
        public const float ROOM_SPACING = 32f;
        public const int DEFAULT_GRID_SIZE = 12;

        // Scene paths (Godot uses res:// paths)
        public const string SCENE_MAIN_MENU = "res://Scenes/MainMenu.tscn";
        public const string SCENE_SECTOR = "res://Scenes/Sector.tscn";
        public const string SCENE_PLAYER = "res://Scenes/Player/Player.tscn";
        public const string SCENE_CHARACTER_CREATION = "res://Scenes/CharacterCreation.tscn";
        public const string SCENE_ENEMY = "res://Scenes/Enemies/Enemy.tscn";
        public const string SCENE_ITEM_PICKUP = "res://Scenes/Items/ItemPickup.tscn";
        public const string SCENE_COMPANION = "res://Scenes/Companion/Companion.tscn";
        public const string SCENE_SAFE_ROOM = "res://Scenes/SafeRoom.tscn";
    }
}
