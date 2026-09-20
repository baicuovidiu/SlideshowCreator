# Motor 2.0 — Foundation Contracts

The engine is split into policy-free execution interfaces so artistic logic cannot force inefficient media processing.

## Core contracts
- IMediaProbe: immutable technical/metadata inspection.
- IAssetCatalog: fingerprinted asset records and cache state.
- IDecodeService: asynchronous still/RAW/video frame requests at requested resolution/quality.
- IResourceCache: SSD/RAM/VRAM residency with budgets and eviction.
- ISceneGraph: retained objects and groups; immutable evaluated frame state.
- IChoreographyEngine: deterministic scene generation from assets + music events + seed.
- IConstraintEngine: validates/regenerates choreography without changing originals.
- IFramePlanner: converts evaluated scene into resource/compute/draw dependencies.
- IGraphicsBackend: compositor abstraction; D3D12 is first backend.
- IVideoDecodeBackend / IVideoEncodeBackend: NVDEC/NVENC first when supported; fallback selected by capabilities.
- IAudioAnalysisService: waveform/musical-event extraction.
- IHardwareProfiler: capability probe + benchmark.
- IRenderScheduler: queues I/O/decode/upload/compute/graphics/encode asynchronously.
- IRenderTelemetry: per-stage timing and bottleneck evidence.

## Invariants
1. Still assets are not transcoded into temporary videos.
2. Decode size follows required visual footprint; full RAW decode is not mandatory for a tiny wall tile.
3. A decoded/prepared static asset is reusable across frames.
4. Preview and export evaluate the same Scene Graph; only quality policy differs.
5. GPU-resident resources stay GPU-resident until a measured reason requires transfer.
6. Choreography has no FFmpeg filter-graph dependency.
7. Every backend is capability-selected at runtime.
8. No engine layer may crop an asset implicitly.
9. Export is deterministic for project state + random seed.
10. Telemetry is part of the engine contract, not debug decoration.

## First engineering gate
Before building the full UI, prove with a benchmark spike:
- probe one ARW and the FX30 sample;
- prepare reusable GPU-ready still resources;
- compose many independent textured quads;
- keep dynamic transforms/Z-order independent from decoding;
- encode a synthetic/real scene through hardware path;
- record decode/upload/compose/encode timings.
Only after this gate passes do we expand choreography and UI.