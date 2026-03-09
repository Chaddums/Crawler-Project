using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Base class for all editor module panels.
    /// Provides standard layout: toolbar at top, content area below.
    /// Handles dirty state, undo/redo, and save/load lifecycle.
    /// </summary>
    public abstract partial class EditorPanel : VBoxContainer
    {
        public event Action OnDirtyChanged;

        protected UndoStack _undo = new();
        protected bool _dirty;
        protected string _dataPath;

        private HBoxContainer _toolbar;
        private Label _statusLabel;
        private VBoxContainer _content;

        /// <summary>Display name shown in the tab.</summary>
        public abstract string PanelName { get; }

        /// <summary>Accent color for this module's header.</summary>
        public abstract Color AccentColor { get; }

        /// <summary>Whether unsaved changes exist.</summary>
        public bool IsDirty => _dirty;

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            // Toolbar
            _toolbar = new HBoxContainer();
            _toolbar.AddThemeConstantOverride("separation", 6);

            var titleLabel = EditorStyles.MakeLabel(PanelName, EditorStyles.FontHeader, AccentColor);
            _toolbar.AddChild(titleLabel);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _toolbar.AddChild(spacer);

            // Undo/Redo
            var undoBtn = EditorStyles.MakeButton("Undo", EditorStyles.FontSmall);
            undoBtn.Pressed += () =>
            {
                var snapshot = _undo.Undo();
                if (snapshot != null) RestoreSnapshot(snapshot);
            };
            _toolbar.AddChild(undoBtn);

            var redoBtn = EditorStyles.MakeButton("Redo", EditorStyles.FontSmall);
            redoBtn.Pressed += () =>
            {
                var snapshot = _undo.Redo();
                if (snapshot != null) RestoreSnapshot(snapshot);
            };
            _toolbar.AddChild(redoBtn);

            // Save
            var saveBtn = EditorStyles.MakeButton("Save", EditorStyles.FontSmall, EditorStyles.StatusSaved);
            saveBtn.Pressed += () => Save();
            _toolbar.AddChild(saveBtn);

            // Reload
            var reloadBtn = EditorStyles.MakeButton("Reload", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            reloadBtn.Pressed += () => Reload();
            _toolbar.AddChild(reloadBtn);

            // Status
            _statusLabel = EditorStyles.MakeLabel("Ready", EditorStyles.FontTiny, EditorStyles.TextMuted);
            _toolbar.AddChild(_statusLabel);

            // Bug report button (skip for the Bugs tab itself)
            if (PanelName != "Bugs")
            {
                var bugBtn = EditorStyles.MakeButton("Bug", EditorStyles.FontSmall, EditorStyles.StatusError);
                bugBtn.TooltipText = "Screenshot this tab and open bug reporter";
                bugBtn.Pressed += () =>
                {
                    var mgr = EditorManager.Instance;
                    if (mgr == null) return;
                    mgr.CaptureEditorScreenshot();
                    mgr.SwitchToTab("Bugs");
                };
                _toolbar.AddChild(bugBtn);
            }

            AddChild(_toolbar);
            AddChild(EditorStyles.MakeSeparator());

            // Content area
            _content = new VBoxContainer();
            _content.SizeFlagsVertical = SizeFlags.ExpandFill;
            _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(_content);

            BuildUI(_content);
            Reload();
        }

        /// <summary>Build the module-specific UI inside the content container.</summary>
        protected abstract void BuildUI(VBoxContainer content);

        /// <summary>Load/reload data from JSON or fallback.</summary>
        protected abstract void Reload();

        /// <summary>Save current data to JSON file.</summary>
        protected abstract void Save();

        /// <summary>Restore state from an undo snapshot.</summary>
        protected abstract void RestoreSnapshot(string jsonSnapshot);

        /// <summary>Mark the panel as having unsaved changes.</summary>
        protected void MarkDirty()
        {
            _dirty = true;
            UpdateStatus();
            OnDirtyChanged?.Invoke();
        }

        /// <summary>Mark the panel as saved/clean.</summary>
        protected void MarkClean()
        {
            _dirty = false;
            UpdateStatus();
            OnDirtyChanged?.Invoke();
        }

        /// <summary>Push current state for undo.</summary>
        protected void PushUndo(string jsonSnapshot)
        {
            _undo.Push(jsonSnapshot);
        }

        /// <summary>Set status text shown in toolbar.</summary>
        protected void SetStatus(string text, Color? color = null)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = text;
                _statusLabel.AddThemeColorOverride("font_color", color ?? EditorStyles.TextMuted);
            }
        }

        private void UpdateStatus()
        {
            if (_dirty)
                SetStatus("Unsaved changes", EditorStyles.StatusDirty);
            else
                SetStatus("Saved", EditorStyles.StatusSaved);
        }

        /// <summary>
        /// Helper: Load JSON from a file path, returning parsed dictionary or null.
        /// Pulls latest from git first to pick up changes from other machines.
        /// </summary>
        protected static Dictionary<string, object> LoadJson(string path)
        {
            // Pull latest data before loading
            GitPullLatest();

            if (!FileAccess.FileExists(path)) return null;
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            var text = file.GetAsText();
            if (string.IsNullOrWhiteSpace(text)) return null;
            return MiniJson.Deserialize(text) as Dictionary<string, object>;
        }

        private static bool _hasPulled;

        /// <summary>
        /// Pull latest from remote once per editor session to get changes from other machines.
        /// Only pulls once to avoid repeated network calls on every tab switch.
        /// </summary>
        private static void GitPullLatest()
        {
            if (_hasPulled) return;
            _hasPulled = true;

            try
            {
                string projectDir = ProjectSettings.GlobalizePath("res://");
                RunGit(projectDir,
                    "-c user.name=calschuss -c user.email=stuart.white28@protonmail.com -c core.hooksPath=/dev/null pull --rebase origin dev");
                GD.Print("[EditorPanel] Pulled latest from git");
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EditorPanel] Git pull failed: {e.Message}");
            }
        }

        /// <summary>
        /// Helper: Save an object as JSON to a file path.
        /// Automatically syncs the saved file to git in the background.
        /// </summary>
        protected static bool SaveJson(string path, object data)
        {
            var json = MiniJsonWriter.Serialize(data);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null) return false;
            file.StoreString(json);

            // Sync to git in background so changes are available on other machines
            GitSyncDataFile(path);
            return true;
        }

        /// <summary>
        /// Git add, commit, and push a Data file in the background.
        /// Runs off the main thread to avoid freezing the editor.
        /// </summary>
        private static void GitSyncDataFile(string resPath)
        {
            // Convert res:// path to filesystem path relative to project root
            string projectDir = ProjectSettings.GlobalizePath("res://");
            string absPath = ProjectSettings.GlobalizePath(resPath);

            // Run git operations in background
            Task.Run(() =>
            {
                try
                {
                    string fileName = System.IO.Path.GetFileName(absPath);

                    // git add the specific file
                    var addResult = RunGit(projectDir, $"add \"{absPath}\"");
                    if (addResult != 0)
                    {
                        GD.PrintErr($"[EditorPanel] git add failed for {fileName}");
                        return;
                    }

                    // Check if there's actually something to commit
                    var statusResult = RunGitOutput(projectDir, "diff --cached --quiet");
                    if (statusResult == 0)
                    {
                        // Nothing staged — file unchanged
                        return;
                    }

                    // Commit
                    string msg = $"Editor: update {fileName}";
                    var commitResult = RunGit(projectDir,
                        $"-c user.name=calschuss -c user.email=stuart.white28@protonmail.com commit -m \"{msg}\"");
                    if (commitResult != 0)
                    {
                        GD.PrintErr($"[EditorPanel] git commit failed for {fileName}");
                        return;
                    }

                    // Push (with LFS hook workaround)
                    var pushResult = RunGit(projectDir, "-c core.hooksPath=/dev/null push origin dev");
                    if (pushResult != 0)
                    {
                        // Try pull --rebase then push again
                        RunGit(projectDir,
                            "-c user.name=calschuss -c user.email=stuart.white28@protonmail.com pull --rebase origin dev");
                        RunGit(projectDir, "-c core.hooksPath=/dev/null push origin dev");
                    }

                    GD.Print($"[EditorPanel] Synced {fileName} to git");
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[EditorPanel] Git sync error: {e.Message}");
                }
            });
        }

        private static int RunGit(string workDir, string args)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(30000);
            return proc?.ExitCode ?? -1;
        }

        private static int RunGitOutput(string workDir, string args)
        {
            return RunGit(workDir, args);
        }
    }
}
