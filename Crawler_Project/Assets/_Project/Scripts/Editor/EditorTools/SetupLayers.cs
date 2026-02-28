using UnityEditor;
using UnityEngine;

namespace DungeonCrawlerCarl.Editor
{
    [InitializeOnLoad]
    public static class SetupLayers
    {
        static SetupLayers()
        {
            SetupProjectLayers();
        }

        [MenuItem("DCC/Setup Layers and Tags")]
        public static void SetupProjectLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Setup layers
            SerializedProperty layers = tagManager.FindProperty("layers");

            SetLayer(layers, 6, "Player");
            SetLayer(layers, 7, "Companion");
            SetLayer(layers, 8, "Enemy");
            SetLayer(layers, 9, "PlayerProjectile");
            SetLayer(layers, 10, "EnemyProjectile");
            SetLayer(layers, 11, "Interactable");
            SetLayer(layers, 12, "Ground");
            SetLayer(layers, 13, "GameUI");

            // Setup tags
            AddTag(tagManager, "Companion");
            AddTag(tagManager, "Enemy");
            AddTag(tagManager, "Interactable");

            tagManager.ApplyModifiedProperties();

            Debug.Log("[DCC] Layers and tags configured successfully!");
            Debug.Log("[DCC]   Layer 6:  Player");
            Debug.Log("[DCC]   Layer 7:  Companion");
            Debug.Log("[DCC]   Layer 8:  Enemy");
            Debug.Log("[DCC]   Layer 9:  PlayerProjectile");
            Debug.Log("[DCC]   Layer 10: EnemyProjectile");
            Debug.Log("[DCC]   Layer 11: Interactable");
            Debug.Log("[DCC]   Layer 12: Ground");
            Debug.Log("[DCC]   Layer 13: GameUI");
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layerProp = layers.GetArrayElementAtIndex(index);
            if (layerProp.stringValue != name)
            {
                layerProp.stringValue = name;
                Debug.Log($"[DCC] Set layer {index} to '{name}'");
            }
        }

        private static void AddTag(SerializedObject tagManager, string tag)
        {
            SerializedProperty tags = tagManager.FindProperty("tags");

            // Check if tag already exists
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        }
    }
}
