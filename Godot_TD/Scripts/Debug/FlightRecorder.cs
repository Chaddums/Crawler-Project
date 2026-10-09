using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A crash trail for runs that end with nothing in the log (the Send All crash on the dev PC).
    /// Once a second it rewrites user://logs/flight.log with the last couple of minutes of
    /// snapshots (fps, phase, wave, stacked waves, enemies, objects, memory) and recent events,
    /// closing the file each time so it survives a hard crash. A trail left without its closing
    /// line is kept as flight_crash.log when the next battle starts.
    /// </summary>
    public partial class FlightRecorder : Node
    {
        public static FlightRecorder Current { get; private set; }
        private const string PATH = "user://logs/flight.log";
        private const string CRASH_PATH = "user://logs/flight_crash.log";
        private const string CLEAN = "END clean";
        private readonly Queue<string> _snaps = new();
        private readonly Queue<string> _events = new();
        private float _tick, _clock;
        private int _spawnsThisSecond, _killsThisSecond;

        /// <summary>Add a line to the event trail.</summary>
        public static void Note(string line) => Current?.AddEvent(line);
        public static void CountSpawn() { if (Current != null) Current._spawnsThisSecond++; }

        public override void _Ready()
        {
            Current = this;
            ProcessMode = ProcessModeEnum.Always;
            DirAccess.MakeDirRecursiveAbsolute("user://logs");
            // The last battle never wrote its closing line: keep its trail
            if (FileAccess.FileExists(PATH))
            {
                var prev = FileAccess.GetFileAsString(PATH);
                if (!string.IsNullOrEmpty(prev) && !prev.TrimEnd().EndsWith(CLEAN))
                {
                    using var f = FileAccess.Open(CRASH_PATH, FileAccess.ModeFlags.Write);
                    f?.StoreString(prev);
                    GD.Print($"[FlightRecorder] The last battle ended without closing its trail; kept as {CRASH_PATH}");
                }
            }
            GameEvents.OnWaveStarted += OnWaveStarted;
            GameEvents.OnPhaseChanged += OnPhase;
            GameEvents.OnEnemyKilled += OnKill;
            GameEvents.OnTowerPlaced += OnTower;
            GameEvents.OnAnnouncement += OnAnnounce;
            AddEvent($"battle P{GameManager.Instance?.CurrentPlanet} {GameManager.Instance?.CurrentTerritorySectionId} role {GameManager.Instance?.SelectedRole}");
        }

        public override void _ExitTree()
        {
            GameEvents.OnWaveStarted -= OnWaveStarted;
            GameEvents.OnPhaseChanged -= OnPhase;
            GameEvents.OnEnemyKilled -= OnKill;
            GameEvents.OnTowerPlaced -= OnTower;
            GameEvents.OnAnnouncement -= OnAnnounce;
            Write(true);
            if (Current == this) Current = null;
        }

        private void OnWaveStarted(int w) => AddEvent($"wave {w} started");
        private void OnPhase(GamePhase p) => AddEvent($"phase {p}");
        private void OnKill(Node n) => _killsThisSecond++;
        private void OnTower(Node n) => AddEvent($"placed {(n as VineNode)?.Data?.Type}");
        private void OnAnnounce(string s) => AddEvent($"announce {s}");

        private void AddEvent(string line)
        {
            _events.Enqueue($"{_clock,7:F1}s {line}");
            while (_events.Count > 60) _events.Dequeue();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _clock += dt;
            _tick += dt;
            if (_tick < 1f) return;
            _tick = 0f;
            var gm = GameManager.Instance;
            ServiceLocator.TryGet<VineWaveManager>(out var wm);
            int enemies = GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY).Count;
            _snaps.Enqueue($"{_clock,7:F1}s fps {Engine.GetFramesPerSecond(),3:F0} x{Engine.TimeScale:F0} {gm?.CurrentPhase} W{wm?.CurrentWave} " +
                $"stacked {wm?.PendingStackedWaves} enemies {enemies} +{_spawnsThisSecond} -{_killsThisSecond} " +
                $"objects {Performance.GetMonitor(Performance.Monitor.ObjectCount):F0} nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):F0} " +
                $"mem {Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576:F0}MB vram {Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:F0}MB");
            while (_snaps.Count > 120) _snaps.Dequeue();
            _spawnsThisSecond = _killsThisSecond = 0;
            Write(false);
        }

        private void Write(bool clean)
        {
            using var f = FileAccess.Open(PATH, FileAccess.ModeFlags.Write);
            if (f == null) return;
            f.StoreString("Snapshots (one a second)\n" + string.Join("\n", _snaps) + "\n\nEvents\n" + string.Join("\n", _events) + "\n");
            if (clean) f.StoreString(CLEAN + "\n");
        }
    }
}
