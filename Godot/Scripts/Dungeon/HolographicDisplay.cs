using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// In-world 3D holographic display for loot box ceremony.
    /// Renders loot reveals as Label3D items on a translucent emissive backdrop.
    /// Visible to all players in co-op — no 2D overlay needed.
    /// </summary>
    public partial class HolographicDisplay : Node3D
    {
        private MeshInstance3D _backdrop;
        private StandardMaterial3D _backdropMat;
        private Node3D _boxModel;
        private Node3D _itemContainer;
        private Label3D _collectPrompt;
        private OmniLight3D _glowLight;

        private List<ItemInstance> _revealedItems;
        private LootBoxTier _tier;
        private ItemRarity _bestRarity;
        private PlayerController _targetPlayer;
        private bool _collected;
        private bool _ceremonyActive;

        public event Action CeremonyCollected;
        public bool IsActive => _ceremonyActive;

        public void Initialize()
        {
            Name = "HolographicDisplay";
            BuildBackdrop();
            SetActive(false);
        }

        private void BuildBackdrop()
        {
            // Translucent holographic screen
            _backdrop = new MeshInstance3D();
            var plane = new PlaneMesh();
            plane.Size = new Vector2(3.6f, 2.8f);
            _backdrop.Mesh = plane;

            _backdropMat = new StandardMaterial3D();
            _backdropMat.AlbedoColor = new Color(0.03f, 0.06f, 0.12f, 0.5f);
            _backdropMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _backdropMat.EmissionEnabled = true;
            _backdropMat.Emission = new Color(0.1f, 0.2f, 0.4f);
            _backdropMat.EmissionEnergyMultiplier = 0.5f;
            _backdropMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            _backdropMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _backdrop.MaterialOverride = _backdropMat;

            // Rotate to be vertical, facing +Z (toward the couch)
            _backdrop.RotationDegrees = new Vector3(90, 0, 0);
            AddChild(_backdrop);

            // Scanline accent — thin line at top of display
            var scanline = new MeshInstance3D();
            var scanMesh = new BoxMesh();
            scanMesh.Size = new Vector3(3.4f, 0.005f, 0.015f);
            scanline.Mesh = scanMesh;
            var scanMat = new StandardMaterial3D();
            scanMat.AlbedoColor = new Color(0.3f, 0.6f, 1f, 0.6f);
            scanMat.EmissionEnabled = true;
            scanMat.Emission = new Color(0.3f, 0.6f, 1f);
            scanMat.EmissionEnergyMultiplier = 2f;
            scanMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            scanline.MaterialOverride = scanMat;
            scanline.Position = new Vector3(0, 1.3f, 0.01f);
            AddChild(scanline);

            // Bottom scanline accent
            var scanlineBot = new MeshInstance3D();
            scanlineBot.Mesh = scanMesh;
            scanlineBot.MaterialOverride = scanMat;
            scanlineBot.Position = new Vector3(0, -1.3f, 0.01f);
            AddChild(scanlineBot);

            // Item container
            _itemContainer = new Node3D();
            _itemContainer.Name = "ItemContainer";
            _itemContainer.Position = new Vector3(0, 0, 0.02f); // Slightly in front of backdrop
            AddChild(_itemContainer);

            // Glow light (off by default)
            _glowLight = new OmniLight3D();
            _glowLight.Position = new Vector3(0, 0.3f, 0.8f);
            _glowLight.LightColor = new Color(0.3f, 0.5f, 1f);
            _glowLight.LightEnergy = 0f;
            _glowLight.OmniRange = 5f;
            _glowLight.ShadowEnabled = false;
            AddChild(_glowLight);

            // Collect prompt (hidden)
            _collectPrompt = new Label3D();
            _collectPrompt.Text = "[E] Collect";
            _collectPrompt.FontSize = 32;
            _collectPrompt.Position = new Vector3(0, -1.1f, 0.02f);
            _collectPrompt.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
            _collectPrompt.Modulate = new Color(0.9f, 0.8f, 0.2f);
            _collectPrompt.OutlineModulate = new Color(0, 0, 0);
            _collectPrompt.OutlineSize = 5;
            _collectPrompt.Visible = false;
            AddChild(_collectPrompt);
        }

        public void SetActive(bool active)
        {
            Visible = active;
            if (!active) ClearDisplay();
        }

        private void ClearDisplay()
        {
            // Remove old item rows
            foreach (var child in _itemContainer.GetChildren())
                if (child is Node n) n.QueueFree();

            if (_boxModel != null && IsInstanceValid(_boxModel))
            {
                _boxModel.QueueFree();
                _boxModel = null;
            }

            _collectPrompt.Visible = false;
            _collected = false;
            _ceremonyActive = false;
        }

        // ===== CEREMONY FLOW =====

        public void StartCeremony(LootBoxData boxData, PlayerController player)
        {
            _targetPlayer = player;
            _tier = boxData.Tier;
            _revealedItems = LootBoxFactory.OpenLootBox(boxData);

            _bestRarity = ItemRarity.Common;
            foreach (var item in _revealedItems)
                if (item.Rarity > _bestRarity) _bestRarity = item.Rarity;

            _collected = false;
            _ceremonyActive = true;
            SetActive(true);

            // Set backdrop tint to tier color
            var tierColor = GetTierColor(_tier);
            _backdropMat.Emission = tierColor * 0.3f;
            _glowLight.LightColor = tierColor;

            AnimateOpening();
        }

        public void StartBatchCeremony(List<LootBoxData> boxes, PlayerController player)
        {
            _targetPlayer = player;
            _tier = LootBoxTier.Bronze;
            foreach (var box in boxes)
                if (box.Tier > _tier) _tier = box.Tier;

            _revealedItems = new List<ItemInstance>();
            foreach (var box in boxes)
                _revealedItems.AddRange(LootBoxFactory.OpenLootBox(box));

            _bestRarity = ItemRarity.Common;
            foreach (var item in _revealedItems)
                if (item.Rarity > _bestRarity) _bestRarity = item.Rarity;

            _collected = false;
            _ceremonyActive = true;
            SetActive(true);

            var tierColor = GetTierColor(_tier);
            _backdropMat.Emission = tierColor * 0.3f;
            _glowLight.LightColor = tierColor;

            GD.Print($"[HolographicDisplay] Batch ceremony: {boxes.Count} boxes, {_revealedItems.Count} items");
            AnimateOpening();
        }

        private void AnimateOpening()
        {
            var tween = CreateTween();

            // 1. Power on — backdrop fades in, light ramps
            _backdropMat.AlbedoColor = new Color(0.03f, 0.06f, 0.12f, 0f);
            tween.TweenProperty(_backdropMat, "albedo_color:a", 0.5f, 0.3f);
            tween.Parallel().TweenProperty(_glowLight, "light_energy", 1.5f, 0.3f);

            // 2. Loot box model appears
            tween.TweenCallback(Callable.From(() => SpawnBoxModel()));

            // 3. Tier-scaled shake
            float shakeDuration = _tier switch
            {
                LootBoxTier.Bronze => 0.3f,
                LootBoxTier.Silver => 0.4f,
                LootBoxTier.Gold => 0.7f,
                LootBoxTier.Diamond => 1.2f,
                LootBoxTier.Legendary => 1.8f,
                LootBoxTier.Celestial => 2.5f,
                _ => 0.5f
            };
            float shakeIntensity = _bestRarity switch
            {
                ItemRarity.Common => 0.02f,
                ItemRarity.Uncommon => 0.03f,
                ItemRarity.Rare => 0.05f,
                ItemRarity.Epic => 0.08f,
                _ => 0.12f
            };

            tween.TweenCallback(Callable.From(() =>
            {
                ShakeBoxModel(shakeDuration, shakeIntensity);
                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("box_shake");
            }));
            tween.TweenInterval(shakeDuration);

            // 4. Box burst
            tween.TweenCallback(Callable.From(() =>
            {
                if (ServiceLocator.TryGet<AudioManager>(out var audio))
                    audio.PlaySFXByName("box_open");

                // Camera shake for Diamond+
                if (_tier >= LootBoxTier.Diamond && ServiceLocator.TryGet<IsometricCamera>(out var cam))
                    cam.Shake(_tier >= LootBoxTier.Legendary ? 0.4f : 0.2f);

                // 3D celebration VFX
                var celebTier = CelebrationVfxManager.TierFromLootBox(_tier, _bestRarity);
                CelebrationVfxManager.Play(GetTree().Root, GlobalPosition, celebTier);

                // Burst the box model
                if (_boxModel != null && IsInstanceValid(_boxModel))
                {
                    var burst = CreateTween();
                    burst.TweenProperty(_boxModel, "scale", Vector3.One * 1.5f, 0.1f);
                    // Fade out each child MeshInstance3D (Node3D has no "transparency" property)
                    foreach (var child in _boxModel.GetChildren())
                    {
                        if (child is MeshInstance3D mesh)
                            burst.Parallel().TweenProperty(mesh, "transparency", 1f, 0.15f);
                    }
                    burst.TweenCallback(Callable.From(() =>
                    {
                        _boxModel?.QueueFree();
                        _boxModel = null;
                    }));
                }

                // Brighten backdrop
                var brightTween = CreateTween();
                brightTween.TweenProperty(_backdropMat, "emission_energy_multiplier", 1.5f, 0.2f);
                brightTween.TweenProperty(_backdropMat, "emission_energy_multiplier", 0.8f, 0.5f);
            }));

            tween.TweenInterval(0.3f);

            // 5. Reveal items
            tween.TweenCallback(Callable.From(RevealItems));
        }

        private void SpawnBoxModel()
        {
            _boxModel = new Node3D();
            _boxModel.Name = "LootBoxModel";

            // Build a simple procedural loot box
            var boxMesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(0.5f, 0.4f, 0.35f);
            boxMesh.Mesh = box;

            var tierColor = GetTierColor(_tier);
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = tierColor * 0.8f;
            mat.EmissionEnabled = true;
            mat.Emission = tierColor;
            mat.EmissionEnergyMultiplier = 1.5f;
            boxMesh.MaterialOverride = mat;
            _boxModel.AddChild(boxMesh);

            // Lid accent
            var lidMesh = new MeshInstance3D();
            var lid = new BoxMesh();
            lid.Size = new Vector3(0.52f, 0.06f, 0.37f);
            lidMesh.Mesh = lid;
            lidMesh.Position = new Vector3(0, 0.23f, 0);
            var lidMat = new StandardMaterial3D();
            lidMat.AlbedoColor = tierColor;
            lidMat.EmissionEnabled = true;
            lidMat.Emission = tierColor;
            lidMat.EmissionEnergyMultiplier = 2f;
            lidMesh.MaterialOverride = lidMat;
            _boxModel.AddChild(lidMesh);

            _boxModel.Position = new Vector3(0, 0.5f, 0.02f);
            _boxModel.Scale = Vector3.Zero;
            AddChild(_boxModel);

            // Scale-in
            var tween = CreateTween();
            tween.TweenProperty(_boxModel, "scale", Vector3.One, 0.3f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);

            // Apply presenter effects
            LootBoxPresenter.Attach(_boxModel, _tier);
        }

        private void ShakeBoxModel(float duration, float maxIntensity)
        {
            if (_boxModel == null) return;
            var basePos = _boxModel.Position;
            var tween = CreateTween();
            int steps = (int)(duration / 0.05f);

            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / steps;
                float intensity = t * maxIntensity;
                float ox = (float)GD.RandRange(-intensity, intensity);
                float oy = (float)GD.RandRange(-intensity, intensity);
                tween.TweenProperty(_boxModel, "position",
                    basePos + new Vector3(ox, oy, 0), 0.05f);
            }
            tween.TweenProperty(_boxModel, "position", basePos, 0.05f);
        }

        private void RevealItems()
        {
            // Tier-scaled stagger
            float stagger = _tier switch
            {
                LootBoxTier.Bronze => 0.15f,
                LootBoxTier.Silver => 0.15f,
                LootBoxTier.Gold => 0.4f,
                LootBoxTier.Diamond => 0.6f,
                LootBoxTier.Legendary => 0.8f,
                LootBoxTier.Celestial => 1.0f,
                _ => 0.3f
            };

            float delay = 0f;
            float startY = 1.0f;
            float rowSpacing = 0.28f;
            int total = _revealedItems.Count;

            for (int i = 0; i < total; i++)
            {
                var item = _revealedItems[i];
                int capturedIdx = i;
                float rowY = startY - i * rowSpacing;
                var capturedItem = item;

                var itemTween = CreateTween();
                itemTween.TweenInterval(delay);
                itemTween.TweenCallback(Callable.From(() =>
                {
                    var row = CreateItemRow(capturedItem, rowY);
                    _itemContainer.AddChild(row);

                    // Slide in from right
                    float startX = 2.5f;
                    row.Position = new Vector3(startX, rowY, 0);
                    var slideTween = CreateTween();
                    slideTween.TweenProperty(row, "position:x", 0f, 0.25f)
                        .SetEase(Tween.EaseType.Out)
                        .SetTrans(Tween.TransitionType.Back);

                    if (ServiceLocator.TryGet<AudioManager>(out var audio))
                        audio.PlaySFXByName("item_reveal");

                    // Narration
                    string narration = BuildItemNarration(capturedItem, capturedIdx, total, _tier);
                    if (narration != null && ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                        commentary.QueueLine(narration.StartsWith("BIT:") ? "BIT" : "AXIS",
                            narration, CommentaryPriority.High, CommentaryCategory.LootReaction);

                    // Epic+ celebration
                    if (capturedItem.Rarity >= ItemRarity.Epic)
                    {
                        var celebTier = CelebrationVfxManager.TierFromRarity(capturedItem.Rarity);
                        CelebrationVfxManager.Play(GetTree().Root, GlobalPosition + Vector3.Up * 0.3f, celebTier);
                    }
                }));

                if (item.Rarity >= ItemRarity.Epic)
                    delay += 0.3f; // Extra breathing room

                delay += stagger;
            }

            // Show collect prompt after all items
            var promptTween = CreateTween();
            promptTween.TweenInterval(delay + 0.3f);
            promptTween.TweenCallback(Callable.From(() => _collectPrompt.Visible = true));
        }

        private Node3D CreateItemRow(ItemInstance item, float y)
        {
            var row = new Node3D();
            var rarityColor = GetRarityColor(item.Rarity);

            // Rarity dot
            var dot = new MeshInstance3D();
            dot.Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 };
            var dotMat = new StandardMaterial3D();
            dotMat.AlbedoColor = rarityColor;
            dotMat.EmissionEnabled = true;
            dotMat.Emission = rarityColor;
            dotMat.EmissionEnergyMultiplier = 2f;
            dotMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            dot.MaterialOverride = dotMat;
            dot.Position = new Vector3(-1.5f, 0, 0);
            row.AddChild(dot);

            // Item name
            var nameLabel = new Label3D();
            nameLabel.Text = item.GetDisplayName();
            nameLabel.FontSize = 28;
            nameLabel.Position = new Vector3(-0.1f, 0, 0);
            nameLabel.Modulate = rarityColor;
            nameLabel.OutlineModulate = new Color(0, 0, 0);
            nameLabel.OutlineSize = 4;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
            row.AddChild(nameLabel);

            // Rarity label
            var rarityLabel = new Label3D();
            rarityLabel.Text = item.Rarity.ToString();
            rarityLabel.FontSize = 18;
            rarityLabel.Position = new Vector3(1.35f, 0, 0);
            rarityLabel.Modulate = new Color(0.5f, 0.5f, 0.6f);
            rarityLabel.OutlineModulate = new Color(0, 0, 0);
            rarityLabel.OutlineSize = 3;
            rarityLabel.HorizontalAlignment = HorizontalAlignment.Right;
            rarityLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
            row.AddChild(rarityLabel);

            // Affix summary line (below main row)
            string affixText = null;
            if (item.BaseData is SalvageCoreItemData coreItem && coreItem.CoreData != null)
            {
                var desc = coreItem.CoreData.Description;
                int nl = desc?.IndexOf('\n') ?? -1;
                affixText = nl >= 0 ? desc[(nl + 1)..] : desc;
            }
            else if (item.Affixes.Count > 0)
            {
                var parts = item.Affixes.Select(a =>
                    a.Data.ModType == ModifierType.Percent
                        ? $"+{a.RolledValue:F0}% {a.Data.Stat}"
                        : $"+{a.RolledValue:F0} {a.Data.Stat}");
                affixText = string.Join(", ", parts);
            }

            if (!string.IsNullOrEmpty(affixText))
            {
                var affixLabel = new Label3D();
                affixLabel.Text = affixText;
                affixLabel.FontSize = 16;
                affixLabel.Position = new Vector3(-0.1f, -0.11f, 0);
                affixLabel.Modulate = new Color(0.4f, 0.6f, 1f);
                affixLabel.OutlineModulate = new Color(0, 0, 0);
                affixLabel.OutlineSize = 3;
                affixLabel.HorizontalAlignment = HorizontalAlignment.Center;
                affixLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
                row.AddChild(affixLabel);
            }

            return row;
        }

        // ===== COLLECTION =====

        /// <summary>
        /// Called by SafeRoomCouch when player presses E to collect items.
        /// </summary>
        public void CollectAll()
        {
            if (_collected || !_ceremonyActive) return;
            _collected = true;

            var player = _targetPlayer ?? PlayerManager.P1;
            if (player != null)
            {
                foreach (var item in _revealedItems)
                    player.Inventory.TryAddItem(item);
            }

            // Fire event
            GameEvents.OnLootBoxOpened?.Invoke(new LootBoxOpenedData
            {
                Tier = _tier,
                Items = new List<object>(_revealedItems.ConvertAll(i => (object)i))
            });

            // Fade out
            var tween = CreateTween();
            tween.TweenProperty(_backdropMat, "albedo_color:a", 0f, 0.4f);
            tween.Parallel().TweenProperty(_glowLight, "light_energy", 0f, 0.4f);

            // Fade item labels
            foreach (var child in _itemContainer.GetChildren())
            {
                if (child is Node3D n)
                {
                    var fadeTween = CreateTween();
                    fadeTween.TweenProperty(n, "scale", Vector3.One * 0.5f, 0.3f);
                }
            }

            tween.TweenCallback(Callable.From(() =>
            {
                _ceremonyActive = false;
                ClearDisplay();
                CeremonyCollected?.Invoke();
            }));

            GD.Print($"[HolographicDisplay] Collected {_revealedItems.Count} items");
        }

        // ===== HELPERS =====

        private static string BuildItemNarration(ItemInstance item, int index, int total, LootBoxTier tier)
        {
            bool isFirst = index == 0;
            bool isLast = index == total - 1;
            bool isEpicPlus = item.Rarity >= ItemRarity.Epic;

            if (tier <= LootBoxTier.Silver && !isEpicPlus) return null;

            string name = item.GetDisplayName();
            string affixText = "";
            if (item.Affixes.Count > 0)
            {
                var parts = item.Affixes.Select(a =>
                    a.Data.ModType == ModifierType.Percent
                        ? $"+{a.RolledValue:F0}% {a.Data.Stat}"
                        : $"+{a.RolledValue:F0} {a.Data.Stat}");
                affixText = $" ({string.Join(", ", parts)})";
            }

            if (isEpicPlus)
                return StringLoader.Get("lootNarration.epicItem", ("{rarity}", item.Rarity.ToString()), ("{name}", name), ("{affixes}", affixText));
            if (isFirst)
                return StringLoader.Get("lootNarration.firstItem", ("{name}", name), ("{affixes}", affixText));
            if (isLast)
                return StringLoader.Get("lootNarration.lastItem", ("{name}", name), ("{affixes}", affixText));

            return StringLoader.Get("lootNarration.normalItem", ("{name}", name), ("{affixes}", affixText));
        }

        private static Color GetTierColor(LootBoxTier tier)
        {
            return tier switch
            {
                LootBoxTier.Bronze => new Color(0.8f, 0.5f, 0.2f),
                LootBoxTier.Silver => new Color(0.8f, 0.8f, 0.9f),
                LootBoxTier.Gold => new Color(1f, 0.84f, 0f),
                LootBoxTier.Diamond => new Color(0.4f, 0.9f, 1f),
                LootBoxTier.Legendary => new Color(0.7f, 0.3f, 0.9f),
                LootBoxTier.Celestial => new Color(1f, 0.95f, 0.7f),
                _ => Colors.White
            };
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.7f, 0.7f, 0.7f),
                ItemRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Epic => new Color(0.7f, 0.3f, 0.9f),
                ItemRarity.Legendary => new Color(1f, 0.5f, 0f),
                ItemRarity.Absurd => new Color(1f, 0.2f, 0.4f),
                _ => Colors.White
            };
        }
    }
}
