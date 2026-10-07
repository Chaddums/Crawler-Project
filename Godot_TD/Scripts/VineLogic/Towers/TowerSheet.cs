using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What a tower looks like and how it moves: kit models and simple shapes on an optional
    /// pedestal, which of them turn to aim, recoil or spin, and where shots leave. One file per
    /// tower in Data/Towers/{id}.json, keyed by the tower's VineNodeData.Id.
    ///
    /// Units are world units. The tower is built from y = 0 (its cell's ground) up, centred on
    /// the cell (2 x 2). It must stay inside the cell at its rest yaw.
    /// </summary>
    public class TowerSheet
    {
        public string Id { get; set; } = "";
        /// <summary>A plinth the parts stand on; null for towers with their own platform.</summary>
        public TowerPedestal Pedestal { get; set; }
        public List<TowerPart> Parts { get; set; } = new();
        /// <summary>Where shots leave, in the aiming parts' own space. Empty = the barrel tip, found from the meshes.</summary>
        public float[] Muzzle { get; set; } = System.Array.Empty<float>();
        /// <summary>Which way the barrel points while idle, degrees from +Z toward +X (45 = along the cell's diagonal).</summary>
        public float RestYaw { get; set; } = 45f;
        /// <summary>Turn rate while tracking, degrees a second.</summary>
        public float AimSpeed { get; set; } = 220f;
        /// <summary>Slow side to side scan while there is nothing to shoot, degrees each way (0 = hold still).</summary>
        public float IdleSweep { get; set; } = 30f;
        /// <summary>How far the aiming parts kick back when the tower fires.</summary>
        public float Recoil { get; set; }
        /// <summary>"wall": a block that joins up with neighbouring walls.</summary>
        public string Special { get; set; } = "";

        private static readonly Dictionary<string, TowerSheet> _cache = new();
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            IncludeFields = true,
        };

        /// <summary>Load Data/Towers/{id}.json (cached). Null when the tower has no sheet.</summary>
        public static TowerSheet Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_cache.TryGetValue(id, out var cached)) return cached;
            string path = $"res://Data/Towers/{id}.json";
            TowerSheet sheet = null;
            if (Godot.FileAccess.FileExists(path))
            {
                using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                try { sheet = JsonSerializer.Deserialize<TowerSheet>(f.GetAsText(), _json); }
                catch (JsonException e) { GD.PushError($"[Tower] {path}: {e.Message}"); }
                if (sheet != null && string.IsNullOrEmpty(sheet.Id)) sheet.Id = id;
            }
            _cache[id] = sheet;
            return sheet;
        }

        public static void ClearCache() => _cache.Clear();
    }

    public class TowerPedestal
    {
        /// <summary>Radius of the body at its foot (the skirt reaches a little further).</summary>
        public float Radius { get; set; } = 0.72f;
        public float Height { get; set; } = 0.42f;
        public int Sides { get; set; } = 8;
        public string Body { get; set; } = "concrete";
        public string Skirt { get; set; } = "trim";
        public string Cap { get; set; } = "panel";
        /// <summary>A lit band in the tower's colour just under the cap.</summary>
        public bool Band { get; set; } = true;
    }

    public class TowerPart
    {
        // A kit model...
        public string Model { get; set; } = "";
        /// <summary>Scale so the longest side seen from above is this long.</summary>
        public float Fit { get; set; }
        /// <summary>Or scale so it is this tall.</summary>
        public float FitHeight { get; set; }
        /// <summary>Or this scale outright.</summary>
        public float Scale { get; set; }
        /// <summary>Kit sub-meshes to leave out (name contains any of these).</summary>
        public string[] Hide { get; set; } = System.Array.Empty<string>();
        /// <summary>Kit sub-meshes that turn to aim while the rest stays put (name contains any).</summary>
        public string[] AimNodes { get; set; } = System.Array.Empty<string>();
        /// <summary>Kit sub-mesh whose middle is the turning point and the cell centre.</summary>
        public string Pivot { get; set; } = "";

        // ...or a shape: box / cylinder / cone / sphere / torus / prism (sides from Sides)
        public string Shape { get; set; } = "";
        public float[] Size { get; set; } = System.Array.Empty<float>();
        public int Sides { get; set; } = 12;
        /// <summary>concrete, panel, trim, hazard, white, dark, copper, chrome, glow (tower colour), tar.</summary>
        public string Surface { get; set; } = "trim";
        /// <summary>Glow brightness for "glow" surfaces.</summary>
        public float Glow { get; set; } = 0.8f;

        public float[] Pos { get; set; } = System.Array.Empty<float>();
        public float[] Rot { get; set; } = System.Array.Empty<float>();
        /// <summary>Stand on the pedestal (positions start at its top) rather than the ground.</summary>
        public bool OnPedestal { get; set; } = true;
        /// <summary>The whole part turns to aim.</summary>
        public bool Aim { get; set; }
        /// <summary>Turns about its own up axis, degrees a second.</summary>
        public float Spin { get; set; }
        /// <summary>Shoots forward this far and back when the tower fires (a ram).</summary>
        public float Punch { get; set; }
        /// <summary>Wall face on this side (+x, -x, +z, -z): shown only when no wall is next door there.</summary>
        public string Face { get; set; } = "";
        /// <summary>Wall link on this side: shown only when a wall is next door there.</summary>
        public string Link { get; set; } = "";
    }
}
