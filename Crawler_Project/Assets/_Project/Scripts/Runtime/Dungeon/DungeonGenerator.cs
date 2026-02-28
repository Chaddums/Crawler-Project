using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private Vector2Int _gridSize = new(8, 8);
        [SerializeField] private float _roomSpacing = 25f;

        [Header("Defaults")]
        [Tooltip("Fallback room prefab used when no template matches a required room type.")]
        [SerializeField] private GameObject _fallbackRoomPrefab;

        [Header("Corridors")]
        [Tooltip("Prefab to instantiate as a corridor between connected rooms. Should be a long, narrow hallway.")]
        [SerializeField] private GameObject _corridorPrefab;
        [Tooltip("Scale multiplier for corridor length (1 = room spacing distance).")]
        [SerializeField] private float _corridorLengthMultiplier = 1f;

        // Internal representation of a cell on the generation grid
        private struct Cell
        {
            public Vector2Int Position;
            public RoomType Type;
            public RoomController Room;
            public bool Occupied;
        }

        private Cell[,] _grid;
        private readonly List<Vector2Int> _mainPath = new();

        /// <summary>
        /// Generates a floor layout based on the provided FloorData and returns all created RoomControllers.
        /// Creates a main path from entrance to boss room, then adds branch rooms for treasure/shops.
        /// </summary>
        public List<RoomController> Generate(FloorData floorData)
        {
            if (floorData == null)
            {
                Debug.LogError("[DungeonGenerator] FloorData is null.");
                return new List<RoomController>();
            }

            _grid = new Cell[_gridSize.x, _gridSize.y];
            _mainPath.Clear();

            int targetRoomCount = Random.Range(floorData.MinRooms, floorData.MaxRooms + 1);
            targetRoomCount = Mathf.Max(targetRoomCount, 3); // need at least entrance, one room, boss

            // Step 1 - Build main path (entrance -> ... -> boss)
            BuildMainPath(targetRoomCount);

            // Step 2 - Assign room types along the main path
            AssignMainPathTypes();

            // Step 3 - Add branch rooms (treasure, shop, etc.)
            int branchBudget = targetRoomCount - _mainPath.Count;
            AddBranchRooms(floorData, branchBudget);

            // Step 4 - Instantiate room prefabs
            var rooms = InstantiateRooms(floorData);

            // Step 5 - Connect adjacent rooms with corridors (visual only)
            ConnectRooms();

            Debug.Log($"[DungeonGenerator] Generated {rooms.Count} rooms on a {_gridSize.x}x{_gridSize.y} grid.");
            return rooms;
        }

        // -------------------------------------------------------------------
        //  Step 1 : Main path via random walk
        // -------------------------------------------------------------------

        private void BuildMainPath(int targetLength)
        {
            // Start near the bottom-left
            Vector2Int current = new(0, _gridSize.y / 2);
            MarkCell(current, RoomType.Entrance);
            _mainPath.Add(current);

            int safety = targetLength * 10;

            while (_mainPath.Count < targetLength && safety-- > 0)
            {
                List<Vector2Int> neighbors = GetUnoccupiedNeighbors(current);
                if (neighbors.Count == 0) break;

                // Bias movement to the right to ensure we reach the far side
                Vector2Int next = PickBiasedNeighbor(neighbors, Vector2Int.right);
                MarkCell(next, RoomType.Combat); // default, reassigned later
                _mainPath.Add(next);
                current = next;
            }
        }

        private Vector2Int PickBiasedNeighbor(List<Vector2Int> neighbors, Vector2Int biasDir)
        {
            // 60 % chance to pick one that moves in the bias direction
            List<Vector2Int> biased = new();
            List<Vector2Int> others = new();

            foreach (var n in neighbors)
            {
                Vector2Int delta = n - (_mainPath.Count > 0 ? _mainPath[^1] : Vector2Int.zero);
                if (delta.x * biasDir.x + delta.y * biasDir.y > 0)
                    biased.Add(n);
                else
                    others.Add(n);
            }

            if (biased.Count > 0 && Random.value < 0.6f)
                return biased[Random.Range(0, biased.Count)];

            return neighbors[Random.Range(0, neighbors.Count)];
        }

        // -------------------------------------------------------------------
        //  Step 2 : Assign types on the main path
        // -------------------------------------------------------------------

        private void AssignMainPathTypes()
        {
            if (_mainPath.Count == 0) return;

            // First cell is Entrance
            SetCellType(_mainPath[0], RoomType.Entrance);

            // Last cell is Boss
            if (_mainPath.Count > 1)
                SetCellType(_mainPath[^1], RoomType.Boss);

            // Place a SafeRoom roughly in the middle
            if (_mainPath.Count > 4)
            {
                int midIndex = _mainPath.Count / 2;
                SetCellType(_mainPath[midIndex], RoomType.SafeRoom);
            }

            // Everything else stays Combat
        }

        // -------------------------------------------------------------------
        //  Step 3 : Branch rooms
        // -------------------------------------------------------------------

        private void AddBranchRooms(FloorData floorData, int budget)
        {
            if (budget <= 0) return;

            RoomType[] branchTypes = { RoomType.Treasure, RoomType.Shop, RoomType.Puzzle, RoomType.Event };
            int placed = 0;

            // Try to branch off each main-path cell
            for (int i = 1; i < _mainPath.Count - 1 && placed < budget; i++)
            {
                var neighbors = GetUnoccupiedNeighbors(_mainPath[i]);
                if (neighbors.Count == 0) continue;

                Vector2Int branchPos = neighbors[Random.Range(0, neighbors.Count)];
                RoomType type = branchTypes[placed % branchTypes.Length];
                MarkCell(branchPos, type);
                placed++;
            }
        }

        // -------------------------------------------------------------------
        //  Step 4 : Instantiate prefabs
        // -------------------------------------------------------------------

        private List<RoomController> InstantiateRooms(FloorData floorData)
        {
            var rooms = new List<RoomController>();

            for (int x = 0; x < _gridSize.x; x++)
            {
                for (int y = 0; y < _gridSize.y; y++)
                {
                    ref Cell cell = ref _grid[x, y];
                    if (!cell.Occupied) continue;

                    GameObject prefab = SelectPrefab(floorData, cell.Type);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[DungeonGenerator] No prefab for room type {cell.Type} at ({x},{y}). Skipping.");
                        continue;
                    }

                    Vector3 worldPos = new Vector3(x * _roomSpacing, 0f, y * _roomSpacing);
                    GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity, transform);
                    instance.name = $"Room_{cell.Type}_{x}_{y}";

                    var rc = instance.GetComponent<RoomController>();
                    if (rc == null)
                        rc = instance.AddComponent<RoomController>();

                    cell.Room = rc;
                    rooms.Add(rc);
                }
            }

            return rooms;
        }

        private GameObject SelectPrefab(FloorData floorData, RoomType type)
        {
            if (floorData.RoomTemplates == null || floorData.RoomTemplates.Count == 0)
                return _fallbackRoomPrefab;

            // Gather matching templates
            float totalWeight = 0f;
            List<RoomTemplate> matching = new();

            foreach (var t in floorData.RoomTemplates)
            {
                if (t.Type == type && t.Prefab != null)
                {
                    matching.Add(t);
                    totalWeight += Mathf.Max(t.Weight, 0.01f);
                }
            }

            if (matching.Count == 0)
                return _fallbackRoomPrefab;

            // Weighted random selection
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var t in matching)
            {
                cumulative += Mathf.Max(t.Weight, 0.01f);
                if (roll <= cumulative)
                    return t.Prefab;
            }

            return matching[^1].Prefab;
        }

        // -------------------------------------------------------------------
        //  Step 5 : Connect rooms with corridors
        // -------------------------------------------------------------------

        private void ConnectRooms()
        {
            // Walk the main path and connect consecutive rooms
            for (int i = 0; i < _mainPath.Count - 1; i++)
            {
                ConnectTwoCells(_mainPath[i], _mainPath[i + 1]);
            }

            // Connect branch rooms to their parent main-path cell
            for (int x = 0; x < _gridSize.x; x++)
            {
                for (int y = 0; y < _gridSize.y; y++)
                {
                    if (!_grid[x, y].Occupied) continue;

                    Vector2Int pos = new(x, y);
                    if (_mainPath.Contains(pos)) continue;

                    // Find an adjacent main-path cell to connect to
                    foreach (var dir in Directions)
                    {
                        Vector2Int neighbor = pos + dir;
                        if (InBounds(neighbor) && _grid[neighbor.x, neighbor.y].Occupied && _mainPath.Contains(neighbor))
                        {
                            ConnectTwoCells(pos, neighbor);
                            break;
                        }
                    }
                }
            }
        }

        private void ConnectTwoCells(Vector2Int a, Vector2Int b)
        {
            ref Cell cellA = ref _grid[a.x, a.y];
            ref Cell cellB = ref _grid[b.x, b.y];

            if (cellA.Room == null || cellB.Room == null) return;

            Vector3 posA = cellA.Room.transform.position;
            Vector3 posB = cellB.Room.transform.position;

            if (_corridorPrefab != null)
            {
                Vector3 midpoint = (posA + posB) * 0.5f;
                Vector3 direction = (posB - posA).normalized;
                float distance = Vector3.Distance(posA, posB);

                Quaternion rotation = direction != Vector3.zero
                    ? Quaternion.LookRotation(direction)
                    : Quaternion.identity;

                var corridor = Instantiate(_corridorPrefab, midpoint, rotation, transform);
                corridor.name = $"Corridor_{a.x},{a.y}_to_{b.x},{b.y}";

                // Scale corridor to span the distance between rooms
                Vector3 scale = corridor.transform.localScale;
                scale.z = distance * _corridorLengthMultiplier / _roomSpacing;
                corridor.transform.localScale = scale;
            }
            else
            {
                // Fallback: debug line when no prefab assigned
                Debug.DrawLine(posA, posB, Color.yellow, 60f);
            }
        }

        // -------------------------------------------------------------------
        //  Grid helpers
        // -------------------------------------------------------------------

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        private bool InBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y;
        }

        private void MarkCell(Vector2Int pos, RoomType type)
        {
            _grid[pos.x, pos.y] = new Cell
            {
                Position = pos,
                Type = type,
                Room = null,
                Occupied = true
            };
        }

        private void SetCellType(Vector2Int pos, RoomType type)
        {
            ref Cell cell = ref _grid[pos.x, pos.y];
            cell.Type = type;
        }

        private List<Vector2Int> GetUnoccupiedNeighbors(Vector2Int pos)
        {
            var results = new List<Vector2Int>(4);
            foreach (var dir in Directions)
            {
                Vector2Int n = pos + dir;
                if (InBounds(n) && !_grid[n.x, n.y].Occupied)
                    results.Add(n);
            }
            return results;
        }
    }
}
