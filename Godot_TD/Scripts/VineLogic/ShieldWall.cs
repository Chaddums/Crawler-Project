using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Visual energy barrier entity on a map edge. Procedural mesh panels with
    /// pulse animation and health-dependent color. Collapses with shatter VFX.
    /// </summary>
    public partial class ShieldWall : Node3D
    {
        private float _maxHP;
        private float _currentHP;
        private CardinalDirection _direction;
        private float _pulseTime;
        private bool _collapsed;

        private readonly List<MeshInstance3D> _panels = new();
        private StandardMaterial3D _barrierMat;
        private Color _baseColor;
        private Color _criticalColor;

        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsCollapsed => _collapsed;

        public void Initialize(ShieldWallConfig config, VineGrid grid)
        {
            _direction = config.Direction;
            _maxHP = config.HP;
            _currentHP = config.HP;

            AddToGroup(Constants.GROUP_SHIELD_WALL);

            // Determine which cells this wall covers based on direction
            var cells = GetWallCells(config, grid);
            if (cells.Count == 0) return;

            // Pick colors based on planet theme
            bool isScrapyard = PlanetTheme.Current is ScrapyardPlanetTheme;
            _baseColor = isScrapyard
                ? new Color(0.9f, 0.6f, 0.15f)   // Warm amber for scrapyard
                : TronTheme.GridCyan;              // Cyan for Tron
            _criticalColor = new Color(0.95f, 0.2f, 0.1f);  // Red-orange at low HP

            // Create shared material for all panels
            _barrierMat = new StandardMaterial3D();
            _barrierMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _barrierMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _barrierMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            _barrierMat.EmissionEnabled = true;
            _barrierMat.Emission = _baseColor;
            _barrierMat.EmissionEnergyMultiplier = 0.6f;
            _barrierMat.AlbedoColor = new Color(_baseColor.R, _baseColor.G, _baseColor.B,
                Constants.SHIELD_WALL_PANEL_ALPHA);

            // Build barrier panels along the edge
            float cs = Constants.VINE_CELL_SIZE;
            float panelH = Constants.SHIELD_WALL_PANEL_HEIGHT;

            foreach (var cell in cells)
            {
                var worldPos = grid.GridToWorld(cell);

                // Main barrier panel — tall translucent box
                var panelMesh = new BoxMesh();
                bool isHorizontal = _direction == CardinalDirection.North || _direction == CardinalDirection.South;
                panelMesh.Size = isHorizontal
                    ? new Vector3(cs * 0.95f, panelH, 0.15f)   // Wide, thin along Z
                    : new Vector3(0.15f, panelH, cs * 0.95f);  // Thin along X, wide along Z

                var panel = new MeshInstance3D();
                panel.Mesh = panelMesh;
                panel.MaterialOverride = _barrierMat;
                panel.Position = new Vector3(worldPos.X, panelH / 2f, worldPos.Z);
                AddChild(panel);
                _panels.Add(panel);

                // Vertical edge accent lines
                var edgeMesh = new BoxMesh();
                float edgeW = 0.04f;
                edgeMesh.Size = isHorizontal
                    ? new Vector3(edgeW, panelH + 0.2f, 0.04f)
                    : new Vector3(0.04f, panelH + 0.2f, edgeW);

                float halfCell = cs * 0.475f;
                for (int side = -1; side <= 1; side += 2)
                {
                    var edge = new MeshInstance3D();
                    edge.Mesh = edgeMesh;

                    var edgeMat = new StandardMaterial3D();
                    edgeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    edgeMat.EmissionEnabled = true;
                    edgeMat.Emission = _baseColor;
                    edgeMat.EmissionEnergyMultiplier = 1.2f;
                    edgeMat.AlbedoColor = _baseColor;
                    edge.MaterialOverride = edgeMat;

                    if (isHorizontal)
                        edge.Position = new Vector3(worldPos.X + side * halfCell, panelH / 2f + 0.1f, worldPos.Z);
                    else
                        edge.Position = new Vector3(worldPos.X, panelH / 2f + 0.1f, worldPos.Z + side * halfCell);

                    AddChild(edge);
                    _panels.Add(edge);
                }
            }

            // Horizontal top bar connecting all panels
            if (cells.Count > 1)
            {
                var first = grid.GridToWorld(cells[0]);
                var last = grid.GridToWorld(cells[cells.Count - 1]);
                var center = (first + last) / 2f;
                float span = first.DistanceTo(last) + cs;

                var topMesh = new BoxMesh();
                bool horiz = _direction == CardinalDirection.North || _direction == CardinalDirection.South;
                topMesh.Size = horiz
                    ? new Vector3(span, 0.08f, 0.2f)
                    : new Vector3(0.2f, 0.08f, span);

                var topBar = new MeshInstance3D();
                topBar.Mesh = topMesh;
                var topMat = new StandardMaterial3D();
                topMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                topMat.EmissionEnabled = true;
                topMat.Emission = _baseColor;
                topMat.EmissionEnergyMultiplier = 1.0f;
                topMat.AlbedoColor = _baseColor;
                topBar.MaterialOverride = topMat;
                topBar.Position = new Vector3(center.X, panelH + 0.1f, center.Z);
                AddChild(topBar);
                _panels.Add(topBar);
            }
        }

        public override void _Process(double delta)
        {
            if (_collapsed || _barrierMat == null) return;

            _pulseTime += (float)delta * Constants.SHIELD_WALL_PULSE_SPEED;

            float hpFrac = _maxHP > 0 ? _currentHP / _maxHP : 0f;
            bool critical = hpFrac < Constants.SHIELD_WALL_CRITICAL_PCT;

            // Pulse alpha
            float baseAlpha = Constants.SHIELD_WALL_PANEL_ALPHA;
            float pulseAmount = critical ? 0.15f : 0.05f;
            float pulseSpeed = critical ? 6f : 1f;
            float alpha = baseAlpha + Mathf.Sin(_pulseTime * pulseSpeed) * pulseAmount;

            // Color lerp from base to critical
            Color current = _baseColor.Lerp(_criticalColor, 1f - hpFrac);
            _barrierMat.AlbedoColor = new Color(current.R, current.G, current.B, alpha);
            _barrierMat.Emission = current;

            // Flicker when critical
            if (critical && Mathf.Sin(_pulseTime * 12f) > 0.6f)
            {
                _barrierMat.AlbedoColor = new Color(current.R, current.G, current.B, alpha * 0.3f);
            }
        }

        public void TakeDamage(float amount)
        {
            if (_collapsed) return;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
        }

        /// <summary>
        /// Play collapse VFX and remove the wall from the scene.
        /// </summary>
        public void Collapse()
        {
            if (_collapsed) return;
            _collapsed = true;
            _currentHP = 0f;

            // Shatter effect: scatter panels outward with fade
            SpawnShatterParticles();

            // Remove all panels immediately
            foreach (var panel in _panels)
                panel.QueueFree();
            _panels.Clear();

            // Self-destruct after particles settle
            var timer = GetTree().CreateTimer(2.0f);
            timer.Timeout += QueueFree;
        }

        private void SpawnShatterParticles()
        {
            var rng = new RandomNumberGenerator();
            int shardCount = 12;

            for (int i = 0; i < shardCount; i++)
            {
                var shard = new MeshInstance3D();
                float size = rng.RandfRange(0.15f, 0.4f);
                shard.Mesh = new BoxMesh { Size = new Vector3(size, size * 0.5f, size * 0.3f) };

                var shardMat = new StandardMaterial3D();
                shardMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                shardMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                shardMat.EmissionEnabled = true;
                shardMat.Emission = _baseColor;
                shardMat.EmissionEnergyMultiplier = 2f;
                shardMat.AlbedoColor = new Color(_baseColor.R, _baseColor.G, _baseColor.B, 0.8f);
                shard.MaterialOverride = shardMat;

                // Scatter from center of the wall
                float panelH = Constants.SHIELD_WALL_PANEL_HEIGHT;
                shard.Position = new Vector3(
                    rng.RandfRange(-2f, 2f),
                    rng.RandfRange(0.5f, panelH),
                    rng.RandfRange(-2f, 2f));
                shard.RotationDegrees = new Vector3(
                    rng.RandfRange(0, 360),
                    rng.RandfRange(0, 360),
                    rng.RandfRange(0, 360));

                AddChild(shard);

                // Animate: fly outward and fade
                var tween = CreateTween();
                var dir = shard.Position.Normalized();
                var targetPos = shard.Position + dir * rng.RandfRange(3f, 8f) + Vector3.Down * 2f;
                tween.TweenProperty(shard, "position", targetPos, rng.RandfRange(0.8f, 1.5f))
                    .SetEase(Tween.EaseType.Out);
                tween.Parallel().TweenProperty(shardMat, "albedo_color",
                    new Color(_baseColor.R, _baseColor.G, _baseColor.B, 0f), 1.2f);
                tween.TweenCallback(Callable.From(() => shard.QueueFree()));
            }
        }

        /// <summary>
        /// Determine which grid cells this wall covers based on direction and entry region.
        /// </summary>
        private static List<Vector2I> GetWallCells(ShieldWallConfig config, VineGrid grid)
        {
            var cells = new List<Vector2I>();

            // Use the entry region's cells as the wall coverage area
            if (config.EntryRegionIndex >= 0 && config.EntryRegionIndex < grid.EntryRegions.Count)
            {
                var region = grid.EntryRegions[config.EntryRegionIndex];
                cells.AddRange(region.Cells);
            }

            return cells;
        }
    }
}
