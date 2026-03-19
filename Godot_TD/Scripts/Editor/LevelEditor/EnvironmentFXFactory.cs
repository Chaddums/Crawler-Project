using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Creates environment FX nodes (fire, sparks, smoke, fog, electric arcs)
    /// as GpuParticles3D or mesh-based effects.
    /// </summary>
    public static class EnvironmentFXFactory
    {
        public static Node3D Create(string fxType, Vector3 position, float radius, float intensity, Color color)
        {
            return fxType switch
            {
                "Fire" => CreateFire(position, radius, intensity, color),
                "Sparks" => CreateSparks(position, radius, intensity, color),
                "Smoke" => CreateSmoke(position, radius, intensity, color),
                "Fog" => CreateFogZone(position, radius, intensity, color),
                "Electric_Arc" => CreateElectricArc(position, radius, intensity, color),
                _ => CreateFire(position, radius, intensity, color)
            };
        }

        private static Node3D CreateFire(Vector3 position, float radius, float intensity, Color color)
        {
            var particles = new GpuParticles3D();
            particles.Position = position;
            particles.Amount = (int)(16 * intensity);
            particles.Lifetime = 1.2;
            particles.Explosiveness = 0.1f;
            particles.Randomness = 0.3f;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 15f;
            mat.InitialVelocityMin = 1f * intensity;
            mat.InitialVelocityMax = 3f * intensity;
            mat.Gravity = new Vector3(0, -0.5f, 0);
            mat.ScaleMin = 0.3f * radius;
            mat.ScaleMax = 0.6f * radius;
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = radius * 0.3f;

            var colorRamp = new Gradient();
            colorRamp.SetColor(0, new Color(color.R, color.G, color.B, 0.9f));
            colorRamp.SetColor(1, new Color(color.R * 0.3f, 0, 0, 0));
            var tex = new GradientTexture1D();
            tex.Gradient = colorRamp;
            mat.ColorRamp = tex;

            particles.ProcessMaterial = mat;

            // Draw pass: simple quad
            var mesh = new QuadMesh();
            mesh.Size = new Vector2(0.5f, 0.5f);
            var meshMat = new StandardMaterial3D();
            meshMat.AlbedoColor = color;
            meshMat.EmissionEnabled = true;
            meshMat.Emission = color;
            meshMat.EmissionEnergyMultiplier = 2f * intensity;
            meshMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            meshMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            meshMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            mesh.Material = meshMat;
            particles.DrawPass1 = mesh;

            return particles;
        }

        private static Node3D CreateSparks(Vector3 position, float radius, float intensity, Color color)
        {
            var particles = new GpuParticles3D();
            particles.Position = position;
            particles.Amount = (int)(24 * intensity);
            particles.Lifetime = 0.6;
            particles.Explosiveness = 0.8f;
            particles.Randomness = 0.7f;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 90f;
            mat.InitialVelocityMin = 3f * intensity;
            mat.InitialVelocityMax = 8f * intensity;
            mat.Gravity = new Vector3(0, -9.8f, 0);
            mat.ScaleMin = 0.05f;
            mat.ScaleMax = 0.15f;
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = radius * 0.2f;
            particles.ProcessMaterial = mat;

            var mesh = new SphereMesh();
            mesh.Radius = 0.05f;
            mesh.Height = 0.1f;
            var meshMat = new StandardMaterial3D();
            meshMat.AlbedoColor = color;
            meshMat.EmissionEnabled = true;
            meshMat.Emission = color;
            meshMat.EmissionEnergyMultiplier = 4f * intensity;
            meshMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mesh.Material = meshMat;
            particles.DrawPass1 = mesh;

            return particles;
        }

        private static Node3D CreateSmoke(Vector3 position, float radius, float intensity, Color color)
        {
            var particles = new GpuParticles3D();
            particles.Position = position;
            particles.Amount = (int)(8 * intensity);
            particles.Lifetime = 3.0;
            particles.Explosiveness = 0f;
            particles.Randomness = 0.5f;

            var mat = new ParticleProcessMaterial();
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 25f;
            mat.InitialVelocityMin = 0.3f * intensity;
            mat.InitialVelocityMax = 1f * intensity;
            mat.Gravity = new Vector3(0, 0.2f, 0);
            mat.ScaleMin = radius * 0.5f;
            mat.ScaleMax = radius * 1.5f;
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = radius * 0.3f;

            var colorRamp = new Gradient();
            colorRamp.SetColor(0, new Color(color.R, color.G, color.B, 0.4f * intensity));
            colorRamp.SetColor(1, new Color(color.R, color.G, color.B, 0f));
            var tex = new GradientTexture1D();
            tex.Gradient = colorRamp;
            mat.ColorRamp = tex;

            particles.ProcessMaterial = mat;

            var mesh = new QuadMesh();
            mesh.Size = new Vector2(1f, 1f);
            var meshMat = new StandardMaterial3D();
            meshMat.AlbedoColor = new Color(color.R, color.G, color.B, 0.3f);
            meshMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            meshMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            meshMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            mesh.Material = meshMat;
            particles.DrawPass1 = mesh;

            return particles;
        }

        private static Node3D CreateFogZone(Vector3 position, float radius, float intensity, Color color)
        {
            var fogNode = new MeshInstance3D();
            fogNode.Position = position;

            var sphere = new SphereMesh();
            sphere.Radius = radius;
            sphere.Height = radius * 2f;
            fogNode.Mesh = sphere;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.15f * intensity);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            fogNode.MaterialOverride = mat;

            return fogNode;
        }

        private static Node3D CreateElectricArc(Vector3 position, float radius, float intensity, Color color)
        {
            var arcNode = new Node3D();
            arcNode.Position = position;

            // Create a static arc using line segments
            var mesh = new ImmediateMesh();
            var meshInst = new MeshInstance3D();
            meshInst.Mesh = mesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 3f * intensity;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            meshInst.MaterialOverride = mat;

            // Draw a jagged arc
            mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
            int segments = 8;
            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)position.GetHashCode();
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float x = Mathf.Lerp(-radius, radius, t);
                float y = rng.RandfRange(-0.5f, 0.5f) * intensity;
                float z = rng.RandfRange(-0.3f, 0.3f) * intensity;
                mesh.SurfaceAddVertex(new Vector3(x, y, z));
            }
            mesh.SurfaceEnd();

            arcNode.AddChild(meshInst);
            return arcNode;
        }
    }
}
