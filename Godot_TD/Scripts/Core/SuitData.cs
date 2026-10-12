using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// One tower in a suit's kit: its type, level and branch. The grid position is where it
    /// stood when the suit was saved; a kit tower can be placed anywhere, so it is only kept for
    /// older saves.
    /// </summary>
    public class SuitNodeEntry
    {
        public int GridX;
        public int GridY;
        public VineNodeType NodeType;
        public TowerComponentType[] Components;
        /// <summary>Upgrade level (1 = as built).</summary>
        public int Level = 1;
        /// <summary>Branch id from Data/tower_upgrades.json, or null.</summary>
        public string Branch;
    }

    /// <summary>
    /// A saved suit: a kit of the best towers from a run (<see cref="Constants.SUIT_KIT_SIZE"/> at
    /// most), taken into a boss fight. There its towers are on the build bar marked KIT, free to
    /// place anywhere at their levels and branches. Lose the boss fight and the suit is lost.
    /// </summary>
    public class SuitSaveData
    {
        public string Name;
        public string Role;             // Obelisk/Arcanist/Bruteforge: the role the kit was built with
        public int Planet;
        public MaterialType Material;
        /// <summary>The kit (named Nodes for older saves).</summary>
        public List<SuitNodeEntry> Nodes = new();
        public string[] RelicNames;     // Attached relics (future)
        public bool Consumed;           // Lost in a failed boss fight
        public long CreatedTimestamp;
        /// <summary>Where it was built: the site's name and the wave the run reached.</summary>
        public string Site = "";
        public int Wave;

        /// <summary>A suit that can be taken into a boss fight.</summary>
        public bool Usable => !Consumed && Nodes.Count > 0;
    }
}
