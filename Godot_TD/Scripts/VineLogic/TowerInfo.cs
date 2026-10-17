namespace JunkyardTD
{
    /// <summary>
    /// What a player needs to choose a tower: its job, what it beats, what beats it, and its
    /// numbers. Shown on the build bar's hover card. Kept in step with the towers' code (VineNode).
    /// </summary>
    public static class TowerInfo
    {
        public sealed record Info(string Role, string Best, string Weak, string Stats);

        public static Info Get(VineNodeType type)
        {
            float cells(float r) => r / Constants.VINE_CELL_SIZE;
            return type switch
            {
                VineNodeType.DamageTower => new(
                    "Single target, heavy rounds",
                    "Armoured enemies, Brutes, bosses",
                    "Flyers (it can't reach them), Swarms",
                    $"{Constants.DAMAGE_TOWER_DPS:0} damage a second  ·  range {cells(Constants.DAMAGE_TOWER_RANGE):0} cells"),
                VineNodeType.SlowField => new(
                    "Slows enemies on the path",
                    "Fast enemies, buying time for other towers",
                    "Flyers (out of reach); Brutes shrug off half; deals no damage",
                    $"{Constants.SLOW_FIELD_AMOUNT * 100:0}% slow  ·  range {cells(Constants.SLOW_FIELD_RANGE):0} cells"),
                VineNodeType.ScatterCannon => new(
                    "Explosive splash, heavy hits",
                    "Swarms, packs and armour",
                    "Flyers (a ground blast), lone fast targets",
                    $"{Constants.SCATTER_CANNON_DAMAGE:0} to everything within {cells(Constants.SCATTER_CANNON_RADIUS):0.#} cells, every {Constants.SCATTER_CANNON_INTERVAL:0.#} s  ·  range {cells(Constants.SCATTER_CANNON_RANGE):0} cells"),
                VineNodeType.TeslaCoil => new(
                    "Chain lightning, reaches flyers",
                    "Shields (three times the damage), flyers, lines of enemies",
                    "Armour (each arc lands light)",
                    $"{Constants.TESLA_COIL_DAMAGE:0} then arcs to {Constants.TESLA_COIL_CHAIN_COUNT} more, every {Constants.TESLA_COIL_INTERVAL:0.#} s  ·  range {cells(Constants.TESLA_COIL_RANGE):0} cells"),
                VineNodeType.FlakBattery => new(
                    "Anti-air: fast fire at many targets",
                    "Flyers (shoots them first), Swarms",
                    "Armour and shields: each hit is small, and shields soak half",
                    $"{Constants.FLAK_BATTERY_DAMAGE:0} damage a second to each of {Constants.FLAK_BATTERY_MAX_TARGETS} targets  ·  range {cells(Constants.FLAK_BATTERY_RANGE):0} cells"),
                VineNodeType.BarrierWall => new(
                    "Barricade: goes on the canyon floor",
                    "Holding a pack under your guns: walkers stop to break it",
                    "Brutes break it fastest; Ghosts walk through; flyers fly over. It never turns them",
                    $"{Constants.BARRIER_WALL_HP:0} HP  ·  one floor cell"),
                VineNodeType.PushPull => new(
                    "Shoves enemies back down the path",
                    "Holding enemies inside your kill zone",
                    "Flyers (out of reach); bosses and Brutes barely move",
                    $"Shove every {Constants.PUSH_PULL_INTERVAL:0.#} s  ·  range {cells(Constants.SENSOR_RANGE):0.#} cells"),
                VineNodeType.BuffEmitter => new(
                    "Boosts the towers next to it",
                    "The middle of a tower cluster",
                    "Does nothing on its own; a second relay on the same tower adds nothing",
                    "+25% damage and fire rate for adjacent towers (the strongest boost wins, they don't stack)"),
                VineNodeType.SalvageDrill => new(
                    "Economy: pays every wave it survives",
                    "Building up Resources for later waves; on a gold resource node it mines that too",
                    $"Stands at least {Constants.SALVAGE_DRILL_MIN_SPIRE_CELLS} cells from the Spire, and raiders leave the path to wreck it: guard it. Each drill makes the next dearer",
                    $"+{Constants.SALVAGE_DRILL_PAY:0} + {Constants.SALVAGE_DRILL_PAY_PER_WAVE:0.#} per wave number, every wave it survives  ·  {Constants.SALVAGE_DRILL_HP:0} HP"),
                // ── Class towers ──
                VineNodeType.MortarPit => new(
                    "Long-range shells onto the canyon floor",
                    "Packs far down the canyon, armour",
                    $"Anything closer than {cells(ClassTowers.MORTAR_MIN_RANGE):0} cells; flyers; shells take a second to land",
                    $"{ClassTowers.MORTAR_DAMAGE * ClassTowers.MORTAR_INTERVAL:0} to everything within {cells(ClassTowers.MORTAR_RADIUS):0.#} cells every {ClassTowers.MORTAR_INTERVAL:0} s  ·  range {cells(ClassTowers.MORTAR_RANGE):0} cells"),
                VineNodeType.RailLance => new(
                    "A slug through every enemy on a line",
                    "Long straight canyons, lines of armour",
                    "Flyers; slow to fire, wasted on a lone target off the line",
                    $"{ClassTowers.RAIL_DAMAGE * ClassTowers.RAIL_INTERVAL:0} to each on the line every {ClassTowers.RAIL_INTERVAL:0.#} s  ·  range {cells(ClassTowers.RAIL_RANGE):0} cells"),
                VineNodeType.SlagChute => new(
                    "Burning slag on the floor below its wall",
                    "Packs walking through a narrow canyon",
                    "Has to stand on a canyon's lip; flyers; short reach",
                    $"A pool burning {ClassTowers.SLAG_DPS:0}/s for {ClassTowers.SLAG_SECONDS:0} s every {ClassTowers.SLAG_INTERVAL:0} s, {ClassTowers.SLAG_SLOW * 100:0}% slow  ·  range {cells(ClassTowers.SLAG_RANGE):0.#} cells"),
                VineNodeType.AutocannonNest => new(
                    "Fast heavy fire, two targets",
                    "Armour, Brutes, a steady stream",
                    "Flyers (unless Flak Rounds); short reach",
                    $"{ClassTowers.NEST_DAMAGE:0} damage a second over two targets  ·  range {cells(ClassTowers.NEST_RANGE):0} cells"),
                VineNodeType.ArcPrism => new(
                    "Echoes the Tesla Coils near it",
                    "A knot of Tesla Coils: every arc goes further",
                    $"Does nothing without a Tesla Coil within {cells(ClassTowers.PRISM_LISTEN):0.#} cells",
                    $"{ClassTowers.PRISM_SHARE * 100:0}% of each arc on to {ClassTowers.PRISM_ARCS} more  ·  range {cells(ClassTowers.PRISM_RANGE):0} cells"),
                VineNodeType.SignalJammer => new(
                    "A field that slows and strips shields",
                    "Shielded packs, fast enemies, flyers",
                    "Low damage on its own",
                    $"{ClassTowers.JAMMER_SLOW * 100:0}% slow, -{ClassTowers.JAMMER_DRAIN:0} shield and {ClassTowers.JAMMER_DPS:0} damage a second in the field  ·  range {cells(ClassTowers.JAMMER_RANGE):0.#} cells"),
                VineNodeType.StormCapacitor => new(
                    "Charges while it waits, then one big blast",
                    "Waves that come in bursts, shields, flyers",
                    "A steady stream (it never charges far); armour",
                    $"{ClassTowers.CAP_DAMAGE:0} for each second of charge (up to {ClassTowers.CAP_MAX:0})  ·  range {cells(ClassTowers.CAP_RANGE):0} cells"),
                VineNodeType.Lens => new(
                    "A beam that burns hotter the longer it holds",
                    "Bosses, Brutes, flyers; a Lens in the middle of a crew",
                    "Swarms (it starts cold on every new target)",
                    $"{ClassTowers.LENS_DPS:0} damage a second, up to x{ClassTowers.LENS_RAMP_MAX:0} held, +{ClassTowers.LENS_PER_LINK * 100:0}% a crew link  ·  range {cells(ClassTowers.LENS_RANGE):0} cells"),
                VineNodeType.GravityWell => new(
                    "Pulls enemies into a knot",
                    "Setting up splash: Scatter, Mortar, Shock Blast",
                    "Deals no damage; bosses barely move; flyers",
                    $"Pulls everything in reach together every {ClassTowers.WELL_INTERVAL:0.#} s  ·  range {cells(ClassTowers.WELL_RANGE):0} cells"),
                VineNodeType.Beacon => new(
                    "Marks the toughest enemies",
                    "Bosses and commanders, and BIT's fights",
                    "Deals no damage on its own",
                    $"Marks {ClassTowers.BEACON_MARKS} for {ClassTowers.BEACON_SECONDS:0} s: +{ClassTowers.BEACON_TAKEN * 100:0}% damage taken, +{ClassTowers.BEACON_BIT_TAKEN * 100:0}% from BIT  ·  range {cells(ClassTowers.BEACON_RANGE):0} cells"),
                _ => new("", "", "", ""),
            };
        }
    }
}
