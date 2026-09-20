namespace SlideshowCreator.Motor2.Core;

public enum GpuResourceState { UploadPending, Ready, Evicted, Faulted }

public sealed record GpuTextureHandle(
    AssetId Asset,
    PixelSize Size,
    string PixelFormat,
    long EstimatedBytes,
    object NativeResource,
    GpuResourceState State);

public interface IGpuResourceCache
{
    long BudgetBytes { get; }
    long ResidentBytes { get; }
    ValueTask<GpuTextureHandle?> TryGetAsync(DecodeRequest request,CancellationToken ct);
    ValueTask<GpuTextureHandle> GetOrUploadAsync(DecodeRequest request,DecodedSurface decoded,CancellationToken ct);
    ValueTask TrimAsync(CancellationToken ct);
}

/// <summary>
/// Backend boundary for low-copy upload. Native D3D12 implementation owns upload heaps,
/// copy queues, resource barriers and fences; callers never round-trip textures through RAM.
/// </summary>
public interface IGpuUploadBackend
{
    ValueTask<GpuTextureHandle> UploadAsync(DecodedSurface source,CancellationToken ct);
    ValueTask ReleaseAsync(GpuTextureHandle texture,CancellationToken ct);
}
