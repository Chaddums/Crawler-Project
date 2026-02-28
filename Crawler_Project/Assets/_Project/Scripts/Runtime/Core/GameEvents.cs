using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Static event bus connecting all game systems without direct assembly references.
    /// Uses base types (ScriptableObject, GameObject, object) to avoid circular dependencies
    /// between assembly definitions. Subscribers cast to concrete types as needed.
    /// </summary>
    public static class GameEvents
    {
        // Game State
        public static Action<GameState> OnGameStateChanged;

        // Combat
        public static Action<DamageInfo> OnDamageDealt;
        public static Action<GameObject> OnEnemyKilled;
        public static Action<GameObject> OnPlayerDeath;

        // Loot — ScriptableObject is ItemData at runtime
        public static Action<ScriptableObject> OnItemPickedUp;
        public static Action<ScriptableObject> OnItemEquipped;
        public static Action<ScriptableObject> OnItemUnequipped;
        public static Action<LootBoxOpenedData> OnLootBoxOpened;

        // Progression — ScriptableObject is AbilityData / CrawlerClassData at runtime
        public static Action<int> OnPlayerLevelUp;
        public static Action<ScriptableObject> OnAbilityUnlocked;
        public static Action<ScriptableObject> OnClassSelected;
        public static Action<int> OnExperienceGained;

        // Dungeon — GameObject carries the RoomController component
        public static Action<int> OnFloorEntered;
        public static Action<GameObject> OnRoomEntered;
        public static Action<GameObject> OnRoomCleared;

        // Commentary
        public static Action<CommentaryEntry> OnCommentaryTriggered;
        public static Action<string> OnAIAnnouncementReceived;

        // Companion — GameObject carries the CompanionController component
        public static Action<GameObject> OnCompanionSummoned;
        public static Action<ScriptableObject> OnCompanionAbilityUsed;

        // UI
        public static Action OnInventoryToggled;
        public static Action OnCharacterSheetToggled;
        public static Action OnPauseToggled;
    }

    [System.Serializable]
    public struct DamageInfo
    {
        public float RawDamage;
        public float FinalDamage;
        public bool IsCritical;
        public DamageType DamageType;
        public GameObject Attacker;
        public GameObject Target;
        public Vector3 HitPoint;
        public List<ScriptableObject> StatusEffects;
        public float KnockbackForce;
        public float StunDuration;
    }

    [System.Serializable]
    public struct LootBoxOpenedData
    {
        public LootBoxTier Tier;
        public List<object> Items;
    }

    /// <summary>
    /// Commentary entry used by the event bus and commentary system.
    /// Lives in Core to avoid circular assembly dependencies.
    /// </summary>
    [System.Serializable]
    public class CommentaryEntry
    {
        public string UniqueId;
        public string Speaker;
        [TextArea] public string Text;
        public CommentaryPriority Priority;
        public CommentaryCategory Category;
        public AudioClip VoiceClip;
        public float DisplayDuration;

        public float GetDisplayDuration()
        {
            if (DisplayDuration > 0) return DisplayDuration;
            // Auto-calculate from text length: ~50ms per character + base time
            return string.IsNullOrEmpty(Text) ? 2f : 2f + Text.Length * 0.05f;
        }
    }
}
