using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// The Salvage Drill: an economy tower. Each one standing when a wave clears pays
    /// SALVAGE_DRILL_PAY + SALVAGE_DRILL_PAY_PER_WAVE x wave (times its upgrades), once for every
    /// wave the clear covers (a stack sent early pays each). It has little health and raiders
    /// leave the path to wreck it, and each drill already standing makes the next one dearer, so
    /// building them is a bet on holding them.
    /// </summary>
    public static class SalvageDrill
    {
        /// <summary>Drills standing now.</summary>
        public static List<VineNode> All()
        {
            var list = new List<VineNode>();
            if (Engine.GetMainLoop() is not SceneTree tree) return list;
            foreach (var n in tree.GetNodesInGroup(Constants.GROUP_VINE_NODE))
                if (n is VineNode vn && !vn.IsDestroyed && vn.Data?.Type == VineNodeType.SalvageDrill && vn.IsInsideTree())
                    list.Add(vn);
            return list;
        }

        /// <summary>What the next drill costs: SALVAGE_DRILL_COST, and SALVAGE_DRILL_COST_STEP of it more for each one standing.</summary>
        public static int NextCost() => CostWith(All().Count);

        public static int CostWith(int standing)
            => Mathf.RoundToInt(Constants.SALVAGE_DRILL_COST * (1f + Constants.SALVAGE_DRILL_COST_STEP * Mathf.Max(0, standing)));

        /// <summary>What a drill at level 1 pays for a wave.</summary>
        public static float BaseAt(int wave) => Constants.SALVAGE_DRILL_PAY + Constants.SALVAGE_DRILL_PAY_PER_WAVE * Mathf.Max(1, wave);

        /// <summary>What this drill pays for clearing <paramref name="wave"/>.</summary>
        public static int PayoutOf(VineNode drill, int wave)
            => drill == null ? 0 : Mathf.RoundToInt(BaseAt(wave) * drill.UpgradeIncomeMult);

        /// <summary>What every drill standing would pay for clearing waves <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static int PayoutFor(int from, int to)
        {
            int total = 0;
            foreach (var d in All())
                for (int w = from; w <= to; w++) total += PayoutOf(d, w);
            return total;
        }

        /// <summary>
        /// A clear covering waves <paramref name="from"/> to <paramref name="to"/>: each drill shows
        /// what it made, Armoured Rigs mend. Returns the total (paid with the wave bonus).
        /// </summary>
        public static int PayOut(int from, int to)
        {
            int total = 0;
            foreach (var d in All())
            {
                int pay = 0;
                for (int w = from; w <= to; w++) pay += PayoutOf(d, w);
                if (pay <= 0) continue;
                total += pay;
                if (d.HasBranch("armoured_rig")) d.Repair(d.NodeMaxHealth);
                DamageNumbers.Tag(d.GlobalPosition + Vector3.Up * 2.4f, $"+{pay}", new Color(1f, 0.78f, 0.3f));
                VfxFactory.SpawnBuffMotes(d.GetTree(), d.GlobalPosition, new Color(1f, 0.7f, 0.25f));
            }
            return total;
        }
    }
}
