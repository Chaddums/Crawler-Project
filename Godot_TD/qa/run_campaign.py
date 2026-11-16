#!/usr/bin/env python3
"""Plays the campaign with a bot from a new save until it is complete: one launch of the game a run.

  python3 qa/run_campaign.py --name novice --config qa/configs/campaign/novice.json

Each run is a headless autoplay with "campaign" on (Scripts/Debug/CampaignPilot.cs): the bot spends
its perk points, takes the campaign's next site or boss, leaves a few waves after the goal and
saves a suit. --state (default autoplay-reports/campaign_<name>/) holds the save (save/, carried from
run to run), a line a run (campaign_log.jsonl) and each run's game log (run_NN.log). It stops when the
game writes campaign_complete, after --max-runs, or when a site has been failed --give-up times.
"""
import argparse, json, os, shutil, subprocess, sys, time

def main():
    here = os.path.dirname(os.path.abspath(__file__))
    project = os.path.dirname(here)
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", default="novice")
    ap.add_argument("--config", default=os.path.join(here, "configs", "campaign", "novice.json"))
    ap.add_argument("--godot", default=os.environ.get("GODOT", "godot"))
    ap.add_argument("--state", default=None)
    ap.add_argument("--max-runs", type=int, default=80)
    ap.add_argument("--give-up", type=int, default=6, help="stop after this many failed tries at one site")
    ap.add_argument("--timeout", type=int, default=3600, help="seconds a run may take")
    ap.add_argument("--fresh", action="store_true", help="start from a new save even if the state folder has one")
    ap.add_argument("--env", action="append", default=[], help="NAME=VALUE for the game, e.g. WAVE_SEED=1")
    a = ap.parse_args()

    state = os.path.abspath(a.state or os.path.join(project, "autoplay-reports", f"campaign_{a.name}"))
    if a.fresh and os.path.isdir(state):
        shutil.rmtree(state)
    os.makedirs(state, exist_ok=True)
    log_path = os.path.join(state, "campaign_log.jsonl")
    done_path = os.path.join(state, "campaign_complete")

    def lines():
        if not os.path.exists(log_path):
            return []
        with open(log_path) as f:
            return [json.loads(l) for l in f if l.strip()]

    saves = os.path.join(state, "save")
    os.makedirs(saves, exist_ok=True)
    env = dict(os.environ, SANDBOX_SEED=saves, SANDBOX_NAME=a.name, CAMPAIGN_STATE=state, CAMPAIGN="1")
    for kv in a.env:
        k, _, v = kv.partition("=")
        env[k] = v
    fails, started = {}, time.time()
    while not os.path.exists(done_path) and len(lines()) < a.max_runs:
        n = len(lines()) + 1
        run_log = os.path.join(state, f"run_{n:02d}.log")
        t0 = time.time()
        with open(run_log, "w") as out:
            try:
                code = subprocess.run([a.godot, "--headless", "--path", project, "--", "--autoplay", "--config", os.path.abspath(a.config)],
                                      stdout=out, stderr=subprocess.STDOUT, env=env, timeout=a.timeout).returncode
            except subprocess.TimeoutExpired:
                code = -9
        after = lines()
        if os.path.exists(done_path):
            break
        if len(after) < n:
            print(f"run {n}: no result (exit {code}, {time.time() - t0:.0f} s); see {run_log}", flush=True)
            fails["(crash)"] = fails.get("(crash)", 0) + 1
            if fails["(crash)"] >= 3:
                print("three runs ended without a result: stopping"); break
            continue
        r = after[-1]
        tag = "BOSS BEATEN" if r["boss"] and r["done"] else "secured" if r["done"] else "FAILED"
        print(f"run {r['run']:>2}: P{r['planet']} {r['name']:<22} {'boss ' if r['boss'] else ''}goal W{r['goal']:<2} reached W{r['reached']:<3} {tag:<11} "
              f"{r['gameMinutes']:>5.1f} game min  +{r['pointsEarned']} pt  held {r['sitesHeld']}/{r['sitesTotal']}  ({time.time() - t0:.0f} s)", flush=True)
        if not r["done"]:
            fails[r["site"]] = fails.get(r["site"], 0) + 1
            if fails[r["site"]] >= a.give_up:
                print(f"{r['name']} failed {a.give_up} times: stopping"); break

    runs = lines()
    print()
    if os.path.exists(done_path):
        print(f"CAMPAIGN COMPLETE in {len(runs)} runs")
    else:
        print(f"stopped after {len(runs)} runs, campaign not complete")
    if runs:
        mins = sum(r["gameMinutes"] for r in runs)
        failed = [r for r in runs if not r["done"]]
        print(f"game time {mins / 60:.1f} hours ({mins:.0f} minutes), {len(failed)} failed run(s), "
              f"perk points spent {runs[-1]['pointsSpent']} (enemies x{runs[-1]['threat']}), banked {runs[-1]['banked']} Resources, "
              f"{runs[-1]['sitesHeld']} of {runs[-1]['sitesTotal']} sites held; wall clock {(time.time() - started) / 60:.0f} min")
        errs = sum(r["errors"] for r in runs)
        if errs:
            print(f"{errs} engine error(s) logged across the runs")
    return 0

if __name__ == "__main__":
    sys.exit(main())
