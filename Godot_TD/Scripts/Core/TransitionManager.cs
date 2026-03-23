using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// UX12: Full-screen fade transition singleton.
    /// Autoload CanvasLayer (Layer 99, ProcessMode.Always).
    /// Wraps scene changes with a fade-out → change → fade-in sequence.
    /// </summary>
    public partial class TransitionManager : CanvasLayer
    {
        public static TransitionManager Instance { get; private set; }

        private ColorRect _overlay;
        private bool _transitioning;

        public override void _Ready()
        {
            Instance = this;
            Layer = 99;
            ProcessMode = ProcessModeEnum.Always;

            _overlay = new ColorRect();
            _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _overlay.Color = new Color(0, 0, 0, 0);
            _overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_overlay);
        }

        /// <summary>
        /// Fade out, change scene, fade in. Fire-and-forget safe via async void.
        /// </summary>
        public async void TransitionToScene(string scenePath)
        {
            if (_transitioning) return;
            _transitioning = true;

            await FadeOutAsync();
            GetTree().ChangeSceneToFile(scenePath);
            // Wait one frame for the new scene to initialize
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await FadeInAsync();

            _transitioning = false;
        }

        /// <summary>Standalone fade to black.</summary>
        public async void FadeOut()
        {
            await FadeOutAsync();
        }

        /// <summary>Standalone fade from black.</summary>
        public async void FadeIn()
        {
            await FadeInAsync();
        }

        private async System.Threading.Tasks.Task FadeOutAsync()
        {
            var tween = CreateTween();
            tween.SetPauseMode(Tween.TweenPauseMode.Process);
            tween.TweenProperty(_overlay, "color:a", 1.0f, Constants.TRANSITION_FADE_DURATION);
            await ToSignal(tween, Tween.SignalName.Finished);
        }

        private async System.Threading.Tasks.Task FadeInAsync()
        {
            var tween = CreateTween();
            tween.SetPauseMode(Tween.TweenPauseMode.Process);
            tween.TweenProperty(_overlay, "color:a", 0.0f, Constants.TRANSITION_FADE_DURATION);
            await ToSignal(tween, Tween.SignalName.Finished);
        }

        public bool IsTransitioning => _transitioning;
    }
}
