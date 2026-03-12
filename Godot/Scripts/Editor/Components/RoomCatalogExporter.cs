using Godot;
using System;
using System.Collections.Generic;

namespace JunkbotArena.Editor
{
    /// <summary>
    /// Batch-renders all room types and combat layouts to PNG screenshots and generates
    /// an HTML gallery. Iterates through every RoomType, building rooms via RoomBuilder.
    /// For Combat and Megabonk types, cycles through all combat layouts.
    ///
    /// Output goes to user://room_catalog/ with an index.html for browsing.
    /// </summary>
    public partial class RoomCatalogExporter : Node
    {
        private SubViewport _viewport;
        private Camera3D _camera;
        private Node3D _roomRoot;

        private readonly List<(string category, string id, RoomType type, int layoutIndex)> _queue = new();
        private int _currentIndex;
        private int _frameWait;
        private string _outputDir;
        private bool _exporting;
        private readonly Dictionary<string, List<(string id, string displayName)>> _capturedByCategory = new();

        public event Action<string> OnProgress;
        public event Action OnComplete;

        private static readonly RoomType[] RoomTypesToExport =
        {
            RoomType.Combat, RoomType.Entrance, RoomType.Treasure, RoomType.Shop,
            RoomType.Boss, RoomType.SafeRoom, RoomType.Puzzle, RoomType.Event, RoomType.Megabonk
        };

        public override void _Ready()
        {
            _viewport = new SubViewport();
            _viewport.Size = new Vector2I(800, 600);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.OwnWorld3D = true;
            _viewport.TransparentBg = false;
            AddChild(_viewport);

            _camera = new Camera3D();
            _camera.Position = new Vector3(40, 35, 40);
            _camera.LookAt(new Vector3(0, 2, 0));
            _viewport.AddChild(_camera);

            _roomRoot = new Node3D();
            _viewport.AddChild(_roomRoot);

            // Three-point lighting — brighter for larger rooms
            var keyLight = new DirectionalLight3D();
            keyLight.RotationDegrees = new Vector3(-55, 40, 0);
            keyLight.LightEnergy = 3.0f;
            keyLight.LightColor = new Color(1.0f, 0.98f, 0.95f);
            _viewport.AddChild(keyLight);

            var fillLight = new DirectionalLight3D();
            fillLight.RotationDegrees = new Vector3(-40, -60, 0);
            fillLight.LightEnergy = 1.2f;
            fillLight.LightColor = new Color(0.85f, 0.9f, 1.0f);
            _viewport.AddChild(fillLight);

            var rimLight = new DirectionalLight3D();
            rimLight.RotationDegrees = new Vector3(-20, 180, 0);
            rimLight.LightEnergy = 0.9f;
            _viewport.AddChild(rimLight);

            var env = new WorldEnvironment();
            var envRes = new Godot.Environment();
            envRes.BackgroundMode = Godot.Environment.BGMode.Color;
            envRes.BackgroundColor = new Color(0.08f, 0.08f, 0.1f);
            envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            envRes.AmbientLightColor = new Color(0.45f, 0.45f, 0.5f);
            envRes.AmbientLightEnergy = 1.2f;
            envRes.TonemapMode = Godot.Environment.ToneMapper.Aces;
            envRes.TonemapWhite = 6.0f;
            envRes.SsaoEnabled = true;
            envRes.SsaoIntensity = 1.0f;
            env.Environment = envRes;
            _viewport.AddChild(env);

            var probe = new ReflectionProbe();
            probe.BoxProjection = true;
            probe.Size = new Vector3(80, 80, 80);
            probe.Interior = true;
            probe.AmbientMode = ReflectionProbe.AmbientModeEnum.Color;
            probe.AmbientColor = new Color(0.3f, 0.3f, 0.35f);
            _viewport.AddChild(probe);
        }

        public void StartExport()
        {
            if (_exporting) return;

            _outputDir = ProjectSettings.GlobalizePath("user://room_catalog");
            DirAccess.MakeDirRecursiveAbsolute(_outputDir);

            _queue.Clear();
            _capturedByCategory.Clear();

            int layoutCount = RoomLayoutLibrary.CombatLayoutCount;
            var layoutNames = RoomLayoutLibrary.GetCombatLayoutNames();

            foreach (var type in RoomTypesToExport)
            {
                string category = type.ToString();

                if (type == RoomType.Combat || type == RoomType.Megabonk)
                {
                    for (int i = 0; i < layoutCount; i++)
                    {
                        string safeName = SanitizeFilename(layoutNames[i]);
                        string id = $"layout_{i}_{safeName}";
                        _queue.Add((category, id, type, i));
                    }
                }
                else
                {
                    _queue.Add((category, category, type, -1));
                }
            }

            _currentIndex = 0;
            _frameWait = 0;
            _exporting = true;

            var seenCats = new HashSet<string>();
            foreach (var (cat, _, _, _) in _queue)
            {
                if (seenCats.Add(cat))
                    DirAccess.MakeDirRecursiveAbsolute($"{_outputDir}/{cat}");
            }

            GD.Print($"[RoomCatalog] Starting export of {_queue.Count} rooms to {_outputDir}");
            OnProgress?.Invoke($"Exporting 0/{_queue.Count}...");
        }

        public override void _Process(double delta)
        {
            if (!_exporting) return;

            if (_frameWait > 0)
            {
                _frameWait--;
                if (_frameWait == 0)
                    CaptureCurrentRoom();
                return;
            }

            if (_currentIndex >= _queue.Count)
            {
                FinishExport();
                return;
            }

            LoadNextRoom();
        }

        private void LoadNextRoom()
        {
            var (category, id, type, layoutIndex) = _queue[_currentIndex];

            // Clear previous room
            foreach (var child in _roomRoot.GetChildren())
            {
                _roomRoot.RemoveChild(child);
                ((Node)child).QueueFree();
            }

            Vector2 roomSize = RoomBuilder.GetRoomSize(type);
            Node3D room = RoomBuilder.BuildRoom(
                Vector3.Zero, roomSize, type,
                doorNorth: true, doorSouth: true, doorEast: true, doorWest: true,
                sectorData: null, RoomShape.Rectangle, default, layoutIndex, -2);

            if (room == null)
            {
                GD.Print($"[RoomCatalog] Skipped {category}/{id} (BuildRoom returned null)");
                _currentIndex++;
                return;
            }

            _roomRoot.AddChild(room);

            // Position camera based on room size
            FitCameraToRoom(roomSize);

            // Wait 4 frames for geometry to settle (rooms have more geometry than single assets)
            _frameWait = 4;
        }

        private void FitCameraToRoom(Vector2 roomSize)
        {
            float halfW = roomSize.X / 2f;
            float halfH = roomSize.Y / 2f;
            float maxDim = Mathf.Max(halfW, halfH);
            float distance = maxDim * 1.6f;
            float height = maxDim * 1.1f;

            _camera.Position = new Vector3(distance, height, distance);
            _camera.LookAt(new Vector3(0, 2, 0));
        }

        private void CaptureCurrentRoom()
        {
            if (_currentIndex >= _queue.Count) return;

            var (category, id, _, _) = _queue[_currentIndex];
            string filename = $"{id}.png";
            string filepath = $"{_outputDir}/{category}/{filename}";

            var img = _viewport.GetTexture().GetImage();
            if (img != null)
            {
                img.SavePng(filepath);

                if (!_capturedByCategory.ContainsKey(category))
                    _capturedByCategory[category] = new List<(string, string)>();

                // Build a readable display name
                string displayName = id;
                if (id.StartsWith("layout_"))
                {
                    // Extract the layout name portion after "layout_N_"
                    int firstUnderscore = id.IndexOf('_');
                    int secondUnderscore = id.IndexOf('_', firstUnderscore + 1);
                    if (secondUnderscore >= 0 && secondUnderscore + 1 < id.Length)
                        displayName = id.Substring(secondUnderscore + 1);
                }

                _capturedByCategory[category].Add((id, displayName));
            }
            else
            {
                GD.PrintErr($"[RoomCatalog] Failed to capture {category}/{id}");
            }

            _currentIndex++;
            int pct = (int)((float)_currentIndex / _queue.Count * 100);
            OnProgress?.Invoke($"Exporting {_currentIndex}/{_queue.Count} ({pct}%) — {category}/{id}");
        }

        private void FinishExport()
        {
            _exporting = false;
            GenerateHtml();

            int total = _currentIndex;
            GD.Print($"[RoomCatalog] Export complete: {total} rooms to {_outputDir}");
            OnProgress?.Invoke($"Done! {total} rooms exported.");
            OnComplete?.Invoke();
        }

        private static string SanitizeFilename(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                else if (c == ' ')
                    sb.Append('_');
            }
            return sb.ToString().ToLower();
        }

        private void GenerateHtml()
        {
            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            html.AppendLine("<title>Junkbot Arena — Room Catalog</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { background: #1a1a2e; color: #e0e0e0; font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #00d4ff; border-bottom: 2px solid #00d4ff; padding-bottom: 8px; }");
            html.AppendLine("h2 { color: #ffa500; margin-top: 30px; cursor: pointer; }");
            html.AppendLine("h2:hover { color: #ffcc00; }");
            html.AppendLine(".category { margin-bottom: 20px; }");
            html.AppendLine(".grid { display: flex; flex-wrap: wrap; gap: 12px; }");
            html.AppendLine(".card { background: #16213e; border: 1px solid #333; border-radius: 8px; padding: 8px; width: 280px; text-align: center; transition: border-color 0.2s; position: relative; }");
            html.AppendLine(".card:hover { border-color: #00d4ff; }");
            html.AppendLine(".card.flagged { border-color: #ff4444; border-width: 2px; background: #1e1428; }");
            html.AppendLine(".card img { width: 260px; height: 195px; object-fit: contain; border-radius: 4px; background: #0a0a1a; cursor: pointer; }");
            html.AppendLine(".card .name { font-size: 11px; color: #aaa; margin-top: 4px; word-break: break-all; }");
            html.AppendLine(".card .id { font-size: 13px; color: #fff; font-weight: bold; margin-top: 2px; }");
            html.AppendLine(".card .flag-row { display:flex; align-items:center; gap:4px; margin-top:6px; justify-content:center; }");
            html.AppendLine(".card .flag-row input[type=checkbox] { accent-color: #ff4444; cursor:pointer; width:16px; height:16px; }");
            html.AppendLine(".card .flag-row label { font-size:11px; color:#ff8888; cursor:pointer; }");
            html.AppendLine(".card .note-box { display:none; margin-top:4px; }");
            html.AppendLine(".card.flagged .note-box { display:block; }");
            html.AppendLine(".card .note-box textarea { width:100%; height:40px; background:#0a0a1a; border:1px solid #444; color:#ddd; font-size:11px; border-radius:4px; padding:4px; resize:vertical; font-family:inherit; }");
            html.AppendLine(".stats { color: #888; font-size: 13px; margin: 10px 0; }");
            html.AppendLine(".toolbar { display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin:10px 0; }");
            html.AppendLine(".filter { padding: 8px 12px; background: #16213e; border: 1px solid #444; color: #fff; font-size: 14px; border-radius: 4px; width: 300px; }");
            html.AppendLine(".toolbar button { padding:8px 16px; border:1px solid #444; border-radius:4px; cursor:pointer; font-size:13px; }");
            html.AppendLine(".btn-export { background:#ff4444; color:#fff; border-color:#ff4444; }");
            html.AppendLine(".btn-export:hover { background:#ff6666; }");
            html.AppendLine(".btn-filter { background:#16213e; color:#ddd; }");
            html.AppendLine(".btn-filter:hover { background:#1e2a4a; }");
            html.AppendLine(".btn-filter.active { background:#3a1a1a; border-color:#ff4444; color:#ff8888; }");
            html.AppendLine(".issue-count { color:#ff8888; font-weight:bold; font-size:14px; }");
            html.AppendLine(".toast { position:fixed; bottom:20px; right:20px; background:#2a5a2a; color:#fff; padding:12px 20px; border-radius:8px; z-index:2000; display:none; font-size:14px; }");
            html.AppendLine(".hidden { display: none !important; }");
            // Lightbox
            html.AppendLine(".lightbox { display:none; position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,0.92); z-index:1000; justify-content:center; align-items:center; flex-direction:column; cursor:pointer; }");
            html.AppendLine(".lightbox.active { display:flex; }");
            html.AppendLine(".lightbox img { max-width:85vw; max-height:75vh; border:2px solid #00d4ff; border-radius:8px; image-rendering: auto; }");
            html.AppendLine(".lightbox .lb-title { color:#fff; font-size:22px; font-weight:bold; margin-top:12px; }");
            html.AppendLine(".lightbox .lb-cat { color:#888; font-size:14px; margin-top:4px; }");
            html.AppendLine(".lightbox .lb-hint { color:#555; font-size:12px; margin-top:8px; }");
            html.AppendLine("</style></head><body>");
            html.AppendLine("<h1>Junkbot Arena — Room Catalog</h1>");
            html.AppendLine($"<p class='stats'>Generated: {DateTime.Now:yyyy-MM-dd HH:mm} | Total rooms: {_currentIndex}</p>");
            html.AppendLine("<div class='toolbar'>");
            html.AppendLine("<input class='filter' type='text' placeholder='Filter rooms...' oninput='filterRooms(this.value)'>");
            html.AppendLine("<button class='btn-filter' id='flaggedOnlyBtn' onclick='toggleFlaggedOnly()'>Show Flagged Only</button>");
            html.AppendLine("<button class='btn-export' onclick='exportIssues()'>Export Issues</button>");
            html.AppendLine("<button class='btn-filter' onclick='clearAllFlags()'>Clear All</button>");
            html.AppendLine("<span class='issue-count' id='issueCount'>0 issues</span>");
            html.AppendLine("</div>");
            // Lightbox element
            html.AppendLine("<div class='lightbox' id='lightbox' onclick='closeLightbox()'>");
            html.AppendLine("<img id='lb-img' src=''><div class='lb-title' id='lb-title'></div><div class='lb-cat' id='lb-cat'></div>");
            html.AppendLine("<div class='lb-hint'>Click anywhere to close | Arrow keys to navigate | Esc to exit</div>");
            html.AppendLine("</div>");

            // Table of contents
            html.AppendLine("<p>");
            foreach (var kvp in _capturedByCategory)
                html.AppendLine($"<a href='#{kvp.Key}' style='color:#00d4ff; margin-right:12px;'>{kvp.Key} ({kvp.Value.Count})</a>");
            html.AppendLine("</p>");

            foreach (var kvp in _capturedByCategory)
            {
                string cat = kvp.Key;
                var entries = kvp.Value;

                html.AppendLine($"<div class='category' id='{cat}'>");
                html.AppendLine($"<h2 onclick=\"this.nextElementSibling.classList.toggle('hidden')\">{cat.ToUpper()} ({entries.Count})</h2>");
                html.AppendLine("<div class='grid'>");

                foreach (var (id, displayName) in entries)
                {
                    string key = $"{cat}/{id}";
                    string safeId = id.Replace("'", "");
                    string safeCat = cat.Replace("'", "");
                    string safeDisplay = displayName.Replace("'", "");
                    html.AppendLine($"<div class='card' data-name='{key}' id='card-{cat}-{safeId}'>");
                    html.AppendLine($"<img src='{cat}/{id}.png' alt='{displayName}' loading='lazy' onclick=\"openLightbox('{safeCat}/{safeId}.png','{safeDisplay}','{safeCat}')\">");
                    html.AppendLine($"<div class='id'>{displayName}</div>");
                    html.AppendLine($"<div class='name'>{cat}</div>");
                    html.AppendLine($"<div class='flag-row'>");
                    html.AppendLine($"<input type='checkbox' id='flag-{cat}-{safeId}' onchange=\"toggleFlag('{key}', this.checked)\">");
                    html.AppendLine($"<label for='flag-{cat}-{safeId}'>Flag Issue</label>");
                    html.AppendLine($"</div>");
                    html.AppendLine($"<div class='note-box'>");
                    html.AppendLine($"<textarea placeholder='Describe the issue...' oninput=\"saveNote('{key}', this.value)\"></textarea>");
                    html.AppendLine($"</div>");
                    html.AppendLine("</div>");
                }

                html.AppendLine("</div></div>");
            }

            // Scripts
            html.AppendLine("<script>");
            // State
            html.AppendLine("let allCards = [], currentIdx = -1, flaggedOnly = false;");
            html.AppendLine("let issues = JSON.parse(localStorage.getItem('roomIssues') || '{}');");
            // Init — restore saved flags/notes on load
            html.AppendLine("document.addEventListener('DOMContentLoaded', () => {");
            html.AppendLine("  allCards = [...document.querySelectorAll('.card')];");
            html.AppendLine("  for (let [key, data] of Object.entries(issues)) {");
            html.AppendLine("    let card = allCards.find(c => c.dataset.name === key);");
            html.AppendLine("    if (!card) continue;");
            html.AppendLine("    if (data.flagged) {");
            html.AppendLine("      card.classList.add('flagged');");
            html.AppendLine("      card.querySelector('input[type=checkbox]').checked = true;");
            html.AppendLine("    }");
            html.AppendLine("    if (data.note) card.querySelector('textarea').value = data.note;");
            html.AppendLine("  }");
            html.AppendLine("  updateIssueCount();");
            html.AppendLine("});");
            // Save helper
            html.AppendLine("function saveIssues() { localStorage.setItem('roomIssues', JSON.stringify(issues)); }");
            // Toggle flag on a card
            html.AppendLine("function toggleFlag(key, checked) {");
            html.AppendLine("  if (!issues[key]) issues[key] = {};");
            html.AppendLine("  issues[key].flagged = checked;");
            html.AppendLine("  let card = allCards.find(c => c.dataset.name === key);");
            html.AppendLine("  if (card) card.classList.toggle('flagged', checked);");
            html.AppendLine("  if (!checked && !issues[key].note) delete issues[key];");
            html.AppendLine("  saveIssues(); updateIssueCount();");
            html.AppendLine("}");
            // Save note text
            html.AppendLine("function saveNote(key, text) {");
            html.AppendLine("  if (!issues[key]) issues[key] = {};");
            html.AppendLine("  issues[key].note = text;");
            html.AppendLine("  saveIssues();");
            html.AppendLine("}");
            // Count display
            html.AppendLine("function updateIssueCount() {");
            html.AppendLine("  let count = Object.values(issues).filter(i => i.flagged).length;");
            html.AppendLine("  document.getElementById('issueCount').textContent = count + ' issue' + (count !== 1 ? 's' : '');");
            html.AppendLine("}");
            // Show flagged only toggle
            html.AppendLine("function toggleFlaggedOnly() {");
            html.AppendLine("  flaggedOnly = !flaggedOnly;");
            html.AppendLine("  let btn = document.getElementById('flaggedOnlyBtn');");
            html.AppendLine("  btn.classList.toggle('active', flaggedOnly);");
            html.AppendLine("  allCards.forEach(c => {");
            html.AppendLine("    if (flaggedOnly && !c.classList.contains('flagged')) c.style.display = 'none';");
            html.AppendLine("    else c.style.display = '';");
            html.AppendLine("  });");
            html.AppendLine("}");
            // Export issues to clipboard as formatted list
            html.AppendLine("function exportIssues() {");
            html.AppendLine("  let lines = ['# Room Review Issues', ''];");
            html.AppendLine("  let flagged = Object.entries(issues).filter(([k,v]) => v.flagged).sort((a,b) => a[0].localeCompare(b[0]));");
            html.AppendLine("  if (flagged.length === 0) { showToast('No flagged issues'); return; }");
            html.AppendLine("  let currentCat = '';");
            html.AppendLine("  flagged.forEach(([key, data]) => {");
            html.AppendLine("    let [cat, ...rest] = key.split('/');");
            html.AppendLine("    let id = rest.join('/');");
            html.AppendLine("    if (cat !== currentCat) { currentCat = cat; lines.push('## ' + cat.toUpperCase()); }");
            html.AppendLine("    let note = data.note ? data.note.trim() : '(no description)';");
            html.AppendLine("    lines.push('- **' + id + '**: ' + note);");
            html.AppendLine("  });");
            html.AppendLine("  lines.push('', '---', 'Total: ' + flagged.length + ' issues');");
            html.AppendLine("  let text = lines.join('\\n');");
            html.AppendLine("  navigator.clipboard.writeText(text).then(() => showToast('Copied ' + flagged.length + ' issues to clipboard'));");
            html.AppendLine("}");
            // Clear all flags
            html.AppendLine("function clearAllFlags() {");
            html.AppendLine("  if (!confirm('Clear all flagged issues?')) return;");
            html.AppendLine("  issues = {};");
            html.AppendLine("  saveIssues();");
            html.AppendLine("  allCards.forEach(c => { c.classList.remove('flagged'); c.querySelector('input[type=checkbox]').checked = false; c.querySelector('textarea').value = ''; });");
            html.AppendLine("  updateIssueCount();");
            html.AppendLine("}");
            // Toast notification
            html.AppendLine("function showToast(msg) {");
            html.AppendLine("  let t = document.getElementById('toast'); t.textContent = msg; t.style.display = 'block';");
            html.AppendLine("  setTimeout(() => t.style.display = 'none', 3000);");
            html.AppendLine("}");
            // Text filter
            html.AppendLine("function filterRooms(text) {");
            html.AppendLine("  text = text.toLowerCase();");
            html.AppendLine("  allCards.forEach(c => {");
            html.AppendLine("    let match = c.dataset.name.toLowerCase().includes(text);");
            html.AppendLine("    if (flaggedOnly && !c.classList.contains('flagged')) match = false;");
            html.AppendLine("    c.style.display = match ? '' : 'none';");
            html.AppendLine("  });");
            html.AppendLine("}");
            // Lightbox
            html.AppendLine("function openLightbox(src, title, cat) {");
            html.AppendLine("  document.getElementById('lb-img').src = src;");
            html.AppendLine("  document.getElementById('lb-title').textContent = title;");
            html.AppendLine("  document.getElementById('lb-cat').textContent = cat;");
            html.AppendLine("  document.getElementById('lightbox').classList.add('active');");
            html.AppendLine("  currentIdx = allCards.findIndex(c => c.dataset.name === cat + '/' + title);");
            html.AppendLine("}");
            html.AppendLine("function closeLightbox() { document.getElementById('lightbox').classList.remove('active'); }");
            html.AppendLine("document.addEventListener('keydown', e => {");
            html.AppendLine("  if (!document.getElementById('lightbox').classList.contains('active')) return;");
            html.AppendLine("  if (e.key === 'Escape') closeLightbox();");
            html.AppendLine("  if (e.key === 'ArrowRight' || e.key === 'ArrowDown') { navigateLB(1); e.preventDefault(); }");
            html.AppendLine("  if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') { navigateLB(-1); e.preventDefault(); }");
            html.AppendLine("});");
            html.AppendLine("function navigateLB(dir) {");
            html.AppendLine("  if (allCards.length === 0) return;");
            html.AppendLine("  currentIdx = (currentIdx + dir + allCards.length) % allCards.length;");
            html.AppendLine("  let c = allCards[currentIdx];");
            html.AppendLine("  let name = c.dataset.name;");
            html.AppendLine("  let slashIdx = name.indexOf('/');");
            html.AppendLine("  let cat = name.substring(0, slashIdx);");
            html.AppendLine("  let id = name.substring(slashIdx + 1);");
            html.AppendLine("  let displayEl = c.querySelector('.id');");
            html.AppendLine("  let displayName = displayEl ? displayEl.textContent : id;");
            html.AppendLine("  openLightbox(name + '.png', displayName, cat);");
            html.AppendLine("}");
            html.AppendLine("</script>");

            html.AppendLine("<div class='toast' id='toast'></div>");
            html.AppendLine("</body></html>");

            using var file = FileAccess.Open("user://room_catalog/index.html", FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(html.ToString());
                GD.Print($"[RoomCatalog] Gallery saved to {_outputDir}/index.html");
            }
        }
    }
}
