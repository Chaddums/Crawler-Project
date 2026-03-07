using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Orchestrates celebration VFX sequences by tier.
    /// Produces distinct emotional responses from "comical junk" through "screen-breaking absurd."
    /// Inspired by: Vampire Survivors (dopamine cascade), Balatro (failure humor),
    /// POE2 (iconic drop sounds + light pillars), DCC (unique box ceremonies).
    ///
    /// Usage:
    ///   CelebrationVfxManager.Play(parentNode, worldPos, CelebrationTier.Legendary);
    ///   CelebrationVfxManager.Play(parentNode, worldPos, CelebrationTier.Junk);
    ///   CelebrationVfxManager.PlayForRarity(parentNode, worldPos, ItemRarity.Epic);
    /// </summary>
    public static class CelebrationVfxManager
    {
        // --- Tier mapping from game systems ---

        public static CelebrationTier TierFromRarity(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => CelebrationTier.Junk,
            ItemRarity.Uncommon => CelebrationTier.Meh,
            ItemRarity.Rare => CelebrationTier.Decent,
            ItemRarity.Epic => CelebrationTier.Exciting,
            ItemRarity.Legendary => CelebrationTier.Legendary,
            ItemRarity.Absurd => CelebrationTier.Absurd,
            _ => CelebrationTier.Meh
        };

        public static CelebrationTier TierFromLootBox(LootBoxTier tier, ItemRarity bestItem) => tier switch
        {
            LootBoxTier.Bronze => bestItem >= ItemRarity.Epic ? CelebrationTier.Exciting : CelebrationTier.Junk,
            LootBoxTier.Silver => bestItem >= ItemRarity.Epic ? CelebrationTier.Exciting : CelebrationTier.Meh,
            LootBoxTier.Gold => bestItem >= ItemRarity.Legendary ? CelebrationTier.Legendary : CelebrationTier.Decent,
            LootBoxTier.Diamond => bestItem >= ItemRarity.Legendary ? CelebrationTier.Legendary : CelebrationTier.Exciting,
            LootBoxTier.Legendary => bestItem >= ItemRarity.Absurd ? CelebrationTier.Absurd : CelebrationTier.Legendary,
            LootBoxTier.Celestial => CelebrationTier.Absurd,
            _ => CelebrationTier.Decent
        };

        // --- Main entry points ---

        /// <summary>Play a full celebration sequence at a world position.</summary>
        public static void Play(Node parent, Vector3 worldPos, CelebrationTier tier)
        {
            switch (tier)
            {
                case CelebrationTier.Junk:
                    PlayJunk(parent, worldPos);
                    break;
                case CelebrationTier.Meh:
                    PlayMeh(parent, worldPos);
                    break;
                case CelebrationTier.Decent:
                    PlayDecent(parent, worldPos);
                    break;
                case CelebrationTier.Exciting:
                    PlayExciting(parent, worldPos);
                    break;
                case CelebrationTier.Legendary:
                    PlayLegendary(parent, worldPos);
                    break;
                case CelebrationTier.Absurd:
                    PlayAbsurd(parent, worldPos);
                    break;
            }
        }

        /// <summary>Shorthand: play celebration matching an item rarity.</summary>
        public static void PlayForRarity(Node parent, Vector3 worldPos, ItemRarity rarity)
        {
            Play(parent, worldPos, TierFromRarity(rarity));
        }

        // =====================================================================
        //  JUNK — Comical failure. The anti-celebration.
        //  Balatro "oh no" energy. Item deflates. Sad gray puff. AXIS loves it.
        // =====================================================================
        private static void PlayJunk(Node parent, Vector3 worldPos)
        {
            // Sad gray puff — particles fall DOWN instead of up
            var sadPuff = VfxFactory.CreateSadPuff();
            sadPuff.GlobalPosition = worldPos + Vector3.Up * 0.5f;
            parent.AddChild(sadPuff);

            // Play sad trombone
            PlaySfx("celebration_junk");

            // Tiny screen shake — barely perceptible, adds to the comedy
            ShakeScreen(parent, 0.03f);

            // Spawn the "womp womp" floating text
            SpawnFloatingText(parent, worldPos + Vector3.Up * 2f, "...",
                new Color(0.5f, 0.5f, 0.5f), 24, deflate: true);

            // AXIS gets a kick out of this
            QueueCommentary("AXIS", "commentary.junkDrop", CommentaryPriority.Low, CommentaryCategory.LootReaction);
        }

        // =====================================================================
        //  MEH — Barely worth it. Quick, quiet, unremarkable.
        //  Player shouldn't feel bad, but shouldn't feel excited either.
        // =====================================================================
        private static void PlayMeh(Node parent, Vector3 worldPos)
        {
            // Small pickup sparkle — white, brief
            var sparkle = VfxFactory.CreatePickupTrail(new Color(0.8f, 0.8f, 0.8f));
            sparkle.GlobalPosition = worldPos + Vector3.Up * 0.3f;
            parent.AddChild(sparkle);

            PlaySfx("pickup");
        }

        // =====================================================================
        //  DECENT — Solid find. Satisfying crunch. Brief rarity flash.
        //  The "nice" tier. Good feedback without overwhelming.
        // =====================================================================
        private static void PlayDecent(Node parent, Vector3 worldPos)
        {
            Color blue = new Color(0.3f, 0.5f, 1f);

            // Upward burst in rarity color
            var burst = VfxFactory.CreateLootBurstParticles(blue);
            burst.GlobalPosition = worldPos;
            parent.AddChild(burst);

            // Small shockwave ring
            var ring = VfxFactory.CreateShockwaveRing(blue);
            ring.GlobalPosition = worldPos;
            parent.AddChild(ring);

            PlaySfx("item_reveal");
            ShakeScreen(parent, 0.08f);
        }

        // =====================================================================
        //  EXCITING — Great drop! This is where it starts feeling GOOD.
        //  POE2 rare-drop energy. Light pillar, screen punch, commentary.
        // =====================================================================
        private static void PlayExciting(Node parent, Vector3 worldPos)
        {
            Color purple = new Color(0.7f, 0.3f, 0.9f);

            // Light pillar
            var pillar = VfxFactory.CreateLightPillar(ItemRarity.Epic);
            pillar.GlobalPosition = worldPos;
            parent.AddChild(pillar);

            // Big burst
            var burst = VfxFactory.CreateCelebrationBurst(purple, 40);
            burst.GlobalPosition = worldPos;
            parent.AddChild(burst);

            // Expanding shockwave
            var ring = VfxFactory.CreateShockwaveRing(purple);
            ring.GlobalPosition = worldPos;
            ring.Scale = Vector3.One * 1.5f;
            parent.AddChild(ring);

            // Screen flash
            FlashScreen(parent, purple, 0.15f, 0.2f);
            ShakeScreen(parent, 0.2f);

            PlaySfx("epic_drop");

            // BIT reacts
            QueueCommentary("BIT", "commentary.epicDrop", CommentaryPriority.Medium, CommentaryCategory.LootReaction);
        }

        // =====================================================================
        //  LEGENDARY — Full dopamine hit. Time slows. Screen goes white.
        //  POE2 exalt-drop energy. Everything stops to acknowledge this moment.
        // =====================================================================
        private static void PlayLegendary(Node parent, Vector3 worldPos)
        {
            Color gold = new Color(1f, 0.7f, 0f);

            // Tall light pillar
            var pillar = VfxFactory.CreateLightPillar(ItemRarity.Legendary);
            pillar.GlobalPosition = worldPos;
            parent.AddChild(pillar);

            // Massive particle storm — confetti + sparks
            var confetti = VfxFactory.CreateConfettiStorm(gold, 80);
            confetti.GlobalPosition = worldPos + Vector3.Up * 3f;
            parent.AddChild(confetti);

            var sparks = VfxFactory.CreateCelebrationBurst(gold, 60);
            sparks.GlobalPosition = worldPos;
            parent.AddChild(sparks);

            // Multiple shockwave rings (staggered)
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                DelayedCall(parent, i * 0.12f, () =>
                {
                    if (!GodotObject.IsInstanceValid(parent)) return;
                    var ring = VfxFactory.CreateShockwaveRing(gold.Lightened(idx * 0.1f));
                    ring.GlobalPosition = worldPos;
                    ring.Scale = Vector3.One * (1f + idx * 0.5f);
                    parent.AddChild(ring);
                });
            }

            // Multi-pulse screen flash (POE2 style)
            FlashScreen(parent, Colors.White, 0.4f, 0.15f);
            DelayedCall(parent, 0.2f, () => FlashScreen(parent, gold, 0.25f, 0.2f));
            DelayedCall(parent, 0.45f, () => FlashScreen(parent, gold, 0.15f, 0.15f));

            // Heavy screen shake cascade
            ShakeScreen(parent, 0.4f);
            DelayedCall(parent, 0.15f, () => ShakeScreen(parent, 0.25f));
            DelayedCall(parent, 0.35f, () => ShakeScreen(parent, 0.15f));

            // Time dilation — brief slow-mo for impact
            SlowMotion(parent, 0.3f, 0.5f);

            PlaySfx("celebration_legendary");

            // Floating text
            SpawnFloatingText(parent, worldPos + Vector3.Up * 3f, "LEGENDARY!",
                gold, 48, inflate: true);

            // Both commentators react
            QueueCommentary("AXIS", "commentary.legendaryDrop", CommentaryPriority.High, CommentaryCategory.LootReaction);
            DelayedCall(parent, 1.5f, () =>
                QueueCommentary("BIT", "commentary.legendaryDropBit", CommentaryPriority.Medium, CommentaryCategory.LootReaction));
        }

        // =====================================================================
        //  ABSURD — Screen goes completely nuts. Glitch effects. Chaos.
        //  Vampire Survivors "what is even happening" energy.
        //  AXIS breaks character. Multiple confetti layers. Screen distortion.
        // =====================================================================
        private static void PlayAbsurd(Node parent, Vector3 worldPos)
        {
            Color pink = new Color(1f, 0.2f, 0.4f);
            Color gold = new Color(1f, 0.7f, 0f);
            Color cyan = new Color(0.4f, 0.9f, 1f);

            // TRIPLE light pillar — different colors
            var pillar1 = VfxFactory.CreateLightPillar(ItemRarity.Absurd);
            pillar1.GlobalPosition = worldPos;
            parent.AddChild(pillar1);

            DelayedCall(parent, 0.1f, () =>
            {
                if (!GodotObject.IsInstanceValid(parent)) return;
                var pillar2 = VfxFactory.CreateLightPillar(ItemRarity.Legendary);
                pillar2.GlobalPosition = worldPos + new Vector3(1.5f, 0, 0);
                parent.AddChild(pillar2);
            });
            DelayedCall(parent, 0.2f, () =>
            {
                if (!GodotObject.IsInstanceValid(parent)) return;
                var pillar3 = VfxFactory.CreateLightPillar(ItemRarity.Epic);
                pillar3.GlobalPosition = worldPos + new Vector3(-1.5f, 0, 0);
                parent.AddChild(pillar3);
            });

            // Massive multi-color confetti storm
            var confetti1 = VfxFactory.CreateConfettiStorm(pink, 120);
            confetti1.GlobalPosition = worldPos + Vector3.Up * 4f;
            parent.AddChild(confetti1);

            var confetti2 = VfxFactory.CreateConfettiStorm(gold, 80);
            confetti2.GlobalPosition = worldPos + Vector3.Up * 5f;
            parent.AddChild(confetti2);

            var confetti3 = VfxFactory.CreateConfettiStorm(cyan, 60);
            confetti3.GlobalPosition = worldPos + Vector3.Up * 3f;
            parent.AddChild(confetti3);

            // Huge burst
            var burst = VfxFactory.CreateCelebrationBurst(pink, 100);
            burst.GlobalPosition = worldPos;
            parent.AddChild(burst);

            // Shockwave cascade — 5 rings
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                Color ringColor = idx % 2 == 0 ? pink : gold;
                DelayedCall(parent, i * 0.08f, () =>
                {
                    if (!GodotObject.IsInstanceValid(parent)) return;
                    var ring = VfxFactory.CreateShockwaveRing(ringColor);
                    ring.GlobalPosition = worldPos;
                    ring.Scale = Vector3.One * (1f + idx * 0.4f);
                    parent.AddChild(ring);
                });
            }

            // Screen flash storm — rapid multi-color
            FlashScreen(parent, Colors.White, 0.6f, 0.1f);
            DelayedCall(parent, 0.15f, () => FlashScreen(parent, pink, 0.4f, 0.1f));
            DelayedCall(parent, 0.3f, () => FlashScreen(parent, gold, 0.3f, 0.12f));
            DelayedCall(parent, 0.5f, () => FlashScreen(parent, cyan, 0.25f, 0.1f));
            DelayedCall(parent, 0.7f, () => FlashScreen(parent, pink, 0.15f, 0.15f));

            // Earthquake screen shake
            ShakeScreen(parent, 0.6f);
            DelayedCall(parent, 0.2f, () => ShakeScreen(parent, 0.4f));
            DelayedCall(parent, 0.5f, () => ShakeScreen(parent, 0.3f));
            DelayedCall(parent, 0.8f, () => ShakeScreen(parent, 0.2f));

            // Longer slow-mo
            SlowMotion(parent, 0.4f, 0.8f);

            PlaySfx("celebration_absurd");

            // Giant floating text with scale animation
            SpawnFloatingText(parent, worldPos + Vector3.Up * 4f, "ABSURD!!!",
                pink, 64, inflate: true);

            // AXIS completely loses it
            QueueCommentary("AXIS", "commentary.absurdDrop", CommentaryPriority.Announcement, CommentaryCategory.LootReaction);
        }

        // =====================================================================
        //  Helpers — screen effects, audio, text, timing
        // =====================================================================

        private static void ShakeScreen(Node context, float trauma)
        {
            var cameras = context.GetTree().GetNodesInGroup("Camera");
            foreach (var cam in cameras)
            {
                if (cam is IsometricCamera isoCam)
                    isoCam.Shake(trauma);
            }
        }

        /// <summary>
        /// Full-screen color flash overlay. Creates a CanvasLayer + ColorRect,
        /// fades from flashAlpha to 0 over duration, then self-destructs.
        /// </summary>
        private static void FlashScreen(Node context, Color color, float flashAlpha, float duration)
        {
            if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;

            var layer = new CanvasLayer();
            layer.Layer = 100;
            context.GetTree().Root.AddChild(layer);

            var rect = new ColorRect();
            rect.Color = new Color(color.R, color.G, color.B, flashAlpha);
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            rect.MouseFilter = Control.MouseFilterEnum.Ignore;
            layer.AddChild(rect);

            var tween = context.GetTree().CreateTween();
            tween.TweenProperty(rect, "color:a", 0f, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
            }));
        }

        /// <summary>
        /// Brief slow-motion effect. Scales Engine.TimeScale down then back up.
        /// </summary>
        private static void SlowMotion(Node context, float slowScale, float duration)
        {
            if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;

            Engine.TimeScale = slowScale;

            // Use a real-time timer (not affected by TimeScale)
            var timer = context.GetTree().CreateTimer(duration, processAlways: true);
            timer.Timeout += () => Engine.TimeScale = 1.0;
        }

        /// <summary>
        /// 3D floating text label that rises and fades. Optionally deflates (junk) or inflates (legendary).
        /// Art plug-in: Replace Label3D with a TextureRect/Sprite3D for final art.
        /// </summary>
        private static void SpawnFloatingText(Node parent, Vector3 worldPos, string text,
            Color color, int fontSize, bool deflate = false, bool inflate = false)
        {
            if (!GodotObject.IsInstanceValid(parent) || !parent.IsInsideTree()) return;

            var label = new Label3D();
            label.Text = text;
            label.FontSize = fontSize;
            label.Modulate = color;
            label.OutlineSize = 4;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.NoDepthTest = true;
            label.GlobalPosition = worldPos;
            parent.AddChild(label);

            var tween = parent.CreateTween();
            tween.SetParallel(true);

            // Rise
            tween.TweenProperty(label, "global_position:y", worldPos.Y + 2f, 1.5f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            if (deflate)
            {
                // Junk: starts normal, shrinks to nothing — comedic deflation
                label.Scale = Vector3.One;
                tween.TweenProperty(label, "scale", Vector3.One * 0.1f, 1.2f)
                    .SetTrans(Tween.TransitionType.Back)
                    .SetEase(Tween.EaseType.In);
            }
            else if (inflate)
            {
                // Legendary: starts small, PUNCHES to large, settles
                label.Scale = Vector3.One * 0.01f;
                tween.TweenProperty(label, "scale", Vector3.One * 1.5f, 0.3f)
                    .SetTrans(Tween.TransitionType.Back)
                    .SetEase(Tween.EaseType.Out);
                // Then settle to normal
                var settle = parent.CreateTween();
                settle.TweenInterval(0.3f);
                settle.TweenProperty(label, "scale", Vector3.One, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.InOut);
            }

            // Fade out
            tween.TweenProperty(label, "modulate:a", 0f, 1.5f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In)
                .SetDelay(0.5f);

            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(label)) label.QueueFree();
            }));
        }

        private static void PlaySfx(string sfxName)
        {
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySFXByName(sfxName);
        }

        private static void QueueCommentary(string speaker, string stringKey,
            CommentaryPriority priority, CommentaryCategory category)
        {
            if (ServiceLocator.TryGet<CommentaryManager>(out var commentary))
            {
                string text = StringLoader.GetRandom(stringKey);
                if (!string.IsNullOrEmpty(text))
                    commentary.QueueLine(speaker, text, priority, category);
            }
        }

        private static void DelayedCall(Node context, float delay, System.Action action)
        {
            if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;
            context.GetTree().CreateTimer(delay).Timeout += () => action();
        }
    }
}
