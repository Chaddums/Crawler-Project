using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Right-click a placed tower to inspect stats, sell, or attach mods.
    /// </summary>
    public partial class TowerInspector : CanvasLayer
    {
        private PanelContainer _panel;
        private VBoxContainer _content;
        private TowerController _selectedTower;
        private MapGrid _grid;
        private Pathfinder _pathfinder;

        public override void _Ready()
        {
            _grid = ServiceLocator.Get<MapGrid>();
            _pathfinder = ServiceLocator.Get<Pathfinder>();
            BuildPanel();
            ServiceLocator.Register(this);
        }

        private void BuildPanel()
        {
            _panel = new PanelContainer();
            _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _panel.CustomMinimumSize = new Vector2(260, 0);
            _panel.Visible = false;

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.07f, 0.06f, 0.9f);
            style.BorderColor = new Color(0.5f, 0.4f, 0.2f, 0.8f);
            style.BorderWidthBottom = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 10;
            style.ContentMarginBottom = 10;
            _panel.AddThemeStyleboxOverride("panel", style);
            AddChild(_panel);

            _content = new VBoxContainer();
            _content.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(_content);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Right)
                {
                    // Check if clicking a tower
                    var tower = RaycastTower(mb.Position);
                    if (tower != null)
                    {
                        ShowInspector(tower, mb.Position);
                        GetViewport().SetInputAsHandled();
                    }
                    else
                    {
                        Hide();
                    }
                }
                else if (mb.ButtonIndex == MouseButton.Left && _panel.Visible)
                {
                    // Close if clicking outside panel
                    if (!_panel.GetGlobalRect().HasPoint(mb.Position))
                        Hide();
                }
            }
        }

        private TowerController RaycastTower(Vector2 mousePos)
        {
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return null;

            var from = camera.ProjectRayOrigin(mousePos);
            var dir = camera.ProjectRayNormal(mousePos);
            if (Mathf.Abs(dir.Y) < 0.001f) return null;
            float t = -from.Y / dir.Y;
            if (t < 0) return null;
            var worldPos = from + dir * t;

            var gridPos = _grid.WorldToGrid(worldPos);
            var towerNode = _grid.GetTower(gridPos.X, gridPos.Y);
            return towerNode as TowerController;
        }

        private void ShowInspector(TowerController tower, Vector2 screenPos)
        {
            _selectedTower = tower;

            // Clear old content
            foreach (var child in _content.GetChildren())
                child.QueueFree();

            // Title
            var title = new Label();
            title.Text = tower.Data.Name;
            title.AddThemeFontSizeOverride("font_size", 22);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            _content.AddChild(title);

            // Description
            var desc = new Label();
            desc.Text = tower.Data.Description;
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.AddThemeFontSizeOverride("font_size", 13);
            desc.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.5f));
            _content.AddChild(desc);

            // Stats
            AddStatLine("Damage", tower.Stats.GetStat(StatType.AttackDamage));
            AddStatLine("Range", tower.Stats.GetStat(StatType.Range));
            AddStatLine("Fire Rate", tower.Stats.GetStat(StatType.AttackSpeed), "/s");
            if (tower.Stats.GetStat(StatType.SplashRadius) > 0)
                AddStatLine("Splash", tower.Stats.GetStat(StatType.SplashRadius));

            // Mod slots
            var modLabel = new Label();
            modLabel.Text = $"Mods: {tower.Mods.Count}/{tower.Data.MaxModSlots}";
            modLabel.AddThemeFontSizeOverride("font_size", 14);
            modLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.9f));
            _content.AddChild(modLabel);

            foreach (var mod in tower.Mods)
            {
                var modLine = new Label();
                modLine.Text = $"  + {mod}";
                modLine.AddThemeFontSizeOverride("font_size", 12);
                modLine.AddThemeColorOverride("font_color", new Color(0.4f, 0.6f, 0.8f));
                _content.AddChild(modLine);
            }

            // Sell button
            var sep = new HSeparator();
            _content.AddChild(sep);

            var sellBtn = new Button();
            sellBtn.Text = $"Sell ({tower.GetSellValue()} scrap)";
            sellBtn.CustomMinimumSize = new Vector2(0, 35);
            sellBtn.Pressed += () => SellTower(tower);
            _content.AddChild(sellBtn);

            // Attach mod button (if has slots and player has components)
            if (tower.CanAttachMod() && ServiceLocator.TryGet<FabricationSystem>(out var fab))
            {
                foreach (ModComponentType modType in System.Enum.GetValues(typeof(ModComponentType)))
                {
                    if (fab.GetComponentCount(modType) <= 0) continue;
                    var attachBtn = new Button();
                    attachBtn.Text = $"Attach {modType} ({fab.GetComponentCount(modType)})";
                    attachBtn.CustomMinimumSize = new Vector2(0, 30);
                    var captured = modType;
                    attachBtn.Pressed += () =>
                    {
                        fab.TryAttachMod(tower, captured);
                        ShowInspector(tower, screenPos); // Refresh
                    };
                    _content.AddChild(attachBtn);
                }
            }

            // Position near mouse
            _panel.Position = new Vector2(
                Mathf.Min(screenPos.X + 15, GetViewport().GetVisibleRect().Size.X - 280),
                Mathf.Min(screenPos.Y, GetViewport().GetVisibleRect().Size.Y - 300)
            );
            _panel.Visible = true;
        }

        private void AddStatLine(string name, float value, string suffix = "")
        {
            var line = new Label();
            line.Text = $"{name}: {value:F1}{suffix}";
            line.AddThemeFontSizeOverride("font_size", 14);
            _content.AddChild(line);
        }

        private void SellTower(TowerController tower)
        {
            int refund = tower.GetSellValue();
            if (ServiceLocator.TryGet<ScrapManager>(out var scrap))
                scrap.AddScrap(refund);

            _grid.RemoveTower(tower.GridPosition.X, tower.GridPosition.Y);
            _pathfinder.RecalculateAllPaths();
            GameEvents.OnTowerSold?.Invoke(tower);
            tower.QueueFree();
            Hide();
        }

        private new void Hide()
        {
            _panel.Visible = false;
            _selectedTower = null;
        }

        public override void _ExitTree()
        {
            ServiceLocator.Unregister<TowerInspector>();
        }
    }
}
