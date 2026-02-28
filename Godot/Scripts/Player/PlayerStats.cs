using System;
using Godot;

namespace JunkbotArena
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

        private BotFrameData _classData;

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

            // Testing: start with bonus skill points
            AvailableSkillPoints = 10;
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

        public void SetClassData(BotFrameData classData)
        {
            _classData = classData;
        }

        private void LevelUp()
        {
            Level++;
            AvailableSkillPoints += _skillPointsPerLevel;
            CalculateExperienceToNextLevel();

            // Apply class-based stat growth
            if (_classData != null)
            {
                Stats.SetBaseStat(StatType.MaxHealth,
                    Stats.GetBaseStat(StatType.MaxHealth) + _classData.HpPerLevel);
                Stats.SetBaseStat(StatType.MaxMana,
                    Stats.GetBaseStat(StatType.MaxMana) + _classData.ManaPerLevel);
                Stats.SetBaseStat(_classData.PrimaryStat,
                    Stats.GetBaseStat(_classData.PrimaryStat) + _classData.PrimaryStatPerLevel);
                Stats.SetBaseStat(_classData.SecondaryStat,
                    Stats.GetBaseStat(_classData.SecondaryStat) + _classData.SecondaryStatPerLevel);
            }

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

        // Setter methods for save/load restoration
        public void SetLevel(int level)
        {
            Level = level;
            CalculateExperienceToNextLevel();
        }

        public void SetExperience(int xp)
        {
            Experience = xp;
        }

        public void SetSkillPoints(int points)
        {
            AvailableSkillPoints = points;
        }

        public void SetMana(float mana)
        {
            CurrentMana = Mathf.Clamp(mana, 0, MaxMana);
            OnManaChanged?.Invoke(CurrentMana, MaxMana);
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
