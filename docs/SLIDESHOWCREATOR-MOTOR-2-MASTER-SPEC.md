# SlideshowCreator — MOTOR 2.0 MASTER SPEC
Status: CANONICAL / source of truth
Started: 2026-09-20

## 1. Product rule
Motor 2.0 is a clean architectural generation. Legacy 0.1.x code is reference/benchmark only. New architecture must not be bent around it.
No accepted requirement may be removed without Ovidiu's explicit approval.
A feature is not DONE until tested with real media.

## 2. Non-negotiable photographic quality
- Originals are never modified.
- Full photograph remains visible by default: proportional CONTAIN; no arbitrary destructive crop.
- No automatic zoom, blur, or clarity-degrading filters.
- Empty/design space is preferable to damaging a photograph.
- Effects never justify distortion, destructive crop, or prolonged useless occlusion.
- Export uses originals; proxies/mipmaps are preview/performance aids only.
- Preserve elegant visual fade-in of first image and fade-out of last image.

## 3. Media
Modes: FOTO+VIDEO, DOAR FOTO, DOAR VIDEO.
Photos: JPEG and professional still formats; Sony ARW/RAW is first-class.
Metadata: DateTimeOriginal primary; camera time offsets are project-only and never rewrite EXIF.
Video: non-destructive IN/OUT trim, original untouched; per-clip Mute/Original.
Music: user-provided; preserve audio fade-in/fade-out.
Final target: MP4, with hardware encode where available.

## 4. Architecture
FFmpeg is auxiliary (demux/mux/audio/fallback codecs), not the slideshow compositor.
Core is an asynchronous GPU media/motion-graphics engine:
Media Asset DB -> async I/O/prefetch -> decode -> RAM/VRAM cache -> Scene Graph -> Choreography + Constraints -> Frame/Execution Graph -> GPU compositor -> hardware encoder -> mux.

Primary Windows/NVIDIA path:
- D3D12 compositor and preview
- CUDA/CUDA Graphs where benchmarked beneficial
- NVDEC for video decode
- NVENC direct hardware encode
- low/zero-copy GPU-resident surfaces where practical
Fallbacks must exist for non-NVIDIA/unsupported hardware.

## 5. Asset database and cache
Track path, fingerprint/hash, dimensions, EXIF orientation, DateTimeOriginal, ICC, RAW metadata, video codec/profile/framerate/VFR-CFR/audio/duration/keyframes, thumbnails/proxy/cache state.
Multi-level cache: SSD cold -> RAM warm -> VRAM hot.
Generate/use mip/proxy pyramid according to actual screen footprint. A static photo should be decoded/prepared once and reused across frames.

## 6. Timeline, Scene Graph, Motion
One retained Scene Graph powers preview and export.
Photo/video objects carry media id, X/Y/Z, scale, rotation, opacity, perspective/mask, Z-order and timing.
Motion supports easing, Bezier/splines, springs/arcs, parenting/groups and virtual canvas/camera.
Procedural animation should run on GPU when advantageous.

## 7. TRANZITII and CARUSEL
Only two main user categories:
TRANZITII = simple inter-frame transitions.
CARUSEL = procedural simultaneous multi-photo/video choreography.
CARUSEL is NOT a small preset list. It is a parameterized choreography engine with controlled randomness and deterministic seed.
2-4 plane families plus large WALL scenes; dynamic Z-order; independent entry/exit/repositioning; anti-repetition history.

Required families include: PHOTO WALL, FILM STRIP, CONVEYOR, MOSAIC FLOW, PARADE, RIVER, ORBIT/RING, CASCADE, WALL->FOCUS, FOCUS->WALL, WAVE, SHUFFLE, CROSS TRAFFIC, ROTATION FLOW, DIAGONAL SLIDE PARADE, DIAGONAL FILM STRIP, ROTATING WALL, FAN, CARD STREAM, SPIRAL/ARC FLOW, DIAGONAL CROSSING, SPIN BURST, EXPLODE/REASSEMBLE, SURPRISE ANGLES, PHOTO HEART, SHAPE MOSAICS, SPEED TRAIN, BURST TO WALL, WALL SHATTER, TUNNEL/DEPTH PARADE, CORNER SWARM, CENTER BLOOM, FAN CASCADE, SPIRAL BURST, DIAGONAL RAIN, ROTATING GRID, MOVING CONTACT SHEET, RIBBON STREAM, DOUBLE/TRIPLE FILM STRIP, MULTI-AXIS PARADE.
Dense spectacle is an accent; alternate with calm/readable Full Frame or 1-4 photo passages.

## 8. Constraint Engine
Generated choreography is validated before rendering:
- preserve aspect ratio/full photo
- minimum readable visibility interval
- prevent useless permanent occlusion
- safe composition boundaries
- avoid recent geometry/direction/rhythm/motion-family repetition
- reject/regenerate invalid scenes
Future subject/face analysis may guide layout but must not alter source imagery by default.

## 9. Audio intelligence
48 kHz float internal pipeline target.
Waveform plus beat/onset/tempo/bar/section/drop/quiet/ending analysis.
Choreography consumes musical events, not merely BPM; not every beat forces a cut.
Mixing/fade/ducking supported.

## 10. Color
Professional managed color path. Evaluate OpenColorIO/ACES + LibRaw/RAWtoACES.
Input transform -> linear high precision working space (FP16 target) -> compositor -> output transform (Rec.709 SDR first; HDR later).
Performance must not silently sacrifice photographic color quality.

## 11. Scheduling/performance
Async queues for I/O, decode, upload/copy, compute, graphics and encode; multiple frames in flight.
Optimize elapsed export time, not cosmetic 100% utilization.
Hardware profiler/auto-tuner measures SSD, CPU decode, GPU upload/composition, VRAM and encoder and sets workers/cache/frames-in-flight/preset.
User modes: Redus / Normal(default) / Maxim plus Performanta maxima la export. Never Realtime process priority.
Local per-stage telemetry identifies bottlenecks.

## 12. Reliability
Preflight before long render.
Checkpoint/resumable strategy for long exports.
Deterministic project state/random seed.
Automated stress/regression suite before user installer: 10, 50, 120+ photos; RAW; photo+video+music; dense CARUSEL; trim; audio; first/last fades; failure paths.
Build success != validated.

## 13. Distribution
Windows standalone application; offline after install.
Single self-contained Setup.exe containing required runtimes/media components.
No manual developer tools or codec packs for user.
Upgrade in-place; preserve projects/settings.
Projects portable; future Pack Project + Relink Media.

## 14. Official real-media bank
Persistent Library folder: SlideshowCreator Test Media.
Current bank: 17 Sony ARW originals + one Sony FX30 MP4 excerpt cut without re-encoding.
Use it for RAW metadata/orientation/decode/color/cache/texture tests and video codec/decode/trim tests.

## 15. Baseline to beat
Legacy 0.1.10: 120 photos + music + CARUSEL Automat completed in about 46 minutes.
Legacy 0.1.11: failed after about 48 minutes around CARUSEL 48/121.
Motor 2.0 must be stable first and then measurably beat this baseline. No exact speed promise before measurement.

## 16. Implementation order
M2.0-A foundation/contracts + hardware capability probe + media probe.
M2.0-B RAW/video decode spike + cache + color correctness.
M2.0-C retained Scene Graph + D3D12 compositor + same-scene preview.
M2.0-D scheduler/frame graph + GPU-resident export/NVENC.
M2.0-E procedural choreography + constraint engine.
M2.0-F audio intelligence.
M2.0-G stress harness, installer, real-media validation.

This file overrides legacy implementation assumptions when they conflict with Motor 2.0 architecture. Legacy behavior requirements remain only where explicitly preserved here or in the canonical product specification.
## Regula absolută FAIL = DEFECT DE REPARAT
Orice rezultat **FAIL** apărut în compile gate, audit static, audit diferențial/CI, audit adversarial, test runtime, test hardware, test de pixeli, stress test, test cu media reală, export sau validare este tratat obligatoriu ca **defect de investigat până la cauza-rădăcină și reparat**.

Este interzis să se ocolească un FAIL prin dezactivarea testului, slăbirea criteriului, reclasificarea lui ca neimportant, ascunderea erorii, ignorarea rezultatului, schimbarea testului doar ca să devină verde sau continuarea livrării ca și cum FAIL-ul nu ar exista. Un test poate fi corectat numai dacă testul însuși este demonstrabil greșit; cauza și corecția testului trebuie documentate.

După orice reparație se rulează din nou verificarea care a eșuat și verificările relevante de regresie. **FAIL-ul este închis numai când cauza-rădăcină este identificată, remedierea este în cod, iar reverificarea trece.** Build Success rămâne diferit de validarea runtime/end-to-end.

Această regulă are prioritate pentru toate versiunile Motor 2.0 și pentru toate etapele viitoare ale proiectului.
