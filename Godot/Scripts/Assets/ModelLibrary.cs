using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Static registry mapping game concepts to res://Models/ asset paths.
    /// Auto-scans for .glb/.tscn files on Initialize(). Falls back gracefully
    /// when assets are not downloaded.
    /// </summary>
    public static class ModelLibrary
    {
        // category -> (id -> res:// path)
        private static readonly Dictionary<string, Dictionary<string, string>> _registry = new();
        private static readonly Dictionary<string, PackedScene> _cache = new();
        private static readonly HashSet<string> _failedPaths = new();
        private static bool _initialized;

        // Category → subfolder mapping (scans both legacy Models/ and PolygonDungeon packs)
        private static readonly Dictionary<string, string> _categoryFolders = new()
        {
            { "player",    "res://Models/Characters/Player" },
            { "enemy",     "res://Models/Characters/Enemies" },
            { "companion", "res://Models/Characters/Companions" },
            { "weapon",  "res://Models/Weapons" },
            { "animation", "res://Models/Animations" },
            { "dungeon", "res://Models/Dungeon" },
            { "prop",    "res://Models/Dungeon/Props" },
            { "floor",   "res://Models/Dungeon/Floors" },
            { "wall",    "res://Models/Dungeon/Walls" },
            { "door",    "res://Models/Dungeon/Doors" },
            { "detail",  "res://Models/Dungeon/Details" },
            { "item",    "res://Models/Items" },
            { "hazard",  "res://Models/Dungeon/Hazards" },
            { "building","res://Models/Dungeon/Buildings" },
        };

        // Additional scan folders — merged into existing categories
        private static readonly (string category, string folder)[] _extraScanFolders = new[]
        {
            ("prop",    "res://Assets/PolygonDungeon/Prefabs/Props"),
            ("prop",    "res://Assets/PolygonDungeon/Prefabs/Environments/Misc"),
            ("wall",    "res://Assets/PolygonDungeon/Prefabs/Environments/Walls"),
            ("door",    "res://Assets/PolygonDungeon/Prefabs/Environments/Walls"),
            ("floor",   "res://Assets/PolygonDungeon/Prefabs/Environments/Floors"),
            ("pillar",  "res://Assets/PolygonDungeon/Prefabs/Environments/Pillars"),
            ("prop",    "res://Assets/PolygonDungeon/Prefabs/Environments/Pillars"),
            ("rock",    "res://Assets/PolygonDungeon/Prefabs/Environments/Rocks"),
            ("wood",    "res://Assets/PolygonDungeon/Prefabs/Environments/Wood"),
            ("bone",    "res://Assets/PolygonDungeon/Prefabs/Environments/Bones"),
            ("item",    "res://Assets/PolygonDungeon/Prefabs/Items"),
            ("weapon",  "res://Assets/PolygonDungeon/Prefabs/Weapons"),
            ("enemy",   "res://Assets/PolygonDungeon/Prefabs/Characters"),
            ("boss",    "res://Assets/PolygonMech/SourceFiles/FBX"),
            ("boss",    "res://Assets/RetroMech/Model"),
            ("boss_anim", "res://Assets/RetroMech/Animations"),
            ("prop",    "res://Models/Dungeon/Props/KitBash"),
            ("door",    "res://Models/Dungeon/Doors/KitBash"),
            ("hazard",  "res://Models/Dungeon/Hazards"),
            ("building","res://Models/Dungeon/Buildings"),
            ("attachment", "res://Assets/PolygonMech/SourceFiles/FBX/MechAttachments"),
        };

        /// <summary>
        /// Scan res://Models/ subfolders and register all .glb and .tscn files.
        /// Safe to call multiple times (no-ops after first).
        /// </summary>
        /// <summary>
        /// When true, ModelLibrary skips scanning and all TryLoad calls return null
        /// (procedural fallback). Set to false once imported models are properly
        /// configured with correct scale and materials.
        /// </summary>
        public static bool ForceProcedural { get; set; } = false;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (ForceProcedural)
            {
                GD.Print("[ModelLibrary] Initialized: ForceProcedural=true, using procedural fallback for all models");
                return;
            }

            foreach (var (category, folder) in _categoryFolders)
            {
                if (!_registry.ContainsKey(category))
                    _registry[category] = new Dictionary<string, string>();

                ScanFolder(category, folder);
            }

            // Scan POLYGON Dungeon pack folders into categories
            foreach (var (category, folder) in _extraScanFolders)
            {
                if (!_registry.ContainsKey(category))
                    _registry[category] = new Dictionary<string, string>();

                ScanFolder(category, folder);
            }

            // Aliases — map game IDs to POLYGON prefab names and legacy models
            AddAlias("prop", "pillar", "column_1");

            // Props: map simple game IDs → POLYGON prefabs
            AddAlias("prop", "barrel",         "sm_prop_barrel_01");
            AddAlias("prop", "barrel_broken",  "sm_prop_barrel_broken_01");
            AddAlias("prop", "crate",          "sm_prop_crate_metal_01");
            AddAlias("prop", "crate_long",     "sm_prop_crate_metal_03");
            AddAlias("prop", "chest",          "sm_prop_chest_01");
            AddAlias("prop", "weapon_rack",    "sm_prop_weaponrack_01");
            AddAlias("prop", "torch",          "sm_prop_torchstick_01");
            AddAlias("prop", "statue",         "sm_env_statue_01");
            AddAlias("prop", "pedestal",       "sm_prop_stonechair_01");
            AddAlias("prop", "shelf_tall",     "sm_prop_bookcase_01");
            AddAlias("prop", "computer",       "sm_prop_tech_switchboard_01");
            AddAlias("prop", "computer_small", "sm_prop_tech_lever_01");
            AddAlias("prop", "pipes",          "sm_prop_tech_pipe_01");
            AddAlias("prop", "capsule",        "sm_prop_tech_chamber_01");
            AddAlias("prop", "pod",            "sm_prop_tech_chamber_01");
            AddAlias("prop", "vessel",         "sm_prop_vase_01");
            AddAlias("prop", "vessel_short",   "sm_prop_vase_02");
            AddAlias("prop", "vessel_tall",    "sm_prop_vase_04");
            AddAlias("prop", "laser",          "sm_prop_tech_crystal_01");
            AddAlias("prop", "portal",         "sm_prop_tech_engine_01");
            AddAlias("prop", "teleporter",     "sm_prop_tech_turbine_01");

            // Layout obstacles: low walls and tall walls
            AddAlias("prop", "low_wall",     "sm_prop_metal_fence_01");
            AddCrossAlias("prop", "tall_wall",  "wood", "sm_env_basement_support_wall_01");
            AddAlias("prop", "low_wall_2",   "sm_env_railing_01");
            AddCrossAlias("prop", "tall_wall_2", "wood", "sm_env_basement_wallpanel_01");
            AddAlias("prop", "low_barrier",  "sm_env_fence_metal_spikes_01");
            AddAlias("prop", "railing",      "sm_env_railing_02");

            // Additional POLYGON props for room dressing
            AddAlias("prop", "brazier",      "sm_prop_brazier_01");
            AddAlias("prop", "bonfire",      "sm_prop_bonfire_01");
            AddAlias("prop", "obelisk",      "sm_env_obelisk_01");
            AddAlias("prop", "altar",        "sm_env_alter_01");
            AddAlias("prop", "grate",        "sm_env_grate_ground_01");
            AddAlias("prop", "lantern",      "sm_prop_lantern_01");
            AddAlias("prop", "mine_cart",    "sm_prop_minecart_01");
            AddAlias("prop", "conveyor",     "sm_prop_tech_conveyor_01");
            AddAlias("prop", "engine",       "sm_prop_tech_engine_01");
            AddAlias("prop", "turbine",      "sm_prop_tech_turbine_01");
            AddAlias("prop", "cog",          "sm_prop_tech_cog_01");

            // Sci-Fi Essentials props
            AddAlias("prop", "scifi_barrel",    "prop_barrel1");
            AddAlias("prop", "scifi_crate",     "prop_crate");
            AddAlias("prop", "scifi_crate_lg",  "prop_crate_large");
            AddAlias("prop", "desk",            "prop_desk_medium");
            AddAlias("prop", "desk_large",      "prop_desk_l");
            AddAlias("prop", "locker",          "prop_locker");
            AddAlias("prop", "health_pack",     "prop_healthpack");
            AddAlias("prop", "satellite_dish",  "prop_satellitedish");
            AddAlias("prop", "shelves",         "prop_shelves_thintall");
            AddAlias("prop", "shelves_short",   "prop_shelves_thinshort");
            AddAlias("prop", "shelves_wide",    "prop_shelves_widetall");
            AddAlias("prop", "mine_prop",       "prop_mine");
            AddAlias("prop", "scifi_chest",     "prop_chest");
            AddAlias("prop", "chair",           "prop_chair");

            // ── KitBash3D Future Warfare Props ──────────────────────────────
            // Industrial sector
            AddAlias("prop", "kb_generator",       "kb3d_ftw_propgenerator_a_grp");
            AddAlias("prop", "kb_generator_2",     "kb3d_ftw_propgenerator_b_grp");
            AddAlias("prop", "kb_generator_3",     "kb3d_ftw_propgenerator_c_grp");
            AddAlias("prop", "kb_barrel",          "kb3d_ftw_propbarrel_a_grp");
            AddAlias("prop", "kb_barrels",         "kb3d_ftw_propbarrels_a_grp");
            AddAlias("prop", "kb_crate",           "kb3d_ftw_propcrate_a_grp");
            AddAlias("prop", "kb_crate_2",         "kb3d_ftw_propcrate_b_grp");
            AddAlias("prop", "kb_crate_3",         "kb3d_ftw_propcrate_c_grp");
            AddAlias("prop", "kb_crate_stack",     "kb3d_ftw_propcratesstack_a_grp");
            AddAlias("prop", "kb_crate_stack_2",   "kb3d_ftw_propcratesstack_b_grp");
            AddAlias("prop", "kb_crate_stack_3",   "kb3d_ftw_propcratesstack_c_grp");
            AddAlias("prop", "kb_container",       "kb3d_ftw_propcontainer_a_grp");
            AddAlias("prop", "kb_container_2",     "kb3d_ftw_propcontainer_b_grp");
            AddAlias("prop", "kb_cargo_box",       "kb3d_ftw_propcargobox_a_grp");
            AddAlias("prop", "kb_cargo_box_2",     "kb3d_ftw_propcargobox_b_grp");
            AddAlias("prop", "kb_pallet",          "kb3d_ftw_proppallet_a_grp");
            AddAlias("prop", "kb_pallet_2",        "kb3d_ftw_proppallet_b_grp");
            AddAlias("prop", "kb_pallet_stack",    "kb3d_ftw_proppalletstack_a_grp");
            AddAlias("prop", "kb_hvac",            "kb3d_ftw_prophvac_a_grp");
            AddAlias("prop", "kb_hvac_2",          "kb3d_ftw_prophvac_b_grp");
            AddAlias("prop", "kb_pipes",           "kb3d_ftw_proprepeater_a_grp");
            AddAlias("prop", "kb_pipes_2",         "kb3d_ftw_proprepeater_b_grp");
            AddAlias("prop", "kb_power_mast",      "kb3d_ftw_proppwermast_a_grp");

            // Toxic/Environmental
            AddAlias("prop", "kb_iso_tank",        "kb3d_ftw_propisotank_a_grp");
            AddAlias("prop", "kb_iso_tank_2",      "kb3d_ftw_propisotank_b_grp");
            // Air filtration GLBs not present in pack — use HVAC models as visual stand-ins
            AddAlias("prop", "kb_air_filter",      "kb3d_ftw_prophvac_a_grp");
            AddAlias("prop", "kb_air_filter_2",    "kb3d_ftw_prophvac_b_grp");
            AddAlias("prop", "kb_trash_bag",       "kb3d_ftw_proptrashbag_a_grp");
            AddAlias("prop", "kb_trash_bags",      "kb3d_ftw_proptrashbags_a_grp");
            AddAlias("prop", "kb_tires",           "kb3d_ftw_proptires_a_grp");
            AddAlias("prop", "kb_tires_2",         "kb3d_ftw_proptires_b_grp");

            // Military
            AddAlias("prop", "kb_barrier",         "kb3d_ftw_propbarrier_a_grp");
            AddAlias("prop", "kb_barrier_2",       "kb3d_ftw_propbarrier_b_grp");
            AddAlias("prop", "kb_barrier_3",       "kb3d_ftw_propbarrier_c_grp");
            AddAlias("prop", "kb_sandbags",        "kb3d_ftw_propsandbags_a_grp");
            AddAlias("prop", "kb_sandbags_2",      "kb3d_ftw_propsandbags_b_grp");
            AddAlias("prop", "kb_sandbags_3",      "kb3d_ftw_propsandbags_c_grp");
            AddAlias("prop", "kb_fence",           "kb3d_ftw_propfencesegment_a_grp");
            AddAlias("prop", "kb_fence_2",         "kb3d_ftw_propfencesegment_b_grp");
            AddAlias("prop", "kb_fence_post",      "kb3d_ftw_propfencepost_a_grp");
            AddAlias("prop", "kb_hedgehog",        "kb3d_ftw_propantitankhedgehog_a_grp");
            AddAlias("prop", "kb_covered_body",    "kb3d_ftw_propcoveredbody_a_grp");
            AddAlias("prop", "kb_flag_pole",       "kb3d_ftw_propflagpole_a_grp");

            // Lab/Tech
            AddAlias("prop", "kb_radar",           "kb3d_ftw_propradar_a_grp");
            AddAlias("prop", "kb_radar_2",         "kb3d_ftw_propradar_b_grp");
            AddAlias("prop", "kb_mobile_radar",    "kb3d_ftw_propmobileradar_a_grp");
            AddAlias("prop", "kb_antenna",         "kb3d_ftw_propantenna_a_grp");
            AddAlias("prop", "kb_antenna_2",       "kb3d_ftw_propantenna_b_grp");
            AddAlias("prop", "kb_satellite",       "kb3d_ftw_propsatellite_a_grp");
            AddAlias("prop", "kb_satellite_2",     "kb3d_ftw_propsatellite_b_grp");
            AddAlias("prop", "kb_security_cam",    "kb3d_ftw_propsecuritycamera_a_grp");
            AddAlias("prop", "kb_security_term",   "kb3d_ftw_propsecurityterminal_a_grp");
            AddAlias("prop", "kb_solar_panel",     "kb3d_ftw_propsolarpanel_a_grp");

            // Structural/Corridor
            AddAlias("prop", "kb_pillar",          "kb3d_ftw_proppillar_a_grp");
            AddAlias("prop", "kb_guard_rail",      "kb3d_ftw_propguardrail_a_grp");
            AddAlias("prop", "kb_overhang",        "kb3d_ftw_propoverhang_a_grp");
            AddAlias("prop", "kb_platform",        "kb3d_ftw_propplatform_a_grp");
            AddAlias("prop", "kb_bridge",          "kb3d_ftw_propbridgesegment_a_grp");
            AddAlias("prop", "kb_corridor",        "kb3d_ftw_propcorridor_a_grp");
            AddAlias("prop", "kb_corridor_2",      "kb3d_ftw_propcorridor_b_grp");
            AddAlias("prop", "kb_corridor_3",      "kb3d_ftw_propcorridor_c_grp");
            AddAlias("prop", "kb_stairs",          "kb3d_ftw_propstair_a_grp");
            AddAlias("prop", "kb_elevator",        "kb3d_ftw_propelevator_a_grp");

            // Lighting
            AddAlias("prop", "kb_lamp_post",       "kb3d_ftw_proplamppost_a_grp");
            AddAlias("prop", "kb_lamp_post_2",     "kb3d_ftw_proplamppost_b_grp");
            AddAlias("prop", "kb_lamp_post_3",     "kb3d_ftw_proplamppost_c_grp");
            AddAlias("prop", "kb_lamp_post_4",     "kb3d_ftw_proplamppost_d_grp");
            AddAlias("prop", "kb_wall_lamp",       "kb3d_ftw_propwalllamp_a_grp");
            AddAlias("prop", "kb_wall_lamp_2",     "kb3d_ftw_propwalllamp_b_grp");
            AddAlias("prop", "kb_linear_lamp",     "kb3d_ftw_proplinearlamp_a_grp");
            AddAlias("prop", "kb_spotlight",       "kb3d_ftw_propportablespotlight_a_grp");

            // Tables/Furniture
            AddAlias("prop", "kb_table",           "kb3d_ftw_proptableset_a_grp");
            AddAlias("prop", "kb_table_2",         "kb3d_ftw_proptableset_b_grp");
            AddAlias("prop", "kb_stretcher",       "kb3d_ftw_propmedicalstretcher_a_grp");
            AddAlias("prop", "kb_ladder",          "kb3d_ftw_propfoldingladder_a_grp");
            AddAlias("prop", "kb_sign",            "kb3d_ftw_propsign_a_grp");
            AddAlias("prop", "kb_sign_2",          "kb3d_ftw_propsign_b_grp");
            AddAlias("prop", "kb_sign_3",          "kb3d_ftw_propsign_c_grp");
            AddAlias("prop", "kb_sign_4",          "kb3d_ftw_propsign_d_grp");

            // Hero/Special
            AddAlias("prop", "kb_robot_observer",  "kb3d_ftw_proprobotobserver_a_grp");
            AddAlias("prop", "kb_surveillance",    "kb3d_ftw_propsurveillancerobot_a_grp");
            AddAlias("prop", "kb_drop_pod",        "kb3d_ftw_heropropdroppod_a_grp");
            AddAlias("prop", "kb_drop_pod_2",      "kb3d_ftw_heropropdroppod_b_grp");
            AddAlias("prop", "kb_armory",          "kb3d_ftw_heropropmobilearmory_a_grp");
            AddAlias("prop", "kb_armory_station",  "kb3d_ftw_heropropmobilearmorystation_a_grp");
            AddAlias("prop", "kb_crashed_rocket",  "kb3d_ftw_propcrashedrocket_a_grp");

            // ── KitBash3D Doors ─────────────────────────────────────────────
            AddAlias("door", "kb_double_door",     "kb3d_ftw_propdoubledoor_a_grp");
            AddAlias("door", "kb_double_door_2",   "kb3d_ftw_propdoubledoor_b_grp");
            AddAlias("door", "kb_double_door_3",   "kb3d_ftw_propdoubledoor_c_grp");
            AddAlias("door", "kb_double_door_4",   "kb3d_ftw_propdoubledoor_d_grp");
            AddAlias("door", "kb_double_door_5",   "kb3d_ftw_propdoubledoor_e_grp");
            AddAlias("door", "kb_single_door",     "kb3d_ftw_propsingledoor_a_grp");
            AddAlias("door", "kb_single_door_2",   "kb3d_ftw_propsingledoor_b_grp");
            AddAlias("door", "kb_single_door_3",   "kb3d_ftw_propsingledoor_c_grp");
            AddAlias("door", "kb_garage_door",     "kb3d_ftw_propgaragedoor_a_grp");
            AddAlias("door", "kb_garage_door_2",   "kb3d_ftw_propgaragedoor_b_grp");
            AddAlias("door", "kb_garage_door_3",   "kb3d_ftw_propgaragedoor_c_grp");

            // ── KitBash3D Hazards (AXIS environmental weapons) ──────────────
            AddAlias("hazard", "axis_plasma_gun",       "kb3d_ftw_heropropplasmagun_a_grp");
            AddAlias("hazard", "axis_rocket_launcher",  "kb3d_ftw_heropropmultirocketlauncher_a_grp");
            AddAlias("hazard", "axis_turret",           "kb3d_ftw_propturret_a_grp");
            AddAlias("hazard", "axis_turret_2",         "kb3d_ftw_propturret_b_grp");
            AddAlias("hazard", "axis_turret_3",         "kb3d_ftw_propturret_c_grp");
            AddAlias("hazard", "axis_weapon",           "kb3d_ftw_propweapon_a_grp");
            AddAlias("hazard", "axis_weapon_2",         "kb3d_ftw_propweapon_b_grp");

            // ── KitBash3D Buildings (safe rooms / AXIS final run) ───────────
            AddAlias("building", "field_barracks",      "kb3d_ftw_bldgsmfieldbarracks_a_grp");
            AddAlias("building", "fuel_tanks",          "kb3d_ftw_bldgsmfueltanks_a_grp");
            AddAlias("building", "checkpoint",          "kb3d_ftw_bldgsmcheckpoint_a_grp");
            AddAlias("building", "outpost",             "kb3d_ftw_bldgsmoutpost_a_grp");
            AddAlias("building", "logistics_center",    "kb3d_ftw_bldgsmlogisticscenter_a_grp");
            AddAlias("building", "water_towers",        "kb3d_ftw_bldgsmwatertowers_a_grp");
            AddAlias("building", "trench",              "kb3d_ftw_bldgsmtrench_a_grp");
            AddAlias("building", "admin_center",        "kb3d_ftw_bldgmdadmincenter_a_grp");

            // Walls: map game wall IDs → POLYGON walls
            AddAlias("wall", "wall_1", "sm_env_wall_01");
            AddAlias("wall", "wall_2", "sm_env_wall_02");
            AddAlias("wall", "wall_3", "sm_env_wall_03");
            AddAlias("wall", "wall_4", "sm_env_wall_04");
            AddAlias("wall", "wall_5", "sm_env_wall_05");

            // Floors: map game floor IDs → POLYGON tiles
            AddAlias("floor", "floortile_basic",  "sm_env_tiles_01");
            AddAlias("floor", "floortile_basic2", "sm_env_tiles_02");

            // Doors: map game door IDs → POLYGON doors (prefer large variants for 10-unit doorways)
            AddAlias("door", "door_frame",       "sm_env_door_large_frame_01");
            AddAlias("door", "door_frame_small", "sm_env_door_frame_01");
            AddAlias("door", "door_double",      "sm_env_doordouble_flat_01");
            AddAlias("door", "door_large_stone", "sm_env_door_large_stone_01");
            AddAlias("door", "door_large_wood",  "sm_env_door_large_wood_01");

            // ── Enemy Model Assignments ──────────────────────────────────────
            // Each enemy gets a DISTINCT model — no duplicates.
            //
            // NOTE: Many enemy FBX files in Models/Characters/Enemies/ are copies
            // of player models (same byte size) and must NOT be used directly.
            // Unique enemy FBX: eye_drone (425K), trilobite (1.3M), quad_shell (1.5M),
            //   decoy_unit (94K, shared with junk_lurker/shard_lobber/volt_sprinter copies).
            // Player-model copies (DO NOT USE): scrap_rat=leela, calibration_target=stan,
            //   wire_worm=mike, spark_drone/patch_bot/overclock_drone=quaternius_robot,
            //   rust_titan/axis_disciple=corrupted_sentry.
            //
            // STRUCTURAL FIX: Fake enemy FBX files (player model copies) have been
            // DELETED from Models/Characters/Enemies/. Only genuine models remain:
            //   spider_bot.fbx, gun_robot.fbx, eye_drone.fbx, trilobite.fbx,
            //   quad_shell.fbx, quaternius_robot.fbx, decoy_unit.fbx, spark_drone.fbx
            //
            // Every enemy must be explicitly aliased to an existing model.
            // DO NOT re-create fake FBX files — they cause recurring player-as-enemy bugs.

            // NOTE: spider_bot.fbx, eye_drone.fbx, and spark_drone.fbx all have
            // MISSING TEXTURES (source PNGs don't exist, only .import sidecars).
            // Until proper textures are sourced, these FBX files render black/invisible.
            // All aliases to these broken models have been REMOVED so enemies fall through
            // to their procedural builders (which have proper colors and animations).
            //
            // Enemies with working FBX models:
            //   trilobite.fbx, quad_shell.fbx, quaternius_robot.fbx, decoy_unit.fbx, gun_robot.fbx
            //
            // TODO: Source textures for spider_bot, eye_drone, spark_drone and re-enable aliases.

            // decoy_unit auto-resolves from own FBX (has working textures)

            // AXIS Avatar — RetroMech six-legged spider (distinct from enemy spider bots)
            AddAlias("boss",  "axis_avatar",        "sk_iso_mech");

            // Pillars: shorthand aliases
            AddAlias("pillar", "column_1", "sm_env_pillar_square_01");
            AddAlias("pillar", "column_2", "sm_env_pillar_round_01");
            AddAlias("pillar", "column_3", "sm_env_pillar_round_02");

            // Player frames: map BotFrameType names → mech model IDs
            AddAlias("player", "tincan",    "stan");
            AddAlias("player", "sparkplug", "leela");
            AddAlias("player", "rustbucket", "leela");   // compact frame, tinted steel blue
            AddAlias("player", "scrapheap", "george");
            AddAlias("player", "noisebox",  "stan");
            AddAlias("player", "clunker",   "george");

            // Weapons: map WeaponType names → FBX model IDs
            AddAlias("weapon", "pistol",     "hand_cannon");
            AddAlias("weapon", "rifle",      "assault_rifle");
            AddAlias("weapon", "shotgun",    "shotgun");
            AddAlias("weapon", "launcher",   "rocket_launcher");
            AddAlias("weapon", "repeater",   "gatling_gun");
            // Melee/AoE weapon models
            AddAlias("weapon", "blade_ring", "energy_sword");
            AddAlias("weapon", "flail_chain","war_hammer");
            AddAlias("weapon", "shock_coil", "arc_rifle");
            AddAlias("weapon", "flame_thrower", "power_rifle");

            // Sci-Fi Essentials weapons
            AddAlias("weapon", "scifi_pistol",   "scifi_pistol");
            AddAlias("weapon", "scifi_revolver", "scifi_revolver");
            AddAlias("weapon", "scifi_rifle",    "scifi_rifle");
            AddAlias("weapon", "scifi_sniper",   "scifi_sniper");

            // Kenney Blasters — direct ID aliases (scanned as kenney_blaster_X)
            AddAlias("weapon", "blaster_a", "kenney_blaster_a");
            AddAlias("weapon", "blaster_b", "kenney_blaster_b");
            AddAlias("weapon", "blaster_c", "kenney_blaster_c");
            AddAlias("weapon", "blaster_d", "kenney_blaster_d");
            AddAlias("weapon", "blaster_e", "kenney_blaster_e");
            AddAlias("weapon", "blaster_f", "kenney_blaster_f");
            AddAlias("weapon", "blaster_g", "kenney_blaster_g");
            AddAlias("weapon", "blaster_h", "kenney_blaster_h");
            AddAlias("weapon", "blaster_i", "kenney_blaster_i");
            AddAlias("weapon", "blaster_j", "kenney_blaster_j");
            AddAlias("weapon", "blaster_k", "kenney_blaster_k");
            AddAlias("weapon", "blaster_l", "kenney_blaster_l");
            AddAlias("weapon", "blaster_m", "kenney_blaster_m");
            AddAlias("weapon", "blaster_n", "kenney_blaster_n");
            AddAlias("weapon", "blaster_o", "kenney_blaster_o");
            AddAlias("weapon", "blaster_p", "kenney_blaster_p");
            AddAlias("weapon", "blaster_q", "kenney_blaster_q");
            AddAlias("weapon", "blaster_r", "kenney_blaster_r");
            // Kenney Blasters — game weapon name aliases
            AddAlias("weapon", "blaster_alt_1",  "kenney_blaster_a");
            AddAlias("weapon", "blaster_alt_2",  "kenney_blaster_b");
            AddAlias("weapon", "blaster_alt_3",  "kenney_blaster_c");
            AddAlias("weapon", "blaster_alt_4",  "kenney_blaster_d");
            AddAlias("weapon", "blaster_alt_5",  "kenney_blaster_e");
            AddAlias("weapon", "blaster_alt_6",  "kenney_blaster_f");
            AddAlias("weapon", "blaster_alt_7",  "kenney_blaster_g");
            AddAlias("weapon", "blaster_alt_8",  "kenney_blaster_h");
            AddAlias("weapon", "blaster_alt_9",  "kenney_blaster_i");
            AddAlias("weapon", "blaster_alt_10", "kenney_blaster_j");
            AddAlias("weapon", "blaster_alt_11", "kenney_blaster_k");
            AddAlias("weapon", "blaster_alt_12", "kenney_blaster_l");
            AddAlias("weapon", "blaster_alt_13", "kenney_blaster_m");
            AddAlias("weapon", "blaster_alt_14", "kenney_blaster_n");
            AddAlias("weapon", "blaster_alt_15", "kenney_blaster_o");
            AddAlias("weapon", "blaster_alt_16", "kenney_blaster_p");
            AddAlias("weapon", "blaster_alt_17", "kenney_blaster_q");
            AddAlias("weapon", "blaster_alt_18", "kenney_blaster_r");

            int total = 0;
            foreach (var cat in _registry.Values)
                total += cat.Count;

            if (total > 0)
                GD.Print($"[ModelLibrary] Initialized: {total} models registered across {_registry.Count} categories");
            else
                GD.Print("[ModelLibrary] Initialized: no model assets found (using procedural fallback)");
        }

        private static void ScanFolder(string category, string folderPath)
        {
            if (!DirAccess.DirExistsAbsolute(folderPath)) return;

            using var dir = DirAccess.Open(folderPath);
            if (dir == null) return;

            // Collect subdirectory names first (avoids CurrentIsDir() edge cases on some platforms)
            var subdirs = new List<string>();
            var files = new List<string>();

            dir.ListDirBegin();
            string fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (dir.CurrentIsDir())
                {
                    if (fileName != "." && fileName != "..")
                        subdirs.Add(fileName);
                }
                else
                {
                    files.Add(fileName);
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();

            // Recurse into subdirectories
            foreach (var subdir in subdirs)
                ScanFolder(category, folderPath + "/" + subdir);

            // Track which base resource paths we've already registered (from original files)
            var registered = new HashSet<string>();

            // First pass: register original asset files (.glb, .tscn, .scn, .fbx)
            foreach (var file in files)
            {
                string lower = file.ToLower();
                if (lower.EndsWith(".glb") || lower.EndsWith(".tscn") || lower.EndsWith(".scn") || lower.EndsWith(".fbx"))
                {
                    string id = System.IO.Path.GetFileNameWithoutExtension(file).ToLower()
                        .Replace(" ", "_").Replace("-", "_");
                    string resPath = folderPath + "/" + file;
                    _registry[category][id] = resPath;
                    registered.Add(lower);
                }
            }

            // Second pass: pick up .import sidecar files for assets that weren't found directly.
            // In some Godot 4.x/.NET configurations, DirAccess may list "file.glb.import"
            // without also listing "file.glb". The resource path is still the original (sans .import).
            foreach (var file in files)
            {
                string lower = file.ToLower();
                if (!lower.EndsWith(".import")) continue;

                // Strip .import to get the original file name
                string baseName = file.Substring(0, file.Length - ".import".Length);
                string baseLower = lower.Substring(0, lower.Length - ".import".Length);

                // Only process asset types we care about
                if (!(baseLower.EndsWith(".glb") || baseLower.EndsWith(".tscn") || baseLower.EndsWith(".scn") || baseLower.EndsWith(".fbx")))
                    continue;

                // Skip if we already registered the original file
                if (registered.Contains(baseLower)) continue;

                string id = System.IO.Path.GetFileNameWithoutExtension(baseName).ToLower()
                    .Replace(" ", "_").Replace("-", "_");
                string resPath = folderPath + "/" + baseName;
                _registry[category][id] = resPath;
            }
        }

        /// <summary>
        /// Try to load and instantiate a model by category and id.
        /// Returns the instantiated Node3D, or null if not found (caller should fall back to procedural).
        /// </summary>
        public static Node3D TryLoad(string category, string id)
        {
            if (!_initialized) Initialize();

            id = id?.ToLower().Replace(" ", "_").Replace("-", "_");
            if (string.IsNullOrEmpty(id)) return null;

            if (!_registry.TryGetValue(category, out var entries)) return null;
            if (!entries.TryGetValue(id, out var resPath)) return null;

            // Check cache (skip paths that previously failed to load)
            if (_failedPaths.Contains(resPath)) return null;
            if (!_cache.TryGetValue(resPath, out var scene))
            {
                if (!ResourceLoader.Exists(resPath))
                {
                    // Try with type hint — GLB files are imported as PackedScene
                    if (!ResourceLoader.Exists(resPath, "PackedScene"))
                    {
                        _failedPaths.Add(resPath);
                        GD.PushWarning($"[ModelLibrary] Resource not found: {category}/{id} at {resPath}");
                        return null;
                    }
                }
                scene = GD.Load<PackedScene>(resPath);
                if (scene == null)
                {
                    _failedPaths.Add(resPath);
                    GD.PrintErr($"[ModelLibrary] Failed to load {category}/{id} from {resPath} — using procedural fallback");
                    return null;
                }
                _cache[resPath] = scene;
                GD.Print($"[ModelLibrary] Loaded {category}/{id} from {resPath}");
            }

            try
            {
                var instance = scene.Instantiate<Node3D>();
                return instance;
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[ModelLibrary] Failed to instantiate {category}/{id} from {resPath}: {ex.Message}");
                _failedPaths.Add(resPath);
                return null;
            }
        }

        private static void AddAlias(string category, string alias, string targetId)
        {
            if (!_registry.TryGetValue(category, out var entries))
            {
                GD.PrintErr($"[ModelLibrary] AddAlias failed: category '{category}' not in registry");
                return;
            }
            if (!entries.ContainsKey(targetId))
            {
                GD.PrintErr($"[ModelLibrary] AddAlias failed: '{category}/{targetId}' not found (alias '{alias}' won't resolve)");
                return;
            }
            entries[alias] = entries[targetId];
        }

        private static void AddCrossAlias(string aliasCategory, string alias, string sourceCategory, string targetId)
        {
            if (!_registry.TryGetValue(aliasCategory, out var destEntries))
            {
                GD.PrintErr($"[ModelLibrary] AddCrossAlias failed: category '{aliasCategory}' not in registry");
                return;
            }
            if (!_registry.TryGetValue(sourceCategory, out var srcEntries) || !srcEntries.ContainsKey(targetId))
            {
                GD.PrintErr($"[ModelLibrary] AddCrossAlias failed: '{sourceCategory}/{targetId}' not found");
                return;
            }
            destEntries[alias] = srcEntries[targetId];
        }

        /// <summary>
        /// Get all model IDs in a category (for random selection).
        /// </summary>
        public static string[] GetCategoryIds(string category)
        {
            if (!_initialized) Initialize();
            if (!_registry.TryGetValue(category, out var entries)) return System.Array.Empty<string>();
            var ids = new string[entries.Count];
            entries.Keys.CopyTo(ids, 0);
            return ids;
        }

        /// <summary>
        /// Get the resource path for a model ID in a category, or null if not found.
        /// Useful for resolving aliases back to the underlying model file name.
        /// </summary>
        public static string GetResourcePath(string category, string id)
        {
            if (!_initialized) Initialize();
            id = id?.ToLower().Replace(" ", "_").Replace("-", "_");
            if (string.IsNullOrEmpty(id)) return null;
            if (!_registry.TryGetValue(category, out var entries)) return null;
            return entries.TryGetValue(id, out var resPath) ? resPath : null;
        }

        /// <summary>
        /// Check if a model exists for the given category and id without loading it.
        /// </summary>
        public static bool HasModel(string category, string id)
        {
            if (!_initialized) Initialize();

            id = id?.ToLower().Replace(" ", "_").Replace("-", "_");
            if (string.IsNullOrEmpty(id)) return false;

            return _registry.TryGetValue(category, out var entries) && entries.ContainsKey(id);
        }

        /// <summary>
        /// Eagerly load and cache all models in a category so subsequent TryLoad calls
        /// are instant. Useful for preloading boss/enemy models at sector start.
        /// </summary>
        public static void PreloadCategory(string category)
        {
            if (!_initialized) Initialize();
            if (!_registry.TryGetValue(category, out var entries)) return;

            int loaded = 0;
            foreach (var (id, resPath) in entries)
            {
                if (_cache.ContainsKey(resPath)) { loaded++; continue; }

                // Clear any previous failure so we retry
                _failedPaths.Remove(resPath);

                if (!ResourceLoader.Exists(resPath)) continue;
                var scene = GD.Load<PackedScene>(resPath);
                if (scene != null)
                {
                    _cache[resPath] = scene;
                    loaded++;
                }
            }
            GD.Print($"[ModelLibrary] Preloaded {loaded}/{entries.Count} models in '{category}'");
        }
    }
}
