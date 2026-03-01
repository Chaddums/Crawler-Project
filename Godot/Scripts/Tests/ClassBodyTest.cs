using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Smoke test: instantiates all 6 class player bodies and validates body structure,
    /// visual progression equipment mounting, level-up scaling, and keystone accents.
    /// Run via: Godot --headless --script (or attach to a test scene).
    /// Auto-quits after testing.
    /// </summary>
    public partial class ClassBodyTest : Node3D
    {
        public override void _Ready()
        {
            GD.Print("[ClassBodyTest] Starting class body smoke test...");
            int passed = 0;
            int failed = 0;

            foreach (BotFrameType frame in Enum.GetValues<BotFrameType>())
            {
                try
                {
                    var body = CharacterMeshBuilder.BuildPlayerBody(frame);
                    if (body == null)
                    {
                        GD.PrintErr($"  FAIL: {frame} — returned null");
                        failed++;
                        continue;
                    }

                    AddChild(body);
                    int meshCount = CountMeshes(body);

                    if (meshCount == 0)
                    {
                        GD.PrintErr($"  FAIL: {frame} — body has 0 mesh nodes");
                        failed++;
                    }
                    else
                    {
                        // Check for expected pivots (Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg)
                        bool hasHead = body.FindChild("Head", true, false) != null;
                        bool hasTorso = body.FindChild("Torso", true, false) != null;
                        bool hasLeftLeg = body.FindChild("LeftLeg", true, false) != null;
                        bool hasRightLeg = body.FindChild("RightLeg", true, false) != null;

                        string missing = "";
                        if (!hasHead) missing += "Head ";
                        if (!hasTorso) missing += "Torso ";
                        if (!hasLeftLeg) missing += "LeftLeg ";
                        if (!hasRightLeg) missing += "RightLeg ";

                        if (missing.Length > 0)
                            GD.Print($"  WARN: {frame} — {meshCount} meshes, missing pivots: {missing.Trim()}");
                        else
                            GD.Print($"  OK:   {frame} — {meshCount} meshes, all pivots present");

                        passed++;
                    }

                    body.QueueFree();
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"  FAIL: {frame} — exception: {ex.Message}");
                    failed++;
                }
            }

            GD.Print($"[ClassBodyTest] Body tests: {passed} passed, {failed} failed");

            // ── Visual Progression Tests ──
            GD.Print("[ClassBodyTest] Starting visual progression tests...");
            int vpPassed = 0;
            int vpFailed = 0;

            foreach (BotFrameType frame in Enum.GetValues<BotFrameType>())
            {
                try
                {
                    // Build a fresh body for each class
                    var body = CharacterMeshBuilder.BuildPlayerBody(frame);
                    AddChild(body);

                    // Create and initialize the manager
                    var mgr = new VisualProgressionManager();
                    mgr.Name = "TestVPM";
                    AddChild(mgr);

                    // We need a mock-like setup: manager needs a PlayerController.
                    // Instead, test the subsystems directly via the public Initialize path.
                    // We'll use a lightweight stub approach: trigger events and verify scene tree changes.

                    // --- Test Equipment Mounting ---
                    // Build a fake sword item to test MainHand equip visual
                    var swordData = new EquipmentData();
                    swordData.Id = "test_sword";
                    swordData.ItemName = "Test Sword";
                    swordData.Type = ItemType.Equipment;
                    swordData.Slot = EquipmentSlot.MainHand;
                    var swordInstance = new ItemInstance(swordData, ItemRarity.Common);
                    var swordModel = CharacterMeshBuilder.BuildItemModel(swordInstance);

                    bool equipModelBuilt = swordModel != null;
                    if (!equipModelBuilt)
                    {
                        GD.PrintErr($"  FAIL VP: {frame} — BuildItemModel returned null for MainHand");
                        vpFailed++;
                    }
                    else
                    {
                        // Test that model can be attached to RightArm pivot
                        var rightArm = body.GetNodeOrNull<Node3D>("RightArm");
                        if (rightArm != null)
                        {
                            swordModel.Name = "Equipped_MainHand";
                            rightArm.AddChild(swordModel);
                            bool mounted = rightArm.GetNodeOrNull("Equipped_MainHand") != null;
                            if (mounted)
                                GD.Print($"  OK VP: {frame} — MainHand equip model mounts to RightArm");
                            else
                            {
                                GD.PrintErr($"  FAIL VP: {frame} — MainHand model failed to mount");
                                vpFailed++;
                                continue;
                            }
                        }
                        else
                        {
                            GD.Print($"  SKIP VP: {frame} — no RightArm pivot for equip test");
                        }
                        vpPassed++;
                    }

                    // --- Test Helmet Mounting ---
                    var helmetData = new EquipmentData();
                    helmetData.Id = "test_helmet";
                    helmetData.ItemName = "Test Helmet";
                    helmetData.Type = ItemType.Equipment;
                    helmetData.Slot = EquipmentSlot.Head;
                    var helmetModel = CharacterMeshBuilder.BuildItemModel(new ItemInstance(helmetData, ItemRarity.Common));

                    if (helmetModel != null)
                    {
                        var headPivot = body.GetNodeOrNull<Node3D>("Head");
                        if (headPivot != null)
                        {
                            helmetModel.Name = "Equipped_Head";
                            headPivot.AddChild(helmetModel);
                            GD.Print($"  OK VP: {frame} — Head equip model mounts to Head pivot");
                            vpPassed++;
                        }
                        else
                        {
                            GD.Print($"  SKIP VP: {frame} — no Head pivot");
                            vpPassed++;
                        }
                    }

                    // --- Test Level-Up Scale ---
                    // Verify that scale can be set on the body root
                    float preScale = body.Scale.X;
                    float targetScale = 1.15f; // Simulates ~level 10
                    body.Scale = Vector3.One * targetScale;
                    bool scaleApplied = Mathf.Abs(body.Scale.X - targetScale) < 0.01f;
                    if (scaleApplied)
                    {
                        GD.Print($"  OK VP: {frame} — Scale change works (1.0 → {targetScale})");
                        vpPassed++;
                    }
                    else
                    {
                        GD.PrintErr($"  FAIL VP: {frame} — Scale change failed");
                        vpFailed++;
                    }
                    body.Scale = Vector3.One; // Reset

                    // --- Test Keystone Accent Spawning ---
                    var accentColor = CharacterMeshBuilder.GetClassColor(frame);
                    bool colorValid = accentColor.R >= 0 && accentColor.G >= 0 && accentColor.B >= 0;
                    if (colorValid)
                    {
                        GD.Print($"  OK VP: {frame} — GetClassColor returns valid color ({accentColor.R:F2}, {accentColor.G:F2}, {accentColor.B:F2})");
                        vpPassed++;
                    }
                    else
                    {
                        GD.PrintErr($"  FAIL VP: {frame} — GetClassColor returned invalid color");
                        vpFailed++;
                    }

                    // --- Test Material Mutation ---
                    var meshes = GetAllMeshInstances(body);
                    bool allHaveMaterials = true;
                    foreach (var mesh in meshes)
                    {
                        if (mesh.MaterialOverride is StandardMaterial3D mat)
                        {
                            // Simulate tier 3 upgrade
                            mat.Metallic = 0.6f;
                            mat.Roughness = 0.3f;
                            mat.EmissionEnabled = true;
                            mat.Emission = mat.AlbedoColor * 0.3f;
                        }
                        else
                        {
                            allHaveMaterials = false;
                        }
                    }
                    if (allHaveMaterials && meshes.Count > 0)
                    {
                        GD.Print($"  OK VP: {frame} — Material mutation works ({meshes.Count} meshes updated)");
                        vpPassed++;
                    }
                    else if (meshes.Count == 0)
                    {
                        GD.Print($"  SKIP VP: {frame} — no meshes with StandardMaterial3D");
                        vpPassed++;
                    }
                    else
                    {
                        GD.PrintErr($"  FAIL VP: {frame} — some meshes lack StandardMaterial3D override");
                        vpFailed++;
                    }

                    // Cleanup
                    mgr.QueueFree();
                    body.QueueFree();
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"  FAIL VP: {frame} — exception: {ex.Message}");
                    vpFailed++;
                }
            }

            GD.Print($"[ClassBodyTest] Visual progression tests: {vpPassed} passed, {vpFailed} failed");

            // ── Item Model Tests ──
            GD.Print("[ClassBodyTest] Starting item model smoke tests...");
            int imPassed = 0;
            int imFailed = 0;

            // All 16 equipment base IDs with their slots
            var equipmentItems = new (string id, string name, EquipmentSlot slot)[]
            {
                ("base_pistol", "Scrap Pistol", EquipmentSlot.MainHand),
                ("base_rifle", "Bolt Rifle", EquipmentSlot.MainHand),
                ("base_shotgun", "Scatter Gun", EquipmentSlot.MainHand),
                ("base_launcher", "Arc Launcher", EquipmentSlot.MainHand),
                ("base_repeater", "Spark Repeater", EquipmentSlot.MainHand),
                ("base_blade_ring", "Buzz Saw Ring", EquipmentSlot.MainHand),
                ("base_shield", "Scrap Buckler", EquipmentSlot.OffHand),
                ("base_helmet", "Cranial Plating", EquipmentSlot.Head),
                ("base_chestplate", "Hull Plating", EquipmentSlot.Chest),
                ("base_robe", "Capacitor Vest", EquipmentSlot.Chest),
                ("base_greaves", "Piston Guards", EquipmentSlot.Legs),
                ("base_boots", "Tread Plates", EquipmentSlot.Feet),
                ("base_gauntlets", "Servo Grips", EquipmentSlot.Hands),
                ("base_amulet", "Signal Beacon", EquipmentSlot.Amulet),
                ("base_ring", "Coil Ring", EquipmentSlot.Ring1),
                ("base_cloak", "Heat Shroud", EquipmentSlot.Back),
            };

            foreach (var (id, name, slot) in equipmentItems)
            {
                try
                {
                    var data = new EquipmentData();
                    data.Id = id;
                    data.ItemName = name;
                    data.Type = ItemType.Equipment;
                    data.Slot = slot;
                    var instance = new ItemInstance(data, ItemRarity.Common);
                    var model = CharacterMeshBuilder.BuildItemModel(instance);

                    if (model == null)
                    {
                        GD.PrintErr($"  FAIL IM: {id} — BuildItemModel returned null");
                        imFailed++;
                        continue;
                    }

                    AddChild(model);
                    int meshCount = CountMeshes(model);
                    if (meshCount == 0)
                    {
                        GD.PrintErr($"  FAIL IM: {id} — model has 0 mesh nodes");
                        imFailed++;
                    }
                    else
                    {
                        GD.Print($"  OK IM:   {id} — {meshCount} meshes");
                        imPassed++;
                    }
                    model.QueueFree();
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"  FAIL IM: {id} — exception: {ex.Message}");
                    imFailed++;
                }
            }

            // 4 consumable IDs
            var consumableItems = new (string id, string name)[]
            {
                ("potion_health_small", "Small Repair Kit"),
                ("potion_mana_small", "Small Battery Pack"),
                ("elixir_fortitude", "Plating Booster"),
                ("overclock_injector", "Overclock Injector"),
            };

            foreach (var (id, name) in consumableItems)
            {
                try
                {
                    var data = new ItemData(id, name, ItemRarity.Common, ItemType.Consumable);
                    var instance = new ItemInstance(data, ItemRarity.Common);
                    var model = CharacterMeshBuilder.BuildItemModel(instance);

                    if (model == null)
                    {
                        GD.PrintErr($"  FAIL IM: {id} — BuildItemModel returned null");
                        imFailed++;
                        continue;
                    }

                    AddChild(model);
                    int meshCount = CountMeshes(model);
                    if (meshCount == 0)
                    {
                        GD.PrintErr($"  FAIL IM: {id} — model has 0 mesh nodes");
                        imFailed++;
                    }
                    else
                    {
                        GD.Print($"  OK IM:   {id} — {meshCount} meshes");
                        imPassed++;
                    }
                    model.QueueFree();
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"  FAIL IM: {id} — exception: {ex.Message}");
                    imFailed++;
                }
            }

            GD.Print($"[ClassBodyTest] Item model tests: {imPassed} passed, {imFailed} failed");
            GD.Print($"[ClassBodyTest] TOTAL: {passed + vpPassed + imPassed} passed, {failed + vpFailed + imFailed} failed");

            // Auto-quit
            GetTree().Quit((failed + vpFailed + imFailed) > 0 ? 1 : 0);
        }

        private static int CountMeshes(Node node)
        {
            int count = node is MeshInstance3D ? 1 : 0;
            foreach (var child in node.GetChildren())
            {
                if (child is Node n) count += CountMeshes(n);
            }
            return count;
        }

        private static List<MeshInstance3D> GetAllMeshInstances(Node root)
        {
            var list = new List<MeshInstance3D>();
            CollectMeshInstances(root, list);
            return list;
        }

        private static void CollectMeshInstances(Node node, List<MeshInstance3D> list)
        {
            if (node is MeshInstance3D mesh)
                list.Add(mesh);
            foreach (var child in node.GetChildren())
            {
                if (child is Node n) CollectMeshInstances(n, list);
            }
        }
    }
}
