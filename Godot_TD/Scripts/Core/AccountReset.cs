using System;
using System.IO;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Start a new account (Settings, ACCOUNT): the saves that make up the current one (the perk
    /// tree and campaign in vine_meta.json, suits, the relic catalogue, loadouts, the first-run
    /// checklist) move into user://old_account_{date_time}/ and the game restarts with none of
    /// them. Nothing is deleted: moving those files back restores the old account. Settings stay.
    /// </summary>
    public static class AccountReset
    {
        /// <summary>The files an account is made of (each with its .bak and .tmp).</summary>
        public static readonly string[] AccountFiles =
        {
            "user://vine_meta.json", "user://suits.json", "user://relic_inventory.json", "user://loadouts.json", "user://ui.cfg",
        };

        /// <summary>
        /// True once the account has been moved aside: saves are refused until the restart, so
        /// the old account still held in memory isn't written back on the way out.
        /// </summary>
        public static bool Blocking { get; private set; }

        /// <summary>
        /// Move the current account's files into a new old_account folder. Returns the folder's
        /// name, or null when there was nothing to move.
        /// </summary>
        public static string ArchiveCurrent()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = $"old_account_{stamp}";
            string dir = SafeFile.DiskPath($"user://{folder}");
            int moved = 0;
            foreach (var godotPath in AccountFiles)
            {
                string path = SafeFile.DiskPath(godotPath);
                foreach (var f in new[] { path, path + ".bak", path + ".tmp" })
                {
                    if (!File.Exists(f)) continue;
                    try
                    {
                        Directory.CreateDirectory(dir);
                        File.Move(f, Path.Combine(dir, Path.GetFileName(f)), overwrite: true);
                        moved++;
                    }
                    catch (Exception e) { GD.PushError($"[AccountReset] Could not move {f}: {e.Message}"); }
                }
            }
            GD.Print($"[AccountReset] {moved} save file(s) moved to user://{folder}");
            return moved > 0 ? folder : null;
        }

        /// <summary>Move the account aside and restart the game fresh.</summary>
        public static void StartNew(Node from)
        {
            ArchiveCurrent();
            Blocking = true;
            OS.SetRestartOnExit(true, OS.GetCmdlineArgs());
            // From the autoload: tearing the scene down frees the screen that asked
            HeadlessShutdown.QuitClean((Node)GameManager.Instance ?? from, 0);
        }
    }
}
