using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JunkyardTD
{
    /// <summary>
    /// Wall time spent in the hot per-frame updates, by name. Off unless a test (the siege bench)
    /// switches it on; when off a sample is one bool check. Usage:
    /// <c>long t = FrameProfiler.Start(); ...; FrameProfiler.Stop("enemies", t);</c>
    /// </summary>
    public static class FrameProfiler
    {
        public static bool Enabled;
        private static readonly Dictionary<string, long> _ticks = new();
        private static readonly Dictionary<string, int> _calls = new();
        // Bytes the main thread allocated inside each key (nested keys count in their parents too)
        private static readonly Dictionary<string, long> _bytes = new();
        private static readonly long[] _allocAt = new long[64];
        private static int _depth;

        public static long Start()
        {
            if (!Enabled) return 0;
            if (_depth < _allocAt.Length) _allocAt[_depth] = System.GC.GetAllocatedBytesForCurrentThread();
            _depth++;
            return Stopwatch.GetTimestamp();
        }

        public static void Stop(string key, long start)
        {
            if (!Enabled || start == 0) return;
            long d = Stopwatch.GetTimestamp() - start;
            _ticks[key] = _ticks.GetValueOrDefault(key) + d;
            _calls[key] = _calls.GetValueOrDefault(key) + 1;
            if (_depth > 0 && --_depth < _allocAt.Length)
                _bytes[key] = _bytes.GetValueOrDefault(key) + (System.GC.GetAllocatedBytesForCurrentThread() - _allocAt[_depth]);
        }

        public static void Reset() { _ticks.Clear(); _calls.Clear(); _bytes.Clear(); _depth = 0; }

        /// <summary>The ticks so far by key, copied into <paramref name="into"/> (benches diff two of them to see what a slow frame spent).</summary>
        public static void Snapshot(Dictionary<string, long> into)
        {
            into.Clear();
            foreach (var kv in _ticks) into[kv.Key] = kv.Value;
        }

        /// <summary>Bytes allocated inside each key so far, biggest first.</summary>
        public static List<(string key, long bytes)> Allocated()
            => _bytes.Select(kv => (kv.Key, kv.Value)).OrderByDescending(x => x.Value).ToList();

        public static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        /// <summary>Milliseconds a frame per key over <paramref name="frames"/> frames, biggest first.</summary>
        public static List<(string key, double msPerFrame, int callsPerFrame)> Report(int frames)
        {
            frames = System.Math.Max(1, frames);
            return _ticks.Keys
                .Select(k => (k, _ticks[k] * 1000.0 / Stopwatch.Frequency / frames, _calls[k] / frames))
                .OrderByDescending(r => r.Item2)
                .ToList();
        }
    }
}
