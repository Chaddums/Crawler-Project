using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// A bot playing the campaign, one run a launch ("campaign": true, or CAMPAIGN=1): before the
    /// run it spends the save's perk points as its policy says and picks what a player following
    /// the Command Center would pick, the next site not yet secured, or the boss once a planet's
    /// sites are all held (with a suit if it has one). It plays to the site's goal and
    /// "campaignExtraWaves" beyond (-1: until the Spire falls), leaves through the pause menu's
    /// End Run, saves a suit when a slot is free, and writes one line about the run.
    ///
    /// The save carries from launch to launch through a folder: CAMPAIGN_STATE gets the run's
    /// line ("campaign_log.jsonl") and its saves (in "save/", which SANDBOX_SEED loads next time)
    /// (qa/run_campaign.py loops this until "campaign_complete" appears there). SANDBOX_NAME
    /// keeps two campaigns running at once in their own save folders.
    /// </summary>
    public static class CampaignPilot
    {
        public record Plan(TerritorySite Site, int Planet, bool Boss, int SuitIndex, string SuitName, int GoalWave, int CapWave,
            List<string> Bought, int PointsLeft, int PointsSpent, float Threat, int RunNumber);

        public static Plan Current { get; private set; }

        private static string StateDir => OS.GetEnvironment("CAMPAIGN_STATE");

        /// <summary>
        /// Spends points, picks the site and says what the run is for; null when nothing is left
        /// to do (every site secured and every boss beaten).
        /// </summary>
        public static Plan Begin(GameManager gm, AutoPlayerConfig cfg)
        {
            gm.MetaSave ??= MetaPerkSave.Load();
            var save = gm.MetaSave;
            int run = save.RunCount + 1;
            var rng = new RandomNumberGenerator { Seed = (ulong)(run * 7919 + Math.Abs((cfg.PerkPolicy ?? "").GetHashCode() % 1000)) };

            var bought = SpendPoints(save, cfg.PerkPolicy, rng);
            if (bought.Count > 0) MetaPerkSave.Save(save);

            TerritorySite site = null;
            if (!string.IsNullOrEmpty(cfg.Site)) site = TerritoryManager.GetSite(cfg.Site);
            else
            {
                site = TerritoryManager.NextSite(save);
                if (site == null)
                {
                    var boss = TerritoryManager.NextBossSite(save);
                    if (boss != null && !TerritoryManager.IsSiteCleared(boss.Id, save)) site = boss;
                }
            }
            if (site == null)
            {
                Current = null;
                GD.Print($"[Campaign] complete after {save.RunCount} runs: every site secured, every boss beaten");
                WriteState(complete: true);
                return null;
            }

            int suit = -1;
            string suitName = "";
            if (site.IsBossSite)
            {
                var suits = SuitManager.GetAll();
                // The most valuable kit it has
                int best = 0;
                for (int i = 0; i < suits.Length; i++)
                    if (suits[i] != null && suits[i].Usable && SuitManager.KitValue(suits[i]) > best)
                    { best = SuitManager.KitValue(suits[i]); suit = i; suitName = suits[i].Name; }
            }
            int goal = site.IsBossSite ? site.BossWave : site.ClearWave;
            int cap = site.IsBossSite || cfg.CampaignExtraWaves < 0 ? int.MaxValue : goal + cfg.CampaignExtraWaves;
            int spent = MetaPerkRegistry.Spent(save);
            Current = new Plan(site, Math.Max(1, TerritoryManager.PlanetOfSite(site.Id)), site.IsBossSite, suit, suitName, goal, cap,
                bought, save.AvailablePoints, spent, Climb.ThreatFor(spent), run);
            var region = TerritoryManager.GetRegionForSite(site.Id);
            GD.Print($"[Campaign] run {run}: {(site.IsBossSite ? "BOSS " : "")}{site.Name} ({site.Id}, {region?.Name}, planet {Current.Planet}, map {site.MapLayout}), "
                + $"goal wave {goal}{(cap < int.MaxValue ? $", leaving after wave {cap}" : "")}"
                + (site.IsBossSite ? (suit >= 0 ? $", suit '{suitName}'" : ", no suit") : "")
                + $"; perk points spent {spent} (enemies x{Current.Threat:0.00}), {save.AvailablePoints} unspent"
                + (bought.Count > 0 ? $"; bought {string.Join(", ", bought)}" : ""));
            return Current;
        }

        /// <summary>
        /// Buys perks until the points run out. "random": any perk that can be bought (a player
        /// clicking what looks good); a lane's name ("network", "player", "harvester"): that
        /// lane first; "balanced": the lane with the least in it; "none": nothing.
        /// </summary>
        public static List<string> SpendPoints(MetaPerkSaveData save, string policy, RandomNumberGenerator rng)
        {
            var bought = new List<string>();
            policy = (policy ?? "random").ToLowerInvariant();
            if (policy == "none") return bought;
            for (int guard = 0; guard < 500 && save.AvailablePoints > 0; guard++)
            {
                var can = MetaPerkRegistry.GetAll().Where(n => MetaPerkRegistry.WhyNot(n, save) == null).ToList();
                if (can.Count == 0) break;
                List<MetaPerkNode> pool = can;
                if (policy == "balanced")
                {
                    var lane = can.Select(n => n.Lane).Distinct().OrderBy(l => MetaPerkRegistry.LaneSpent(save, l)).First();
                    pool = can.Where(n => n.Lane == lane).ToList();
                }
                else if (policy != "random")
                {
                    var inLane = can.Where(n => n.Lane.ToString().Equals(policy, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (inLane.Count > 0) pool = inLane;
                }
                var pick = pool[rng.RandiRange(0, pool.Count - 1)];
                if (!MetaPerkRegistry.TryBuy(pick.Id, save)) break;
                bought.Add(pick.Name);
            }
            return bought;
        }

        /// <summary>
        /// The run is over and banked (the debrief has loaded): keeps a suit if there's a slot,
        /// writes the run's line and hands the saves back.
        /// </summary>
        public static void End(GameManager gm, AutoPlayerConfig cfg, string result, int waveReached, float gameSeconds, int towers, int errors)
        {
            var plan = Current;
            if (plan == null) return;
            var save = gm.MetaSave ?? MetaPerkSave.Load();
            string suitSaved = "";
            if (cfg.SaveSuit && !plan.Boss && gm.PendingSuitSnapshot != null && SuitManager.FreeSlots > 0
                && SuitManager.KitValue(gm.PendingSuitSnapshot) > 0)
            {
                string name = $"{plan.Site.Name} W{waveReached}";
                if (SuitManager.SaveSnapshot(gm.PendingSuitSnapshot, name) >= 0) suitSaved = SuitManager.KitSummary(gm.PendingSuitSnapshot);
            }
            bool held = TerritoryManager.IsSiteCleared(plan.Site.Id, save);
            bool done = plan.Boss ? gm.LastRunVictory : gm.SiteSecuredThisRun || held;
            int sites = 0, cleared = 0;
            foreach (int pid in new[] { 1, 2, 3 })
            {
                var p = TerritoryManager.GetPlanet(pid);
                if (p == null) continue;
                foreach (var r in p.Regions) foreach (var s in r.Sites) { sites++; if (TerritoryManager.IsSiteCleared(s.Id, save)) cleared++; }
            }
            var line = new Godot.Collections.Dictionary
            {
                ["run"] = plan.RunNumber, ["site"] = plan.Site.Id, ["name"] = plan.Site.Name, ["planet"] = plan.Planet,
                ["region"] = TerritoryManager.GetRegionForSite(plan.Site.Id)?.Name ?? "", ["map"] = plan.Site.MapLayout ?? "",
                ["boss"] = plan.Boss, ["goal"] = plan.GoalWave, ["reached"] = waveReached, ["done"] = done, ["result"] = result,
                ["suit"] = plan.SuitName, ["suitSaved"] = suitSaved, ["extracted"] = gm.TotalExtracted,
                ["banked"] = save.MetaResources, ["pointsEarned"] = gm.MetaPointsEarnedThisRun, ["pointsUnspent"] = save.AvailablePoints,
                ["pointsSpent"] = plan.PointsSpent, ["threat"] = Math.Round(plan.Threat, 3), ["bought"] = string.Join(", ", plan.Bought),
                ["gameMinutes"] = Math.Round(gameSeconds / 60.0, 1), ["towers"] = towers, ["errors"] = errors,
                ["sitesHeld"] = cleared, ["sitesTotal"] = sites, ["role"] = gm.SelectedRole ?? "", ["strategy"] = cfg.Strategy,
            };
            GD.Print($"[Campaign] run {plan.RunNumber} result: {plan.Site.Name} {(plan.Boss ? "boss" : "site")} goal W{plan.GoalWave}, reached W{waveReached}, "
                + $"{(done ? (plan.Boss ? "BOSS BEATEN" : "SECURED") : "not done")} ({result}); extracted {gm.TotalExtracted}, +{gm.MetaPointsEarnedThisRun} perk point(s), "
                + $"{gameSeconds / 60f:0.0} game minutes; {cleared} of {sites} sites held" + (suitSaved.Length > 0 ? $"; suit saved: {suitSaved}" : ""));
            WriteState(complete: false, line: Json.Stringify(line));
        }

        /// <summary>Copies the run's saves to CAMPAIGN_STATE and appends the run's line there.</summary>
        private static void WriteState(bool complete, string line = null)
        {
            string dir = StateDir;
            if (string.IsNullOrEmpty(dir)) return;
            try
            {
                // The saves go in their own folder (the one SANDBOX_SEED reads): beside the run logs
                // they were seeded into the sandbox and copied back over the log being written
                string saves = Path.Combine(dir, "save");
                Directory.CreateDirectory(saves);
                string from = SafeFile.DiskPath("user://");
                if (SafeFile.IsSandboxed && Directory.Exists(from))
                    foreach (var f in Directory.GetFiles(from))
                        File.Copy(f, Path.Combine(saves, Path.GetFileName(f)), overwrite: true);
                if (line != null) File.AppendAllText(Path.Combine(dir, "campaign_log.jsonl"), line + "\n");
                if (complete) File.WriteAllText(Path.Combine(dir, "campaign_complete"), "every site secured, every boss beaten\n");
            }
            catch (Exception e) { GD.PushWarning($"[Campaign] couldn't write to {dir}: {e.Message}"); }
        }
    }
}
