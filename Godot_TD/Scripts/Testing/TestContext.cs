using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    public class TestResult
    {
        public string Name { get; set; }
        public bool Passed { get; set; }
        public long DurationMs { get; set; }
        public string Message { get; set; } = "";
        public string Screenshot { get; set; }
        public Dictionary<string, object> VisualData { get; set; }
    }

    public class TestReport
    {
        public string RequestId { get; set; }
        public string Suite { get; set; }
        public string Timestamp { get; set; }
        public double DurationSeconds { get; set; }
        public string BuildHash { get; set; }
        public List<TestResult> Tests { get; set; } = new();
        public TestSummary Summary { get; set; } = new();
    }

    public class TestSummary
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
    }

    public class TestContext
    {
        private readonly SceneTree _tree;
        private readonly List<TestResult> _results = new();
        private readonly Dictionary<string, int> _eventCounts = new();
        private readonly Stopwatch _suiteTimer = new();
        private Stopwatch _testTimer;
        private bool _tracking;
        private int _eventsVersion;

        public SceneTree Tree => _tree;
        public List<TestResult> Results => _results;

        public TestContext(SceneTree tree)
        {
            _tree = tree;
            _suiteTimer.Start();
        }

        // ── Assertions ──

        public void Assert(bool condition, string testName, string msg = "")
        {
            var elapsed = _testTimer?.ElapsedMilliseconds ?? 0;
            _results.Add(new TestResult
            {
                Name = testName,
                Passed = condition,
                DurationMs = elapsed,
                Message = condition ? msg : (string.IsNullOrEmpty(msg) ? "Assertion failed" : msg)
            });
            if (condition)
                GD.Print($"  [PASS] {testName}");
            else
                GD.PrintErr($"  [FAIL] {testName}: {msg}");
        }

        public void AssertEqual<T>(T expected, T actual, string testName, string context = "")
        {
            bool passed = EqualityComparer<T>.Default.Equals(expected, actual);
            string msg = passed ? "" : $"Expected {expected}, got {actual}";
            if (!string.IsNullOrEmpty(context)) msg = $"{context}: {msg}";
            Assert(passed, testName, msg);
        }

        public void AssertGreater(float actual, float threshold, string testName, string msg = "")
        {
            bool passed = actual > threshold;
            Assert(passed, testName,
                passed ? "" : $"{msg} Expected > {threshold}, got {actual}");
        }

        public void AssertGreaterEqual(int actual, int threshold, string testName, string msg = "")
        {
            bool passed = actual >= threshold;
            Assert(passed, testName,
                passed ? "" : $"{msg} Expected >= {threshold}, got {actual}");
        }

        public void AssertNotNull(object obj, string testName, string msg = "")
        {
            Assert(obj != null, testName,
                obj != null ? "" : $"Object was null. {msg}");
        }

        public void AssertInRange(float value, float min, float max, string testName, string msg = "")
        {
            bool passed = value >= min && value <= max;
            Assert(passed, testName,
                passed ? "" : $"{msg} Expected [{min}, {max}], got {value}");
        }

        public void AssertColorMatch(Color actual, Color expected, float tolerance, string testName)
        {
            float dr = Mathf.Abs(actual.R - expected.R);
            float dg = Mathf.Abs(actual.G - expected.G);
            float db = Mathf.Abs(actual.B - expected.B);
            float maxDev = Mathf.Max(dr, Mathf.Max(dg, db));
            bool passed = maxDev <= tolerance;
            Assert(passed, testName,
                passed
                    ? $"Color match within {maxDev:F3}"
                    : $"Color mismatch: actual ({actual.R:F2},{actual.G:F2},{actual.B:F2}) vs expected ({expected.R:F2},{expected.G:F2},{expected.B:F2}), max deviation {maxDev:F3} > tolerance {tolerance}");
        }

        // ── Timing ──

        public void StartTest()
        {
            _testTimer = Stopwatch.StartNew();
        }

        // ── Async helpers ──

        public async Task Wait(float seconds)
        {
            await _tree.ToSignal(_tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        }

        public async Task<bool> WaitForPhase(GamePhase target, float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (GameManager.Instance?.CurrentPhase == target)
                    return true;
                await Wait(0.1f);
                elapsed += 0.1f;
            }
            return GameManager.Instance?.CurrentPhase == target;
        }

        public async Task<bool> WaitForEvent(string eventName, float timeout)
        {
            int startCount = GetEventCount(eventName);
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (GetEventCount(eventName) > startCount)
                    return true;
                await Wait(0.1f);
                elapsed += 0.1f;
            }
            return GetEventCount(eventName) > startCount;
        }

        public async Task<bool> WaitUntil(Func<bool> predicate, float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (predicate()) return true;
                await Wait(0.1f);
                elapsed += 0.1f;
            }
            return predicate();
        }

        // ── Event tracking ──

        public void BeginEventTracking()
        {
            _tracking = true;
            _eventCounts.Clear();
            _eventsVersion = GameEvents.Version;

            GameEvents.OnSignalFired += (n, t) => TrackEvent("OnSignalFired");
            GameEvents.OnSignalReceived += (n, t) => TrackEvent("OnSignalReceived");
            GameEvents.OnGateStateChanged += (n, o) => TrackEvent("OnGateStateChanged");
            GameEvents.OnSwitchToggled += (n, i) => TrackEvent("OnSwitchToggled");
            GameEvents.OnVineNodePlaced += n => TrackEvent("OnVineNodePlaced");
            GameEvents.OnVineNodeSold += n => TrackEvent("OnVineNodeSold");
            GameEvents.OnWaveStarted += w => TrackEvent("OnWaveStarted");
            GameEvents.OnWaveCompleted += w => TrackEvent("OnWaveCompleted");
            GameEvents.OnEnemyKilled += n => TrackEvent("OnEnemyKilled");
            GameEvents.OnEnemyLeaked += (n, p) => TrackEvent("OnEnemyLeaked");
            GameEvents.OnScrapChanged += s => TrackEvent("OnScrapChanged");
            GameEvents.OnCoreLivesChanged += l => TrackEvent("OnCoreLivesChanged");
            GameEvents.OnPhaseChanged += p => TrackEvent("OnPhaseChanged");
            GameEvents.OnCoreDestroyed += () => TrackEvent("OnCoreDestroyed");
            GameEvents.OnAllWavesCleared += w => TrackEvent("OnAllWavesCleared");
            GameEvents.OnVinePathRecalculated += () => TrackEvent("OnVinePathRecalculated");
        }

        private void TrackEvent(string name)
        {
            if (!_tracking) return;
            if (GameEvents.Version != _eventsVersion)
            {
                _tracking = false;
                return;
            }
            _eventCounts.TryGetValue(name, out int count);
            _eventCounts[name] = count + 1;
        }

        public int GetEventCount(string name)
        {
            return _eventCounts.TryGetValue(name, out int count) ? count : 0;
        }

        public void ResetEventCounts()
        {
            _eventCounts.Clear();
        }

        // ── Screenshot capture ──

        public string CaptureScreenshot(string name)
        {
            try
            {
                var viewport = _tree.Root;
                var image = viewport.GetTexture().GetImage();
                string dir = "test-reports/screenshots";
                DirAccess.MakeDirRecursiveAbsolute($"res://{dir}");
                string path = $"{dir}/{name}.png";
                image.SavePng($"res://{path}");
                GD.Print($"  [SCREENSHOT] {path}");
                return path;
            }
            catch (Exception e)
            {
                GD.PrintErr($"  [SCREENSHOT FAILED] {name}: {e.Message}");
                return null;
            }
        }

        public string CaptureVisualScreenshot(string name)
        {
            try
            {
                var viewport = _tree.Root;
                var image = viewport.GetTexture().GetImage();
                string dir = "test-reports/visual/current";
                DirAccess.MakeDirRecursiveAbsolute($"res://{dir}");
                string path = $"{dir}/{name}.png";
                image.SavePng($"res://{path}");
                GD.Print($"  [VISUAL CAPTURE] {path}");
                return path;
            }
            catch (Exception e)
            {
                GD.PrintErr($"  [VISUAL CAPTURE FAILED] {name}: {e.Message}");
                return null;
            }
        }

        // ── Material sampling ──

        public (Color albedo, Color emission, float emissionEnergy, bool emissionEnabled) SampleMaterial(MeshInstance3D mesh)
        {
            if (mesh?.MaterialOverride is StandardMaterial3D mat)
            {
                return (mat.AlbedoColor, mat.Emission, mat.EmissionEnergyMultiplier, mat.EmissionEnabled);
            }
            return (Colors.Magenta, Colors.Black, 0f, false);
        }

        // ── Camera control ──

        public void LockCamera(Vector3 target, float zoom)
        {
            var camera = ServiceLocator.TryGet<TDCamera>(out var cam) ? cam : null;
            if (camera != null)
            {
                camera.SetMapBounds(
                    Constants.VINE_MAP_WIDTH * Constants.VINE_CELL_SIZE,
                    Constants.VINE_MAP_HEIGHT * Constants.VINE_CELL_SIZE);
                // Use reflection or public API to set position
                camera.Position = target + new Vector3(0, zoom, zoom * 0.5f);
                camera.LookAt(target, Vector3.Up);
            }
        }

        // ── Find nodes in tree ──

        public T FindNode<T>(string name = null) where T : Node
        {
            return FindNodeInTree<T>(_tree.Root, name);
        }

        private T FindNodeInTree<T>(Node root, string name) where T : Node
        {
            if (root is T match && (name == null || root.Name == name))
                return match;
            foreach (var child in root.GetChildren())
            {
                var found = FindNodeInTree<T>(child as Node, name);
                if (found != null) return found;
            }
            return null;
        }

        public List<T> FindNodes<T>(Node root = null) where T : Node
        {
            var results = new List<T>();
            CollectNodes(root ?? _tree.Root, results);
            return results;
        }

        private void CollectNodes<T>(Node node, List<T> results) where T : Node
        {
            if (node is T match) results.Add(match);
            foreach (var child in node.GetChildren())
                CollectNodes(child as Node, results);
        }

        // ── Result writing ──

        public void WriteResults(string requestId, string suiteName)
        {
            _suiteTimer.Stop();

            string buildHash = "unknown";
            try
            {
                var output = new List<string>();
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "git";
                process.StartInfo.Arguments = "rev-parse --short HEAD";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                if (process.Start())
                {
                    buildHash = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(3000);
                }
            }
            catch { }

            int passed = 0, failed = 0;
            foreach (var r in _results)
            {
                if (r.Passed) passed++;
                else failed++;
            }

            var report = new TestReport
            {
                RequestId = requestId,
                Suite = suiteName,
                Timestamp = DateTime.UtcNow.ToString("o"),
                DurationSeconds = _suiteTimer.Elapsed.TotalSeconds,
                BuildHash = buildHash,
                Tests = _results,
                Summary = new TestSummary
                {
                    Total = _results.Count,
                    Passed = passed,
                    Failed = failed
                }
            };

            string dir = ProjectSettings.GlobalizePath("res://test-reports/results");
            DirAccess.MakeDirRecursiveAbsolute(dir);
            string path = $"{dir}/{requestId}.json";

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            string json = JsonSerializer.Serialize(report, options);
            System.IO.File.WriteAllText(path, json);

            GD.Print($"\n[TestHarness] Results written to {path}");
            GD.Print($"[TestHarness] {passed}/{_results.Count} passed, {failed} failed ({_suiteTimer.Elapsed.TotalSeconds:F1}s)");
        }
    }
}
