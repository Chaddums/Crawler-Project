using Godot;

namespace JunkyardTD
{
    public static class RelicRegistry
    {
        public struct Relic
        {
            public string Id;
            public string Name;
            public string Desc;
            public string Icon;
            public string Rarity;
            public Color Tint;
            public string Tradeoff;

            public Relic(string id, string name, string desc, string icon, string rarity, Color tint, string tradeoff = null)
            {
                Id = id; Name = name; Desc = desc; Icon = icon;
                Rarity = rarity; Tint = tint; Tradeoff = tradeoff;
            }
        }

        public static readonly Relic[] All = new[]
        {
            new Relic("null-shard",        "Null Shard",        "Negates the first hit each wave",               "shield",           "legendary", new Color(0.6f, 0.2f, 0.8f)),
            new Relic("hex-capacitor",     "Hex Capacitor",     "+15% signal travel speed",                      "electric_bolt",    "rare",      new Color(0.2f, 0.8f, 0.4f)),
            new Relic("phantom-register",  "Phantom Register",  "Towers fire once at ghosts that aren't there",  "visibility_off",   "rare",      new Color(0.8f, 0.3f, 0.5f), "Wastes 1 ammo per wave on decoys"),
            new Relic("aether-coil",       "Aether Coil",       "Passive regen: 2 HP/sec to all nodes",          "healing",          "uncommon",  new Color(0.3f, 0.6f, 0.9f)),
            new Relic("entropic-lens",     "Entropic Lens",     "Critical hits deal 3x instead of 2x",           "auto_awesome",     "legendary", new Color(0.9f, 0.4f, 0.1f), "-10% base damage"),
            new Relic("runic-transistor",  "Runic Transistor",  "Routing nodes gain +1 signal power",            "memory",           "uncommon",  new Color(0.4f, 0.9f, 0.7f)),
            new Relic("void-beacon",       "Void Beacon",       "Reveals cloaked enemies within 12 range",       "radar",            "common",    new Color(0.5f, 0.1f, 0.7f)),
            new Relic("flux-mandala",      "Flux Mandala",      "Slow fields also reduce armor by 2",            "blur_circular",    "rare",      new Color(0.9f, 0.8f, 0.2f)),
            new Relic("quantum-splicer",   "Quantum Splicer",   "10% chance to duplicate any placed node",       "content_copy",     "legendary", new Color(0.1f, 0.7f, 0.8f), "-5% node HP"),
        };
    }
}
