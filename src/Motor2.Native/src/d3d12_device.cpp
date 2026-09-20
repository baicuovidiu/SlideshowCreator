#include "motor2_native.h"
#include <windows.h>
#include <dxgi1_6.h>
#include <d3d12.h>
#include <wrl/client.h>
#include <algorithm>
#include <cwchar>
#include <cstring>

using Microsoft::WRL::ComPtr;

struct NativeContext {
    ComPtr<IDXGIFactory6> factory;
    ComPtr<IDXGIAdapter1> adapter;
    ComPtr<ID3D12Device> device;
    ComPtr<ID3D12CommandQueue> directQueue;
    ComPtr<ID3D12CommandQueue> copyQueue;
    ComPtr<ID3D12Fence> fence;
    UINT64 fenceValue = 0;
    HANDLE fenceEvent = nullptr;
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
    ctx->fenceEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (!ctx->fenceEvent) { delete ctx; return nullptr; }
    return ctx;
}

MOTOR2_API void motor2_d3d12_destroy(void* context) {
    auto* ctx = static_cast<NativeContext*>(context);
    if (ctx && ctx->fenceEvent) CloseHandle(ctx->fenceEvent);
    delete ctx;
}

MOTOR2_API void* motor2_d3d12_create_texture_rgba8(void* context, std::uint32_t width, std::uint32_t height) {
    auto* ctx = static_cast<NativeContext*>(context);
    if (!ctx || !width || !height) return nullptr;
    auto* texture = new ComPtr<ID3D12Resource>();
    D3D12_HEAP_PROPERTIES heap{}; heap.Type = D3D12_HEAP_TYPE_DEFAULT;
    D3D12_RESOURCE_DESC d{}; d.Dimension=D3D12_RESOURCE_DIMENSION_TEXTURE2D; d.Width=width; d.Height=height; d.DepthOrArraySize=1; d.MipLevels=1; d.Format=DXGI_FORMAT_R8G8B8A8_UNORM; d.SampleDesc.Count=1; d.Layout=D3D12_TEXTURE_LAYOUT_UNKNOWN;
    if (FAILED(ctx->device->CreateCommittedResource(&heap,D3D12_HEAP_FLAG_NONE,&d,D3D12_RESOURCE_STATE_COPY_DEST,nullptr,IID_PPV_ARGS(texture->ReleaseAndGetAddressOf())))) { delete texture; return nullptr; }
    return texture;
}

MOTOR2_API int motor2_d3d12_upload_rgba8(void* context, void* resource, const void* pixels, std::uint32_t rowPitch, std::uint32_t height) {
    auto* ctx=static_cast<NativeContext*>(context); auto* tex=static_cast<ComPtr<ID3D12Resource>*>(resource);
    if(!ctx||!tex||!pixels||!rowPitch||!height) return E_INVALIDARG;
    D3D12_RESOURCE_DESC td=(*tex)->GetDesc(); D3D12_PLACED_SUBRESOURCE_FOOTPRINT fp{}; UINT rows=0; UINT64 rowBytes=0,total=0;
    ctx->device->GetCopyableFootprints(&td,0,1,0,&fp,&rows,&rowBytes,&total);
    ComPtr<ID3D12Resource> upload; D3D12_HEAP_PROPERTIES hp{}; hp.Type=D3D12_HEAP_TYPE_UPLOAD; D3D12_RESOURCE_DESC bd{}; bd.Dimension=D3D12_RESOURCE_DIMENSION_BUFFER; bd.Width=total; bd.Height=1; bd.DepthOrArraySize=1; bd.MipLevels=1; bd.SampleDesc.Count=1; bd.Layout=D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
    HRESULT hr=ctx->device->CreateCommittedResource(&hp,D3D12_HEAP_FLAG_NONE,&bd,D3D12_RESOURCE_STATE_GENERIC_READ,nullptr,IID_PPV_ARGS(&upload)); if(FAILED(hr)) return hr;
    std::uint8_t* dst=nullptr; D3D12_RANGE empty{0,0}; hr=upload->Map(0,&empty,reinterpret_cast<void**>(&dst)); if(FAILED(hr)) return hr;
    auto* src=static_cast<const std::uint8_t*>(pixels); UINT copyRows=std::min<UINT>(rows,height); UINT copyBytes=std::min<UINT>(static_cast<UINT>(rowBytes),rowPitch);
    for(UINT y=0;y<copyRows;++y) std::memcpy(dst+fp.Offset+static_cast<size_t>(y)*fp.Footprint.RowPitch,src+static_cast<size_t>(y)*rowPitch,copyBytes); upload->Unmap(0,nullptr);
    ComPtr<ID3D12CommandAllocator> alloc; ComPtr<ID3D12GraphicsCommandList> list; if(FAILED(hr=ctx->device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_COPY,IID_PPV_ARGS(&alloc)))) return hr; if(FAILED(hr=ctx->device->CreateCommandList(0,D3D12_COMMAND_LIST_TYPE_COPY,alloc.Get(),nullptr,IID_PPV_ARGS(&list)))) return hr;
    D3D12_TEXTURE_COPY_LOCATION s{}; s.pResource=upload.Get(); s.Type=D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT; s.PlacedFootprint=fp; D3D12_TEXTURE_COPY_LOCATION d{}; d.pResource=tex->Get(); d.Type=D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX; d.SubresourceIndex=0; list->CopyTextureRegion(&d,0,0,0,&s,nullptr); if(FAILED(hr=list->Close())) return hr; ID3D12CommandList* lists[]={list.Get()}; ctx->copyQueue->ExecuteCommandLists(1,lists);
    UINT64 fv=++ctx->fenceValue; if(FAILED(hr=ctx->copyQueue->Signal(ctx->fence.Get(),fv))) return hr; if(ctx->fence->GetCompletedValue()<fv){ if(FAILED(hr=ctx->fence->SetEventOnCompletion(fv,ctx->fenceEvent))) return hr; WaitForSingleObject(ctx->fenceEvent,INFINITE); } return S_OK;
}

MOTOR2_API void motor2_d3d12_release_resource(void* resource) { delete static_cast<ComPtr<ID3D12Resource>*>(resource); }

