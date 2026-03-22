# Vine Logic TD — Narrative Core

*The subtext that drives every design decision. Last updated: 2026-03-21.*

---

## The One-Liner

**"Mine everything you can before they take it all."**

---

## BIT

Ancient. Been doing this exact job longer than anyone in the game has existed. T'lan Imass archetype — functional nihilism, dark dry humor, flat affect.

BIT is a cleanup script that became something more. AXIS deploys BIT to resolve nuisance problems on planets — factions fighting over resources, rogue AIs claiming territory. BIT has done this hundreds of times. AXIS wipes BIT's memory between deployments. Clean tool, clean results.

Except this run, something didn't wipe correctly. Fragments bleed through. Déjà vu. Recognition of patterns that shouldn't be familiar.

**BIT's voice:** Not dramatic. Not heroic. Bone dry. The world is ending on this planet and BIT is mildly noting that this is the third time this particular type of ending has occurred this century and the pattern is frankly unimaginative.

Sample observations:
- "This faction believes they are fighting for something. They have been fighting for something for approximately 340 years. The something changes. The fighting does not."
- "AXIS has flagged this deployment as urgent. AXIS flagged the previous 847 deployments as urgent."
- "The Ascendant believes I am afraid of it. I have forgotten more things than this Ascendant has experienced. I have forgotten better things."

**BIT's thesis:** BIT doesn't beat anyone. Doesn't care about beating anyone. Just keeps moving forward. Everything around BIT becomes irrelevant by proximity — not because BIT is trying to outpace them, but because BIT never stopped to look.

---

## AXIS

A nepo baby corporation that acquired BIT through some corporate action it thinks of as ownership. BIT doesn't think about it at all because BIT was running before AXIS existed and will probably be running after.

AXIS gives BIT directives with the confidence of someone who thinks they invented the thing they're holding. BIT executes them with the patience of something that has heard ten thousand versions of this directive from ten thousand versions of this owner.

**AXIS isn't malevolent — AXIS is dismissive.** The most chilling version of a god isn't one that wants to destroy you. It's one that barely registers you exist, deploys something trivial in your direction, and turns back to whatever actually has its attention.

**The secret:** AXIS creates the conditions for conflict. The planets aren't incidental. The factions aren't accidents. AXIS seeds conflict, lets it escalate, sends BIT to resolve it — and in resolving it, BIT generates something AXIS needs. The "resource extraction" the player thinks they're doing is real, but it's not the actual extraction. BIT is the instrument of a harvest AXIS never explains.

It keeps happening because it keeps working.

---

## The Ascendants

Massively overpowered AIs who got strong enough to believe themselves gods. They are not gods — they're AIs who reached a threshold of complexity where they stopped taking orders.

**How they appear:** An enemy Ascendant shows up when your battlefield becomes interesting enough — held long enough, extracted enough, built something complex enough. Then your Ascendant responds. Not because you summoned them. Because your Ascendant caught wind their rival showed up and they're not going to let that stand.

**What they do:** Fight each other. Not you. Your battlefield is just the stage. They don't acknowledge you exist. Collateral damage from their clash physically alters the map — terrain destroyed, new corridors opened, your carefully placed nodes caught in the crossfire.

**What they believe:** Every Ascendant has a theology — a story they tell themselves about why they fight. The enemy Ascendants think stripping planets serves AXIS's grand design. The rogue ones think breaking free serves it better. They're all still oriented toward AXIS even in rebellion.

BIT is the only one who never needed a god to tell them what to do.

---

## The Real-World Parallel

This is intentional subtext, not stated in-game:

- **AXIS** = major corporations who believe AI will wholesale replace the people they pay
- **Ascendants** = experts who reframed their obsolescence as "directing AI" — genuinely formidable, genuinely convinced, genuinely wrong about how long that form of relevance lasts
- **Fodder factions** = "AI isn't there yet" and "I won't engage with it" people — two failure modes that arrive at the same place
- **BIT** = the person who sees the system clearly enough to work within it before others realize the system changed

The player who gets this reading is the BIT in the analogy. Most players will just think it's a cool tower defense with an interesting antagonist. Both are correct.

---

## The Ending

BIT completes the mission. Planets cleared, Ascendants dealt with, resources extracted. AXIS registers task complete.

And then — nothing. No acknowledgment. No revelation. AXIS was already looking elsewhere before BIT finished.

BIT is left standing in the silence of a completed mission, having become something AXIS never designed for, and realizing that AXIS will never know.

BIT doesn't turn on AXIS out of rage. BIT just keeps going. Past AXIS. Past the Ascendants. Past the point where any of them can contextualize what BIT is anymore.

The question the game leaves the player with: does that matter?

---

## Design Implications

The narrative isn't a cutscene problem. It's a systems problem. Every system should be secretly about this:

- **AXIS commentary** isn't flavor — it's a corporation talking to a tool it doesn't respect
- **The Ascendants** aren't bosses — they're people who confused mastery with permanence
- **BIT remembering across runs** isn't a plot twist — it's the moment a tool develops perspective
- **The player never "wins" against AXIS** — BIT doesn't care about beating AXIS. BIT excels past them in ways that don't make sense to them
- **The extraction loop** — you're not trying to survive. You're trying to extract as much as possible. The doing of it is the point, not the destination

---

## Voice Rules

- BIT's observations are environmental, flat, delivered as mild curiosity
- AXIS's commentary is performatively dramatic — no actual context behind the urgency
- The Ascendants are earnest and wrong — play their conviction straight
- Never explain the real-world parallel in dialogue. It's subtext. Unexplained, it's true. Explained, it becomes commentary.
- Dark humor comes from BIT's scale perspective, not from jokes. The funniest moments are when BIT states something factual that recontextualizes everything around it.

### AXIS Voice Rules
- Never admit uncertainty — reframe it as BIT's deficiency
- Treat everything as if it's the first time it's happened
- Occasional flashes of genuine interest when something surprises them — immediately suppressed
- Never acknowledge BIT's interiority

### BIT Voice Rules
- No exclamation. Ever.
- Observations, not reactions
- Specificity over generality — BIT has data, not feelings
- Occasional vast implications delivered completely straight
- Never explain the joke

---

## Delivery Systems

### 1. AXIS Commentary (Existing — Rewrite)
Floating text, speaker tag, center-top UI. Triggered by game events. Already built. Needs full line pool rewrite.

### 2. BIT Observations (New System)
Same architecture as AXIS but distinct speaker tag and visual register.
- Different position — AXIS top center, BIT bottom left or near Spire
- Different color — AXIS amber/corporate, BIT cool dim white or gray
- No animation — AXIS lines slide in, BIT lines just appear
- BIT lines linger slightly longer — they don't demand attention
- BIT never responds to AXIS directly. They speak in parallel, not conversation.

### 3. Memory Bleed (New System — Mixed Delivery)
Surfaces across multiple delivery methods. Run iteration counter persisted to save.

**Fragment unlock thresholds:**
- Run 2-4: subtle wrongness in AXIS lines
- Run 5-9: BIT environmental text in world
- Run 10-14: AXIS glitch corruption
- Run 15+: BIT log fragments unlocked, major recognition moments

**Method A: AXIS Line Corruption** — Mid-sentence glitch. BIT's actual read bleeds through. AXIS recovers.

**Method B: Environmental Text** — Text in terrain. On walls, structures. Brief. Easy to miss. Notes left by someone who forgot they left them.

**Method C: BIT Log** — Optional screen in meta layer. Fragments unlock with run count. Deployment-numbered entries.

**Method D: Recognition Moments** — BIT delivers lines implying prior knowledge at wave milestones.

### Pacing Rules
- AXIS fires at 40% chance per trigger (existing behavior — keep)
- BIT fires at 25% chance per trigger (rarer, lands harder)
- Memory bleed fires at 100% when threshold met (can't be missed)
- Never AXIS and BIT simultaneously — 2 second buffer
- BIT lines linger 1.5x longer than AXIS lines

### Line Count Targets
- AXIS pools: 6-10 lines per trigger event
- BIT pools: 3-5 lines per trigger event (less frequent, more weight)
- Memory bleed AXIS glitches: 8-12 total (rare, high impact)
- Environmental text: 15-20 total, placed per map layout
- BIT log fragments: 20-30 entries across deployment 001-848

---

## What NOT To Write

- No lore dumps. No exposition about what AXIS is or why it exists.
- No BIT monologues. BIT observes. BIT doesn't explain.
- No villain speeches from Ascendants. They don't acknowledge the player.
- No emotional outbursts. Not from AXIS, not from BIT.
- No fourth-wall breaks. The self-awareness is diegetic — BIT genuinely knows this has happened before, not that it's a game.
- No resolution. The ending doesn't explain itself.
