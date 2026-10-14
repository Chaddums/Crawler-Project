using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages relic drops during runs, persistent inventory, equipping to BIT,
    /// and applying relic gameplay effects.
    ///
    /// Relics drop from wave completions and commander kills. They go into a
    /// persistent inventory. A limited number can be equipped on BIT per run.
    /// Some relics have negative tradeoffs that enable powerful synergy builds.
    /// Visual-only relics are skill flex — choosing looks over power.
    /// </summary>
    public partial class RelicManager : Node
    {
        // Inventory (owned + equipped) lives in RelicInventory so the meta screens can use it
        // without a battle loaded. This node handles drops and effect queries during a run.
        public IReadOnlyList<string> EquippedRelics => RelicInventory.Equipped;

        public const int MAX_EQUIPPED = 3;       // Max relics equipped per run
        public const int MAX_BOSS_CARRY = 2;      // Max relics brought into a boss run
        public const float DROP_CHANCE_WAVE = 0.15f;   // 15% per wave clear
        public const float DROP_CHANCE_COMMANDER = 0.5f; // 50% from commander kills

        private RandomNumberGenerator _rng = new();

        // Active effects cache (rebuilt when equip changes)
        private HashSet<string> _activeEffects = new();

        public override void _Ready()
        {
            RebuildActiveEffects();

            // Hook drop events
            GameEvents.OnWaveCleared += OnWaveComplete;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnRelicEquipChanged += RebuildActiveEffects;

            ServiceLocator.Register(this);
            GD.Print($"[RelicManager] {RelicInventory.OwnedCount} owned relics, {RelicInventory.EquippedCount} equipped");
        }

        // ── Drop Logic ──

        /// <summary>A relic from a loot crate.</summary>
        public void GrantRelic(string relicId, string source, bool announce = true) => AcquireRelic(relicId, source, announce);

        private void OnWaveComplete(int wave)
        {
            // Relics come in loot crates now (Data/loot_boxes.json); the old direct drops are the fallback
            if (LootTable.Loaded) return;
            // Relics start dropping after wave 5
            if (wave < 5) return;

            // Higher waves = slightly higher chance; territory conquest buff ("relic_drop_mult")
            float chance = (DROP_CHANCE_WAVE + (wave - 5) * 0.005f) * (GameManager.Instance?.RunRelicDropMult ?? 1f);
            if (_rng.Randf() > chance) return;

            DropRandomRelic("wave_clear");
        }

        private void OnEnemyKilled(Node enemy)
        {
            // Commanders have higher drop rate
            if (!LootTable.Loaded && enemy is VineEnemy ve && ve.IsCommander)
            {
                if (_rng.Randf() < DROP_CHANCE_COMMANDER * (GameManager.Instance?.RunRelicDropMult ?? 1f))
                    DropRandomRelic("commander_kill");
            }
        }

        private void DropRandomRelic(string source)
        {
            // Weight by rarity — commons drop more often
            var candidates = new List<(RelicRegistry.Relic relic, float weight)>();
            foreach (var relic in RelicRegistry.All)
            {
                float weight = relic.Rarity switch
                {
                    "common" => 4f,
                    "uncommon" => 3f,
                    "rare" => 1.5f,
                    "legendary" => 0.5f,
                    _ => 2f
                };

                // Already owned = much lower chance (but not zero — duplicates become currency later)
                if (RelicInventory.FoundThisRun(relic.Id))
                    weight *= 0.2f;

                candidates.Add((relic, weight));
            }

            // Weighted random selection
            float totalWeight = candidates.Sum(c => c.weight);
            float roll = _rng.Randf() * totalWeight;
            float cumulative = 0;

            foreach (var (relic, weight) in candidates)
            {
                cumulative += weight;
                if (roll <= cumulative)
                {
                    AcquireRelic(relic.Id, source);
                    return;
                }
            }
        }

        private void AcquireRelic(string relicId, string source, bool announce = true)
        {
            bool isNew = RelicInventory.Acquire(relicId);

            var relic = GetRelicById(relicId);
            string name = relic?.Name ?? relicId;

            GD.Print($"[RelicManager] Relic acquired: {name} (source: {source}, new: {isNew})");

            // Fire event for UI notification
            if (announce) GameEvents.OnRelicAcquired?.Invoke(relicId, isNew);

            // BIT comment on relic acquisition
            if (ServiceLocator.TryGet<BITCommentary>(out var bit) && isNew)
                bit.Say($"new relic. {name.ToLower()}. catalogued.");
        }

        // ── Equip / Unequip ──

        public bool CanEquip(string relicId) => RelicInventory.CanEquip(relicId);

        public bool Equip(string relicId)
        {
            if (!RelicInventory.Equip(relicId)) return false;
            RebuildActiveEffects();
            GD.Print($"[RelicManager] Equipped: {GetRelicById(relicId)?.Name ?? relicId}");
            return true;
        }

        public bool Unequip(string relicId)
        {
            if (!RelicInventory.Unequip(relicId)) return false;
            RebuildActiveEffects();
            GD.Print($"[RelicManager] Unequipped: {GetRelicById(relicId)?.Name ?? relicId}");
            return true;
        }

        // ── Effect queries ──

        /// <summary>Whether a relic is equipped this battle (its slot holds it).</summary>
        public bool HasEffect(string relicId) => _activeEffects.Contains(relicId);

        // ── Inventory Queries ──

        public List<string> GetOwnedRelicIds() => new(RelicInventory.Owned);

        public bool OwnsRelic(string relicId) => RelicInventory.OwnsRelic(relicId);

        public int OwnedCount => RelicInventory.OwnedCount;

        public bool IsEquipped(string relicId) => RelicInventory.IsEquipped(relicId);
        public int EquippedCount => RelicInventory.EquippedCount;
        public int MaxEquipSlots => MAX_EQUIPPED;

        /// <summary>Force-add a relic to owned list (testing only — not saved).</summary>
        public void ForceOwn(string relicId) => RelicInventory.ForceOwn(relicId);

        public static RelicRegistry.Relic? GetRelicById(string id)
        {
            foreach (var r in RelicRegistry.All)
                if (r.Id == id) return r;
            return null;
        }

        private void RebuildActiveEffects()
        {
            _activeEffects.Clear();
            foreach (var id in RelicInventory.Equipped)
                _activeEffects.Add(id);
            // The battle plays with what's in the slots now (BIT re-arms, towers and the Spire re-dress)
            RelicRun.Begin();
        }

        /// <summary>
        /// Equip a relic now, mid-run (from a loot crate's reveal): it takes its kind's slot and the
        /// battle changes at once. False if it isn't owned or is already on.
        /// </summary>
        public bool EquipNow(string relicId)
        {
            if (!RelicInventory.Equip(relicId)) return RelicInventory.IsEquipped(relicId);
            var r = RelicRegistry.Get(relicId);
            GD.Print($"[RelicManager] Equipped mid-run: {r?.Name ?? relicId} ({r?.Kind})");
            GameEvents.OnAnnouncement?.Invoke($"{r?.Name ?? relicId} equipped");
            return true;
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveCleared -= OnWaveComplete;
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnRelicEquipChanged -= RebuildActiveEffects;
            ServiceLocator.Unregister<RelicManager>();
        }
    }
}
