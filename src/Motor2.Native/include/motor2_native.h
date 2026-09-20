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
