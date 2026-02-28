using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _speakerText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Typewriter Settings")]
        [SerializeField] private float _charsPerSecond = 30f;
        [SerializeField] private float _displayDuration = 4f;
        [SerializeField] private float _fadeDuration = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource _voiceSource;

        private readonly Queue<QueuedMessage> _messageQueue = new();
        private Coroutine _displayCoroutine;
        private bool _isDisplaying;

        private struct QueuedMessage
        {
            public string Speaker;
            public string Text;
            public AudioClip VoiceClip;
        }

        public void ShowMessage(string text)
        {
            ShowMessage(string.Empty, text);
        }

        public void ShowMessage(string speaker, string text)
        {
            var message = new QueuedMessage { Speaker = speaker, Text = text };

            if (_isDisplaying)
            {
                _messageQueue.Enqueue(message);
                return;
            }

            DisplayMessage(message);
        }

        public void ShowCommentary(CommentaryEntry entry)
        {
            var message = new QueuedMessage
            {
                Speaker = entry.Speaker,
                Text = entry.Text,
                VoiceClip = entry.VoiceClip
            };

            if (_isDisplaying)
            {
                _messageQueue.Enqueue(message);
                return;
            }

            DisplayMessage(message);
        }

        public void ShowAnnouncement(string announcement)
        {
            ShowMessage("SYSTEM", announcement);
        }

        private void DisplayMessage(QueuedMessage message)
        {
            if (_displayCoroutine != null)
                StopCoroutine(_displayCoroutine);

            _displayCoroutine = StartCoroutine(DisplayMessageCoroutine(message));
        }

        private IEnumerator DisplayMessageCoroutine(QueuedMessage message)
        {
            _isDisplaying = true;

            // Set speaker name
            if (_speakerText != null)
            {
                _speakerText.text = string.IsNullOrEmpty(message.Speaker) ? string.Empty : message.Speaker;
                _speakerText.gameObject.SetActive(!string.IsNullOrEmpty(message.Speaker));
            }

            // Fade in
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = true;
                float fadeInElapsed = 0f;
                while (fadeInElapsed < _fadeDuration)
                {
                    fadeInElapsed += Time.deltaTime;
                    _canvasGroup.alpha = Mathf.Clamp01(fadeInElapsed / _fadeDuration);
                    yield return null;
                }
                _canvasGroup.alpha = 1f;
            }

            // Play voice clip if available
            if (message.VoiceClip != null && _voiceSource != null)
            {
                _voiceSource.clip = message.VoiceClip;
                _voiceSource.Play();
            }

            // Typewriter effect
            if (_messageText != null)
            {
                _messageText.text = string.Empty;
                float charInterval = 1f / _charsPerSecond;
                float elapsed = 0f;
                int charsDisplayed = 0;

                while (charsDisplayed < message.Text.Length)
                {
                    elapsed += Time.deltaTime;
                    int targetChars = Mathf.Min(Mathf.FloorToInt(elapsed / charInterval), message.Text.Length);

                    if (targetChars > charsDisplayed)
                    {
                        charsDisplayed = targetChars;
                        _messageText.text = message.Text.Substring(0, charsDisplayed);
                    }

                    yield return null;
                }

                _messageText.text = message.Text;
            }

            // Hold display
            yield return new WaitForSeconds(_displayDuration);

            // Fade out
            if (_canvasGroup != null)
            {
                float fadeOutElapsed = 0f;
                while (fadeOutElapsed < _fadeDuration)
                {
                    fadeOutElapsed += Time.deltaTime;
                    _canvasGroup.alpha = 1f - Mathf.Clamp01(fadeOutElapsed / _fadeDuration);
                    yield return null;
                }
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            _isDisplaying = false;

            // Process next message in queue
            if (_messageQueue.Count > 0)
            {
                DisplayMessage(_messageQueue.Dequeue());
            }
        }

        public void ClearQueue()
        {
            _messageQueue.Clear();

            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                _displayCoroutine = null;
            }

            _isDisplaying = false;

            if (_voiceSource != null && _voiceSource.isPlaying)
                _voiceSource.Stop();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_messageText != null)
                _messageText.text = string.Empty;

            if (_speakerText != null)
                _speakerText.text = string.Empty;
        }

        private void OnDisable()
        {
            ClearQueue();
        }
    }
}
