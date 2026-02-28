using Godot;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Simple idle bob animation for characters. Attach as child of a Node3D
    /// that contains the visual body root.
    /// </summary>
    public partial class SimpleAnimator : Node
    {
        private Node3D _bodyRoot;
        private float _timer;
        private float _baseY;
        private bool _initialized;

        [Export] private float _bobAmount = 0.05f;
        [Export] private float _bobSpeed = 2.5f;

        public void Initialize(Node3D bodyRoot)
        {
            _bodyRoot = bodyRoot;
            if (_bodyRoot != null)
            {
                _baseY = _bodyRoot.Position.Y;
                _initialized = true;
            }
        }

        public override void _Process(double delta)
        {
            if (!_initialized || _bodyRoot == null || !GodotObject.IsInstanceValid(_bodyRoot))
                return;

            _timer += (float)delta * _bobSpeed;
            float bob = Mathf.Sin(_timer) * _bobAmount;
            var pos = _bodyRoot.Position;
            pos.Y = _baseY + bob;
            _bodyRoot.Position = pos;
        }
    }
}
