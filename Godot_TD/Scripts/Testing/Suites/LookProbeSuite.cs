using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Probe (not in "all"): starts a canyon run and logs every mesh within LOOK_RADIUS (default
    /// 9) of the Spire with what it's made of, so a thing seen on screen (a black ball, a black
    /// column) can be traced to its node and material without a display. STALL_SITE picks the
    /// site, LOOK_ROLE the class. Each line: path, mesh, size, material (metal, roughness,
    /// albedo, emission), and "DARK" when it will draw near black: a smooth metal has only the
    /// sky to reflect, and the canyon has none.
    /// </summary>
    public class LookProbeSuite : ITestSuite
    {
        public string SuiteName => "lookprobe";

        public async Task Run(TestContext ctx)
        {
            var gm = GameManager.Instance;
            gm.SuppressAutoDebrief = true;
            gm.CurrentTerritorySectionId = OS.GetEnvironment("STALL_SITE") is { Length: > 0 } s ? s : "p1_ng_relay";
            gm.CurrentRunMode = RunMode.Harvest;
            gm.SelectedRole = OS.GetEnvironment("LOOK_ROLE") is { Length: > 0 } r ? r : "Bruteforge";
            gm.AvailableNodes = VineDraftScreen.GetRoleNodes(0);
            gm.StartVineRun();
            await ctx.Wait(1.0f);
            ctx.StartTest();
            bool inBuild = await ctx.WaitForPhase(GamePhase.Build, 15f);
            ctx.Assert(inBuild, "look/battle_loads");
            if (!inBuild) return;
            await ctx.Wait(3.0f);
            var grid = ServiceLocator.Get<VineGrid>();
            var spire = grid.Harvester;
            float radius = float.TryParse(OS.GetEnvironment("LOOK_RADIUS"), out var lr) ? lr : 9f;
            var at = spire.GlobalPosition;
            var lines = new List<string>();
            int dark = 0;
            foreach (var mi in All<MeshInstance3D>(ctx.Tree.Root))
            {
                if (!mi.IsVisibleInTree() || mi.Mesh == null) continue;
                var p = mi.GlobalPosition;
                if (new Vector2(p.X - at.X, p.Z - at.Z).Length() > radius) continue;
                var size = mi.GetAabb().Size * mi.GlobalBasis.Scale;
                if (size.Length() > 30f) continue; // the ground and the sky
                var mats = new List<string>();
                bool isDark = false;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    var m = mi.GetActiveMaterial(i);
                    mats.Add(Describe(m, out bool d));
                    isDark |= d;
                }
                if (isDark) dark++;
                lines.Add($"{(isDark ? "DARK " : "     ")}{Short(mi.GetPath())}  {mi.Mesh.GetClass()} {size.X:0.0}x{size.Y:0.0}x{size.Z:0.0} at ({p.X - at.X:0.0}, {p.Y - at.Y:0.0}, {p.Z - at.Z:0.0})  {string.Join(" | ", mats.Distinct())}");
            }
            foreach (var l in lines.OrderBy(l => l)) GD.Print("[Look] " + l);
            GD.Print($"[Look] {lines.Count} meshes within {radius} of the Spire, {dark} that draw near black");
            ctx.Assert(true, "look/listed");
        }

        /// <summary>A material in a few words; dark = it will draw near black in the canyon.</summary>
        public static string Describe(Material m, out bool dark)
        {
            dark = false;
            if (m == null) return "no material";
            if (m is ShaderMaterial sm) return $"shader {sm.Shader?.ResourcePath?.GetFile()}";
            if (m is not BaseMaterial3D b) return m.GetClass();
            bool lit = b.ShadingMode != BaseMaterial3D.ShadingModeEnum.Unshaded;
            var (metal, rough) = ModelLook.Effective(b);
            dark = ModelLook.DrawsBlack(b);
            return $"{(lit ? "lit" : "unshaded")} metal {metal:0.00} rough {rough:0.00} albedo {b.AlbedoColor.Luminance:0.00}{(b.AlbedoTexture != null ? "*tex" : "")}{(b.EmissionEnabled ? $" emit {b.EmissionEnergyMultiplier:0.0}" : "")}";
        }

        private static string Short(NodePath path)
        {
            var parts = path.ToString().Split('/');
            return string.Join("/", parts.Skip(System.Math.Max(0, parts.Length - 5)));
        }

        private static IEnumerable<T> All<T>(Node n) where T : Node
        {
            if (n is T t) yield return t;
            foreach (var c in n.GetChildren())
                foreach (var x in All<T>(c)) yield return x;
        }
    }
}
