using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Data loaded from Data/Spires/*.json. Defines a spire archetype's
    /// model, defense stats, placement mode, and tower mechanics.
    /// </summary>
    public class SpireData
    {
        // ── Identity ──
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Tagline { get; set; }
        public Color Color { get; set; }

        // ── Model ──
        public string ModelPath { get; set; }
        public float ModelScale { get; set; }
        public float BurialDepth { get; set; }
        public float RotationSpeed { get; set; }

        // ── Placement ──
        public PlacementMode PlacementMode { get; set; }
        public bool GridSnap { get; set; }
        public float IncrementSize { get; set; }
        public float BasePowerRadius { get; set; }

        // ── Defense ──
        public float MaxHP { get; set; }
        public float HpRegenPerSec { get; set; }
        public float Shield { get; set; }
        public float ShieldRechargeDelay { get; set; }

        // ── Attack ──
        public bool HasBeamAttack { get; set; }
        public float BeamDamage { get; set; }
        public float BeamCooldown { get; set; }
        public float BeamRange { get; set; }
        public float BeamDuration { get; set; }

        public int AutocannonCount { get; set; }
        public float AutocannonFireRate { get; set; }
        public float AutocannonDamage { get; set; }
        public float AutocannonRange { get; set; }

        // ── Tower Mechanics ──
        public bool RequiresLinkToSpire { get; set; }

        // Pylons (Obelisk)
        public bool PylonsEnabled { get; set; }
        public float PylonPowerRadius { get; set; }
        public bool PylonMagicInfusable { get; set; }
        public bool PylonStackOverlapping { get; set; }

        // Sockets (Arcanist)
        public bool SocketsEnabled { get; set; }
        public bool SocketMagicInfusable { get; set; }
        public bool SocketMagicAppliesToAll { get; set; }
        public bool PrismsEnabled { get; set; }

        // Wires (Bruteforge)
        public bool WiresEnabled { get; set; }
        public bool WiresMustConnectToForge { get; set; }
        public int WireDefaultPropagationRange { get; set; }
        public bool WirePowerNodesEnabled { get; set; }

        // ── Available nodes ──
        public VineNodeType[] Nodes { get; set; }

        // ── Registry ──
        private static readonly Dictionary<string, SpireData> _registry = new();

        public static SpireData Get(string id)
        {
            if (_registry.Count == 0) LoadAll();
            return _registry.TryGetValue(id, out var data) ? data : null;
        }

        public static IEnumerable<SpireData> All()
        {
            if (_registry.Count == 0) LoadAll();
            return _registry.Values;
        }

        private static void LoadAll()
        {
            var dir = DirAccess.Open("res://Data/Spires");
            if (dir == null)
            {
                GD.PrintErr("[SpireData] Failed to open Data/Spires directory");
                return;
            }

            dir.ListDirBegin();
            string fileName;
            while ((fileName = dir.GetNext()) != "")
            {
                if (!fileName.EndsWith(".json")) continue;
                string path = $"res://Data/Spires/{fileName}";
                var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr($"[SpireData] Failed to open {path}");
                    continue;
                }

                string json = file.GetAsText();
                file.Close();

                var parsed = Json.ParseString(json);
                if (parsed.VariantType != Variant.Type.Dictionary)
                {
                    GD.PrintErr($"[SpireData] Invalid JSON in {path}");
                    continue;
                }

                var dict = parsed.AsGodotDictionary();
                var data = ParseFromDict(dict);
                if (data != null)
                {
                    _registry[data.Id] = data;
                    GD.Print($"[SpireData] Loaded: {data.Id} ({data.DisplayName})");
                }
            }
            dir.ListDirEnd();
        }

        private static SpireData ParseFromDict(Godot.Collections.Dictionary dict)
        {
            var data = new SpireData();

            data.Id = dict.TryGetValue("id", out var id) ? id.AsString() : "Unknown";
            data.DisplayName = dict.TryGetValue("displayName", out var dn) ? dn.AsString() : data.Id;
            data.Tagline = dict.TryGetValue("tagline", out var tl) ? tl.AsString() : "";

            if (dict.TryGetValue("color", out var colorVar))
            {
                var c = colorVar.AsGodotArray();
                data.Color = new Color((float)c[0], (float)c[1], (float)c[2]);
            }

            // Model
            if (dict.TryGetValue("model", out var modelVar))
            {
                var m = modelVar.AsGodotDictionary();
                data.ModelPath = GetStr(m, "path", "");
                data.ModelScale = GetFloat(m, "scale", 0.35f);
                data.BurialDepth = GetFloat(m, "burialDepth", 0f);
                data.RotationSpeed = GetFloat(m, "rotationSpeed", 0f);
            }

            // Placement
            if (dict.TryGetValue("placement", out var placeVar))
            {
                var p = placeVar.AsGodotDictionary();
                string mode = GetStr(p, "mode", "WireNetwork");
                data.PlacementMode = mode switch
                {
                    "FreeRadius" => PlacementMode.FreeRadius,
                    "SocketGrid" => PlacementMode.SocketGrid,
                    _ => PlacementMode.WireNetwork
                };
                data.GridSnap = GetBool(p, "gridSnap", true);
                data.IncrementSize = GetFloat(p, "incrementSize", 2f);
                data.BasePowerRadius = GetFloat(p, "basePowerRadius", 0f);
            }

            // Defense
            if (dict.TryGetValue("defense", out var defVar))
            {
                var d = defVar.AsGodotDictionary();
                data.MaxHP = GetFloat(d, "maxHP", 200f);
                data.HpRegenPerSec = GetFloat(d, "hpRegenPerSec", 0f);
                data.Shield = GetFloat(d, "shield", 0f);
                data.ShieldRechargeDelay = GetFloat(d, "shieldRechargeDelay", 0f);

                if (d.TryGetValue("attack", out var atkVar) && atkVar.VariantType == Variant.Type.Dictionary)
                {
                    var a = atkVar.AsGodotDictionary();
                    string atkType = GetStr(a, "type", "");
                    data.HasBeamAttack = atkType == "Beam";
                    data.BeamDamage = GetFloat(a, "damage", 0f);
                    data.BeamCooldown = GetFloat(a, "cooldown", 5f);
                    data.BeamRange = GetFloat(a, "range", 20f);
                    data.BeamDuration = GetFloat(a, "beamDuration", 0.4f);
                }

                data.AutocannonCount = GetInt(d, "autocannons", 0);
                if (d.TryGetValue("autocannonConfig", out var acVar) && acVar.VariantType == Variant.Type.Dictionary)
                {
                    var ac = acVar.AsGodotDictionary();
                    data.AutocannonFireRate = GetFloat(ac, "fireRate", 2f);
                    data.AutocannonDamage = GetFloat(ac, "damage", 12f);
                    data.AutocannonRange = GetFloat(ac, "range", 14f);
                }
            }

            // Tower Mechanics
            if (dict.TryGetValue("towerMechanics", out var mechVar))
            {
                var tm = mechVar.AsGodotDictionary();
                data.RequiresLinkToSpire = GetBool(tm, "requiresLinkToSpire", false);

                if (tm.TryGetValue("pylons", out var pylVar) && pylVar.VariantType == Variant.Type.Dictionary)
                {
                    var py = pylVar.AsGodotDictionary();
                    data.PylonsEnabled = GetBool(py, "enabled", false);
                    data.PylonPowerRadius = GetFloat(py, "powerRadius", 6f);
                    data.PylonMagicInfusable = GetBool(py, "magicInfusable", false);
                    data.PylonStackOverlapping = GetBool(py, "stackOverlapping", false);
                }

                if (tm.TryGetValue("sockets", out var sockVar) && sockVar.VariantType == Variant.Type.Dictionary)
                {
                    var so = sockVar.AsGodotDictionary();
                    data.SocketsEnabled = GetBool(so, "enabled", false);
                    data.SocketMagicInfusable = GetBool(so, "magicInfusable", false);
                    data.SocketMagicAppliesToAll = GetBool(so, "magicAppliesToAllConnected", false);
                    if (so.TryGetValue("prisms", out var prVar) && prVar.VariantType == Variant.Type.Dictionary)
                    {
                        var pr = prVar.AsGodotDictionary();
                        data.PrismsEnabled = GetBool(pr, "enabled", false);
                    }
                }

                if (tm.TryGetValue("wires", out var wireVar) && wireVar.VariantType == Variant.Type.Dictionary)
                {
                    var w = wireVar.AsGodotDictionary();
                    data.WiresEnabled = GetBool(w, "enabled", false);
                    data.WiresMustConnectToForge = GetBool(w, "mustConnectToForge", false);
                    data.WireDefaultPropagationRange = GetInt(w, "defaultPropagationRange", 4);
                    if (w.TryGetValue("powerNodes", out var pnVar) && pnVar.VariantType == Variant.Type.Dictionary)
                    {
                        var pn = pnVar.AsGodotDictionary();
                        data.WirePowerNodesEnabled = GetBool(pn, "enabled", false);
                    }
                }
            }

            // Nodes
            if (dict.TryGetValue("nodes", out var nodesVar))
            {
                var nodeArray = nodesVar.AsGodotArray();
                var nodes = new List<VineNodeType>();
                foreach (var n in nodeArray)
                {
                    if (System.Enum.TryParse<VineNodeType>(n.AsString(), out var nodeType))
                        nodes.Add(nodeType);
                }
                data.Nodes = nodes.ToArray();
            }

            return data;
        }

        private static string GetStr(Godot.Collections.Dictionary d, string key, string def)
            => d.TryGetValue(key, out var v) ? v.AsString() : def;

        private static float GetFloat(Godot.Collections.Dictionary d, string key, float def)
            => d.TryGetValue(key, out var v) ? (float)v.AsDouble() : def;

        private static int GetInt(Godot.Collections.Dictionary d, string key, int def)
            => d.TryGetValue(key, out var v) ? v.AsInt32() : def;

        private static bool GetBool(Godot.Collections.Dictionary d, string key, bool def)
            => d.TryGetValue(key, out var v) ? v.AsBool() : def;
    }
}
