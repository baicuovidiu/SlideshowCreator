namespace SlideshowCreator.Motor2.Core;

public sealed record EncoderSettings(PixelSize Size,double Fps,int Bitrate,string Codec,bool MaximumPerformance);

public interface INvencNativeSession : IAsyncDisposable
{
    ValueTask OpenAsync(EncoderSettings settings,CancellationToken ct);
    ValueTask<ulong> SubmitD3D12SurfaceAsync(object nativeSurface,TimeSpan pts,CancellationToken ct);
    ValueTask WaitForCompletionAsync(ulong submissionId,CancellationToken ct);
    ValueTask DrainAsync(CancellationToken ct);
}

/// <summary>
/// Direct GPU-surface encoder boundary. No PNG/temp MP4/CPU readback is permitted here.
/// </summary>
public sealed class NvencEncodeBackend : IVideoEncodeBackend, IGpuFrameCompletionSource
{
    private readonly INvencNativeSession _session;
    private EncoderSettings? _settings;
    private readonly Dictionary<object,ulong> _submissions=new(ReferenceEqualityComparer.Instance);
    public NvencEncodeBackend(INvencNativeSession session)=>_session=session;

    public async ValueTask InitializeAsync(PixelSize size,double fps,HardwareCapabilities capabilities,CancellationToken ct)
    {
        if(!capabilities.NvEnc)throw new NotSupportedException("NVENC capability was not proven.");
        _settings=new(size,fps,24_000_000,"h264",true);
        await _session.OpenAsync(_settings,ct);
    }

    public async ValueTask EncodeAsync(object gpuFrame,TimeSpan pts,CancellationToken ct)
    {
        if(_settings is null)throw new InvalidOperationException("Encoder not initialized.");
        if(gpuFrame is not GpuComposedFrame f)throw new ArgumentException("NVENC requires a GPU composed frame.",nameof(gpuFrame));
        var id=await _session.SubmitD3D12SurfaceAsync(f.NativeSurface,pts,ct);
        lock(_submissions)_submissions[gpuFrame]=id;
    }
    public async ValueTask WaitForFrameCompletionAsync(object gpuFrame,CancellationToken ct)
    {
        ulong id; lock(_submissions){if(!_submissions.TryGetValue(gpuFrame,out id))throw new InvalidOperationException("NVENC submission ownership token missing.");}
        await _session.WaitForCompletionAsync(id,ct);
        lock(_submissions)_submissions.Remove(gpuFrame);
    }
    public ValueTask FinalizeAsync(CancellationToken ct)=>_session.DrainAsync(ct);
}
