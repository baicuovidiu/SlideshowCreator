# Motor 2.0 GPU frame lifetime

Target lifetime for one output frame:

1. SceneGraph.Evaluate(t)
2. FramePlanner computes visible assets + required footprints.
3. RAM cache resolves decoded surfaces.
4. VRAM cache resolves reusable textures.
5. D3D12 compositor emits sorted textured-quad draw commands.
6. D3D12 render target remains GPU-resident.
7. NVENC consumes that GPU surface directly.
8. Surface returns to a render-target pool after encoder fence completion.

Forbidden hot-path operations:
- still -> temporary MP4;
- per-frame RAW/JPEG decode for an unchanged still;
- GPU -> CPU readback merely to feed encoder;
- temporary PNG/JPEG frames;
- rebuilding media files to change X/Y/scale/rotation/Z.

Gate still pending: concrete native D3D12 COM implementation and concrete NVENC SDK session. Until both capability probes succeed, preflight must not describe the hardware path as available.
