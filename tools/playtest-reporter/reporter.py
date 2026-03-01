"""
Junkbot Arena — Playtest Bug/Feature Reporter

Global hotkeys:
  F9  → Screenshot + Bug Report dialog
  F10 → Screenshot + Feature Request dialog

Reports saved to <project>/test-reports/bugs/ or test-reports/features/
"""

import os
import sys
import threading
from datetime import datetime
from pathlib import Path
from io import BytesIO

import keyboard
import mss
import mss.tools
from PIL import Image, ImageTk
import tkinter as tk
from tkinter import scrolledtext

# Project root is two levels up from this script
PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent
REPORTS_DIR = PROJECT_ROOT / "test-reports"


def capture_screenshot() -> Image.Image:
    """Capture the entire primary monitor and return as a PIL Image."""
    with mss.mss() as sct:
        monitor = sct.monitors[1]  # 1 = primary monitor
        raw = sct.grab(monitor)
        img = Image.frombytes("RGB", raw.size, raw.bgra, "raw", "BGRX")
    return img


def save_report(report_type: str, title: str, description: str, screenshot: Image.Image):
    """Save screenshot + report.md into a timestamped folder."""
    timestamp = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
    subfolder = "bugs" if report_type == "Bug" else "features"
    report_dir = REPORTS_DIR / subfolder / timestamp
    report_dir.mkdir(parents=True, exist_ok=True)

    # Save screenshot
    screenshot.save(report_dir / "screenshot.png", "PNG")

    # Write report markdown
    report_title = title.strip() if title.strip() else timestamp
    readable_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    md = (
        f"# {report_type} Report: {report_title}\n"
        f"**Date:** {readable_time}\n"
        f"**Type:** {report_type}\n"
        f"\n"
        f"## Description\n"
        f"{description.strip()}\n"
    )
    (report_dir / "report.md").write_text(md, encoding="utf-8")

    print(f"  Saved {report_type.lower()} report → {report_dir.relative_to(PROJECT_ROOT)}")


class ReportDialog:
    """Tkinter dialog for entering a bug/feature report."""

    def __init__(self, report_type: str, screenshot: Image.Image):
        self.report_type = report_type
        self.screenshot = screenshot
        self.submitted = False

        self.root = tk.Tk()
        self.root.title(f"{report_type} Report — Junkbot Arena")
        self.root.attributes("-topmost", True)
        self.root.resizable(False, False)
        self.root.protocol("WM_DELETE_WINDOW", self._cancel)
        self.root.bind("<Escape>", lambda e: self._cancel())

        # Colors
        if report_type == "Bug":
            header_bg = "#c0392b"
            header_fg = "#ffffff"
        else:
            header_bg = "#2980b9"
            header_fg = "#ffffff"

        # Header
        header = tk.Frame(self.root, bg=header_bg, padx=12, pady=8)
        header.pack(fill=tk.X)
        tk.Label(
            header, text=f"{report_type} Report", font=("Segoe UI", 16, "bold"),
            bg=header_bg, fg=header_fg
        ).pack(anchor=tk.W)

        body = tk.Frame(self.root, padx=12, pady=8)
        body.pack(fill=tk.BOTH, expand=True)

        # Screenshot thumbnail
        thumb = self.screenshot.copy()
        thumb.thumbnail((400, 225))
        self._photo = ImageTk.PhotoImage(thumb)
        tk.Label(body, image=self._photo, borderwidth=1, relief=tk.SUNKEN).pack(pady=(0, 8))

        # Title
        tk.Label(body, text="Title (optional):", font=("Segoe UI", 10), anchor=tk.W).pack(fill=tk.X)
        self.title_entry = tk.Entry(body, font=("Segoe UI", 10))
        self.title_entry.pack(fill=tk.X, pady=(0, 6))

        # Description
        tk.Label(body, text="Description:", font=("Segoe UI", 10), anchor=tk.W).pack(fill=tk.X)
        self.desc_text = scrolledtext.ScrolledText(body, font=("Segoe UI", 10), height=6, wrap=tk.WORD)
        self.desc_text.pack(fill=tk.BOTH, expand=True, pady=(0, 8))

        # Buttons
        btn_frame = tk.Frame(body)
        btn_frame.pack(fill=tk.X)
        tk.Button(
            btn_frame, text="Submit", font=("Segoe UI", 10, "bold"),
            bg=header_bg, fg=header_fg, padx=16, pady=4, command=self._submit
        ).pack(side=tk.RIGHT, padx=(4, 0))
        tk.Button(
            btn_frame, text="Cancel", font=("Segoe UI", 10),
            padx=16, pady=4, command=self._cancel
        ).pack(side=tk.RIGHT)

        # Center on screen
        self.root.update_idletasks()
        w = self.root.winfo_width()
        h = self.root.winfo_height()
        x = (self.root.winfo_screenwidth() - w) // 2
        y = (self.root.winfo_screenheight() - h) // 2
        self.root.geometry(f"+{x}+{y}")

        # Focus the description field
        self.desc_text.focus_set()

    def _submit(self):
        description = self.desc_text.get("1.0", tk.END).strip()
        if not description:
            self.desc_text.focus_set()
            return
        title = self.title_entry.get()
        save_report(self.report_type, title, description, self.screenshot)
        self.submitted = True
        self.root.destroy()

    def _cancel(self):
        self.root.destroy()

    def run(self):
        self.root.mainloop()


def open_report_dialog(report_type: str):
    """Capture screenshot and open the report dialog on the main thread."""
    print(f"  Capturing screenshot for {report_type.lower()} report...")
    screenshot = capture_screenshot()

    dialog = ReportDialog(report_type, screenshot)
    dialog.run()

    if not dialog.submitted:
        print("  Cancelled.")


def on_hotkey(report_type: str):
    """Called from the keyboard listener thread — opens dialog on a new thread
    so the hotkey listener isn't blocked."""
    t = threading.Thread(target=open_report_dialog, args=(report_type,), daemon=True)
    t.start()


def main():
    print("=" * 52)
    print("  Junkbot Arena — Playtest Reporter")
    print("=" * 52)
    print()
    print("  Hotkeys:")
    print("    Ctrl+Shift+B → Bug Report (screenshot + dialog)")
    print("    Ctrl+Shift+F → Feature Request (screenshot + dialog)")
    print()
    print(f"  Reports saved to: {REPORTS_DIR.relative_to(PROJECT_ROOT)}/")
    print()
    print("  Press Ctrl+C to quit.")
    print()

    keyboard.add_hotkey("ctrl+shift+b", lambda: on_hotkey("Bug"), suppress=True)
    keyboard.add_hotkey("ctrl+shift+f", lambda: on_hotkey("Feature"), suppress=True)

    try:
        keyboard.wait()  # Block forever until Ctrl+C
    except KeyboardInterrupt:
        print("\n  Reporter stopped. Goodbye!")
        sys.exit(0)


if __name__ == "__main__":
    main()
