using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Procedural geodesic hemisphere (frequency-2 icosahedron) built from CylinderMesh
    /// beams and SphereMesh joints. Forms an industrial truss dome overhead, like
    /// concert staging truss. Provides beam/joint data for spider pathfinding.
    /// </summary>
    public partial class TrussDome : Node3D
    {
        // ── Dome geometry ──
        private float _radius = 280f;
        private float _apexHeight = 80f;

        // ── Public pathfinding data ──
        public List<(Vector3 Start, Vector3 End)> Beams { get; } = new();
        public List<Vector3> Joints { get; } = new();
        public Dictionary<int, List<int>> Adjacency { get; } = new();

        // ── Materials ──
        private static readonly Color TRUSS_COLOR = new(0.06f, 0.05f, 0.08f);
        private static readonly Color JOINT_GLOW = new(0.4f, 0.15f, 0.6f); // AXIS purple
        private static readonly Color ACCENT_RED = new(1f, 0.12f, 0.08f);

        public void Initialize(float radius)
        {
            _radius = radius * 0.875f; // fit inside dungeon perimeter
            _apexHeight = _radius * 0.286f; // ~80 for 280 radius

            Name = "TrussDome";

            var vertices = GenerateGeodesicVertices();
            var edges = GenerateGeodesicEdges(vertices);

            // Store joints
            for (int i = 0; i < vertices.Count; i++)
            {
                Joints.Add(vertices[i]);
                Adjacency[i] = new List<int>();
            }

            // Build beams and adjacency
            var beamMat = MakeBeamMaterial(0.88f);
            var crossMat = MakeBeamMaterial(0.82f);

            for (int e = 0; e < edges.Count; e++)
            {
                var (a, b) = edges[e];
                var start = vertices[a];
                var end = vertices[b];
                Beams.Add((start, end));

                Adjacency[a].Add(e);
                Adjacency[b].Add(e);

                // Determine if main strut or cross-brace (shorter edges are cross-braces)
                float len = start.DistanceTo(end);
                float avgEdgeLen = _radius * 0.55f;
                bool isCross = len < avgEdgeLen * 0.85f;

                float beamRadius = isCross ? 0.25f : 0.4f;
                var mat = isCross ? crossMat : beamMat;
                BuildBeam(start, end, beamRadius, mat);
            }

            // Build joint spheres
            var jointMat = MakeJointMaterial();
            for (int i = 0; i < vertices.Count; i++)
            {
                BuildJoint(vertices[i], jointMat);
            }

            // Accent lights on every 4th-5th joint (lower hemisphere preferred)
            int lightInterval = Mathf.Max(1, vertices.Count / 6);
            for (int i = 0; i < vertices.Count; i += lightInterval)
            {
                var light = new OmniLight3D();
                light.LightColor = ACCENT_RED;
                light.LightEnergy = 0.4f;
                light.OmniRange = 25f;
                light.OmniAttenuation = 2f;
                light.ShadowEnabled = false;
                light.Position = vertices[i];
                AddChild(light);
            }

            GD.Print($"[TrussDome] Built: {Joints.Count} joints, {Beams.Count} beams, radius={_radius:F0}");
        }

        /// <summary>
        /// Generate vertices for a frequency-2 geodesic hemisphere.
        /// Starts with icosahedron, takes upper hemisphere, subdivides once.
        /// </summary>
        private List<Vector3> GenerateGeodesicVertices()
        {
            // Icosahedron vertices (unit sphere)
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var icoVerts = new List<Vector3>
            {
                new(-1,  t,  0), new( 1,  t,  0), new(-1, -t,  0), new( 1, -t,  0),
                new( 0, -1,  t), new( 0,  1,  t), new( 0, -1, -t), new( 0,  1, -t),
                new( t,  0, -1), new( t,  0,  1), new(-t,  0, -1), new(-t,  0,  1),
            };

            // Normalize to unit sphere
            for (int i = 0; i < icoVerts.Count; i++)
                icoVerts[i] = icoVerts[i].Normalized();

            // Icosahedron faces (triangles)
            var icoFaces = new List<(int, int, int)>
            {
                (0,11,5), (0,5,1), (0,1,7), (0,7,10), (0,10,11),
                (1,5,9), (5,11,4), (11,10,2), (10,7,6), (7,1,8),
                (3,9,4), (3,4,2), (3,2,6), (3,6,8), (3,8,9),
                (4,9,5), (2,4,11), (6,2,10), (8,6,7), (9,8,1),
            };

            // Subdivide each face once (frequency-2)
            var midpointCache = new Dictionary<long, int>();
            var verts = new List<Vector3>(icoVerts);
            var faces = new List<(int, int, int)>();

            foreach (var (a, b, c) in icoFaces)
            {
                int ab = GetMidpoint(verts, midpointCache, a, b);
                int bc = GetMidpoint(verts, midpointCache, b, c);
                int ca = GetMidpoint(verts, midpointCache, c, a);

                faces.Add((a, ab, ca));
                faces.Add((b, bc, ab));
                faces.Add((c, ca, bc));
                faces.Add((ab, bc, ca));
            }

            // Filter to upper hemisphere (Y >= -0.1 on unit sphere)
            // and project onto dome shape
            var hemisphereVerts = new List<Vector3>();
            var vertexMap = new Dictionary<int, int>(); // old index → new index

            for (int i = 0; i < verts.Count; i++)
            {
                if (verts[i].Y < -0.1f) continue;

                var v = verts[i].Normalized();
                // Project: XZ spread to radius, Y maps to dome height
                float heightFactor = Mathf.Max(0, v.Y);
                var domePos = new Vector3(
                    v.X * _radius,
                    heightFactor * _apexHeight,
                    v.Z * _radius
                );

                vertexMap[i] = hemisphereVerts.Count;
                hemisphereVerts.Add(domePos);
            }

            // Store face connectivity for edge extraction
            _hemisphereVertexMap = vertexMap;
            _subdividedFaces = faces;

            return hemisphereVerts;
        }

        private Dictionary<int, int> _hemisphereVertexMap;
        private List<(int, int, int)> _subdividedFaces;

        private List<(int, int)> GenerateGeodesicEdges(List<Vector3> vertices)
        {
            var edgeSet = new HashSet<long>();
            var edges = new List<(int, int)>();

            foreach (var (a, b, c) in _subdividedFaces)
            {
                TryAddEdge(a, b, edgeSet, edges);
                TryAddEdge(b, c, edgeSet, edges);
                TryAddEdge(c, a, edgeSet, edges);
            }

            return edges;
        }

        private void TryAddEdge(int a, int b, HashSet<long> edgeSet, List<(int, int)> edges)
        {
            // Both vertices must be in the hemisphere
            if (!_hemisphereVertexMap.TryGetValue(a, out int mappedA)) return;
            if (!_hemisphereVertexMap.TryGetValue(b, out int mappedB)) return;

            int lo = Mathf.Min(mappedA, mappedB);
            int hi = Mathf.Max(mappedA, mappedB);
            long key = ((long)lo << 32) | (long)hi;

            if (edgeSet.Add(key))
                edges.Add((mappedA, mappedB));
        }

        private static int GetMidpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            int lo = Mathf.Min(a, b);
            int hi = Mathf.Max(a, b);
            long key = ((long)lo << 32) | (long)hi;

            if (cache.TryGetValue(key, out int idx))
                return idx;

            var mid = ((verts[a] + verts[b]) * 0.5f).Normalized();
            idx = verts.Count;
            verts.Add(mid);
            cache[key] = idx;
            return idx;
        }

        // ── Mesh builders ──

        private void BuildBeam(Vector3 from, Vector3 to, float radius, StandardMaterial3D mat)
        {
            float length = from.DistanceTo(to);
            if (length < 0.1f) return;

            var mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = length,
                RadialSegments = 6,
            };

            var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };

            // Position at midpoint, orient along beam direction
            mi.Position = (from + to) * 0.5f;

            Vector3 dir = (to - from).Normalized();
            // CylinderMesh is along Y axis by default — rotate to align with beam direction
            if (dir.Dot(Vector3.Up) > 0.999f)
            {
                // Already aligned with Y
            }
            else if (dir.Dot(Vector3.Up) < -0.999f)
            {
                mi.RotationDegrees = new Vector3(180, 0, 0);
            }
            else
            {
                Vector3 axis = Vector3.Up.Cross(dir).Normalized();
                float angle = Mathf.Acos(Mathf.Clamp(Vector3.Up.Dot(dir), -1f, 1f));
                mi.Transform = new Transform3D(new Basis(axis, angle), mi.Position);
            }

            AddChild(mi);
        }

        private void BuildJoint(Vector3 position, StandardMaterial3D mat)
        {
            var mi = new MeshInstance3D();
            mi.Mesh = new SphereMesh { Radius = 0.6f, Height = 1.2f, RadialSegments = 8, Rings = 4 };
            mi.MaterialOverride = mat;
            mi.Position = position;
            AddChild(mi);
        }

        // ── Material helpers ──

        private StandardMaterial3D MakeBeamMaterial(float metallic)
        {
            return new StandardMaterial3D
            {
                AlbedoColor = TRUSS_COLOR,
                Metallic = metallic,
                Roughness = 0.3f,
                EmissionEnabled = true,
                Emission = JOINT_GLOW * 0.03f,
                EmissionEnergyMultiplier = 0.1f,
            };
        }

        private static StandardMaterial3D MakeJointMaterial()
        {
            return new StandardMaterial3D
            {
                AlbedoColor = new Color(0.04f, 0.03f, 0.06f),
                Metallic = 0.9f,
                Roughness = 0.2f,
                EmissionEnabled = true,
                Emission = JOINT_GLOW,
                EmissionEnergyMultiplier = 0.8f,
            };
        }

        /// <summary>
        /// Given a beam index, returns the joint index at the other end from the given joint.
        /// </summary>
        public int GetOtherJoint(int beamIndex, int fromJoint)
        {
            var (start, end) = Beams[beamIndex];
            // Find which joint indices match the beam endpoints
            for (int j = 0; j < Joints.Count; j++)
            {
                if (j == fromJoint) continue;
                if (Joints[j].DistanceSquaredTo(start) < 1f || Joints[j].DistanceSquaredTo(end) < 1f)
                    return j;
            }
            return fromJoint; // fallback
        }

        /// <summary>
        /// Precompute beam-to-joint mappings for fast lookup.
        /// Call after Initialize. Returns (jointA, jointB) for each beam.
        /// </summary>
        public List<(int, int)> GetBeamJointPairs()
        {
            var pairs = new List<(int, int)>();
            for (int b = 0; b < Beams.Count; b++)
            {
                var (start, end) = Beams[b];
                int ja = -1, jb = -1;
                for (int j = 0; j < Joints.Count; j++)
                {
                    if (ja < 0 && Joints[j].DistanceSquaredTo(start) < 1f) ja = j;
                    if (jb < 0 && Joints[j].DistanceSquaredTo(end) < 1f) jb = j;
                    if (ja >= 0 && jb >= 0) break;
                }
                pairs.Add((ja >= 0 ? ja : 0, jb >= 0 ? jb : 0));
            }
            return pairs;
        }
    }
}
