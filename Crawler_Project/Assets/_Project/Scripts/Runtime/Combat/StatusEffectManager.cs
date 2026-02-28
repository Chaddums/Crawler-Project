using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class StatusEffectManager : MonoBehaviour
    {
        private List<StatusEffect> _activeEffects = new();
        private StatBlock _stats;

        public void SetStats(StatBlock stats)
        {
            _stats = stats;
        }

        private void Update()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                _activeEffects[i].Tick(Time.deltaTime);

                if (_activeEffects[i].IsExpired)
                {
                    RemoveEffect(i);
                }
            }
        }

        public void ApplyEffect(StatusEffectData data, GameObject source)
        {
            if (!data.Stackable)
            {
                var existing = _activeEffects.Find(e => e.Data == data);
                if (existing != null)
                {
                    existing.RemainingDuration = data.Duration;
                    return;
                }
            }
            else
            {
                int currentStacks = _activeEffects.FindAll(e => e.Data == data).Count;
                if (data.MaxStacks > 0 && currentStacks >= data.MaxStacks)
                    return;
            }

            var effect = new StatusEffect(data, source, gameObject);
            _activeEffects.Add(effect);

            if (_stats != null)
                effect.OnApply(_stats);

            if (data.VisualEffectPrefab != null)
                Instantiate(data.VisualEffectPrefab, transform);
        }

        public void RemoveEffectsOfType(StatusEffectData data)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i].Data == data)
                    RemoveEffect(i);
            }
        }

        public void ClearAllEffects()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
                RemoveEffect(i);
        }

        private void RemoveEffect(int index)
        {
            var effect = _activeEffects[index];
            if (_stats != null)
                effect.OnRemove(_stats);
            _activeEffects.RemoveAt(index);
        }

        public bool HasEffect(StatusEffectData data)
        {
            return _activeEffects.Exists(e => e.Data == data);
        }

        public int GetStackCount(StatusEffectData data)
        {
            return _activeEffects.FindAll(e => e.Data == data).Count;
        }
    }
}
