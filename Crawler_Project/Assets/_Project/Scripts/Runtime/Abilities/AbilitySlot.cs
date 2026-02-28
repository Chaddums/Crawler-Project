namespace DungeonCrawlerCarl
{
    [System.Serializable]
    public class AbilitySlot
    {
        public AbilityData Ability;
        public float CurrentCooldown;

        public bool IsReady => CurrentCooldown <= 0f && Ability != null;
        public bool IsEmpty => Ability == null;

        public float CooldownPercent
        {
            get
            {
                if (Ability == null || Ability.Cooldown <= 0) return 0f;
                return CurrentCooldown / Ability.Cooldown;
            }
        }

        public void Use()
        {
            if (Ability != null)
                CurrentCooldown = Ability.Cooldown;
        }

        public void Tick(float deltaTime, float cooldownReduction = 0f)
        {
            if (CurrentCooldown > 0)
                CurrentCooldown -= deltaTime * (1f + cooldownReduction);

            if (CurrentCooldown < 0)
                CurrentCooldown = 0;
        }

        public void ResetCooldown()
        {
            CurrentCooldown = 0f;
        }
    }
}
