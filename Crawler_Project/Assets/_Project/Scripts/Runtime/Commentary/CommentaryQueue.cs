using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCrawlerCarl
{
    public class CommentaryQueue
    {
        private SortedList<CommentaryPriority, Queue<CommentaryEntry>> _queues;

        public bool HasPending => _queues.Any(q => q.Value.Count > 0);
        public int TotalCount => _queues.Sum(q => q.Value.Count);

        public CommentaryQueue()
        {
            _queues = new SortedList<CommentaryPriority, Queue<CommentaryEntry>>(
                Comparer<CommentaryPriority>.Create((a, b) => b.CompareTo(a))
            );

            foreach (CommentaryPriority p in Enum.GetValues(typeof(CommentaryPriority)))
                _queues[p] = new Queue<CommentaryEntry>();
        }

        public void Enqueue(CommentaryEntry entry)
        {
            _queues[entry.Priority].Enqueue(entry);
        }

        public CommentaryEntry Dequeue()
        {
            foreach (var kvp in _queues)
            {
                if (kvp.Value.Count > 0)
                    return kvp.Value.Dequeue();
            }
            return null;
        }

        public CommentaryEntry Peek()
        {
            foreach (var kvp in _queues)
            {
                if (kvp.Value.Count > 0)
                    return kvp.Value.Peek();
            }
            return null;
        }

        public void Clear()
        {
            foreach (var kvp in _queues)
                kvp.Value.Clear();
        }
    }
}
