using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// How a player mech changes during a run: the XP it earns, what each level gives, and the
    /// gear each perk bolts on. One file per mech in Data/Mechs/{id}.json.
    ///
    /// Positions and sizes are in the model's own units, before it is scaled to PLAYER_HEIGHT.
    /// The model faces +Z; its right side is -X.
    /// </summary>
    public class MechSheet
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public MechXpSources Xp { get; set; } = new();
        public MechLevels Levels { get; set; } = new();
        /// <summary>Named mount points: a bone to follow (optional) and an offset.</summary>
        public Dictionary<string, MechSocket> Sockets { get; set; } = new();
        /// <summary>Perk id → the parts that perk bolts on.</summary>
        public Dictionary<string, List<MechPart>> Gear { get; set; } = new();

        private static readonly Dictionary<string, MechSheet> _cache = new();
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            IncludeFields = true,
        };

        /// <summary>Load Data/Mechs/{id}.json (cached). A missing or broken file gives an empty sheet.</summary>
        public static MechSheet Load(string id)
        {
            if (_cache.TryGetValue(id, out var cached)) return cached;
            string path = $"res://Data/Mechs/{id}.json";
            MechSheet sheet = null;
            if (Godot.FileAccess.FileExists(path))
            {
                using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                try { sheet = JsonSerializer.Deserialize<MechSheet>(f.GetAsText(), _json); }
                catch (JsonException e) { GD.PushError($"[Mech] {path}: {e.Message}"); }
            }
            else GD.PushWarning($"[Mech] No sheet at {path}");
            sheet ??= new MechSheet { Id = id };
            _cache[id] = sheet;
            return sheet;
        }

        /// <summary>Drop the cache (tests edit nothing, but the editor may).</summary>
        public static void ClearCache() => _cache.Clear();
    }

    public class MechXpSources
    {
        public float Kill { get; set; } = 1f;           // any enemy killed, by anyone
        public float PersonalKill { get; set; } = 1f;   // extra when the mech lands the kill
        public float CommanderKill { get; set; } = 5f;  // replaces Kill
        public float BossKill { get; set; } = 15f;      // replaces Kill
        public float WaveCleared { get; set; } = 3f;
    }

    public class MechLevels
    {
        public int Max { get; set; } = 10;
        /// <summary>XP from level n to n+1 = XpBase * XpGrowth^(n-1), rounded.</summary>
        public float XpBase { get; set; } = 10f;
        public float XpGrowth { get; set; } = 1.4f;
        /// <summary>Model scale added per level above 1.</summary>
        public float GrowthPerLevel { get; set; } = 0.04f;
        public float MaxHPPerLevel { get; set; } = 8f;
        /// <summary>Share of the starting attack damage added per level.</summary>
        public float AttackDamagePerLevel { get; set; } = 0.05f;
        /// <summary>Hull outline width at level 1, and added per level.</summary>
        public float OutlineWidth { get; set; } = 0.035f;
        public float OutlineWidthPerLevel { get; set; } = 0.003f;
        /// <summary>Eye glow at level 1, and added per level.</summary>
        public float EyeGlow { get; set; } = 4f;
        public float EyeGlowPerLevel { get; set; } = 0.4f;
        /// <summary>Parts added when a level is reached.</summary>
        public List<MechTier> Tiers { get; set; } = new();

        public float XpToNext(int level) =>
            level >= Max ? 0f : Mathf.Round(XpBase * Mathf.Pow(XpGrowth, level - 1));
    }

    public class MechTier
    {
        public int Level { get; set; }
        public string Name { get; set; } = "";
        public List<MechPart> Parts { get; set; } = new();
    }

    public class MechSocket
    {
        public string Bone { get; set; } = "";
        public float[] Offset { get; set; } = { 0, 0, 0 };
    }

    /// <summary>
    /// One piece of gear: a kit model fitted to a size, or a simple shape. Hull parts take the
    /// mech's hull look (dark body, outline); glow parts take its accent glow.
    /// </summary>
    public class MechPart
    {
        public string Socket { get; set; } = "";
        /// <summary>A model path (res://...). Its longest side is fitted to <see cref="Fit"/>.</summary>
        public string Model { get; set; } = "";
        public float Fit { get; set; } = 0.5f;
        /// <summary>box | sphere | cylinder | capsule | torus, when there is no model.</summary>
        public string Shape { get; set; } = "";
        /// <summary>box: x,y,z. sphere: radius. cylinder/capsule: radius, height. torus: inner, outer.</summary>
        public float[] Size { get; set; } = { 0.1f, 0.1f, 0.1f };
        /// <summary>Non-uniform scale on top (optional).</summary>
        public float[] Scale { get; set; }
        public float[] Rot { get; set; } = { 0, 0, 0 };
        public float[] Offset { get; set; } = { 0, 0, 0 };
        /// <summary>hull (BIT's dark body and outline), glow (its accent glow), metal (lit gunmetal) or kit (the model's own materials).</summary>
        public string Surface { get; set; } = "hull";
        /// <summary>Hull outline width as a share of the body's (-1: 0.25 for models, 1 for shapes).</summary>
        public float Outline { get; set; } = -1f;
        /// <summary>How much lighter than the body's dark the hull is (-1: default).</summary>
        public float Tone { get; set; } = -1f;
        /// <summary>Degrees per second around the part's own Y axis (radars, rings).</summary>
        public float Spin { get; set; }
        /// <summary>Weapons: where shots leave, in the part's space (after Rot).</summary>
        public float[] Muzzle { get; set; }
        /// <summary>Weapons: R or L picks the matching attack clip; empty plays the two-handed one.</summary>
        public string Hand { get; set; } = "";
        public MechProjectile Projectile { get; set; }

        public bool IsWeapon => Muzzle != null && Muzzle.Length == 3;
    }

    public class MechProjectile
    {
        /// <summary>Multiplier on the base shot's size.</summary>
        public float Size { get; set; } = 1f;
        /// <summary>"theme" (planet projectile colour), "accent" (player accent) or a hex colour.</summary>
        public string Color { get; set; } = "theme";
        public float Speed { get; set; } = 18f;
    }
}
