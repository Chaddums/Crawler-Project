"""
Image diff engine for visual regression testing.

Uses PIL and numpy to compute per-pixel differences between a baseline
screenshot and the current screenshot, then generates a highlighted diff
image for human review.
"""

from __future__ import annotations

import os
from typing import Optional

import numpy as np
from PIL import Image

# ---------------------------------------------------------------------------
# Pre-defined regions of interest (x, y, w, h) at 1920x1080
# ---------------------------------------------------------------------------

REGIONS: dict[str, tuple[int, int, int, int]] = {
    "hud_top": (0, 0, 1920, 50),
    "hud_bottom": (0, 970, 1920, 110),
    "viewport_3d": (0, 50, 1920, 920),
}

# ---------------------------------------------------------------------------
# Pixel distance threshold
# ---------------------------------------------------------------------------
# Two pixels are considered "changed" when their Euclidean distance in
# normalised RGB space exceeds this value (on a 0-255 scale).
_PIXEL_DISTANCE_THRESHOLD = 15


class VisualCompare:
    """Compare two screenshots and produce a diff visualisation."""

    def __init__(self, threshold: float = 0.02):
        """
        Parameters
        ----------
        threshold : float
            Maximum fraction of pixels that may differ before the
            comparison is considered a failure.  0.02 = 2 %.
        """
        self.threshold = threshold

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def compare(
        self,
        baseline_path: str,
        current_path: str,
        diff_path: Optional[str] = None,
    ) -> tuple[bool, float, Optional[str]]:
        """Compare two full images.

        Returns
        -------
        (passed, diff_percent, diff_image_path)
            *passed* is True when the fraction of changed pixels is within
            the threshold.  *diff_percent* is 0-100.  *diff_image_path* is
            the written diff image (or None if *diff_path* was not given).
        """
        baseline = self._load(baseline_path)
        current = self._load(current_path)

        if baseline.shape != current.shape:
            # Size mismatch is always a failure.
            return False, 100.0, None

        changed_mask = self._pixel_diff_mask(baseline, current)
        diff_frac = float(changed_mask.sum()) / changed_mask.size
        diff_percent = round(diff_frac * 100.0, 4)
        passed = diff_frac <= self.threshold

        written_path: Optional[str] = None
        if diff_path is not None:
            written_path = self._write_diff(baseline, current, changed_mask, diff_path)

        return passed, diff_percent, written_path

    def compare_region(
        self,
        baseline_path: str,
        current_path: str,
        region: tuple[int, int, int, int],
        diff_path: Optional[str] = None,
    ) -> tuple[bool, float, Optional[str]]:
        """Compare a specific rectangular region (x, y, w, h)."""
        baseline = self._load(baseline_path)
        current = self._load(current_path)

        x, y, w, h = region
        baseline_crop = baseline[y : y + h, x : x + w]
        current_crop = current[y : y + h, x : x + w]

        if baseline_crop.shape != current_crop.shape:
            return False, 100.0, None

        changed_mask = self._pixel_diff_mask(baseline_crop, current_crop)
        diff_frac = float(changed_mask.sum()) / changed_mask.size
        diff_percent = round(diff_frac * 100.0, 4)
        passed = diff_frac <= self.threshold

        written_path: Optional[str] = None
        if diff_path is not None:
            written_path = self._write_diff(baseline_crop, current_crop, changed_mask, diff_path)

        return passed, diff_percent, written_path

    def color_histogram(self, image_path: str, bins: int = 32) -> dict[str, list[int]]:
        """Return per-channel RGB histograms for palette health checks.

        Returns
        -------
        dict with keys "r", "g", "b", each mapping to a list of *bins*
        integer counts.
        """
        img = self._load(image_path)
        result: dict[str, list[int]] = {}
        for idx, channel in enumerate(("r", "g", "b")):
            counts, _ = np.histogram(img[:, :, idx], bins=bins, range=(0, 256))
            result[channel] = counts.tolist()
        return result

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _load(path: str) -> np.ndarray:
        """Load an image as an (H, W, 3) uint8 numpy array."""
        img = Image.open(path).convert("RGB")
        return np.asarray(img, dtype=np.uint8)

    @staticmethod
    def _pixel_diff_mask(a: np.ndarray, b: np.ndarray) -> np.ndarray:
        """Return a boolean (H, W) mask where True = pixel has changed."""
        diff = a.astype(np.float32) - b.astype(np.float32)
        dist = np.sqrt(np.sum(diff ** 2, axis=2))
        return dist > _PIXEL_DISTANCE_THRESHOLD

    @staticmethod
    def _write_diff(
        baseline: np.ndarray,
        current: np.ndarray,
        mask: np.ndarray,
        out_path: str,
    ) -> str:
        """Create and save a diff visualisation image.

        Changed pixels are shown in bright red; unchanged pixels are
        dimmed versions of the current screenshot.
        """
        dimmed = (current.astype(np.float32) * 0.3).astype(np.uint8)
        canvas = dimmed.copy()

        # Paint changed pixels red.
        canvas[mask, 0] = 255
        canvas[mask, 1] = 0
        canvas[mask, 2] = 0

        os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
        Image.fromarray(canvas).save(out_path)
        return out_path
