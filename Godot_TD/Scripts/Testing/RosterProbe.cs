using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// For the gameplay suite's roster test: frees its target in a physics step and reads
    /// <see cref="Roster"/> in the next process frame, counting freed entries it was handed.
    /// </summary>
    public partial class RosterProbe : Node
    {
        public VineEnemy Target;
        public bool FreedInPhysics, Done, TargetAfter;
        public int StaleAfter;

        public override void _PhysicsProcess(double delta)
        {
            if (FreedInPhysics || Target == null) return;
            Roster.Enemies(GetTree()); // the step's list, with the target in it
            FreedInPhysics = Engine.IsInPhysicsFrame();
            Target.QueueFree();
        }

        public override void _Process(double delta)
        {
            if (!FreedInPhysics || Done) return;
            foreach (var e in Roster.Enemies(GetTree()))
            {
                if (!IsInstanceValid(e)) StaleAfter++;
                if (ReferenceEquals(e, Target)) TargetAfter = true;
            }
            Done = true;
        }
    }
}
