using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Captures engine and script errors/warnings through Godot's Logger API (4.5+).
    ///
    /// AutoPlayer reports had an "errors" array that nothing ever filled, so the QA gate that
    /// checks for <c>"errors": []</c> could never fail. Exceptions thrown inside engine callbacks
    /// (_Ready, _Process, signal handlers) are caught by the C# bridge and only printed — this is
    /// the one place they can be collected.
    ///
    /// Entries are de-duplicated by message + location and counted. May be called from any
    /// thread, so everything is under a lock and nothing here prints (printing would recurse).
    /// </summary>
    public partial class ErrorCaptureLogger : Logger
    {
        public static ErrorCaptureLogger Instance { get; private set; }

        private const int MaxDistinct = 200;
        private readonly object _lock = new();
        private readonly Dictionary<string, int> _errors = new();
        private readonly Dictionary<string, int> _warnings = new();
        private int _errorCount, _warningCount;

        /// <summary>Register with the engine once per process.</summary>
        public static ErrorCaptureLogger Install()
        {
            if (Instance != null) return Instance;
            Instance = new ErrorCaptureLogger();
            OS.AddLogger(Instance);
            return Instance;
        }

        /// <summary>Unregister before shutdown (a managed logger shouldn't outlive the runtime).</summary>
        public static void Uninstall()
        {
            if (Instance == null) return;
            OS.RemoveLogger(Instance);
            Instance = null;
        }

        public override void _LogError(string function, string file, int line, string code, string rationale,
            bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
        {
            string message = string.IsNullOrEmpty(rationale) ? code : rationale;
            string where = string.IsNullOrEmpty(file) ? function : $"{System.IO.Path.GetFileName(file)}:{line}";
            string key = $"{message} [{where}]";
            bool warning = errorType == (int)ErrorType.Warning;

            lock (_lock)
            {
                var bucket = warning ? _warnings : _errors;
                if (warning) _warningCount++; else _errorCount++;
                if (bucket.TryGetValue(key, out int n)) bucket[key] = n + 1;
                else if (bucket.Count < MaxDistinct) bucket[key] = 1;
            }
        }

        public override void _LogMessage(string message, bool error)
        {
            // Plain prints (GD.Print / GD.PrintErr) aren't engine errors; ignore.
        }

        public void Reset()
        {
            lock (_lock)
            {
                _errors.Clear(); _warnings.Clear();
                _errorCount = 0; _warningCount = 0;
            }
        }

        /// <summary>Distinct entries as "message [file:line] (xN)", most frequent first.</summary>
        public (List<string> errors, List<string> warnings, int errorCount, int warningCount) Snapshot()
        {
            lock (_lock)
            {
                return (Format(_errors), Format(_warnings), _errorCount, _warningCount);
            }
        }

        private static List<string> Format(Dictionary<string, int> bucket)
        {
            var list = new List<KeyValuePair<string, int>>(bucket);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var result = new List<string>(list.Count);
            foreach (var kv in list)
                result.Add(kv.Value > 1 ? $"{kv.Key} (x{kv.Value})" : kv.Key);
            return result;
        }
    }
}
