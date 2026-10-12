using System.Linq;
using Godot;
using Godot.Collections;

namespace JunkyardTD
{
    /// <summary>
    /// Suits: each saved suit's kit of towers (names, levels, branches, value), where it came
    /// from, and Take into a boss fight, Rename and Salvage. CEF renders ui/suit-armory/index.html;
    /// the built-in screen does the same when it can't.
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

        public override void _ExitTree() => CleanupCef();

        private void CleanupCef()
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

        private int _blankPages;
        private Control _backdrop;

        private void OnPageBlank()
        {
            _blankPages++;
            CleanupCef();
            if (_blankPages == 1) { CreateCefBrowser(); return; }
            if (_backdrop != null && IsInstanceValid(_backdrop)) _backdrop.QueueFree();
            BuildFallbackUI();
        }

        private void CreateCefBrowser()
        {
            if (_backdrop == null || !IsInstanceValid(_backdrop))
                _backdrop = CefHelper.AddBackdrop(this, "SUITS\nEsc: back");
            _cefTexture = ClassDB.Instantiate("CefTexture").AsGodotObject();
            if (_cefTexture is not Control cefControl)
            {
                BuildFallbackUI();
                return;
            }

            cefControl.SetAnchorsPreset(LayoutPreset.FullRect);
            cefControl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cefControl.SizeFlagsVertical = SizeFlags.ExpandFill;

            _cefTexture.Set("background_color", new Color(0.043f, 0.075f, 0.149f, 1f));
            CefHelper.Configure(_cefTexture); // CPU rendering unless the project setting asks for GPU sharing

            _cefTexture.Connect("load_finished", Callable.From<string, int>(OnPageLoaded));
            _cefTexture.Connect("load_error", Callable.From<string, int, string>(OnPageError));
            _cefTexture.Connect("ipc_data_message", Callable.From<Variant>(OnIpcData));
            _cefTexture.Connect("console_message", Callable.From<int, string, string, int>(OnConsoleMessage));

            AddChild(cefControl);
            // A page whose render process dies draws nothing (grey, no way out): load it again
            CefHelper.WatchCrash(_cefTexture, "Suits", () => _cefTexture?.Set("url", "res://ui/suit-armory/index.html"));
            // A web view that never paints showed Godot's grey clear colour with no way out:
            // make a new one, and if that doesn't draw either, use the built-in screen
            CefHelper.WatchPaint(this, "Suits", OnPageBlank);
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
                case "back": GameManager.Instance?.ShowMetaHub(); break;
                case "suit-selected": _selectedIndex = index; break;
                case "deploy": Deploy(index); break;
                case "salvage":
                    if (SuitManager.Salvage(index)) { _selectedIndex = -1; PushSuitData(); }
                    break;
                case "rename":
                    string name = payload != null && payload.ContainsKey("name") ? payload["name"].AsString() : "";
                    if (SuitManager.Rename(index, name)) PushSuitData();
                    break;
            }
        }

        // ── Actions (the web page and the built-in screen share them) ──

        /// <summary>The boss fight a suit would go into now, or null.</summary>
        internal static TerritorySite BossFor(SuitSaveData suit)
        {
            var gm = GameManager.Instance;
            var save = gm?.MetaSave ?? MetaPerkSave.Load();
            return TerritoryManager.NextBossSite(save, suit?.Planet ?? 0);
        }

        internal const string NoBossNote = "No boss fight is open yet: secure the sites of a region to open its boss.";

        /// <summary>Take a suit to the boss fight screen for the next open boss.</summary>
        internal static bool Deploy(int index)
        {
            var suits = SuitManager.GetAll();
            if (index < 0 || index >= suits.Length || suits[index] == null || !suits[index].Usable) return false;
            var boss = BossFor(suits[index]);
            if (boss == null) return false;
            GD.Print($"[SuitArmory] Deploy suit {index} to {boss.Name}");
            GameManager.Instance?.ShowBossConfirmation(TerritoryManager.PlanetOfSite(boss.Id), index, boss.Id);
            return true;
        }

        private static string Hex(Color c) => "#" + c.ToHtml(false);

        /// <summary>Everything the page shows, as JSON-ready data (tests read it too).</summary>
        internal static Godot.Collections.Array SuitsData()
        {
            var arr = new Godot.Collections.Array();
            var suits = SuitManager.GetAll();
            for (int i = 0; i < Constants.MAX_SUIT_SLOTS; i++)
            {
                var suit = i < suits.Length ? suits[i] : null;
                if (suit == null || suit.Nodes.Count == 0)
                {
                    arr.Add(new Godot.Collections.Dictionary { ["empty"] = true });
                    continue;
                }
                var towers = new Godot.Collections.Array();
                foreach (var line in SuitManager.Describe(suit))
                    towers.Add(new Godot.Collections.Dictionary
                    {
                        ["name"] = line.Name, ["level"] = line.Level, ["maxLevel"] = line.MaxLevel,
                        ["branch"] = line.BranchName ?? "", ["color"] = Hex(line.Color.Lightened(0.25f)),
                    });
                var role = SpireData.Get(suit.Role ?? "");
                arr.Add(new Godot.Collections.Dictionary
                {
                    ["empty"] = false,
                    ["name"] = suit.Name ?? $"Suit {i + 1}",
                    ["role"] = suit.Role ?? "",
                    ["roleColor"] = Hex(role?.Color ?? new Color(0.55f, 0.57f, 0.6f)),
                    ["origin"] = SuitManager.Origin(suit),
                    ["consumed"] = suit.Consumed,
                    ["created"] = suit.CreatedTimestamp,
                    ["count"] = suit.Nodes.Count,
                    ["value"] = SuitManager.KitValue(suit),
                    ["towers"] = towers,
                });
            }
            return arr;
        }

        internal static Godot.Collections.Dictionary BossData()
        {
            var boss = BossFor(null);
            return boss == null
                ? new Godot.Collections.Dictionary { ["open"] = false, ["note"] = NoBossNote }
                : new Godot.Collections.Dictionary { ["open"] = true, ["name"] = boss.Name, ["wave"] = boss.BossWave };
        }

        private void PushSuitData()
        {
            string json = Json.Stringify(SuitsData());
            string boss = Json.Stringify(BossData());
            _cefTexture?.Call("eval", $"window.__suitArmoryUI.setSuits({json}, {Constants.MAX_SUIT_SLOTS}, {boss});");
        }

        // ── Fallback (built in, when the web page can't show) ──

        private VBoxContainer _fallbackRoot;
        /// <summary>The built-in screen's slot cards (tests).</summary>
        internal readonly System.Collections.Generic.List<VBoxContainer> SlotCards = new();

        private static Label FL(string text, int size, Color color)
        {
            var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            return l;
        }

        private void BuildFallbackUI()
        {
            var bg = new ColorRect { Color = new Color(0.043f, 0.075f, 0.149f) };
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(bg);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", 60);
            margin.AddThemeConstantOverride("margin_top", 40);
            margin.AddThemeConstantOverride("margin_bottom", 40);
            AddChild(margin);

            _fallbackRoot = new VBoxContainer();
            _fallbackRoot.AddThemeConstantOverride("separation", 14);
            margin.AddChild(_fallbackRoot);
            RefreshFallback();
        }

        internal void RefreshFallback()
        {
            if (_fallbackRoot == null) return;
            foreach (var c in _fallbackRoot.GetChildren()) c.QueueFree();
            SlotCards.Clear();
            var cyan = new Color(0.0f, 0.85f, 0.95f);
            var dim = new Color(0.62f, 0.66f, 0.74f);
            var text = new Color(0.88f, 0.9f, 0.96f);

            _fallbackRoot.AddChild(FL("SUITS", 34, cyan));
            _fallbackRoot.AddChild(FL("A suit is a kit of your best towers from a run, taken into a boss fight. There its towers are on the build bar marked KIT, free to place anywhere, already at their levels. Lose the fight and the suit is lost; win and you keep it. Save one at the end of a run.", 16, dim));

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 18);
            row.SizeFlagsVertical = SizeFlags.ExpandFill;
            _fallbackRoot.AddChild(row);
            var suits = SuitManager.GetAll();
            var bossNow = BossFor(null);
            for (int i = 0; i < Constants.MAX_SUIT_SLOTS; i++)
            {
                int idx = i;
                var suit = suits[i];
                var panel = new PanelContainer { CustomMinimumSize = new Vector2(380, 420), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                var st = new StyleBoxFlat { BgColor = new Color(0.03f, 0.05f, 0.09f, 0.95f), BorderColor = new Color(0.2f, 0.4f, 0.55f),
                    ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 14, ContentMarginBottom = 14 };
                st.SetBorderWidthAll(2);
                st.SetCornerRadiusAll(6);
                panel.AddThemeStyleboxOverride("panel", st);
                row.AddChild(panel);
                var v = new VBoxContainer { Name = $"Slot{i}" };
                v.AddThemeConstantOverride("separation", 6);
                panel.AddChild(v);
                SlotCards.Add(v);

                if (suit == null || suit.Nodes.Count == 0)
                {
                    v.AddChild(FL($"SLOT {i + 1}: EMPTY", 18, dim));
                    v.AddChild(FL("Finish a run and save its best towers at the debrief.", 14, dim));
                    continue;
                }
                var roleColor = SpireData.Get(suit.Role ?? "")?.Color ?? dim;
                v.AddChild(FL(suit.Name, 22, text));
                v.AddChild(FL($"{suit.Role}   {SuitManager.Origin(suit)}", 14, roleColor.Lightened(0.2f)));
                if (suit.Consumed)
                {
                    v.AddChild(FL("LOST in a boss fight. Save a new suit here at the end of a run.", 15, new Color(0.95f, 0.4f, 0.35f)));
                    continue;
                }
                v.AddChild(FL($"{suit.Nodes.Count} TOWERS, worth {SuitManager.KitValue(suit)} Resources", 13, dim));
                int k = 0;
                foreach (var line in SuitManager.Describe(suit))
                {
                    var t = FL($"{line.Name}   L{line.Level}/{line.MaxLevel}{(line.BranchName != null ? "   " + line.BranchName : "")}", 16, line.Color.Lightened(0.35f));
                    t.Name = $"Tower{k++}";
                    v.AddChild(t);
                }
                v.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

                var deploy = new Button { Name = "Deploy", Text = bossNow != null ? $"TAKE INTO {bossNow.Name.ToUpper()}" : "NO BOSS FIGHT OPEN", Disabled = bossNow == null };
                if (bossNow == null) deploy.TooltipText = NoBossNote;
                deploy.Pressed += () => Deploy(idx);
                v.AddChild(deploy);

                var renameRow = new HBoxContainer();
                var nameEdit = new LineEdit { Name = "NameEdit", Text = suit.Name, MaxLength = 28, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                var rename = new Button { Name = "Rename", Text = "Rename" };
                rename.Pressed += () => { if (SuitManager.Rename(idx, nameEdit.Text)) RefreshFallback(); };
                nameEdit.TextSubmitted += _ => rename.EmitSignal(BaseButton.SignalName.Pressed);
                renameRow.AddChild(nameEdit);
                renameRow.AddChild(rename);
                v.AddChild(renameRow);

                var salvage = new Button { Name = "Salvage", Text = "Salvage" };
                bool armed = false;
                salvage.Pressed += () =>
                {
                    if (!armed) { armed = true; salvage.Text = "Click again to salvage"; return; }
                    if (SuitManager.Salvage(idx)) RefreshFallback();
                };
                v.AddChild(salvage);
            }
            if (bossNow == null) _fallbackRoot.AddChild(FL(NoBossNote, 14, dim));

            var backBtn = new Button { Text = "Back to the Command Center  [Esc]" };
            backBtn.AddThemeFontSizeOverride("font_size", 18);
            backBtn.Pressed += () => GameManager.Instance?.ShowMetaHub();
            _fallbackRoot.AddChild(backBtn);
        }
    }
}
