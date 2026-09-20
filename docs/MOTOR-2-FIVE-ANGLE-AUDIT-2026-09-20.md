# Motor 2.0 — Five-Angle Audit + Meta-Audit — 2026-09-20

Rule: any FAIL is repaired, never bypassed. Build success is not runtime validation.

## 1. Build / packaging / delivery / UX
#157 failed because Add-Type ConsoleApplication is unsupported; repaired with .NET 8 PublishSingleFile and #158 passed. #159 then failed because top-level C# used invalid static/readonly declarations; repaired immediately and fresh CI is mandatory. Earlier package omitted Motor2Native.dll; explicit copy/existence assertions now guard it. Earlier console evidence vanished after ENTER; persistent Desktop PASS/FAIL result is now required. GitHub artifact transport still wraps a one-EXE payload in ZIP, so literal direct-EXE delivery remains separate work.
Status: OPEN until fresh CI + user runtime result.

## 2. GPU / D3D12 correctness / lifetime
Motor2 uses FP16 render targets and retained textures, not still-to-video intermediates. Encoder completion is required before composed target release. Upload/release still use CPU WaitForGpu, safe but serial. Shared direct/copy fence is conservative only because current waits serialize work; future async overlap needs stronger queue-specific discipline. Device-removed/DRED recovery and rich shader diagnostics remain absent.
Status: OPEN for production async/recovery.

## 3. NVENC / completion / memory / delayed output
Real NVENC SDK branch compiles; runtime driver DLL is dynamically loaded; converted GPU surfaces live until completion; prior HMODULE leak was repaired. CompletedBitstreams retains all packet byte arrays indefinitely: acceptable for a 90-frame diagnostic only, not long exports. Submit→wait is serial. NEED_MORE_INPUT/delayed output/EOS semantics need robust queue handling and real hardware proof. Color/VUI and final MP4/audio mux remain incomplete.
Status: OPEN; no end-to-end claim before RTX hardware result.

## 4. Failure paths / cancellation / diagnostics
Capabilities fail early and scheduler avoids freeing encoder-owned surfaces. Cancellation after submit relies on session disposal recovery and is not stress-proven. Result persistence is now added, but SaveResult itself can fail because of Desktop permissions/redirection and could mask an original error; fallback diagnostics are required. ONE-EXE launcher extracts to temp but does not clean its temp directory.
Status: OPEN with concrete diagnostic robustness defects.

## 5. Product / image quality / scale / performance
HardwareGate renders EMPTY scenes. It proves plumbing only: not ARW decode, EXIF chronology, color-managed RAW, CONTAIN, CARUSEL/WALL, music/beat, trim, mixed media, or 120-photo real-media behavior. Current scheduler waits each NVENC frame, so it cannot prove final throughput. Current linear→sRGB bridge is not full OCIO/ACES/Rec.709 validation.
Status: product validation OPEN.

## Cross-comparison
The audits converge on a key separation: package success, GPU lifetime, encoder correctness, diagnostic resilience, and real-product behavior are independent gates. One PASS cannot substitute for another. Highest-risk intersections are NVENC memory+performance; D3D12 fence+encoder completion; delivery+diagnostic evidence; empty-scene GPU success vs real-photo correctness; and delayed encoder output vs observable recovery.

## Meta-audit of the comparison
Checked blind spots: compile-only false green, empty-scene false green, short-test hiding RAM growth, one NVIDIA machine generalized to all hardware, package-presence mistaken for good UX, and regression from fixes. All are explicitly rejected. Every repair requires fresh CI plus relevant regression gates. RTX 4060 runtime and the persistent real ARW/MP4 bank remain mandatory.

## Blocking order
P0-A fresh CI after #159 repair.
P0-B exception-safe result persistence with fallback.
P0-C ONE-EXE runtime on RTX 4060 with persistent result.
P0-D remove unbounded bitstream retention before long stress/export.
P0-E define delayed-output/EOS semantics before pipelining.
Then real media/color/CONTAIN, 10/50/120+ stress, mixed media/audio, performance tuning.

No milestone is validated while an applicable P0 remains open.
