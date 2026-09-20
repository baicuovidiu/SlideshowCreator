namespace SlideshowCreator.Motor2.Core;

/// <summary>
/// Contract for the dedicated RAW backend (LibRaw/RAWtoACES candidate).
/// It explicitly separates metadata from pixel decode so sorting/import never pays full RAW decode cost.
/// </summary>
public sealed record RawMetadata(
    PixelSize NativeSize,
    int Orientation,
    DateTimeOffset? DateTimeOriginal,
    string? CameraMake,
    string? CameraModel,
    string? ColorProfile);

public interface IRawBackend
{
    ValueTask<RawMetadata> ReadMetadataAsync(string path,CancellationToken ct);
    ValueTask<DecodedSurface> DecodeAsync(MediaAsset asset,PixelSize requiredFootprint,bool exportQuality,CancellationToken ct);
}

public sealed class RawDecodeService : IDecodeService
{
    private readonly IAssetCatalog _catalog;
    private readonly IRawBackend _raw;
    public RawDecodeService(IAssetCatalog catalog,IRawBackend raw)=>(_catalog,_raw)=(catalog,raw);

    public async ValueTask<DecodedSurface> DecodeAsync(DecodeRequest request,CancellationToken ct)
    {
        var asset=await _catalog.GetAsync(request.Asset,ct) ?? throw new KeyNotFoundException(request.Asset.Value);
        if(asset.Kind!=MediaKind.Raw) throw new NotSupportedException("RawDecodeService accepts RAW assets only.");
        return await _raw.DecodeAsync(asset,request.RequiredFootprint,request.ExportQuality,ct);
    }
}
