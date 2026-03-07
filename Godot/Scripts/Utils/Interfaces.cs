using Godot;

namespace JunkbotArena
{
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damage);
        bool IsAlive { get; }
        Node3D Node { get; }
        Team Team { get; }
    }

    public interface IAttacker
    {
        StatBlock Stats { get; }
        Node3D Node { get; }
        Team Team { get; }
    }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        void Interact(Node playerNode);
        bool CanInteract { get; }
    }

    public interface IAbilityUser
    {
        StatBlock Stats { get; }
        Node3D Node { get; }
        Team Team { get; }
        void OnAbilityUsed(Resource ability);
    }

    public interface IKnockbackable
    {
        void ApplyKnockback(Vector3 sourcePosition, float force);
        void ApplyStun(float duration);
    }

    public interface IItemReceiver
    {
        string DisplayName { get; }
        bool TryAddItem(object item);
    }
}
