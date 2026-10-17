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
        /// <summary>Bigger bodies BIT climbs into at set levels (<see cref="MechFrame"/>), lowest level first.</summary>
        public List<MechFrame> Frames { get; set; } = new();

        /// <summary>The frame BIT rides at <paramref name="level"/>: the one with the highest level at or under it (null: on foot).</summary>
        public MechFrame FrameFor(int level)
        {
            MechFrame best = null;
            foreach (var f in Frames)
                if (f.Level <= level && (best == null || f.Level > best.Level)) best = f;
            return best;
        }

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
        /// <summary>Share of move speed added per level (BIT grows, so without this it read as slowing down).</summary>
        public float MoveSpeedPerLevel { get; set; } = 0.05f;
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
        /// <summary>box | sphere | cylinder | cone | capsule | torus | prism, when there is no model.</summary>
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
        /// <summary>A glow part's own colour (hex), instead of BIT's accent (relic gear: ice, lightning).</summary>
        public string Color { get; set; } = "";
        /// <summary>Glow strength for a coloured glow part.</summary>
        public float Glow { get; set; } = 2.6f;
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

    /// <summary>
    /// A bigger body BIT climbs into at <see cref="Level"/> (the playtest: "BIT should still be the
    /// main character, then as he levels you get into a bigger bot"). BIT rides it, visible at the
    /// <see cref="Seat"/>; the frame has its own weapon (<see cref="Muzzles"/>, <see cref="Shot"/>),
    /// stats and Q, E and R. Sizes here are in world units at the frame's <see cref="Height"/>.
    /// </summary>
    public class MechFrame
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; } = 4;
        public string Model { get; set; } = "";
        /// <summary>Texture the hull shader reads its panel detail and paint from.</summary>
        public string Texture { get; set; } = "";
        public float Height { get; set; } = 2.6f;
        /// <summary>"quaternius": the same rig and clips as BIT's body (clips by name, arms that aim); else clips by state.</summary>
        public string Rig { get; set; } = "";
        /// <summary>Body circle kept out of rock (BIT's is BIT_BODY_RADIUS).</summary>
        public float BodyRadius { get; set; } = 0.5f;
        /// <summary>Raised by this on top of its rest-pose grounding (a clip that stands lower than the rest pose sank into the floor).</summary>
        public float Lift { get; set; }
        public MechSeat Seat { get; set; } = new();
        public List<MechMuzzle> Muzzles { get; set; } = new();
        public MechFrameHull Hull { get; set; } = new();
        public MechFrameStats Stats { get; set; } = new();
        public MechFrameShot Shot { get; set; } = new();
        /// <summary>Q, E and R, in that order.</summary>
        public List<MechAbility> Abilities { get; set; } = new();
        /// <summary>The celebration's line when BIT climbs in, and BIT's on the comms.</summary>
        public string Arrive { get; set; } = "";
        public string Line { get; set; } = "";
    }

    /// <summary>Where BIT rides: a bone of the frame, an offset from it (right, up, forward in the frame's facing) and BIT's size there.</summary>
    public class MechSeat
    {
        public string Bone { get; set; } = "";
        public float[] Offset { get; set; } = { 0, 0, 0 };
        /// <summary>BIT's size riding, as a share of its size on foot.</summary>
        public float Scale { get; set; } = 0.45f;
    }

    /// <summary>A gun barrel: a bone (the hand) and how far past it shots leave, forward and up.</summary>
    public class MechMuzzle
    {
        public string Bone { get; set; } = "";
        public float Forward { get; set; } = 0.3f;
        public float Up { get; set; } = 0f;
        /// <summary>R or L: the arm that fires (its shoot clip on a quaternius rig).</summary>
        public string Hand { get; set; } = "";
    }

    public class MechFrameHull
    {
        /// <summary>Saturation and brightness where the texture's paint starts and is fully lit (BIT's: 0.35, 0.55).</summary>
        public float PaintLo { get; set; } = 0.35f;
        public float PaintHi { get; set; } = 0.55f;
        public float Glow { get; set; } = 2.2f;
        /// <summary>How much of the texture's brightness shows as panel detail (BIT's: 0.9).</summary>
        public float Detail { get; set; } = 0.9f;
    }

    /// <summary>What the frame changes about BIT (on top of its level and upgrades).</summary>
    public class MechFrameStats
    {
        /// <summary>Health added on climbing in (BIT is healed by it).</summary>
        public float Health { get; set; }
        public float Damage { get; set; } = 1f;
        public float Rate { get; set; } = 1f;
        /// <summary>Added to BIT's auto-fire reach.</summary>
        public float Range { get; set; }
        public float Speed { get; set; } = 1f;
        /// <summary>Share of the naruto run's sprint the frame keeps.</summary>
        public float Sprint { get; set; } = 1f;
        /// <summary>Damage taken is multiplied by this.</summary>
        public float Taken { get; set; } = 1f;
    }

    public class MechFrameShot
    {
        public string Color { get; set; } = "accent";
        public float Size { get; set; } = 1f;
        public float Speed { get; set; } = 22f;
        /// <summary>A shell bursts on what it hits: everything within this takes <see cref="SplashShare"/> of the hit (0: none).</summary>
        public float Splash { get; set; }
        public float SplashShare { get; set; } = 0.5f;
    }

    /// <summary>
    /// One of a frame's abilities. <see cref="Kind"/> picks what it does: "salvo" (rockets at the
    /// toughest enemies in reach), "repair" (the Spire, BIT and towers in reach, and with
    /// <see cref="Seconds"/> fortifies them), "overclock" (towers in reach for <see cref="Seconds"/>;
    /// <see cref="Strength"/> above 0 also speeds the frame's own guns), "slam" (damage, stun and a
    /// shove round the frame).
    /// </summary>
    public class MechAbility
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public float Cooldown { get; set; } = 8f;
        public float Cost { get; set; } = 20f;
        public float Range { get; set; } = 10f;
        /// <summary>Times BIT's shot damage.</summary>
        public float Damage { get; set; }
        public float Stun { get; set; }
        public int Count { get; set; }
        public float Radius { get; set; }
        public float Seconds { get; set; }
        public float Strength { get; set; }
        /// <summary>Share of a tower's health a repair puts back.</summary>
        public float Repair { get; set; }
        /// <summary>Cells a slam throws enemies back.</summary>
        public float Shove { get; set; }
        public string Color { get; set; } = "#ffd040";
        public string Text { get; set; } = "";
    }
}
