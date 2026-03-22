using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// Static helper for loading/saving loadout data to user://loadouts.json.
    /// Follows the same pattern as MetaPerkSave.
    /// </summary>
    public static class LoadoutSave
    {
        private const string SavePath = "user://loadouts.json";
        private const int LOADOUT_COUNT = 6;
        private const int TOWER_SLOTS = 4;
        private const int RELIC_SLOTS = 3;

        public struct SavedLoadout
        {
            public string Name;
            public string[] TowerBaseNames;
            public string[] FuncBaseNames;
            public string[] TowerModNames;
            public string[] FuncModNames;
            public string[] RelicNames;
        }

        public struct LoadoutSaveData
        {
            public SavedLoadout[] Loadouts;
        }

        public static LoadoutSaveData Load()
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
                GD.PushWarning($"[LoadoutSave] Failed to parse save: {json.GetErrorMessage()}");
                return CreateDefault();
            }

            var root = json.Data.AsGodotDictionary();
            var data = CreateDefault();

            if (!root.ContainsKey("loadouts"))
                return data;

            var arr = root["loadouts"].AsGodotArray();
            for (int i = 0; i < arr.Count && i < LOADOUT_COUNT; i++)
            {
                var entry = arr[i].AsGodotDictionary();
                if (entry.ContainsKey("name"))
                    data.Loadouts[i].Name = entry["name"].AsString();

                ReadStringArray(entry, "tower_bases", data.Loadouts[i].TowerBaseNames);
                ReadStringArray(entry, "func_bases", data.Loadouts[i].FuncBaseNames);
                ReadStringArray(entry, "tower_mods", data.Loadouts[i].TowerModNames);
                ReadStringArray(entry, "func_mods", data.Loadouts[i].FuncModNames);
                ReadStringArray(entry, "relics", data.Loadouts[i].RelicNames);
            }

            return data;
        }

        public static void Save(LoadoutSaveData data)
        {
            var root = new Dictionary();
            var arr = new Array();

            for (int i = 0; i < LOADOUT_COUNT; i++)
            {
                var entry = new Dictionary();
                var ld = data.Loadouts[i];

                entry["name"] = ld.Name ?? $"Loadout {i + 1}";
                entry["tower_bases"] = WriteStringArray(ld.TowerBaseNames);
                entry["func_bases"] = WriteStringArray(ld.FuncBaseNames);
                entry["tower_mods"] = WriteStringArray(ld.TowerModNames);
                entry["func_mods"] = WriteStringArray(ld.FuncModNames);
                entry["relics"] = WriteStringArray(ld.RelicNames);

                arr.Add(entry);
            }

            root["loadouts"] = arr;

            var text = Json.Stringify(root, "  ");
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError("[LoadoutSave] Failed to open save file for writing");
                return;
            }
            file.StoreString(text);
            GD.Print("[LoadoutSave] Saved to disk");
        }

        private static void ReadStringArray(Dictionary dict, string key, string[] target)
        {
            if (!dict.ContainsKey(key)) return;
            var arr = dict[key].AsGodotArray();
            for (int i = 0; i < arr.Count && i < target.Length; i++)
            {
                var val = arr[i].AsString();
                target[i] = string.IsNullOrEmpty(val) ? null : val;
            }
        }

        private static Array WriteStringArray(string[] source)
        {
            var arr = new Array();
            if (source == null) return arr;
            foreach (var s in source)
                arr.Add(s ?? "");
            return arr;
        }

        private static LoadoutSaveData CreateDefault()
        {
            var data = new LoadoutSaveData();
            data.Loadouts = new SavedLoadout[LOADOUT_COUNT];
            for (int i = 0; i < LOADOUT_COUNT; i++)
            {
                data.Loadouts[i] = new SavedLoadout
                {
                    Name = $"Loadout {i + 1}",
                    TowerBaseNames = new string[TOWER_SLOTS],
                    FuncBaseNames = new string[TOWER_SLOTS],
                    TowerModNames = new string[TOWER_SLOTS],
                    FuncModNames = new string[TOWER_SLOTS],
                    RelicNames = new string[RELIC_SLOTS]
                };
            }
            return data;
        }
    }
}
