"""
Report generator for the Vine Logic TD test runner.

Produces both Markdown and self-contained HTML reports from analysed
test results.
"""

from __future__ import annotations

import base64
import os
from datetime import datetime, timezone
from typing import Any, Optional

from config import PROJECT_PATH

_REPORT_DIR = os.path.join(PROJECT_PATH, "test-reports")


def _now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")


def _duration_str(seconds: float) -> str:
    m, s = divmod(int(seconds), 60)
    return f"{m}m {s}s"


def _git_info() -> tuple[str, str]:
    """Return (short_hash, branch) via git, falling back to unknowns."""
    import subprocess

    try:
        git_hash = (
            subprocess.check_output(
                ["git", "rev-parse", "--short", "HEAD"],
                cwd=PROJECT_PATH,
                stderr=subprocess.DEVNULL,
            )
            .decode()
            .strip()
        )
    except Exception:
        git_hash = "unknown"
    try:
        branch = (
            subprocess.check_output(
                ["git", "rev-parse", "--abbrev-ref", "HEAD"],
                cwd=PROJECT_PATH,
                stderr=subprocess.DEVNULL,
            )
            .decode()
            .strip()
        )
    except Exception:
        branch = "unknown"
    return git_hash, branch


# ---------------------------------------------------------------------------
# Markdown report
# ---------------------------------------------------------------------------


def generate_markdown(
    results: dict[str, Any],
    analysis: dict[str, Any],
    lkg_info: Optional[dict[str, Any]] = None,
    duration: float = 0.0,
) -> str:
    """Write ``test-reports/latest_report.md`` and return the path."""
    git_hash, branch = _git_info()

    summary = analysis.get("summary", {})
    total_passed = sum(s["passed"] for s in summary.values())
    total_tests = sum(s["total"] for s in summary.values())
    failures = analysis.get("failures", [])
    regressions = analysis.get("regressions", [])
    improvements = analysis.get("improvements", [])
    visual_diffs = analysis.get("visual_diffs", [])

    lines: list[str] = []

    # Header
    lines.append(f"# Test Report: {_now_iso()}")
    lines.append(
        f"Build: {git_hash} (branch: {branch}) | "
        f"Duration: {_duration_str(duration)} | "
        f"{total_passed}/{total_tests} PASSED"
    )
    lines.append("")

    # LKG status
    lines.append("## LKG Status")
    if lkg_info:
        lkg_hash = lkg_info.get("git_hash", "unknown")
        lkg_date = lkg_info.get("timestamp", "unknown")
        promoted = _check_promoted(analysis)
        status = "PROMOTED" if promoted else "not promoted"
        lines.append(
            f"Current LKG: {lkg_hash} ({lkg_date}) -- This build: {status}"
        )
    else:
        lines.append("No LKG baseline established yet.")
    lines.append("")

    # Per-suite summary
    lines.append("## Suite Summary")
    for suite_name, s in summary.items():
        status = "PASS" if s["failed"] == 0 else "FAIL"
        lines.append(f"- **{suite_name}**: {s['passed']}/{s['total']} [{status}]")
    lines.append("")

    # Failures
    if failures:
        lines.append("## Failures")
        for f in failures:
            lines.append(f"- **{f['suite']}::{f['name']}**: {f['message']}")
        lines.append("")

    # Visual regressions
    visual_failures = [v for v in visual_diffs if not v.get("passed", True)]
    if visual_failures:
        lines.append("## Visual Regressions")
        for v in visual_failures:
            lines.append(
                f"- **{v['name']}**: {v['diff_percent']}% pixel diff"
            )
            bl = v.get("baseline", "")
            cur = v.get("current", "")
            diff = v.get("diff", "")
            lines.append(f"  -> [Baseline]({bl}) | [Current]({cur}) | [Diff]({diff})")
        lines.append("")

    # Regressions vs LKG
    if regressions:
        lines.append("## Regressions vs LKG")
        for r in regressions:
            lines.append(f"- **{r['suite']}::{r['name']}**: NEW FAILURE (passed in LKG)")
        lines.append("")

    # Improvements vs LKG
    if improvements:
        lines.append("## Improvements vs LKG")
        for imp in improvements:
            lines.append(f"- **{imp['suite']}::{imp['name']}**: now passing")
        lines.append("")

    # Pass list
    lines.append("## Passed Tests")
    for suite_name, suite_data in results.get("suites", {}).items():
        for t in suite_data.get("tests", []):
            if t.get("passed", False):
                lines.append(f"- {suite_name}::{t['name']}")
    lines.append("")

    md_text = "\n".join(lines)
    out_path = os.path.join(_REPORT_DIR, "latest_report.md")
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write(md_text)
    return out_path


# ---------------------------------------------------------------------------
# HTML report
# ---------------------------------------------------------------------------

_HTML_TEMPLATE_HEAD = """\
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Test Report</title>
<style>
  body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
         background: #0a0a14; color: #d0d0d0; margin: 2em; }}
  h1 {{ color: #00e5ff; }}
  h2 {{ color: #00b8d4; border-bottom: 1px solid #1a3a4a; padding-bottom: 4px; }}
  .pass {{ color: #69f0ae; }}
  .fail {{ color: #ff5252; }}
  .regression {{ color: #ff9100; }}
  .improvement {{ color: #69f0ae; }}
  table {{ border-collapse: collapse; width: 100%; margin-bottom: 1.5em; }}
  th, td {{ border: 1px solid #1a3a4a; padding: 6px 10px; text-align: left; }}
  th {{ background: #112233; }}
  img.diff {{ max-width: 320px; border: 1px solid #333; margin: 4px; }}
  .summary-bar {{ display: flex; gap: 1.5em; margin-bottom: 1em; }}
  .summary-box {{ background: #112233; padding: 10px 18px; border-radius: 6px; }}
</style>
</head>
<body>
"""

_HTML_TEMPLATE_TAIL = "</body>\n</html>\n"


def _img_base64(path: str) -> str:
    """Return an HTML <img> tag with base64-encoded image data."""
    if not path or not os.path.isfile(path):
        return "<em>not available</em>"
    with open(path, "rb") as fh:
        data = base64.b64encode(fh.read()).decode("ascii")
    ext = os.path.splitext(path)[1].lstrip(".").lower()
    mime = {"png": "image/png", "jpg": "image/jpeg", "jpeg": "image/jpeg"}.get(
        ext, "image/png"
    )
    return f'<img class="diff" src="data:{mime};base64,{data}" alt="{os.path.basename(path)}">'


def generate_html(
    results: dict[str, Any],
    analysis: dict[str, Any],
    lkg_info: Optional[dict[str, Any]] = None,
    duration: float = 0.0,
) -> str:
    """Write ``test-reports/latest_report.html`` and return the path."""
    git_hash, branch = _git_info()

    summary = analysis.get("summary", {})
    total_passed = sum(s["passed"] for s in summary.values())
    total_tests = sum(s["total"] for s in summary.values())
    failures = analysis.get("failures", [])
    regressions = analysis.get("regressions", [])
    improvements = analysis.get("improvements", [])
    visual_diffs = analysis.get("visual_diffs", [])

    parts: list[str] = [_HTML_TEMPLATE_HEAD]

    # Header
    promoted = _check_promoted(analysis)
    status_class = "pass" if promoted else "fail"
    parts.append(f"<h1>Test Report: {_now_iso()}</h1>")
    parts.append(
        f'<p>Build: <code>{git_hash}</code> (branch: <code>{branch}</code>) | '
        f'Duration: {_duration_str(duration)} | '
        f'<span class="{status_class}">{total_passed}/{total_tests} PASSED</span></p>'
    )

    # LKG
    parts.append("<h2>LKG Status</h2>")
    if lkg_info:
        lkg_hash = lkg_info.get("git_hash", "unknown")
        lkg_date = lkg_info.get("timestamp", "unknown")
        label = "PROMOTED" if promoted else "not promoted"
        parts.append(
            f"<p>Current LKG: <code>{lkg_hash}</code> ({lkg_date}) &mdash; "
            f'This build: <strong class="{status_class}">{label}</strong></p>'
        )
    else:
        parts.append("<p>No LKG baseline established yet.</p>")

    # Suite summary boxes
    parts.append('<div class="summary-bar">')
    for suite_name, s in summary.items():
        cls = "pass" if s["failed"] == 0 else "fail"
        parts.append(
            f'<div class="summary-box"><strong>{suite_name}</strong><br>'
            f'<span class="{cls}">{s["passed"]}/{s["total"]}</span></div>'
        )
    parts.append("</div>")

    # Failures table
    if failures:
        parts.append("<h2>Failures</h2>")
        parts.append("<table><tr><th>Suite</th><th>Test</th><th>Message</th></tr>")
        for f in failures:
            parts.append(
                f'<tr class="fail"><td>{f["suite"]}</td>'
                f'<td>{f["name"]}</td><td>{f["message"]}</td></tr>'
            )
        parts.append("</table>")

    # Visual regressions with inline images
    visual_failures = [v for v in visual_diffs if not v.get("passed", True)]
    if visual_failures:
        parts.append("<h2>Visual Regressions</h2>")
        for v in visual_failures:
            parts.append(f'<h3 class="fail">{v["name"]} &mdash; {v["diff_percent"]}% diff</h3>')
            parts.append("<table><tr><th>Baseline</th><th>Current</th><th>Diff</th></tr><tr>")
            parts.append(f'<td>{_img_base64(v.get("baseline", ""))}</td>')
            parts.append(f'<td>{_img_base64(v.get("current", ""))}</td>')
            parts.append(f'<td>{_img_base64(v.get("diff", ""))}</td>')
            parts.append("</tr></table>")

    # Regressions vs LKG
    if regressions:
        parts.append("<h2>Regressions vs LKG</h2>")
        parts.append("<table><tr><th>Suite</th><th>Test</th><th>Detail</th></tr>")
        for r in regressions:
            parts.append(
                f'<tr class="regression"><td>{r["suite"]}</td>'
                f'<td>{r["name"]}</td><td>NEW FAILURE (passed in LKG)</td></tr>'
            )
        parts.append("</table>")

    # Improvements
    if improvements:
        parts.append("<h2>Improvements vs LKG</h2>")
        parts.append("<table><tr><th>Suite</th><th>Test</th></tr>")
        for imp in improvements:
            parts.append(
                f'<tr class="improvement"><td>{imp["suite"]}</td>'
                f'<td>{imp["name"]}</td></tr>'
            )
        parts.append("</table>")

    # Passed tests
    parts.append("<h2>Passed Tests</h2><ul>")
    for suite_name, suite_data in results.get("suites", {}).items():
        for t in suite_data.get("tests", []):
            if t.get("passed", False):
                parts.append(f'<li class="pass">{suite_name}::{t["name"]}</li>')
    parts.append("</ul>")

    parts.append(_HTML_TEMPLATE_TAIL)

    html_text = "\n".join(parts)
    out_path = os.path.join(_REPORT_DIR, "latest_report.html")
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write(html_text)
    return out_path


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _check_promoted(analysis: dict[str, Any]) -> bool:
    """Quick check: did the analyser flag this build as promotable?"""
    # The caller can set this explicitly; otherwise assume not promoted.
    return analysis.get("promoted", False)
