using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Print a line the first time it comes up. Model set-up lines were printed for every enemy
    /// spawned (about ten lines each, 13,000 enemies by wave 140: a 6 MB log written mid-wave).
    /// </summary>
    public static class LogOnce
    {
        private static readonly HashSet<string> _seen = new();

        public static void Print(string line)
        {
            if (_seen.Count > 4000) return; // a runaway caller can't grow this without end
            if (_seen.Add(line)) GD.Print(line);
        }
    }
}
