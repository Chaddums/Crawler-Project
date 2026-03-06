using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Static event bus connecting all game systems without direct dependencies.
    /// Uses base types (Node, Resource, object) to avoid circular references.
    /// </summary>
    public static class GameEvents
    {
        // Game State
        public static Action<GameState> OnGameStateChanged;

        // Combat
        public static Action<DamageInfo> OnDamageDealt;
        public static Action<Node> OnEnemyKilled;
        public static Action<Node> OnPlayerDeath;

        // Loot — Resource is ItemData at runtime
        public static Action<Resource> OnItemPickedUp;
        public static Action<Resource> OnItemEquipped;
        public static Action<Resource> OnItemUnequipped;
        public static Action<LootBoxOpenedData> OnLootBoxOpened;

        // Progression — Resource is AbilityData / BotFrameData at runtime
        public static Action<int> OnPlayerLevelUp;
        public static Action<Resource> OnAbilityUnlocked;
        public static Action<Resource> OnClassSelected;
        public static Action<int> OnExperienceGained;

        // Dungeon — Node carries the RoomController script
        public static Action<int> OnSectorEntered;
        public static Action<Node> OnRoomEntered;
        public static Action<Node> OnRoomCleared;

        // Commentary
        public static Action<CommentaryEntry> OnCommentaryTriggered;
        public static Action<string> OnAIAnnouncementReceived;

        // Companion — Node carries the CompanionController script
        public static Action<Node> OnCompanionSummoned;
        public static Action<Resource> OnCompanionAbilityUsed;

        // Passive Tree
        public static Action<string> OnPassiveNodeAllocated;
        public static Action<string> OnPassiveNodeDeallocated;
        public static Action OnPassiveTreeReset;

        // System Messages
        public static Action<string, string> OnSystemMessage;
        public static Action<string> OnAchievementUnlocked;

        // Boss
        public static Action<Node> OnBossSpawned;
        public static Action<Node> OnBossDefeated;

        // Items
        public static Action<object> OnItemUsed;

        // Combo
        public static Action<int> OnComboHit;

        // Lift Timer
        public static Action<float> OnTimerWarning;
        public static Action OnTimerExpired;

        // Fog of War
        public static Action<HashSet<Vector2I>> OnFogUpdated;

        // UI
        public static Action OnInventoryToggled;
        public static Action OnCharacterSheetToggled;
        public static Action OnPauseToggled;

        /// <summary>
        /// Clear all static event subscriptions. Must be called on scene transitions
        /// (ReturnToMainMenu, StartNewGame) to prevent leaked lambdas from
        /// DungeonGenerator and other non-Node subscribers.
        /// </summary>
        public static void ClearAll()
        {
            OnGameStateChanged = null;
            OnDamageDealt = null;
            OnEnemyKilled = null;
            OnPlayerDeath = null;
            OnItemPickedUp = null;
            OnItemEquipped = null;
            OnItemUnequipped = null;
            OnLootBoxOpened = null;
            OnPlayerLevelUp = null;
            OnAbilityUnlocked = null;
            OnClassSelected = null;
            OnExperienceGained = null;
            OnSectorEntered = null;
            OnRoomEntered = null;
            OnRoomCleared = null;
            OnCommentaryTriggered = null;
            OnAIAnnouncementReceived = null;
            OnCompanionSummoned = null;
            OnCompanionAbilityUsed = null;
            OnPassiveNodeAllocated = null;
            OnPassiveNodeDeallocated = null;
            OnPassiveTreeReset = null;
            OnSystemMessage = null;
            OnAchievementUnlocked = null;
            OnBossSpawned = null;
            OnBossDefeated = null;
            OnItemUsed = null;
            OnComboHit = null;
            OnTimerWarning = null;
            OnTimerExpired = null;
            OnFogUpdated = null;
            OnInventoryToggled = null;
            OnCharacterSheetToggled = null;
            OnPauseToggled = null;
        }
    }

    public struct DamageInfo
    {
        public float RawDamage;
        public float FinalDamage;
        public bool IsCritical;
        public DamageType DamageType;
        public Node Attacker;
        public Node Target;
        public Vector3 HitPoint;
        public List<Resource> StatusEffects;
        public float KnockbackForce;
        public float StunDuration;
    }

    public struct LootBoxOpenedData
    {
        public LootBoxTier Tier;
        public List<object> Items;
    }

    /// <summary>
    /// Commentary entry used by the event bus and commentary system.
    /// </summary>
    public class CommentaryEntry
    {
        public string UniqueId;
        public string Speaker;
        public string Text;
        public CommentaryPriority Priority;
        public CommentaryCategory Category;
        public AudioStream VoiceClip;
        public float DisplayDuration;

        public float GetDisplayDuration()
        {
            if (DisplayDuration > 0) return DisplayDuration;
            return string.IsNullOrEmpty(Text) ? 2f : 2f + Text.Length * 0.05f;
        }
    }
}
