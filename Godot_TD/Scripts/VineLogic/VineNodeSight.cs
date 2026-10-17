using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What a tower sees, remembered by floor cell (Sight). A tower never moves, so for each cell
    /// in its reach and each body height band it works out once whether an enemy anywhere in that
    /// cell is in sight (every sample of the cell, at the band's lowest and highest body, is),
    /// out of sight (none is), or depends where in the cell it stands; only the last needs the
    /// exact line each time. 300 towers searching every enemy with an exact line each doubled the
    /// towers' time a frame. Forgotten when the ground changes (VineGrid.TerrainVersion).
    /// </summary>
    public partial class VineNode
    {
        // Per band, per cell in a square round the tower: 0 not worked out, 1 seen, 2 hidden, 3 partly
        private byte[] _sightCells;
        private Vector2I _sightOrigin;
        private int _sightSide = -1, _sightTerrain = -1;
        private float _sightEye = float.NaN;

        // Body centre bands (an enemy's CentreUp): low (rats, drones), mid, tall; over that, exact
        private static readonly float[] BandLo = { 0.25f, 0.45f, 0.9f }, BandHi = { 0.45f, 0.9f, 1.3f };
        // Where in a cell bodies are sampled: its middle and four points in from its corners
        private static readonly Vector2[] CellSamples =
            { Vector2.Zero, new(0.75f, 0.75f), new(-0.75f, 0.75f), new(0.75f, -0.75f), new(-0.75f, -0.75f) };

        /// <summary>
        /// Can an enemy standing at <paramref name="at"/> see this tower (the same line both ways):
        /// for enemy fire, whatever the tower is (a mortar doesn't need to see, its crew can be seen).
        /// </summary>
        internal bool SeenBy(VineEnemy ve, Vector3 at) => Sees(ve, at, EyePoint);

        /// <summary>Does this tower see an enemy standing at <paramref name="at"/> (its feet), seeing from <paramref name="eye"/>.</summary>
        private bool Sees(VineEnemy ve, Vector3 at, Vector3 eye)
        {
            float up = ve.CentreUp;
            if (!ServiceLocator.TryGet<VineGrid>(out var grid)) return true;
            int band = up < BandLo[0] ? -1 : up <= BandHi[0] ? 0 : up <= BandHi[1] ? 1 : up <= BandHi[2] ? 2 : -1;
            if (ve.IsFlying || band < 0) return grid.HasLineOfSight(eye, at + Vector3.Up * up);
            var cell = grid.WorldToGrid(at);
            int i = SightIndex(grid, cell, band, eye);
            if (i < 0) return grid.HasLineOfSight(eye, at + Vector3.Up * up);
            byte s = _sightCells[i];
            if (s == 0) _sightCells[i] = s = Classify(grid, cell, band, eye);
            return s == 1 || (s == 3 && grid.HasLineOfSight(eye, at + Vector3.Up * up));
        }

        /// <summary>The slot for <paramref name="cell"/> and <paramref name="band"/>, making the table when needed; -1 outside it.</summary>
        private int SightIndex(VineGrid grid, Vector2I cell, int band, Vector3 eye)
        {
            if (_sightCells == null || _sightTerrain != grid.TerrainVersion || !Mathf.IsEqualApprox(_sightEye, eye.Y)
                || Mathf.Abs(_sightOrigin.X + _sightSide / 2 - GridPosition.X) > 0 || Mathf.Abs(_sightOrigin.Y + _sightSide / 2 - GridPosition.Y) > 0)
            {
                // Reach in cells, with room for range boosts
                int r = Mathf.CeilToInt(Mathf.Max(Data?.Range ?? 8f, 8f) * 1.6f / Constants.VINE_CELL_SIZE) + 1;
                _sightSide = 2 * r + 1;
                _sightOrigin = GridPosition - new Vector2I(r, r);
                _sightCells = new byte[_sightSide * _sightSide * BandLo.Length];
                _sightTerrain = grid.TerrainVersion;
                _sightEye = eye.Y;
            }
            int x = cell.X - _sightOrigin.X, y = cell.Y - _sightOrigin.Y;
            if (x < 0 || y < 0 || x >= _sightSide || y >= _sightSide) return -1;
            return (band * _sightSide + y) * _sightSide + x;
        }

        private static byte Classify(VineGrid grid, Vector2I cell, int band, Vector3 eye)
        {
            var c = grid.GridToWorld(cell);
            int seen = 0, total = 0;
            foreach (var o in CellSamples)
            {
                float px = c.X + o.X, pz = c.Z + o.Y;
                float g = grid.GetWorldHeight(px, pz);
                foreach (float up in new[] { BandLo[band], BandHi[band] })
                {
                    total++;
                    if (grid.HasLineOfSight(eye, new Vector3(px, g + up, pz))) seen++;
                }
            }
            return seen == total ? (byte)1 : seen == 0 ? (byte)2 : (byte)3;
        }
    }
}
