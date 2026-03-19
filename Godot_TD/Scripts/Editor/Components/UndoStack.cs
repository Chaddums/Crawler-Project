using System;
using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// Generic undo/redo stack with snapshot-based state management.
    /// </summary>
    public class UndoStack<T> where T : class
    {
        private readonly List<T> _states = new();
        private int _index = -1;
        private readonly int _maxDepth;

        public event Action OnChanged;

        public bool CanUndo => _index > 0;
        public bool CanRedo => _index < _states.Count - 1;
        public int Count => _states.Count;

        public UndoStack(int maxDepth = 50)
        {
            _maxDepth = maxDepth;
        }

        public void Push(T state)
        {
            // Remove any redo states beyond current position
            if (_index < _states.Count - 1)
                _states.RemoveRange(_index + 1, _states.Count - _index - 1);

            _states.Add(state);
            _index = _states.Count - 1;

            // Trim oldest if over max depth
            if (_states.Count > _maxDepth)
            {
                _states.RemoveAt(0);
                _index--;
            }

            OnChanged?.Invoke();
        }

        public T Undo()
        {
            if (!CanUndo) return null;
            _index--;
            OnChanged?.Invoke();
            return _states[_index];
        }

        public T Redo()
        {
            if (!CanRedo) return null;
            _index++;
            OnChanged?.Invoke();
            return _states[_index];
        }

        public T Current => _index >= 0 && _index < _states.Count ? _states[_index] : null;

        public void Clear()
        {
            _states.Clear();
            _index = -1;
            OnChanged?.Invoke();
        }
    }
}
