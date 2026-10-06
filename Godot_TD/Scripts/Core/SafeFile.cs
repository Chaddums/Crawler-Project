using System;
using System.IO;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Crash-safe save file I/O for user:// data.
    ///
    /// Writes go to a temp file first and are swapped into place, keeping the previous
    /// version as .bak. Opening the save directly with FileAccess.ModeFlags.Write truncates
    /// it immediately, so a crash mid-write left a partial file — which then failed to
    /// parse, loaded as defaults, and was overwritten on the next save (progress lost).
    /// </summary>
    public static class SafeFile
    {
        private const string SandboxDir = "sandbox/";
        private static string _userPrefix; // resolved on first use

        /// <summary>True when automated runs (test harness / AutoPlayer) are redirected to user://sandbox/.</summary>
        public static bool IsSandboxed { get { Resolve("user://"); return _userPrefix.Length > 0; } }

        /// <summary>
        /// Map a user:// path to disk. Automated runs (--test-harness, --autoplay) use a fresh
        /// user://sandbox/ so they never read or overwrite the player's real saves. Decided
        /// here from the command line because GameManager loads saves before the test
        /// harness or AutoPlayer autoloads get a chance to run.
        /// </summary>
        private static string Resolve(string godotPath)
        {
            if (_userPrefix == null)
            {
                _userPrefix = "";
                var args = new System.Collections.Generic.List<string>(OS.GetCmdlineArgs());
                args.AddRange(OS.GetCmdlineUserArgs());
                if (args.Contains("--test-harness") || args.Contains("--autoplay"))
                {
                    _userPrefix = SandboxDir;
                    string dir = ProjectSettings.GlobalizePath("user://" + SandboxDir);
                    try
                    {
                        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                        Directory.CreateDirectory(dir);
                    }
                    catch (Exception e) { GD.PushWarning($"[SafeFile] Could not reset sandbox: {e.Message}"); }
                    GD.Print("[SafeFile] Automated run — saves redirected to user://sandbox/");
                }
            }
            if (_userPrefix.Length > 0 && godotPath.StartsWith("user://"))
                godotPath = "user://" + _userPrefix + godotPath.Substring("user://".Length);
            return ProjectSettings.GlobalizePath(godotPath);
        }

        /// <summary>Atomically replace <paramref name="godotPath"/> with <paramref name="text"/>.</summary>
        public static bool WriteAllText(string godotPath, string text)
        {
            string path = Resolve(godotPath);
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var fs = new FileStream(tmp, FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(text);
                    writer.Flush();
                    fs.Flush(true); // push to disk before the swap
                }
                if (File.Exists(path))
                    File.Copy(path, bak, overwrite: true);
                File.Move(tmp, path, overwrite: true);
                return true;
            }
            catch (Exception e)
            {
                GD.PushError($"[SafeFile] Failed to write {godotPath}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Read <paramref name="godotPath"/>, falling back to its .bak when the main file is
        /// missing, empty or fails <paramref name="isValid"/>. A corrupt main file is moved
        /// aside (.corrupt) so it isn't silently overwritten. Returns null if nothing usable.
        /// </summary>
        public static string ReadAllText(string godotPath, Func<string, bool> isValid)
        {
            string path = Resolve(godotPath);
            string bak = path + ".bak";

            string main = TryRead(path);
            if (!string.IsNullOrWhiteSpace(main) && isValid(main))
                return main;

            if (main != null)
            {
                string corrupt = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                try { File.Copy(path, corrupt, overwrite: true); } catch { /* best effort */ }
                GD.PushWarning($"[SafeFile] {godotPath} is unreadable — copied to {Path.GetFileName(corrupt)}, trying backup");
            }

            string backup = TryRead(bak);
            if (!string.IsNullOrWhiteSpace(backup) && isValid(backup))
            {
                // Put the good copy back in place so the next write's .bak rotation
                // doesn't replace the good backup with the corrupt file.
                try { File.Copy(bak, path, overwrite: true); } catch { /* best effort */ }
                GD.PushWarning($"[SafeFile] Restored {godotPath} from backup");
                return backup;
            }

            return null;
        }

        private static string TryRead(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception e)
            {
                GD.PushWarning($"[SafeFile] Failed to read {path}: {e.Message}");
                return null;
            }
        }
    }
}
