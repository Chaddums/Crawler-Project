using System;
using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Level, XP, mana, and stat management for the player.
    /// </summary>
    public partial class PlayerStats : Node
    {
        [Export] private int _startingLevel = 1;
        [Export] private int _baseExperienceToLevel = 100;
        [Export] private float _experienceScaleFactor = 1.5f;
        [Export] private int _skillPointsPerLevel = 1;

        public StatBlock Stats { get; private set; } = new();
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int ExperienceToNextLevel { get; private set; }
        public int AvailableSkillPoints { get; private set; }
        public float CurrentMana { get; private set; }
        public float MaxMana => Stats.GetStat(StatType.MaxMana);

        public event Action<int> OnLevelUp;
        public event Action<float, float> OnManaChanged;

        public override void _Ready()
        {
            Level = _startingLevel;
            CalculateExperienceToNextLevel();

            // Set some default base stats for testing
            Stats.SetBaseStat(StatType.MaxHealth, 100f);
            Stats.SetBaseStat(StatType.MaxMana, 50f);
            Stats.SetBaseStat(StatType.Strength, 10f);
            Stats.SetBaseStat(StatType.Dexterity, 10f);
            Stats.SetBaseStat(StatType.Constitution, 10f);
            Stats.SetBaseStat(StatType.Intelligence, 10f);
            Stats.SetBaseStat(StatType.MoveSpeed, Constants.DEFAULT_MOVE_SPEED);

            CurrentMana = MaxMana;
        }

        public override void _EnterTree()
        {
            GameEvents.OnExperienceGained += AddExperience;
        }

        public override void _ExitTree()
        {
            GameEvents.OnExperienceGained -= AddExperience;
        }

        public void InitializeStats(StatBlock baseStats)
        {
            Stats.CopyBaseStatsFrom(baseStats);
            CurrentMana = MaxMana;
        }

        public void AddExperience(int amount)
        {
            Experience += amount;

            while (Experience >= ExperienceToNextLevel)
            {
                Experience -= ExperienceToNextLevel;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            Level++;
            AvailableSkillPoints += _skillPointsPerLevel;
            CalculateExperienceToNextLevel();

            OnLevelUp?.Invoke(Level);
            GameEvents.OnPlayerLevelUp?.Invoke(Level);

            GD.Print($"[PlayerStats] Level up! Now level {Level}");
        }

        private void CalculateExperienceToNextLevel()
        {
            ExperienceToNextLevel = Mathf.RoundToInt(_baseExperienceToLevel * Mathf.Pow(_experienceScaleFactor, Level - 1));
        }

        public bool SpendSkillPoint()
        {
            if (AvailableSkillPoints <= 0) return false;
            AvailableSkillPoints--;
            return true;
        }

        public void RefundSkillPoints(int count)
        {
            AvailableSkillPoints += count;
        }

        public float GetStat(StatType type) => Stats.GetStat(type);

        public bool SpendMana(float amount)
        {
            if (amount <= 0f) return true;
            if (CurrentMana < amount) return false;

            CurrentMana -= amount;
            OnManaChanged?.Invoke(CurrentMana, MaxMana);
            return true;
        }

        public void RestoreMana(float amount)
        {
            if (amount <= 0f) return;
            float max = MaxMana;
            CurrentMana = Mathf.Min(max, CurrentMana + amount);
            OnManaChanged?.Invoke(CurrentMana, max);
        }
    }
}
