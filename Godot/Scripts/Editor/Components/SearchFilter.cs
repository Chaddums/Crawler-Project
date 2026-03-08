using Godot;
using System;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Text filter widget. Emits OnFilterChanged when the user types.
    /// Used to filter DataTable rows by ID/name match.
    /// </summary>
    public partial class SearchFilter : HBoxContainer
    {
        private LineEdit _input;

        public event Action<string> OnFilterChanged;
        public string CurrentFilter => _input?.Text ?? "";

        public override void _Ready()
        {
            var icon = EditorStyles.MakeLabel("Search:", EditorStyles.FontSmall, EditorStyles.TextSecondary);
            icon.CustomMinimumSize = new Vector2(50, 0);
            AddChild(icon);

            _input = EditorStyles.MakeLineEdit("Filter...", EditorStyles.FontSmall);
            _input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _input.TextChanged += text => OnFilterChanged?.Invoke(text);
            AddChild(_input);

            var clearBtn = EditorStyles.MakeButton("X", EditorStyles.FontSmall);
            clearBtn.CustomMinimumSize = new Vector2(28, 0);
            clearBtn.Pressed += () =>
            {
                _input.Text = "";
                OnFilterChanged?.Invoke("");
            };
            AddChild(clearBtn);
        }

        public void Clear()
        {
            if (_input != null) _input.Text = "";
        }
    }
}
