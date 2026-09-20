# Motor 2.0 — NVENC Triple Audit — 2026-09-20

Scope: D3D12 compositor FP16 -> GPU conversion -> encoder-compatible BGRA -> NVENC H.264 -> bitstream -> completion -> release.

## Audit A — static contract / ownership
Findings:
- P0 found: WaitForCompletion locked the NVENC bitstream, then GetBitstream locked the same output buffer again.
- P0 found: completed native submissions were never erased and registered/mapped/bitstream resources survived every frame.
- P0 found: managed Dispose released converted D3D12 targets before native NVENC unregister.
Repairs:
- completion now performs the single blocking lock and copies bytes into submission-owned storage;
- GetBitstream consumes stored bytes, unmaps, unregisters, destroys bitstream buffer, and erases submission;
- Dispose closes native NVENC first, then releases any remaining converted targets.

## Audit B — history / CI / diff
- #129, #130, #131 SUCCESS.
- #132 historical intermediate FAIL; later dependency chain repaired.
- #133 through #141 SUCCESS.
- Historical FAIL remains visible and is not relabelled.
- New remediation commits require fresh compile gates before closure.

## Audit C — adversarial lifetime / failure paths
Finding:
- P0: scheduler released compositor targets safely only on normal completion. Exceptions/cancellation could either leak a pre-submit target or tempt premature release after submit.
Repair:
- GPU export now requires IGpuFrameCompletionSource explicitly.
- target is released in finally only if encoder ownership is no longer outstanding.
- a submitted-but-unconfirmed target is deliberately retained for session recovery rather than freed under encoder use.

## Still not runtime-proven
A green GitHub compile gate cannot prove actual NVENC encoding on the user's RTX 4060 because the hosted runner is not that GPU.
The runtime D3D12 pixel gate and real NVENC H.264 bitstream must still execute on real supported NVIDIA hardware before this stage can be called end-to-end validated.

## Closure rule
This audit is not CLOSED until all remediation compile gates are green and no P0 remains in static/history/adversarial review. Hardware runtime validation is a separate mandatory gate before user release.
