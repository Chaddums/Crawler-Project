using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Snapshot-based undo/redo stack. Stores JSON string snapshots of editor state.
    /// Each module maintains its own UndoStack instance.
    /// </summary>
    public class UndoStack
    {
        private readonly List<string> _snapshots = new();
        private int _current = -1;
        private const int MAX_UNDO = 50;

        /// <summary>True if there's a state to undo to.</summary>
        public bool CanUndo => _current > 0;

        /// <summary>True if there's a state to redo to.</summary>
        public bool CanRedo => _current < _snapshots.Count - 1;

        /// <summary>
        /// Push a new state snapshot. Clears any redo history.
        /// </summary>
        public void Push(string jsonSnapshot)
        {
            // Clear redo history
            if (_current < _snapshots.Count - 1)
                _snapshots.RemoveRange(_current + 1, _snapshots.Count - _current - 1);

            _snapshots.Add(jsonSnapshot);
            _current = _snapshots.Count - 1;

            // Cap history size
            if (_snapshots.Count > MAX_UNDO)
            {
                _snapshots.RemoveAt(0);
                _current--;
            }
        }

        /// <summary>
        /// Move back one state. Returns the snapshot to restore, or null if can't undo.
        /// </summary>
        public string Undo()
        {
            if (!CanUndo) return null;
            _current--;
            return _snapshots[_current];
        }

        /// <summary>
        /// Move forward one state. Returns the snapshot to restore, or null if can't redo.
        /// </summary>
        public string Redo()
        {
            if (!CanRedo) return null;
            _current++;
            return _snapshots[_current];
        }

        /// <summary>Clear all history.</summary>
        public void Clear()
        {
            _snapshots.Clear();
            _current = -1;
        }
    }
}
