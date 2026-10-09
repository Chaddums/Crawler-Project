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

        public static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;

        public static void Stop(string key, long start)
        {
            if (!Enabled || start == 0) return;
            long d = Stopwatch.GetTimestamp() - start;
            _ticks[key] = _ticks.GetValueOrDefault(key) + d;
            _calls[key] = _calls.GetValueOrDefault(key) + 1;
        }

        public static void Reset() { _ticks.Clear(); _calls.Clear(); }

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
