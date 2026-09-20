# Motor 2.0 Hardware Gate — Triple Audit — 2026-09-20

Scope: deliverable Windows x64 Hardware Gate for D3D12 -> FP16 -> GPU BGRA conversion -> real NVENC H.264 -> bitstream -> completion -> release.

## A. Static/package audit
- Real NVENC compile branch is mandatory: CI supplies nvEncodeAPI.h from FFmpeg/nv-codec-headers 13.1.x and defines MOTOR2_HAS_NVENC_SDK.
- Stub-only package is not acceptable.
- Self-contained .NET runtime plus Motor2Native.dll are packaged together.
- Native driver DLL nvEncodeAPI64.dll is runtime-loaded from the installed NVIDIA driver; it is not redistributed.
- HMODULE lifetime leak found during this audit: repaired by storing the module handle in NativeNvencSession and FreeLibrary after encoder destruction.

## B. CI/history audit
- #148/#149 failed due to stale Hardware Gate managed API names/signatures. Root cause repaired in #150.
- #150 and #151 passed.
- Fresh gates after real-NVENC packaging and HMODULE repair are mandatory before delivery.

## C. Adversarial/runtime audit
- Hardware Gate submits 90 consecutive 1920x1080 frames.
- It requires D3D12 and a successful native NVENC capability probe.
- It requires one completed H.264 bitstream per submitted test frame and non-empty Annex-B output.
- Any capability, encode, completion, bitstream or release failure is fatal.
- Actual RTX 4060 execution remains mandatory. CI success is not end-to-end hardware validation.

## Blocking rule
Any FAIL/P0 blocks delivery and must be repaired and retested. NVENC is not end-to-end validated until the packaged gate passes on the user's RTX 4060.
