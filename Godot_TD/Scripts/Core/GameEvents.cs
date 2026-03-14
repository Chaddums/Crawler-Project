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
