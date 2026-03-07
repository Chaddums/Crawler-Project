using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Manages three interconnected visual progression systems:
    /// 1. Visible Equipment — gear appears on the body
    /// 2. Progressive Growth — size + material upgrades on level-up
    /// 3. Keystone Ascension — major glow + accent piece on keystone allocation
    /// Added as child of PlayerController after class selection.
    /// </summary>
    public partial class VisualProgressionManager : Node
    {
        private PlayerController _player;
        private BotFrameType _className;
        private Node3D _bodyRoot;
        private PlayerInventory _inventory;

        // ── System 1: Visible Equipment ──
        private readonly Dictionary<EquipmentSlot, Node3D> _mountedGear = new();
        private Node3D _defaultWeapon;

        // Slots that get visual models
        private static readonly HashSet<EquipmentSlot> VisualSlots = new()
        {
            EquipmentSlot.MainHand,
            EquipmentSlot.OffHand,
            EquipmentSlot.Head,
            EquipmentSlot.Chest,
            EquipmentSlot.Legs,
            EquipmentSlot.Feet,
            EquipmentSlot.Hands,
            EquipmentSlot.Back
        };

        // ── System 2: Progressive Growth ──
        private int _currentLevel = 1;
        private float _currentScale = 1.0f;
        private int _currentTier;

        private const float MinScale = 1.0f;
        private const float MaxScale = 1.3f;
        private const int MaxScaleLevel = 20;

        // ── System 3: Keystone Ascension ──
        private bool _keystoneActive;
        private Node3D _accentPiece;

        public void Initialize(PlayerController player, BotFrameType className)
        {
            _player = player;
            _className = className;
            _bodyRoot = player.BodyRoot;
            _inventory = player.Inventory;

            // Cache default weapon node
            _defaultWeapon = _bodyRoot?.GetNodeOrNull<Node3D>("Weapon");

            // Subscribe to events
            if (_inventory != null)
                _inventory.OnEquipmentChanged += OnEquipmentChanged;

            GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;
            GameEvents.OnPassiveNodeAllocated += OnPassiveNodeAllocated;
            GameEvents.OnPassiveTreeReset += OnPassiveTreeReset;

            GD.Print("[VisualProgression] Initialized");
        }

        public override void _ExitTree()
        {
            if (_inventory != null)
                _inventory.OnEquipmentChanged -= OnEquipmentChanged;

            GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
            GameEvents.OnPassiveNodeAllocated -= OnPassiveNodeAllocated;
            GameEvents.OnPassiveTreeReset -= OnPassiveTreeReset;
        }

        // =====================================================================
        //  SYSTEM 1: Visible Equipment
        // =====================================================================

        private void OnEquipmentChanged(EquipmentSlot slot, ItemInstance item)
        {
            if (!VisualSlots.Contains(slot)) return;
            if (_bodyRoot == null) return;

            // Remove existing gear model for this slot
            if (_mountedGear.TryGetValue(slot, out var existing))
            {
                existing.QueueFree();
                _mountedGear.Remove(slot);
            }

            if (item == null)
            {
                // Unequipped — restore default weapon if MainHand
                if (slot == EquipmentSlot.MainHand && _defaultWeapon != null)
                    _defaultWeapon.Visible = true;
                return;
            }

            // Build and mount the gear model
            var model = CharacterMeshBuilder.BuildItemModel(item);
            if (model == null) return;

            var pivot = GetPivotForSlot(slot);
            if (pivot == null)
            {
                model.QueueFree();
                return;
            }

            model.Name = $"Equipped_{slot}";
            ApplySlotTransform(model, slot);
            pivot.AddChild(model);
            _mountedGear[slot] = model;

            // Hide default blaster when MainHand is equipped
            if (slot == EquipmentSlot.MainHand && _defaultWeapon != null)
                _defaultWeapon.Visible = false;
        }

        private Node3D GetPivotForSlot(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.MainHand => _bodyRoot.GetNodeOrNull<Node3D>("RightArm"),
                EquipmentSlot.OffHand => _bodyRoot.GetNodeOrNull<Node3D>("LeftArm"),
                EquipmentSlot.Head => _bodyRoot.GetNodeOrNull<Node3D>("Head"),
                EquipmentSlot.Chest => _bodyRoot.GetNodeOrNull<Node3D>("Torso"),
                EquipmentSlot.Legs => _bodyRoot.GetNodeOrNull<Node3D>("LeftLeg"),
                EquipmentSlot.Feet => _bodyRoot.GetNodeOrNull<Node3D>("LeftLeg"),
                EquipmentSlot.Hands => _bodyRoot.GetNodeOrNull<Node3D>("RightArm"),
                EquipmentSlot.Back => _bodyRoot.GetNodeOrNull<Node3D>("Torso"),
                _ => null
            };
        }

        private static void ApplySlotTransform(Node3D model, EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.MainHand:
                    model.Position = new Vector3(0, -0.15f, -0.1f);
                    model.Scale = Vector3.One * 0.8f;
                    break;
                case EquipmentSlot.OffHand:
                    model.Position = new Vector3(0, -0.15f, -0.1f);
                    model.Scale = Vector3.One * 0.7f;
                    break;
                case EquipmentSlot.Head:
                    model.Position = new Vector3(0, 0.12f, 0);
                    model.Scale = Vector3.One * 0.9f;
                    break;
                case EquipmentSlot.Chest:
                    model.Position = new Vector3(0, 0, -0.08f);
                    model.Scale = Vector3.One * 0.85f;
                    break;
                case EquipmentSlot.Legs:
                    model.Position = new Vector3(0, -0.05f, -0.03f);
                    model.Scale = Vector3.One * 0.8f;
                    break;
                case EquipmentSlot.Feet:
                    model.Position = new Vector3(0, -0.15f, -0.02f);
                    model.Scale = Vector3.One * 0.75f;
                    break;
                case EquipmentSlot.Hands:
                    model.Position = new Vector3(0, -0.2f, -0.05f);
                    model.Scale = Vector3.One * 0.7f;
                    break;
                case EquipmentSlot.Back:
                    model.Position = new Vector3(0, 0.05f, 0.15f);
                    model.Scale = Vector3.One * 0.75f;
                    break;
            }
        }

        // =====================================================================
        //  SYSTEM 2: Progressive Growth
        // =====================================================================

        private void OnPlayerLevelUp(int level)
        {
            _currentLevel = level;
            ApplyGrowth();
        }

        private void ApplyGrowth()
        {
            if (_bodyRoot == null) return;

            // Scale: linear lerp from 1.0 (lv1) to 1.3 (lv20+)
            float t = Mathf.Clamp((_currentLevel - 1) / (float)(MaxScaleLevel - 1), 0f, 1f);
            float targetScale = Mathf.Lerp(MinScale, MaxScale, t);
            if (_keystoneActive)
                targetScale += 0.06f;

            AnimateScale(targetScale);

            // Material tier
            int tier = _currentLevel switch
            {
                < 5 => 0,
                < 10 => 1,
                < 15 => 2,
                < 20 => 3,
                _ => 4
            };

            if (tier != _currentTier || _keystoneActive)
            {
                _currentTier = tier;
                ApplyMaterialTier(tier);
            }
        }

        private void AnimateScale(float targetScale)
        {
            if (Mathf.Abs(_currentScale - targetScale) < 0.001f) return;

            var tween = CreateTween();
            tween.TweenProperty(_bodyRoot, "scale",
                Vector3.One * targetScale, 0.4f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            tween.TweenCallback(Callable.From(() =>
            {
                _currentScale = targetScale;
                _player?.ReinitializeAnimator();
            }));
        }

        private void ApplyMaterialTier(int tier)
        {
            if (_bodyRoot == null) return;

            foreach (var mesh in GetAllMeshes(_bodyRoot))
            {
                if (mesh.MaterialOverride is not StandardMaterial3D mat) continue;

                switch (tier)
                {
                    case 0: // Flat matte (default)
                        mat.Metallic = 0f;
                        mat.Roughness = 0.9f;
                        mat.EmissionEnabled = false;
                        break;
                    case 1: // Slight metallic sheen
                        mat.Metallic = 0.3f;
                        mat.Roughness = 0.7f;
                        mat.EmissionEnabled = false;
                        break;
                    case 2: // Glossy, saturated
                        mat.Metallic = 0.5f;
                        mat.Roughness = 0.4f;
                        mat.EmissionEnabled = false;
                        break;
                    case 3: // Faint emissive glow
                        mat.Metallic = 0.6f;
                        mat.Roughness = 0.3f;
                        mat.EmissionEnabled = true;
                        mat.Emission = mat.AlbedoColor * 0.3f;
                        mat.EmissionEnergyMultiplier = 0.5f;
                        break;
                    case 4: // Strong emissive accents
                        mat.Metallic = 0.7f;
                        mat.Roughness = 0.2f;
                        mat.EmissionEnabled = true;
                        mat.Emission = mat.AlbedoColor * 0.6f;
                        mat.EmissionEnergyMultiplier = 1.2f;
                        break;
                }

                // Override with keystone glow when active
                if (_keystoneActive)
                    ApplyKeystoneGlow(mat);
            }
        }

        // =====================================================================
        //  SYSTEM 3: Keystone Ascension
        // =====================================================================

        private void OnPassiveNodeAllocated(string nodeId)
        {
            var keystoneId = $"ks_{_className}";
            if (nodeId != keystoneId) return;

            _keystoneActive = true;
            ApplyGrowth(); // Refreshes scale (+0.06) and materials with glow
            SpawnAccentPiece();

            GD.Print($"[VisualProgression] Keystone ascension: {_className}");
        }

        private void OnPassiveTreeReset()
        {
            if (!_keystoneActive) return;

            _keystoneActive = false;

            // Remove accent piece
            _accentPiece?.QueueFree();
            _accentPiece = null;

            // Revert visuals to current level tier
            ApplyGrowth();

            GD.Print("[VisualProgression] Keystone reverted (tree reset)");
        }

        private void ApplyKeystoneGlow(StandardMaterial3D mat)
        {
            var classColor = CharacterMeshBuilder.GetClassColor(_className);
            mat.EmissionEnabled = true;
            mat.Emission = classColor;
            mat.EmissionEnergyMultiplier = 2.0f;
            mat.Metallic = 0.8f;
            mat.Roughness = 0.15f;
        }

        private void SpawnAccentPiece()
        {
            _accentPiece?.QueueFree();

            var classColor = CharacterMeshBuilder.GetClassColor(_className);
            _accentPiece = _className switch
            {
                BotFrameType.Scrapheap => BuildShoulderSpikes(classColor),
                BotFrameType.TinCan => BuildHoloProjector(classColor),
                BotFrameType.SparkPlug => BuildTeslaCoilRing(classColor),
                BotFrameType.RustBucket => BuildCloakingEmitter(classColor),
                BotFrameType.NoiseBox => BuildResonanceCrown(classColor),
                BotFrameType.Clunker => BuildFistAuras(classColor),
                _ => null
            };

            if (_accentPiece == null) return;

            var pivot = GetAccentPivot();
            if (pivot != null)
            {
                pivot.AddChild(_accentPiece);
            }
            else
            {
                _accentPiece.QueueFree();
                _accentPiece = null;
            }
        }

        private Node3D GetAccentPivot()
        {
            if (_bodyRoot == null) return null;

            return _className switch
            {
                BotFrameType.Scrapheap => _bodyRoot.GetNodeOrNull<Node3D>("Torso"),
                BotFrameType.TinCan => _bodyRoot.GetNodeOrNull<Node3D>("Torso"),
                BotFrameType.SparkPlug => _bodyRoot.GetNodeOrNull<Node3D>("Torso"),
                BotFrameType.RustBucket => _bodyRoot.GetNodeOrNull<Node3D>("Torso"),
                BotFrameType.NoiseBox => _bodyRoot.GetNodeOrNull<Node3D>("Head"),
                BotFrameType.Clunker => _bodyRoot, // fist auras go on the body root
                _ => _bodyRoot
            };
        }

        // ── Accent Piece Builders ──

        private static Node3D BuildShoulderSpikes(Color color)
        {
            var root = new Node3D { Name = "Accent_ShoulderSpikes" };

            // Left spike
            var leftSpike = CreateEmissiveMesh("LeftSpike",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.06f, Height = 0.25f, RadialSegments = 6 },
                color, new Vector3(-0.25f, 0.15f, 0));
            leftSpike.RotationDegrees = new Vector3(0, 0, 25f);
            root.AddChild(leftSpike);

            // Right spike
            var rightSpike = CreateEmissiveMesh("RightSpike",
                new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.06f, Height = 0.25f, RadialSegments = 6 },
                color, new Vector3(0.25f, 0.15f, 0));
            rightSpike.RotationDegrees = new Vector3(0, 0, -25f);
            root.AddChild(rightSpike);

            return root;
        }

        private static Node3D BuildHoloProjector(Color color)
        {
            var root = new Node3D { Name = "Accent_HoloProjector" };

            // Base disc
            var disc = CreateEmissiveMesh("Disc",
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.02f, RadialSegments = 12 },
                color, new Vector3(0, 0.2f, 0));
            root.AddChild(disc);

            // Projection beam (thin cylinder upward)
            var beam = CreateEmissiveMesh("Beam",
                new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.05f, Height = 0.15f, RadialSegments = 8 },
                color * 1.5f, new Vector3(0, 0.3f, 0));
            root.AddChild(beam);

            return root;
        }

        private static Node3D BuildTeslaCoilRing(Color color)
        {
            var root = new Node3D { Name = "Accent_TeslaRing" };

            // Outer ring (torus approximated with a flat cylinder)
            var ring = CreateEmissiveMesh("Ring",
                new TorusMesh { InnerRadius = 0.15f, OuterRadius = 0.2f, Rings = 16, RingSegments = 12 },
                color, new Vector3(0, 0.25f, 0));
            root.AddChild(ring);

            // Central spark sphere
            var spark = CreateEmissiveMesh("Spark",
                new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 4 },
                color * 2f, new Vector3(0, 0.25f, 0));
            root.AddChild(spark);

            return root;
        }

        private static Node3D BuildCloakingEmitter(Color color)
        {
            var root = new Node3D { Name = "Accent_CloakEmitter" };

            // Flat disc on the back
            var disc = CreateEmissiveMesh("EmitterDisc",
                new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.015f, RadialSegments = 16 },
                color, new Vector3(0, 0.05f, 0.18f));
            disc.RotationDegrees = new Vector3(90f, 0, 0);
            root.AddChild(disc);

            // Inner ring
            var inner = CreateEmissiveMesh("InnerRing",
                new TorusMesh { InnerRadius = 0.05f, OuterRadius = 0.08f, Rings = 12, RingSegments = 8 },
                color * 1.5f, new Vector3(0, 0.05f, 0.19f));
            inner.RotationDegrees = new Vector3(90f, 0, 0);
            root.AddChild(inner);

            return root;
        }

        private static Node3D BuildResonanceCrown(Color color)
        {
            var root = new Node3D { Name = "Accent_ResonanceCrown" };

            // Floating crown ring above head
            var crown = CreateEmissiveMesh("Crown",
                new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.14f, Rings = 16, RingSegments = 8 },
                color, new Vector3(0, 0.3f, 0));
            root.AddChild(crown);

            // Three small orbiting spheres
            for (int i = 0; i < 3; i++)
            {
                float angle = Mathf.DegToRad(120f * i);
                var orb = CreateEmissiveMesh($"Orb_{i}",
                    new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 6, Rings = 3 },
                    color * 1.8f,
                    new Vector3(Mathf.Cos(angle) * 0.12f, 0.3f, Mathf.Sin(angle) * 0.12f));
                root.AddChild(orb);
            }

            return root;
        }

        private static Node3D BuildFistAuras(Color color)
        {
            var root = new Node3D { Name = "Accent_FistAuras" };

            // Left fist glow
            var leftGlow = CreateEmissiveMesh("LeftFistGlow",
                new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 8, Rings = 4 },
                color, Vector3.Zero);
            root.AddChild(leftGlow);

            // Right fist glow — offset so the parent (bodyRoot) positions them on each arm
            var rightGlow = CreateEmissiveMesh("RightFistGlow",
                new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 8, Rings = 4 },
                color, Vector3.Zero);
            root.AddChild(rightGlow);

            // Position relative to body root — attach to arms at runtime
            var leftArm = root.GetParent()?.GetNodeOrNull<Node3D>("LeftArm");
            var rightArm = root.GetParent()?.GetNodeOrNull<Node3D>("RightArm");

            // Since we can't access arms yet (not in tree), set positions for body-root-relative
            leftGlow.Position = new Vector3(-0.25f, -0.1f, -0.15f);
            rightGlow.Position = new Vector3(0.25f, -0.1f, -0.15f);

            return root;
        }

        // ── Helpers ──

        private static MeshInstance3D CreateEmissiveMesh(string name, Mesh mesh, Color color, Vector3 position)
        {
            var node = new MeshInstance3D();
            node.Name = name;
            node.Mesh = mesh;
            node.Position = position;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 1.5f;
            mat.Metallic = 0.7f;
            mat.Roughness = 0.2f;
            node.MaterialOverride = mat;

            return node;
        }

        private static List<MeshInstance3D> GetAllMeshes(Node root)
        {
            var meshes = new List<MeshInstance3D>();
            CollectMeshes(root, meshes);
            return meshes;
        }

        private static void CollectMeshes(Node node, List<MeshInstance3D> list)
        {
            if (node is MeshInstance3D mesh)
                list.Add(mesh);
            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    CollectMeshes(n, list);
            }
        }
    }
}
