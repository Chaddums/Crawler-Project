using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Serializable data for meta perk persistence.
    /// </summary>
    public class MetaPerkSaveData
    {
        public List<int> AllocatedIds { get; set; } = new();
        public int AvailablePoints { get; set; }

        /// <summary>Total runs completed. Used for memory bleed narrative progression.</summary>
        public int RunCount { get; set; }

        /// <summary>Key = "planet_floor" (e.g. "1_1"), value = points awarded.</summary>
        public Dictionary<string, int> ClearedMilestones { get; set; } = new();

        /// <summary>Key = planet id, value = commander kills on that planet.</summary>
        public Dictionary<string, int> CommanderKills { get; set; } = new();
    }

    /// <summary>
    /// Static helper for loading/saving meta perk data to user://vine_meta.json.
    /// </summary>
    public static class MetaPerkSave
    {
        private const string SavePath = "user://vine_meta.json";

        public static MetaPerkSaveData Load()
        {
            if (!FileAccess.FileExists(SavePath))
                return CreateDefault();

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
                return CreateDefault();

            var text = file.GetAsText();
            var json = new Json();
            var err = json.Parse(text);
            if (err != Error.Ok)
            {
                GD.PushWarning($"[MetaPerkSave] Failed to parse save: {json.GetErrorMessage()}");
                return CreateDefault();
            }

            var dict = json.Data.AsGodotDictionary();
            var data = new MetaPerkSaveData();

            if (dict.ContainsKey("allocated"))
            {
                var arr = dict["allocated"].AsGodotArray();
                foreach (var id in arr)
                    data.AllocatedIds.Add(id.AsInt32());
            }

            if (dict.ContainsKey("points"))
                data.AvailablePoints = dict["points"].AsInt32();

            if (dict.ContainsKey("milestones"))
            {
                var ms = dict["milestones"].AsGodotDictionary();
                foreach (var key in ms.Keys)
                    data.ClearedMilestones[key.AsString()] = ms[key].AsInt32();
            }

            if (dict.ContainsKey("commander_kills"))
            {
                var ck = dict["commander_kills"].AsGodotDictionary();
                foreach (var key in ck.Keys)
                    data.CommanderKills[key.AsString()] = ck[key].AsInt32();
            }

            // Ensure root (id 0) is always allocated
            if (!data.AllocatedIds.Contains(0))
                data.AllocatedIds.Add(0);

            return data;
        }

        public static void Save(MetaPerkSaveData data)
        {
            var dict = new Godot.Collections.Dictionary();

            var arr = new Godot.Collections.Array();
            foreach (var id in data.AllocatedIds)
                arr.Add(id);
            dict["allocated"] = arr;

            dict["points"] = data.AvailablePoints;

            var ms = new Godot.Collections.Dictionary();
            foreach (var kv in data.ClearedMilestones)
                ms[kv.Key] = kv.Value;
            dict["milestones"] = ms;

            var ck = new Godot.Collections.Dictionary();
            foreach (var kv in data.CommanderKills)
                ck[kv.Key] = kv.Value;
            dict["commander_kills"] = ck;

            var text = Json.Stringify(dict, "  ");
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"[MetaPerkSave] Failed to open save file for writing");
                return;
            }
            file.StoreString(text);
        }

        public static void Reset()
        {
            var data = CreateDefault();
            Save(data);
        }

        /// <summary>
        /// Award points for completing a floor milestone.
        /// Returns the number of points awarded (0 if already cleared).
        /// Floor 1 awards 3 points, others award 2 points.
        /// </summary>
        public static int TryAwardFloorPoints(MetaPerkSaveData data, int planet, int floor)
        {
            string key = $"{planet}_{floor}";
            if (data.ClearedMilestones.ContainsKey(key))
                return 0;

            int points = floor == 1 ? 3 : 2;
            data.ClearedMilestones[key] = points;
            data.AvailablePoints += points;
            return points;
        }

        private static MetaPerkSaveData CreateDefault()
        {
            var data = new MetaPerkSaveData();
            data.AllocatedIds.Add(0); // Root always allocated
            return data;
        }
    }
}
