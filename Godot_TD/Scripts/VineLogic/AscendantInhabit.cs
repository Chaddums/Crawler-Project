using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Ascendant Inhabit System — BIT can take over a fallen Ascendant's body.
    ///
    /// When an Ascendant dies, its corpse stays on the battlefield for a window.
    /// If BIT is strong enough (run 15+ or specific relic), an "Inhabit" prompt appears.
    /// BIT merges with the corpse. Player now controls the Ascendant mech with its
    /// abilities while the tower network runs autonomously below.
    ///
    /// While inhabiting:
    /// - Player controls the Ascendant (WASD movement, Ascendant abilities on Q/E/R)
    /// - Camera zooms out to accommodate the larger model
    /// - Tower network continues running — no building, no mining toggle
    /// - Player's equipped relics amplify the Ascendant's stats
    /// - Timer counts down — body degrades over 30-60 seconds
    /// - On expiry: BIT ejects, returns to normal size and abilities
    ///
    /// BIT doesn't claim to be a god. BIT just wears their bodies better than they did.
    /// </summary>
    public partial class AscendantInhabit : Node
    {
        // State
        private bool _isInhabiting;
        private Ascendant _inhabitedAscendant;
        private float _inhabitTimer;
        private float _maxInhabitTime;

        // Original BIT state (to restore on eject)
        private VinePlayerAbility[] _originalAbilities;
        private float _originalSpeed;
        private Vector3 _originalScale;
        private float _originalAttackDamage;
        private float _originalAttackRange;

        // Corpse prompt
        private Ascendant _availableCorpse;
        private float _corpseWindowTimer;
        private Label3D _inhabitPrompt;

        private const float CORPSE_WINDOW = 10f;          // Seconds corpse stays available
        private const float BASE_INHABIT_TIME = 40f;       // Base duration in Ascendant body
        private const int MIN_RUN_COUNT = 10;               // Minimum runs for inhabit unlock
        private const float CAMERA_ZOOM_OUT = 1.8f;        // Camera zoom multiplier while inhabiting

        public bool IsInhabiting => _isInhabiting;

        public override void _Ready()
        {
            GameEvents.OnAscendantDefeated += OnAscendantDefeated;
            ServiceLocator.Register(this);
        }

        private void OnAscendantDefeated(Ascendant fallen)
        {
            if (_isInhabiting) return;  // Already in one
            if (_availableCorpse != null) return;  // Already have a prompt

            // Check eligibility — run count or relic
            int runCount = GameManager.Instance?.MetaSave?.RunCount ?? 0;
            bool hasRelic = ServiceLocator.TryGet<RelicManager>(out var rm) &&
                            rm.HasEffect("ascendant-conduit");  // Future relic that enables early inhabit

            if (runCount < MIN_RUN_COUNT && !hasRelic) return;

            // Corpse becomes available
            _availableCorpse = fallen;
            _corpseWindowTimer = CORPSE_WINDOW;

            // Show prompt above corpse
            ShowInhabitPrompt(fallen);

            // BIT commentary
            if (ServiceLocator.TryGet<BITCommentary>(out var bit))
            {
                if (runCount >= 15)
                    bit.Say("the body is empty. i know how to use it. i have always known how to use it.");
                else
                    bit.Say("the ascendant fell. its chassis is intact. this is... possible.");
            }

            GD.Print($"[AscendantInhabit] Corpse available: {fallen.AscendantName}. Window: {CORPSE_WINDOW}s");
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Corpse window countdown
            if (_availableCorpse != null && !_isInhabiting)
            {
                _corpseWindowTimer -= dt;

                // Check for inhabit input (F key)
                if (Input.IsActionJustPressed("interact") || Input.IsKeyPressed(Key.F))
                {
                    // Check proximity — BIT must be near the corpse
                    if (ServiceLocator.TryGet<VinePlayer>(out var player))
                    {
                        float dist = player.GlobalPosition.DistanceTo(_availableCorpse.GlobalPosition);
                        if (dist < 8f)
                        {
                            Inhabit(player, _availableCorpse);
                            return;
                        }
                    }
                }

                if (_corpseWindowTimer <= 0)
                {
                    // Window expired — corpse disappears
                    GD.Print("[AscendantInhabit] Corpse window expired");
                    DismissCorpse();
                }

                // Update prompt with time remaining
                if (_inhabitPrompt != null)
                    _inhabitPrompt.Text = $"[F] INHABIT ({_corpseWindowTimer:F0}s)";
            }

            // Inhabit timer countdown
            if (_isInhabiting)
            {
                _inhabitTimer -= dt;

                // Update HUD with remaining time
                // (The pause menu will show this via the extraction counter area)

                if (_inhabitTimer <= 0)
                {
                    Eject();
                }
            }
        }

        private void Inhabit(VinePlayer player, Ascendant corpse)
        {
            _isInhabiting = true;
            _inhabitedAscendant = corpse;
            _availableCorpse = null;

            // Calculate inhabit duration — base + relic bonuses
            _maxInhabitTime = BASE_INHABIT_TIME;
            if (ServiceLocator.TryGet<RelicManager>(out var rm))
            {
                // Each equipped relic adds 5 seconds
                _maxInhabitTime += rm.EquippedRelics.Count * 5f;
            }
            _inhabitTimer = _maxInhabitTime;

            // Store original BIT state
            _originalSpeed = player.MoveSpeed;
            _originalScale = player.Scale;
            _originalAttackDamage = Constants.VINE_PLAYER_ATTACK_DAMAGE;
            _originalAttackRange = Constants.VINE_PLAYER_ATTACK_RANGE;

            // Transform BIT into the Ascendant
            // Hide BIT's model, show Ascendant model at BIT's position
            player.SetVisible(false);
            corpse.GlobalPosition = player.GlobalPosition;

            // Reparent corpse under player so it moves with WASD
            corpse.GetParent()?.RemoveChild(corpse);
            player.AddChild(corpse);
            corpse.Position = Vector3.Zero;

            // Override BIT's stats with Ascendant stats
            player.MoveSpeed = corpse.Speed;
            player.Scale = Vector3.One;  // Ascendant model handles its own scale

            // Replace abilities with Ascendant abilities
            _originalAbilities = player.GetAbilities();
            player.SetAbilities(CreateAscendantAbilities(corpse));

            // Camera zoom out
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
            {
                cam.Shake(1.2f, 0.4f);
                cam.SetZoomMultiplier(CAMERA_ZOOM_OUT);
            }

            // Dismiss prompt
            DismissPrompt();

            // Commentary
            if (ServiceLocator.TryGet<AXISCommentary>(out var axis))
                axis.Say("AXIS", "What are you doing. That is not sanctioned. That is not—");

            if (ServiceLocator.TryGet<BITCommentary>(out var bit))
                bit.Say("inhabited. the ascendant's systems are primitive. this will be sufficient.");

            GD.Print($"[AscendantInhabit] BIT inhabiting {corpse.AscendantName} for {_maxInhabitTime:F0}s");

            GameEvents.OnAnnouncement?.Invoke($"ASCENDANT INHABITED — {_maxInhabitTime:F0}s");
        }

        private void Eject()
        {
            if (!_isInhabiting) return;
            _isInhabiting = false;

            if (!ServiceLocator.TryGet<VinePlayer>(out var player)) return;

            // Restore BIT
            player.SetVisible(true);
            player.MoveSpeed = _originalSpeed;

            // Remove Ascendant corpse
            if (_inhabitedAscendant != null)
            {
                // Death burst at the Ascendant's final position
                VfxFactory.SpawnBossDeathBurst(GetTree(),
                    player.GlobalPosition, new Color(0.5f, 0.3f, 0.9f));

                _inhabitedAscendant.QueueFree();
                _inhabitedAscendant = null;
            }

            // Restore abilities
            if (_originalAbilities != null)
                player.SetAbilities(_originalAbilities);

            // Camera reset
            if (ServiceLocator.TryGet<TDCamera>(out var cam))
            {
                cam.Shake(0.8f, 0.3f);
                cam.SetZoomMultiplier(1f);
            }

            if (ServiceLocator.TryGet<BITCommentary>(out var bit))
                bit.Say("ejected. the body was failing. returning to standard operation.");

            GD.Print("[AscendantInhabit] BIT ejected, returning to normal");

            GameEvents.OnAnnouncement?.Invoke("INHABIT ENDED");
        }

        private VinePlayerAbility[] CreateAscendantAbilities(Ascendant corpse)
        {
            float dmgMult = 1f;
            // Relic amplification — equipped relics boost Ascendant abilities
            if (ServiceLocator.TryGet<RelicManager>(out var rm))
            {
                var mods = rm.GetStatMods();
                dmgMult = 1f + mods.BaseDamageMult;
            }

            float baseDmg = corpse.Damage * dmgMult;
            float range = corpse.AttackRange;

            return new VinePlayerAbility[]
            {
                new VinePlayerAbility
                {
                    Name = "Ascendant Strike",
                    Description = $"Massive damage in range ({baseDmg:F0} DMG)",
                    Cooldown = corpse.AttackInterval,
                    MaterialsCost = 0,  // Ascendant abilities are free — they're burning the body's energy
                    Range = range,
                    IconColor = new Color(0.9f, 0.4f, 0.1f),
                    Execute = player =>
                    {
                        var enemies = player.GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
                        foreach (var enemy in enemies)
                        {
                            if (enemy is not VineEnemy ve || !ve.IsAlive) continue;
                            float dist = player.GlobalPosition.DistanceTo(ve.GlobalPosition);
                            if (dist < range)
                            {
                                ve.TakeDamage(baseDmg);
                                if (!ve.IsAlive) player.EnemiesKilledPersonally++;
                            }
                        }
                        VfxFactory.SpawnAreaPulse(player.GetTree(), player.GlobalPosition,
                            range, corpse.IsEnemy ? new Color(0.9f, 0.3f, 0.2f) : new Color(0.2f, 0.8f, 0.9f));
                        if (ServiceLocator.TryGet<TDCamera>(out var cam))
                            cam.Shake(0.5f, 0.15f);
                    }
                },
                new VinePlayerAbility
                {
                    Name = "Chaos Pulse",
                    Description = "Destroy terrain in radius, damage all enemies",
                    Cooldown = 8f,
                    MaterialsCost = 0,
                    Range = corpse.ChaosRadius,
                    IconColor = new Color(0.6f, 0.2f, 0.9f),
                    Execute = player =>
                    {
                        // Area damage to all enemies
                        float chaosRange = corpse.ChaosRadius;
                        var enemies = player.GetTree().GetNodesInGroup(Constants.GROUP_VINE_ENEMY);
                        foreach (var enemy in enemies)
                        {
                            if (enemy is not VineEnemy ve || !ve.IsAlive) continue;
                            float dist = player.GlobalPosition.DistanceTo(ve.GlobalPosition);
                            if (dist < chaosRange)
                                ve.TakeDamage(baseDmg * 0.5f);
                        }

                        // Terrain destruction (same as Ascendant chaos)
                        if (ServiceLocator.TryGet<VineGrid>(out var grid))
                        {
                            Vector2I gridPos = grid.WorldToGrid(player.GlobalPosition);
                            int cellRadius = Mathf.CeilToInt(chaosRange / 2f);
                            var rng = new RandomNumberGenerator();
                            for (int dx = -cellRadius; dx <= cellRadius; dx++)
                            {
                                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                                {
                                    int x = gridPos.X + dx;
                                    int y = gridPos.Y + dy;
                                    if (!grid.InBounds(x, y)) continue;
                                    if (new Vector2(dx, dy).Length() > cellRadius) continue;
                                    if (rng.Randf() > 0.2f) continue;

                                    if (grid.GetCell(x, y) == VineCellType.Wall)
                                        grid.ClearCell(x, y);
                                }
                            }
                            GameEvents.OnTerrainChanged?.Invoke(gridPos);
                        }

                        VfxFactory.SpawnBossDeathBurst(player.GetTree(), player.GlobalPosition,
                            new Color(0.6f, 0.2f, 0.9f));
                        if (ServiceLocator.TryGet<TDCamera>(out var cam))
                            cam.Shake(1f, 0.3f);
                    }
                },
                new VinePlayerAbility
                {
                    Name = "Eject",
                    Description = "Leave the Ascendant body and return to BIT",
                    Cooldown = 0f,
                    MaterialsCost = 0,
                    Range = 0f,
                    IconColor = new Color(0.9f, 0.9f, 0.2f),
                    Execute = _ => Eject()
                }
            };
        }

        private void ShowInhabitPrompt(Ascendant corpse)
        {
            DismissPrompt();
            _inhabitPrompt = new Label3D();
            _inhabitPrompt.Text = $"[F] INHABIT ({CORPSE_WINDOW:F0}s)";
            _inhabitPrompt.FontSize = 48;
            _inhabitPrompt.OutlineSize = 8;
            _inhabitPrompt.Modulate = new Color(0.9f, 0.6f, 1f);
            _inhabitPrompt.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _inhabitPrompt.Position = corpse.GlobalPosition + Vector3.Up * 6f;
            GetTree().CurrentScene.AddChild(_inhabitPrompt);
        }

        private void DismissPrompt()
        {
            if (_inhabitPrompt != null)
            {
                _inhabitPrompt.QueueFree();
                _inhabitPrompt = null;
            }
        }

        private void DismissCorpse()
        {
            DismissPrompt();
            if (_availableCorpse != null)
            {
                _availableCorpse.QueueFree();
                _availableCorpse = null;
            }
        }

        public override void _ExitTree()
        {
            GameEvents.OnAscendantDefeated -= OnAscendantDefeated;
            ServiceLocator.Unregister<AscendantInhabit>();
        }
    }
}
