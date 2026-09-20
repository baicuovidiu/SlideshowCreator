#include "motor2_native.h"
#include <windows.h>
#include <dxgi1_6.h>
#include <d3d12.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <algorithm>
#include <cwchar>
#include <cstring>
#include <unordered_map>
#include <vector>
#ifdef MOTOR2_HAS_NVENC_SDK
#include <nvEncodeAPI.h>
#endif

using Microsoft::WRL::ComPtr;

struct NativeRenderTarget {
    ComPtr<ID3D12Resource> resource;
    ComPtr<ID3D12DescriptorHeap> rtvHeap;
    std::uint32_t width=0,height=0;
};

struct NativeContext {
    ComPtr<IDXGIFactory6> factory;
    ComPtr<IDXGIAdapter1> adapter;
    ComPtr<ID3D12Device> device;
    ComPtr<ID3D12CommandQueue> directQueue;
    ComPtr<ID3D12CommandQueue> copyQueue;
    ComPtr<ID3D12Fence> fence;
    UINT64 fenceValue = 0;
    HANDLE fenceEvent = nullptr;
    ComPtr<ID3D12RootSignature> rootSignature;
    ComPtr<ID3D12PipelineState> pipelineState;
    ComPtr<ID3D12DescriptorHeap> srvHeap;
    UINT srvStride = 0;
    UINT nextSrv = 0;
    std::vector<UINT> freeSrv;
    std::unordered_map<ID3D12Resource*, UINT> srvByResource;
    static constexpr UINT FrameCount = 3;
    ComPtr<ID3D12CommandAllocator> frameAllocators[FrameCount];
    UINT64 frameFence[FrameCount]{};
    UINT frameIndex = 0;
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
    D3D12_DESCRIPTOR_HEAP_DESC sh{}; sh.Type=D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV; sh.NumDescriptors=4096; sh.Flags=D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE;
    if(FAILED(ctx->device->CreateDescriptorHeap(&sh,IID_PPV_ARGS(&ctx->srvHeap)))) { CloseHandle(ctx->fenceEvent); delete ctx; return nullptr; }
    ctx->srvStride=ctx->device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
    for(UINT i=0;i<NativeContext::FrameCount;++i) if(FAILED(ctx->device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,IID_PPV_ARGS(&ctx->frameAllocators[i])))){CloseHandle(ctx->fenceEvent);delete ctx;return nullptr;}
    return ctx;
}

MOTOR2_API void motor2_d3d12_destroy(void* context) {
    auto* ctx = static_cast<NativeContext*>(context);
    if (!ctx) return;
    if (ctx->directQueue && ctx->fence) {
        const UINT64 fv=++ctx->fenceValue;
        if (SUCCEEDED(ctx->directQueue->Signal(ctx->fence.Get(),fv)) && ctx->fence->GetCompletedValue()<fv && ctx->fenceEvent) {
            if (SUCCEEDED(ctx->fence->SetEventOnCompletion(fv,ctx->fenceEvent))) WaitForSingleObject(ctx->fenceEvent,INFINITE);
        }
    }
    if (ctx->fenceEvent) CloseHandle(ctx->fenceEvent);
    delete ctx;
}

MOTOR2_API void* motor2_d3d12_create_texture_rgba8(void* context, std::uint32_t width, std::uint32_t height) {
    auto* ctx = static_cast<NativeContext*>(context);
    if (!ctx || !width || !height) return nullptr;
    auto* texture = new ComPtr<ID3D12Resource>();
    D3D12_HEAP_PROPERTIES heap{}; heap.Type = D3D12_HEAP_TYPE_DEFAULT;
    D3D12_RESOURCE_DESC d{}; d.Dimension=D3D12_RESOURCE_DIMENSION_TEXTURE2D; d.Width=width; d.Height=height; d.DepthOrArraySize=1; d.MipLevels=1; d.Format=DXGI_FORMAT_R8G8B8A8_UNORM; d.SampleDesc.Count=1; d.Layout=D3D12_TEXTURE_LAYOUT_UNKNOWN;
    if (FAILED(ctx->device->CreateCommittedResource(&heap,D3D12_HEAP_FLAG_NONE,&d,D3D12_RESOURCE_STATE_COPY_DEST,nullptr,IID_PPV_ARGS(texture->ReleaseAndGetAddressOf())))) { delete texture; return nullptr; }
    UINT srvIndex=0;
    if(!ctx->freeSrv.empty()){srvIndex=ctx->freeSrv.back();ctx->freeSrv.pop_back();}
    else { if(ctx->nextSrv>=4096){delete texture;return nullptr;} srvIndex=ctx->nextSrv++; }
    D3D12_SHADER_RESOURCE_VIEW_DESC sv{}; sv.Format=DXGI_FORMAT_R8G8B8A8_UNORM; sv.ViewDimension=D3D12_SRV_DIMENSION_TEXTURE2D; sv.Shader4ComponentMapping=D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING; sv.Texture2D.MipLevels=1;
    auto cpu=ctx->srvHeap->GetCPUDescriptorHandleForHeapStart(); cpu.ptr+=static_cast<SIZE_T>(srvIndex)*ctx->srvStride;
    ctx->device->CreateShaderResourceView(texture->Get(),&sv,cpu);
    ctx->srvByResource[texture->Get()]=srvIndex;
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
    UINT64 fv=++ctx->fenceValue; if(FAILED(hr=ctx->copyQueue->Signal(ctx->fence.Get(),fv))) return hr; if(ctx->fence->GetCompletedValue()<fv){ if(FAILED(hr=ctx->fence->SetEventOnCompletion(fv,ctx->fenceEvent))) return hr; WaitForSingleObject(ctx->fenceEvent,INFINITE); }
    ComPtr<ID3D12CommandAllocator> ta; ComPtr<ID3D12GraphicsCommandList> tl;
    if(FAILED(hr=ctx->device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,IID_PPV_ARGS(&ta)))) return hr;
    if(FAILED(hr=ctx->device->CreateCommandList(0,D3D12_COMMAND_LIST_TYPE_DIRECT,ta.Get(),nullptr,IID_PPV_ARGS(&tl)))) return hr;
    D3D12_RESOURCE_BARRIER b{}; b.Type=D3D12_RESOURCE_BARRIER_TYPE_TRANSITION; b.Transition.pResource=tex->Get(); b.Transition.StateBefore=D3D12_RESOURCE_STATE_COPY_DEST; b.Transition.StateAfter=D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE; b.Transition.Subresource=D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES; tl->ResourceBarrier(1,&b);
    if(FAILED(hr=tl->Close())) return hr; ID3D12CommandList* trans[]={tl.Get()}; ctx->directQueue->ExecuteCommandLists(1,trans);
    UINT64 tf=++ctx->fenceValue; if(FAILED(hr=ctx->directQueue->Signal(ctx->fence.Get(),tf))) return hr; if(ctx->fence->GetCompletedValue()<tf){if(FAILED(hr=ctx->fence->SetEventOnCompletion(tf,ctx->fenceEvent))) return hr; WaitForSingleObject(ctx->fenceEvent,INFINITE);} return S_OK;
}

static void WaitForGpu(NativeContext* ctx) {
    if(!ctx||!ctx->directQueue||!ctx->fence) return;
    const UINT64 fv=++ctx->fenceValue;
    if(SUCCEEDED(ctx->directQueue->Signal(ctx->fence.Get(),fv)) && ctx->fence->GetCompletedValue()<fv && ctx->fenceEvent && SUCCEEDED(ctx->fence->SetEventOnCompletion(fv,ctx->fenceEvent))) WaitForSingleObject(ctx->fenceEvent,INFINITE);
}

MOTOR2_API void motor2_d3d12_release_resource(void* context, void* resource) {
    auto* ctx=static_cast<NativeContext*>(context); auto* tex=static_cast<ComPtr<ID3D12Resource>*>(resource); if(!tex)return;
    WaitForGpu(ctx);
    if(ctx && tex->Get()){auto it=ctx->srvByResource.find(tex->Get()); if(it!=ctx->srvByResource.end()){ctx->freeSrv.push_back(it->second);ctx->srvByResource.erase(it);}}
    delete tex;
}

MOTOR2_API void motor2_d3d12_release_render_target(void* context, void* target) {
    auto* ctx=static_cast<NativeContext*>(context); auto* rt=static_cast<NativeRenderTarget*>(target); if(!rt)return; WaitForGpu(ctx); delete rt;
}



static HRESULT EnsureQuadPipeline(NativeContext* ctx) {
    if (ctx->pipelineState) return S_OK;
    const char* shader = R"(
struct VSOut { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
cbuffer DrawCB : register(b0) { float4 r0; float4 r1; };
VSOut VS(uint id:SV_VertexID) {
    float2 p[6]={{-1,-1},{-1,1},{1,1},{-1,-1},{1,1},{1,-1}};
    float2 uv[6]={{0,1},{0,0},{1,0},{0,1},{1,0},{1,1}};
    float2 q=p[id]; VSOut o;
    o.pos=float4(q.x*r0.x+q.y*r0.z+r1.x, q.x*r0.y+q.y*r0.w+r1.y, r1.z, 1);
    o.uv=uv[id]; return o;
}
Texture2D tex0:register(t0); SamplerState samp0:register(s0);
float4 PS(VSOut i):SV_TARGET { float4 c=tex0.Sample(samp0,i.uv); return float4(c.rgb,c.a*r1.w); })";
    ComPtr<ID3DBlob> vs, ps, err;
    HRESULT hr=D3DCompile(shader,strlen(shader),nullptr,nullptr,nullptr,"VS","vs_5_1",0,0,&vs,&err); if(FAILED(hr)) return hr;
    hr=D3DCompile(shader,strlen(shader),nullptr,nullptr,nullptr,"PS","ps_5_1",0,0,&ps,&err); if(FAILED(hr)) return hr;
    D3D12_DESCRIPTOR_RANGE range{}; range.RangeType=D3D12_DESCRIPTOR_RANGE_TYPE_SRV; range.NumDescriptors=1; range.BaseShaderRegister=0;
    D3D12_ROOT_PARAMETER params[2]{}; params[0].ParameterType=D3D12_ROOT_PARAMETER_TYPE_32BIT_CONSTANTS; params[0].Constants.Num32BitValues=8; params[0].ShaderVisibility=D3D12_SHADER_VISIBILITY_ALL; params[1].ParameterType=D3D12_ROOT_PARAMETER_TYPE_DESCRIPTOR_TABLE; params[1].DescriptorTable.NumDescriptorRanges=1; params[1].DescriptorTable.pDescriptorRanges=&range; params[1].ShaderVisibility=D3D12_SHADER_VISIBILITY_PIXEL;
    D3D12_STATIC_SAMPLER_DESC samp{}; samp.Filter=D3D12_FILTER_MIN_MAG_MIP_LINEAR; samp.AddressU=samp.AddressV=samp.AddressW=D3D12_TEXTURE_ADDRESS_MODE_CLAMP; samp.ShaderVisibility=D3D12_SHADER_VISIBILITY_PIXEL;
    D3D12_ROOT_SIGNATURE_DESC rs{}; rs.NumParameters=2; rs.pParameters=params; rs.NumStaticSamplers=1; rs.pStaticSamplers=&samp; rs.Flags=D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT;
    ComPtr<ID3DBlob> sig; hr=D3D12SerializeRootSignature(&rs,D3D_ROOT_SIGNATURE_VERSION_1,&sig,&err); if(FAILED(hr)) return hr; hr=ctx->device->CreateRootSignature(0,sig->GetBufferPointer(),sig->GetBufferSize(),IID_PPV_ARGS(&ctx->rootSignature)); if(FAILED(hr)) return hr;
    D3D12_GRAPHICS_PIPELINE_STATE_DESC p{}; p.pRootSignature=ctx->rootSignature.Get(); p.VS={vs->GetBufferPointer(),vs->GetBufferSize()}; p.PS={ps->GetBufferPointer(),ps->GetBufferSize()}; p.BlendState.AlphaToCoverageEnable=FALSE; p.BlendState.IndependentBlendEnable=FALSE; auto& rt=p.BlendState.RenderTarget[0]; rt.BlendEnable=TRUE; rt.SrcBlend=D3D12_BLEND_SRC_ALPHA; rt.DestBlend=D3D12_BLEND_INV_SRC_ALPHA; rt.BlendOp=D3D12_BLEND_OP_ADD; rt.SrcBlendAlpha=D3D12_BLEND_ONE; rt.DestBlendAlpha=D3D12_BLEND_INV_SRC_ALPHA; rt.BlendOpAlpha=D3D12_BLEND_OP_ADD; rt.RenderTargetWriteMask=D3D12_COLOR_WRITE_ENABLE_ALL; p.SampleMask=UINT_MAX; p.RasterizerState.FillMode=D3D12_FILL_MODE_SOLID; p.RasterizerState.CullMode=D3D12_CULL_MODE_NONE; p.RasterizerState.DepthClipEnable=TRUE; p.DepthStencilState.DepthEnable=FALSE; p.DepthStencilState.StencilEnable=FALSE; p.PrimitiveTopologyType=D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE; p.NumRenderTargets=1; p.RTVFormats[0]=DXGI_FORMAT_R16G16B16A16_FLOAT; p.SampleDesc.Count=1;
    return ctx->device->CreateGraphicsPipelineState(&p,IID_PPV_ARGS(&ctx->pipelineState));
}

MOTOR2_API void* motor2_d3d12_create_render_target(void* context, std::uint32_t width, std::uint32_t height) {
    auto* ctx=static_cast<NativeContext*>(context); if(!ctx||!width||!height) return nullptr;
    auto* target=new NativeRenderTarget(); target->width=width; target->height=height;
    D3D12_HEAP_PROPERTIES heap{}; heap.Type=D3D12_HEAP_TYPE_DEFAULT;
    D3D12_RESOURCE_DESC d{}; d.Dimension=D3D12_RESOURCE_DIMENSION_TEXTURE2D; d.Width=width; d.Height=height; d.DepthOrArraySize=1; d.MipLevels=1; d.Format=DXGI_FORMAT_R16G16B16A16_FLOAT; d.SampleDesc.Count=1; d.Layout=D3D12_TEXTURE_LAYOUT_UNKNOWN; d.Flags=D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET;
    D3D12_CLEAR_VALUE clear{}; clear.Format=d.Format; clear.Color[3]=1.0f;
    if(FAILED(ctx->device->CreateCommittedResource(&heap,D3D12_HEAP_FLAG_NONE,&d,D3D12_RESOURCE_STATE_RENDER_TARGET,&clear,IID_PPV_ARGS(target->resource.ReleaseAndGetAddressOf())))){delete target;return nullptr;}
    D3D12_DESCRIPTOR_HEAP_DESC rh{}; rh.Type=D3D12_DESCRIPTOR_HEAP_TYPE_RTV; rh.NumDescriptors=1;
    if(FAILED(ctx->device->CreateDescriptorHeap(&rh,IID_PPV_ARGS(&target->rtvHeap)))){delete target;return nullptr;}
    ctx->device->CreateRenderTargetView(target->resource.Get(),nullptr,target->rtvHeap->GetCPUDescriptorHandleForHeapStart());
    return target;
}

MOTOR2_API int motor2_d3d12_begin_frame(void* context, void* target) {
    auto* ctx=static_cast<NativeContext*>(context); if(!ctx||!target) return E_INVALIDARG;
    return EnsureQuadPipeline(ctx);
}

MOTOR2_API int motor2_d3d12_draw_quads(void* context, void* target, const Motor2DrawQuad* commands, std::uint32_t count) {
    if(!context||!target||(count&&!commands)) return E_INVALIDARG;
    auto* ctx=static_cast<NativeContext*>(context);
    HRESULT hr=EnsureQuadPipeline(ctx); if(FAILED(hr)) return hr;
    auto* tgt=static_cast<NativeRenderTarget*>(target);
    const UINT slot=ctx->frameIndex++ % NativeContext::FrameCount; const UINT64 pending=ctx->frameFence[slot];
    if(pending && ctx->fence->GetCompletedValue()<pending){if(FAILED(hr=ctx->fence->SetEventOnCompletion(pending,ctx->fenceEvent))) return hr; WaitForSingleObject(ctx->fenceEvent,INFINITE);}
    auto* alloc=ctx->frameAllocators[slot].Get(); if(FAILED(hr=alloc->Reset())) return hr;
    ComPtr<ID3D12GraphicsCommandList> list;
    if(FAILED(hr=ctx->device->CreateCommandList(0,D3D12_COMMAND_LIST_TYPE_DIRECT,alloc,ctx->pipelineState.Get(),IID_PPV_ARGS(&list)))) return hr;
    auto rtv=tgt->rtvHeap->GetCPUDescriptorHandleForHeapStart(); list->OMSetRenderTargets(1,&rtv,FALSE,nullptr);
    auto td=tgt->resource->GetDesc(); D3D12_VIEWPORT vp{0,0,static_cast<float>(td.Width),static_cast<float>(td.Height),0,1}; D3D12_RECT sc{0,0,static_cast<LONG>(td.Width),static_cast<LONG>(td.Height)}; list->RSSetViewports(1,&vp); list->RSSetScissorRects(1,&sc);
    const float clear[4]={0,0,0,1}; list->ClearRenderTargetView(rtv,clear,0,nullptr);
    list->SetGraphicsRootSignature(ctx->rootSignature.Get()); ID3D12DescriptorHeap* heaps[]={ctx->srvHeap.Get()}; list->SetDescriptorHeaps(1,heaps); list->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    for(std::uint32_t i=0;i<count;++i){
        const auto& q=commands[i]; if(!q.texture) continue;
        float constants[8]={q.m11,q.m12,q.m21,q.m22,q.m31,q.m32,q.z,q.opacity};
        list->SetGraphicsRoot32BitConstants(0,8,constants,0);
        auto* tex=static_cast<ComPtr<ID3D12Resource>*>(q.texture); auto it=ctx->srvByResource.find(tex->Get()); if(it==ctx->srvByResource.end()) continue;
        auto gpu=ctx->srvHeap->GetGPUDescriptorHandleForHeapStart(); gpu.ptr+=static_cast<UINT64>(it->second)*ctx->srvStride;
        list->SetGraphicsRootDescriptorTable(1,gpu); list->DrawInstanced(6,1,0,0);
    }
    if(FAILED(hr=list->Close())) return hr; ID3D12CommandList* lists[]={list.Get()}; ctx->directQueue->ExecuteCommandLists(1,lists);
    UINT64 fv=++ctx->fenceValue; if(FAILED(hr=ctx->directQueue->Signal(ctx->fence.Get(),fv))) return hr; ctx->frameFence[slot]=fv; return S_OK;
}

MOTOR2_API int motor2_d3d12_end_frame(void* context, void* target) {
    return (context&&target) ? S_OK : E_INVALIDARG;
}


MOTOR2_API int motor2_d3d12_readback_rgba16f(void* context, void* target, void* destination, std::uint32_t destinationBytes) {
    auto* ctx=static_cast<NativeContext*>(context); auto* tgt=static_cast<NativeRenderTarget*>(target);
    if(!ctx||!tgt||!destination) return E_INVALIDARG;
    WaitForGpu(ctx);
    auto td=tgt->resource->GetDesc(); D3D12_PLACED_SUBRESOURCE_FOOTPRINT fp{}; UINT rows=0; UINT64 rowBytes=0,total=0;
    ctx->device->GetCopyableFootprints(&td,0,1,0,&fp,&rows,&rowBytes,&total);
    if(destinationBytes < rowBytes*rows) return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
    D3D12_HEAP_PROPERTIES hp{}; hp.Type=D3D12_HEAP_TYPE_READBACK; D3D12_RESOURCE_DESC bd{}; bd.Dimension=D3D12_RESOURCE_DIMENSION_BUFFER; bd.Width=total; bd.Height=1; bd.DepthOrArraySize=1; bd.MipLevels=1; bd.SampleDesc.Count=1; bd.Layout=D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
    ComPtr<ID3D12Resource> rb; HRESULT hr=ctx->device->CreateCommittedResource(&hp,D3D12_HEAP_FLAG_NONE,&bd,D3D12_RESOURCE_STATE_COPY_DEST,nullptr,IID_PPV_ARGS(&rb)); if(FAILED(hr)) return hr;
    ComPtr<ID3D12CommandAllocator> a; ComPtr<ID3D12GraphicsCommandList> l; if(FAILED(hr=ctx->device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,IID_PPV_ARGS(&a)))) return hr; if(FAILED(hr=ctx->device->CreateCommandList(0,D3D12_COMMAND_LIST_TYPE_DIRECT,a.Get(),nullptr,IID_PPV_ARGS(&l)))) return hr;
    D3D12_RESOURCE_BARRIER b{}; b.Type=D3D12_RESOURCE_BARRIER_TYPE_TRANSITION; b.Transition.pResource=tgt->resource.Get(); b.Transition.StateBefore=D3D12_RESOURCE_STATE_RENDER_TARGET; b.Transition.StateAfter=D3D12_RESOURCE_STATE_COPY_SOURCE; b.Transition.Subresource=D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES; l->ResourceBarrier(1,&b);
    D3D12_TEXTURE_COPY_LOCATION s{}; s.pResource=tgt->resource.Get(); s.Type=D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX; D3D12_TEXTURE_COPY_LOCATION d{}; d.pResource=rb.Get(); d.Type=D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT; d.PlacedFootprint=fp; l->CopyTextureRegion(&d,0,0,0,&s,nullptr);
    std::swap(b.Transition.StateBefore,b.Transition.StateAfter); l->ResourceBarrier(1,&b); if(FAILED(hr=l->Close())) return hr; ID3D12CommandList* lists[]={l.Get()}; ctx->directQueue->ExecuteCommandLists(1,lists); WaitForGpu(ctx);
    std::uint8_t* p=nullptr; D3D12_RANGE rr{static_cast<SIZE_T>(fp.Offset),static_cast<SIZE_T>(fp.Offset+total)}; if(FAILED(hr=rb->Map(0,&rr,reinterpret_cast<void**>(&p)))) return hr;
    auto* out=static_cast<std::uint8_t*>(destination); for(UINT y=0;y<rows;++y) std::memcpy(out+static_cast<size_t>(y)*rowBytes,p+fp.Offset+static_cast<size_t>(y)*fp.Footprint.RowPitch,static_cast<size_t>(rowBytes)); D3D12_RANGE wr{0,0}; rb->Unmap(0,&wr); return S_OK;
}

MOTOR2_API int motor2_nvenc_probe(Motor2NvencProbeInfo* info) {
    if(!info) return E_POINTER;
    info->apiVersion = 0; info->maxSupportedVersion = 0;
#ifdef MOTOR2_HAS_NVENC_SDK
    info->apiVersion = NVENCAPI_VERSION;
    HMODULE dll=LoadLibraryW(sizeof(void*)==8 ? L"nvEncodeAPI64.dll" : L"nvEncodeAPI.dll");
    if(!dll) return HRESULT_FROM_WIN32(GetLastError());
    using GetMaxFn = NVENCSTATUS (NVENCAPI*)(uint32_t*);
    auto getMax=reinterpret_cast<GetMaxFn>(GetProcAddress(dll,"NvEncodeAPIGetMaxSupportedVersion"));
    if(!getMax){FreeLibrary(dll);return E_NOINTERFACE;}
    uint32_t maxVersion=0; const auto st=getMax(&maxVersion); FreeLibrary(dll);
    if(st!=NV_ENC_SUCCESS) return E_FAIL;
    info->maxSupportedVersion=maxVersion;
    const uint32_t required=(NVENCAPI_MAJOR_VERSION<<4)|NVENCAPI_MINOR_VERSION;
    return maxVersion>=required ? S_OK : HRESULT_FROM_WIN32(ERROR_OLD_WIN_VERSION);
#else
    return E_NOTIMPL;
#endif
}

#ifdef MOTOR2_HAS_NVENC_SDK
struct NativeNvencSession {
    NV_ENCODE_API_FUNCTION_LIST api{};
    void* encoder=nullptr;
    NativeContext* d3d=nullptr;
    Motor2NvencSessionSettings settings{};
    std::uint64_t nextId=1;
    struct Submission { NV_ENC_REGISTERED_PTR registered=nullptr; NV_ENC_INPUT_PTR mapped=nullptr; NV_ENC_OUTPUT_PTR bitstream=nullptr; bool complete=false; };
    std::unordered_map<std::uint64_t,Submission> submissions;
};
static GUID Motor2H264Preset(){ return NV_ENC_PRESET_P4_GUID; }
#endif

MOTOR2_API void* motor2_nvenc_open_d3d12(void* d3d12Context,const Motor2NvencSessionSettings* settings){
#ifndef MOTOR2_HAS_NVENC_SDK
    (void)d3d12Context;(void)settings; return nullptr;
#else
    auto* d3d=static_cast<NativeContext*>(d3d12Context); if(!d3d||!settings||!settings->width||!settings->height)return nullptr;
    HMODULE dll=LoadLibraryW(L"nvEncodeAPI64.dll"); if(!dll)return nullptr;
    using CreateFn=NVENCSTATUS (NVENCAPI*)(NV_ENCODE_API_FUNCTION_LIST*);
    auto create=reinterpret_cast<CreateFn>(GetProcAddress(dll,"NvEncodeAPICreateInstance")); if(!create){FreeLibrary(dll);return nullptr;}
    auto* s=new NativeNvencSession(); s->d3d=d3d; s->settings=*settings; s->api.version=NV_ENCODE_API_FUNCTION_LIST_VER;
    if(create(&s->api)!=NV_ENC_SUCCESS){delete s;FreeLibrary(dll);return nullptr;}
    NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS op{}; op.version=NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS_VER; op.device=d3d->device.Get(); op.deviceType=NV_ENC_DEVICE_TYPE_DIRECTX; op.apiVersion=NVENCAPI_VERSION;
    if(s->api.nvEncOpenEncodeSessionEx(&op,&s->encoder)!=NV_ENC_SUCCESS){delete s;FreeLibrary(dll);return nullptr;}
    NV_ENC_INITIALIZE_PARAMS ip{}; NV_ENC_CONFIG cfg{}; ip.version=NV_ENC_INITIALIZE_PARAMS_VER; cfg.version=NV_ENC_CONFIG_VER; ip.encodeGUID=NV_ENC_CODEC_H264_GUID; ip.presetGUID=Motor2H264Preset(); ip.encodeWidth=settings->width; ip.encodeHeight=settings->height; ip.darWidth=settings->width; ip.darHeight=settings->height; ip.frameRateNum=settings->fpsNum; ip.frameRateDen=settings->fpsDen?settings->fpsDen:1; ip.enablePTD=1; ip.encodeConfig=&cfg;
    NV_ENC_PRESET_CONFIG pc{}; pc.version=NV_ENC_PRESET_CONFIG_VER; pc.presetCfg.version=NV_ENC_CONFIG_VER;
    if(s->api.nvEncGetEncodePresetConfigEx(s->encoder,ip.encodeGUID,ip.presetGUID,NV_ENC_TUNING_INFO_HIGH_QUALITY,&pc)!=NV_ENC_SUCCESS){s->api.nvEncDestroyEncoder(s->encoder);delete s;FreeLibrary(dll);return nullptr;}
    cfg=pc.presetCfg; cfg.rcParams.rateControlMode=NV_ENC_PARAMS_RC_VBR; cfg.rcParams.averageBitRate=settings->bitrate; cfg.rcParams.maxBitRate=settings->bitrate+settings->bitrate/2; ip.tuningInfo=NV_ENC_TUNING_INFO_HIGH_QUALITY;
    if(s->api.nvEncInitializeEncoder(s->encoder,&ip)!=NV_ENC_SUCCESS){s->api.nvEncDestroyEncoder(s->encoder);delete s;FreeLibrary(dll);return nullptr;}
    return s;
#endif
}
MOTOR2_API int motor2_nvenc_submit(void* session,void* renderTarget,std::int64_t pts100ns,std::uint64_t* submissionId){
#ifndef MOTOR2_HAS_NVENC_SDK
    return E_NOTIMPL;
#else
    auto* s=static_cast<NativeNvencSession*>(session); auto* rt=static_cast<NativeRenderTarget*>(renderTarget); if(!s||!rt||!submissionId)return E_INVALIDARG;
    WaitForGpu(s->d3d);
    // The compositor target is FP16 (R16G16B16A16_FLOAT). NVENC does not accept that surface
    // as an H.264 input resource. Reject it here instead of lying about a direct compatible path.
    // A dedicated GPU conversion target (NV12/P010) is required before registration.
    const auto desc=rt->resource->GetDesc();
    if(desc.Format==DXGI_FORMAT_R16G16B16A16_FLOAT) return HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED);
    NV_ENC_BUFFER_FORMAT fmt=NV_ENC_BUFFER_FORMAT_UNDEFINED;
    if(desc.Format==DXGI_FORMAT_NV12) fmt=NV_ENC_BUFFER_FORMAT_NV12;
    else if(desc.Format==DXGI_FORMAT_P010) fmt=NV_ENC_BUFFER_FORMAT_YUV420_10BIT;
    else if(desc.Format==DXGI_FORMAT_B8G8R8A8_UNORM) fmt=NV_ENC_BUFFER_FORMAT_ARGB;
    else return HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED);
    NV_ENC_REGISTER_RESOURCE rr{}; rr.version=NV_ENC_REGISTER_RESOURCE_VER; rr.resourceType=NV_ENC_INPUT_RESOURCE_TYPE_DIRECTX; rr.resourceToRegister=rt->resource.Get(); rr.width=rt->width; rr.height=rt->height; rr.pitch=0; rr.bufferFormat=fmt; rr.bufferUsage=NV_ENC_INPUT_IMAGE;
    if(s->api.nvEncRegisterResource(s->encoder,&rr)!=NV_ENC_SUCCESS)return E_FAIL;
    NV_ENC_MAP_INPUT_RESOURCE mr{}; mr.version=NV_ENC_MAP_INPUT_RESOURCE_VER; mr.registeredResource=rr.registeredResource; if(s->api.nvEncMapInputResource(s->encoder,&mr)!=NV_ENC_SUCCESS){s->api.nvEncUnregisterResource(s->encoder,rr.registeredResource);return E_FAIL;}
    NV_ENC_CREATE_BITSTREAM_BUFFER bb{}; bb.version=NV_ENC_CREATE_BITSTREAM_BUFFER_VER; if(s->api.nvEncCreateBitstreamBuffer(s->encoder,&bb)!=NV_ENC_SUCCESS){s->api.nvEncUnmapInputResource(s->encoder,mr.mappedResource);s->api.nvEncUnregisterResource(s->encoder,rr.registeredResource);return E_FAIL;}
    NV_ENC_PIC_PARAMS pp{}; pp.version=NV_ENC_PIC_PARAMS_VER; pp.inputBuffer=mr.mappedResource; pp.bufferFmt=mr.mappedBufferFmt; pp.inputWidth=s->settings.width; pp.inputHeight=s->settings.height; pp.outputBitstream=bb.bitstreamBuffer; pp.inputTimeStamp=pts100ns; pp.pictureStruct=NV_ENC_PIC_STRUCT_FRAME;
    auto st=s->api.nvEncEncodePicture(s->encoder,&pp); if(st!=NV_ENC_SUCCESS && st!=NV_ENC_ERR_NEED_MORE_INPUT){s->api.nvEncDestroyBitstreamBuffer(s->encoder,bb.bitstreamBuffer);s->api.nvEncUnmapInputResource(s->encoder,mr.mappedResource);s->api.nvEncUnregisterResource(s->encoder,rr.registeredResource);return E_FAIL;}
    auto id=s->nextId++; s->submissions[id]={rr.registeredResource,mr.mappedResource,bb.bitstreamBuffer,false}; *submissionId=id; return S_OK;
#endif
}
MOTOR2_API int motor2_nvenc_wait(void* session,std::uint64_t submissionId){
#ifndef MOTOR2_HAS_NVENC_SDK
 return E_NOTIMPL;
#else
 auto* s=static_cast<NativeNvencSession*>(session); if(!s)return E_INVALIDARG; auto it=s->submissions.find(submissionId); if(it==s->submissions.end())return E_INVALIDARG;
 NV_ENC_LOCK_BITSTREAM lk{}; lk.version=NV_ENC_LOCK_BITSTREAM_VER; lk.outputBitstream=it->second.bitstream; lk.doNotWait=0; if(s->api.nvEncLockBitstream(s->encoder,&lk)!=NV_ENC_SUCCESS)return E_FAIL; s->api.nvEncUnlockBitstream(s->encoder,it->second.bitstream); it->second.complete=true; return S_OK;
#endif
}
MOTOR2_API int motor2_nvenc_get_bitstream(void* session,std::uint64_t submissionId,void* destination,std::uint32_t capacity,std::uint32_t* written){
#ifndef MOTOR2_HAS_NVENC_SDK
 return E_NOTIMPL;
#else
 auto* s=static_cast<NativeNvencSession*>(session); if(!s||!written)return E_INVALIDARG; auto it=s->submissions.find(submissionId); if(it==s->submissions.end())return E_INVALIDARG;
 NV_ENC_LOCK_BITSTREAM lk{}; lk.version=NV_ENC_LOCK_BITSTREAM_VER; lk.outputBitstream=it->second.bitstream; lk.doNotWait=0; if(s->api.nvEncLockBitstream(s->encoder,&lk)!=NV_ENC_SUCCESS)return E_FAIL; *written=lk.bitstreamSizeInBytes; if(!destination||capacity<*written){s->api.nvEncUnlockBitstream(s->encoder,it->second.bitstream);return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);} std::memcpy(destination,lk.bitstreamBufferPtr,*written); s->api.nvEncUnlockBitstream(s->encoder,it->second.bitstream); it->second.complete=true; return S_OK;
#endif
}
MOTOR2_API int motor2_nvenc_drain(void* session){
#ifndef MOTOR2_HAS_NVENC_SDK
 return E_NOTIMPL;
#else
 auto* s=static_cast<NativeNvencSession*>(session); if(!s)return E_INVALIDARG; NV_ENC_PIC_PARAMS pp{}; pp.version=NV_ENC_PIC_PARAMS_VER; pp.encodePicFlags=NV_ENC_PIC_FLAG_EOS; auto st=s->api.nvEncEncodePicture(s->encoder,&pp); return st==NV_ENC_SUCCESS?S_OK:E_FAIL;
#endif
}
MOTOR2_API void motor2_nvenc_close(void* session){
#ifdef MOTOR2_HAS_NVENC_SDK
 auto* s=static_cast<NativeNvencSession*>(session); if(!s)return; for(auto& kv:s->submissions){auto& x=kv.second;if(x.mapped)s->api.nvEncUnmapInputResource(s->encoder,x.mapped);if(x.registered)s->api.nvEncUnregisterResource(s->encoder,x.registered);if(x.bitstream)s->api.nvEncDestroyBitstreamBuffer(s->encoder,x.bitstream);} if(s->encoder)s->api.nvEncDestroyEncoder(s->encoder); delete s;
#else
 (void)session;
#endif
}
