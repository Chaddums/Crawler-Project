using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Builds map layouts. Sets spawn points, core position, initial debris/walls.
    /// Maps can be hand-crafted (JSON) or procedurally generated.
    /// </summary>
    public partial class MapBuilder : Node
    {
        /// <summary>
        /// Build a default test map: spawn on left, core on right, scattered debris.
        /// </summary>
        public static void BuildDefaultMap(MapGrid grid)
        {
            // Spawn points on left edge
            grid.SetSpawnPoint(0, grid.Height / 4);
            grid.SetSpawnPoint(0, grid.Height * 3 / 4);

            // Core on right side
            grid.SetCorePosition(grid.Width - 2, grid.Height / 2);

            // Build the core visual
            BuildCoreVisual(grid, grid.CorePosition);

            // Scatter debris (pillar #3: terrain as resource)
            var rng = new RandomNumberGenerator();
            rng.Seed = 42;

            int debrisCount = (grid.Width * grid.Height) / 8;
            for (int i = 0; i < debrisCount; i++)
            {
                int x = rng.RandiRange(2, grid.Width - 4);
                int y = rng.RandiRange(1, grid.Height - 2);
                if (grid.GetCell(x, y) == TerrainType.Open)
                {
                    grid.SetCell(x, y, TerrainType.Debris);
                    BuildDebrisVisual(grid, x, y, rng);
                }
            }

            // A few permanent walls for initial maze structure
            for (int y = grid.Height / 3; y < grid.Height / 3 + 4; y++)
            {
                int x = grid.Width / 3;
                if (grid.GetCell(x, y) == TerrainType.Open)
                {
                    grid.SetCell(x, y, TerrainType.Blocked);
                    BuildWallVisual(grid, x, y);
                }
            }
            for (int y = grid.Height * 2 / 3 - 3; y < grid.Height * 2 / 3 + 1; y++)
            {
                int x = grid.Width * 2 / 3;
                if (grid.GetCell(x, y) == TerrainType.Open)
                {
                    grid.SetCell(x, y, TerrainType.Blocked);
                    BuildWallVisual(grid, x, y);
                }
            }

            // Build spawn point visuals
            foreach (var spawn in grid.SpawnPoints)
                BuildSpawnVisual(grid, spawn);
        }

        public static void BuildCoreVisual(MapGrid grid, Vector2I pos)
        {
            var mesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(Constants.CELL_SIZE * 0.8f, 2f, Constants.CELL_SIZE * 0.8f);
            mesh.Mesh = box;
            mesh.Position = grid.GridToWorld(pos) + new Vector3(0, 1f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.2f, 0.6f, 1f);
            mat.Emission = new Color(0.1f, 0.3f, 0.8f);
            mat.EmissionEnabled = true;
            mat.EmissionEnergyMultiplier = 2f;
            mesh.MaterialOverride = mat;
            grid.AddChild(mesh);
        }

        public static void BuildSpawnVisual(MapGrid grid, Vector2I pos)
        {
            var mesh = new MeshInstance3D();
            var cylinder = new CylinderMesh();
            cylinder.TopRadius = Constants.CELL_SIZE * 0.4f;
            cylinder.BottomRadius = Constants.CELL_SIZE * 0.4f;
            cylinder.Height = 0.3f;
            mesh.Mesh = cylinder;
            mesh.Position = grid.GridToWorld(pos) + new Vector3(0, 0.15f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.9f, 0.2f, 0.1f);
            mat.Emission = new Color(0.8f, 0.1f, 0.05f);
            mat.EmissionEnabled = true;
            mesh.MaterialOverride = mat;
            grid.AddChild(mesh);
        }

        public static void BuildDebrisVisual(MapGrid grid, int x, int y, RandomNumberGenerator rng)
        {
            var mesh = new MeshInstance3D();
            var box = new BoxMesh();
            float h = rng.RandfRange(0.3f, 0.8f);
            box.Size = new Vector3(
                Constants.CELL_SIZE * rng.RandfRange(0.4f, 0.7f),
                h,
                Constants.CELL_SIZE * rng.RandfRange(0.4f, 0.7f)
            );
            mesh.Mesh = box;
            mesh.Position = grid.GridToWorld(x, y) + new Vector3(0, h / 2f, 0);
            mesh.RotateY(rng.RandfRange(0, Mathf.Pi));

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(
                rng.RandfRange(0.25f, 0.4f),
                rng.RandfRange(0.2f, 0.3f),
                rng.RandfRange(0.1f, 0.2f)
            );
            mat.Roughness = 0.95f;
            mesh.MaterialOverride = mat;
            grid.AddChild(mesh);
        }

        public static void BuildWallVisual(MapGrid grid, int x, int y)
        {
            var mesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(Constants.CELL_SIZE * 0.9f, 1.5f, Constants.CELL_SIZE * 0.9f);
            mesh.Mesh = box;
            mesh.Position = grid.GridToWorld(x, y) + new Vector3(0, 0.75f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 0.28f, 0.25f);
            mat.Roughness = 0.85f;
            mesh.MaterialOverride = mat;
            grid.AddChild(mesh);
        }
    }
}
