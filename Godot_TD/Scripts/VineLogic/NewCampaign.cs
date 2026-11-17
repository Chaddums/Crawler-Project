using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The campaign's end and what follows. Winning the last boss fight ends the game: the ending
    /// plays (<see cref="EndingScreen"/>) and from it the next campaign begins: every site and
    /// region is given back, and the perk tree, the bank, the store's items, the relic catalogue
    /// and the suits stay. Each campaign finished makes every enemy tougher
    /// (<see cref="Climb.CampaignHp"/>). Until 2026-11 nothing followed the last boss: the map
    /// said "complete" and every site could be played again.
    /// </summary>
    public static class NewCampaign
    {
        /// <summary>The campaign being played: 1 on a new save.</summary>
        public static int Number(MetaPerkSaveData save) => (save?.CampaignsCompleted ?? 0) + 1;

        public static string Name(MetaPerkSaveData save) => $"Campaign {Roman(Number(save))}";

        public static string Roman(int n)
        {
            if (n <= 0 || n >= 4000) return n.ToString();
            (int v, string s)[] map = { (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") };
            var sb = new System.Text.StringBuilder();
            foreach (var (v, s) in map)
                while (n >= v) { sb.Append(s); n -= v; }
            return sb.ToString();
        }

        /// <summary>Every site is held and every boss is down: the ending is due.</summary>
        public static bool EndingDue(MetaPerkSaveData save) => TerritoryManager.CampaignComplete(save);

        /// <summary>How much tougher the next campaign's enemies are than this one's, in percent.</summary>
        public static int NextStepPercent(MetaPerkSaveData save)
        {
            int done = save?.CampaignsCompleted ?? 0;
            float now = Climb.CampaignHpFor(done), next = Climb.CampaignHpFor(done + 1);
            return Mathf.RoundToInt((next / Mathf.Max(0.01f, now) - 1f) * 100f);
        }

        /// <summary>
        /// Begin the next campaign: the sites and regions are given back (and their buffs with
        /// them), everything else stays. Saved at once. False when the campaign isn't finished.
        /// </summary>
        public static bool Begin(MetaPerkSaveData save)
        {
            if (save == null || !EndingDue(save)) return false;
            save.CampaignsCompleted++;
            save.CampaignStartRun = save.RunCount;
            save.ClearedSites.Clear();
            save.ConqueredRegions.Clear();
            MetaPerkSave.Save(save);
            GD.Print($"[Campaign] {Name(save)} begins: enemies x{Climb.CampaignHpFor(save.CampaignsCompleted):0.00} health; "
                + $"{MetaPerkRegistry.SpentFixed(save)} perk points, {save.MetaResources} banked and {save.OwnedCosmetics.Count} store items kept");
            return true;
        }

        /// <summary>The finished campaign in numbers, for the ending.</summary>
        public static List<(string label, string value)> Stats(MetaPerkSaveData save)
        {
            var list = new List<(string, string)>();
            if (save == null) return list;
            var (held, total) = TerritoryManager.CampaignProgress(save);
            int bosses = 0;
            foreach (var id in save.ClearedSites)
                if (TerritoryManager.GetSite(id)?.IsBossSite == true) bosses++;
            list.Add(("Sites held", $"{held} of {total}"));
            list.Add(("Bosses destroyed", bosses.ToString()));
            list.Add(("Deployments", Mathf.Max(1, save.RunCount - save.CampaignStartRun).ToString()));
            list.Add(("Perk points owned", (MetaPerkRegistry.SpentFixed(save) + save.AvailablePoints).ToString()));
            list.Add(("Resources banked", save.MetaResources.ToString("N0")));
            return list;
        }
    }
}
