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
                    "Armour: each hit is small",
                    $"{Constants.FLAK_BATTERY_DAMAGE:0} damage a second to each of {Constants.FLAK_BATTERY_MAX_TARGETS} targets  ·  range {cells(Constants.FLAK_BATTERY_RANGE):0} cells"),
                VineNodeType.BarrierWall => new(
                    "Cheap wall: shape the path",
                    "Making enemies walk past your towers for longer",
                    "Brutes smash walls; Ghosts walk through; flyers fly over",
                    $"{Constants.BARRIER_WALL_HP:0} HP  ·  blocks one cell"),
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
                _ => new("", "", "", ""),
            };
        }
    }
}
