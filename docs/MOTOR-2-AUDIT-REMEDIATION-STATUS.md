# Motor 2.0 — audit remediation status — 2026-09-20

This file tracks the concrete remediation of the triple-audit findings.

## Closed in code
- RTV compile regression: repaired; Gate #99 passed.
- Architecture Guard matching: repaired; Gate #95 passed.
- Shader constant visibility and straight-alpha opacity math: repaired.
- Uploaded texture transition COPY_DEST -> PIXEL_SHADER_RESOURCE: present.
- Batched per-frame draw submission: present.
- Triple command-allocator frames-in-flight: present.
- Empty frame now submits a native draw/clear instead of silently skipping rendering.
- Pixel-space -> NDC convention is explicit in PhotoLayout and applied by D3D12Compositor.
- Numeric transform-convention self-check added.
- Texture release is context-aware, drains GPU before destruction, removes resource->SRV mapping and recycles SRV slots.
- Render-target release has a distinct typed ABI.
- Context destroy drains the direct queue before COM resources are destroyed.
- VRAM cache no longer trims during frame assembly; TrimAsync is an explicit serialized safe point.
- HardwareProfiler now proves D3D12 using the native probe rather than brand inference.
- Telemetry counters are cumulative; render.frame increments by one.
- Diagnostic-only FP16 render-target readback added.
- Adversarial D3D12RuntimePixelGate added: verifies a real cleared FP16 frame byte-for-byte when executed on Windows D3D12 hardware.

## Still open — implementation work, not hidden as 'fixed'
- Native NVENC SDK session is not implemented; current code is an interface/backend boundary only.
- Render-target ownership cannot be finalized until the NVENC session exposes completion/fence semantics. A target must not be recycled before encoder consumption completes.
- Upload path remains correctness-first and CPU-blocking; upload heap/allocator/list pooling and queue-to-queue synchronization are pending.
- Managed scheduler remains sequential; native graphics frames-in-flight alone is not the final asynchronous pipeline.
- RAW concrete LibRaw/RAWtoACES backend is pending.
- EXIF DateTimeOriginal/orientation ingestion is pending in concrete RAW path.
- OCIO/ACES/sRGB transfer handling is pending; FP16 alone is not color management.
- Video VFR/NVDEC path is pending.
- Runtime pixel gate must be executed on actual D3D12 hardware; GitHub compile runners prove compilation only unless hardware execution is explicitly available.
- Full 10/50/120+ real-media stress is blocked until NVENC and target ownership are complete.

## Release rule
No user installer is considered validated while any P0 runtime/encoder ownership item above remains open. Compile SUCCESS is necessary but insufficient.
