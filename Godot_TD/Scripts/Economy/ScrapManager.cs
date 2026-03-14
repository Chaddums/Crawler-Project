using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Manages the scrap economy (Pillar #1).
    /// Scrap drops from enemies, degrades on the ground, collected manually or by Recyclers.
    /// </summary>
    public partial class ScrapManager : Node
    {
        public int CurrentScrap { get; private set; } = Constants.STARTING_SCRAP;

        // Scrap piles on the ground waiting to be collected
        private readonly List<ScrapPile> _piles = new();

        private class ScrapPile
        {
            public Vector3 Position;
            public int Amount;
            public float TimeRemaining;
            public MeshInstance3D Visual;
        }

        public override void _Ready()
        {
            ServiceLocator.Register(this);
            GameEvents.OnScrapDropped += OnScrapDropped;
        }

        public void AddScrap(int amount)
        {
            CurrentScrap += amount;
            GameEvents.OnScrapChanged?.Invoke(CurrentScrap);
        }

        public bool TrySpend(int amount)
        {
            if (CurrentScrap < amount) return false;
            CurrentScrap -= amount;
            GameEvents.OnScrapChanged?.Invoke(CurrentScrap);
            return true;
        }

        public bool CanAfford(int amount) => CurrentScrap >= amount;

        private void OnScrapDropped(Vector3 position, int amount)
        {
            // Create a scrap pile on the ground
            var visual = new MeshInstance3D();
            var mesh = new SphereMesh();
            mesh.Radius = 0.15f + amount * 0.02f;
            mesh.Height = 0.3f + amount * 0.04f;
            visual.Mesh = mesh;
            visual.Position = position + new Vector3(0, 0.2f, 0);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.8f, 0.65f, 0.2f);
            mat.Emission = new Color(0.6f, 0.5f, 0.1f);
            mat.EmissionEnabled = true;
            mat.EmissionEnergyMultiplier = 0.5f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            visual.MaterialOverride = mat;
            GetTree().CurrentScene.AddChild(visual);

            _piles.Add(new ScrapPile {
                Position = position,
                Amount = amount,
                TimeRemaining = Constants.SCRAP_DECAY_TIME,
                Visual = visual
            });
        }

        public override void _PhysicsProcess(double delta)
        {
            for (int i = _piles.Count - 1; i >= 0; i--)
            {
                var pile = _piles[i];
                pile.TimeRemaining -= (float)delta;

                // Pulsing glow effect
                if (IsInstanceValid(pile.Visual) && pile.Visual.MaterialOverride is StandardMaterial3D mat)
                {
                    float pulse = 0.3f + 0.2f * Mathf.Sin((float)Time.GetTicksMsec() * 0.005f);
                    mat.EmissionEnergyMultiplier = pulse;

                    // Flash red when about to expire
                    if (pile.TimeRemaining < 3f)
                        mat.AlbedoColor = new Color(0.9f, 0.3f, 0.1f);
                }

                if (pile.TimeRemaining <= 0)
                {
                    // Scrap degrades — gone forever
                    if (IsInstanceValid(pile.Visual))
                        pile.Visual.QueueFree();
                    _piles.RemoveAt(i);
                }
            }

            // Auto-collect by Recycler towers
            CollectByRecyclers();
        }

        /// <summary>
        /// Player clicks near scrap to collect it (or walks hero bot near it).
        /// </summary>
        public void TryCollectNear(Vector3 position, float radius)
        {
            for (int i = _piles.Count - 1; i >= 0; i--)
            {
                // 2D distance (ignore Y) — click lands on ground plane, piles float above it
                var diff = _piles[i].Position - position;
                float dist2D = Mathf.Sqrt(diff.X * diff.X + diff.Z * diff.Z);
                if (dist2D <= radius)
                {
                    CollectPile(i);
                }
            }
        }

        private void CollectByRecyclers()
        {
            foreach (var node in GetTree().GetNodesInGroup(Constants.GROUP_TOWER))
            {
                if (node is TowerController tower && tower.Data?.Type == TowerType.Recycler)
                {
                    float range = tower.Stats.GetStat(StatType.Range) * Constants.CELL_SIZE;
                    for (int i = _piles.Count - 1; i >= 0; i--)
                    {
                        if (_piles[i].Position.DistanceTo(tower.GlobalPosition) <= range)
                            CollectPile(i);
                    }
                }
            }
        }

        private void CollectPile(int index)
        {
            var pile = _piles[index];

            // VFX: collect pop
            VfxFactory.SpawnScrapCollectPop(GetTree(), pile.Position);

            AddScrap(pile.Amount);
            GameEvents.OnScrapCollected?.Invoke(pile.Amount);

            if (IsInstanceValid(pile.Visual))
                pile.Visual.QueueFree();
            _piles.RemoveAt(index);
        }

        public override void _ExitTree()
        {
            GameEvents.OnScrapDropped -= OnScrapDropped;
            ServiceLocator.Unregister<ScrapManager>();
        }
    }
}
