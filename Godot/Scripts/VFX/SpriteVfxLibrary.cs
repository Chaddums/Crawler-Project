using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Loads PNG frame sequences from VFX/Sprites/ into SpriteFrames resources,
    /// caches them, and spawns billboard AnimatedSprite3D nodes in the 3D world.
    /// </summary>
    public static class SpriteVfxLibrary
    {
        private static readonly Dictionary<string, SpriteFrames> _cache = new();
        private static bool _scanned;

        private const string VFX_ROOT = "res://VFX/Sprites/";

        /// <summary>
        /// Scan VFX/Sprites/ for frame sequence folders and pre-cache them.
        /// Safe to call multiple times.
        /// </summary>
        public static void Initialize()
        {
            if (_scanned) return;
            _scanned = true;

            using var dir = DirAccess.Open(VFX_ROOT);
            if (dir == null)
            {
                GD.PrintErr($"[SpriteVfxLibrary] Cannot open {VFX_ROOT}");
                return;
            }

            dir.ListDirBegin();
            string folder = dir.GetNext();
            while (!string.IsNullOrEmpty(folder))
            {
                if (dir.CurrentIsDir() && !folder.StartsWith("."))
                    LoadSequence(folder);
                folder = dir.GetNext();
            }
            dir.ListDirEnd();

            // Load sprite sheet-based effects
            LoadSpriteSheets();

            GD.Print($"[SpriteVfxLibrary] Loaded {_cache.Count} sprite VFX sequences");
        }

        private static void LoadSequence(string folderName)
        {
            string path = VFX_ROOT + folderName + "/";
            using var dir = DirAccess.Open(path);
            if (dir == null) return;

            // Collect PNG files sorted by name (frame order)
            var pngs = new List<string>();
            dir.ListDirBegin();
            string file = dir.GetNext();
            while (!string.IsNullOrEmpty(file))
            {
                if (!dir.CurrentIsDir() && file.ToLower().EndsWith(".png"))
                    pngs.Add(file);
                file = dir.GetNext();
            }
            dir.ListDirEnd();

            if (pngs.Count == 0) return;
            pngs.Sort();

            var frames = new SpriteFrames();
            // Remove the default animation and create our own
            if (frames.HasAnimation("default"))
                frames.RemoveAnimation("default");
            frames.AddAnimation(folderName);
            frames.SetAnimationLoop(folderName, false);
            frames.SetAnimationSpeed(folderName, 30); // 30 fps

            foreach (string png in pngs)
            {
                var tex = GD.Load<Texture2D>(path + png);
                if (tex != null)
                    frames.AddFrame(folderName, tex);
            }

            _cache[folderName] = frames;
        }

        private const string SHEET_ROOT = "res://VFX/SpriteSheets/";

        /// <summary>
        /// Load sprite sheet-based effects (blood splashes, explosions).
        /// Each PNG is a 4×4 grid of 64px frames.
        /// </summary>
        private static void LoadSpriteSheets()
        {
            LoadSheetFolder("blood", "blood", 4, 4);
            LoadSheetFolder("explosion", "explosion", 4, 4);
        }

        private static void LoadSheetFolder(string folderName, string prefix, int cols, int rows)
        {
            string path = SHEET_ROOT + folderName + "/";
            using var dir = DirAccess.Open(path);
            if (dir == null) return;

            var files = new List<string>();
            dir.ListDirBegin();
            string file = dir.GetNext();
            while (!string.IsNullOrEmpty(file))
            {
                if (!dir.CurrentIsDir() && file.ToLower().EndsWith(".png"))
                    files.Add(file);
                file = dir.GetNext();
            }
            dir.ListDirEnd();
            files.Sort();

            // Load each sheet as a separate named effect
            int idx = 0;
            foreach (string sheetFile in files)
            {
                var tex = GD.Load<Texture2D>(path + sheetFile);
                if (tex == null) continue;

                string effectName = $"{prefix}_{idx:D2}";
                int frameW = (int)tex.GetWidth() / cols;
                int frameH = (int)tex.GetHeight() / rows;

                var frames = new SpriteFrames();
                if (frames.HasAnimation("default"))
                    frames.RemoveAnimation("default");
                frames.AddAnimation(effectName);
                frames.SetAnimationLoop(effectName, false);
                frames.SetAnimationSpeed(effectName, 20);

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        var atlas = new AtlasTexture();
                        atlas.Atlas = tex;
                        atlas.Region = new Rect2(c * frameW, r * frameH, frameW, frameH);
                        frames.AddFrame(effectName, atlas);
                    }
                }

                _cache[effectName] = frames;
                idx++;
            }
        }

        /// <summary>
        /// Check if a named sprite VFX sequence exists.
        /// </summary>
        public static bool Has(string name) => _cache.ContainsKey(name);

        /// <summary>
        /// Get all available sprite VFX names.
        /// </summary>
        public static IEnumerable<string> AvailableEffects => _cache.Keys;

        /// <summary>
        /// Spawn a one-shot billboard AnimatedSprite3D at a world position.
        /// Auto-frees when animation completes.
        /// </summary>
        public static AnimatedSprite3D Spawn(
            Node parent,
            Vector3 worldPos,
            string effectName,
            float scale = 1f,
            float speedScale = 1f,
            bool loop = false,
            Color? modulate = null)
        {
            if (!_cache.TryGetValue(effectName, out var frames))
            {
                GD.PrintErr($"[SpriteVfxLibrary] Unknown effect: {effectName}");
                return null;
            }

            var sprite = new AnimatedSprite3D();
            sprite.SpriteFrames = frames;
            sprite.Animation = effectName;
            sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            sprite.Shaded = false;
            sprite.AlphaCut = SpriteBase3D.AlphaCutMode.Discard;
            sprite.RenderPriority = 1;
            sprite.NoDepthTest = true;
            sprite.PixelSize = 0.002f * scale; // ~1 meter per 500px at scale 1

            if (modulate.HasValue)
                sprite.Modulate = modulate.Value;

            sprite.SpeedScale = speedScale;

            parent.AddChild(sprite);
            sprite.GlobalPosition = worldPos;
            sprite.Play(effectName);

            if (loop)
            {
                // Duplicate frames so we don't mutate the cached resource
                var loopFrames = frames.Duplicate() as SpriteFrames;
                loopFrames.SetAnimationLoop(effectName, true);
                sprite.SpriteFrames = loopFrames;
                sprite.Play(effectName);
            }
            else
            {
                // Auto-free when animation finishes
                sprite.AnimationFinished += () =>
                {
                    if (GodotObject.IsInstanceValid(sprite))
                        sprite.QueueFree();
                };
            }

            return sprite;
        }

        /// <summary>
        /// Spawn a looping billboard sprite (for persistent effects like loot glow).
        /// Caller is responsible for freeing.
        /// </summary>
        public static AnimatedSprite3D SpawnLooping(
            Node parent,
            Vector3 worldPos,
            string effectName,
            float scale = 1f,
            Color? modulate = null)
        {
            return Spawn(parent, worldPos, effectName, scale, 1f, true, modulate);
        }

        /// <summary>
        /// Get the SpriteFrames resource for a named effect (for custom use).
        /// </summary>
        public static SpriteFrames GetFrames(string name)
        {
            _cache.TryGetValue(name, out var frames);
            return frames;
        }

        // ===== CONVENIENCE METHODS =====

        /// <summary>
        /// Spawn the appropriate loot box ceremony effect for a given tier.
        /// </summary>
        public static AnimatedSprite3D SpawnLootBoxEffect(Node parent, Vector3 worldPos, LootBoxTier tier, float scale = 2f)
        {
            string name = tier switch
            {
                LootBoxTier.Bronze => "lootbox_bronze",
                LootBoxTier.Silver => "lootbox_silver",
                LootBoxTier.Gold => "lootbox_gold",
                LootBoxTier.Diamond => "lootbox_diamond",
                LootBoxTier.Legendary => "lootbox_legendary",
                LootBoxTier.Celestial => "lootbox_celestial",
                _ => "lootbox_bronze"
            };

            if (!Has(name)) return null;
            return Spawn(parent, worldPos, name, scale);
        }

        /// <summary>
        /// Spawn a loot glow loop under a world item.
        /// Returns the sprite — caller must free it when the item is collected.
        /// </summary>
        public static AnimatedSprite3D SpawnLootGlow(Node parent, Vector3 worldPos, bool rare = false)
        {
            string name = rare ? "loot_glow_02" : "loot_glow_01";
            if (!Has(name)) return null;
            return SpawnLooping(parent, worldPos, name, 1.5f);
        }

        /// <summary>
        /// Spawn a random explosion effect at a position.
        /// Uses bomb frame sequences (large, dramatic) or sheet-based explosions (small, fast).
        /// </summary>
        public static AnimatedSprite3D SpawnExplosion(Node parent, Vector3 worldPos, float scale = 1.5f, bool small = false)
        {
            if (small)
            {
                // Small sheet-based explosion
                int count = 0;
                while (Has($"explosion_{count:D2}")) count++;
                if (count == 0) return null;
                string name = $"explosion_{GD.RandRange(0, count - 1):D2}";
                return Spawn(parent, worldPos, name, scale * 3f); // Scale up since sheets are tiny
            }

            // Large bomb frame sequence
            string[] variants = { "bomb_01", "bomb_02", "bomb_03" };
            string bombName = variants[GD.RandRange(0, variants.Length - 1)];
            if (!Has(bombName)) return null;
            return Spawn(parent, worldPos, bombName, scale);
        }

        /// <summary>
        /// Spawn a random blood splash at a hit point.
        /// </summary>
        public static AnimatedSprite3D SpawnBloodSplash(Node parent, Vector3 worldPos, float scale = 1f)
        {
            int count = 0;
            while (Has($"blood_{count:D2}")) count++;
            if (count == 0) return null;
            string name = $"blood_{GD.RandRange(0, count - 1):D2}";
            return Spawn(parent, worldPos, name, scale * 3f); // Scale up since sheets are 64px
        }

        /// <summary>
        /// Spawn the level-up effect at the player's position.
        /// </summary>
        public static AnimatedSprite3D SpawnLevelUp(Node parent, Vector3 worldPos)
        {
            if (!Has("level_up")) return null;
            return Spawn(parent, worldPos + Vector3.Up * 0.5f, "level_up", 2.5f);
        }

        /// <summary>
        /// Spawn the crafting upgrade effect at the bench position.
        /// </summary>
        public static AnimatedSprite3D SpawnCraftEffect(Node parent, Vector3 worldPos)
        {
            if (!Has("craft_upgrade")) return null;
            return Spawn(parent, worldPos + Vector3.Up * 0.8f, "craft_upgrade", 2f);
        }

        /// <summary>
        /// Spawn a room reveal VFX for the dungeon intro sequence.
        /// One-shot vertical light beam or stylized effect based on room type.
        /// </summary>
        public static AnimatedSprite3D SpawnRoomReveal(Node parent, Vector3 worldPos, RoomType roomType, float scale = 8f)
        {
            string name = roomType switch
            {
                RoomType.Treasure => "room_treasure_confetti",
                RoomType.Boss => "vlight_red",
                RoomType.Shop => "vlight_green",
                RoomType.Event => "vlight_prism",
                RoomType.Megabonk => "room_megabonk_cartoon",
                _ => "vlight_diamond"
            };

            if (!Has(name))
            {
                GD.PrintErr($"[SpriteVfxLibrary] Missing room reveal effect '{name}'");
                return null;
            }

            var sprite = Spawn(parent, worldPos, name, scale, speedScale: 0.5f, loop: true);

            // VFX PNGs have proper RGBA alpha channels with smooth gradients.
            // Use Disabled (full alpha blending) instead of Discard for soft wispy edges.
            if (sprite != null)
                sprite.AlphaCut = SpriteBase3D.AlphaCutMode.Disabled;

            return sprite;
        }

        /// <summary>
        /// Spawn a looping card effect behind a loot item row based on rarity.
        /// Returns the sprite — caller must free it when the ceremony ends.
        /// </summary>
        public static AnimatedSprite3D SpawnCardEffect(Node parent, Vector3 worldPos, ItemRarity rarity, float scale = 1f)
        {
            // Map rarity to card effect — more impressive effects for higher rarity
            string name = rarity switch
            {
                ItemRarity.Common => "card_specks",        // Subtle purple specks
                ItemRarity.Uncommon => "card_dream",       // Dreamy particles
                ItemRarity.Rare => "card_ethereal",        // Ethereal light filaments
                ItemRarity.Epic => "card_dynasty",         // Purple gold dynasty
                ItemRarity.Legendary => "card_golden",     // Golden radiance
                ItemRarity.Absurd => "card_prismatic",     // Prismatic radiance (rainbow)
                _ => "card_glitter"
            };

            if (!Has(name)) return null;
            return SpawnLooping(parent, worldPos, name, scale);
        }
    }
}
