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
    ComPtr<ID3D12RootSignature> rootSignature;
    ComPtr<ID3D12PipelineState> pipelineState;
    ComPtr<ID3D12DescriptorHeap> rtvHeap;
    ComPtr<ID3D12DescriptorHeap> srvHeap;
    UINT rtvStride = 0;
    UINT srvStride = 0;
    UINT nextSrv = 0;
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
    D3D12_DESCRIPTOR_HEAP_DESC rh{}; rh.Type=D3D12_DESCRIPTOR_HEAP_TYPE_RTV; rh.NumDescriptors=1;
    if(FAILED(ctx->device->CreateDescriptorHeap(&rh,IID_PPV_ARGS(&ctx->rtvHeap)))) { CloseHandle(ctx->fenceEvent); delete ctx; return nullptr; }
    ctx->rtvStride=ctx->device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
    D3D12_DESCRIPTOR_HEAP_DESC sh{}; sh.Type=D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV; sh.NumDescriptors=4096; sh.Flags=D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE;
    if(FAILED(ctx->device->CreateDescriptorHeap(&sh,IID_PPV_ARGS(&ctx->srvHeap)))) { CloseHandle(ctx->fenceEvent); delete ctx; return nullptr; }
    ctx->srvStride=ctx->device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
    for(UINT i=0;i<NativeContext::FrameCount;++i) if(FAILED(ctx->device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,IID_PPV_ARGS(&ctx->frameAllocators[i])))){CloseHandle(ctx->fenceEvent);delete ctx;return nullptr;}
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
    if(ctx->nextSrv>=4096){delete texture;return nullptr;}
    D3D12_SHADER_RESOURCE_VIEW_DESC sv{}; sv.Format=DXGI_FORMAT_R8G8B8A8_UNORM; sv.ViewDimension=D3D12_SRV_DIMENSION_TEXTURE2D; sv.Shader4ComponentMapping=D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING; sv.Texture2D.MipLevels=1;
    auto cpu=ctx->srvHeap->GetCPUDescriptorHandleForHeapStart(); cpu.ptr+=static_cast<SIZE_T>(ctx->nextSrv)*ctx->srvStride;
    ctx->device->CreateShaderResourceView(texture->Get(),&sv,cpu);
    ctx->srvByResource[texture->Get()]=ctx->nextSrv;
    ++ctx->nextSrv;
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

MOTOR2_API void motor2_d3d12_release_resource(void* resource) { delete static_cast<ComPtr<ID3D12Resource>*>(resource); }



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
float4 PS(VSOut i):SV_TARGET { return tex0.Sample(samp0,i.uv)*r1.w; })";
    ComPtr<ID3DBlob> vs, ps, err;
    HRESULT hr=D3DCompile(shader,strlen(shader),nullptr,nullptr,nullptr,"VS","vs_5_1",0,0,&vs,&err); if(FAILED(hr)) return hr;
    hr=D3DCompile(shader,strlen(shader),nullptr,nullptr,nullptr,"PS","ps_5_1",0,0,&ps,&err); if(FAILED(hr)) return hr;
    D3D12_DESCRIPTOR_RANGE range{}; range.RangeType=D3D12_DESCRIPTOR_RANGE_TYPE_SRV; range.NumDescriptors=1; range.BaseShaderRegister=0;
    D3D12_ROOT_PARAMETER params[2]{}; params[0].ParameterType=D3D12_ROOT_PARAMETER_TYPE_32BIT_CONSTANTS; params[0].Constants.Num32BitValues=8; params[0].ShaderVisibility=D3D12_SHADER_VISIBILITY_VERTEX; params[1].ParameterType=D3D12_ROOT_PARAMETER_TYPE_DESCRIPTOR_TABLE; params[1].DescriptorTable.NumDescriptorRanges=1; params[1].DescriptorTable.pDescriptorRanges=&range; params[1].ShaderVisibility=D3D12_SHADER_VISIBILITY_PIXEL;
    D3D12_STATIC_SAMPLER_DESC samp{}; samp.Filter=D3D12_FILTER_MIN_MAG_MIP_LINEAR; samp.AddressU=samp.AddressV=samp.AddressW=D3D12_TEXTURE_ADDRESS_MODE_CLAMP; samp.ShaderVisibility=D3D12_SHADER_VISIBILITY_PIXEL;
    D3D12_ROOT_SIGNATURE_DESC rs{}; rs.NumParameters=2; rs.pParameters=params; rs.NumStaticSamplers=1; rs.pStaticSamplers=&samp; rs.Flags=D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT;
    ComPtr<ID3DBlob> sig; hr=D3D12SerializeRootSignature(&rs,D3D_ROOT_SIGNATURE_VERSION_1,&sig,&err); if(FAILED(hr)) return hr; hr=ctx->device->CreateRootSignature(0,sig->GetBufferPointer(),sig->GetBufferSize(),IID_PPV_ARGS(&ctx->rootSignature)); if(FAILED(hr)) return hr;
    D3D12_GRAPHICS_PIPELINE_STATE_DESC p{}; p.pRootSignature=ctx->rootSignature.Get(); p.VS={vs->GetBufferPointer(),vs->GetBufferSize()}; p.PS={ps->GetBufferPointer(),ps->GetBufferSize()}; p.BlendState.AlphaToCoverageEnable=FALSE; p.BlendState.IndependentBlendEnable=FALSE; auto& rt=p.BlendState.RenderTarget[0]; rt.BlendEnable=TRUE; rt.SrcBlend=D3D12_BLEND_SRC_ALPHA; rt.DestBlend=D3D12_BLEND_INV_SRC_ALPHA; rt.BlendOp=D3D12_BLEND_OP_ADD; rt.SrcBlendAlpha=D3D12_BLEND_ONE; rt.DestBlendAlpha=D3D12_BLEND_INV_SRC_ALPHA; rt.BlendOpAlpha=D3D12_BLEND_OP_ADD; rt.RenderTargetWriteMask=D3D12_COLOR_WRITE_ENABLE_ALL; p.SampleMask=UINT_MAX; p.RasterizerState.FillMode=D3D12_FILL_MODE_SOLID; p.RasterizerState.CullMode=D3D12_CULL_MODE_NONE; p.RasterizerState.DepthClipEnable=TRUE; p.DepthStencilState.DepthEnable=FALSE; p.DepthStencilState.StencilEnable=FALSE; p.PrimitiveTopologyType=D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE; p.NumRenderTargets=1; p.RTVFormats[0]=DXGI_FORMAT_R16G16B16A16_FLOAT; p.SampleDesc.Count=1;
    return ctx->device->CreateGraphicsPipelineState(&p,IID_PPV_ARGS(&ctx->pipelineState));
}

MOTOR2_API void* motor2_d3d12_create_render_target(void* context, std::uint32_t width, std::uint32_t height) {
    auto* ctx=static_cast<NativeContext*>(context); if(!ctx||!width||!height) return nullptr;
    auto* target=new ComPtr<ID3D12Resource>();
    D3D12_HEAP_PROPERTIES heap{}; heap.Type=D3D12_HEAP_TYPE_DEFAULT;
    D3D12_RESOURCE_DESC d{}; d.Dimension=D3D12_RESOURCE_DIMENSION_TEXTURE2D; d.Width=width; d.Height=height; d.DepthOrArraySize=1; d.MipLevels=1; d.Format=DXGI_FORMAT_R16G16B16A16_FLOAT; d.SampleDesc.Count=1; d.Layout=D3D12_TEXTURE_LAYOUT_UNKNOWN; d.Flags=D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET;
    D3D12_CLEAR_VALUE clear{}; clear.Format=d.Format; clear.Color[3]=1.0f;
    if(FAILED(ctx->device->CreateCommittedResource(&heap,D3D12_HEAP_FLAG_NONE,&d,D3D12_RESOURCE_STATE_RENDER_TARGET,&clear,IID_PPV_ARGS(target->ReleaseAndGetAddressOf())))){delete target;return nullptr;}
    ctx->device->CreateRenderTargetView(target->Get(),nullptr,ctx->rtvHeap->GetCPUDescriptorHandleForHeapStart());
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
    auto* tgt=static_cast<ComPtr<ID3D12Resource>*>(target);
    const UINT slot=ctx->frameIndex++ % NativeContext::FrameCount; const UINT64 pending=ctx->frameFence[slot];
    if(pending && ctx->fence->GetCompletedValue()<pending){if(FAILED(hr=ctx->fence->SetEventOnCompletion(pending,ctx->fenceEvent))) return hr; WaitForSingleObject(ctx->fenceEvent,INFINITE);}
    auto* alloc=ctx->frameAllocators[slot].Get(); if(FAILED(hr=alloc->Reset())) return hr;
    ComPtr<ID3D12GraphicsCommandList> list;
    if(FAILED(hr=ctx->device->CreateCommandList(0,D3D12_COMMAND_LIST_TYPE_DIRECT,alloc,ctx->pipelineState.Get(),IID_PPV_ARGS(&list)))) return hr;
    auto rtv=ctx->rtvHeap->GetCPUDescriptorHandleForHeapStart(); list->OMSetRenderTargets(1,&rtv,FALSE,nullptr);
    auto td=(*tgt)->GetDesc(); D3D12_VIEWPORT vp{0,0,static_cast<float>(td.Width),static_cast<float>(td.Height),0,1}; D3D12_RECT sc{0,0,static_cast<LONG>(td.Width),static_cast<LONG>(td.Height)}; list->RSSetViewports(1,&vp); list->RSSetScissorRects(1,&sc);
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
