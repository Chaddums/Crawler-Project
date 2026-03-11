using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Interactive couch in the safe room. Player sits down to open loot boxes
    /// via the holographic display. Handles sit/stand animations, camera zoom,
    /// and ceremony flow.
    /// </summary>
    public partial class SafeRoomCouch : Area3D, IInteractable
    {
        private HolographicDisplay _display;
        private PlayerController _seatedPlayer;
        private Label3D _promptLabel;
        private bool _isSitting;
        private bool _ceremonyInProgress;

        // Pending loot boxes queued for this couch session
        private readonly List<LootBoxData> _batchBoxes = new();
        private readonly List<LootBoxData> _premiumBoxes = new();

        private Vector3 _seatPosition;
        private Vector3 _standPosition;

        public string InteractionPrompt
        {
            get
            {
                if (_isSitting)
                    return _ceremonyInProgress ? "" : "[E] Stand Up";
                int pending = AchievementManager.PendingLootBoxes.Count;
                return pending > 0 ? $"[E] Open Loot Boxes ({pending})" : "[E] Sit Down";
            }
        }

        public bool CanInteract => !_ceremonyInProgress;

        public void Initialize(Vector3 position, HolographicDisplay display)
        {
            _display = display;
            Name = "SafeRoomCouch";
            Position = position;
            _seatPosition = position + new Vector3(0, 0.4f, 0);

            CollisionLayer = Constants.MASK_INTERACTABLE;
            CollisionMask = Constants.MASK_PLAYER;
            AddToGroup(Constants.GROUP_INTERACTABLE);

            // Trigger zone
            var shape = new CollisionShape3D();
            var box = new BoxShape3D();
            box.Size = new Vector3(2.5f, 2f, 2f);
            shape.Shape = box;
            shape.Position = new Vector3(0, 1, 0);
            AddChild(shape);

            BuildVisuals();
        }

        private void BuildVisuals()
        {
            // Try to load a model for the couch
            var model = ModelLibrary.TryLoad("prop", "pod");
            if (model != null)
            {
                RoomBuilder.ScaleModelToFitEffective(model, 1.4f);
                AddChild(model);
                RoomBuilder.GroundModel(model);
            }
            else
            {
                // Procedural couch: seat + backrest
                var seatMat = new StandardMaterial3D();
                seatMat.AlbedoColor = new Color(0.18f, 0.2f, 0.28f);

                // Seat
                var seat = new MeshInstance3D();
                seat.Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.3f, 0.7f) };
                seat.Position = new Vector3(0, 0.25f, 0);
                seat.MaterialOverride = seatMat;
                AddChild(seat);

                // Backrest
                var back = new MeshInstance3D();
                back.Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.6f, 0.15f) };
                back.Position = new Vector3(0, 0.6f, -0.35f);
                back.MaterialOverride = seatMat;
                AddChild(back);

                // Armrests
                var armMat = new StandardMaterial3D();
                armMat.AlbedoColor = new Color(0.22f, 0.24f, 0.32f);

                foreach (float side in new[] { -0.85f, 0.85f })
                {
                    var arm = new MeshInstance3D();
                    arm.Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.25f, 0.7f) };
                    arm.Position = new Vector3(side, 0.5f, 0);
                    arm.MaterialOverride = armMat;
                    AddChild(arm);
                }

                // Accent trim (blue emission line along seat front)
                var trimMesh = new MeshInstance3D();
                trimMesh.Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.02f, 0.02f) };
                trimMesh.Position = new Vector3(0, 0.4f, 0.34f);
                var trimMat = new StandardMaterial3D();
                trimMat.AlbedoColor = new Color(0.3f, 0.5f, 0.9f);
                trimMat.EmissionEnabled = true;
                trimMat.Emission = new Color(0.3f, 0.5f, 0.9f);
                trimMat.EmissionEnergyMultiplier = 1.5f;
                trimMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                trimMesh.MaterialOverride = trimMat;
                AddChild(trimMesh);
            }

            // Interaction prompt
            _promptLabel = new Label3D();
            _promptLabel.FontSize = 18;
            _promptLabel.Position = new Vector3(0, 1.8f, 0);
            _promptLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _promptLabel.Modulate = new Color(0.9f, 0.8f, 0.2f);
            _promptLabel.OutlineModulate = new Color(0, 0, 0);
            _promptLabel.OutlineSize = 4;
            _promptLabel.Visible = false;
            AddChild(_promptLabel);
        }

        public void Interact(Node playerNode)
        {
            if (playerNode is not PlayerController player) return;

            if (_isSitting)
            {
                if (_ceremonyInProgress) return;
                StandUp();
            }
            else
            {
                SitDown(player);
            }
        }

        private void SitDown(PlayerController player)
        {
            _seatedPlayer = player;
            _isSitting = true;
            _standPosition = player.GlobalPosition;

            // Disable player input
            player.InputHandler.DisableInput();

            // Tween player to seat position
            var seatWorld = GlobalPosition + new Vector3(0, 0.4f, 0.1f);
            var tween = CreateTween();
            tween.TweenProperty(player, "global_position", seatWorld, 0.4f)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);

            // Zoom camera to the couch + display area (modest zoom for sitting)
            var displayCenter = _display != null
                ? (_display.GlobalPosition + GlobalPosition) * 0.5f + Vector3.Up * 0.5f
                : GlobalPosition + Vector3.Up;

            if (ServiceLocator.TryGet<IsometricCamera>(out var cam))
                cam.ZoomToTarget(displayCenter, 7f, 0.8f);

            // After seated, check for pending loot boxes
            tween.TweenCallback(Callable.From(() =>
            {
                UpdatePromptLabel();
                ProcessLootBoxes();
            }));

            GD.Print("[SafeRoomCouch] Player sat down");
        }

        private void StandUp()
        {
            if (_seatedPlayer == null) return;

            _isSitting = false;

            // Re-enable input
            _seatedPlayer.InputHandler.EnableInput();

            // Return camera to normal
            if (ServiceLocator.TryGet<IsometricCamera>(out var cam))
                cam.ReturnToFollow(0.6f);

            // Tween player back to standing
            var tween = CreateTween();
            tween.TweenProperty(_seatedPlayer, "global_position", _standPosition, 0.3f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);

            _seatedPlayer = null;
            UpdatePromptLabel();
            GD.Print("[SafeRoomCouch] Player stood up");
        }

        private void ProcessLootBoxes()
        {
            if (AchievementManager.PendingLootBoxes.Count == 0) return;

            _batchBoxes.Clear();
            _premiumBoxes.Clear();

            while (AchievementManager.PendingLootBoxes.Count > 0)
            {
                var lootBox = AchievementManager.PendingLootBoxes.Dequeue();
                if (lootBox?.BaseData is not LootBoxData lootBoxData) continue;

                if (lootBoxData.Tier <= LootBoxTier.Silver)
                    _batchBoxes.Add(lootBoxData);
                else
                    _premiumBoxes.Add(lootBoxData);
            }

            // Start with batch (Bronze/Silver), then premium one-by-one
            if (_batchBoxes.Count > 0)
                StartBatchCeremony();
            else if (_premiumBoxes.Count > 0)
                StartNextPremiumCeremony();
        }

        private void StartBatchCeremony()
        {
            if (_display == null || _seatedPlayer == null) return;
            _ceremonyInProgress = true;
            UpdatePromptLabel();

            _display.StartBatchCeremony(_batchBoxes, _seatedPlayer);
            _display.CeremonyCollected += OnBatchCollected;
        }

        private void OnBatchCollected()
        {
            if (_display != null) _display.CeremonyCollected -= OnBatchCollected;

            // Short pause, then start premium boxes
            if (_premiumBoxes.Count > 0)
            {
                GetTree().CreateTimer(0.8f).Timeout += StartNextPremiumCeremony;
            }
            else
            {
                _ceremonyInProgress = false;
                UpdatePromptLabel();
            }
        }

        private int _premiumIndex;

        private void StartNextPremiumCeremony()
        {
            if (_premiumIndex >= _premiumBoxes.Count)
            {
                _ceremonyInProgress = false;
                _premiumIndex = 0;
                UpdatePromptLabel();
                return;
            }

            if (_display == null || _seatedPlayer == null) return;
            _ceremonyInProgress = true;
            UpdatePromptLabel();

            var boxData = _premiumBoxes[_premiumIndex];
            _premiumIndex++;

            _display.StartCeremony(boxData, _seatedPlayer);
            _display.CeremonyCollected += OnPremiumCollected;
        }

        private void OnPremiumCollected()
        {
            if (_display != null) _display.CeremonyCollected -= OnPremiumCollected;

            if (_premiumIndex < _premiumBoxes.Count)
                GetTree().CreateTimer(0.8f).Timeout += StartNextPremiumCeremony;
            else
            {
                _ceremonyInProgress = false;
                _premiumIndex = 0;
                UpdatePromptLabel();
            }
        }

        // ===== INPUT HANDLING =====

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!_isSitting) return;

            bool isInteract = @event.IsActionPressed("interact");
            bool isGamepadA = @event is InputEventJoypadButton jb && jb.Pressed
                && (jb.ButtonIndex == JoyButton.A || jb.ButtonIndex == JoyButton.Y);

            if (!isInteract && !isGamepadA) return;

            if (_ceremonyInProgress && _display != null && _display.IsActive)
            {
                // Collect loot from the holographic display
                _display.CollectAll();
                GetViewport().SetInputAsHandled();
            }
            else if (!_ceremonyInProgress)
            {
                // Stand up
                StandUp();
                GetViewport().SetInputAsHandled();
            }
        }

        // ===== PROMPT UPDATES =====

        public override void _Process(double delta)
        {
            if (_promptLabel == null) return;

            // Show prompt when a player is nearby (within 3m)
            bool anyNearby = false;
            foreach (var player in PlayerManager.Players)
            {
                if (player == null || !IsInstanceValid(player)) continue;
                if (GlobalPosition.FlatDistance(player.GlobalPosition) < 3f)
                {
                    anyNearby = true;
                    break;
                }
            }

            _promptLabel.Visible = anyNearby || _isSitting;
            if (_promptLabel.Visible)
                UpdatePromptLabel();
        }

        private void UpdatePromptLabel()
        {
            if (_promptLabel == null) return;
            _promptLabel.Text = InteractionPrompt;

            // Color: yellow for actionable, dim for info
            _promptLabel.Modulate = CanInteract
                ? new Color(0.9f, 0.8f, 0.2f)
                : new Color(0.5f, 0.5f, 0.6f);
        }
    }
}
