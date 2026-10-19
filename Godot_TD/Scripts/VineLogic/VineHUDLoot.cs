using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The HUD's loot card (right-hand column): the crates BIT carries by tier, how many still
    /// wait in the field, and OPEN [O], which opens them between waves (<see cref="LootManager"/>).
    /// Hidden while there are no crates anywhere.
    /// </summary>
    public partial class VineHUD
    {
        private PanelContainer _lootCard;
        private VBoxContainer _lootRows;
        private Label _lootField, _lootHead;
        private Button _lootOpen;
        private string _lootShown;
        private LootManager _lootHooked;

        private void BuildLootCard()
        {
            EnsureRightColumn();
            var col = new VBoxContainer { Name = "Loot", MouseFilter = Control.MouseFilterEnum.Ignore };
            col.AddThemeConstantOverride("separation", 2);
            _lootHead = MakeLabel("LOOT CRATES", 14);
            _lootHead.HorizontalAlignment = HorizontalAlignment.Right;
            _lootHead.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
            col.AddChild(_lootHead);
            _lootRows = new VBoxContainer { Name = "Rows", MouseFilter = Control.MouseFilterEnum.Ignore };
            _lootRows.AddThemeConstantOverride("separation", 0);
            col.AddChild(_lootRows);
            _lootField = MakeLabel("", 14);
            _lootField.HorizontalAlignment = HorizontalAlignment.Right;
            _lootField.AddThemeColorOverride("font_color", new Color(0.7f, 0.76f, 0.86f));
            col.AddChild(_lootField);
            _lootOpen = new Button { Name = "OpenCrates", Text = "OPEN  [O]", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(200, 34) };
            _lootOpen.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
            _lootOpen.AddThemeFontSizeOverride("font_size", 16);
            var st = new StyleBoxFlat { BgColor = new Color(0.32f, 0.22f, 0.04f, 0.95f), BorderColor = new Color(1f, 0.78f, 0.3f) };
            st.SetBorderWidthAll(1);
            st.SetCornerRadiusAll(3);
            _lootOpen.AddThemeStyleboxOverride("normal", st);
            var hov = (StyleBoxFlat)st.Duplicate();
            hov.BgColor = new Color(0.48f, 0.33f, 0.06f, 0.98f);
            _lootOpen.AddThemeStyleboxOverride("hover", hov);
            _lootOpen.AddThemeStyleboxOverride("pressed", hov);
            var off = (StyleBoxFlat)st.Duplicate();
            off.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            off.BorderColor = new Color(0.35f, 0.35f, 0.4f);
            _lootOpen.AddThemeStyleboxOverride("disabled", off);
            _lootOpen.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.7f));
            _lootOpen.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.57f, 0.62f));
            _lootOpen.Pressed += () =>
            {
                if (ServiceLocator.TryGet<LootManager>(out var loot)) loot.OpenAll();
                RefreshLoot();
            };
            col.AddChild(_lootOpen);
            _lootCard = Card(col);
            _lootCard.Name = "LootCard";
            _lootCard.Visible = false;
        }

        /// <summary>The loot card, kept up to date every half second and whenever BIT takes or opens a crate.</summary>
        private void RefreshLoot()
        {
            if (_lootCard == null) return;
            if (!ServiceLocator.TryGet<LootManager>(out var loot) || !IsInstanceValid(loot)) { _lootCard.Visible = false; return; }
            if (_lootHooked != loot)
            {
                _lootHooked = loot;
                loot.Changed += () => { if (IsInstanceValid(this)) RefreshLoot(); };
            }
            int field = LootManager.InField(GetTree());
            var carried = loot.Carried;
            string why = loot.CantOpen();
            string key = string.Join(",", carried.Select(t => t.Id)) + "|" + field + "|" + why;
            if (key == _lootShown) return;
            _lootShown = key;

            _lootCard.Visible = carried.Count > 0 || field > 0;
            foreach (var c in _lootRows.GetChildren()) { _lootRows.RemoveChild(c); c.QueueFree(); }
            foreach (var g in carried.GroupBy(t => t.Id).OrderByDescending(g => g.First().Rank))
            {
                var t = g.First();
                var row = MakeLabel($"{g.Count()}x {t.Name}  ■", 16);
                row.HorizontalAlignment = HorizontalAlignment.Right;
                row.AddThemeColorOverride("font_color", t.Tint.Lightened(0.15f));
                _lootRows.AddChild(row);
            }
            _lootHead.Text = carried.Count > 0 ? $"LOOT CRATES  {carried.Count}" : "LOOT CRATES";
            _lootField.Text = field > 0 ? $"{field} in the field: walk BIT over {(field == 1 ? "it" : "them")}" : "";
            _lootField.Visible = field > 0;
            _lootOpen.Visible = carried.Count > 0;
            _lootOpen.Disabled = why != null;
            bool safe = ServiceLocator.TryGet<VineWaveManager>(out var wm) && wm.InSafeRoom;
            _lootOpen.Text = why == null ? (safe ? "OPEN EVERYTHING  [O]" : "OPEN  [O]") : why == "Opening" ? "OPENING..."
                : why.StartsWith("Opens in the safe room") ? $"SAFE ROOM AFTER W{WavePacing.NextSafeRoom(Mathf.Max(1, GameManager.Instance?.CurrentWave ?? 1))}" : "OPEN BETWEEN WAVES";
            _lootOpen.TooltipText = why ?? "";
        }

        /// <summary>Tests: the loot card and its button.</summary>
        internal PanelContainer LootCard => _lootCard;
        internal Button LootOpenButton => _lootOpen;
        internal string LootFieldText => _lootField?.Text ?? "";
    }
}
