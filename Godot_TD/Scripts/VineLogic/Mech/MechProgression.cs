using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A player mech's in-run XP and level. Kills (by anyone), the mech's own kills and cleared
    /// waves earn XP; each level adds health, damage and size, and each perk picked bolts on its
    /// gear. Everything resets with the run: nothing here is saved.
    /// </summary>
    public partial class MechProgression : Node
    {
        private VinePlayer _player;
        private MechAppearance _look;
        private MechSheet _sheet;
        private float _startDamage;

        public int Level { get; private set; } = 1;
        /// <summary>XP gathered toward the next level.</summary>
        public float Xp { get; private set; }
        public float TotalXp { get; private set; }
        public float XpToNext => _sheet?.Levels.XpToNext(Level) ?? 0f;
        public bool IsMaxLevel => _sheet == null || Level >= _sheet.Levels.Max;

        public void Init(VinePlayer player, MechAppearance look, MechSheet sheet)
        {
            _player = player;
            _look = look;
            _sheet = sheet;
            Name = "MechProgression";
        }

        public override void _Ready()
        {
            _startDamage = _player?.AttackDamage ?? 0f;
            GameEvents.OnEnemyKilled += OnEnemyKilled;
            GameEvents.OnWaveCompleted += OnWaveCompleted;
            GameEvents.OnPerkSelected += OnPerkSelected;
            // Perks picked before the mech existed (never in a normal run, but cheap to honour)
            var perks = GameManager.Instance?.ActivePerks;
            if (perks != null)
                foreach (var p in perks) _look?.AddPerkGear(p.Id, animate: false);
            Emit();
        }

        public override void _ExitTree()
        {
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnWaveCompleted -= OnWaveCompleted;
            GameEvents.OnPerkSelected -= OnPerkSelected;
        }

        /// <summary>Add XP, levelling up as many times as it covers. Nothing past the top level.</summary>
        public void AddXp(float amount)
        {
            if (_sheet == null || amount <= 0f || IsMaxLevel) return;
            Xp += amount;
            TotalXp += amount;
            while (!IsMaxLevel && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                LevelUp();
            }
            if (IsMaxLevel) Xp = 0f;
            Emit();
        }

        /// <summary>The mech landed a killing blow itself.</summary>
        public void CreditPersonalKill() => AddXp(_sheet?.Xp.PersonalKill ?? 0f);

        private void LevelUp()
        {
            Level++;
            var lv = _sheet.Levels;
            if (_player != null)
            {
                _player.MaxHP += lv.MaxHPPerLevel;
                _player.Heal(lv.MaxHPPerLevel);
                _player.AttackDamage += _startDamage * lv.AttackDamagePerLevel;
            }
            _look?.SetLevel(Level, animate: true);
            var gm = GameManager.Instance;
            GD.Print($"[Mech] {_sheet.Id} level {Level} at P{gm?.CurrentPlanet ?? 0}-W{gm?.CurrentWave ?? 0} ({TotalXp:F0} XP)");
            GameEvents.OnMechLevelUp?.Invoke(Level);
            GameEvents.OnAnnouncement?.Invoke($"{(_sheet.Name.Length > 0 ? _sheet.Name : "Mech")} reached level {Level}");
            if (_player != null && _player.IsInsideTree())
            {
                var at = _player.GlobalPosition + Vector3.Up * 0.8f;
                VfxFactory.SpawnEnergyBurst(_player.GetTree(), at, BitPalette.Accent, 10);
                VfxFactory.SpawnAreaPulse(_player.GetTree(), _player.GlobalPosition + Vector3.Up * 0.1f, 2.5f, BitPalette.Accent);
            }
        }

        private void OnEnemyKilled(Node node)
        {
            // Stuck enemies are despawned through the same event while still alive: no XP
            if (node is not VineEnemy e || e.IsAlive) return;
            var x = _sheet.Xp;
            AddXp(e.IsBoss ? x.BossKill : e.IsCommander ? x.CommanderKill : x.Kill);
        }

        private void OnWaveCompleted(int wave) => AddXp(_sheet.Xp.WaveCleared);

        private void OnPerkSelected(PerkData perk)
        {
            if (perk != null) _look?.AddPerkGear(perk.Id, animate: true);
        }

        private void Emit() => GameEvents.OnMechXpChanged?.Invoke(Level, Xp, XpToNext);
    }
}
