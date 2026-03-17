# Game Jam Planning Meeting — Agenda
*Stu + Adam | Today | Goal: locked decisions + weekend battle plan*

You both know where the build is. This meeting is about alignment and committing — no more "leaning toward." Leave with locked answers.

---

## Section 1: The One Big Question (15 min)

*Everything else flows from this.*

**Campaign (level-based) vs Roguelike run structure?**

Adam already leaned roguelike in Discord. This is the right call — here's why it also fits what's built:
- The VineLogic node system has enormous build variety already baked in
- A roguelike draft (pick 2 of 3 node pools) makes each run feel different with zero new content
- Campaign would require designing 10-20 hand-crafted maps/challenges — much more content work for the weekend
- StS-style: each wave cleared = new node type offered + scrap. Run ends = start fresh with different draft.

**Decision to make:** Lock roguelike. Yes or no.

If yes, the weekend structure prototype is:
1. Pre-run draft screen (pick starting node pool)
2. Waves (build phase → wave → build phase loop)
3. Wave clear = node reward + scrap bonus
4. Run ends on core destroyed OR all waves cleared
5. No persistent meta-progression for the jam — just the run

---

## Section 2: Scope Lock — What Does "Done" Mean for the Weekend? (20 min)

*Be ruthless here. This is the most important conversation.*

### The Jam Build should have exactly:

**Core loop (non-negotiable):**
- [ ] Pre-run draft — pick starting node pool (3 choices, pick 1)
- [ ] 10 waves, balanced and playable start to finish
- [ ] Build phase → wave → reward loop working cleanly
- [ ] Win screen + lose screen
- [ ] Sound design (even placeholder clunks and sparks)

**Minimum viable content:**
- [ ] 3 map layouts (one is already in — need 2 more)
- [ ] 3 enemy types per faction (Scavenger, Brute tuned — Ghost/Swarm lite)
- [ ] 8-10 node types available in draft (not all 18 — pick the most fun ones)
- [ ] 3-4 corruption/modifier events mid-run (one per run randomly)

**Cut for post-jam:**
- Meta progression / unlocks
- HeroBot system (exists in code, not needed for jam)
- Full 18 node types at launch
- Campaign mode
- Multiplayer (already decided)
- Steam / console — just desktop for jam

### Time budget reality check:
| Day | Focus |
|-----|-------|
| Friday night | Roguelike run structure + draft screen |
| Saturday AM | Wave balance + content pass (10 waves) |
| Saturday PM | 2 more maps + corruptions |
| Sunday AM | Sound + visual polish (signal readability) |
| Sunday PM | Bug fix + build + buffer |

---

## Section 3: Theme and Aesthetic Lock (15 min)

You're aligned on: single player, sci-fi/fantastical, biomechanical, not realistic tech.

**Remaining decisions:**

**1. The 3 starting roles** (Adam's idea)
Each role = different starting node pool + different enemy faction you face first.
Proposal:
- *Scrapwright* — structural/routing nodes, faces Scavengers (fast, swarm)
- *Arcanist* — sensor/input heavy, faces Ghosts (phase through, tricky to catch)
- *Bruteforge* — effect/output heavy, faces Brutes (bulldoze your switches)

Do these feel right or do you want different splits?

**2. Name of the game**
Working title: Vine Logic, Junkyard TD, AXIS TD (commentary system is named AXISCommentary already)
What's it called?

**3. Biomechanical enemy aesthetic**
Adam said "Borg-like not Zerg-like" — confirmed?
Enemies are mechanical constructs that move organically, not flesh-towers.

---

## Section 4: Node Type Shortlist for Jam (10 min)

Full list is 18. You want 10-12 max in any single run. For the jam, let's agree on the core set.

**Definitely in (foundational):**
- Cable Splice (Extender)
- Junction Box
- Switch
- Proximity Sensor
- Damage Tower
- Slow Field

**Probably in:**
- Gate (AND)
- Delay
- Count Sensor
- Buff Emitter

**Post-jam:**
- Latch (complex, needs tutorial)
- Inverter (fun but steep)
- HP Sensor
- Loop Anchor (powerful but unstable risk)
- Push/Pull
- Timer
- Type Sensor
- Signal Cannon

**Decision:** Does this shortlist feel right? Any swaps?

---

## Section 5: Divide the Weekend (10 min)

Based on what's built, suggested split:

| Stu | Adam |
|-----|------|
| Roguelike run structure + draft UI | Wave content (10 waves designed and balanced) |
| Map layouts (2 more) | Enemy faction tuning + corruption events |
| Signal visual polish (readability) | Commentary lines (AXISCommentary needs text) |
| Build + deploy | Sound design sourcing |

Does this match strengths? What does Adam actually want to own?

---

## Section 6: Quick Other Stuff (10 min)

*From Discord — clear these fast:*

- **Platform target for jam build:** Desktop only, Windows first
- **Gameplay length target:** Aim for a run taking 25-35 min. Players will fall off around 10 hours of total time (5-10 runs). Don't design for 20 hours this weekend.
- **Early 2000s web game energy:** Small, tight, replayable. That's the right reference point.
- **Anything to buy?** Assets, sounds, fonts? Decide now so someone can grab them tonight.

---

## Exit Criteria — Don't Leave Without These

By end of meeting:

- [ ] Roguelike confirmed, campaign shelved
- [ ] Node shortlist agreed (10-12 types)
- [ ] 3 starting roles named and split defined
- [ ] Game has a name
- [ ] Weekend work divided
- [ ] First task each person is doing tonight is clear
