"""
Flaky test detection and tracking.

Maintains a rolling window of pass/fail results per test name so the
runner can decide whether to retry and the reporter can flag unreliable
tests.
"""

from __future__ import annotations

import json
import os
from typing import Any

from config import LKG_DIR

_DB_PATH = os.path.join(LKG_DIR, "flaky_db.json")
_WINDOW = 10  # number of recent results to keep per test


def _load_db() -> dict[str, list[bool]]:
    """Load the flaky database, returning an empty dict on first run."""
    if not os.path.isfile(_DB_PATH):
        return {}
    with open(_DB_PATH, "r", encoding="utf-8") as fh:
        raw: dict[str, list[bool]] = json.load(fh)
    return raw


def _save_db(db: dict[str, list[bool]]) -> None:
    os.makedirs(os.path.dirname(_DB_PATH), exist_ok=True)
    with open(_DB_PATH, "w", encoding="utf-8") as fh:
        json.dump(db, fh, indent=2)


def record_result(test_name: str, passed: bool) -> None:
    """Append a pass/fail result for *test_name*, trimming to the window."""
    db = _load_db()
    history = db.get(test_name, [])
    history.append(passed)
    db[test_name] = history[-_WINDOW:]
    _save_db(db)


def is_flaky(test_name: str) -> bool:
    """Return True if *test_name* has both passes and failures in the window."""
    db = _load_db()
    history = db.get(test_name, [])
    if len(history) < 2:
        return False
    return (True in history) and (False in history)


def get_flaky_tests() -> list[str]:
    """Return a list of all currently flaky test names."""
    db = _load_db()
    return [name for name in db if is_flaky(name)]


def should_retry(test_name: str) -> bool:
    """Return True if the test is flaky but passes often enough to be worth
    retrying (pass rate > 30 %)."""
    db = _load_db()
    history = db.get(test_name, [])
    if len(history) < 2:
        return False
    if not is_flaky(test_name):
        return False
    pass_rate = sum(1 for h in history if h) / len(history)
    return pass_rate > 0.30


def pass_rate(test_name: str) -> float:
    """Return the pass rate (0.0 - 1.0) for a test, or 1.0 if unknown."""
    db = _load_db()
    history = db.get(test_name, [])
    if not history:
        return 1.0
    return sum(1 for h in history if h) / len(history)
