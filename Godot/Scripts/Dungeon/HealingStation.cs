using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Functional healing fountain for safe rooms. Heals player on proximity
    /// (passive regen) and offers instant full heal via interaction (once per visit).
    /// </summary>
    public partial class HealingStation : Area3D, IInteractable
    {
        private bool _fullHealUsed;
        private float _regenTimer;
        private const float RegenInterval = 0.5f;
        private const float RegenPercent = 0.05f;

        private Label3D _promptLabel;
        private MeshInstance3D _waterMesh;
        private OmniLight3D _light;

        public string InteractionPrompt => _fullHealUsed ? "Healing Fountain" : "[E] Full Heal";
        public bool CanInteract => !_fullHealUsed;

        public void Initialize()
        {
            Name = "HealingStation";
            CollisionLayer = Constants.MASK_INTERACTABLE;
            CollisionMask = Constants.MASK_PLAYER;
            AddToGroup(Constants.GROUP_INTERACTABLE);

            // Trigger zone
            var shape = new CollisionShape3D();
            var sphere = new SphereShape3D();
            sphere.Radius = 3f;
            shape.Shape = sphere;
            shape.Position = new Vector3(0, 1, 0);
            AddChild(shape);

            BuildVisuals();

            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        private void BuildVisuals()
        {
            // Try FBX model first
            var model = ModelLibrary.TryLoad("prop", "pedestal");
            if (model != null)
            {
                RoomBuilder.ScaleModelToFitEffective(model, 1.5f);
                AddChild(model);
                RoomBuilder.GroundModel(model);
            }
            else
            {
                // Pedestal base
                var baseMat = new StandardMaterial3D();
                baseMat.AlbedoColor = new Color(0.3f, 0.35f, 0.45f);
                var baseMesh = new MeshInstance3D();
                baseMesh.Mesh = new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.9f, Height = 0.4f, RadialSegments = 12 };
                baseMesh.Position = new Vector3(0, 0.2f, 0);
                baseMesh.MaterialOverride = baseMat;
                AddChild(baseMesh);
            }

            // Water sphere (always added — the healing indicator)
            var waterMat = new StandardMaterial3D();
            waterMat.AlbedoColor = new Color(0.2f, 0.5f, 0.9f, 0.7f);
            waterMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            waterMat.EmissionEnabled = true;
            waterMat.Emission = new Color(0.3f, 0.5f, 1f);
            waterMat.EmissionEnergyMultiplier = 1.5f;

            _waterMesh = new MeshInstance3D();
            _waterMesh.Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f, RadialSegments = 12, Rings = 6 };
            _waterMesh.Position = new Vector3(0, 0.8f, 0);
            _waterMesh.MaterialOverride = waterMat;
            AddChild(_waterMesh);

            // Pulse tween
            var tween = _waterMesh.CreateTween();
            tween.SetLoops(10000);
            tween.TweenProperty(_waterMesh, "scale", new Vector3(1.1f, 1.1f, 1.1f), 1.5f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(_waterMesh, "scale", Vector3.One, 1.5f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

            // Particles
            var particles = VfxFactory.CreateAmbientParticles(new Color(0.3f, 0.6f, 1f), 0.6f);
            particles.Position = new Vector3(0, 0.6f, 0);
            AddChild(particles);

            // Light
            _light = new OmniLight3D();
            _light.Position = new Vector3(0, 1.2f, 0);
            _light.LightColor = new Color(0.3f, 0.5f, 1f);
            _light.LightEnergy = 1f;
            _light.OmniRange = 4f;
            AddChild(_light);

            // Interaction prompt
            _promptLabel = new Label3D();
            _promptLabel.Text = "[E] Full Heal";
            _promptLabel.FontSize = 20;
            _promptLabel.Position = new Vector3(0, 2f, 0);
            _promptLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _promptLabel.Modulate = new Color(0.3f, 0.8f, 1f);
            _promptLabel.OutlineModulate = new Color(0, 0, 0);
            _promptLabel.OutlineSize = 4;
            _promptLabel.Visible = false;
            AddChild(_promptLabel);
        }

        public void Interact(Node playerNode)
        {
            if (_fullHealUsed) return;
            if (playerNode is not PlayerController player) return;

            _fullHealUsed = true;
            player.Health.Heal(player.Health.MaxHealth);
            _promptLabel.Visible = false;

            // Green flash on heal
            if (_light != null)
            {
                _light.LightColor = new Color(0.3f, 1f, 0.5f);
                _light.LightEnergy = 3f;
                var tween = CreateTween();
                tween.TweenProperty(_light, "light_energy", 1f, 0.8f);
                tween.TweenCallback(Callable.From(() => _light.LightColor = new Color(0.3f, 0.5f, 1f)));
            }

            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName("heal");

            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                commentary.QueueLine("BIT", "Systems restored to full capacity!",
                    CommentaryPriority.Medium, CommentaryCategory.CombatReaction);

            GD.Print("[HealingStation] Full heal used");
        }

        private bool _playerNearby;

        private void OnBodyEntered(Node3D body)
        {
            if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;
            _playerNearby = true;
            if (!_fullHealUsed) _promptLabel.Visible = true;
        }

        private void OnBodyExited(Node3D body)
        {
            if (!body.IsInGroup(Constants.GROUP_PLAYER)) return;
            _playerNearby = false;
            _promptLabel.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (!_playerNearby) return;

            // Passive regen while nearby
            _regenTimer += (float)delta;
            if (_regenTimer >= RegenInterval)
            {
                _regenTimer -= RegenInterval;
                foreach (var player in PlayerManager.Players)
                {
                    if (player == null || !IsInstanceValid(player)) continue;
                    float dist = GlobalPosition.FlatDistance(player.GlobalPosition);
                    if (dist < 3f)
                        player.Health.Heal(player.Health.MaxHealth * RegenPercent);
                }
            }
        }
    }
}
