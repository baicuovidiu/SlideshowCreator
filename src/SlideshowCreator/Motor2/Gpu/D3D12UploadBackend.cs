namespace SlideshowCreator.Motor2.Core;

/// <summary>
/// D3D12 native boundary. Implementation will keep COPY and DIRECT work independent,
/// synchronize with fences, and return shader-readable textures.
/// </summary>
public interface ID3D12DeviceContext : IAsyncDisposable
{
    string AdapterName { get; }
    long DedicatedVideoMemoryBytes { get; }
    ValueTask InitializeAsync(CancellationToken ct);
    ValueTask<object> CreateTextureAsync(PixelSize size,string pixelFormat,CancellationToken ct);
    ValueTask UploadTextureAsync(object texture,DecodedSurface source,CancellationToken ct);
    ValueTask ReleaseAsync(object resource,CancellationToken ct);
}

public sealed class D3D12UploadBackend : IGpuUploadBackend
{
    private readonly ID3D12DeviceContext _device;
    private readonly IRenderTelemetry _telemetry;
    public D3D12UploadBackend(ID3D12DeviceContext device,IRenderTelemetry telemetry)=>(_device,_telemetry)=(device,telemetry);

    public async ValueTask<GpuTextureHandle> UploadAsync(DecodedSurface source,CancellationToken ct)
    {
        using(_telemetry.Measure("gpu.upload",source.Asset))
        {
            var native=await _device.CreateTextureAsync(source.Size,source.PixelFormat,ct);
            await _device.UploadTextureAsync(native,source,ct);
            var bytes=checked((long)source.Size.Width*source.Size.Height*4);
            return new(source.Asset,source.Size,source.PixelFormat,bytes,native,GpuResourceState.Ready);
        }
    }
    public ValueTask ReleaseAsync(GpuTextureHandle texture,CancellationToken ct)=>_device.ReleaseAsync(texture.NativeResource,ct);
}
