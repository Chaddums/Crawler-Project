"""
Junkbot Arena — String & Dialogue Editor
Serves a web UI for editing Data/strings.json.

Usage:
    python string_editor.py
    Then open http://localhost:8080

No pip dependencies required — stdlib only.
"""

import http.server
import json
import os
import shutil
import socketserver
from datetime import datetime
from pathlib import Path
from urllib.parse import urlparse

PORT = 8080
SCRIPT_DIR = Path(__file__).parent.resolve()
PROJECT_DIR = SCRIPT_DIR.parent
STRINGS_FILE = PROJECT_DIR / "Data" / "strings.json"
BACKUP_DIR = PROJECT_DIR / "Data" / "backups"
EDITOR_HTML = SCRIPT_DIR / "editor.html"
MAX_BACKUPS = 20


def load_strings():
    with open(STRINGS_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


def save_strings(data):
    BACKUP_DIR.mkdir(parents=True, exist_ok=True)

    # Create timestamped backup
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_path = BACKUP_DIR / f"strings_{timestamp}.json"
    if STRINGS_FILE.exists():
        shutil.copy2(STRINGS_FILE, backup_path)

    # Prune old backups
    backups = sorted(BACKUP_DIR.glob("strings_*.json"))
    while len(backups) > MAX_BACKUPS:
        backups.pop(0).unlink()

    # Write updated file
    with open(STRINGS_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


def scan_usage():
    """Scan .cs files for StringLoader references to build usage metadata."""
    usage = {}
    scripts_dir = PROJECT_DIR / "Scripts"
    if not scripts_dir.exists():
        return usage

    for cs_file in scripts_dir.rglob("*.cs"):
        try:
            content = cs_file.read_text(encoding="utf-8")
        except Exception:
            continue

        rel_path = cs_file.relative_to(PROJECT_DIR).as_posix()

        # Find StringLoader.Get("...") and StringLoader.GetArray("...") calls
        import re
        for match in re.finditer(r'StringLoader\.(?:Get|GetArray|GetRandom)\(\s*"([^"]+)"', content):
            key = match.group(1)
            if key not in usage:
                usage[key] = []
            if rel_path not in usage[key]:
                usage[key].append(rel_path)

    return usage


class EditorHandler(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        parsed = urlparse(self.path)

        if parsed.path == "/" or parsed.path == "/index.html":
            self.serve_file(EDITOR_HTML, "text/html")
        elif parsed.path == "/api/strings":
            self.send_json(load_strings())
        elif parsed.path == "/api/meta":
            self.send_json({"usage": scan_usage()})
        elif parsed.path == "/favicon.ico":
            self.send_response(204)
            self.end_headers()
        else:
            self.send_error(404)

    def do_POST(self):
        parsed = urlparse(self.path)

        if parsed.path == "/api/strings":
            length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(length)
            try:
                data = json.loads(body.decode("utf-8"))
                save_strings(data)
                self.send_json({"ok": True, "message": "Saved successfully."})
            except Exception as e:
                self.send_json({"ok": False, "message": str(e)}, status=400)
        else:
            self.send_error(404)

    def serve_file(self, filepath, content_type):
        try:
            content = filepath.read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", f"{content_type}; charset=utf-8")
            self.send_header("Content-Length", len(content))
            self.end_headers()
            self.wfile.write(content)
        except FileNotFoundError:
            self.send_error(404, f"File not found: {filepath.name}")

    def send_json(self, data, status=200):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", len(body))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        # Quieter logging — suppress /api/ request spam
        try:
            if args and isinstance(args[0], str) and "/api/" in args[0]:
                return
        except TypeError:
            pass
        super().log_message(format, *args)


if __name__ == "__main__":
    if not STRINGS_FILE.exists():
        print(f"ERROR: strings.json not found at {STRINGS_FILE}")
        print("Make sure you're running from the Godot/Tools directory.")
        input("Press Enter to exit...")
        exit(1)

    print(f"Junkbot Arena String Editor")
    print(f"Loading: {STRINGS_FILE}")
    print(f"Server:  http://localhost:{PORT}")
    print(f"Press Ctrl+C to stop.\n")

    import webbrowser
    webbrowser.open(f"http://localhost:{PORT}")

    with socketserver.TCPServer(("", PORT), EditorHandler) as httpd:
        httpd.allow_reuse_address = True
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nShutting down.")
