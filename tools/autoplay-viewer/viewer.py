#!/usr/bin/env python3
"""
Simple HTTP server that serves the latest autoplay screenshot with auto-refresh.
Access from your phone browser at http://<your-pc-ip>:8090

Usage: python viewer.py [--port 8090] [--dir <screenshot_dir>]
"""

import argparse
import os
import sys
import time
from http.server import HTTPServer, SimpleHTTPRequestHandler
from pathlib import Path

# Default Godot user data paths by platform
def get_default_screenshot_dir():
    if sys.platform == "win32":
        return os.path.expandvars(r"%APPDATA%\Godot\app_userdata\Junkbot Arena\autoplay_screenshots")
    elif sys.platform == "linux":
        # WSL can access Windows appdata
        user = os.environ.get("USER", "")
        wsl_path = f"/mnt/c/Users/{user}/AppData/Roaming/Godot/app_userdata/Junkbot Arena/autoplay_screenshots"
        if os.path.exists(wsl_path):
            return wsl_path
        return os.path.expanduser("~/.local/share/godot/app_userdata/Junkbot Arena/autoplay_screenshots")
    return "."

SCREENSHOT_DIR = None

HTML_PAGE = """<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
<title>Junkbot Arena - AutoPlay Viewer</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    background: #0a0a0f;
    color: #e0d8c0;
    font-family: monospace;
    display: flex;
    flex-direction: column;
    align-items: center;
    min-height: 100vh;
    overflow-x: hidden;
  }
  h1 {
    color: #e6cc66;
    font-size: 1.2em;
    padding: 10px;
    text-align: center;
  }
  #status {
    color: #888;
    font-size: 0.8em;
    padding: 4px;
  }
  #screenshot {
    max-width: 100%;
    max-height: 85vh;
    border: 1px solid #333;
    image-rendering: auto;
  }
  .controls {
    display: flex;
    gap: 10px;
    padding: 8px;
    flex-wrap: wrap;
    justify-content: center;
  }
  button {
    background: #1a1a2e;
    color: #e6cc66;
    border: 1px solid #e6cc66;
    padding: 8px 16px;
    font-family: monospace;
    font-size: 0.9em;
    cursor: pointer;
    border-radius: 4px;
  }
  button:active { background: #2a2a3e; }
  button.active { background: #3a3520; }
  .gallery {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    padding: 8px;
    justify-content: center;
    max-width: 100%;
  }
  .gallery img {
    width: 120px;
    height: auto;
    border: 1px solid #333;
    cursor: pointer;
  }
  .gallery img:hover { border-color: #e6cc66; }
</style>
</head>
<body>
<h1>Junkbot Arena — AutoPlay Viewer</h1>
<div id="status">Connecting...</div>
<img id="screenshot" src="/latest.png" alt="Game Screenshot">
<div class="controls">
  <button id="btnLive" class="active" onclick="setMode('live')">LIVE</button>
  <button id="btnGallery" onclick="setMode('gallery')">GALLERY</button>
  <button onclick="location.reload()">REFRESH</button>
</div>
<div id="gallery" class="gallery" style="display:none;"></div>

<script>
let mode = 'live';
let refreshInterval = null;
const img = document.getElementById('screenshot');
const status = document.getElementById('status');
const gallery = document.getElementById('gallery');

function setMode(m) {
  mode = m;
  document.getElementById('btnLive').className = m === 'live' ? 'active' : '';
  document.getElementById('btnGallery').className = m === 'gallery' ? 'active' : '';
  img.style.display = m === 'live' ? 'block' : 'none';
  gallery.style.display = m === 'gallery' ? 'flex' : 'none';
  if (m === 'gallery') loadGallery();
}

function refreshImage() {
  if (mode !== 'live') return;
  const t = Date.now();
  const newImg = new Image();
  newImg.onload = function() {
    img.src = this.src;
    status.textContent = 'Live — ' + new Date().toLocaleTimeString();
  };
  newImg.onerror = function() {
    status.textContent = 'Waiting for screenshots...';
  };
  newImg.src = '/latest.png?t=' + t;
}

function loadGallery() {
  fetch('/list')
    .then(r => r.json())
    .then(files => {
      gallery.innerHTML = '';
      files.forEach(f => {
        const thumb = document.createElement('img');
        thumb.src = '/' + f + '?t=' + Date.now();
        thumb.onclick = () => {
          img.src = thumb.src;
          setMode('live');
          clearInterval(refreshInterval);
          status.textContent = 'Paused — viewing ' + f;
        };
        gallery.appendChild(thumb);
      });
    })
    .catch(() => { gallery.innerHTML = '<p>No screenshots yet</p>'; });
}

// Auto-refresh every 2 seconds
refreshInterval = setInterval(refreshImage, 2000);
refreshImage();
</script>
</body>
</html>"""


class ViewerHandler(SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/" or self.path == "/index.html":
            self.send_response(200)
            self.send_header("Content-Type", "text/html")
            self.end_headers()
            self.wfile.write(HTML_PAGE.encode())
            return

        if self.path == "/list":
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            files = sorted(
                [f for f in os.listdir(SCREENSHOT_DIR) if f.startswith("frame_") and f.endswith(".png")],
                reverse=True
            )
            import json
            self.wfile.write(json.dumps(files).encode())
            return

        # Serve screenshot files
        clean_path = self.path.split("?")[0].lstrip("/")
        if clean_path.endswith(".png"):
            filepath = os.path.join(SCREENSHOT_DIR, clean_path)
            if os.path.exists(filepath):
                self.send_response(200)
                self.send_header("Content-Type", "image/png")
                self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
                self.end_headers()
                with open(filepath, "rb") as f:
                    self.wfile.write(f.read())
                return

        self.send_response(404)
        self.end_headers()

    def log_message(self, format, *args):
        # Suppress request logging to keep console clean
        pass


def main():
    global SCREENSHOT_DIR

    parser = argparse.ArgumentParser(description="AutoPlay screenshot viewer")
    parser.add_argument("--port", type=int, default=8090)
    parser.add_argument("--dir", type=str, default=None, help="Screenshot directory path")
    args = parser.parse_args()

    SCREENSHOT_DIR = args.dir or get_default_screenshot_dir()

    # Create dir if it doesn't exist yet (game may not have started)
    os.makedirs(SCREENSHOT_DIR, exist_ok=True)

    print(f"AutoPlay Viewer")
    print(f"  Screenshot dir: {SCREENSHOT_DIR}")
    print(f"  Server: http://0.0.0.0:{args.port}")
    print(f"  Open on your phone: http://<your-pc-ip>:{args.port}")
    print()

    # Show local IP for convenience
    try:
        import socket
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        local_ip = s.getsockname()[0]
        s.close()
        print(f"  Detected LAN IP: http://{local_ip}:{args.port}")
    except Exception:
        pass

    print()
    server = HTTPServer(("0.0.0.0", args.port), ViewerHandler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")


if __name__ == "__main__":
    main()
