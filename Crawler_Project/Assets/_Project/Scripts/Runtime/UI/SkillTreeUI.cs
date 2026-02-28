using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class SkillTreeUI : UIPanel, IExclusivePanel
    {
        [Header("Layout")]
        [SerializeField] private RectTransform _nodeContainer;
        [SerializeField] private RectTransform _lineContainer;
        [SerializeField] private GameObject _nodePrefab;
        [SerializeField] private GameObject _linePrefab;
        [SerializeField] private float _nodeScale = 80f;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _pointsText;

        [Header("Tooltip")]
        [SerializeField] private RectTransform _tooltipPanel;
        [SerializeField] private TextMeshProUGUI _tooltipName;
        [SerializeField] private TextMeshProUGUI _tooltipDescription;
        [SerializeField] private TextMeshProUGUI _tooltipCost;

        [Header("Respec")]
        [SerializeField] private Button _respecButton;
        [SerializeField] private int _respecGoldCost = 100;

        [Header("Colors")]
        [SerializeField] private Color _lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _availableColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color _unlockedColor = new Color(0.2f, 1f, 0.4f, 1f);
        [SerializeField] private Color _lineLockedColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
        [SerializeField] private Color _lineUnlockedColor = new Color(0.2f, 1f, 0.4f, 0.8f);

        private SkillTree _skillTree;
        private PlayerStats _playerStats;
        private readonly Dictionary<string, SkillNodeView> _nodeViews = new();
        private readonly List<GameObject> _lineObjects = new();

        private void OnEnable()
        {
            GameEvents.OnPlayerLevelUp += HandleLevelUp;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerLevelUp -= HandleLevelUp;
        }

        private void Start()
        {
            SetVisibleImmediate(false);
            if (_tooltipPanel != null)
                _tooltipPanel.gameObject.SetActive(false);
        }

        public void Open(SkillTree skillTree, PlayerStats playerStats)
        {
            _skillTree = skillTree;
            _playerStats = playerStats;
            Show();
            BuildTree();
            RefreshAll();

            if (_respecButton != null)
            {
                _respecButton.onClick.RemoveAllListeners();
                _respecButton.onClick.AddListener(HandleRespec);
            }
        }

        public void Close()
        {
            Hide();
            HideTooltip();

            if (_respecButton != null)
                _respecButton.onClick.RemoveAllListeners();
        }

        public void Toggle(SkillTree skillTree, PlayerStats playerStats)
        {
            if (IsOpen)
                Close();
            else
                Open(skillTree, playerStats);
        }

        private void BuildTree()
        {
            ClearTree();
            if (_skillTree == null || _skillTree.Data == null) return;

            if (_titleText != null)
                _titleText.text = _skillTree.Data.TreeName ?? "Skill Tree";

            // Create node views
            foreach (var node in _skillTree.Data.Nodes)
            {
                CreateNodeView(node);
            }

            // Draw connections
            foreach (var node in _skillTree.Data.Nodes)
            {
                if (node.PrerequisiteNodeIds == null) continue;

                foreach (var prereqId in node.PrerequisiteNodeIds)
                {
                    if (_nodeViews.TryGetValue(prereqId, out var fromView) &&
                        _nodeViews.TryGetValue(node.NodeId, out var toView))
                    {
                        CreateConnectionLine(fromView.RectTransform, toView.RectTransform, prereqId, node.NodeId);
                    }
                }
            }
        }

        private void CreateNodeView(SkillNodeData node)
        {
            if (_nodePrefab == null || _nodeContainer == null) return;

            var nodeObj = Instantiate(_nodePrefab, _nodeContainer);
            var rectTransform = nodeObj.GetComponent<RectTransform>();

            // Position using TreePosition from data
            rectTransform.anchoredPosition = node.TreePosition * _nodeScale;

            var view = nodeObj.GetComponent<SkillNodeView>();
            if (view == null)
                view = nodeObj.AddComponent<SkillNodeView>();

            view.Initialize(node, OnNodeClicked, OnNodeHoverEnter, OnNodeHoverExit);
            _nodeViews[node.NodeId] = view;
        }

        private void CreateConnectionLine(RectTransform from, RectTransform to, string fromId, string toId)
        {
            if (_linePrefab == null || _lineContainer == null) return;

            var lineObj = Instantiate(_linePrefab, _lineContainer);
            var lineRect = lineObj.GetComponent<RectTransform>();
            var lineImage = lineObj.GetComponent<Image>();

            // Position line between two nodes
            Vector2 fromPos = from.anchoredPosition;
            Vector2 toPos = to.anchoredPosition;
            Vector2 midpoint = (fromPos + toPos) * 0.5f;
            float distance = Vector2.Distance(fromPos, toPos);
            float angle = Mathf.Atan2(toPos.y - fromPos.y, toPos.x - fromPos.x) * Mathf.Rad2Deg;

            lineRect.anchoredPosition = midpoint;
            lineRect.sizeDelta = new Vector2(distance, 3f);
            lineRect.localRotation = Quaternion.Euler(0, 0, angle);

            // Color based on unlock status
            bool connected = _skillTree.IsNodeUnlocked(fromId) && _skillTree.IsNodeUnlocked(toId);
            if (lineImage != null)
                lineImage.color = connected ? _lineUnlockedColor : _lineLockedColor;

            _lineObjects.Add(lineObj);
        }

        private void OnNodeClicked(SkillNodeData node)
        {
            if (_skillTree == null || _playerStats == null) return;
            if (!_skillTree.CanUnlock(node)) return;
            if (!_playerStats.SpendSkillPoint()) return;

            _skillTree.AddPoints(1); // Convert skill point to tree point
            _skillTree.UnlockNode(node.NodeId, _playerStats.Stats);
            UISfx.Instance?.PlaySkillUnlock();
            RefreshAll();
        }

        private void OnNodeHoverEnter(SkillNodeData node)
        {
            ShowTooltip(node);
        }

        private void OnNodeHoverExit(SkillNodeData node)
        {
            HideTooltip();
        }

        private void ShowTooltip(SkillNodeData node)
        {
            if (_tooltipPanel == null) return;

            _tooltipPanel.gameObject.SetActive(true);

            if (_tooltipName != null)
                _tooltipName.text = node.NodeName;

            if (_tooltipDescription != null)
                _tooltipDescription.text = node.Description;

            if (_tooltipCost != null)
            {
                bool unlocked = _skillTree.IsNodeUnlocked(node.NodeId);
                _tooltipCost.text = unlocked ? "UNLOCKED" : $"Cost: {node.PointCost} point(s)";
            }
        }

        private void HideTooltip()
        {
            if (_tooltipPanel != null)
                _tooltipPanel.gameObject.SetActive(false);
        }

        private void RefreshAll()
        {
            if (_skillTree == null) return;

            if (_pointsText != null)
            {
                int availablePoints = _playerStats != null ? _playerStats.AvailableSkillPoints : 0;
                _pointsText.text = $"Skill Points: {availablePoints}";
            }

            foreach (var kvp in _nodeViews)
            {
                var node = kvp.Value.NodeData;
                bool unlocked = _skillTree.IsNodeUnlocked(node.NodeId);
                bool canUnlock = _skillTree.CanUnlock(node) && _playerStats != null && _playerStats.AvailableSkillPoints > 0;

                Color color = unlocked ? _unlockedColor : canUnlock ? _availableColor : _lockedColor;
                kvp.Value.SetState(unlocked, canUnlock, color);
            }

            // Refresh line colors
            RefreshLines();
        }

        private void RefreshLines()
        {
            // Rebuild lines is simplest — they're cheap
            foreach (var line in _lineObjects)
            {
                if (line != null)
                    Destroy(line);
            }
            _lineObjects.Clear();

            if (_skillTree?.Data?.Nodes == null) return;

            foreach (var node in _skillTree.Data.Nodes)
            {
                if (node.PrerequisiteNodeIds == null) continue;
                foreach (var prereqId in node.PrerequisiteNodeIds)
                {
                    if (_nodeViews.TryGetValue(prereqId, out var fromView) &&
                        _nodeViews.TryGetValue(node.NodeId, out var toView))
                    {
                        CreateConnectionLine(fromView.RectTransform, toView.RectTransform, prereqId, node.NodeId);
                    }
                }
            }
        }

        private void ClearTree()
        {
            foreach (var kvp in _nodeViews)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            _nodeViews.Clear();

            foreach (var line in _lineObjects)
            {
                if (line != null)
                    Destroy(line);
            }
            _lineObjects.Clear();
        }

        private void HandleRespec()
        {
            if (_skillTree == null || _playerStats == null) return;
            if (_skillTree.UnlockedNodes.Count == 0) return;

            int refunded = _skillTree.Respec(_playerStats.Stats);

            // Refund skill points back to the player
            _playerStats.RefundSkillPoints(refunded);

            // Rebuild the entire tree UI
            BuildTree();
            RefreshAll();

            Debug.Log($"[SkillTreeUI] Respec complete. {refunded} point(s) refunded.");
        }

        private void HandleLevelUp(int newLevel)
        {
            if (IsOpen)
                RefreshAll();
        }
    }

    public class SkillNodeView : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _nameLabel;

        public SkillNodeData NodeData { get; private set; }
        public RectTransform RectTransform { get; private set; }

        private System.Action<SkillNodeData> _onClicked;
        private System.Action<SkillNodeData> _onHoverEnter;
        private System.Action<SkillNodeData> _onHoverExit;

        private bool _isAvailable;
        private bool _isUnlocked;
        private Color _baseColor;
        private float _pulseTimer;

        public void Initialize(SkillNodeData data,
            System.Action<SkillNodeData> onClicked,
            System.Action<SkillNodeData> onHoverEnter,
            System.Action<SkillNodeData> onHoverExit)
        {
            NodeData = data;
            RectTransform = GetComponent<RectTransform>();
            _onClicked = onClicked;
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;

            if (_iconImage == null)
                _iconImage = GetComponentInChildren<Image>();

            if (_iconImage != null && data.Icon != null)
            {
                _iconImage.sprite = data.Icon;
                _iconImage.enabled = true;
            }

            if (_nameLabel != null)
                _nameLabel.text = data.NodeName;

            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClicked?.Invoke(NodeData));
            }
        }

        public void SetState(bool unlocked, bool canUnlock, Color tint)
        {
            _isUnlocked = unlocked;
            _isAvailable = canUnlock;
            _baseColor = tint;

            if (_background != null)
                _background.color = tint;

            var button = GetComponent<Button>();
            if (button != null)
                button.interactable = !unlocked && canUnlock;
        }

        private void Update()
        {
            // Pulse animation on available (unlockable) nodes
            if (_isAvailable && !_isUnlocked && _background != null)
            {
                _pulseTimer += Time.unscaledDeltaTime * 3f;
                float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f; // 0..1
                _background.color = Color.Lerp(_baseColor, Color.white, pulse * 0.3f);
            }
        }

        /// <summary>Flash white→green over 0.3s when unlocked.</summary>
        public void PlayUnlockEffect()
        {
            StartCoroutine(UnlockFlashCoroutine());
        }

        private IEnumerator UnlockFlashCoroutine()
        {
            if (_background == null) yield break;

            Color green = new Color(0.2f, 1f, 0.4f, 1f);
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _background.color = Color.Lerp(Color.white, green, t);
                yield return null;
            }

            _background.color = green;
        }

        public void OnPointerEnter()
        {
            _onHoverEnter?.Invoke(NodeData);
            UISfx.Instance?.PlayHover();
        }

        public void OnPointerExit()
        {
            _onHoverExit?.Invoke(NodeData);
        }
    }
}
