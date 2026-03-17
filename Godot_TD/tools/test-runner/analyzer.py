"""
Result analyser for the Vine Logic TD test runner.

Parses the JSON results produced by the Godot test harness, compares
them against the current LKG, and returns a categorised analysis dict
used by the reporter.
"""

from __future__ import annotations

import json
import os
from typing import Any, Optional

from config import RESULTS_DIR, VISUAL_BASELINES_DIR, VISUAL_CURRENT_DIR, VISUAL_DIFFS_DIR
from lkg import get_current_lkg
from visual_compare import VisualCompare


def _load_results(request_id: str) -> dict[str, Any]:
    """Load the JSON results file written by the Godot harness."""
    path = os.path.join(RESULTS_DIR, f"{request_id}.json")
    if not os.path.isfile(path):
        raise FileNotFoundError(f"Results file not found: {path}")
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def _test_key(suite: str, test: dict[str, Any]) -> str:
    """Build a unique key for a test across suites."""
    return f"{suite}::{test.get('name', 'unknown')}"


def analyse(
    request_id: str,
    visual_threshold: float = 0.02,
) -> dict[str, Any]:
    """Run the full analysis pipeline.

    Parameters
    ----------
    request_id : str
        The request ID whose results file we should read.
    visual_threshold : float
        Pixel-diff fraction threshold passed to ``VisualCompare``.

    Returns
    -------
    dict with keys:
        results       -- raw loaded results
        summary       -- per-suite {passed, failed, total}
        failures      -- list of {suite, name, message}
        regressions   -- list of {suite, name, message}  (new failures vs LKG)
        improvements  -- list of {suite, name}            (new passes vs LKG)
        visual_diffs  -- list of {name, diff_percent, baseline, current, diff}
        lkg           -- current LKG entry or None
    """
    results = _load_results(request_id)
    lkg_entry = get_current_lkg()

    # Build a set of test names that passed in LKG.
    lkg_passed: set[str] = set()
    lkg_failed: set[str] = set()
    if lkg_entry is not None:
        for suite_name, suite_info in lkg_entry.get("suites", {}).items():
            for t in suite_info.get("tests_passed", []):
                lkg_passed.add(f"{suite_name}::{t}")
            for t in suite_info.get("tests_failed", []):
                lkg_failed.add(f"{suite_name}::{t}")

    summary: dict[str, dict[str, int]] = {}
    failures: list[dict[str, str]] = []
    regressions: list[dict[str, str]] = []
    improvements: list[dict[str, str]] = []
    visual_diffs: list[dict[str, Any]] = []

    comparator = VisualCompare(threshold=visual_threshold)

    suites = results.get("suites", {})
    for suite_name, suite_data in suites.items():
        tests: list[dict[str, Any]] = suite_data.get("tests", [])
        passed_count = 0
        failed_count = 0

        for test in tests:
            key = _test_key(suite_name, test)
            test_passed = test.get("passed", False)

            if test_passed:
                passed_count += 1
                # Was this previously failing in LKG?
                if key in lkg_failed:
                    improvements.append({"suite": suite_name, "name": test["name"]})
            else:
                failed_count += 1
                msg = test.get("message", "")
                failures.append({
                    "suite": suite_name,
                    "name": test["name"],
                    "message": msg,
                })
                # Was this previously passing in LKG?
                if key in lkg_passed:
                    regressions.append({
                        "suite": suite_name,
                        "name": test["name"],
                        "message": msg,
                    })

            # Visual baseline comparison
            if suite_name == "visual" and test.get("screenshot"):
                screenshot_name = test["screenshot"]
                baseline = os.path.join(VISUAL_BASELINES_DIR, screenshot_name)
                current = os.path.join(VISUAL_CURRENT_DIR, screenshot_name)
                diff_out = os.path.join(
                    VISUAL_DIFFS_DIR,
                    f"diff_{screenshot_name}",
                )
                if os.path.isfile(baseline) and os.path.isfile(current):
                    vis_passed, diff_pct, diff_path = comparator.compare(
                        baseline, current, diff_path=diff_out
                    )
                    visual_diffs.append({
                        "name": test["name"],
                        "diff_percent": diff_pct,
                        "passed": vis_passed,
                        "baseline": baseline,
                        "current": current,
                        "diff": diff_path,
                    })

        summary[suite_name] = {
            "passed": passed_count,
            "failed": failed_count,
            "total": passed_count + failed_count,
        }

    return {
        "results": results,
        "summary": summary,
        "failures": failures,
        "regressions": regressions,
        "improvements": improvements,
        "visual_diffs": visual_diffs,
        "lkg": lkg_entry,
    }
