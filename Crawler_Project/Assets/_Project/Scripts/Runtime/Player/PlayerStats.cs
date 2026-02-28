using System;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private int _startingLevel = 1;
        [SerializeField] private int _baseExperienceToLevel = 100;
        [SerializeField] private float _experienceScaleFactor = 1.5f;
        [SerializeField] private int _skillPointsPerLevel = 1;

        public StatBlock Stats { get; private set; } = new();
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int ExperienceToNextLevel { get; private set; }
        public int AvailableSkillPoints { get; private set; }
        public float CurrentMana { get; private set; }
        public float MaxMana => Stats.GetStat(StatType.MaxMana);

        public event Action<int> OnLevelUp;
        public event Action<float, float> OnManaChanged;

        private void Awake()
        {
            Level = _startingLevel;
            CalculateExperienceToNextLevel();
        }

        private void OnEnable()
        {
            GameEvents.OnExperienceGained += AddExperience;
        }

        private void OnDisable()
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

            Debug.Log($"[PlayerStats] Level up! Now level {Level}");
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
