namespace DungeonCrawlerCarl
{
    public interface IAnimatable
    {
        void SetState(AnimState state);
        AnimState CurrentState { get; }
    }
}
