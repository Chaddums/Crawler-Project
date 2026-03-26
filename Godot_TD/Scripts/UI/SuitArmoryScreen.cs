using System.Linq;
using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// Suit Armory — browse, inspect, rename, salvage saved suits.
    /// CEF bridge renders ui/suit-armory/index.html.
    /// </summary>
    public partial class SuitArmoryScreen : Control
    {
        private GodotObject _cefTexture;
        private int _selectedIndex = -1;

        public override void _Ready()
        {
            if (CefHelper.Available)
                CreateCefBrowser();
            else
                BuildFallbackUI();
        }

        public override void _ExitTree()
        {
            if (_cefTexture == null) return;
            try { _cefTexture.Set("url", "about:blank"); } catch { /* ignore */ }
            if (_cefTexture is Node cefNode && IsInstanceValid(cefNode))
                cefNode.QueueFree();
            _cefTexture = null;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
            {
                GetViewport().SetInputAsHandled();
                GameManager.Instance?.ShowMetaHub();
            }
        }

        // ── CEF ──

        private void CreateCefBrowser()
        {
            _cefTexture = ClassDB.Instantiate("CefTexture").AsGodotObject();
            if (_cefTexture is not Control cefControl)
            {
                BuildFallbackUI();
                return;
            }

            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.043f, 0.075f, 0.149f);
            AddChild(bg);

            cefControl.SetAnchorsPreset(LayoutPreset.FullRect);
            cefControl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cefControl.SizeFlagsVertical = SizeFlags.ExpandFill;

            _cefTexture.Set("background_color", new Color(0.043f, 0.075f, 0.149f, 1f));
            _cefTexture.Set("enable_accelerated_osr", true);

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);
            _cefTexture.Set("url", "res://ui/suit-armory/index.html");
        }

        private void OnPageLoaded(string url, int httpStatus)
        {
            GD.Print($"[SuitArmory] Loaded: {url} ({httpStatus})");
            PushSuitData();
        }

        private void OnPageError(string url, int code, string text)
        {
            GD.PrintErr($"[SuitArmory] CEF error: {text} ({code}) {url}");
        }

        private void OnConsoleMessage(int level, string msg, string src, int line)
        {
            if (level >= 2) GD.PrintErr($"[SuitArmory/JS] {msg} ({src}:{line})");
        }

        private void OnIpcData(Variant data)
        {
            if (data.Obj is not Godot.Collections.Dictionary dict) return;
            string action = dict.ContainsKey("action") ? dict["action"].AsString() : "";
            Godot.Collections.Dictionary payload = null;
            if (dict.ContainsKey("data") && dict["data"].Obj is Godot.Collections.Dictionary d)
                payload = d;

            int index = payload != null && payload.ContainsKey("index") ? payload["index"].AsInt32() : _selectedIndex;

            GD.Print($"[SuitArmory] IPC: {action} (index={index})");

            switch (action)
            {
                case "back":
                    GameManager.Instance?.ShowMetaHub();
                    break;

                case "suit-selected":
                    _selectedIndex = index;
                    break;

                case "deploy":
                    if (index >= 0)
                    {
                        var suits = SuitManager.GetAll();
                        if (index < suits.Length && suits[index] != null && !suits[index].Consumed)
                        {
                            // Find boss section from current territory, or go to territory to pick
                            GD.Print($"[SuitArmory] Deploy suit {index} — redirecting to territory for site selection");
                            GameManager.Instance?.ShowTerritory();
                        }
                    }
                    break;

                case "salvage":
                    if (index >= 0)
                    {
                        var suits = SuitManager.GetAll();
                        if (index < suits.Length && suits[index] != null && !suits[index].Consumed)
                        {
                            GD.Print($"[SuitArmory] Salvaging suit {index}: {suits[index].Name}");
                            suits[index] = null;
                            SuitManager.Save(suits);
                            _selectedIndex = -1;
                            PushSuitData();
                        }
                    }
                    break;

                case "rename":
                    // TODO: show rename dialog — for now just log
                    GD.Print($"[SuitArmory] Rename requested for suit {index}");
                    break;
            }
        }

        private void PushSuitData()
        {
            var suits = SuitManager.GetAll();
            var arr = new Godot.Collections.Array();

            foreach (var suit in suits)
            {
                if (suit == null || string.IsNullOrEmpty(suit.Name))
                {
                    arr.Add(new Godot.Collections.Dictionary());
                    continue;
                }

                int towers = 0, sensors = 0;
                foreach (var node in suit.Nodes)
                {
                    var data = VineNodeRegistry.Get(node.NodeType);
                    if (data == null) continue;
                    if (data.AutoFires) towers++;
                    else if (data.Category == VineNodeCategory.Sensor) sensors++;
                }

                // Build grid data: flat array of cell categories for the preview
                int gridW = 0, gridH = 0;
                Godot.Collections.Array gridData = null;
                if (suit.Nodes.Count > 0)
                {
                    int minX = int.MaxValue, maxX = 0, minY = int.MaxValue, maxY = 0;
                    foreach (var n in suit.Nodes)
                    {
                        if (n.GridX < minX) minX = n.GridX;
                        if (n.GridX > maxX) maxX = n.GridX;
                        if (n.GridY < minY) minY = n.GridY;
                        if (n.GridY > maxY) maxY = n.GridY;
                    }
                    // Add 1-cell padding
                    minX = Mathf.Max(0, minX - 1);
                    minY = Mathf.Max(0, minY - 1);
                    gridW = maxX - minX + 3;
                    gridH = maxY - minY + 3;
                    // Cap grid size for display
                    if (gridW > 30) gridW = 30;
                    if (gridH > 30) gridH = 30;

                    gridData = new Godot.Collections.Array();
                    // Initialize all empty
                    for (int i = 0; i < gridW * gridH; i++)
                        gridData.Add(0);

                    foreach (var n in suit.Nodes)
                    {
                        int gx = n.GridX - minX;
                        int gy = n.GridY - minY;
                        if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) continue;

                        var nodeData = VineNodeRegistry.Get(n.NodeType);
                        int cat;
                        if (nodeData != null && nodeData.AutoFires) cat = 3; // tower
                        else if (nodeData?.Category == VineNodeCategory.Sensor) cat = 2;
                        else if (nodeData?.Category == VineNodeCategory.Effect) cat = 3;
                        else cat = 1; // structural
                        gridData[gy * gridW + gx] = cat;
                    }
                }

                // Planet name
                var planet = TerritoryManager.GetPlanet(suit.Planet);
                string planetName = planet?.Name ?? $"Planet {suit.Planet}";

                // Relics
                var relics = new Godot.Collections.Array();
                if (suit.RelicNames != null)
                {
                    foreach (var rName in suit.RelicNames)
                    {
                        if (string.IsNullOrEmpty(rName)) continue;
                        var relic = RelicRegistry.All.FirstOrDefault(r => r.Id == rName || r.Name == rName);
                        relics.Add(new Godot.Collections.Dictionary
                        {
                            ["name"] = relic.Name ?? rName,
                            ["icon"] = relic.Icon ?? "auto_awesome",
                        });
                    }
                }

                var suitDict = new Godot.Collections.Dictionary
                {
                    ["name"] = suit.Name,
                    ["role"] = suit.Role ?? "Obelisk",
                    ["planet"] = suit.Planet,
                    ["planetName"] = planetName,
                    ["material"] = (int)suit.Material,
                    ["consumed"] = suit.Consumed,
                    ["created"] = suit.CreatedTimestamp,
                    ["nodes"] = suit.Nodes.Count,
                    ["towers"] = towers,
                    ["sensors"] = sensors,
                    ["relics"] = relics,
                    ["gridW"] = gridW,
                    ["gridH"] = gridH,
                };
                if (gridData != null) suitDict["gridData"] = gridData;

                arr.Add(suitDict);
            }

            string json = Json.Stringify(arr);
            _cefTexture?.Call("eval", $"window.__suitArmoryUI.setSuits({json}, {Constants.MAX_SUIT_SLOTS});");
        }

        // ── Fallback ──

        private void BuildFallbackUI()
        {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.043f, 0.075f, 0.149f);
            AddChild(bg);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 40);
            margin.AddThemeConstantOverride("margin_right", 40);
            margin.AddThemeConstantOverride("margin_top", 30);
            margin.AddThemeConstantOverride("margin_bottom", 30);
            AddChild(margin);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 16);
            margin.AddChild(vbox);

            var title = new Label();
            title.Text = "SUIT ARMORY";
            title.AddThemeFontSizeOverride("font_size", 32);
            title.AddThemeColorOverride("font_color", new Color(0.0f, 0.85f, 0.95f));
            vbox.AddChild(title);

            var suits = SuitManager.GetAll();
            for (int i = 0; i < Constants.MAX_SUIT_SLOTS; i++)
            {
                var suit = suits[i];
                var label = new Label();
                if (suit == null || string.IsNullOrEmpty(suit.Name))
                    label.Text = $"Slot {i + 1}: [EMPTY]";
                else if (suit.Consumed)
                    label.Text = $"Slot {i + 1}: {suit.Name} [DESTROYED]";
                else
                    label.Text = $"Slot {i + 1}: {suit.Name} ({suit.Role}, P{suit.Planet}, {suit.Nodes.Count} nodes)";

                label.AddThemeFontSizeOverride("font_size", 18);
                label.AddThemeColorOverride("font_color", new Color(0.86f, 0.89f, 0.99f));
                vbox.AddChild(label);
            }

            var backBtn = new Button();
            backBtn.Text = "Back";
            backBtn.AddThemeFontSizeOverride("font_size", 18);
            backBtn.Pressed += () => GameManager.Instance?.ShowMetaHub();
            vbox.AddChild(backBtn);
        }
    }
}
