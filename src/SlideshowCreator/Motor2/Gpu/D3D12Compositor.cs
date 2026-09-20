using System.Collections.Immutable;
using System.Numerics;

namespace SlideshowCreator.Motor2.Core;

public sealed record GpuDrawCommand(
    AssetId Asset, GpuTextureHandle Texture, Matrix3x2 Transform,
    float Opacity, float Z, PixelSize OutputSize);

public sealed record GpuComposedFrame(
    PixelSize Size, TimeSpan Pts, object NativeSurface,
    ImmutableArray<GpuDrawCommand> Commands);

public interface ID3D12CompositorDevice : ID3D12DeviceContext
{
    ValueTask<object> CreateRenderTargetAsync(PixelSize size,CancellationToken ct);
    ValueTask BeginFrameAsync(object target,CancellationToken ct);
    ValueTask DrawTexturedQuadAsync(object target,GpuDrawCommand command,CancellationToken ct);
    ValueTask EndFrameAsync(object target,CancellationToken ct);
}

/// <summary>
/// Retained compositor: transforms/Z-order become draw commands; media decode is not repeated.
/// </summary>
public sealed class D3D12Compositor : IGraphicsBackend
{
    private readonly ID3D12CompositorDevice _device;
    private readonly IGpuResourceCache _textures;
    private readonly IDecodeService _decode;
    private PixelSize _size;

    public D3D12Compositor(ID3D12CompositorDevice device,IGpuResourceCache textures,IDecodeService decode)
        =>(_device,_textures,_decode)=(device,textures,decode);

    public async ValueTask InitializeAsync(HardwareCapabilities capabilities,CancellationToken ct)
    {
        if(!capabilities.D3D12)throw new NotSupportedException("D3D12 capability was not proven.");
        await _device.InitializeAsync(ct);
    }

    public async ValueTask<object> ComposeAsync(FramePlan frame,IReadOnlyDictionary<AssetId,DecodedSurface> surfaces,CancellationToken ct)
    {
        _size=frame.Scene.OutputSize;
        var target=await _device.CreateRenderTargetAsync(_size,ct);
        await _device.BeginFrameAsync(target,ct);
        var commands=ImmutableArray.CreateBuilder<GpuDrawCommand>();
        foreach(var obj in frame.Scene.Objects.Where(x=>x.Opacity>0).OrderBy(x=>x.Z))
        {
            var req=frame.RequiredAssets.FirstOrDefault(x=>x.Asset==obj.Asset);
            if(req is null)continue;
            if(!surfaces.TryGetValue(obj.Asset,out var decoded))decoded=await _decode.DecodeAsync(req,ct);
            var tex=await _textures.GetOrUploadAsync(req,decoded,ct);
            var cmd=new GpuDrawCommand(obj.Asset,tex,obj.Transform,obj.Opacity,obj.Z,_size);
            await _device.DrawTexturedQuadAsync(target,cmd,ct);
            commands.Add(cmd);
        }
        await _device.EndFrameAsync(target,ct);
        return new GpuComposedFrame(_size,frame.Time,target,commands.ToImmutable());
    }
}
