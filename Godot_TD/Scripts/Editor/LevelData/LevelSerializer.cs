using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace JunkyardTD
{
    public static class LevelSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private const string LevelsDir = "res://Data/Levels/";

        public static string Serialize(LevelData data)
        {
            return JsonSerializer.Serialize(data, Options);
        }

        public static LevelData Deserialize(string json)
        {
            return JsonSerializer.Deserialize<LevelData>(json, Options);
        }

        public static void SaveToFile(LevelData data, string filename)
        {
            string json = Serialize(data);
            string path = LevelsDir + filename;

            // Ensure directory exists
            string globalDir = ProjectSettings.GlobalizePath(LevelsDir);
            if (!System.IO.Directory.Exists(globalDir))
                System.IO.Directory.CreateDirectory(globalDir);

            string globalPath = ProjectSettings.GlobalizePath(path);
            System.IO.File.WriteAllText(globalPath, json);
            GD.Print($"[LevelSerializer] Saved level to {path}");
        }

        public static LevelData LoadFromFile(string filename)
        {
            string path = LevelsDir + filename;
            string globalPath = ProjectSettings.GlobalizePath(path);

            if (!System.IO.File.Exists(globalPath))
            {
                GD.PrintErr($"[LevelSerializer] File not found: {path}");
                return null;
            }

            string json = System.IO.File.ReadAllText(globalPath);
            var data = Deserialize(json);
            GD.Print($"[LevelSerializer] Loaded level '{data?.Name}' from {path}");
            return data;
        }

        public static bool FileExists(string filename)
        {
            string globalPath = ProjectSettings.GlobalizePath(LevelsDir + filename);
            return System.IO.File.Exists(globalPath);
        }

        public static string[] ListLevelFiles()
        {
            string globalDir = ProjectSettings.GlobalizePath(LevelsDir);
            if (!System.IO.Directory.Exists(globalDir))
                return System.Array.Empty<string>();

            var files = System.IO.Directory.GetFiles(globalDir, "*.json");
            for (int i = 0; i < files.Length; i++)
                files[i] = System.IO.Path.GetFileName(files[i]);
            return files;
        }
    }
}
