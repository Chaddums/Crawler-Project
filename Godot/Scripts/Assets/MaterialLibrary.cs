using Godot;
using System.Collections.Generic;

namespace JunkbotArena
{
    /// <summary>
    /// Provides PBR materials for dungeon surfaces, with sector-based variation.
    /// Loads pre-generated StandardMaterial3D .tres files from Assets/Materials/Generated/.
    /// Falls back to null (caller uses procedural shader) when materials unavailable.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<string, StandardMaterial3D> _cache = new();
        private static bool _initialized;

        // Sector → material assignments for floors and walls
        // Each sector gets a distinct visual identity using the AmbientCG PBR materials
        private static readonly Dictionary<int, string[]> _floorMaterials = new()
        {
            { 1, new[] { "PavingStones049", "Ground031" } },           // Sector 1: stone/ground
            { 2, new[] { "Metal040", "Metal042A" } },                  // Sector 2: metal plating
            { 3, new[] { "concrete_crack", "damaged_concrete" } },     // Sector 3: cracked concrete
            { 4, new[] { "Metal048A", "Metal053C" } },                 // Sector 4: heavy industrial
            { 5, new[] { "PavingStones084", "rusted_metal_plate" } },  // Sector 5: rust + stone
        };

        private static readonly Dictionary<int, string[]> _wallMaterials = new()
        {
            { 1, new[] { "Metal042B", "PavingStones082" } },           // Sector 1: mixed metal/stone
            { 2, new[] { "Metal045B", "Metal049A" } },                 // Sector 2: dark metal
            { 3, new[] { "Metal055C", "Leaking019B" } },               // Sector 3: corroded/leaking
            { 4, new[] { "Metal053C", "Metal040" } },                  // Sector 4: heavy plate
            { 5, new[] { "rusted_metal_plate", "Metal042A" } },        // Sector 5: rusted
        };

        // Decal materials for environmental storytelling
        private static readonly string[] _decalMaterials = new[]
        {
            "hand_smear", "small_garbage_scatter", "industrial_rubble",
            "painted_stop_sign", "painted_zero", "poster",
            "japanese_no_posters_sign", "garbage_pile"
        };

        private const string MATERIAL_PATH = "res://Assets/Materials/Generated/";

        /// <summary>
        /// Get a PBR floor material appropriate for the given sector.
        /// Returns null if materials not available (caller should use procedural fallback).
        /// </summary>
        public static StandardMaterial3D GetFloorMaterial(int sectorNumber, int variant = 0)
        {
            int key = Mathf.Clamp(sectorNumber, 1, 5);
            if (!_floorMaterials.TryGetValue(key, out var names)) return null;
            string name = names[variant % names.Length];
            return LoadMaterial(name);
        }

        /// <summary>
        /// Get a PBR wall material appropriate for the given sector.
        /// Returns null if materials not available.
        /// </summary>
        public static StandardMaterial3D GetWallMaterial(int sectorNumber, int variant = 0)
        {
            int key = Mathf.Clamp(sectorNumber, 1, 5);
            if (!_wallMaterials.TryGetValue(key, out var names)) return null;
            string name = names[variant % names.Length];
            return LoadMaterial(name);
        }

        /// <summary>
        /// Get a random decal material for environmental detail.
        /// </summary>
        public static StandardMaterial3D GetRandomDecalMaterial()
        {
            string name = _decalMaterials[GD.RandRange(0, _decalMaterials.Length - 1)];
            return LoadMaterial(name);
        }

        /// <summary>
        /// Get a specific named PBR material.
        /// </summary>
        public static StandardMaterial3D GetMaterial(string name)
        {
            return LoadMaterial(name);
        }

        private static StandardMaterial3D LoadMaterial(string name)
        {
            if (_cache.TryGetValue(name, out var cached))
                return cached;

            string path = MATERIAL_PATH + name + ".tres";
            if (!ResourceLoader.Exists(path))
            {
                _cache[name] = null;
                return null;
            }

            var mat = GD.Load<StandardMaterial3D>(path);
            if (mat == null)
            {
                GD.PrintErr($"[MaterialLibrary] Failed to load material: {path}");
                _cache[name] = null;
                return null;
            }

            _cache[name] = mat;
            if (!_initialized)
            {
                _initialized = true;
                GD.Print($"[MaterialLibrary] First material loaded: {name}");
            }
            return mat;
        }

        /// <summary>
        /// Create a tinted duplicate of a PBR material (for room-type color variation).
        /// </summary>
        public static StandardMaterial3D GetTintedMaterial(string name, Color tint)
        {
            var baseMat = LoadMaterial(name);
            if (baseMat == null) return null;

            var tinted = (StandardMaterial3D)baseMat.Duplicate();
            tinted.AlbedoColor = tint;
            return tinted;
        }
    }
}
