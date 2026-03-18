using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A region of entry cells that enemies can spawn from.
    /// Instead of a single point, this gives a range of cells for chaotic spawning.
    /// </summary>
    public class VineEntryRegion
    {
        public int Index { get; }
        public List<Vector2I> Cells { get; } = new();

        public VineEntryRegion(int index)
        {
            Index = index;
        }

        /// <summary>
        /// Center cell of the region (for cached path lookups and preview).
        /// </summary>
        public Vector2I Center
        {
            get
            {
                if (Cells.Count == 0) return Vector2I.Zero;
                int midIdx = Cells.Count / 2;
                return Cells[midIdx];
            }
        }

        /// <summary>
        /// Pick a random cell from this region for spawning.
        /// </summary>
        public Vector2I GetRandomSpawnCell(RandomNumberGenerator rng)
        {
            if (Cells.Count == 0) return Vector2I.Zero;
            return Cells[rng.RandiRange(0, Cells.Count - 1)];
        }
    }
}
