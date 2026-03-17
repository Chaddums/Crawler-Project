# AXIS Producer — Ambient Session Listener
*Claude Code spec | Build tonight, run all weekend*

---

## What It Does

Runs silently in the background. Listens for voices. When it hears talking, it records. When silence returns, it transcribes the chunk with Whisper locally. Every N minutes it batches the accumulated transcript and sends it to Claude with a producer system prompt. Claude extracts structured notes — decisions locked, ideas generated, open questions, action items — and appends them to a running markdown log with timestamps.

No keypress. No interruption. Just a terminal window running while you work.

---

## Architecture

```
Mic → VAD (silence detection) → audio chunks
                                      ↓
                             Faster-Whisper (local)
                                      ↓
                           rolling transcript buffer
                                      ↓ (every 5 min or on demand)
                          Claude API (producer prompt)
                                      ↓
                        session_log.md (append, timestamped)
```

All transcription is local (no audio sent anywhere). Only text goes to Claude API.

---

## Stack

```
Python 3.10+
sounddevice          # cross-platform mic capture
webrtcvad            # Google's VAD — fast, lightweight, no ML overhead
faster-whisper       # local Whisper, 4-8x faster than original
anthropic            # Claude API
numpy                # audio buffer handling
```

Install:
```bash
pip install sounddevice webrtcvad faster-whisper anthropic numpy
```

For faster-whisper model on first run (auto-downloads):
- Use `base.en` model for speed — good enough for conversational speech
- Use `small.en` if accuracy matters more than latency

---

## Core Components

### 1. VAD + Audio Capture (`capture.py`)

```python
# Config
SAMPLE_RATE = 16000          # Required by webrtcvad
FRAME_DURATION_MS = 30       # 10, 20, or 30ms — webrtcvad requirement
SILENCE_THRESHOLD_SEC = 2.0  # Seconds of silence before chunk is "done"
MIN_CHUNK_DURATION_SEC = 1.0 # Ignore chunks shorter than this (coughs, etc.)
VAD_AGGRESSIVENESS = 2       # 0-3, higher = more aggressive filtering
```

Behavior:
- Runs continuously
- VAD filters out silence and background noise
- When voice detected: start accumulating audio frames
- When silence > threshold: seal the chunk, pass to transcription queue
- Thread-safe queue between capture and transcription

### 2. Transcription Worker (`transcriber.py`)

```python
# Config
WHISPER_MODEL = "base.en"    # or "small.en" for better accuracy
DEVICE = "cpu"               # or "cuda" if GPU available
COMPUTE_TYPE = "int8"        # quantized, fast on CPU
```

Behavior:
- Runs in background thread, pulls from capture queue
- Transcribes each audio chunk
- Prepends rough timestamp: `[14:32] some text here`
- Appends to in-memory rolling buffer
- Does NOT write raw transcript to disk (optional flag to enable)

### 3. Batch Processor + Claude Integration (`producer.py`)

```python
# Config
BATCH_INTERVAL_SEC = 300     # Send to Claude every 5 minutes
MIN_WORDS_TO_BATCH = 50      # Don't send tiny fragments
MODEL = "claude-sonnet-4-20250514"
MAX_TOKENS = 1024
```

**System prompt:**
```
You are a producer observing a creative game development session between two developers.
Your job is to extract signal from their conversation and produce structured notes.

For each batch of transcript you receive, output ONLY the following sections 
(omit any section that has nothing to report):

## Decisions Locked
- [specific decisions made, stated as facts]

## Ideas Generated  
- [new concepts, mechanics, or approaches discussed]

## Open Questions
- [unresolved questions raised, phrased as questions]

## Action Items
- [specific tasks someone said they would do]

## Watch List
- [concerns, risks, or disagreements flagged]

Rules:
- Be terse. One line per item.
- No editorializing. Capture what was said, not your opinion of it.
- If someone says "we should" or "we need to" — that's an action item.
- If something was discussed but not resolved — that's an open question.
- Ignore small talk, tangents, and repetition.
- If the transcript is too unclear to extract anything useful, output: [nothing to report]
```

Behavior:
- Every `BATCH_INTERVAL_SEC`, grab the rolling buffer
- If buffer has > MIN_WORDS_TO_BATCH words, send to Claude
- Clear the buffer after sending
- Append Claude's response to session log with timestamp header
- Also support a manual trigger (keyboard shortcut or signal) to force a batch immediately

### 4. Session Log (`session_log.md`)

Output format:
```markdown
# AXIS Session Log
Started: 2026-03-16 09:14

---

## [09:19] Batch 1

### Decisions Locked
- Single player only, no multiplayer for jam build
- Roguelike run structure confirmed over campaign

### Ideas Generated
- 3 starting roles each with different node pool: Scrapwright, Arcanist, Bruteforge
- AXISCommentary system can be used for producer-style in-game feedback

### Open Questions
- What do we call the game?
- Is Latch node needed at launch or can it be post-jam?

### Action Items
- Stu: Build roguelike run wrapper Friday night
- Adam: Design 10 waves

---

## [09:24] Batch 2

...
```

---

## Main Entry Point (`axis_producer.py`)

```python
# Usage
python axis_producer.py                          # Normal run, log to ./session_log.md
python axis_producer.py --log ./logs/day2.md    # Custom log path
python axis_producer.py --model small.en        # Higher accuracy Whisper
python axis_producer.py --interval 600          # 10 min batches instead of 5
python axis_producer.py --verbose               # Print transcript to console as it comes in
```

Startup output:
```
AXIS Producer — listening
Whisper: base.en | VAD: level 2 | Batch: every 5 min
Log: ./session_log.md
Mic: MacBook Pro Microphone (device 0)
[Ctrl+C to stop] [Ctrl+B to force batch now]
```

---

## Threading Model

```
Main thread:        UI, signal handling, keyboard shortcuts
Capture thread:     sounddevice InputStream, VAD, chunk detection
Transcription thread: Faster-Whisper worker, pulls from capture queue
Batch timer thread: Fires every N seconds, triggers Claude call
File writer:        Single writer, no race conditions on log file
```

Use `threading.Event` for clean shutdown on Ctrl+C. Flush remaining buffer to Claude before exit.

---

## Edge Cases to Handle

| Scenario | Behavior |
|---|---|
| Two people talking over each other | VAD merges into one chunk — fine |
| Long silence (nobody talking for 10+ min) | Nothing sent, buffer stays empty |
| Crosstalk with TV/music in background | VAD aggressiveness=2 handles most of this |
| Claude API timeout or error | Log error, keep buffer, retry on next batch |
| Transcript is just background noise | MIN_WORDS_TO_BATCH filters this out |
| Script crashes mid-session | Log is already written up to last batch — no data loss |
| Running overnight with no conversation | Zero cost — nothing sent to API |

---

## Platform Notes

**Windows (the-rig):**
- Run in WSL2 (Ubuntu) — sounddevice works fine
- OR run natively in PowerShell — sounddevice also works on Windows Python
- WSL2 mic passthrough: may need PulseAudio bridge or run natively
- Recommend: native Windows Python for audio capture simplicity

**macOS:**
- Works out of the box

**Recommended: run natively on Windows**, not in WSL2, to avoid mic passthrough complexity.

---

## Privacy Notes

- Audio never leaves the machine — Whisper runs 100% locally
- Only text transcripts sent to Claude API (Anthropic)
- Log file is plaintext markdown — store wherever you want
- Add `session_log.md` to `.gitignore` if in a repo

---

## Nice-to-Have (post-core, add if time)

- `--speaker-labels` flag: crude diarization using audio energy differences between chunks (Stu louder on left, Adam louder on right if using stereo)
- Web UI: local Flask server serving the log as a live-updating page
- End-of-session summary: on Ctrl+C, send full session log to Claude and ask for a final executive summary
- Slack/Discord webhook: post each batch output to a channel automatically
- `--highlight` keyword list: always flag when specific words are spoken ("ship", "cut", "name")

---

## First Test

After building:
1. Start the script
2. Say out loud: "Okay we've decided the game is called AXIS TD, it's a single player roguelike tower defense. Adam is going to design the first 10 waves tonight and Stu is going to build the run wrapper. One question we haven't answered is what the three starting roles are called."
3. Wait for next batch (or Ctrl+B to force)
4. Check session_log.md — should have all 4 sections populated correctly

If that works, it's ready for the weekend.
