#pragma once
#include <cstdint>

#ifdef _WIN32
#define MOTOR2_API extern "C" __declspec(dllexport)
#else
#define MOTOR2_API extern "C"
#endif

struct Motor2AdapterInfo {
    wchar_t name[128];
    std::uint64_t dedicatedVideoMemory;
    std::uint32_t featureLevel;
};

MOTOR2_API int motor2_d3d12_probe(Motor2AdapterInfo* info);
MOTOR2_API void* motor2_d3d12_create();
MOTOR2_API void motor2_d3d12_destroy(void* context);
MOTOR2_API void* motor2_d3d12_create_texture_rgba8(void* context, std::uint32_t width, std::uint32_t height);
MOTOR2_API int motor2_d3d12_upload_rgba8(void* context, void* texture, const void* pixels, std::uint32_t rowPitch, std::uint32_t height);
MOTOR2_API void motor2_d3d12_release_resource(void* context, void* resource);
MOTOR2_API void motor2_d3d12_release_render_target(void* context, void* target);

struct Motor2DrawQuad {
    void* texture;
    std::uint32_t srvIndex;
    float m11, m12, m21, m22, m31, m32;
    float opacity;
    float z;
};

MOTOR2_API void* motor2_d3d12_create_render_target(void* context, std::uint32_t width, std::uint32_t height);
MOTOR2_API int motor2_d3d12_begin_frame(void* context, void* target);
MOTOR2_API int motor2_d3d12_draw_quads(void* context, void* target, const Motor2DrawQuad* commands, std::uint32_t count);
MOTOR2_API int motor2_d3d12_end_frame(void* context, void* target);
MOTOR2_API int motor2_d3d12_readback_rgba16f(void* context, void* target, void* destination, std::uint32_t destinationBytes);
