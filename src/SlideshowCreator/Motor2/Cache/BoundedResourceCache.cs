using System.Collections.Concurrent;

namespace SlideshowCreator.Motor2.Core;

/// <summary>
/// Gate-A bounded RAM cache. VRAM cache will live in the graphics backend.
/// Keys include requested footprint/quality so a wall thumbnail never forces a full RAW decode.
/// </summary>
public sealed class BoundedResourceCache : IResourceCache
{
    private sealed record Entry(DecodedSurface Surface,long Bytes,long Tick);
    private readonly ConcurrentDictionary<string,Entry> _ram=new();
    private long _tick;
    public long RamBudgetBytes { get; }
    public long VramBudgetBytes { get; }

    public BoundedResourceCache(long ramBudgetBytes,long vramBudgetBytes)
    {
        RamBudgetBytes=Math.Max(64L*1024*1024,ramBudgetBytes);
        VramBudgetBytes=Math.Max(64L*1024*1024,vramBudgetBytes);
    }

    public ValueTask<DecodedSurface?> TryGetAsync(DecodeRequest request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key=Key(request);
        if(!_ram.TryGetValue(key,out var e)) return ValueTask.FromResult<DecodedSurface?>(null);
        _ram[key]=e with { Tick=Interlocked.Increment(ref _tick) };
        return ValueTask.FromResult<DecodedSurface?>(e.Surface);
    }

    public ValueTask StoreAsync(DecodeRequest request,DecodedSurface surface,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes=EstimateBytes(surface);
        if(bytes>RamBudgetBytes) return ValueTask.CompletedTask;
        _ram[Key(request)]=new(surface,bytes,Interlocked.Increment(ref _tick));
        Trim();
        return ValueTask.CompletedTask;
    }

    private void Trim()
    {
        while(_ram.Values.Sum(x=>x.Bytes)>RamBudgetBytes)
        {
            var victim=_ram.OrderBy(x=>x.Value.Tick).FirstOrDefault();
            if(victim.Key is null) break;
            _ram.TryRemove(victim.Key,out _);
        }
    }
    private static long EstimateBytes(DecodedSurface s)=>checked((long)s.Size.Width*s.Size.Height*4);
    private static string Key(DecodeRequest r)=>$"{r.Asset.Value}:{r.RequiredFootprint.Width}x{r.RequiredFootprint.Height}:{r.Timestamp?.Ticks ?? -1}:{r.ExportQuality}";
}
