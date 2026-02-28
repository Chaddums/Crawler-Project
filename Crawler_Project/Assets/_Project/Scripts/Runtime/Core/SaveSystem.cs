using System.IO;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class SaveSystem
    {
        public void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            string path = GetSavePath();
            File.WriteAllText(path, json);
            Debug.Log($"[SaveSystem] Game saved to {path}");
        }

        public SaveData Load()
        {
            string path = GetSavePath();
            if (!File.Exists(path))
            {
                Debug.LogWarning("[SaveSystem] No save file found.");
                return null;
            }

            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("[SaveSystem] Game loaded.");
            return data;
        }

        public bool HasSave()
        {
            return File.Exists(GetSavePath());
        }

        public void DeleteSave()
        {
            string path = GetSavePath();
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log("[SaveSystem] Save file deleted.");
            }
        }

        private string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, Constants.SAVE_FILE);
        }
    }
}
