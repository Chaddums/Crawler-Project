"""
Last Known Good (LKG) database management.

An LKG entry records a Git commit hash, timestamp, and per-suite pass
counts that met the promotion criteria.  The runner promotes a build to
LKG when all suites satisfy their quality gates.
"""

from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from typing import Any, Optional

from config import LKG_DIR

_DB_PATH = os.path.join(LKG_DIR, "lkg_database.json")

# ---------------------------------------------------------------------------
# Promotion criteria
# ---------------------------------------------------------------------------
# Each suite specifies either an exact required pass rate (1.0 = 100 %) or a
# maximum number of allowed failures.

PROMOTION_CRITERIA: dict[str, dict[str, Any]] = {
    "content": {"pass_rate": 1.0},
    "ui": {"pass_rate": 1.0},
    "gameplay": {"pass_rate": 1.0},
    "visual": {
        # Tier 1 (headless): 100 % pass.
        # Tier 2 (opengl3 headless): allow up to 2 failures for GPU variance.
        "pass_rate": 1.0,
        "max_failures_tier2": 2,
    },
    "integration": {"max_failures": 1},
}

# ---------------------------------------------------------------------------
# Database helpers
# ---------------------------------------------------------------------------


def _empty_db() -> dict[str, Any]:
    return {"entries": [], "current_index": -1}


def load_lkg() -> dict[str, Any]:
    """Read the LKG database from disk (or return an empty one)."""
    if not os.path.isfile(_DB_PATH):
        return _empty_db()
    with open(_DB_PATH, "r", encoding="utf-8") as fh:
        data: dict[str, Any] = json.load(fh)
    return data


def save_lkg(data: dict[str, Any]) -> None:
    """Persist the LKG database."""
    os.makedirs(os.path.dirname(_DB_PATH), exist_ok=True)
    with open(_DB_PATH, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2)


def get_current_lkg() -> Optional[dict[str, Any]]:
    """Return the latest promoted build entry, or None."""
    db = load_lkg()
    entries = db.get("entries", [])
    idx = db.get("current_index", -1)
    if idx < 0 or idx >= len(entries):
        return None
    return entries[idx]


# ---------------------------------------------------------------------------
# Promotion logic
# ---------------------------------------------------------------------------


def check_promotion(results: dict[str, Any]) -> tuple[bool, list[str]]:
    """Check whether *results* satisfy all promotion criteria.

    Parameters
    ----------
    results : dict
        Mapping of suite name to a dict with at least ``passed`` (int),
        ``failed`` (int), and ``total`` (int) keys.  Visual suites may
        also contain ``tier`` ("tier1" | "tier2").

    Returns
    -------
    (can_promote, reasons)
        *can_promote* is True when every suite passes its gate.
        *reasons* lists human-readable explanations for each failing gate.
    """
    reasons: list[str] = []

    for suite, criteria in PROMOTION_CRITERIA.items():
        suite_data = results.get(suite)
        if suite_data is None:
            # Suite was not run -- skip (partial runs cannot promote).
            reasons.append(f"{suite}: not executed")
            continue

        total = suite_data.get("total", 0)
        failed = suite_data.get("failed", 0)
        passed = suite_data.get("passed", 0)

        if total == 0:
            reasons.append(f"{suite}: no tests found")
            continue

        # -- max_failures gate (integration, visual tier2) -----------------
        max_fail = criteria.get("max_failures")
        if suite == "visual" and suite_data.get("tier") == "tier2":
            max_fail = criteria.get("max_failures_tier2", max_fail)

        if max_fail is not None:
            if failed > max_fail:
                reasons.append(
                    f"{suite}: {failed} failure(s) exceed max allowed ({max_fail})"
                )
            continue  # max_failures is the only gate for this suite

        # -- pass_rate gate ------------------------------------------------
        required = criteria.get("pass_rate", 1.0)
        actual = passed / total
        if actual < required:
            reasons.append(
                f"{suite}: pass rate {actual:.1%} < required {required:.1%}"
            )

    return (len(reasons) == 0, reasons)


def promote(git_hash: str, results: dict[str, Any]) -> dict[str, Any]:
    """Record *results* as a new LKG entry.

    Returns the newly created entry.
    """
    entry: dict[str, Any] = {
        "git_hash": git_hash,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "suites": {},
    }
    for suite_name, suite_data in results.items():
        entry["suites"][suite_name] = {
            "passed": suite_data.get("passed", 0),
            "failed": suite_data.get("failed", 0),
            "total": suite_data.get("total", 0),
        }

    db = load_lkg()
    db["entries"].append(entry)
    db["current_index"] = len(db["entries"]) - 1
    save_lkg(db)
    return entry
