using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The run's story, from Data/story.json, spoken through <see cref="Comms"/>: the premise on
    /// the first run, a planet's arrival the first time BIT lands there, the region's briefing and
    /// the site's line at the start of every run, beats at set waves (BIT's memory of earlier
    /// deployments by how many runs it has done, who the faction is, AXIS's quarterly
    /// indifference), a site secured, a shield wall down, and the run's end.
    ///
    /// Pools are used one line a run, in order across runs (the run count picks the line), so a
    /// player hears them all before any repeats.
    /// </summary>
    public partial class StoryDirector : Node
    {
        public class Beat
        {
            public int Wave { get; set; }
            /// <summary>When the wave is sent (else when it is cleared).</summary>
            public bool Start { get; set; }
            /// <summary>Only on the first this many runs (0: always).</summary>
            public int FirstRuns { get; set; }
            public string Pool { get; set; }
            public List<string[]> Lines { get; set; }
        }

        public class StoryData
        {
            public List<string[]> Premise { get; set; } = new();
            public Dictionary<string, List<string[]>> Arrival { get; set; } = new();
            public Dictionary<string, List<string[]>> Regions { get; set; } = new();
            public Dictionary<string, string[]> Sites { get; set; } = new();
            public List<Beat> Beats { get; set; } = new();
            public Dictionary<string, List<string[]>> Pools { get; set; } = new();
            public Dictionary<string, List<string[]>> Memory { get; set; } = new();
            public List<List<string[]>> Secured { get; set; } = new();
            public List<List<string[]>> WallDown { get; set; } = new();
            public Dictionary<string, List<string[]>> RunEnd { get; set; } = new();
        }

        private static StoryData _data;
        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static StoryData Data
        {
            get
            {
                if (_data != null) return _data;
                const string path = "res://Data/story.json";
                if (Godot.FileAccess.FileExists(path))
                {
                    using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                    try { _data = JsonSerializer.Deserialize<StoryData>(f.GetAsText(), _json); }
                    catch (JsonException e) { GD.PushError($"[Story] {path}: {e.Message}"); }
                }
                return _data ??= new StoryData();
            }
        }

        private int _runCount, _wallsDown;
        private bool _briefed, _securedSaid, _endSaid;

        /// <summary>Beats spoken this run (tests).</summary>
        public static int BeatsSpoken { get; private set; }

        public override void _Ready()
        {
            BeatsSpoken = 0;
            _runCount = GameManager.Instance?.MetaSave?.RunCount ?? 0;
            GameEvents.OnPhaseChanged += OnPhase;
            GameEvents.OnWaveStarted += OnWaveStarted;
            GameEvents.OnWaveCompleted += OnWaveCleared;
            GameEvents.OnShieldWallDestroyed += OnWallDown;
            GameEvents.OnCoreDestroyed += OnRunEnd;
            ServiceLocator.Register(this);
        }

        public override void _ExitTree()
        {
            GameEvents.OnPhaseChanged -= OnPhase;
            GameEvents.OnWaveStarted -= OnWaveStarted;
            GameEvents.OnWaveCompleted -= OnWaveCleared;
            GameEvents.OnShieldWallDestroyed -= OnWallDown;
            GameEvents.OnCoreDestroyed -= OnRunEnd;
            if (ServiceLocator.TryGet<StoryDirector>(out var s) && s == this) ServiceLocator.Unregister<StoryDirector>();
        }

        public override void _Process(double delta)
        {
            var gm = GameManager.Instance;
            if (!_securedSaid && gm != null && gm.SiteSecuredThisRun)
            {
                _securedSaid = true;
                SayAll(Pick(Data.Secured, _runCount));
            }
        }

        private void OnPhase(GamePhase phase)
        {
            if (phase != GamePhase.Build || _briefed || (GameManager.Instance?.CurrentWave ?? 0) > 0) return;
            _briefed = true;
            Brief();
        }

        /// <summary>The start of a run: the premise (first run), the planet (first landing), the region and the site.</summary>
        internal void Brief()
        {
            var gm = GameManager.Instance;
            int planet = gm?.CurrentPlanet ?? 1;
            string siteId = gm?.CurrentTerritorySectionId;
            if (_runCount == 0) SayAll(Data.Premise);
            if (gm?.MetaSave != null && TerritoryManager.GetPlanetProgress(planet, gm.MetaSave).cleared == 0
                && Data.Arrival.TryGetValue(planet.ToString(), out var arrival))
                SayAll(arrival);
            var region = string.IsNullOrEmpty(siteId) ? null : TerritoryManager.GetRegionForSite(siteId);
            if (region != null && Data.Regions.TryGetValue(region.Id, out var brief)) SayAll(brief);
            if (!string.IsNullOrEmpty(siteId) && Data.Sites.TryGetValue(siteId, out var line)) Say(line);
        }

        private void OnWaveStarted(int wave) => RunBeats(wave, start: true);
        private void OnWaveCleared(int wave) => RunBeats(wave, start: false);

        private void RunBeats(int wave, bool start)
        {
            foreach (var b in Data.Beats)
            {
                if (b.Wave != wave || b.Start != start) continue;
                if (b.FirstRuns > 0 && _runCount >= b.FirstRuns) continue;
                BeatsSpoken++;
                if (b.Lines != null) SayAll(b.Lines);
                if (!string.IsNullOrEmpty(b.Pool) && PoolLine(b.Pool, wave) is { } l) Say(l);
            }
        }

        /// <summary>One line of a pool for this run: memory by how many runs BIT has done, the faction by planet.</summary>
        private string[] PoolLine(string pool, int wave)
        {
            int planet = GameManager.Instance?.CurrentPlanet ?? 1;
            if (pool == "memory")
            {
                string tier = _runCount >= 15 ? "final" : _runCount >= 10 ? "late" : _runCount >= 5 ? "mid" : _runCount >= 1 ? "early" : null;
                if (tier == null || !Data.Memory.TryGetValue(tier, out var mem)) return null;
                // Two memory beats a run (waves 3 and 15 and on), different lines
                return Pick(mem, _runCount * 2 + (wave >= 15 ? 1 : 0));
            }
            if (pool == "faction")
                return Data.Pools.TryGetValue($"faction_{planet}", out var fac) ? Pick(fac, _runCount * 2 + (wave >= 30 ? 1 : 0)) : null;
            return Data.Pools.TryGetValue(pool, out var lines) ? Pick(lines, _runCount) : null;
        }

        private void OnWallDown(CardinalDirection dir)
        {
            var lines = Pick(Data.WallDown, _runCount + _wallsDown++);
            if (lines == null) return;
            foreach (var l in lines) Say(l, ("{dir}", dir.ToString().ToLowerInvariant()));
        }

        private void OnRunEnd()
        {
            if (_endSaid) return;
            _endSaid = true;
            if (Data.RunEnd.TryGetValue(_runCount >= 10 ? "late" : "early", out var bit)) Say(Pick(bit, _runCount));
            if (Data.RunEnd.TryGetValue("axis", out var axis)) Say(Pick(axis, _runCount));
        }

        private static T Pick<T>(List<T> list, int n) where T : class
            => list == null || list.Count == 0 ? null : list[((n % list.Count) + list.Count) % list.Count];

        private void SayAll(List<string[]> lines)
        {
            if (lines == null) return;
            foreach (var l in lines) Say(l);
        }

        private void Say(string[] line, params (string key, string value)[] extra)
        {
            if (line == null || line.Length < 2) return;
            Comms.Say(line[0] == Comms.Bit ? Comms.Bit : Comms.Axis, Fill(line[1], extra), story: true);
        }

        /// <summary>{wave}, {deployment}, {extracted}, {site}, {region}, {planet}, {clear} and any extras.</summary>
        internal string Fill(string text, params (string key, string value)[] extra)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            var gm = GameManager.Instance;
            string siteId = gm?.CurrentTerritorySectionId;
            var site = string.IsNullOrEmpty(siteId) ? null : TerritoryManager.GetSite(siteId);
            var region = string.IsNullOrEmpty(siteId) ? null : TerritoryManager.GetRegionForSite(siteId);
            text = text.Replace("{wave}", (gm?.CurrentWave ?? 0).ToString())
                .Replace("{deployment}", Deployment(_runCount).ToString())
                .Replace("{extracted}", (gm?.TotalExtracted ?? 0).ToString("N0"))
                .Replace("{site}", site?.Name ?? "the site")
                .Replace("{region}", region?.Name ?? "the region")
                .Replace("{planet}", (gm?.CurrentPlanet ?? 1) == 2 ? "the Scrapyard" : "Grid Prime")
                .Replace("{clear}", (site?.ClearWave ?? Constants.TERRITORY_DEFAULT_CLEAR_WAVE).ToString());
            foreach (var (k, v) in extra) text = text.Replace(k, v);
            return text;
        }

        /// <summary>BIT's deployment number: it has done hundreds before this player's first.</summary>
        public static int Deployment(int runCount) => 834 + runCount;
    }
}
