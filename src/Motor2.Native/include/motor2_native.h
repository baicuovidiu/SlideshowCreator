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
MOTOR2_API void* motor2_d3d12_create_nvenc_bgra_target(void* context, std::uint32_t width, std::uint32_t height);
MOTOR2_API int motor2_d3d12_convert_fp16_to_bgra8(void* context, void* fp16Target, void* bgraTarget);
MOTOR2_API int motor2_d3d12_begin_frame(void* context, void* target);
MOTOR2_API int motor2_d3d12_draw_quads(void* context, void* target, const Motor2DrawQuad* commands, std::uint32_t count);
MOTOR2_API int motor2_d3d12_end_frame(void* context, void* target);
MOTOR2_API int motor2_d3d12_readback_rgba16f(void* context, void* target, void* destination, std::uint32_t destinationBytes);


struct Motor2NvencProbeInfo {
    std::uint32_t apiVersion;
    std::uint32_t maxSupportedVersion;
};
MOTOR2_API int motor2_nvenc_probe(Motor2NvencProbeInfo* info);

struct Motor2NvencSessionSettings { std::uint32_t width,height,fpsNum,fpsDen,bitrate; };
MOTOR2_API void* motor2_nvenc_open_d3d12(void* d3d12Context, const Motor2NvencSessionSettings* settings);
MOTOR2_API int motor2_nvenc_get_last_open_status();
MOTOR2_API int motor2_nvenc_submit(void* session, void* renderTarget, std::int64_t pts100ns, std::uint64_t* submissionId);
MOTOR2_API int motor2_nvenc_wait(void* session, std::uint64_t submissionId);
MOTOR2_API int motor2_nvenc_drain(void* session);
MOTOR2_API int motor2_nvenc_get_bitstream(void* session, std::uint64_t submissionId, void* destination, std::uint32_t capacity, std::uint32_t* written);
MOTOR2_API void motor2_nvenc_close(void* session);
