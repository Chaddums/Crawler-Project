using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The Company Store (from the Command Center): banked Resources buy looks for BIT and its
    /// frames, the towers and the Spire (<see cref="Cosmetics"/>, Data/cosmetics.json). On the
    /// left BIT, a tower and the Spire stand as they would look; pointing at an item puts it on
    /// them. BUY asks once more before it spends (nothing sells back); an owned item is worn or
    /// taken off with a click. Esc goes back.
    /// </summary>
    public partial class CompanyStoreScreen : CanvasLayer
    {
        private static readonly Color Amber = new(1f, 0.78f, 0.3f);
        private static readonly Color Dim = new(0.62f, 0.68f, 0.76f);
        private static readonly Color Good = new(0.4f, 0.99f, 0.9f);

        private MetaPerkSaveData _save;
        private Label _bank, _detailName, _detailLine;
        private VBoxContainer _sections;
        private StorePreview _preview;
        private string _confirming;
        private readonly Dictionary<string, Button> _buttons = new();

        /// <summary>Tests: the action button of an item ("standard:paint" for a slot's standard issue).</summary>
        public Button ButtonFor(string id) => _buttons.TryGetValue(id, out var b) && IsInstanceValid(b) ? b : null;
        public string BankText => _bank?.Text ?? "";
        internal StorePreview Preview => _preview;

        public override void _Ready()
        {
            Layer = 10;
            _save = GameManager.Instance?.MetaSave ?? MetaPerkSave.Load();
            Cosmetics.ClearPreview();
            Build();
        }

        public override void _ExitTree() => Cosmetics.ClearPreview();

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            {
                GetViewport().SetInputAsHandled();
                GameManager.Instance?.ShowMetaHub();
            }
        }

        private void Build()
        {
            var bg = new ColorRect { Color = new Color(0.024f, 0.035f, 0.07f) };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);
            AddChild(new GridBackdrop { Modulate = new Color(1, 1, 1, 0.5f) });

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 40);
            margin.AddThemeConstantOverride("margin_right", 40);
            margin.AddThemeConstantOverride("margin_top", 28);
            margin.AddThemeConstantOverride("margin_bottom", 26);
            AddChild(margin);
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 14);
            margin.AddChild(root);

            // Header: what this is, and the bank
            var header = new HBoxContainer();
            root.AddChild(header);
            var titles = new VBoxContainer();
            titles.AddThemeConstantOverride("separation", 2);
            header.AddChild(titles);
            titles.AddChild(L("COMPANY STORE", 34, Amber));
            titles.AddChild(L("Banked Resources buy looks for BIT, your towers and the Spire. Nothing here changes how anything fights. Nothing sells back.", 16, Dim));
            header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _bank = L("", 24, Amber);
            _bank.VerticalAlignment = VerticalAlignment.Center;
            header.AddChild(_bank);

            var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 28);
            root.AddChild(body);

            // Left: the three as they would look, and what is pointed at
            var left = new VBoxContainer { CustomMinimumSize = new Vector2(600, 0) };
            left.AddThemeConstantOverride("separation", 8);
            body.AddChild(left);
            _preview = new StorePreview { CustomMinimumSize = new Vector2(600, 640), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            left.AddChild(_preview);
            _detailName = L("", 20, Amber);
            left.AddChild(_detailName);
            _detailLine = L("", 16, new Color(0.86f, 0.89f, 0.94f));
            _detailLine.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _detailLine.CustomMinimumSize = new Vector2(600, 48);
            left.AddChild(_detailLine);

            // Right: the shelves
            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            body.AddChild(scroll);
            _sections = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _sections.AddThemeConstantOverride("separation", 10);
            scroll.AddChild(_sections);

            var bottom = new HBoxContainer();
            root.AddChild(bottom);
            var back = new Button { Text = "◀  Command Center  [Esc]", CustomMinimumSize = new Vector2(300, 46) };
            back.AddThemeFontSizeOverride("font_size", 17);
            back.Pressed += () => GameManager.Instance?.ShowMetaHub();
            bottom.AddChild(back);

            Refresh();
            ShowDetail(null, null);
        }

        /// <summary>The bank and every shelf, as the save stands now.</summary>
        private void Refresh()
        {
            _bank.Text = $"Banked: {_save.MetaResources:N0} Resources";
            foreach (var c in _sections.GetChildren()) c.QueueFree();
            _buttons.Clear();
            foreach (var slot in Cosmetics.Slots)
            {
                var head = new HBoxContainer();
                head.AddThemeConstantOverride("separation", 12);
                head.AddChild(L(slot.Name.ToUpperInvariant(), 20, Good));
                var what = L(slot.What, 15, Dim);
                what.VerticalAlignment = VerticalAlignment.Bottom;
                head.AddChild(what);
                _sections.AddChild(head);

                var shelf = new HFlowContainer();
                shelf.AddThemeConstantOverride("h_separation", 10);
                shelf.AddThemeConstantOverride("v_separation", 10);
                _sections.AddChild(shelf);
                shelf.AddChild(Card(slot, null));
                foreach (var item in Cosmetics.In(slot.Id)) shelf.AddChild(Card(slot, item));
                _sections.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
            }
        }

        private Control Card(CosmeticSlot slot, CosmeticItem item)
        {
            string key = item?.Id ?? $"standard:{slot.Id}";
            var wornNow = _save.WornCosmetics.TryGetValue(slot.Id, out var w) && Cosmetics.Owns(_save, w) ? w : null;
            bool owned = item == null || Cosmetics.Owns(_save, item.Id);
            bool worn = item == null ? wornNow == null : wornNow == item.Id;
            string why = item == null ? null : Cosmetics.WhyNot(_save, item);

            var card = new PanelContainer { CustomMinimumSize = new Vector2(228, 122) };
            var style = new StyleBoxFlat { BgColor = new Color(0.04f, 0.05f, 0.08f, 0.96f), BorderColor = worn ? Good : owned ? new Color(0.35f, 0.5f, 0.6f) : new Color(0.25f, 0.27f, 0.32f) };
            style.SetBorderWidthAll(worn ? 2 : 1);
            style.SetCornerRadiusAll(6);
            style.ContentMarginLeft = 12; style.ContentMarginRight = 12; style.ContentMarginTop = 9; style.ContentMarginBottom = 9;
            card.AddThemeStyleboxOverride("panel", style);
            card.MouseEntered += () => { Cosmetics.SetPreview(slot.Id, item, standardIssue: item == null); _preview.Rebuild(); ShowDetail(slot, item); };
            card.MouseExited += () =>
            {
                // Moving onto the card's own button is not leaving the card
                if (IsInstanceValid(card) && card.GetGlobalRect().HasPoint(card.GetGlobalMousePosition())) return;
                Cosmetics.SetPreview(slot.Id, null); _preview.Rebuild(); ShowDetail(null, null);
            };

            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            box.AddThemeConstantOverride("separation", 5);
            card.AddChild(box);

            var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            top.AddThemeConstantOverride("separation", 5);
            box.AddChild(top);
            foreach (var c in Swatches(item)) top.AddChild(Chip(c));
            top.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
            if (item != null && item.RequiresCampaigns > 0)
                top.AddChild(L($"CAMPAIGN {NewCampaign.Roman(item.RequiresCampaigns)}", 12, Amber));

            box.AddChild(L(item?.Name ?? "Standard issue", 17, Colors.White));
            string state = worn ? "Worn" : owned ? "Owned" : $"{item.Price:N0} Resources";
            box.AddChild(L(state, 14, worn ? Good : owned ? Dim : Amber));

            var btn = new Button { CustomMinimumSize = new Vector2(0, 32), FocusMode = Control.FocusModeEnum.None, MouseFilter = Control.MouseFilterEnum.Pass };
            btn.AddThemeFontSizeOverride("font_size", 14);
            if (worn) { btn.Text = item == null ? "WORN" : "TAKE OFF"; btn.Disabled = item == null; }
            else if (owned) btn.Text = "WEAR";
            else if (why != null) { btn.Text = why; btn.Disabled = true; }
            else btn.Text = _confirming == key ? $"CONFIRM: SPEND {item.Price:N0}" : "BUY";
            btn.Pressed += () => Act(slot, item);
            box.AddChild(btn);
            _buttons[key] = btn;
            return card;
        }

        /// <summary>A card's button: wear, take off, or buy (the second press of BUY spends).</summary>
        public void Act(CosmeticSlot slot, CosmeticItem item)
        {
            string key = item?.Id ?? $"standard:{slot.Id}";
            if (item == null || Cosmetics.Owns(_save, item.Id))
            {
                bool wornNow = item != null && _save.WornCosmetics.TryGetValue(slot.Id, out var w) && w == item.Id;
                Cosmetics.Wear(_save, slot.Id, wornNow ? null : item);
                _confirming = null;
            }
            else if (_confirming == key)
            {
                _confirming = null;
                if (Cosmetics.Buy(_save, item))
                    GD.Print($"[Store] {item.Name} is on");
            }
            else
            {
                if (Cosmetics.WhyNot(_save, item) != null) return;
                _confirming = key;
            }
            Cosmetics.SetPreview(slot.Id, null);
            Refresh();
            _preview.Rebuild();
        }

        private void ShowDetail(CosmeticSlot slot, CosmeticItem item)
        {
            if (slot == null)
            {
                _detailName.Text = "Point at an item to see it on.";
                _detailLine.Text = "";
                return;
            }
            _detailName.Text = item == null ? $"{slot.Name}: standard issue" : $"{slot.Name}: {item.Name}";
            _detailLine.Text = item == null ? "What AXIS issued. Free, and it shows." : item.Line;
        }

        private static IEnumerable<Color> Swatches(CosmeticItem item)
        {
            if (item == null) { yield return new Color(0.3f, 0.33f, 0.4f); yield break; }
            if (item.HullLight is { } hl) yield return hl;
            if (item.HullDark is { } hd) yield return hd;
            if (item.Glow is { } g) yield return g;
            if (item.Rim is { } r) yield return r;
        }

        private static Control Chip(Color c)
        {
            var chip = new Panel { CustomMinimumSize = new Vector2(26, 16), MouseFilter = Control.MouseFilterEnum.Ignore };
            var s = new StyleBoxFlat { BgColor = c, BorderColor = new Color(1, 1, 1, 0.35f) };
            s.SetBorderWidthAll(1);
            s.SetCornerRadiusAll(3);
            chip.AddThemeStyleboxOverride("panel", s);
            return chip;
        }

        private static Label L(string text, int size, Color color)
        {
            var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            return l;
        }
    }

    /// <summary>
    /// The store's stage: BIT, a Junk Turret and the Spire in a small 3D view, wearing whatever
    /// <see cref="Cosmetics"/> says is worn (the pointed-at item included). <see cref="Rebuild"/>
    /// repaints them after a change.
    /// </summary>
    public partial class StorePreview : SubViewportContainer
    {
        private SubViewport _view;
        private Node3D _stage, _bit, _tower, _spire;
        private float _turn;

        /// <summary>Tests: what stands on the stage.</summary>
        internal Node3D Bit => _bit;
        internal Node3D Tower => _tower;
        internal Node3D Spire => _spire;
        internal int Rebuilds { get; private set; }

        public override void _Ready()
        {
            Stretch = true;
            MouseFilter = MouseFilterEnum.Ignore;
            _view = new SubViewport { OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(_view);

            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.02f, 0.03f, 0.055f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.5f, 0.56f, 0.7f), AmbientLightEnergy = 0.55f,
                GlowEnabled = true, GlowIntensity = 0.7f, GlowBloom = 0.08f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            };
            _view.AddChild(new WorldEnvironment { Environment = env });
            _view.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, -35, 0), LightEnergy = 1.25f });
            _view.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 140, 0), LightEnergy = 0.45f, LightColor = new Color(0.6f, 0.75f, 1f) });
            var cam = new Camera3D { Fov = 40f, Position = new Vector3(0f, 3.6f, 11.6f) };
            _view.AddChild(cam);
            cam.LookAtFromPosition(cam.Position, new Vector3(0f, 2.45f, 0f), Vector3.Up);

            _stage = new Node3D { Name = "Stage" };
            _view.AddChild(_stage);
            var floor = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 4.6f, BottomRadius = 4.8f, Height = 0.12f, RadialSegments = 48 },
                Position = new Vector3(0, -0.06f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.06f, 0.09f), Metallic = 0.3f, Roughness = 0.6f },
            };
            _stage.AddChild(floor);
            CallDeferred(MethodName.Rebuild);
        }

        public override void _Process(double delta)
        {
            // A slow turn, so the edge lights and the glow are seen from more than one side
            _turn += (float)delta * 0.35f;
            if (_bit != null && IsInstanceValid(_bit)) _bit.Rotation = new Vector3(0, 0.45f + Mathf.Sin(_turn) * 0.5f, 0);
            if (_tower != null && IsInstanceValid(_tower)) _tower.Rotation = new Vector3(0, -0.5f + Mathf.Sin(_turn * 0.8f + 1f) * 0.6f, 0);
        }

        /// <summary>Build (or repaint) the three under what is worn now.</summary>
        public void Rebuild()
        {
            if (_stage == null || !IsInstanceValid(_stage)) return;
            Rebuilds++;
            BuildBit();
            BuildTower();
            BuildSpire();
        }

        private void BuildBit()
        {
            if (_bit == null || !IsInstanceValid(_bit))
            {
                _bit = new Node3D { Name = "Bit", Position = new Vector3(-1.85f, 0, 2.0f) };
                _stage.AddChild(_bit);
                var model = VinePlayer.BuildPreviewBody(_bit, 1.9f);
                if (model == null) return;
            }
            VinePlayer.PaintPreviewBody(_bit);
        }

        private void BuildTower()
        {
            if (_tower != null && IsInstanceValid(_tower)) _tower.QueueFree();
            _tower = new Node3D { Name = "Tower", Position = new Vector3(1.85f, 0, 2.0f) };
            _stage.AddChild(_tower);
            var sheet = TowerSheet.Load("damage_tower");
            if (sheet == null) return;
            var look = TowerLook.Build(sheet, VineNodeRegistry.Get(VineNodeType.DamageTower)?.TintColor ?? Colors.White, 0f);
            look.Scale = Vector3.One * 1.25f;
            _tower.AddChild(look);
            // The planet's own edge light on Grid Prime when no trim is worn
            BitPalette.ApplyAccentRim(look, Cosmetics.TowerRim ?? new Color(1.0f, 0.58f, 0.18f), Cosmetics.RimStrength(Cosmetics.Worn(Cosmetics.Trim), 0.9f));
        }

        private void BuildSpire()
        {
            if (_spire != null && IsInstanceValid(_spire)) _spire.QueueFree();
            _spire = new Node3D { Name = "Spire", Position = new Vector3(0f, 0, -1.4f) };
            _stage.AddChild(_spire);
            var data = SpireData.Get(GameManager.Instance?.SelectedRole ?? "Obelisk") ?? SpireData.Get("Obelisk");
            var scene = string.IsNullOrEmpty(data?.ModelPath) ? null : GD.Load<PackedScene>(data.ModelPath);
            if (scene == null) return;
            var model = scene.Instantiate<Node3D>();
            _spire.AddChild(model);
            var box = AssetLibrary.GetCombinedAABB(model);
            float scale = 4.3f / Mathf.Max(0.01f, box.Size.Y);
            model.Scale = Vector3.One * scale;
            model.Position = new Vector3(0, -box.Position.Y * scale, 0);
            ModelLook.Tame(model);
            var lights = Cosmetics.SpireLights;
            BitPalette.ApplyAccentRim(model, lights?.Rim ?? new Color(1.0f, 0.58f, 0.18f), Cosmetics.RimStrength(lights, 0.9f));
            var crownSheet = string.IsNullOrEmpty(lights?.Crown) ? null : TowerSheet.Load(lights.Crown);
            if (crownSheet == null) return;
            var crown = TowerLook.Build(crownSheet, lights.Rim ?? Colors.White, 0f);
            crown.Name = "SpireCrown";
            // The crown at the size it has on the Spire in a run (the model here is fitted to the stage)
            float inRun = Mathf.Max(0.01f, data.ModelScale);
            crown.Scale = Vector3.One * Cosmetics.CrownScaleFor(lights, Mathf.Max(box.Size.X, box.Size.Z) * inRun) * scale / inRun;
            crown.Position = new Vector3(0, 4.3f, 0);
            _spire.AddChild(crown);
        }
    }
}
