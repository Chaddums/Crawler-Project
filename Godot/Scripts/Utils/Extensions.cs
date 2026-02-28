using Godot;

namespace DungeonCrawlerCarl
{
    public static class Extensions
    {
        /// <summary>
        /// Returns the vector with Y zeroed out (flat on XZ plane).
        /// </summary>
        public static Vector3 Flat(this Vector3 v)
        {
            return new Vector3(v.X, 0f, v.Z);
        }

        /// <summary>
        /// Distance between two points ignoring Y axis.
        /// </summary>
        public static float FlatDistance(this Vector3 a, Vector3 b)
        {
            var diff = a - b;
            diff.Y = 0;
            return diff.Length();
        }

        /// <summary>
        /// Remap a value from one range to another.
        /// </summary>
        public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }
    }
}
