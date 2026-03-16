using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Represents a stacked group of identical items for display in the ceremony.
    /// </summary>
    public class RevealEntry
    {
        public ItemInstance Item;
        public int Count;
        public string DisplayName => Count > 1 ? $"{Item.GetDisplayName()} x{Count}" : Item.GetDisplayName();
    }

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
        private List<RevealEntry> _revealEntries;
        private LootBoxTier _tier;
        private ItemRarity _bestRarity;
        private PlayerController _targetPlayer;
        private bool _collected;
        private bool _ceremonyActive;
        private bool _isPremium;
        private readonly List<AnimatedSprite3D> _cardSprites = new();

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
            plane.Size = new Vector2(4.5f, 3.5f);
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
            scanMesh.Size = new Vector3(4.3f, 0.005f, 0.015f);
            scanline.Mesh = scanMesh;
            var scanMat = new StandardMaterial3D();
            scanMat.AlbedoColor = new Color(0.3f, 0.6f, 1f, 0.6f);
            scanMat.EmissionEnabled = true;
            scanMat.Emission = new Color(0.3f, 0.6f, 1f);
            scanMat.EmissionEnergyMultiplier = 2f;
            scanMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            scanline.MaterialOverride = scanMat;
            scanline.Position = new Vector3(0, 1.65f, 0.01f);
            AddChild(scanline);

            // Bottom scanline accent
            var scanlineBot = new MeshInstance3D();
            scanlineBot.Mesh = scanMesh;
            scanlineBot.MaterialOverride = scanMat;
            scanlineBot.Position = new Vector3(0, -1.65f, 0.01f);
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
            _collectPrompt.FontSize = 36;
            _collectPrompt.Position = new Vector3(0, -1.4f, 0.02f);
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

            // Free card effect sprites
            foreach (var sprite in _cardSprites)
                if (IsInstanceValid(sprite)) sprite.QueueFree();
            _cardSprites.Clear();

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
            _isPremium = _tier >= LootBoxTier.Gold;
            _revealedItems = LootBoxFactory.OpenLootBox(boxData);

            _bestRarity = ItemRarity.Common;
            foreach (var item in _revealedItems)
                if (item.Rarity > _bestRarity) _bestRarity = item.Rarity;

            _revealEntries = StackItems(_revealedItems);
            _collected = false;
            _ceremonyActive = true;
            SetActive(true);

            var tierColor = GetTierColor(_tier);
            _backdropMat.Emission = tierColor * 0.3f;
            _glowLight.LightColor = tierColor;

            // Premium boxes get closer camera
            if (_isPremium && ServiceLocator.TryGet<IsometricCamera>(out var cam))
            {
                var displayCenter = GlobalPosition + Vector3.Up * 0.5f;
                float zoom = _tier >= LootBoxTier.Diamond ? 5.5f : 6.5f;
                cam.ZoomToTarget(displayCenter, zoom, 0.6f);
            }

            AnimateOpening();
        }

        public void StartBatchCeremony(List<LootBoxData> boxes, PlayerController player)
        {
            _targetPlayer = player;
            _tier = LootBoxTier.Bronze;
            _isPremium = false;
            foreach (var box in boxes)
                if (box.Tier > _tier) _tier = box.Tier;

            _revealedItems = new List<ItemInstance>();
            foreach (var box in boxes)
                _revealedItems.AddRange(LootBoxFactory.OpenLootBox(box));

            _bestRarity = ItemRarity.Common;
            foreach (var item in _revealedItems)
                if (item.Rarity > _bestRarity) _bestRarity = item.Rarity;

            _revealEntries = StackItems(_revealedItems);
            _collected = false;
            _ceremonyActive = true;
            SetActive(true);

            var tierColor = GetTierColor(_tier);
            _backdropMat.Emission = tierColor * 0.3f;
            _glowLight.LightColor = tierColor;

            GD.Print($"[HolographicDisplay] Batch ceremony: {boxes.Count} boxes, {_revealedItems.Count} items → {_revealEntries.Count} entries (stacked)");
            AnimateOpening();
        }

        /// <summary>
        /// Stack identical items (same BaseData.Id and Rarity) into RevealEntries.
        /// Equipment is never stacked (each piece has unique affixes).
        /// </summary>
        private static List<RevealEntry> StackItems(List<ItemInstance> items)
        {
            var entries = new List<RevealEntry>();
            var stackMap = new Dictionary<string, RevealEntry>();

            foreach (var item in items)
            {
                // Only stack non-equipment (consumables, crafting mats, etc.)
                bool canStack = item.BaseData is not EquipmentData;
                string key = $"{item.BaseData.Id}_{item.Rarity}";

                if (canStack && stackMap.TryGetValue(key, out var existing))
                {
                    existing.Count++;
                }
                else
                {
                    var entry = new RevealEntry { Item = item, Count = 1 };
                    entries.Add(entry);
                    if (canStack)
                        stackMap[key] = entry;
                }
            }

            return entries;
        }

        private void AnimateOpening()
        {
            var tween = CreateTween();

            // Bronze/Silver batch: skip all ceremony, just show items fast
            if (!_isPremium && _tier <= LootBoxTier.Silver)
            {
                _backdropMat.AlbedoColor = new Color(0.03f, 0.06f, 0.12f, 0f);
                tween.TweenProperty(_backdropMat, "albedo_color:a", 0.5f, 0.15f);
                tween.Parallel().TweenProperty(_glowLight, "light_energy", 1f, 0.15f);
                tween.TweenCallback(Callable.From(RevealItems));
                return;
            }

            // 1. Power on — backdrop fades in, light ramps
            _backdropMat.AlbedoColor = new Color(0.03f, 0.06f, 0.12f, 0f);
            float fadeIn = 0.4f;
            tween.TweenProperty(_backdropMat, "albedo_color:a", 0.5f, fadeIn);
            tween.Parallel().TweenProperty(_glowLight, "light_energy", 2f, fadeIn);

            // 2. Loot box model appears
            tween.TweenCallback(Callable.From(() => SpawnBoxModel()));

            // 3. Tier-scaled shake — premium boxes build tension
            float shakeDuration = _tier switch
            {
                LootBoxTier.Gold => 0.8f,
                LootBoxTier.Diamond => 1.4f,
                LootBoxTier.Legendary => 2.0f,
                LootBoxTier.Celestial => 2.8f,
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

                // Sprite VFX burst behind display
                SpriteVfxLibrary.SpawnLootBoxEffect(GetTree().Root, GlobalPosition + Vector3.Up * 0.3f, _tier, 3f);

                // Burst the box model
                if (_boxModel != null && IsInstanceValid(_boxModel))
                {
                    var burst = CreateTween();
                    burst.TweenProperty(_boxModel, "scale", Vector3.One * 1.5f, 0.1f);
                    foreach (var child in _boxModel.GetChildren())
                    {
                        if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D meshMat)
                        {
                            meshMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                            burst.Parallel().TweenProperty(meshMat, "albedo_color:a", 0f, 0.15f);
                        }
                    }
                    burst.TweenCallback(Callable.From(() =>
                    {
                        _boxModel?.QueueFree();
                        _boxModel = null;
                    }));
                }

                // Brighten backdrop
                var brightTween = CreateTween();
                brightTween.TweenProperty(_backdropMat, "emission_energy_multiplier", 2f, 0.2f);
                brightTween.TweenProperty(_backdropMat, "emission_energy_multiplier", 0.8f, 0.5f);
            }));

            tween.TweenInterval(0.4f);

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
            bool isBatch = !_isPremium && _tier <= LootBoxTier.Silver;

            // Tier-scaled stagger — batch is instant, premium is dramatic
            float stagger = isBatch ? 0.04f : _tier switch
            {
                LootBoxTier.Gold => 0.5f,
                LootBoxTier.Diamond => 0.7f,
                LootBoxTier.Legendary => 0.9f,
                LootBoxTier.Celestial => 1.1f,
                _ => 0.3f
            };

            int total = _revealEntries.Count;

            // Dynamic row spacing: fit all entries within the display height (3.0 usable with bigger backdrop)
            float startY = 1.3f;
            float maxSpacing = 0.32f;
            float minSpacing = 0.18f;
            float rowSpacing = total <= 1 ? maxSpacing : Mathf.Clamp(2.6f / total, minSpacing, maxSpacing);

            float delay = 0f;

            for (int i = 0; i < total; i++)
            {
                var entry = _revealEntries[i];
                int capturedIdx = i;
                float rowY = startY - i * rowSpacing;
                var capturedEntry = entry;

                var itemTween = CreateTween();
                itemTween.TweenInterval(delay);
                itemTween.TweenCallback(Callable.From(() =>
                {
                    var row = CreateItemRow(capturedEntry, rowY);
                    _itemContainer.AddChild(row);

                    // Slide in from right (batch: quick pop, premium: dramatic slide)
                    float startX = isBatch ? 1.0f : 2.5f;
                    row.Position = new Vector3(startX, rowY, 0);
                    var slideTween = CreateTween();
                    float slideSpeed = isBatch ? 0.1f : (_isPremium ? 0.3f : 0.15f);
                    slideTween.TweenProperty(row, "position:x", 0f, slideSpeed)
                        .SetEase(Tween.EaseType.Out)
                        .SetTrans(Tween.TransitionType.Back);

                    if (ServiceLocator.TryGet<AudioManager>(out var audio))
                        audio.PlaySFXByName("item_reveal");

                    // Card VFX behind item row for Rare+ items
                    if (capturedEntry.Item.Rarity >= ItemRarity.Rare)
                    {
                        var cardPos = GlobalPosition + new Vector3(0, rowY * 0.45f, -0.05f);
                        float cardScale = capturedEntry.Item.Rarity >= ItemRarity.Legendary ? 1.2f : 0.8f;
                        var card = SpriteVfxLibrary.SpawnCardEffect(GetTree().Root, cardPos, capturedEntry.Item.Rarity, cardScale);
                        if (card != null) _cardSprites.Add(card);
                    }

                    // Narration — only for premium boxes, batch gets nothing
                    if (!isBatch)
                    {
                        string narration = BuildItemNarration(capturedEntry.Item, capturedIdx, total, _tier, _isPremium);
                        if (narration != null && ServiceLocator.TryGet<CommentaryManager>(out var commentary))
                            commentary.QueueLine(narration.StartsWith("BIT:") ? "BIT" : "AXIS",
                                narration, CommentaryPriority.High, CommentaryCategory.LootReaction);
                    }

                    // Epic+ celebration VFX — only for premium boxes
                    if (!isBatch && capturedEntry.Item.Rarity >= ItemRarity.Epic)
                    {
                        var celebTier = CelebrationVfxManager.TierFromRarity(capturedEntry.Item.Rarity);
                        CelebrationVfxManager.Play(GetTree().Root, GlobalPosition + Vector3.Up * 0.3f, celebTier);
                    }
                }));

                // Extra delay for epic items in premium boxes
                if (!isBatch && entry.Item.Rarity >= ItemRarity.Epic)
                    delay += _isPremium ? 0.5f : 0.2f;

                delay += stagger;
            }

            // Show collect prompt after all items
            var promptTween = CreateTween();
            promptTween.TweenInterval(delay + (isBatch ? 0.1f : 0.3f));
            promptTween.TweenCallback(Callable.From(() => _collectPrompt.Visible = true));
        }

        private Node3D CreateItemRow(RevealEntry entry, float y)
        {
            var item = entry.Item;
            var row = new Node3D();
            var rarityColor = GetRarityColor(item.Rarity);

            // Rarity dot
            var dot = new MeshInstance3D();
            dot.Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 6, Rings = 3 };
            var dotMat = new StandardMaterial3D();
            dotMat.AlbedoColor = rarityColor;
            dotMat.EmissionEnabled = true;
            dotMat.Emission = rarityColor;
            dotMat.EmissionEnergyMultiplier = 2f;
            dotMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            dot.MaterialOverride = dotMat;
            dot.Position = new Vector3(-1.9f, 0, 0);
            row.AddChild(dot);

            // Item name (with stack count)
            var nameLabel = new Label3D();
            nameLabel.Text = entry.DisplayName;
            nameLabel.FontSize = 34;
            nameLabel.Position = new Vector3(-0.1f, 0, 0);
            nameLabel.Modulate = rarityColor;
            nameLabel.OutlineModulate = new Color(0, 0, 0);
            nameLabel.OutlineSize = 5;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
            row.AddChild(nameLabel);

            // Rarity label
            var rarityLabel = new Label3D();
            rarityLabel.Text = item.Rarity.ToString();
            rarityLabel.FontSize = 22;
            rarityLabel.Position = new Vector3(1.7f, 0, 0);
            rarityLabel.Modulate = new Color(0.5f, 0.5f, 0.6f);
            rarityLabel.OutlineModulate = new Color(0, 0, 0);
            rarityLabel.OutlineSize = 3;
            rarityLabel.HorizontalAlignment = HorizontalAlignment.Right;
            rarityLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
            row.AddChild(rarityLabel);

            // Affix summary line (below main row) — only for single items, not stacks
            string affixText = null;
            if (entry.Count == 1)
            {
                if (item.BaseData is SalvageCoreItemData coreItem && coreItem.CoreData != null)
                {
                    var desc = coreItem.CoreData.Description;
                    int nl = desc?.IndexOf('\n') ?? -1;
                    affixText = nl >= 0 ? desc[(nl + 1)..] : desc;
                }
                else if (item.Affixes.Count > 0)
                {
                    var parts = item.Affixes.Select(a =>
                    {
                        if (a.Data.ModType == ModifierType.Percent)
                            return $"+{a.RolledValue * 100:F0}% {a.Data.Stat}";
                        string fmt = Mathf.Abs(a.RolledValue) < 1f ? "F2" : "F0";
                        return $"+{a.RolledValue.ToString(fmt)} {a.Data.Stat}";
                    });
                    affixText = string.Join(", ", parts);
                }
            }

            if (!string.IsNullOrEmpty(affixText))
            {
                var affixLabel = new Label3D();
                affixLabel.Text = affixText;
                affixLabel.FontSize = 20;
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

        private static string BuildItemNarration(ItemInstance item, int index, int total, LootBoxTier tier, bool isPremium)
        {
            bool isFirst = index == 0;
            bool isLast = index == total - 1;
            bool isEpicPlus = item.Rarity >= ItemRarity.Epic;

            // Batch boxes: only narrate Epic+ items
            if (!isPremium && tier <= LootBoxTier.Silver && !isEpicPlus) return null;

            string name = item.GetDisplayName();
            string affixText = "";
            if (item.Affixes.Count > 0)
            {
                var parts = item.Affixes.Select(a =>
                {
                    if (a.Data.ModType == ModifierType.Percent)
                        return $"+{a.RolledValue * 100:F0}% {a.Data.Stat}";
                    string fmt = Mathf.Abs(a.RolledValue) < 1f ? "F2" : "F0";
                    return $"+{a.RolledValue.ToString(fmt)} {a.Data.Stat}";
                });
                affixText = $" ({string.Join(", ", parts)})";
            }

            if (isEpicPlus)
                return StringLoader.GetRandom("lootNarration.epicItem", ("{rarity}", item.Rarity.ToString()), ("{name}", name), ("{affixes}", affixText));

            // Premium boxes: AXIS narrates every item dramatically
            if (isPremium)
            {
                if (isFirst)
                    return StringLoader.Get("lootNarration.firstItem", ("{name}", name), ("{affixes}", affixText));
                if (isLast)
                    return StringLoader.Get("lootNarration.lastItem", ("{name}", name), ("{affixes}", affixText));
                return StringLoader.Get("lootNarration.normalItem", ("{name}", name), ("{affixes}", affixText));
            }

            if (isFirst)
                return StringLoader.Get("lootNarration.firstItem", ("{name}", name), ("{affixes}", affixText));
            if (isLast)
                return StringLoader.Get("lootNarration.lastItem", ("{name}", name), ("{affixes}", affixText));

            return null; // Batch non-epic items: no narration
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
