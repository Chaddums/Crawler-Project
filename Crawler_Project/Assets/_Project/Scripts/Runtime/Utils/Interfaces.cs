using UnityEngine;

namespace DungeonCrawlerCarl
{
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damage);
        bool IsAlive { get; }
        Transform Transform { get; }
        Team Team { get; }
    }

    public interface IAttacker
    {
        StatBlock Stats { get; }
        Transform Transform { get; }
        Team Team { get; }
    }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        void Interact(GameObject playerObj);
        bool CanInteract { get; }
    }

    public interface IAbilityUser
    {
        StatBlock Stats { get; }
        Transform Transform { get; }
        Team Team { get; }
        void OnAbilityUsed(ScriptableObject ability);
    }

    public interface IKnockbackable
    {
        void ApplyKnockback(Vector3 sourcePosition, float force);
        void ApplyStun(float duration);
    }
}
