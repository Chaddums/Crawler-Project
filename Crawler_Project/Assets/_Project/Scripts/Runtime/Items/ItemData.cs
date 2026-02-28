using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "DCC/Items/Generic Item")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string ItemId;
        public string ItemName;

        [TextArea(2, 4)]
        public string Description;

        [TextArea(2, 4)]
        public string FlavorText;

        [Header("Visuals")]
        public Sprite Icon;

        [Header("Properties")]
        public ItemRarity Rarity = ItemRarity.Common;
        public ItemType Type = ItemType.Miscellaneous;
        public int MaxStack = 1;
        public int SellValue;

        [Header("World")]
        public GameObject WorldPrefab;
    }
}
