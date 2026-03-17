using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Builds the AXIS boss visual — a spider mech standing on a dramatic arena platform.
    /// Loads the RetroMech ISO Mech FBX, applies dark metallic AXIS materials.
    /// Falls back to procedural geometry when the FBX isn't available.
    /// </summary>
    public static class AxisBossBody
    {
        // AXIS color palette — brightened for SubViewport visibility (no env reflections)
        private static readonly Color CorePurple = new(0.5f, 0.25f, 0.7f);
        private static readonly Color DarkPlate = new(0.3f, 0.27f, 0.38f);
        private static readonly Color HoloBlue = new(0.3f, 0.7f, 1f);
        private static readonly Color EyeRed = new(1f, 0.15f, 0.1f);
        private static readonly Color Gold = new(0.85f, 0.7f, 0.2f);
        private static readonly Color DataGreen = new(0.2f, 1f, 0.4f);
        private static readonly Color ArmorMetal = new(0.4f, 0.36f, 0.48f);
        private static readonly Color WeaponMetal = new(0.45f, 0.38f, 0.55f);

        /// <summary>
        /// Build the complete AXIS boss body with materials and arena.
        /// </summary>
        public static Node3D Build()
        {
            var root = new Node3D();
            root.Name = "AxisBossBody";

            // ── Try PolygonMech FBX first ──
            var mechModel = ModelLibrary.TryLoad("boss", "axis_avatar");
            if (mechModel != null && HasAnyMesh(mechModel))
            {
                mechModel.Name = "MechModel";

                // AABB-based scaling — use HEIGHT not maxDim so the spider mech
                // isn't squished by its wide leg span.  Target: 12 units tall.
                ScaleModelToHeight(mechModel, 12f);

                // Face the player (-Z)
                mechModel.RotateY(Mathf.Pi);

                // Play the first available animation to escape T-pose / rest pose
                PlayMechAnimation(mechModel);

                // Map FBX bones for pivot support
                FbxPivotMapper.MapHierarchy(mechModel);

                // Let the FBX's own embedded materials (red/yellow/black) render.
                // Do NOT override with custom materials — the SK_ISO_Mech FBX has
                // correct PBR materials baked in.

                // Position at ground level (no platform)
                mechModel.Position = new Vector3(0, 0f, 0);
                root.AddChild(mechModel);

                return root;
            }

            if (mechModel != null)
                mechModel.QueueFree();

            // ── Procedural fallback ──
            root.AddChild(BuildRift());
            root.AddChild(BuildTorso());
            root.AddChild(BuildHead());
            root.AddChild(BuildLeftHand());
            root.AddChild(BuildRightHand());
            root.AddChild(BuildAmbientEffects());

            return root;
        }

        /// <summary>
        /// Apply dark metallic AXIS materials to all MeshInstance3D nodes in the mech.
        /// Parts are categorized by node name for varied material treatment.
        /// </summary>
        private static void ApplyAXISMaterials(Node3D model)
        {
            int count = 0;
            ApplyMaterialsRecursive(model, ref count);
        }

        private static void ApplyMaterialsRecursive(Node node, ref int count)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                string name = mi.Name.ToString().ToLower();
                StandardMaterial3D mat;

                if (name.Contains("head") || name.Contains("cockpit"))
                {
                    // Head/cockpit — red visor accent
                    mat = MakeMetalMat(ArmorMetal, 0.3f, 0.45f);
                    mat.EmissionEnabled = true;
                    mat.Emission = EyeRed;
                    mat.EmissionEnergyMultiplier = 0.5f;
                }
                else if (name.Contains("weapon") || name.Contains("launcher"))
                {
                    // Weapons — purple accent
                    mat = MakeMetalMat(WeaponMetal, 0.3f, 0.5f);
                    mat.EmissionEnabled = true;
                    mat.Emission = CorePurple;
                    mat.EmissionEnergyMultiplier = 0.6f;
                }
                else if (name.Contains("exhaust") || name.Contains("jetpack") || name.Contains("intake"))
                {
                    // Exhaust/jets — warm glow
                    mat = MakeMetalMat(new Color(0.35f, 0.28f, 0.2f), 0.25f, 0.5f);
                    mat.EmissionEnabled = true;
                    mat.Emission = new Color(0.8f, 0.3f, 0.1f);
                    mat.EmissionEnergyMultiplier = 1.0f;
                }
                else if (name.Contains("armor") || name.Contains("shield"))
                {
                    // Armor plates — purple edge glow
                    mat = MakeMetalMat(ArmorMetal.Lightened(0.05f), 0.3f, 0.5f);
                    mat.EmissionEnabled = true;
                    mat.Emission = CorePurple;
                    mat.EmissionEnergyMultiplier = 0.25f;
                }
                else if (name.Contains("collar") || name.Contains("belt") || name.Contains("radio"))
                {
                    // Accessories — lighter metal
                    mat = MakeMetalMat(DarkPlate, 0.3f, 0.5f);
                    mat.EmissionEnabled = true;
                    mat.Emission = CorePurple;
                    mat.EmissionEnergyMultiplier = 0.1f;
                }
                else
                {
                    // Default — visible gunmetal with subtle emission
                    mat = MakeMetalMat(ArmorMetal, 0.3f, 0.5f);
                    mat.EmissionEnabled = true;
                    mat.Emission = CorePurple;
                    mat.EmissionEnergyMultiplier = 0.1f;
                }

                mi.MaterialOverride = mat;
                count++;
            }

            foreach (Node child in node.GetChildren())
                ApplyMaterialsRecursive(child, ref count);
        }

        private static StandardMaterial3D MakeMetalMat(Color color, float metallic, float roughness)
        {
            return new StandardMaterial3D
            {
                AlbedoColor = color,
                Metallic = metallic,
                Roughness = roughness
            };
        }

        /// <summary>
        /// Play the FBX animation if available. Plays the first animation
        /// to get out of rest pose.
        /// </summary>
        private static void PlayMechAnimation(Node3D model)
        {
            var animPlayer = FindNode<AnimationPlayer>(model);
            if (animPlayer == null) return;

            var anims = animPlayer.GetAnimationList();
            if (anims.Length == 0) return;

            // Play the first available animation
            string animName = anims[0];
            animPlayer.Play(animName);

            // If it's a single-frame pose, pause it on the first frame
            if (anims.Length == 1)
            {
                animPlayer.Seek(0, true);
                animPlayer.Pause();
            }
        }

        private static T FindNode<T>(Node root) where T : Node
        {
            if (root is T t) return t;
            foreach (Node child in root.GetChildren())
            {
                var found = FindNode<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Scale a model so its AABB HEIGHT matches targetHeight.
        /// Unlike CharacterMeshBuilder.ScaleModelToFit (which uses maxDim),
        /// this uses the Y dimension so wide spider mechs don't end up tiny.
        /// </summary>
        private static void ScaleModelToHeight(Node3D model, float targetHeight)
        {
            var aabb = CharacterMeshBuilder.GetModelAabb(model);
            float height = aabb.Size.Y;
            if (height <= 0.001f)
            {
                // Fallback — use maxDim like ScaleModelToFit
                float maxDim = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z));
                if (maxDim <= 0.001f)
                {
                    model.Scale = Vector3.One * 0.01f * targetHeight;
                    GD.Print($"[AxisBossBody] AABB detection failed, fallback scale for {targetHeight}m");
                    return;
                }
                height = maxDim;
            }
            float scale = targetHeight / height;
            model.Scale = Vector3.One * scale;
            GD.Print($"[AxisBossBody] ScaleModelToHeight '{model.Name}' AABB={aabb.Size} height={height} targetH={targetHeight} scale={scale}");
        }

        private static bool HasAnyMesh(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null) return true;
            if (node is GeometryInstance3D) return true;
            foreach (Node child in node.GetChildren())
                if (HasAnyMesh(child)) return true;
            return false;
        }

        // ══════════════════════════════════════════════════════════════
        //  PROCEDURAL FALLBACK — Same as before
        // ══════════════════════════════════════════════════════════════

        private static Node3D BuildTorso()
        {
            var torso = new Node3D();
            torso.Name = "Torso";
            torso.Position = new Vector3(0, 3f, 0);

            var chest = CreateMesh("_Chest",
                new BoxMesh { Size = new Vector3(4f, 3f, 2f) },
                DarkPlate, Vector3.Zero);
            torso.AddChild(chest);

            for (int i = 0; i < 3; i++)
            {
                float y = 0.8f - i * 0.7f;
                float w = 3.5f - i * 0.3f;
                torso.AddChild(CreateMesh($"_ArmorPlate{i}",
                    new BoxMesh { Size = new Vector3(w, 0.4f, 0.3f) },
                    CorePurple.Darkened(i * 0.1f), new Vector3(0, y, -1.1f)));
            }

            torso.AddChild(CreateGlow("_CoreGem",
                new SphereMesh { Radius = 0.4f, Height = 0.8f, RadialSegments = 12, Rings = 6 },
                HoloBlue, new Vector3(0, 0.3f, -1.2f), 3f));

            torso.AddChild(CreateGlow("_CoreRing",
                new TorusMesh { InnerRadius = 0.5f, OuterRadius = 0.65f, Rings = 12, RingSegments = 16 },
                HoloBlue, new Vector3(0, 0.3f, -1.15f), 1.5f));

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 2.3f;
                torso.AddChild(CreateMesh($"_Shoulder{(side < 0 ? "L" : "R")}",
                    new BoxMesh { Size = new Vector3(1.2f, 1.5f, 1.4f) },
                    DarkPlate.Lightened(0.05f), new Vector3(x, 0.8f, 0)));
                torso.AddChild(CreateGlow($"_ShoulderGlow{(side < 0 ? "L" : "R")}",
                    new BoxMesh { Size = new Vector3(0.1f, 1.2f, 0.1f) },
                    CorePurple, new Vector3(x, 0.8f, -0.75f), 2f));
            }

            for (int i = 0; i < 5; i++)
            {
                float x = -1f + i * 0.5f;
                var color = i % 2 == 0 ? DataGreen : EyeRed;
                torso.AddChild(CreateGlow($"_StatusLight{i}",
                    new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                    color, new Vector3(x, -0.5f, -1.15f), 2f));
            }

            torso.AddChild(CreateMesh("_Neck",
                new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.8f, Height = 1f, RadialSegments = 10 },
                DarkPlate, new Vector3(0, 2f, 0)));

            return torso;
        }

        private static Node3D BuildHead()
        {
            var head = new Node3D();
            head.Name = "Head";
            head.Position = new Vector3(0, 7f, 0);

            head.AddChild(CreateMesh("_Skull",
                new BoxMesh { Size = new Vector3(1.8f, 1.4f, 1.6f) },
                DarkPlate, Vector3.Zero));
            head.AddChild(CreateMesh("_FacePlate",
                new BoxMesh { Size = new Vector3(1.6f, 1.0f, 0.3f) },
                CorePurple.Darkened(0.15f), new Vector3(0, -0.1f, -0.9f)));
            head.AddChild(CreateGlow("_Visor",
                new BoxMesh { Size = new Vector3(1.2f, 0.25f, 0.1f) },
                EyeRed, new Vector3(0, 0.05f, -1.05f), 5f));
            head.AddChild(CreateGlow("_EyePupil",
                new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 8, Rings = 4 },
                EyeRed, new Vector3(0, 0.05f, -1.1f), 8f));

            for (int i = 0; i < 5; i++)
            {
                float x = -0.6f + i * 0.3f;
                float h = 0.6f + (i == 2 ? 0.4f : 0f);
                head.AddChild(CreateGlow($"_Spike{i}",
                    new CylinderMesh { TopRadius = 0f, BottomRadius = 0.08f, Height = h, RadialSegments = 4 },
                    Gold, new Vector3(x, 0.7f + h / 2f, 0), 1.5f));
            }

            for (int side = -1; side <= 1; side += 2)
            {
                head.AddChild(CreateMesh($"_Antenna{(side < 0 ? "L" : "R")}",
                    new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.06f, Height = 0.8f, RadialSegments = 4 },
                    DarkPlate, new Vector3(side * 1.1f, 0.2f, 0)));
                head.AddChild(CreateGlow($"_AntennaTip{(side < 0 ? "L" : "R")}",
                    new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                    HoloBlue, new Vector3(side * 1.1f, 0.65f, 0), 3f));
            }

            return head;
        }

        private static Node3D BuildLeftHand() => BuildHand("LeftArm", new Vector3(-5f, 4.5f, -2f));
        private static Node3D BuildRightHand() => BuildHand("RightArm", new Vector3(5f, 4.5f, -2f));

        private static Node3D BuildHand(string name, Vector3 position)
        {
            var hand = new Node3D();
            hand.Name = name;
            hand.Position = position;
            bool isLeft = name.Contains("Left");
            float mirror = isLeft ? 1f : -1f;

            hand.AddChild(CreateMesh("_Palm",
                new BoxMesh { Size = new Vector3(1.8f, 0.5f, 2f) },
                DarkPlate, Vector3.Zero));
            hand.AddChild(CreateGlow("_PalmGlow",
                new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.05f, RadialSegments = 12 },
                CorePurple, new Vector3(0, -0.28f, 0), 2f));

            for (int i = 0; i < 4; i++)
            {
                float x = -0.6f + i * 0.4f;
                hand.AddChild(CreateMesh($"_Finger{i}A",
                    new BoxMesh { Size = new Vector3(0.3f, 0.35f, 0.7f) },
                    DarkPlate.Lightened(0.03f), new Vector3(x, 0, -1.2f)));
                hand.AddChild(CreateMesh($"_Finger{i}B",
                    new BoxMesh { Size = new Vector3(0.25f, 0.3f, 0.5f) },
                    DarkPlate.Lightened(0.06f), new Vector3(x, 0, -1.7f)));
                hand.AddChild(CreateGlow($"_FingerGlow{i}",
                    new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                    CorePurple, new Vector3(x, 0, -0.85f), 1.5f));
            }

            hand.AddChild(CreateMesh("_Thumb",
                new BoxMesh { Size = new Vector3(0.35f, 0.35f, 0.6f) },
                DarkPlate.Lightened(0.03f), new Vector3(mirror * 1.1f, 0, -0.5f)));
            hand.AddChild(CreateGlow("_WristRing",
                new TorusMesh { InnerRadius = 0.4f, OuterRadius = 0.55f, Rings = 8, RingSegments = 12 },
                HoloBlue, new Vector3(0, 0, 1.2f), 2f));

            var palmLight = new OmniLight3D();
            palmLight.Name = "_PalmLight";
            palmLight.LightColor = CorePurple;
            palmLight.LightEnergy = 2f;
            palmLight.OmniRange = 4f;
            palmLight.Position = new Vector3(0, -0.5f, 0);
            hand.AddChild(palmLight);

            return hand;
        }

        private static Node3D BuildRift()
        {
            var rift = new Node3D();
            rift.Name = "Rift";
            rift.Position = new Vector3(0, 0.1f, 0);

            var disc = CreateGlow("_RiftDisc",
                new CylinderMesh { TopRadius = 5f, BottomRadius = 5f, Height = 0.1f, RadialSegments = 24 },
                CorePurple, Vector3.Zero, 1.5f);
            rift.AddChild(disc);

            rift.AddChild(CreateGlow("_RiftInner",
                new TorusMesh { InnerRadius = 2f, OuterRadius = 2.5f, Rings = 16, RingSegments = 24 },
                HoloBlue, new Vector3(0, 0.15f, 0), 3f));

            rift.AddChild(CreateGlow("_RiftOuter",
                new TorusMesh { InnerRadius = 4f, OuterRadius = 4.3f, Rings = 16, RingSegments = 24 },
                CorePurple, new Vector3(0, 0.12f, 0), 1.5f));

            var particles = new GpuParticles3D();
            particles.Name = "_RiftParticles";
            particles.Amount = 30;
            particles.Lifetime = 3f;
            particles.Preprocess = 1f;

            var mat = new ParticleProcessMaterial();
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
            mat.EmissionRingRadius = 4f;
            mat.EmissionRingInnerRadius = 1f;
            mat.EmissionRingHeight = 0.1f;
            mat.Direction = new Vector3(0, 1, 0);
            mat.Spread = 10f;
            mat.InitialVelocityMin = 2f;
            mat.InitialVelocityMax = 5f;
            mat.Gravity = Vector3.Zero;
            mat.ScaleMin = 0.1f;
            mat.ScaleMax = 0.3f;
            mat.Color = new Color(0.5f, 0.3f, 0.9f, 0.7f);
            particles.ProcessMaterial = mat;

            var mesh = new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 4, Rings = 2 };
            var meshMat = new StandardMaterial3D
            {
                AlbedoColor = CorePurple,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = CorePurple,
                EmissionEnergyMultiplier = 3f
            };
            mesh.Material = meshMat;
            particles.DrawPass1 = mesh;
            rift.AddChild(particles);

            return rift;
        }

        private static Node3D BuildAmbientEffects()
        {
            var effects = new Node3D();
            effects.Name = "AmbientEffects";

            for (int i = 0; i < 6; i++)
            {
                float angle = (float)i / 6f * Mathf.Tau;
                float radius = 3.5f;
                effects.AddChild(CreateGlow($"_DataStream{i}",
                    new BoxMesh { Size = new Vector3(0.05f, 3f + i * 0.3f, 0.05f) },
                    DataGreen, new Vector3(
                        Mathf.Cos(angle) * radius,
                        4f + i * 0.5f,
                        Mathf.Sin(angle) * radius), 1.5f));
            }

            for (int i = 0; i < 3; i++)
            {
                var light = new OmniLight3D();
                light.Name = $"_OrbitLight{i}";
                float angle = (float)i / 3f * Mathf.Tau;
                light.Position = new Vector3(Mathf.Cos(angle) * 4f, 5f, Mathf.Sin(angle) * 4f);
                light.LightColor = CorePurple;
                light.LightEnergy = 1.5f;
                light.OmniRange = 8f;
                effects.AddChild(light);
            }

            return effects;
        }

        // --- Mesh helpers ---

        private static MeshInstance3D CreateMesh(string name, Mesh mesh, Color color, Vector3 position)
        {
            var node = new MeshInstance3D();
            node.Name = name;
            node.Mesh = mesh;
            node.Position = position;
            var mat = new StandardMaterial3D { AlbedoColor = color };
            node.MaterialOverride = mat;
            return node;
        }

        private static MeshInstance3D CreateGlow(string name, Mesh mesh, Color color, Vector3 position, float emissionStrength)
        {
            var node = new MeshInstance3D();
            node.Name = name;
            node.Mesh = mesh;
            node.Position = position;
            var mat = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier = emissionStrength
            };
            node.MaterialOverride = mat;
            return node;
        }
    }
}
