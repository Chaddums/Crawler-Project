using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    public static class GameEvents
    {
        // Game State
        public static Action<GamePhase> OnPhaseChanged;

        // Combat
        public static Action<DamageInfo> OnDamageDealt;
        public static Action<Node> OnEnemyKilled;
        public static Action<Node, Vector3> OnEnemyLeaked;  // Enemy reached the core

        // Towers
        public static Action<Node> OnTowerPlaced;
        public static Action<Node> OnTowerSold;
        public static Action<Node> OnTowerUpgraded;
        public static Action<Node, ModComponentType> OnModAttached;

        // Economy
        public static Action<int> OnScrapChanged;         // Total scrap
        public static Action<Vector3, int> OnScrapDropped; // World position + amount
        public static Action<int> OnScrapCollected;        // Amount collected

        // Waves
        public static Action<int> OnWaveStarted;
        public static Action<int> OnWaveCompleted;
        public static Action<int> OnAllWavesCleared;

        // Terrain
        public static Action<Vector2I> OnTerrainChanged;   // Grid position
        public static Action OnPathRecalculated;

        // Core
        public static Action<int> OnCoreLivesChanged;
        public static Action OnCoreDestroyed;

        // Commentary (shared universe — AXIS talks here too)
        public static Action<string, string> OnCommentary;  // Speaker, text
        public static Action<string> OnAnnouncement;

        // Hero Bot
        public static Action<Node> OnHeroBotDeployed;
        public static Action<Node> OnHeroBotRecalled;

        // Fabrication
        public static Action<Node, ModComponentType> OnFabricationComplete;

        // ── Vine Logic TD ──
        public static Action<int> OnFloorCompleted;                   // Floor number
        public static Action OnBossSpawned;
        public static Action<PerkData> OnPerkSelected;

        // Harvester & Dome
        public static Action<float> OnHarvesterDamaged;              // currentHP
        public static Action<float, float> OnHarvesterHPChanged;     // currentHP, maxHP
        public static Action OnDomeCollapsed;                         // Dome radius hit 0 — last stand

        // Player
        public static Action<float, float> OnPlayerHPChanged;        // current, max
        public static Action<float, float> OnPlayerManaChanged;      // current, max
        public static Action<int, float> OnAbilityCooldownChanged;   // slot, remaining
        public static Action OnPlayerDied;

        public static Action<Node, SignalType> OnSignalFired;         // Node that fired, signal type
        public static Action<Node, SignalType> OnSignalReceived;      // Node that received, signal type
        public static Action<Node, bool> OnGateStateChanged;          // Gate node, is open
        public static Action<Node, int> OnSwitchToggled;              // Switch node, active output index
        public static Action<Node> OnVineNodePlaced;
        public static Action<Node> OnVineNodeSold;
        public static Action<Node> OnVineNodeDestroyed;                    // Tower destroyed by enemy fire
        public static Action OnVinePathRecalculated;

        public static int Version { get; private set; }

        public static void ClearAll()
        {
            Version++;
            OnPhaseChanged = null;
            OnDamageDealt = null;
            OnEnemyKilled = null;
            OnEnemyLeaked = null;
            OnTowerPlaced = null;
            OnTowerSold = null;
            OnTowerUpgraded = null;
            OnModAttached = null;
            OnScrapChanged = null;
            OnScrapDropped = null;
            OnScrapCollected = null;
            OnWaveStarted = null;
            OnWaveCompleted = null;
            OnAllWavesCleared = null;
            OnTerrainChanged = null;
            OnPathRecalculated = null;
            OnCoreLivesChanged = null;
            OnCoreDestroyed = null;
            OnCommentary = null;
            OnAnnouncement = null;
            OnHeroBotDeployed = null;
            OnHeroBotRecalled = null;
            OnFabricationComplete = null;
            OnSignalFired = null;
            OnSignalReceived = null;
            OnGateStateChanged = null;
            OnSwitchToggled = null;
            OnFloorCompleted = null;
            OnBossSpawned = null;
            OnPerkSelected = null;
            OnVineNodePlaced = null;
            OnVineNodeSold = null;
            OnVineNodeDestroyed = null;
            OnVinePathRecalculated = null;
            OnHarvesterDamaged = null;
            OnHarvesterHPChanged = null;
            OnDomeCollapsed = null;
            OnPlayerHPChanged = null;
            OnPlayerManaChanged = null;
            OnAbilityCooldownChanged = null;
            OnPlayerDied = null;
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
        public float KnockbackForce;
        public float SlowAmount;
        public float SlowDuration;
        public float BurnDamage;
        public float BurnDuration;
    }
}
