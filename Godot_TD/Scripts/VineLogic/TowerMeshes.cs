using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Built-in models for towers with no kit model (Tesla Coil, Flak Battery, Scatter Cannon,
    /// Barrier Wall), which were plain coloured cubes. Each sits on the same dark plinth with a
    /// band in the tower's tint, built from y = 0 up, at most one cell across. Materials are
    /// per instance so hit flashes stay on one tower.
    /// </summary>
    public static class TowerMeshes
    {
        public static Node3D Build(VineNodeType type, Color tint)
        {
            return type switch
            {
                VineNodeType.TeslaCoil => Tesla(tint),
                VineNodeType.FlakBattery => Flak(tint),
                VineNodeType.ScatterCannon => Scatter(tint),
                VineNodeType.BarrierWall => Wall(tint),
                _ => null,
            };
        }

        private static StandardMaterial3D Metal(float shade = 0.09f) => new()
        {
            AlbedoColor = new Color(shade, shade * 1.05f, shade * 1.2f),
            Metallic = 0.75f,
            Roughness = 0.35f,
        };

        private static StandardMaterial3D Copper() => new()
        {
            AlbedoColor = new Color(0.62f, 0.36f, 0.18f),
            Metallic = 0.9f,
            Roughness = 0.3f,
        };

        private static StandardMaterial3D Glow(Color tint, float energy) => new()
        {
            AlbedoColor = tint,
            EmissionEnabled = true,
            Emission = tint,
            EmissionEnergyMultiplier = energy,
            Roughness = 0.4f,
        };

        private static MeshInstance3D Part(Node3D parent, Mesh mesh, Material mat, Vector3 pos, Vector3? rotDeg = null)
        {
            var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Position = pos };
            if (rotDeg != null) mi.RotationDegrees = rotDeg.Value;
            parent.AddChild(mi);
            return mi;
        }

        /// <summary>Hex plinth with a tinted band, 0.25 tall.</summary>
        private static Node3D Plinth(Color tint, float radius = 0.72f)
        {
            var root = new Node3D { Name = "TowerModel" };
            Part(root, new CylinderMesh { TopRadius = radius * 0.9f, BottomRadius = radius, Height = 0.22f, RadialSegments = 6, Rings = 1 },
                Metal(), new Vector3(0, 0.11f, 0));
            Part(root, new CylinderMesh { TopRadius = radius * 0.92f, BottomRadius = radius * 0.92f, Height = 0.05f, RadialSegments = 6, Rings = 1 },
                Glow(tint, 0.8f), new Vector3(0, 0.235f, 0));
            return root;
        }

        private static Node3D Tesla(Color tint)
        {
            var root = Plinth(tint, 0.6f);
            Part(root, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.2f, Height = 0.95f, RadialSegments = 10 },
                Metal(0.12f), new Vector3(0, 0.73f, 0));
            var copper = Copper();
            float[] ys = { 0.5f, 0.72f, 0.92f };
            for (int i = 0; i < ys.Length; i++)
            {
                float r = 0.36f - i * 0.06f;
                Part(root, new TorusMesh { InnerRadius = r - 0.07f, OuterRadius = r, Rings = 16, RingSegments = 8 },
                    copper, new Vector3(0, ys[i], 0));
            }
            Part(root, new SphereMesh { Radius = 0.17f, Height = 0.34f, RadialSegments = 12, Rings = 6 },
                Glow(tint, 1.4f), new Vector3(0, 1.28f, 0));
            return root;
        }

        private static Node3D Flak(Color tint)
        {
            var root = Plinth(tint);
            var metal = Metal(0.11f);
            Part(root, new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 0.2f, RadialSegments = 10 },
                metal, new Vector3(0, 0.34f, 0));
            // Launcher box tilted up, a 2x2 pack of tubes with lit mouths
            var head = new Node3D { Position = new Vector3(0, 0.62f, 0), RotationDegrees = new Vector3(-35, 0, 0) };
            root.AddChild(head);
            Part(head, new BoxMesh { Size = new Vector3(0.78f, 0.42f, 0.62f) }, metal, Vector3.Zero);
            var tube = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.2f, RadialSegments = 10 };
            var mouth = Glow(tint, 1.1f);
            foreach (var x in new[] { -0.19f, 0.19f })
            foreach (var y in new[] { -0.09f, 0.11f })
                Part(head, tube, mouth, new Vector3(x, y, 0.36f), new Vector3(90, 0, 0));
            return root;
        }

        private static Node3D Scatter(Color tint)
        {
            var root = Plinth(tint);
            var metal = Metal(0.1f);
            Part(root, new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.48f, Height = 0.34f, RadialSegments = 12 },
                metal, new Vector3(0, 0.41f, 0));
            var barrel = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.09f, Height = 0.7f, RadialSegments = 10 };
            var ring = new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.11f, Rings = 12, RingSegments = 6 };
            var glow = Glow(tint, 1.1f);
            foreach (var yaw in new[] { -22f, 0f, 22f })
            {
                // Fanned barrels pointing forward (+Z), with a lit muzzle ring
                var pivot = new Node3D { Position = new Vector3(0, 0.5f, 0), RotationDegrees = new Vector3(0, yaw, 0) };
                root.AddChild(pivot);
                Part(pivot, barrel, metal, new Vector3(0, 0, 0.42f), new Vector3(90, 0, 0));
                Part(pivot, ring, glow, new Vector3(0, 0, 0.78f), new Vector3(90, 0, 0));
            }
            return root;
        }

        private static Node3D Wall(Color tint)
        {
            // Fills the cell so a line of walls reads as a wall, not a row of crates
            var root = new Node3D { Name = "TowerModel" };
            var metal = Metal(0.1f);
            Part(root, new BoxMesh { Size = new Vector3(1.86f, 0.8f, 1.86f) }, metal, new Vector3(0, 0.4f, 0));
            Part(root, new BoxMesh { Size = new Vector3(1.6f, 0.12f, 1.6f) }, Metal(0.14f), new Vector3(0, 0.86f, 0));
            // Tinted seam around the top edge
            var seam = Glow(tint, 0.7f);
            foreach (var (pos, size) in new (Vector3, Vector3)[] {
                (new Vector3(0, 0.78f, 0.935f), new Vector3(1.88f, 0.05f, 0.02f)),
                (new Vector3(0, 0.78f, -0.935f), new Vector3(1.88f, 0.05f, 0.02f)),
                (new Vector3(0.935f, 0.78f, 0), new Vector3(0.02f, 0.05f, 1.88f)),
                (new Vector3(-0.935f, 0.78f, 0), new Vector3(0.02f, 0.05f, 1.88f)) })
                Part(root, new BoxMesh { Size = size }, seam, pos);
            return root;
        }
    }
}
