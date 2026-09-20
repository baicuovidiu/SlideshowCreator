# Motor 2.0 — Gate A Benchmark Protocol

Gate A exists to prevent another long user test of a fundamentally bad pipeline.

## Inputs
Persistent test bank contains 18 assets: 17 Sony ARW stills and one 458,548,763-byte FX30 MP4 excerpt.

## Required measurements
For every run record wall-clock and per-stage time for:
1. metadata probe;
2. RAW decode at thumbnail/1080p/full requirement;
3. video probe + first-frame decode;
4. RAM -> GPU upload;
5. retained-texture reuse over 150 frames;
6. 16/32/64/120 independent textured quads with changing transforms/Z-order;
7. GPU composition;
8. encoder submission/drain;
9. total render.

## Pass conditions before expanding UI
- No still image is transcoded to an intermediate video.
- Static still decode count does not scale with number of output frames.
- CARUSEL transform/Z-order changes do not trigger re-decode.
- Full-photo CONTAIN is preserved.
- A failed capability/decode/encode probe fails before a long render.
- Telemetry identifies the dominant stage.
- The 120-object stress scene completes without graph construction failure.
- Preview and export consume the same evaluated Scene Graph.

## Performance discipline
No target-minute claim until this gate runs on Ovidiu's real machine.
GPU percentage alone is not a success metric; elapsed time and stage timings are.
