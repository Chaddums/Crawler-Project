using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Makes an imported model readable in the canyon. The canyon has no sky and weak lights
    /// (sun 0.35, fill 0.15), so a smooth metal has nothing to reflect and draws pure black:
    /// the Bruteforge Spire (Driller.glb, whose mask texture was exported as its metal and
    /// roughness map: metal 0.99, roughness 0.05) stood as a black silhouette in the middle of
    /// the screen (seen on the PC's renderer, 2026-11). <see cref="Tame"/> gives such surfaces a
    /// metal the lights can show, leaving their colour and normal maps alone.
    /// </summary>
    public static class ModelLook
    {
        /// <summary>At or above this metal and at or below this roughness, a surface only mirrors the (black) sky.</summary>
        public const float BlackMetal = 0.8f, BlackRough = 0.3f;
        /// <summary>What a tamed surface gets: the tower kit's worn steel.</summary>
        public const float TameMetal = 0.55f, TameRough = 0.5f;

        private static readonly Dictionary<ulong, Color> _means = new();

        /// <summary>The metal and roughness a material draws with: its numbers times its textures' channels, averaged.</summary>
        public static (float Metal, float Rough) Effective(BaseMaterial3D b)
        {
            if (b == null) return (0f, 1f);
            float metal = b.Metallic, rough = b.Roughness;
            if (b.MetallicTexture != null) metal *= Channel(Mean(b.MetallicTexture), b.MetallicTextureChannel);
            if (b.RoughnessTexture != null) rough *= Channel(Mean(b.RoughnessTexture), b.RoughnessTextureChannel);
            return (metal, rough);
        }

        /// <summary>A lit, unglowing mirror metal: black in a place with no sky.</summary>
        public static bool DrawsBlack(BaseMaterial3D b)
        {
            if (b == null || b.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded || b.EmissionEnabled) return false;
            var (metal, rough) = Effective(b);
            return metal >= BlackMetal && rough <= BlackRough;
        }

        /// <summary>
        /// Every surface under <paramref name="root"/> that would draw black gets its own copy of
        /// its material as worn steel (its colour, normal and other maps kept). Returns how many.
        /// </summary>
        public static int Tame(Node root)
        {
            if (root == null) return 0;
            int n = 0;
            var copies = new Dictionary<ulong, BaseMaterial3D>();
            foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (node is not MeshInstance3D mi || mi.Mesh == null) continue;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    if (mi.GetActiveMaterial(i) is not BaseMaterial3D b || !DrawsBlack(b)) continue;
                    if (!copies.TryGetValue(b.GetInstanceId(), out var copy))
                    {
                        copy = (BaseMaterial3D)b.Duplicate();
                        copy.MetallicTexture = null;
                        copy.RoughnessTexture = null;
                        copy.Metallic = TameMetal;
                        copy.Roughness = TameRough;
                        copies[b.GetInstanceId()] = copy;
                    }
                    mi.SetSurfaceOverrideMaterial(i, copy);
                    n++;
                }
            }
            return n;
        }

        private static float Channel(Color c, BaseMaterial3D.TextureChannel ch) => ch switch
        {
            BaseMaterial3D.TextureChannel.Red => c.R,
            BaseMaterial3D.TextureChannel.Green => c.G,
            BaseMaterial3D.TextureChannel.Blue => c.B,
            BaseMaterial3D.TextureChannel.Alpha => c.A,
            _ => (c.R + c.G + c.B) / 3f,
        };

        /// <summary>A texture's average colour (white if it can't be read), kept per texture.</summary>
        private static Color Mean(Texture2D t)
        {
            ulong id = t.GetInstanceId();
            if (_means.TryGetValue(id, out var mean)) return mean;
            mean = Colors.White;
            var img = t.GetImage();
            if (img != null && !img.IsEmpty() && (!img.IsCompressed() || img.Decompress() == Error.Ok))
            {
                while (img.GetWidth() > 8 && img.GetHeight() > 8) img.ShrinkX2();
                float r = 0, g = 0, b = 0, a = 0;
                int w = img.GetWidth(), h = img.GetHeight();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var p = img.GetPixel(x, y);
                        r += p.R; g += p.G; b += p.B; a += p.A;
                    }
                float k = 1f / (w * h);
                mean = new Color(r * k, g * k, b * k, a * k);
            }
            _means[id] = mean;
            return mean;
        }
    }
}
