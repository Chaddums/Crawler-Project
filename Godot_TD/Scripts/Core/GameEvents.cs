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
        public static Action<Node, Vector3> OnEnemyLeaked;

        // Towers
        public static Action<Node> OnTowerPlaced;
        public static Action<Node> OnTowerSold;
        public static Action<Node> OnTowerUpgraded;
        public static Action<Node, ModComponentType> OnModAttached;

        // S1: Economy — Resources (universal) + Materials (accumulated for upgrades)
        public static Action<int> OnResourcesChanged;
        public static Action<Vector3, int> OnResourcesDropped;
        public static Action<int> OnResourcesCollected;

        // Waves
        public static Action<int> OnWaveStarted;
        public static Action<int> OnWaveCompleted;
        public static Action<int> OnAllWavesCleared;

        // Terrain
        public static Action<Vector2I> OnTerrainChanged;
        public static Action OnPathRecalculated;

        // Core
        public static Action<int> OnCoreLivesChanged;
        public static Action OnCoreDestroyed;

        // Commentary
        public static Action<string, string> OnCommentary;
        public static Action<string> OnAnnouncement;

        // S1: removed OnHeroBotDeployed, OnHeroBotRecalled (HeroBotController deleted)
        // S1: removed OnFabricationComplete (FabricationSystem deleted)
        // S1: removed OnFloorCompleted (floors removed — S2 will add OnWaveMilestone)

        // ── Vine Logic TD ──
        public static Action OnBossSpawned;
        public static Action<PerkData> OnPerkSelected;

        // Mining Building & Dome
        public static Action<float> OnHarvesterDamaged;
        public static Action<float, float> OnHarvesterHPChanged;
        public static Action OnDomeCollapsed;
        // S1: renamed Scrap→Resources, Magic→Materials
        public static Action<MiningMode> OnMiningModeChanged;
        public static Action<float> OnMaterialsChanged;
        public static Action<MaterialType> OnMaterialTypeSelected;
        public static Action<float, MaterialType> OnMaterialsAccumulated;

        // Player
        public static Action<float, float> OnPlayerHPChanged;
        public static Action<float, float> OnPlayerMaterialsChanged;
        public static Action<int, float> OnAbilityCooldownChanged;
        public static Action OnPlayerDied;

        public static Action<Node, SignalType> OnSignalFired;
        public static Action<Node, SignalType> OnSignalReceived;
        public static Action<Node, bool> OnGateStateChanged;
        public static Action<Node, int> OnSwitchToggled;
        public static Action<Node> OnVineNodePlaced;
        public static Action<Node> OnVineNodeSold;
        public static Action<Node> OnVineNodeDestroyed;
        public static Action OnVinePathRecalculated;

        // Buff/Debuff
        public static Action<Node, string, float> OnBuffApplied;
        public static Action<Node, string> OnBuffRemoved;
        public static Action<Node, string, float> OnDebuffApplied;
        public static Action<Node, string> OnDebuffRemoved;

        // Difficulty
        public static Action OnSurgeStarted;
        public static Action OnSurgeEnded;

        // Corruption
        public static Action<CorruptionType> OnCorruptionStarted;
        public static Action<CorruptionType> OnCorruptionEnded;

        // S2: Wave milestone event (replaces OnFloorCompleted)
        public static Action<int, string> OnWaveMilestone;  // (waveNumber, milestoneType)

        // Shield Walls
        public static Action<CardinalDirection, float, float> OnShieldWallDamaged;  // direction, currentHP, maxHP
        public static Action<CardinalDirection> OnShieldWallDestroyed;

        // S5: Tower slot system
        public static Action<Node, TowerComponentType, TowerSlotType> OnComponentSlotted;  // tower, component, slot
        public static Action<Node, TowerComponentType> OnComponentRemoved;                  // tower, component
        public static Action<Node, Node, string> OnSynergyActivated;                        // tower1, tower2, synergyName

        // S4: Territory
        public static Action<string> OnTerritoryUnlocked;        // sectionId
        public static Action<string> OnBossSectionCleared;       // sectionId

        // S4: Suits
        public static Action<int> OnSuitSaved;                   // slot index
        public static Action<int> OnSuitDestroyed;               // slot index
        public static Action OnSuitEquipped;

        // S4: Boss Run
        public static Action OnBossDefeated;
        public static Action OnBossRunComplete;

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
            OnResourcesChanged = null;
            OnResourcesDropped = null;
            OnResourcesCollected = null;
            OnWaveStarted = null;
            OnWaveCompleted = null;
            OnAllWavesCleared = null;
            OnTerrainChanged = null;
            OnPathRecalculated = null;
            OnCoreLivesChanged = null;
            OnCoreDestroyed = null;
            OnCommentary = null;
            OnAnnouncement = null;
            OnSignalFired = null;
            OnSignalReceived = null;
            OnGateStateChanged = null;
            OnSwitchToggled = null;
            OnBossSpawned = null;
            OnPerkSelected = null;
            OnVineNodePlaced = null;
            OnVineNodeSold = null;
            OnVineNodeDestroyed = null;
            OnVinePathRecalculated = null;
            OnHarvesterDamaged = null;
            OnHarvesterHPChanged = null;
            OnDomeCollapsed = null;
            OnMiningModeChanged = null;
            OnMaterialsChanged = null;
            OnMaterialTypeSelected = null;
            OnMaterialsAccumulated = null;
            OnPlayerHPChanged = null;
            OnPlayerMaterialsChanged = null;
            OnAbilityCooldownChanged = null;
            OnPlayerDied = null;
            OnBuffApplied = null;
            OnBuffRemoved = null;
            OnDebuffApplied = null;
            OnDebuffRemoved = null;
            OnSurgeStarted = null;
            OnSurgeEnded = null;
            OnCorruptionStarted = null;
            OnCorruptionEnded = null;
            OnWaveMilestone = null;
            OnShieldWallDamaged = null;
            OnShieldWallDestroyed = null;
            OnComponentSlotted = null;
            OnComponentRemoved = null;
            OnSynergyActivated = null;
            OnTerritoryUnlocked = null;
            OnBossSectionCleared = null;
            OnSuitSaved = null;
            OnSuitDestroyed = null;
            OnSuitEquipped = null;
            OnBossDefeated = null;
            OnBossRunComplete = null;
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
