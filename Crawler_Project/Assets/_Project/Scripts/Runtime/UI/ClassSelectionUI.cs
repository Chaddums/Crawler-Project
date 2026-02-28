using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawlerCarl
{
    public class ClassSelectionUI : MonoBehaviour
    {
        [Header("Race Selection")]
        [SerializeField] private RaceData[] _availableRaces;
        [SerializeField] private Transform _raceButtonContainer;
        [SerializeField] private GameObject _raceButtonPrefab;
        [SerializeField] private Image _racePortrait;
        [SerializeField] private TextMeshProUGUI _raceNameText;
        [SerializeField] private TextMeshProUGUI _raceDescriptionText;

        [Header("Class Selection")]
        [SerializeField] private CrawlerClassData[] _availableClasses;
        [SerializeField] private Transform _classButtonContainer;
        [SerializeField] private GameObject _classButtonPrefab;
        [SerializeField] private Image _classIcon;
        [SerializeField] private TextMeshProUGUI _classNameText;
        [SerializeField] private TextMeshProUGUI _classDescriptionText;

        [Header("Name Input")]
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private string _defaultName = "Carl";

        [Header("Confirm")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _confirmButtonText;

        [Header("Stat Preview")]
        [SerializeField] private TextMeshProUGUI _statPreviewText;

        public event Action<CrawlerClassData> OnClassSelected;
        public event Action<RaceData> OnRaceSelected;

        private RaceData _selectedRace;
        private CrawlerClassData _selectedClass;
        private readonly List<GameObject> _spawnedRaceButtons = new();
        private readonly List<GameObject> _spawnedClassButtons = new();

        private void Start()
        {
            PopulateRaces();
            PopulateClasses();
            UpdateConfirmButton();

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(HandleConfirm);

            if (_nameInput != null)
                _nameInput.text = _defaultName;
        }

        private void PopulateRaces()
        {
            if (_availableRaces == null || _raceButtonPrefab == null || _raceButtonContainer == null)
                return;

            foreach (var race in _availableRaces)
            {
                GameObject buttonObj = Instantiate(_raceButtonPrefab, _raceButtonContainer);
                _spawnedRaceButtons.Add(buttonObj);

                var label = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = race.RaceName;

                var icon = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null && race.Portrait != null)
                {
                    icon.sprite = race.Portrait;
                    icon.enabled = true;
                }

                var button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    var capturedRace = race;
                    button.onClick.AddListener(() => SelectRace(capturedRace));
                }
            }

            // Auto-select first race
            if (_availableRaces.Length > 0)
                SelectRace(_availableRaces[0]);
        }

        private void PopulateClasses()
        {
            if (_availableClasses == null || _classButtonPrefab == null || _classButtonContainer == null)
                return;

            foreach (var classData in _availableClasses)
            {
                GameObject buttonObj = Instantiate(_classButtonPrefab, _classButtonContainer);
                _spawnedClassButtons.Add(buttonObj);

                var label = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = classData.ClassName;

                var icon = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null && classData.ClassIcon != null)
                {
                    icon.sprite = classData.ClassIcon;
                    icon.enabled = true;
                }

                var button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    var capturedClass = classData;
                    button.onClick.AddListener(() => SelectClass(capturedClass));
                }
            }

            // Auto-select first class
            if (_availableClasses.Length > 0)
                SelectClass(_availableClasses[0]);
        }

        private void SelectRace(RaceData race)
        {
            _selectedRace = race;

            if (_raceNameText != null)
                _raceNameText.text = race.RaceName;

            if (_raceDescriptionText != null)
                _raceDescriptionText.text = race.Description;

            if (_racePortrait != null && race.Portrait != null)
            {
                _racePortrait.sprite = race.Portrait;
                _racePortrait.enabled = true;
            }

            OnRaceSelected?.Invoke(race);
            UpdateStatPreview();
            UpdateConfirmButton();
            RefreshAvailableClasses();
        }

        private void SelectClass(CrawlerClassData classData)
        {
            _selectedClass = classData;

            if (_classNameText != null)
                _classNameText.text = classData.ClassName;

            if (_classDescriptionText != null)
                _classDescriptionText.text = classData.Description;

            if (_classIcon != null && classData.ClassIcon != null)
            {
                _classIcon.sprite = classData.ClassIcon;
                _classIcon.enabled = true;
            }

            OnClassSelected?.Invoke(classData);
            UpdateStatPreview();
            UpdateConfirmButton();
        }

        private void RefreshAvailableClasses()
        {
            if (_selectedRace == null || _selectedRace.AvailableClassIds == null) return;

            var allowedIds = new HashSet<string>(_selectedRace.AvailableClassIds);

            for (int i = 0; i < _spawnedClassButtons.Count && i < _availableClasses.Length; i++)
            {
                bool allowed = allowedIds.Count == 0 || allowedIds.Contains(_availableClasses[i].ClassId);
                _spawnedClassButtons[i].SetActive(allowed);

                var button = _spawnedClassButtons[i].GetComponent<Button>();
                if (button != null)
                    button.interactable = allowed;
            }
        }

        private void UpdateStatPreview()
        {
            if (_statPreviewText == null) return;

            if (_selectedRace == null)
            {
                _statPreviewText.text = "Select a race to preview stats.";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Base Stats</b>");

            foreach (var entry in _selectedRace.BaseStats.BaseStats)
            {
                sb.AppendLine($"{entry.Type}: {entry.BaseValue:F0}");
            }

            if (_selectedRace.RacialPassives != null && _selectedRace.RacialPassives.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b>Racial Passives</b>");
                foreach (var passive in _selectedRace.RacialPassives)
                {
                    sb.AppendLine($"- {passive.PassiveName}: {passive.Description}");
                }
            }

            if (_selectedClass != null && _selectedClass.StartingAbilities != null && _selectedClass.StartingAbilities.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b>Starting Abilities</b>");
                foreach (var ability in _selectedClass.StartingAbilities)
                {
                    sb.AppendLine($"- {ability.AbilityName}");
                }
            }

            _statPreviewText.text = sb.ToString();
        }

        private void UpdateConfirmButton()
        {
            if (_confirmButton != null)
                _confirmButton.interactable = _selectedRace != null && _selectedClass != null;

            if (_confirmButtonText != null)
            {
                _confirmButtonText.text = _selectedRace != null && _selectedClass != null
                    ? "Enter the Dungeon"
                    : "Select Race & Class";
            }
        }

        private void HandleConfirm()
        {
            if (_selectedRace == null || _selectedClass == null) return;

            string playerName = _nameInput != null && !string.IsNullOrWhiteSpace(_nameInput.text)
                ? _nameInput.text.Trim()
                : _defaultName;

            if (ServiceLocator.TryGet<PlayerController>(out var player))
            {
                player.Initialize(playerName, _selectedClass, _selectedRace);
            }

            // Transition to gameplay
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.Stairwell);
                var sceneLoader = ServiceLocator.Get<SceneLoader>();
                sceneLoader?.LoadScene(Constants.SCENE_STAIRWELL);
            }
        }

        private void OnDestroy()
        {
            foreach (var obj in _spawnedRaceButtons)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedRaceButtons.Clear();

            foreach (var obj in _spawnedClassButtons)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedClassButtons.Clear();
        }
    }
}
