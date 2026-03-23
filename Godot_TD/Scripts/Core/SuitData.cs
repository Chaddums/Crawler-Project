using System.Collections.Generic;

namespace JunkyardTD
{
    /// <summary>
    /// A single placed vine node, serialized for suit storage.
    /// </summary>
    public class SuitNodeEntry
    {
        public int GridX;
        public int GridY;
        public VineNodeType NodeType;
        public TowerComponentType[] Components;
    }

    /// <summary>
    /// A saved suit — a snapshot of a successful tower build from a farming run.
    /// Brought into a boss run pre-placed.
    /// </summary>
    public class SuitSaveData
    {
        public string Name;
        public string Role;             // Obelisk/Arcanist/Bruteforge
        public int Planet;
        public MaterialType Material;
        public List<SuitNodeEntry> Nodes = new();
        public string[] RelicNames;     // Attached relics (future)
        public bool Consumed;           // Destroyed in failed boss run
        public long CreatedTimestamp;
    }
}
