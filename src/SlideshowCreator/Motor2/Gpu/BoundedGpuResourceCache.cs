using System.Collections.Concurrent;

namespace SlideshowCreator.Motor2.Core;

public sealed class BoundedGpuResourceCache : IGpuResourceCache
{
    private sealed record Entry(GpuTextureHandle Texture,long Tick);
    private readonly ConcurrentDictionary<string,Entry> _items=new();
    private readonly IGpuUploadBackend _upload;
    private long _tick;
    private readonly SemaphoreSlim _gate=new(1,1);
    public long BudgetBytes { get; }
    public long ResidentBytes => _items.Values.Sum(x=>x.Texture.EstimatedBytes);

    public BoundedGpuResourceCache(long budgetBytes,IGpuUploadBackend upload)
    {
        BudgetBytes=Math.Max(128L*1024*1024,budgetBytes);
        _upload=upload;
    }

    public ValueTask<GpuTextureHandle?> TryGetAsync(DecodeRequest request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key=Key(request);
        if(!_items.TryGetValue(key,out var e)) return ValueTask.FromResult<GpuTextureHandle?>(null);
        _items[key]=e with { Tick=Interlocked.Increment(ref _tick) };
        return ValueTask.FromResult<GpuTextureHandle?>(e.Texture);
    }

    public async ValueTask<GpuTextureHandle> GetOrUploadAsync(DecodeRequest request,DecodedSurface decoded,CancellationToken ct)
    {
        var hit=await TryGetAsync(request,ct);
        if(hit is not null)return hit;
        await _gate.WaitAsync(ct);
        try
        {
            hit=await TryGetAsync(request,ct);
            if(hit is not null)return hit;
            var texture=await _upload.UploadAsync(decoded,ct);
            if(texture.EstimatedBytes>BudgetBytes)return texture;
            _items[Key(request)]=new(texture,Interlocked.Increment(ref _tick));
            // Do not evict here: the caller is assembling a frame and earlier returned textures
            // may still be referenced by that frame. Trimming is an explicit safe-point operation.
            return texture;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask TrimAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            while(ResidentBytes>BudgetBytes)
            {
                ct.ThrowIfCancellationRequested();
                var victim=_items.OrderBy(x=>x.Value.Tick).FirstOrDefault();
                if(victim.Key is null)break;
                if(_items.TryRemove(victim.Key,out var removed))
                    await _upload.ReleaseAsync(removed.Texture,ct);
            }
        }
        finally { _gate.Release(); }
    }
    private static string Key(DecodeRequest r)=>$"{r.Asset.Value}:{r.RequiredFootprint.Width}x{r.RequiredFootprint.Height}:{r.ExportQuality}";
}
