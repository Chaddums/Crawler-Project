namespace DungeonCrawlerCarl
{
    public class AbilitySlot
    {
        public AbilityData Data { get; set; }
        public float CooldownRemaining { get; private set; }
        public bool IsReady => Data != null && CooldownRemaining <= 0f;
        public bool IsEmpty => Data == null;

        public AbilitySlot() { }

        public AbilitySlot(AbilityData data)
        {
            Data = data;
        }

        public void StartCooldown()
        {
            if (Data != null)
                CooldownRemaining = Data.Cooldown;
        }

        public void TickCooldown(float delta, float cooldownReduction = 0f)
        {
            if (CooldownRemaining > 0)
            {
                float rate = 1f + cooldownReduction;
                CooldownRemaining -= delta * rate;
                if (CooldownRemaining < 0) CooldownRemaining = 0;
            }
        }

        public void ResetCooldown()
        {
            CooldownRemaining = 0;
        }
    }
}
