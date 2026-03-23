"""
Configuration for the Vine Logic TD test runner.
Handles paths, timeouts, Godot binary detection, and render modes.
"""

import os
import shutil
import sys

# ---------------------------------------------------------------------------
# Path helpers
# ---------------------------------------------------------------------------

# This file lives in tools/test-runner/ relative to the Godot_TD project root.
_THIS_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_PATH = os.path.normpath(os.path.join(_THIS_DIR, "..", ".."))

RESULTS_DIR = os.path.join(PROJECT_PATH, "test-reports", "results")
SCREENSHOTS_DIR = os.path.join(PROJECT_PATH, "test-reports", "screenshots")
VISUAL_DIR = os.path.join(PROJECT_PATH, "test-reports", "visual")
LKG_DIR = os.path.join(PROJECT_PATH, "test-reports", "lkg")

# Baselines live inside the visual directory.
VISUAL_BASELINES_DIR = os.path.join(VISUAL_DIR, "baselines")
VISUAL_CURRENT_DIR = os.path.join(VISUAL_DIR, "current")
VISUAL_DIFFS_DIR = os.path.join(VISUAL_DIR, "diffs")

# ---------------------------------------------------------------------------
# Godot binary detection
# ---------------------------------------------------------------------------

_CANDIDATE_NAMES = ["godot", "godot4", "Godot_v4"]

_CANDIDATE_PATHS_WINDOWS = [
    r"C:\Program Files\Godot\godot.exe",
    r"C:\Program Files (x86)\Godot\godot.exe",
    r"C:\Program Files\Godot\Godot_v4.exe",
    r"C:\Godot\godot.exe",
    os.path.expanduser(r"~\scoop\apps\godot-mono\current\godot.exe"),
    os.path.expanduser(r"~\scoop\apps\godot\current\godot.exe"),
]

_CANDIDATE_PATHS_UNIX = [
    "/usr/local/bin/godot4",
    "/usr/bin/godot4",
    "/usr/local/bin/godot",
    "/usr/bin/godot",
    "/snap/bin/godot",
    os.path.expanduser("~/Godot/godot"),
]


def _detect_godot_binary() -> str:
    """Return the path to a usable Godot 4 binary, or raise RuntimeError."""
    # 1. Environment variable override
    env = os.environ.get("GODOT_BINARY")
    if env and (os.path.isfile(env) or shutil.which(env)):
        return env

    # 2. Check PATH for common command names
    for name in _CANDIDATE_NAMES:
        found = shutil.which(name)
        if found:
            return found

    # 3. Platform-specific well-known locations
    candidates = _CANDIDATE_PATHS_WINDOWS if sys.platform == "win32" else _CANDIDATE_PATHS_UNIX
    for path in candidates:
        if os.path.isfile(path):
            return path

    raise RuntimeError(
        "Could not locate a Godot 4 binary. "
        "Set the GODOT_BINARY environment variable or ensure 'godot' / 'godot4' is on your PATH."
    )


try:
    GODOT_BINARY: str = _detect_godot_binary()
except RuntimeError:
    # Allow imports to succeed even when Godot is not installed (e.g. for
    # report-only mode).  Runner will re-raise when it actually needs to
    # launch a process.
    GODOT_BINARY = ""

# ---------------------------------------------------------------------------
# Timeouts (seconds)
# ---------------------------------------------------------------------------

TIMEOUTS: dict[str, int] = {
    "content": 30,
    "ui": 60,
    "gameplay": 180,
    "map-validation": 300,
    "visual": 180,
    "integration": 300,
    "all": 1200,
}

# ---------------------------------------------------------------------------
# Render modes
# ---------------------------------------------------------------------------
# Each suite maps to the CLI flags Godot should be launched with.
#   - content / ui / gameplay: headless (no rendering needed)
#   - visual: Tier 2 uses opengl3 + headless display driver so the GPU
#     rasterises frames without a window.  Tier 1 (fallback) uses --headless.
#   - integration: rendered (no special flags; needs a display)

RENDER_MODES: dict[str, list[str]] = {
    "content": ["--headless"],
    "ui": ["--headless"],
    "gameplay": ["--headless"],
    "visual": ["--rendering-driver", "opengl3", "--display-driver", "headless"],
    "visual_headless": ["--headless"],  # Tier 1 fallback
    "integration": [],  # rendered — no extra flags
}

# ---------------------------------------------------------------------------
# Ensure output directories exist
# ---------------------------------------------------------------------------


def ensure_dirs() -> None:
    """Create all output directories if they don't already exist."""
    for d in (
        RESULTS_DIR,
        SCREENSHOTS_DIR,
        VISUAL_DIR,
        VISUAL_BASELINES_DIR,
        VISUAL_CURRENT_DIR,
        VISUAL_DIFFS_DIR,
        LKG_DIR,
    ):
        os.makedirs(d, exist_ok=True)
