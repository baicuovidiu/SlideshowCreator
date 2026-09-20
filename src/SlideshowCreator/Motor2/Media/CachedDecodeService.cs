namespace SlideshowCreator.Motor2.Core;

/// <summary>
/// Ensures repeated frame requests reuse prepared still resources.
/// The concrete RAW decoder is injected; this layer owns cache semantics.
/// </summary>
public sealed class CachedDecodeService : IDecodeService
{
    private readonly IDecodeService _inner;
    private readonly IResourceCache _cache;
    private readonly IRenderTelemetry _telemetry;

    public CachedDecodeService(IDecodeService inner,IResourceCache cache,IRenderTelemetry telemetry)
        =>(_inner,_cache,_telemetry)=(inner,cache,telemetry);

    public async ValueTask<DecodedSurface> DecodeAsync(DecodeRequest request,CancellationToken ct)
    {
        var cached=await _cache.TryGetAsync(request,ct);
        if(cached is not null){_telemetry.Counter("decode.cache.hit",1);return cached;}
        using(_telemetry.Measure("decode",request.Asset))
        {
            var surface=await _inner.DecodeAsync(request,ct);
            await _cache.StoreAsync(request,surface,ct);
            _telemetry.Counter("decode.cache.miss",1);
            return surface;
        }
    }
}

/// <summary>Calculates a decode footprint that preserves aspect ratio and never crops.</summary>
public static class ContainFootprint
{
    public static PixelSize Calculate(PixelSize source,PixelSize box,int overscanPercent=10)
    {
        if(source.Width<=0||source.Height<=0||box.Width<=0||box.Height<=0) throw new ArgumentOutOfRangeException();
        var scale=Math.Min((double)box.Width/source.Width,(double)box.Height/source.Height);
        scale*=1.0+Math.Clamp(overscanPercent,0,50)/100.0;
        scale=Math.Min(scale,1.0); // never upscale decode beyond source here
        return new PixelSize(Math.Max(1,(int)Math.Ceiling(source.Width*scale)),Math.Max(1,(int)Math.Ceiling(source.Height*scale)));
    }
}
