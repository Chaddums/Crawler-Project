using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewCommentaryPack", menuName = "DCC/Commentary Pack")]
    public class CommentaryPackData : ScriptableObject
    {
        public string PackName;
        public CommentaryCategory Category;
        public List<CommentaryEntry> Entries;

        public CommentaryEntry GetRandom()
        {
            if (Entries == null || Entries.Count == 0) return null;
            return Entries[Random.Range(0, Entries.Count)];
        }

        public CommentaryEntry GetRandom(string speaker)
        {
            if (Entries == null || Entries.Count == 0) return null;

            var filtered = Entries.FindAll(e => e.Speaker == speaker);
            if (filtered.Count == 0) return GetRandom();

            return filtered[Random.Range(0, filtered.Count)];
        }
    }
}
