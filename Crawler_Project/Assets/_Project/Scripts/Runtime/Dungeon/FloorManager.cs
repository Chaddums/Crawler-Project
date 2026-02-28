using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class FloorManager : MonoBehaviour
    {
        [Header("Floor Configuration")]
        [SerializeField] private FloorData _floorData;
        [SerializeField] private Transform _roomParent;
        [SerializeField] private Transform _playerSpawnPoint;

        [Header("Generation")]
        [Tooltip("Optional DungeonGenerator for procedural layouts. If absent, hand-placed rooms are used.")]
        [SerializeField] private DungeonGenerator _dungeonGenerator;

        private readonly List<RoomController> _rooms = new();
        private RoomController _currentRoom;

        public FloorData FloorData => _floorData;
        public IReadOnlyList<RoomController> Rooms => _rooms;
        public RoomController CurrentRoom => _currentRoom;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void Start()
        {
            GenerateFloor();
            SpawnPlayer();

            GameEvents.OnFloorEntered?.Invoke(_floorData != null ? _floorData.FloorNumber : 0);
        }

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoomEntered;
        }

        private void GenerateFloor()
        {
            _rooms.Clear();

            if (_floorData != null && _floorData.UseProceduralGeneration && _dungeonGenerator != null)
            {
                // Procedural path
                var generated = _dungeonGenerator.Generate(_floorData);
                if (generated != null)
                    _rooms.AddRange(generated);
            }
            else
            {
                // Hand-placed path: find existing RoomControllers in the scene
                Transform searchRoot = _roomParent != null ? _roomParent : transform;
                var found = searchRoot.GetComponentsInChildren<RoomController>(true);
                _rooms.AddRange(found);
            }

            // Initialize every room with the floor data
            foreach (var room in _rooms)
            {
                room.Initialize(_floorData);
            }

            Debug.Log($"[FloorManager] Floor generated with {_rooms.Count} rooms. " +
                      $"(Procedural: {_floorData != null && _floorData.UseProceduralGeneration})");
        }

        private void SpawnPlayer()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                Vector3 spawnPosition = _playerSpawnPoint != null
                    ? _playerSpawnPoint.position
                    : transform.position;

                Quaternion spawnRotation = _playerSpawnPoint != null
                    ? _playerSpawnPoint.rotation
                    : Quaternion.identity;

                playerObj.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                Debug.Log($"[FloorManager] Player spawned at {spawnPosition}.");
            }
            else
            {
                Debug.LogWarning("[FloorManager] Player not found by tag. " +
                                 "Player must be spawned externally.");
            }
        }

        private void HandleRoomEntered(GameObject roomObj)
        {
            _currentRoom = roomObj != null ? roomObj.GetComponent<RoomController>() : null;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<FloorManager>();
        }
    }
}
