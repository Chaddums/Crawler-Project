using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Planet 2: Scrapyard — rusted metal, industrial grime, mechanical debris.
    /// The Junkbot Arena aesthetic. Warm browns, oranges, corroded steel.
    /// Opposite feel to Tron's clean digital lines.
    /// </summary>
    public class ScrapyardPlanetTheme : PlanetTheme
    {
        public override string PlanetName => "Scrapyard";
        public override string PlanetDescription => "A dying industrial world. Rusted hulks and corroded machinery litter the surface. Everything here was built to last — and failed.";

        // Base palette — warm industrial
        public override Color GroundColor => new(0.12f, 0.09f, 0.07f);          // Dark brown dirt
        public override Color GridLineColor => new(0.6f, 0.35f, 0.1f);          // Rusty orange
        public override Color WallColor => new(0.15f, 0.12f, 0.1f);             // Corroded metal
        public override Color BackgroundColor => new(0.06f, 0.04f, 0.03f);      // Murky brown-black
        public override Color AmbientColor => new(0.12f, 0.08f, 0.05f);         // Warm amber ambient
        public override Color FogColor => new(0.08f, 0.06f, 0.04f);             // Brown haze

        // Factions — warm tones instead of Tron's cool
        public override Color PlayerPrimary => new(0.9f, 0.6f, 0.15f);          // Bright amber/gold
        public override Color PlayerSensor => new(0.8f, 0.55f, 0.2f);           // Warm gold
        public override Color PlayerEffect => new(0.9f, 0.45f, 0.1f);           // Hot orange
        public override Color PlayerRoute => new(0.7f, 0.5f, 0.25f);            // Dull brass

        public override Color EnemyScavenger => new(0.4f, 0.6f, 0.3f);          // Sickly green (corrosion)
        public override Color EnemyBrute => new(0.3f, 0.3f, 0.35f);             // Gunmetal grey
        public override Color EnemySwarm => new(0.5f, 0.7f, 0.2f);              // Acid green
        public override Color EnemyGhost => new(0.35f, 0.5f, 0.45f);            // Patina teal

        // VFX
        public override Color ProjectileColor => new(0.95f, 0.6f, 0.1f);        // Molten orange
        public override Color SignalPulseColor => new(0.9f, 0.5f, 0.15f);       // Amber spark
        public override Color DeathBurstColor => new(0.6f, 0.4f, 0.2f);         // Rust chunks
        public override Color ImpactFlashColor => new(1f, 0.8f, 0.4f);          // Warm flash

        // UI
        public override Color EntryMarkerColor => new(0.9f, 0.7f, 0.2f);        // Gold entry
        public override Color ExitMarkerColor => new(0.8f, 0.2f, 0.15f);        // Danger red
        public override Color PanelBgColor => new(0.08f, 0.06f, 0.05f);         // Dark brown panel

        // Lighting — warm industrial sun
        public override Color MainLightColor => new(0.9f, 0.75f, 0.55f);        // Warm sunlight
        public override Color FillLightColor => new(0.15f, 0.1f, 0.08f);        // Dim warm fill

        public override StandardMaterial3D MakeThemedMaterial(Color tint)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = tint.Darkened(0.5f);
            mat.Roughness = 0.85f;
            mat.Metallic = 0.6f;
            // Subtle warm emission — things glow with heat, not data
            mat.EmissionEnabled = true;
            mat.Emission = tint;
            mat.EmissionEnergyMultiplier = 0.25f;
            return mat;
        }

        public override StandardMaterial3D MakeEnemyMaterial(Color tint)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = tint.Darkened(0.3f);
            mat.Roughness = 0.9f;
            mat.Metallic = 0.5f;
            // Enemies glow faintly with toxic/corrosive energy
            mat.EmissionEnabled = true;
            mat.Emission = tint;
            mat.EmissionEnergyMultiplier = 0.4f;
            return mat;
        }

        public override StandardMaterial3D MakeProjectileMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = ProjectileColor;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = ProjectileColor;
            mat.EmissionEnergyMultiplier = 2.5f;
            return mat;
        }

        public override StandardMaterial3D MakeGroundMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = GroundColor;
            mat.Roughness = 0.95f;
            mat.Metallic = 0.1f;
            return mat;
        }

        public override StandardMaterial3D MakeWallMaterial()
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = WallColor;
            mat.Roughness = 0.9f;
            mat.Metallic = 0.5f;
            // Slight rust-orange emission at edges
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.5f, 0.25f, 0.08f);
            mat.EmissionEnergyMultiplier = 0.15f;
            return mat;
        }
    }
}
