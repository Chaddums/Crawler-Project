using Godot;
using System;
using System.Collections.Generic;

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
        /// </summary>
        protected static Dictionary<string, object> LoadJson(string path)
        {
            if (!FileAccess.FileExists(path)) return null;
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            var text = file.GetAsText();
            if (string.IsNullOrWhiteSpace(text)) return null;
            return MiniJson.Deserialize(text) as Dictionary<string, object>;
        }

        /// <summary>
        /// Helper: Save an object as JSON to a file path.
        /// </summary>
        protected static bool SaveJson(string path, object data)
        {
            var json = MiniJsonWriter.Serialize(data);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null) return false;
            file.StoreString(json);
            return true;
        }
    }
}
