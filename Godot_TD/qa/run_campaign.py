#!/usr/bin/env python3
"""Plays the campaign with a bot from a new save until it is complete: one launch of the game a run.

  python3 qa/run_campaign.py --name novice --config qa/configs/campaign/novice.json
  python3 qa/run_campaign.py --name two --config qa/configs/campaign/talented.json --env CAMPAIGNS=2

With CAMPAIGNS=n the bot takes the ending when a campaign is won and plays the next (harder,
everything earned kept), until n are won.

Each run is a headless autoplay with "campaign" on (Scripts/Debug/CampaignPilot.cs): the bot spends
its perk points, takes the campaign's next site or boss, leaves a few waves after the goal and
saves a suit. --state (default autoplay-reports/campaign_<name>/) holds the save (save/, carried from
run to run), a line a run (campaign_log.jsonl) and each run's game log (run_NN.log). A run in which no
wave clears for --stall seconds is stopped and noted in softlocks.txt (its log kept as failed_NN_*.log). It stops when the
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
    ap.add_argument("--stall", type=int, default=420, help="seconds without a wave cleared before a run is called a softlock and stopped")
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
        stalled = None
        with open(run_log, "w") as out:
            proc = subprocess.Popen([a.godot, "--headless", "--path", project, "--", "--autoplay", "--config", os.path.abspath(a.config)],
                                    stdout=out, stderr=subprocess.STDOUT, env=env)
            waves, since = -1, time.time()
            while proc.poll() is None:
                time.sleep(5)
                # A wave that never ends (an enemy nothing can reach) would hold the run for good
                text = open(run_log, errors="replace").read()
                done_waves = text.count(" done: leaks ")
                if done_waves != waves:
                    waves, since = done_waves, time.time()
                elif time.time() - since > a.stall or time.time() - t0 > a.timeout:
                    who = [l for l in text.splitlines() if "[AutoPlayer/stuck]  " in l]
                    where = [l for l in text.splitlines() if l.startswith("[Campaign] run")]
                    stalled = f"no wave cleared for {a.stall} s after wave {waves}" + (f"; {who[-1].strip()}" if who else "")
                    proc.kill()
                    proc.wait()
                    with open(os.path.join(state, "softlocks.txt"), "a") as f:
                        f.write(f"run {n}: {where[-1] if where else ''}\n  {stalled}\n")
                    break
            code = proc.returncode
        after = lines()
        if os.path.exists(done_path):
            break
        if len(after) < n:
            print(f"run {n}: {'SOFTLOCK, ' + stalled if stalled else f'no result (exit {code})'} ({time.time() - t0:.0f} s); see {run_log}", flush=True)
            shutil.copyfile(run_log, os.path.join(state, f"failed_{n:02d}_{int(time.time())}.log"))
            fails["(crash)"] = fails.get("(crash)", 0) + 1
            if fails["(crash)"] >= 4:
                print("four runs ended without a result: stopping"); break
            continue
        r = after[-1]
        tag = "BOSS BEATEN" if r["boss"] and r["done"] else "secured" if r["done"] else "FAILED"
        camp = f"C{r['campaign']} " if r.get("campaign", 1) > 1 else ""
        hard = f"x{r['siteHp']:<4} " if "siteHp" in r else ""
        print(f"run {r['run']:>2}: {camp}P{r['planet']} {r['name']:<22} {'boss ' if r['boss'] else ''}{hard}goal W{r['goal']:<2} reached W{r['reached']:<3} {tag:<11} "
              f"{r['gameMinutes']:>5.1f} game min  leaks {r.get('leaks', '?'):<3} +{r['pointsEarned']} pt  held {r['sitesHeld']}/{r['sitesTotal']}  ({time.time() - t0:.0f} s)", flush=True)
        if not r["done"]:
            fails[r["site"]] = fails.get(r["site"], 0) + 1
            if fails[r["site"]] >= a.give_up:
                print(f"{r['name']} failed {a.give_up} times: stopping"); break

    runs = lines()
    print()
    if os.path.exists(done_path):
        print(f"CAMPAIGN{'S' if runs and runs[-1].get('campaign', 1) > 1 else ''} COMPLETE in {len(runs)} runs")
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
