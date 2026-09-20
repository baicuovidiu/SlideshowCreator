#include "motor2_native.h"
#include <windows.h>
#include <dxgi1_6.h>
#include <d3d12.h>
#include <wrl/client.h>
#include <algorithm>
#include <cwchar>

using Microsoft::WRL::ComPtr;

struct NativeContext {
    ComPtr<IDXGIFactory6> factory;
    ComPtr<IDXGIAdapter1> adapter;
    ComPtr<ID3D12Device> device;
    ComPtr<ID3D12CommandQueue> directQueue;
    ComPtr<ID3D12CommandQueue> copyQueue;
    ComPtr<ID3D12Fence> fence;
};

static HRESULT SelectAdapter(IDXGIFactory6* factory, IDXGIAdapter1** selected) {
    for (UINT i = 0;; ++i) {
        ComPtr<IDXGIAdapter1> adapter;
        if (factory->EnumAdapterByGpuPreference(i, DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE,
            IID_PPV_ARGS(&adapter)) == DXGI_ERROR_NOT_FOUND) break;
        DXGI_ADAPTER_DESC1 desc{};
        adapter->GetDesc1(&desc);
        if (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) continue;
        if (SUCCEEDED(D3D12CreateDevice(adapter.Get(), D3D_FEATURE_LEVEL_12_0,
            __uuidof(ID3D12Device), nullptr))) {
            *selected = adapter.Detach();
            return S_OK;
        }
    }
    return DXGI_ERROR_NOT_FOUND;
}

MOTOR2_API int motor2_d3d12_probe(Motor2AdapterInfo* info) {
    if (!info) return E_POINTER;
    ComPtr<IDXGIFactory6> factory;
    if (FAILED(CreateDXGIFactory2(0, IID_PPV_ARGS(&factory)))) return E_FAIL;
    ComPtr<IDXGIAdapter1> adapter;
    if (FAILED(SelectAdapter(factory.Get(), &adapter))) return E_FAIL;
    DXGI_ADAPTER_DESC1 desc{};
    if (FAILED(adapter->GetDesc1(&desc))) return E_FAIL;
    std::wmemset(info->name, 0, 128);
    std::wcsncpy(info->name, desc.Description, 127);
    info->dedicatedVideoMemory = desc.DedicatedVideoMemory;
    info->featureLevel = 0xC000;
    return S_OK;
}

MOTOR2_API void* motor2_d3d12_create() {
    auto* ctx = new NativeContext();
    if (FAILED(CreateDXGIFactory2(0, IID_PPV_ARGS(&ctx->factory)))) { delete ctx; return nullptr; }
    if (FAILED(SelectAdapter(ctx->factory.Get(), &ctx->adapter))) { delete ctx; return nullptr; }
    if (FAILED(D3D12CreateDevice(ctx->adapter.Get(), D3D_FEATURE_LEVEL_12_0, IID_PPV_ARGS(&ctx->device)))) { delete ctx; return nullptr; }
    D3D12_COMMAND_QUEUE_DESC q{};
    q.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;
    if (FAILED(ctx->device->CreateCommandQueue(&q, IID_PPV_ARGS(&ctx->directQueue)))) { delete ctx; return nullptr; }
    q.Type = D3D12_COMMAND_LIST_TYPE_COPY;
    if (FAILED(ctx->device->CreateCommandQueue(&q, IID_PPV_ARGS(&ctx->copyQueue)))) { delete ctx; return nullptr; }
    if (FAILED(ctx->device->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&ctx->fence)))) { delete ctx; return nullptr; }
    return ctx;
}

MOTOR2_API void motor2_d3d12_destroy(void* context) {
    delete static_cast<NativeContext*>(context);
}
