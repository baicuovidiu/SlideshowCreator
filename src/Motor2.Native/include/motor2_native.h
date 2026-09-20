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
MOTOR2_API void motor2_d3d12_release_resource(void* resource);
