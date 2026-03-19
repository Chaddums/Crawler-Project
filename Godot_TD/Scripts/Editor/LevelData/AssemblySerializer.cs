using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace JunkyardTD
{
    public static class AssemblySerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private const string AssembliesDir = "res://Data/Assemblies/";

        public static string Serialize(AssemblyData data)
        {
            return JsonSerializer.Serialize(data, Options);
        }

        public static AssemblyData Deserialize(string json)
        {
            return JsonSerializer.Deserialize<AssemblyData>(json, Options);
        }

        public static void SaveToFile(AssemblyData data, string filename)
        {
            string json = Serialize(data);
            string path = AssembliesDir + filename;

            string globalDir = ProjectSettings.GlobalizePath(AssembliesDir);
            if (!System.IO.Directory.Exists(globalDir))
                System.IO.Directory.CreateDirectory(globalDir);

            string globalPath = ProjectSettings.GlobalizePath(path);
            System.IO.File.WriteAllText(globalPath, json);
            GD.Print($"[AssemblySerializer] Saved assembly to {path}");
        }

        public static AssemblyData LoadFromFile(string filename)
        {
            string path = AssembliesDir + filename;
            string globalPath = ProjectSettings.GlobalizePath(path);

            if (!System.IO.File.Exists(globalPath))
            {
                GD.PrintErr($"[AssemblySerializer] File not found: {path}");
                return null;
            }

            string json = System.IO.File.ReadAllText(globalPath);
            return Deserialize(json);
        }

        public static string[] ListAssemblyFiles()
        {
            string globalDir = ProjectSettings.GlobalizePath(AssembliesDir);
            if (!System.IO.Directory.Exists(globalDir))
                return System.Array.Empty<string>();

            var files = System.IO.Directory.GetFiles(globalDir, "*.json");
            for (int i = 0; i < files.Length; i++)
                files[i] = System.IO.Path.GetFileName(files[i]);
            return files;
        }
    }
}
