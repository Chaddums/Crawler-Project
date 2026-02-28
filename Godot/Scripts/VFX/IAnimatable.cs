namespace JunkbotArena
{
    public interface IAnimatable
    {
        void SetState(AnimState state);
        AnimState CurrentState { get; }
    }
}
