using Godot;
using JunkbotArena.Editor;

namespace JunkbotArena
{
    /// <summary>
    /// Temporary autoload: runs catalog export then quits.
    /// Add to autoloads, run once, then remove.
    /// </summary>
    public partial class CatalogExportRunner : Node
    {
        private AssetCatalogExporter _exporter;
        private bool _started;

        public override void _Process(double delta)
        {
            if (_started) return;
            _started = true;

            _exporter = new AssetCatalogExporter();
            AddChild(_exporter);
            _exporter.OnProgress += msg => GD.Print($"[CatalogRunner] {msg}");
            _exporter.OnComplete += () =>
            {
                GD.Print("[CatalogRunner] Export complete! Quitting...");
                GetTree().Quit(0);
            };
            _exporter.StartExport();
            GD.Print("[CatalogRunner] Started catalog export...");
        }
    }
}
