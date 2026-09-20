using System.Runtime.InteropServices;

namespace SlideshowCreator.Motor2.Core;

public sealed class NativeNvencSession : INvencNativeSession
{
    private readonly D3D12NativeDeviceContext _device;
    private nint _session;
    private readonly Dictionary<ulong,byte[]> _bitstreams=new();
    private readonly Dictionary<ulong,nint> _convertedTargets=new();
    private PixelSize _size = new(1,1);
    public NativeNvencSession(D3D12NativeDeviceContext device)=>_device=device;

    public ValueTask OpenAsync(EncoderSettings settings,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ctx=_device.NativeContextForInterop; _size=settings.Size;
        var s=new Motor2Native.NvencSessionSettings{Width=(uint)settings.Size.Width,Height=(uint)settings.Size.Height,FpsNum=(uint)Math.Round(settings.Fps*1000),FpsDen=1000,Bitrate=(uint)settings.Bitrate};
        _session=Motor2Native.NvencOpenD3D12(ctx,ref s); if(_session==0)throw new InvalidOperationException("Native NVENC session could not be opened.");
        return ValueTask.CompletedTask;
    }
    public ValueTask<ulong> SubmitD3D12SurfaceAsync(object nativeSurface,TimeSpan pts,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if(_session==0)throw new InvalidOperationException("NVENC session is not open.");
        if(nativeSurface is not nint h||h==0)throw new ArgumentException("Expected native D3D12 render target.");
        var converted=_device.ConvertForNvencAsync(h,_size,ct).GetAwaiter().GetResult();
        var hr=Motor2Native.NvencSubmit(_session,converted,pts.Ticks,out var id); if(hr<0){Motor2Native.ReleaseRenderTarget(_device.NativeContextForInterop,converted);Marshal.ThrowExceptionForHR(hr);} _convertedTargets[id]=converted; return ValueTask.FromResult(id);
    }
    public unsafe ValueTask WaitForCompletionAsync(ulong id,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var hr=Motor2Native.NvencWait(_session,id); if(hr<0)Marshal.ThrowExceptionForHR(hr);
        uint bytes=0; hr=Motor2Native.NvencGetBitstream(_session,id,null,0,out bytes);
        const int insufficient=unchecked((int)0x8007007A); if(hr<0&&hr!=insufficient)Marshal.ThrowExceptionForHR(hr);
        if(bytes>0){var data=new byte[bytes];fixed(byte* p=data){hr=Motor2Native.NvencGetBitstream(_session,id,p,bytes,out var written);if(hr<0)Marshal.ThrowExceptionForHR(hr);if(written!=bytes)Array.Resize(ref data,(int)written);} _bitstreams[id]=data;}
        if(_convertedTargets.Remove(id,out var converted)) Motor2Native.ReleaseRenderTarget(_device.NativeContextForInterop,converted);
        return ValueTask.CompletedTask;
    }
    public ValueTask DrainAsync(CancellationToken ct){ct.ThrowIfCancellationRequested();var hr=Motor2Native.NvencDrain(_session);if(hr<0)Marshal.ThrowExceptionForHR(hr);return ValueTask.CompletedTask;}
    public IReadOnlyDictionary<ulong,byte[]> CompletedBitstreams=>_bitstreams;
    public IReadOnlyList<byte[]> TakeCompletedBitstreams()
    {
        if(_bitstreams.Count==0)return Array.Empty<byte[]>();
        var packets=_bitstreams.OrderBy(x=>x.Key).Select(x=>x.Value).ToArray();
        _bitstreams.Clear();
        return packets;
    }
    public ValueTask DisposeAsync(){if(_session!=0)Motor2Native.NvencClose(_session);_session=0;foreach(var t in _convertedTargets.Values)Motor2Native.ReleaseRenderTarget(_device.NativeContextForInterop,t);_convertedTargets.Clear();return ValueTask.CompletedTask;}
}
