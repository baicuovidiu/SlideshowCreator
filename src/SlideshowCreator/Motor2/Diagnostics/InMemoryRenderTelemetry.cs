using System.Collections.Concurrent;
using System.Diagnostics;

namespace SlideshowCreator.Motor2.Core;

public sealed record StageSample(string Stage, string? Asset, double Milliseconds);
public sealed class InMemoryRenderTelemetry : IRenderTelemetry
{
    private readonly ConcurrentQueue<StageSample> _samples=new();
    private readonly ConcurrentDictionary<string,long> _counters=new();
    public IReadOnlyCollection<StageSample> Samples=>_samples.ToArray();
    public IReadOnlyDictionary<string,long> Counters=>_counters;
    public IDisposable Measure(string stage, AssetId? asset=null)=>new Scope(stage,asset?.Value,_samples);
    public void Counter(string name,long value)=>_counters[name]=value;
    private sealed class Scope(string stage,string? asset,ConcurrentQueue<StageSample> sink):IDisposable {
        private readonly Stopwatch _sw=Stopwatch.StartNew();
        public void Dispose(){_sw.Stop();sink.Enqueue(new(stage,asset,_sw.Elapsed.TotalMilliseconds));}
    }
}
