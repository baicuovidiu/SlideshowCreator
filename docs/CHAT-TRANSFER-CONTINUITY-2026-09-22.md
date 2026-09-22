# SlideshowCreator — CHAT TRANSFER / CONTINUITY — 2026-09-22

## Purpose
Authoritative handoff for a new ChatGPT conversation. Read this file first, then the complex audit. Do not reconstruct project state from assumptions.

## User command
Plain **CONTINUĂ** means: inspect the repository and latest CI; continue from the last validated point; repair FAIL before new work; test the exact product path; never call compile-only work functional.

## Product objective
Immediate recovery target: **120 real photographs -> real CARUSEL -> user music -> MP4 -> measured export time**, while every photograph remains complete/proportional (CONTAIN), originals untouched, no arbitrary crop/blur/automatic zoom.

User is a professional photographer, not the developer/debugger. Do not ask him to install development tools, run shell commands, copy source trees, or diagnose builds. Real-machine testing should be requested only after all reasonable repository/CI/software checks are clean.

## Repository
- Repo: baicuovidiu/SlideshowCreator
- Branch: motor-2.0
- Recovery application: src/SlideshowCreator
- Master spec: docs/SLIDESHOWCREATOR-MOTOR-2-MASTER-SPEC.md
- Authoritative recovery audit: docs/SALVAGE-COMPLEX-AUDIT-2026-09-22.md
- Official real-media bank: Library folder /SlideshowCreator Test Media (17 Sony A7 IV ARW + one Sony FX30 MP4). Do not claim it has been exercised until it actually has.

## Validation vocabulary
- COMPILAT = build only.
- VALIDAT SOFTWARE = exact relevant software test passed.
- NEVERIFICAT PE RTX = until user's RTX 4060 machine actually runs it.
- FAIL = defect to investigate and repair; never bypass/disable a correct test merely to turn CI green.
- FUNCȚIONEAZĂ = only after relevant real execution succeeds.

## Strategic constraints
Do not restart another low-level engine by default. The old WPF/FFmpeg application already exported video; recovery strategy is to salvage the shortest mature path to the product. Low-level Motor2.Native/HardwareGate sources may remain in repo but must not block salvage CI. Prefer mature components/libraries over reinventing codecs/compositors, but do not migrate to a new framework without a small comparative proof first.

## Current exact state at transfer
Last known clean CI before the newest refactor: **run #234 SUCCESS**, commit **95ad578dac0e62c158384a7c14e975e4f9d1451c**.
That commit quarantined the remaining Hardware Gate/native build pipeline from salvage CI and corrected misleading synthetic test labels.

Newest commit: **65f77bf4724f923ab665c4e4ef5f4ba8f7e96c22** — refactor intended to isolate the exact product CARUSEL graph builder so CI can test the same graph used by export.
**Run #235 FAILED at step 'Compile Motor 2.0'. THIS FAIL IS OPEN.**
Do not continue feature work until the compile failure is diagnosed and repaired, then rerun CI/regressions.

## Why the refactor was made
Audit found that prior tests named as CARUSEL did not invoke ExportPhotosCarousel. They only validated surrogate FFmpeg primitives/simple concat. The required repair is an executable test seam around the exact graph builder used by the application, followed by a true 120-photo CARUSEL + music software export.

## Audit repair order
A. Repair current #235 compile FAIL first.
B. Finish exact CARUSEL test seam: application export and test must share the same graph builder.
C. Run true 120-photo CARUSEL + music software export; ffprobe output/duration and decode integrity.
D. Repair Windows 120-real-input command-line risk before asking user to test.
E. Implement and validate video Original/Mute + explicit TrimOut before claiming mixed-media support.
F. Exercise official ARW/FX30 bank before claiming RAW/video readiness.
G. Only then package next user-test build and measure RTX 4060 against legacy ~46 min baseline.

## Known product gaps (do not silently forget)
- Mute/Original video audio currently ineffective; source video audio is not mapped.
- No explicit TrimOut; Duration is conflated with OUT in UI.
- CARUSEL is photo-only.
- Music fade-in/out absent.
- Opening/final visual fades are not consistently applied to simple/mixed export.
- RAW extension is listed but dedicated ARW decode/readiness is not validated.
- Current CARUSEL is limited fixed pair/3/4 layouts; no WALL/rotation/diagonal/anti-repetition/dynamic-Z engine yet.
- No project save/load, Pack Project/Relink, installer/upgrader, performance modes, beat engine, or slideshow CARUSEL preview.
- Export validation is too shallow.
- Final deliverable is not yet the requested single offline Setup.exe.

## Protected requirements
Never sacrifice the photograph for an effect: full image visible, proportional CONTAIN, no arbitrary source crop, no blur, no automatic zoom, originals untouched. Multi-plane photos remain independent and complete. Future CARUSEL includes 2-4 plane layouts, evolving layouts, WALL/filmstrip/conveyor/mosaic/orbit/cascade, diagonal/rotation families and controlled spectacular bursts. Audio fade-in/out plus elegant first-image entrance and last-image exit are protected requirements.

## Development discipline
Before editing: read current affected code + audit + relevant requirement. Define the exact failure/goal, smallest safe change, regression surface, and test. Use narrow edits; never broad placeholder rewrites. After editing, audit the diff adversarially. One meaningful issue per commit where practical. Never invent infrastructure merely because it is interesting. Periodically reconcile every change with the immediate product objective.

## Important history
Legacy 0.1.10 exported 120 photos + music + CARUSEL in about 46 minutes: functional but too slow/weak visually. 0.1.11 failed at CARUSEL 48/121 after ~48 min. Direct low-level NVENC Hardware Gate later failed on the user's RTX 4060 at nvEncInitializeEncoder status 40008 / INVALID_PARAM. Do not confuse that obsolete experiment with current salvage app.

## New-chat first action
On **CONTINUĂ**:
1. Inspect current branch/head and CI run #235.
2. Diagnose the Compile Motor 2.0 failure from commit 65f77bf4.
3. Repair root cause with a minimal change.
4. Rerun/check CI.
5. Only after clean compile continue the exact-CARUSEL validation seam.
