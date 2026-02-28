using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Centralized UI sound effects. Singleton with serialized AudioClip fields.
    /// </summary>
    public class UISfx : MonoBehaviour
    {
        public static UISfx Instance { get; private set; }

        [Header("Panel")]
        [SerializeField] private AudioClip _panelOpen;
        [SerializeField] private AudioClip _panelClose;

        [Header("Buttons")]
        [SerializeField] private AudioClip _buttonHover;
        [SerializeField] private AudioClip _buttonClick;
        [SerializeField] private AudioClip _buttonBack;

        [Header("Items")]
        [SerializeField] private AudioClip _itemPickup;
        [SerializeField] private AudioClip _itemEquip;
        [SerializeField] private AudioClip _itemDrop;

        [Header("Progression")]
        [SerializeField] private AudioClip _levelUp;
        [SerializeField] private AudioClip _skillUnlock;

        [Header("Feedback")]
        [SerializeField] private AudioClip _error;

        [Header("Loot Reveal")]
        [SerializeField] private AudioClip _lootRevealCommon;
        [SerializeField] private AudioClip _lootRevealRare;
        [SerializeField] private AudioClip _lootRevealLegendary;

        [Header("Settings")]
        [SerializeField] private float _hoverVolume = 0.5f;

        private AudioSource _audioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;
        }

        public void PlayPanelOpen() => Play(_panelOpen);
        public void PlayPanelClose() => Play(_panelClose);
        public void PlayHover() => Play(_buttonHover, _hoverVolume);
        public void PlayClick() => Play(_buttonClick);
        public void PlayBack() => Play(_buttonBack);
        public void PlayItemPickup() => Play(_itemPickup);
        public void PlayItemEquip() => Play(_itemEquip);
        public void PlayItemDrop() => Play(_itemDrop);
        public void PlayLevelUp() => Play(_levelUp);
        public void PlaySkillUnlock() => Play(_skillUnlock);
        public void PlayError() => Play(_error);

        public void PlayLootReveal(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Legendary:
                case ItemRarity.Absurd:
                    Play(_lootRevealLegendary);
                    break;
                case ItemRarity.Rare:
                case ItemRarity.Epic:
                    Play(_lootRevealRare);
                    break;
                default:
                    Play(_lootRevealCommon);
                    break;
            }
        }

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clip, volume);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
