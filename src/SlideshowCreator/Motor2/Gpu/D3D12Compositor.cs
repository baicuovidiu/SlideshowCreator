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
    ValueTask DrawTexturedQuadsAsync(object target,IReadOnlyList<GpuDrawCommand> commands,CancellationToken ct);
    ValueTask EndFrameAsync(object target,CancellationToken ct);
    ValueTask ReleaseRenderTargetAsync(object target,CancellationToken ct);
    ValueTask<byte[]> ReadbackRgba16fAsync(object target,PixelSize size,CancellationToken ct);
}

/// <summary>
/// Retained compositor: transforms/Z-order become draw commands; media decode is not repeated.
/// </summary>
public sealed class D3D12Compositor : IGraphicsBackend
{
    private readonly ID3D12CompositorDevice _device;
    private PixelSize _size = new(1, 1);

    public D3D12Compositor(ID3D12CompositorDevice device) => _device = device;

    public async ValueTask InitializeAsync(HardwareCapabilities capabilities,CancellationToken ct)
    {
        if(!capabilities.D3D12)throw new NotSupportedException("D3D12 capability was not proven.");
        await _device.InitializeAsync(ct);
    }

    public async ValueTask<object> ComposeAsync(FramePlan frame,IReadOnlyDictionary<AssetId,GpuTextureHandle> textures,CancellationToken ct)
    {
        _size=frame.Scene.OutputSize;
        var target=await _device.CreateRenderTargetAsync(_size,ct);
        await _device.BeginFrameAsync(target,ct);
        var commands=ImmutableArray.CreateBuilder<GpuDrawCommand>();
        foreach(var obj in frame.Scene.Objects.Where(x=>x.Opacity>0).OrderBy(x=>x.Z))
        {
            if(!textures.TryGetValue(obj.Asset,out var tex))
                throw new InvalidOperationException($"GPU texture missing for asset {obj.Asset}.");
            var ndc=PhotoLayout.PixelToNdc(obj.Transform,tex.Size,_size);
            commands.Add(new GpuDrawCommand(obj.Asset,tex,ndc,obj.Opacity,obj.Z,_size));
        }
        await _device.DrawTexturedQuadsAsync(target,commands,ct);
        await _device.EndFrameAsync(target,ct);
        return new GpuComposedFrame(_size,frame.Time,target,commands.ToImmutable());
    }
}
