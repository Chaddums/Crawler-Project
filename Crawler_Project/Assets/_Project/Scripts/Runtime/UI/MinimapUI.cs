using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private GameObject _roomIconPrefab;

        [Header("Player Marker")]
        [SerializeField] private Image _playerMarker;

        [Header("Colors")]
        [SerializeField] private Color _undiscoveredColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color _discoveredColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color _currentRoomColor = new Color(1f, 0.85f, 0f, 1f);
        [SerializeField] private Color _clearedColor = new Color(0.3f, 0.7f, 0.3f, 1f);
        [SerializeField] private Color _bossRoomColor = new Color(0.8f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color _treasureRoomColor = new Color(0.9f, 0.7f, 0.1f, 1f);
        [SerializeField] private Color _shopRoomColor = new Color(0.3f, 0.5f, 1f, 1f);

        [Header("Layout")]
        [SerializeField] private float _roomIconSize = 24f;
        [SerializeField] private float _roomSpacing = 4f;

        private readonly Dictionary<RoomController, RoomIconData> _roomIcons = new();
        private RoomController _currentRoom;
        private float _pulseTimer;

        private struct RoomIconData
        {
            public GameObject IconObject;
            public Image IconImage;
            public RectTransform RectTransform;
            public bool IsDiscovered;
        }

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnRoomCleared += HandleRoomCleared;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnRoomCleared -= HandleRoomCleared;
        }

        private void Start()
        {
            // Hide player marker until a room is entered
            if (_playerMarker != null)
                _playerMarker.enabled = false;
        }

        private void Update()
        {
            // Pulse current room icon
            if (_currentRoom != null && _roomIcons.TryGetValue(_currentRoom, out var data) && data.IconImage != null)
            {
                _pulseTimer += Time.deltaTime * 3f;
                float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f; // 0..1
                data.IconImage.color = Color.Lerp(_currentRoomColor, Color.white, pulse * 0.25f);
            }
        }

        public void Initialize(IReadOnlyList<RoomController> rooms)
        {
            ClearIcons();

            if (rooms == null || rooms.Count == 0) return;

            // Calculate grid center offset
            Vector3 centerPos = Vector3.zero;
            foreach (var room in rooms)
            {
                centerPos += room.transform.position;
            }
            centerPos /= rooms.Count;

            foreach (var room in rooms)
            {
                CreateRoomIcon(room, centerPos);
            }
        }

        private void CreateRoomIcon(RoomController room, Vector3 centerOffset)
        {
            if (_roomIconPrefab == null || _container == null) return;

            GameObject iconObj = Instantiate(_roomIconPrefab, _container);
            var rectTransform = iconObj.GetComponent<RectTransform>();
            var image = iconObj.GetComponent<Image>();

            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(_roomIconSize, _roomIconSize);

                // Map world position to minimap position
                Vector3 worldPos = room.transform.position - centerOffset;
                float scale = (_roomIconSize + _roomSpacing) / 10f; // rough scale factor
                rectTransform.anchoredPosition = new Vector2(worldPos.x * scale, worldPos.z * scale);
            }

            if (image != null)
            {
                image.color = _undiscoveredColor;
            }

            var iconData = new RoomIconData
            {
                IconObject = iconObj,
                IconImage = image,
                RectTransform = rectTransform,
                IsDiscovered = false
            };

            _roomIcons[room] = iconData;
        }

        public void HighlightCurrentRoom(RoomController room)
        {
            // Revert previous current room color
            if (_currentRoom != null && _roomIcons.TryGetValue(_currentRoom, out var prevData))
            {
                if (prevData.IconImage != null)
                {
                    prevData.IconImage.color = prevData.IsDiscovered
                        ? GetRoomTypeColor(_currentRoom)
                        : _undiscoveredColor;
                }
            }

            _currentRoom = room;
            _pulseTimer = 0f;

            // Highlight new current room
            if (_currentRoom != null && _roomIcons.TryGetValue(_currentRoom, out var data))
            {
                data.IsDiscovered = true;
                _roomIcons[_currentRoom] = data;

                if (data.IconImage != null)
                    data.IconImage.color = _currentRoomColor;

                // Position player marker at current room icon
                if (_playerMarker != null && data.RectTransform != null)
                {
                    _playerMarker.enabled = true;
                    _playerMarker.rectTransform.anchoredPosition = data.RectTransform.anchoredPosition;
                }
            }
        }

        private void HandleRoomEntered(GameObject roomObj)
        {
            if (roomObj == null) return;
            var room = roomObj.GetComponent<RoomController>();
            if (room == null) return;

            // Mark as discovered
            if (_roomIcons.TryGetValue(room, out var data))
            {
                data.IsDiscovered = true;
                _roomIcons[room] = data;
            }

            HighlightCurrentRoom(room);
        }

        private void HandleRoomCleared(GameObject roomObj)
        {
            if (roomObj == null) return;
            var room = roomObj.GetComponent<RoomController>();
            if (room == null) return;

            if (_roomIcons.TryGetValue(room, out var data))
            {
                data.IsDiscovered = true;
                _roomIcons[room] = data;

                // Update color if not current room (current room stays gold)
                if (room != _currentRoom && data.IconImage != null)
                    data.IconImage.color = _clearedColor;
            }
        }

        private Color GetRoomTypeColor(RoomController room)
        {
            if (room.IsCleared) return _clearedColor;

            switch (room.RoomType)
            {
                case RoomType.Boss: return _bossRoomColor;
                case RoomType.Treasure: return _treasureRoomColor;
                case RoomType.Shop: return _shopRoomColor;
                default: return _discoveredColor;
            }
        }

        private void ClearIcons()
        {
            foreach (var kvp in _roomIcons)
            {
                if (kvp.Value.IconObject != null)
                    Destroy(kvp.Value.IconObject);
            }
            _roomIcons.Clear();
            _currentRoom = null;

            if (_playerMarker != null)
                _playerMarker.enabled = false;
        }

        private void OnDestroy()
        {
            ClearIcons();
        }
    }
}
