using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What a tower sees of the canyon floor, drawn on the floor (Sight): while the build ghost is
    /// on a wall top, and while a tower's panel is open. The floor cells in its reach it can see
    /// are lit in the player's colour; the ones in reach it can't see (under the lip in front of a
    /// tower set back from the edge, or behind another wall) are marked dark. Canyon maps only.
    /// One multimesh, rebuilt only when the cell or the reach changes.
    /// </summary>
    public partial class SightOverlay : MultiMeshInstance3D
    {
        private static SightOverlay _current;
        private Vector2I _cell = new(-999, -999);
        private float _range = -1f, _eyeUp = -1f;
        private bool _air;

        /// <summary>Floor cells in reach it sees, and in reach it doesn't (tests, the build hint).</summary>
        public static int Seen { get; private set; }
        public static int Hidden { get; private set; }
        /// <summary>For a tower that hits flyers: cells in reach (floor and wall tops) where it sees a flyer, of all in reach.</summary>
        public static int AirSeen { get; private set; }
        public static int AirCells { get; private set; }
        public static bool ShowsAir { get; private set; }
        private static readonly Color AirColor = new(0.55f, 0.85f, 1f, 0.42f);
        public static bool Showing => _current != null && IsInstanceValid(_current) && _current.Visible;

        private static readonly Color SeenColor = new(0.3f, 0.9f, 1f, 0.26f), HiddenColor = new(0.06f, 0.04f, 0.1f, 0.42f);

        /// <summary>Show what a tower on <paramref name="cell"/> with <paramref name="range"/> would see.</summary>
        /// <summary>
        /// Show what a tower on <paramref name="cell"/> would see. <paramref name="air"/>: it hits
        /// flyers, so the air it covers shows too (dots at a flyer's height over every cell in
        /// reach where it would see one: flyers cross the wall tops, so the floor alone said
        /// nothing about them; the playtest: "the attackable space doesn't take into account
        /// flying units").
        /// </summary>
        public static void Show(Node anyNode, VineGrid grid, Vector2I cell, float range, float eyeUp, bool air)
        {
            _wantAir = air;
            Show(anyNode, grid, cell, range, eyeUp);
        }
        private static bool _wantAir;

        public static void Show(Node anyNode, VineGrid grid, Vector2I cell, float range, float eyeUp)
        {
            if (grid == null || !grid.IsCanyon || range <= 0f || anyNode == null || !anyNode.IsInsideTree()) { Hide(); return; }
            if (_current == null || !IsInstanceValid(_current) || !_current.IsInsideTree())
            {
                _current = new SightOverlay { Name = "SightOverlay", CastShadow = ShadowCastingSetting.Off };
                anyNode.GetTree().CurrentScene.AddChild(_current);
            }
            _current.Visible = true;
            bool air = _wantAir;
            _wantAir = false;
            if (_current._cell == cell && Mathf.IsEqualApprox(_current._range, range) && Mathf.IsEqualApprox(_current._eyeUp, eyeUp) && _current._air == air) return;
            _current._air = air;
            _current.Build(grid, cell, range, eyeUp);
        }

        public static void Hide()
        {
            if (_current != null && IsInstanceValid(_current)) _current.Visible = false;
        }

        private void Build(VineGrid grid, Vector2I cell, float range, float eyeUp)
        {
            _cell = cell; _range = range; _eyeUp = eyeUp;
            float cs = Constants.VINE_CELL_SIZE;
            var eye = Sight.TowerEyeAt(grid, cell, eyeUp);
            int r = Mathf.CeilToInt(range / cs);
            var at = new List<(Vector3 p, bool seen)>();
            int seen = 0, hidden = 0;
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    var c = cell + new Vector2I(dx, dy);
                    if (!grid.InBounds(c) || !grid.IsWalkable(c)) continue;
                    var w = grid.GridToWorld(c);
                    if (new Vector2(w.X - eye.X, w.Z - eye.Z).Length() > range) continue;
                    bool sees = grid.HasLineOfSight(eye, Sight.WalkerAt(grid, c));
                    if (sees) seen++; else hidden++;
                    at.Add((new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z) + 0.07f, w.Z), sees));
                }
            Seen = seen; Hidden = hidden;
            // The air: a dot at a flyer's height over every cell in reach where it would see one
            var air = new List<Vector3>();
            int airCells = 0;
            if (_air)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var c = cell + new Vector2I(dx, dy);
                        if (!grid.InBounds(c)) continue;
                        var w = grid.GridToWorld(c);
                        if (new Vector2(w.X - eye.X, w.Z - eye.Z).Length() > range) continue;
                        airCells++;
                        var fly = new Vector3(w.X, grid.GetWorldHeight(w.X, w.Z) + VineEnemy.FLY_HEIGHT, w.Z);
                        if (grid.HasLineOfSight(eye, fly)) air.Add(fly);
                    }
            AirSeen = air.Count; AirCells = airCells; ShowsAir = _air;
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = new PlaneMesh { Size = new Vector2(cs * 0.86f, cs * 0.86f) },
                InstanceCount = at.Count + air.Count,
            };
            for (int i = 0; i < at.Count; i++)
            {
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, at[i].p));
                mm.SetInstanceColor(i, at[i].seen ? SeenColor : HiddenColor);
            }
            var dot = Basis.Identity.Scaled(new Vector3(0.22f, 1f, 0.22f)).Rotated(Vector3.Up, Mathf.Pi / 4f);
            for (int i = 0; i < air.Count; i++)
            {
                mm.SetInstanceTransform(at.Count + i, new Transform3D(dot, air[i]));
                mm.SetInstanceColor(at.Count + i, AirColor);
            }
            Multimesh = mm;
            MaterialOverride ??= new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
                AlbedoColor = Colors.White,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
        }

        public override void _ExitTree()
        {
            if (_current == this) _current = null;
        }
    }
}
