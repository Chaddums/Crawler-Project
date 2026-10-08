using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Tells a click from a drag. Right-drag turns the camera, so acting on the right button's
    /// press sold the tower (or flipped the Spire's mining mode) under the cursor every time a
    /// player started turning the camera over one. Feed it every event for the button; it answers
    /// true on the release that ends a click (barely moved, quickly let go).
    /// </summary>
    public sealed class ClickGuard
    {
        private readonly MouseButton _button;
        private Vector2 _down;
        private ulong _downMs;
        private bool _armed;
        public const float MaxMovePx = 8f;
        public const ulong MaxHoldMs = 400;

        public ClickGuard(MouseButton button) { _button = button; }

        public bool IsClick(InputEvent @event)
        {
            if (@event is not InputEventMouseButton mb || mb.ButtonIndex != _button) return false;
            if (mb.Pressed)
            {
                _down = mb.Position;
                _downMs = Time.GetTicksMsec();
                _armed = true;
                return false;
            }
            bool click = _armed && (mb.Position - _down).Length() < MaxMovePx && Time.GetTicksMsec() - _downMs <= MaxHoldMs;
            _armed = false;
            return click;
        }
    }
}
