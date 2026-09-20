using SlideshowCreator.Motor2.Core;
using System.Security.Cryptography;

static void Fail(string message){Console.Error.WriteLine("FAIL: "+message);Environment.Exit(2);}
try
{
    Console.WriteLine("Motor 2.0 NVENC Hardware Gate");
    if(!OperatingSystem.IsWindows())Fail("Windows required.");
    var profiler=new WindowsHardwareProfiler();
    var caps=await profiler.ProbeAsync(CancellationToken.None);
    Console.WriteLine($"Adapter={caps.AdapterName} VRAM={caps.VramBytes} D3D12={caps.D3D12} NVENC={caps.NvEnc}");
    if(!caps.D3D12)Fail("D3D12 capability not proven.");
    if(!caps.NvEnc)Fail("NVENC capability not proven by driver API.");

    await using var device=new D3D12NativeDeviceContext();
    await device.InitializeAsync(CancellationToken.None);
    var compositor=new D3D12Compositor(device);
    await compositor.InitializeAsync(caps,CancellationToken.None);
    await using var nativeSession=new NativeNvencSession(device);
    var encoder=new NvencEncodeBackend(nativeSession);
    var size=new PixelSize(1920,1080);
    await encoder.InitializeAsync(size,30,caps,CancellationToken.None);

    const int frames=90;
    long totalBytes=0; byte[]? first=null;
    for(int i=0;i<frames;i++)
    {
        var scene=new SceneSnapshot(size,TimeSpan.FromSeconds(i/30.0),[]);
        var plan=new FramePlan(scene,[]);
        var frame=await compositor.ComposeAsync(plan,new Dictionary<AssetId,GpuTextureHandle>(),CancellationToken.None);
        try {
            await encoder.EncodeAsync(frame,TimeSpan.FromSeconds(i/30.0),CancellationToken.None);
            await encoder.WaitForFrameCompletionAsync(frame,CancellationToken.None);
        } finally { await compositor.ReleaseComposedFrameAsync(frame,CancellationToken.None); }
    }
    await encoder.FinalizeAsync(CancellationToken.None);
    foreach(var b in nativeSession.CompletedBitstreams.Values){totalBytes+=b.Length;first??=b;}
    if(nativeSession.CompletedBitstreams.Count!=frames)Fail($"Expected {frames} completed bitstreams, got {nativeSession.CompletedBitstreams.Count}.");
    if(totalBytes<=0||first is null)Fail("NVENC produced no H.264 bytes.");
    bool annexB=first.AsSpan().IndexOf(new byte[]{0,0,1})>=0;
    if(!annexB)Fail("H.264 Annex-B start code not found.");
    var hash=Convert.ToHexString(SHA256.HashData(first));
    Console.WriteLine($"PASS: frames={frames}, h264Bytes={totalBytes}, firstSHA256={hash}");
    Console.WriteLine("PASS: D3D12 -> FP16 -> GPU BGRA -> NVENC H.264 -> completion -> release");
    return 0;
}
catch(Exception ex){Fail(ex.ToString());return 2;}
