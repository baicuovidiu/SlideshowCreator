using System.Collections.Immutable;

namespace SlideshowCreator.Motor2.Core;

public enum MediaKind { Still, Raw, Video, Audio }
public enum ResidencyTier { ColdStorage, Ram, Vram }

public sealed record AssetId(string Value);
public sealed record PixelSize(int Width, int Height);
public sealed record AssetFingerprint(string Algorithm, string Value);

public sealed record MediaAsset(
    AssetId Id,
    string SourcePath,
    MediaKind Kind,
    AssetFingerprint Fingerprint,
    PixelSize? NativeSize,
    int ExifOrientation,
    DateTimeOffset? DateTimeOriginal,
    string? IccProfile,
    TimeSpan? Duration,
    string? Codec,
    string? CodecProfile,
    double? FrameRate,
    bool? VariableFrameRate,
    ImmutableArray<string> AudioStreams);

public sealed record DecodeRequest(
    AssetId Asset,
    PixelSize RequiredFootprint,
    TimeSpan? Timestamp,
    bool ExportQuality);

public sealed record DecodedSurface(
    AssetId Asset,
    PixelSize Size,
    string PixelFormat,
    ResidencyTier Residency,
    object NativeHandle);

public sealed record HardwareCapabilities(
    string AdapterName,
    long DedicatedVideoMemoryBytes,
    bool D3D12,
    bool Cuda,
    bool NvDec,
    bool NvEnc,
    ImmutableArray<string> DecodeCodecs,
    ImmutableArray<string> EncodeCodecs);

public interface IMediaProbe
{
    ValueTask<MediaAsset> ProbeAsync(string path, CancellationToken ct);
}

public interface IAssetCatalog
{
    ValueTask<MediaAsset?> GetAsync(AssetId id, CancellationToken ct);
    ValueTask UpsertAsync(MediaAsset asset, CancellationToken ct);
}

public interface IDecodeService
{
    ValueTask<DecodedSurface> DecodeAsync(DecodeRequest request, CancellationToken ct);
}

public interface IResourceCache
{
    ValueTask<DecodedSurface?> TryGetAsync(DecodeRequest request, CancellationToken ct);
    ValueTask StoreAsync(DecodeRequest request, DecodedSurface surface, CancellationToken ct);
    long RamBudgetBytes { get; }
    long VramBudgetBytes { get; }
}

public interface IHardwareProfiler
{
    ValueTask<HardwareCapabilities> ProbeAsync(CancellationToken ct);
}

public interface IRenderTelemetry
{
    IDisposable Measure(string stage, AssetId? asset = null);
    void Counter(string name, long value);
}
