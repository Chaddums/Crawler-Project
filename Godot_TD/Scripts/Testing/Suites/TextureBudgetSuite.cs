using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Guards the texture import policy for 3D assets (Models/, Materials/): every texture is
    /// VRAM compressed and no larger than its folder's cap once imported, and actually reimported
    /// that way. The kit came in with no size limits (327 textures at 4096 or more) and 447 of them
    /// lossless, so a Grid Prime battle held about 5 GB of textures and ran a software-rendered
    /// test out of memory; with the policy the same battle holds about 0.25 GB.
    /// Reads image headers and .import files only, so it's fast and headless-safe.
    /// </summary>
    public class TextureBudgetSuite : ITestSuite
    {
        public string SuiteName => "textures";

        // Longest prefix wins. Kept in step with the import settings in the .import files.
        internal static readonly (string prefix, int cap)[] Caps =
        {
            ("Models/Spires/", 2048),
            ("Materials/", 2048),
            ("Models/", 1024),
        };

        private static readonly Regex ImageImport = new(@"\.(png|jpe?g)\.import$", RegexOptions.IgnoreCase);

        internal static int? CapFor(string rel)
        {
            (string prefix, int cap)? best = null;
            foreach (var c in Caps)
                if (rel.StartsWith(c.prefix) && (best == null || c.prefix.Length > best.Value.prefix.Length))
                    best = c;
            return best?.cap;
        }

        public Task Run(TestContext ctx)
        {
            string root = ProjectSettings.GlobalizePath("res://");
            var uncompressed = new List<string>();
            var oversize = new List<string>();
            var stale = new List<string>();
            var unreadable = 0;
            var vramByFolder = new Dictionary<string, double>();
            int checkedCount = 0;

            foreach (var dir in new[] { "Models", "Materials" })
            {
                string abs = Path.Combine(root, dir);
                if (!Directory.Exists(abs)) continue;
                foreach (var imp in Directory.EnumerateFiles(abs, "*.import", SearchOption.AllDirectories))
                {
                    if (!ImageImport.IsMatch(imp)) continue;
                    string src = imp[..^".import".Length];
                    if (!File.Exists(src)) continue; // stale import with no source
                    string rel = Path.GetRelativePath(root, src).Replace('\\', '/');
                    int? cap = CapFor(rel);
                    if (cap == null) continue;

                    string text = File.ReadAllText(imp);
                    if (!text.Contains("importer=\"texture\"")) continue;
                    int mode = ReadInt(text, "compress/mode", -1);
                    int limit = ReadInt(text, "process/size_limit", 0);
                    var info = ReadImageInfo(src);
                    if (info == null) { unreadable++; continue; }
                    checkedCount++;

                    var (w, h, alpha) = info.Value;
                    int longest = Math.Max(w, h);
                    float scale = limit > 0 && longest > limit ? (float)limit / longest : 1f;
                    int ew = Math.Max(1, (int)(w * scale)), eh = Math.Max(1, (int)(h * scale));

                    if (mode != 2) uncompressed.Add($"{rel} (mode {mode})");
                    if (Math.Max(ew, eh) > cap) oversize.Add($"{rel} ({ew}x{eh} > {cap})");
                    // The importer writes this flag; settings that say VRAM over a file that
                    // says otherwise were never reimported, and the game still loads RGB8
                    if (mode == 2 && !text.Contains("\"vram_texture\": true")) stale.Add(rel);

                    // BC1 for opaque, BC3 with alpha; RGB8 or RGBA8 when lossless
                    double bytesPerPixel = mode == 2 ? (alpha ? 1.0 : 0.5) : (alpha ? 4.0 : 3.0);
                    string folder = string.Join('/', rel.Split('/').Take(2));
                    vramByFolder[folder] = vramByFolder.GetValueOrDefault(folder) + ew * (double)eh * bytesPerPixel * 4.0 / 3.0;
                }
            }

            ctx.StartTest();
            ctx.Assert(checkedCount > 0, "textures/found", "No 3D textures found under Models/ or Materials/");
            ctx.StartTest();
            ctx.Assert(uncompressed.Count == 0, "textures/vram_compressed",
                $"{uncompressed.Count} 3D textures import uncompressed: {string.Join(", ", uncompressed.Take(5))}");
            ctx.StartTest();
            ctx.Assert(oversize.Count == 0, "textures/within_cap",
                $"{oversize.Count} textures import above their folder's cap: {string.Join(", ", oversize.Take(5))}");
            ctx.StartTest();
            ctx.Assert(stale.Count == 0, "textures/imported_as_vram",
                $"{stale.Count} textures are set to VRAM but were never reimported (select them in the " +
                $"FileSystem dock and press Reimport): {string.Join(", ", stale.Take(5))}");

            double total = vramByFolder.Values.Sum();
            GD.Print($"[TextureBudget] {checkedCount} textures checked, {unreadable} unreadable (LFS pointers?), " +
                     $"{total / 1048576.0:F0} MB if every one loaded");
            foreach (var (folder, bytes) in vramByFolder.OrderByDescending(kv => kv.Value))
                GD.Print($"[TextureBudget]   {folder}: {bytes / 1048576.0:F0} MB");
            return Task.CompletedTask;
        }

        private static int ReadInt(string text, string key, int fallback)
        {
            var m = Regex.Match(text, "^" + Regex.Escape(key) + @"=(-?\d+)\s*$", RegexOptions.Multiline);
            return m.Success ? int.Parse(m.Groups[1].Value) : fallback;
        }

        /// <summary>Width, height and alpha from a PNG or JPEG header, without decoding the image.</summary>
        internal static (int w, int h, bool alpha)? ReadImageInfo(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var head = new byte[26];
                if (fs.Read(head, 0, 26) < 26) return null;
                if (head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G')
                    return (BigEndian(head, 16), BigEndian(head, 20), head[25] is 4 or 6); // colour type: grey+alpha, RGBA
                if (head[0] != 0xFF || head[1] != 0xD8) return null;

                // JPEG: walk markers to the first start-of-frame
                fs.Position = 2;
                while (fs.Position < fs.Length)
                {
                    int b = fs.ReadByte();
                    if (b != 0xFF) continue;
                    int marker = fs.ReadByte();
                    while (marker == 0xFF) marker = fs.ReadByte();
                    if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) continue;
                    int len = (fs.ReadByte() << 8) | fs.ReadByte();
                    bool sof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                    if (sof)
                    {
                        fs.ReadByte(); // precision
                        int h = (fs.ReadByte() << 8) | fs.ReadByte();
                        int w = (fs.ReadByte() << 8) | fs.ReadByte();
                        return (w, h, false);
                    }
                    fs.Position += len - 2;
                }
            }
            catch (Exception) { }
            return null;
        }

        private static int BigEndian(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
    }
}
