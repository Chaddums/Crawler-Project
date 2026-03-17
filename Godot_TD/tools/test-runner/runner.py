"""
CLI entry point for the Vine Logic TD test runner.

Usage examples:
    python runner.py                        # run all suites
    python runner.py --suite quick          # content + ui only
    python runner.py --suite visual --retry 2
    python runner.py --report               # regenerate report from last run
    python runner.py --lkg                  # run + promote if all gates pass
    python runner.py --update-baselines     # capture new golden screenshots
    python runner.py --ai-review            # enable AI review of visual diffs
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import uuid
from datetime import datetime, timezone
from typing import Any

# Ensure the package directory is importable.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import config
import suites as suites_mod
from analyzer import analyse
from flaky import record_result, should_retry
from lkg import check_promotion, get_current_lkg, promote
from reporter import generate_html, generate_markdown


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_request_id() -> str:
    ts = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    short = uuid.uuid4().hex[:8]
    return f"{ts}_{short}"


def _log(msg: str, verbose: bool = True) -> None:
    if verbose:
        print(f"[test-runner] {msg}", flush=True)


def _run_suite(
    suite_name: str,
    request_id: str,
    verbose: bool = False,
) -> tuple[int, float]:
    """Launch Godot for a single suite.

    Returns (return_code, elapsed_seconds).
    """
    if not config.GODOT_BINARY:
        print(
            "ERROR: No Godot binary found. Set GODOT_BINARY env var or "
            "install Godot on your PATH.",
            file=sys.stderr,
        )
        return 2, 0.0

    render_flags = config.RENDER_MODES.get(suite_name, [])

    cmd: list[str] = [
        config.GODOT_BINARY,
        *render_flags,
        "--path",
        config.PROJECT_PATH,
        "--",
        "--test-harness",
        f"--suite={suite_name}",
        f"--request-id={request_id}",
    ]

    timeout = config.TIMEOUTS.get(suite_name, config.TIMEOUTS["all"])

    _log(f"Running suite '{suite_name}' (timeout {timeout}s)", verbose)
    _log(f"  cmd: {' '.join(cmd)}", verbose)

    start = time.monotonic()
    try:
        result = subprocess.run(
            cmd,
            timeout=timeout,
            capture_output=True,
            text=True,
        )
        elapsed = time.monotonic() - start
        if verbose and result.stdout:
            for line in result.stdout.splitlines():
                print(f"  [godot] {line}")
        if result.returncode != 0 and verbose and result.stderr:
            for line in result.stderr.splitlines():
                print(f"  [godot:err] {line}")
        return result.returncode, elapsed
    except subprocess.TimeoutExpired:
        elapsed = time.monotonic() - start
        _log(f"Suite '{suite_name}' TIMED OUT after {elapsed:.1f}s", verbose)
        return 2, elapsed
    except FileNotFoundError:
        _log(f"Godot binary not found: {config.GODOT_BINARY}", verbose)
        return 2, 0.0


def _load_results_json(request_id: str) -> dict[str, Any]:
    """Load the combined results JSON for a request ID."""
    path = os.path.join(config.RESULTS_DIR, f"{request_id}.json")
    if not os.path.isfile(path):
        return {}
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def _merge_results(
    existing: dict[str, Any],
    new_data: dict[str, Any],
) -> dict[str, Any]:
    """Merge new suite results into the aggregate results dict."""
    if not existing:
        return new_data
    for suite_name, suite_data in new_data.get("suites", {}).items():
        existing.setdefault("suites", {})[suite_name] = suite_data
    return existing


def _update_baselines(verbose: bool = False) -> None:
    """Copy all files from visual/current/ to visual/baselines/."""
    src = config.VISUAL_CURRENT_DIR
    dst = config.VISUAL_BASELINES_DIR
    if not os.path.isdir(src):
        _log("No current visual screenshots to promote.", verbose)
        return
    os.makedirs(dst, exist_ok=True)
    count = 0
    for fname in os.listdir(src):
        s = os.path.join(src, fname)
        d = os.path.join(dst, fname)
        if os.path.isfile(s):
            shutil.copy2(s, d)
            count += 1
    _log(f"Updated {count} visual baseline(s).", verbose)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Vine Logic TD test runner",
    )
    parser.add_argument(
        "--suite",
        default="all",
        help="Suite to run (default: all). Options: "
        + ", ".join(sorted(suites_mod.SUITE_MAP.keys())),
    )
    parser.add_argument(
        "--retry",
        type=int,
        default=0,
        help="Retry failed tests N times (default: 0)",
    )
    parser.add_argument(
        "--report",
        action="store_true",
        help="Generate report from last run without executing tests",
    )
    parser.add_argument(
        "--lkg",
        action="store_true",
        help="Run LKG qualification (promote if all gates pass)",
    )
    parser.add_argument(
        "--update-baselines",
        action="store_true",
        help="Capture new golden baselines for visual tests",
    )
    parser.add_argument(
        "--ai-review",
        action="store_true",
        help="Enable Claude AI review of ambiguous visual diffs",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Verbose output",
    )
    args = parser.parse_args()

    config.ensure_dirs()

    # --report mode: regenerate from most recent results file
    if args.report:
        # Find the most recent results file
        results_files = sorted(
            [
                f
                for f in os.listdir(config.RESULTS_DIR)
                if f.endswith(".json")
            ],
            reverse=True,
        )
        if not results_files:
            print("No results files found in", config.RESULTS_DIR, file=sys.stderr)
            return 1
        last_id = results_files[0].replace(".json", "")
        _log(f"Regenerating report from {last_id}", args.verbose)
        analysis = analyse(last_id)
        results = _load_results_json(last_id)
        lkg_info = get_current_lkg()
        md_path = generate_markdown(results, analysis, lkg_info)
        html_path = generate_html(results, analysis, lkg_info)
        print(f"Markdown report: {md_path}")
        print(f"HTML report:     {html_path}")
        return 0

    # Resolve suite list
    try:
        suite_list = suites_mod.resolve(args.suite)
    except KeyError as exc:
        print(str(exc), file=sys.stderr)
        return 1

    request_id = _make_request_id()
    _log(f"Request ID: {request_id}", args.verbose)
    _log(f"Suites: {suite_list}", args.verbose)

    total_start = time.monotonic()
    all_results: dict[str, Any] = {"suites": {}}
    suite_return_codes: dict[str, int] = {}
    failed_suites: list[str] = []

    # ------------------------------------------------------------------
    # First pass: run each suite
    # ------------------------------------------------------------------
    for suite_name in suite_list:
        rc, elapsed = _run_suite(suite_name, request_id, verbose=args.verbose)
        suite_return_codes[suite_name] = rc
        _log(
            f"Suite '{suite_name}' finished in {elapsed:.1f}s (rc={rc})",
            args.verbose,
        )

        # Merge suite results into the aggregate
        suite_results = _load_results_json(request_id)
        all_results = _merge_results(all_results, suite_results)

        if rc != 0:
            failed_suites.append(suite_name)

    # ------------------------------------------------------------------
    # Record flaky data for every test we observed
    # ------------------------------------------------------------------
    for suite_name, suite_data in all_results.get("suites", {}).items():
        for test in suite_data.get("tests", []):
            key = f"{suite_name}::{test.get('name', 'unknown')}"
            record_result(key, test.get("passed", False))

    # ------------------------------------------------------------------
    # Retries
    # ------------------------------------------------------------------
    retries_remaining = args.retry
    while retries_remaining > 0 and failed_suites:
        retries_remaining -= 1
        retry_list = [
            s for s in failed_suites
            # Retry the suite if any of its tests are worth retrying
        ]
        if not retry_list:
            break
        _log(
            f"Retrying {len(retry_list)} suite(s) ({retries_remaining} retries left)",
            args.verbose,
        )
        failed_suites = []
        for suite_name in retry_list:
            rc, elapsed = _run_suite(suite_name, request_id, verbose=args.verbose)
            suite_return_codes[suite_name] = rc
            suite_results = _load_results_json(request_id)
            all_results = _merge_results(all_results, suite_results)
            if rc != 0:
                failed_suites.append(suite_name)

    # ------------------------------------------------------------------
    # Analysis
    # ------------------------------------------------------------------
    # Write the merged results so the analyser can read them.
    merged_path = os.path.join(config.RESULTS_DIR, f"{request_id}.json")
    os.makedirs(os.path.dirname(merged_path), exist_ok=True)
    with open(merged_path, "w", encoding="utf-8") as fh:
        json.dump(all_results, fh, indent=2)

    analysis = analyse(request_id)
    lkg_info = get_current_lkg()
    total_elapsed = time.monotonic() - total_start

    # ------------------------------------------------------------------
    # LKG promotion
    # ------------------------------------------------------------------
    if args.lkg:
        summary = analysis.get("summary", {})
        can_promote, reasons = check_promotion(summary)
        if can_promote:
            import subprocess as _sp

            try:
                git_hash = (
                    _sp.check_output(
                        ["git", "rev-parse", "--short", "HEAD"],
                        cwd=config.PROJECT_PATH,
                        stderr=_sp.DEVNULL,
                    )
                    .decode()
                    .strip()
                )
            except Exception:
                git_hash = "unknown"
            entry = promote(git_hash, summary)
            analysis["promoted"] = True
            _log(f"Build {git_hash} PROMOTED to LKG.", args.verbose)
        else:
            analysis["promoted"] = False
            _log("Build did NOT meet LKG promotion criteria:", args.verbose)
            for r in reasons:
                _log(f"  - {r}", args.verbose)

    # ------------------------------------------------------------------
    # Update baselines
    # ------------------------------------------------------------------
    if args.update_baselines:
        _update_baselines(verbose=args.verbose)

    # ------------------------------------------------------------------
    # Reports
    # ------------------------------------------------------------------
    md_path = generate_markdown(
        all_results, analysis, lkg_info, duration=total_elapsed
    )
    html_path = generate_html(
        all_results, analysis, lkg_info, duration=total_elapsed
    )
    _log(f"Markdown report: {md_path}", args.verbose)
    _log(f"HTML report:     {html_path}", args.verbose)

    # ------------------------------------------------------------------
    # Summary
    # ------------------------------------------------------------------
    summary = analysis.get("summary", {})
    total_passed = sum(s["passed"] for s in summary.values())
    total_tests = sum(s["total"] for s in summary.values())
    total_failed = sum(s["failed"] for s in summary.values())

    print()
    print(f"{'=' * 60}")
    print(f"  {total_passed}/{total_tests} passed, {total_failed} failed")
    print(f"  Duration: {_duration_str(total_elapsed)}")
    if analysis.get("regressions"):
        print(f"  REGRESSIONS: {len(analysis['regressions'])} test(s) newly failing")
    if analysis.get("improvements"):
        print(f"  IMPROVEMENTS: {len(analysis['improvements'])} test(s) newly passing")
    print(f"{'=' * 60}")
    print()

    # Exit code
    any_crash = any(rc == 2 for rc in suite_return_codes.values())
    if any_crash:
        return 2
    if total_failed > 0:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
