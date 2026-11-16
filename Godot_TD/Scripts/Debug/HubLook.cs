using System.IO;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The screens between runs as a save sees them (HUB_LOOK=&lt;folder&gt;, with a display, in an
    /// --autoplay launch): opens the Command Center, the territory map and, when a boss fight is
    /// the campaign's next step, its confirmation, shoots each to the folder as
    /// "&lt;HUB_LOOK_NAME&gt;_1_command_center.png" and so on, and quits. No run is played.
    /// SANDBOX_SEED picks the save, so a campaign's stages (a new save, a boss open, the end) can
    /// be looked at one after another (qa\hub_look.bat does every folder in test-reports\hub_saves).
    ///
    /// CAMPAIGN_SHOTS=&lt;folder&gt; is the other half, for a campaign run that is played: the bot
    /// shoots the screen the run ends on (the win or the loss) and its debrief before it quits.
    /// </summary>
    public static class HubLook
    {
        public static string Folder => OS.GetEnvironment("HUB_LOOK");
        public static bool Wanted => !string.IsNullOrEmpty(Folder);

        public static async Task Run(Node host)
        {
            var gm = GameManager.Instance;
            var tree = host.GetTree();
            string dir = Folder;
            string name = OS.GetEnvironment("HUB_LOOK_NAME") is { Length: > 0 } n ? n : "hub";
            float wait = float.TryParse(OS.GetEnvironment("HUB_LOOK_WAIT"), out var w) ? w : 5f;
            Directory.CreateDirectory(dir);
            gm.MetaSave = MetaPerkSave.Load();
            // Direct scene changes, as the bot's runs use: nothing here is about the fade
            TransitionManager.Instance = null;
            var camp = TerritoryManager.Campaign(gm.MetaSave, gm.CurrentPlanet);
            var (held, total) = TerritoryManager.CampaignProgress(gm.MetaSave);
            GD.Print($"[HubLook] {name}: {held} of {total} held, runs {gm.MetaSave.RunCount}, points {gm.MetaSave.AvailablePoints} unspent; "
                + $"next: {(camp.Complete ? "nothing, the campaign is complete" : camp.Boss ? "boss " + camp.Site?.Name : camp.Site?.Name ?? "none")}. \"{camp.Line}\"");

            gm.ShowMetaHub();
            await Shoot(tree, dir, name + "_1_command_center", wait);
            gm.ShowTerritory();
            await Shoot(tree, dir, name + "_2_territory", wait);
            if (camp.Boss && camp.Site != null)
            {
                gm.ShowBossConfirmation(camp.Planet, -1, camp.Site.Id);
                await Shoot(tree, dir, name + "_3_boss", wait);
            }
            gm.ShowMetaPerkTree();
            await Shoot(tree, dir, name + "_4_perks", wait);
            GD.Print($"[HubLook] {name}: done");
            HeadlessShutdown.QuitClean(host, 0);
        }

        private static async Task Shoot(SceneTree tree, string dir, string label, float wait)
        {
            // Real seconds: the screens are web pages, loaded after the scene is
            await Wait(tree, wait);
            if (!CanShoot)
            {
                GD.Print($"[HubLook] {label}: no display, not shot");
                return;
            }
            await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Save(tree, dir, label);
        }

        public static bool CanShoot => RenderingServer.RenderLoopEnabled && DisplayServer.GetName() != "headless";

        /// <summary>The window as it was last drawn, to "&lt;dir&gt;/&lt;label&gt;.png".</summary>
        public static void Save(SceneTree tree, string dir, string label)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, label + ".png");
                tree.Root.GetTexture().GetImage().SavePng(file);
                GD.Print($"[HubLook] {label} -> {file}");
            }
            catch (System.Exception e) { GD.PushWarning($"[HubLook] {label} not saved: {e.Message}"); }
        }

        private static async Task Wait(SceneTree tree, float seconds)
            => await tree.ToSignal(tree.CreateTimer(seconds, true, false, true), SceneTreeTimer.SignalName.Timeout);
    }
}
