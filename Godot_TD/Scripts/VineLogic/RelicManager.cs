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
        // ── Inventory (persisted between runs) ──
        private List<string> _ownedRelicIds = new();

        // ── Equipped this run ──
        private List<string> _equippedRelicIds = new();
        public IReadOnlyList<string> EquippedRelics => _equippedRelicIds;

        public const int MAX_EQUIPPED = 3;       // Max relics equipped per run
        public const int MAX_BOSS_CARRY = 2;      // Max relics brought into a boss run
        public const float DROP_CHANCE_WAVE = 0.15f;   // 15% per wave clear
        public const float DROP_CHANCE_COMMANDER = 0.5f; // 50% from commander kills

        private RandomNumberGenerator _rng = new();

        // Active effects cache (rebuilt when equip changes)
        private HashSet<string> _activeEffects = new();

        // ── Save path ──
        private const string SavePath = "user://relic_inventory.json";

        public override void _Ready()
        {
            LoadInventory();

            // Hook drop events
            GameEvents.OnWaveCompleted += OnWaveComplete;
            GameEvents.OnEnemyKilled += OnEnemyKilled;

            ServiceLocator.Register(this);
            GD.Print($"[RelicManager] Loaded {_ownedRelicIds.Count} owned relics, {_equippedRelicIds.Count} equipped");
        }

        // ── Drop Logic ──

        private void OnWaveComplete(int wave)
        {
            // Relics start dropping after wave 5
            if (wave < 5) return;

            // Higher waves = slightly higher chance
            float chance = DROP_CHANCE_WAVE + (wave - 5) * 0.005f;
            if (_rng.Randf() > chance) return;

            DropRandomRelic("wave_clear");
        }

        private void OnEnemyKilled(Node enemy)
        {
            // Commanders have higher drop rate
            if (enemy is VineEnemy ve && ve.IsCommander)
            {
                if (_rng.Randf() < DROP_CHANCE_COMMANDER)
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
                if (_ownedRelicIds.Contains(relic.Id))
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

        private void AcquireRelic(string relicId, string source)
        {
            bool isNew = !_ownedRelicIds.Contains(relicId);
            if (isNew)
                _ownedRelicIds.Add(relicId);

            SaveInventory();

            var relic = GetRelicById(relicId);
            string name = relic?.Name ?? relicId;

            GD.Print($"[RelicManager] Relic acquired: {name} (source: {source}, new: {isNew})");

            // Fire event for UI notification
            GameEvents.OnRelicAcquired?.Invoke(relicId, isNew);

            // BIT comment on relic acquisition
            if (ServiceLocator.TryGet<BITCommentary>(out var bit) && isNew)
                bit.Say($"new relic. {name.ToLower()}. catalogued.");
        }

        // ── Equip / Unequip ──

        public bool CanEquip(string relicId)
        {
            if (_equippedRelicIds.Count >= MAX_EQUIPPED) return false;
            if (_equippedRelicIds.Contains(relicId)) return false;
            if (!_ownedRelicIds.Contains(relicId)) return false;
            return true;
        }

        public bool Equip(string relicId)
        {
            if (!CanEquip(relicId)) return false;

            _equippedRelicIds.Add(relicId);
            RebuildActiveEffects();

            var relic = GetRelicById(relicId);
            GD.Print($"[RelicManager] Equipped: {relic?.Name ?? relicId}");

            return true;
        }

        public bool Unequip(string relicId)
        {
            if (!_equippedRelicIds.Remove(relicId)) return false;

            RebuildActiveEffects();
            var relic = GetRelicById(relicId);
            GD.Print($"[RelicManager] Unequipped: {relic?.Name ?? relicId}");

            return true;
        }

        // ── Effect Queries (called by combat systems) ──

        public bool HasEffect(string relicId) => _activeEffects.Contains(relicId);

        /// <summary>Negates first hit each wave.</summary>
        public bool HasNullShard => HasEffect("null-shard");

        /// <summary>+15% signal travel speed.</summary>
        public bool HasHexCapacitor => HasEffect("hex-capacitor");

        /// <summary>Passive 2 HP/sec regen to all nodes.</summary>
        public bool HasAetherCoil => HasEffect("aether-coil");

        /// <summary>Crits deal 3x instead of 2x. Base damage -10%.</summary>
        public bool HasEntropicLens => HasEffect("entropic-lens");

        /// <summary>Routing nodes gain +1 signal power.</summary>
        public bool HasRunicTransistor => HasEffect("runic-transistor");

        /// <summary>Reveals cloaked enemies in range 12.</summary>
        public bool HasVoidBeacon => HasEffect("void-beacon");

        /// <summary>Slow fields also reduce armor by 2.</summary>
        public bool HasFluxMandala => HasEffect("flux-mandala");

        /// <summary>10% chance to duplicate placed nodes. Node HP -5%.</summary>
        public bool HasQuantumSplicer => HasEffect("quantum-splicer");

        /// <summary>Towers fire once at phantom targets. Wastes ammo.</summary>
        public bool HasPhantomRegister => HasEffect("phantom-register");

        /// <summary>Get stat modifiers from all equipped relics combined.</summary>
        public RelicStatMods GetStatMods()
        {
            var mods = new RelicStatMods();
            foreach (var id in _equippedRelicIds)
            {
                switch (id)
                {
                    case "hex-capacitor":
                        mods.SignalSpeedMult += 0.15f;
                        break;
                    case "entropic-lens":
                        mods.CritMultiplier = 3f;
                        mods.BaseDamageMult -= 0.10f;
                        break;
                    case "runic-transistor":
                        mods.BonusSignalPower += 1;
                        break;
                    case "flux-mandala":
                        mods.SlowFieldArmorReduction += 2f;
                        break;
                    case "quantum-splicer":
                        mods.NodeDuplicateChance = 0.10f;
                        mods.NodeHPMult -= 0.05f;
                        break;
                    case "phantom-register":
                        mods.PhantomFireChance = 0.1f;  // 10% wasted shots
                        break;
                }
            }
            return mods;
        }

        // ── Inventory Queries ──

        public List<string> GetOwnedRelicIds() => new(_ownedRelicIds);

        public bool OwnsRelic(string relicId) => _ownedRelicIds.Contains(relicId);

        public int OwnedCount => _ownedRelicIds.Count;

        public bool IsEquipped(string relicId) => _equippedRelicIds.Contains(relicId);
        public int EquippedCount => _equippedRelicIds.Count;
        public int MaxEquipSlots => MAX_EQUIPPED;

        public static RelicRegistry.Relic? GetRelicById(string id)
        {
            foreach (var r in RelicRegistry.All)
                if (r.Id == id) return r;
            return null;
        }

        // ── Persistence ──

        private void SaveInventory()
        {
            var dict = new Godot.Collections.Dictionary();
            var arr = new Godot.Collections.Array();
            foreach (var id in _ownedRelicIds)
                arr.Add(id);
            dict["owned"] = arr;

            var text = Json.Stringify(dict, "  ");
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file != null)
                file.StoreString(text);
        }

        private void LoadInventory()
        {
            _ownedRelicIds.Clear();

            if (!FileAccess.FileExists(SavePath)) return;

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null) return;

            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok) return;

            if (json.Data.Obj is not Godot.Collections.Dictionary dict) return;
            if (!dict.ContainsKey("owned")) return;

            var arr = dict["owned"].AsGodotArray();
            foreach (var id in arr)
                _ownedRelicIds.Add(id.AsString());
        }

        private void RebuildActiveEffects()
        {
            _activeEffects.Clear();
            foreach (var id in _equippedRelicIds)
                _activeEffects.Add(id);
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveCompleted -= OnWaveComplete;
            ServiceLocator.Unregister<RelicManager>();
        }
    }

    /// <summary>
    /// Stat modifiers from all equipped relics combined.
    /// Combat systems read these to apply relic effects.
    /// </summary>
    public struct RelicStatMods
    {
        public float SignalSpeedMult;      // +% signal travel speed
        public float CritMultiplier;       // Crit damage multiplier (default 2x, entropic lens = 3x)
        public float BaseDamageMult;       // +/- % base damage
        public int BonusSignalPower;       // Extra power budget for routing nodes
        public float SlowFieldArmorReduction; // Armor reduction from slow fields
        public float NodeDuplicateChance;  // % chance to duplicate placed nodes
        public float NodeHPMult;           // +/- % node max HP
        public float PhantomFireChance;    // % chance towers waste a shot on phantom target

        public RelicStatMods()
        {
            SignalSpeedMult = 0;
            CritMultiplier = 2f;  // Default crit multiplier
            BaseDamageMult = 0;
            BonusSignalPower = 0;
            SlowFieldArmorReduction = 0;
            NodeDuplicateChance = 0;
            NodeHPMult = 0;
            PhantomFireChance = 0;
        }
    }
}
