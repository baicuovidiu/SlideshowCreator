using System.Collections.Immutable;
using SlideshowCreator.Motor2.Core;
using System.Security.Cryptography;
using System.Text;

string ResultPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Motor2_HardwareGate_RESULT.txt");
void SaveResult(string status,string details){
    var body=$"Motor 2.0 Hardware Gate\r\nSTATUS: {status}\r\n{details}\r\nTime: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n";
    Console.WriteLine(); Console.WriteLine(body);
    try {
        File.WriteAllText(ResultPath,body,Encoding.UTF8);
        Console.WriteLine("Rezultatul a fost salvat pe Desktop:"); Console.WriteLine(ResultPath);
    } catch(Exception saveEx) {
        var fallback=Path.Combine(Path.GetTempPath(),"Motor2_HardwareGate_RESULT.txt");
        try { File.WriteAllText(fallback,body+"ResultSaveWarning: "+saveEx.Message+"\r\n",Encoding.UTF8); Console.WriteLine("Desktop indisponibil. Rezultat salvat temporar:"); Console.WriteLine(fallback); }
        catch { Console.WriteLine("ATENTIE: rezultatul nu a putut fi scris in fisier. Fotografiaza textul de mai sus."); }
    }
}
void Pause(){Console.WriteLine();Console.WriteLine("Fa o poza acestui rezultat sau trimite fisierul Motor2_HardwareGate_RESULT.txt.");Console.WriteLine("Apasa ENTER pentru inchidere...");Console.ReadLine();}
void Fail(string message){SaveResult("FAIL",message);Pause();Environment.Exit(2);}
try
{
    Console.WriteLine("Motor 2.0 NVENC Hardware Gate");
    if(!OperatingSystem.IsWindows())Fail("Windows required.");
    var profiler=new WindowsHardwareProfiler();
    var caps=await profiler.ProbeAsync(CancellationToken.None);
    Console.WriteLine($"Adapter={caps.AdapterName} VRAM={caps.DedicatedVideoMemoryBytes} D3D12={caps.D3D12} NVENC={caps.NvEnc}");
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
    long totalBytes=0; byte[]? first=null; int completedPackets=0;
    for(int i=0;i<frames;i++)
    {
        var t=TimeSpan.FromSeconds(i/30.0);
        var scene=new EvaluatedScene(t,size,ImmutableArray<SceneObject>.Empty);
        var plan=new FramePlan(t,ImmutableArray<DecodeRequest>.Empty,scene);
        var frame=await compositor.ComposeAsync(plan,new Dictionary<AssetId,GpuTextureHandle>(),CancellationToken.None);
        try {
            await encoder.EncodeAsync(frame,TimeSpan.FromSeconds(i/30.0),CancellationToken.None);
            await encoder.WaitForFrameCompletionAsync(frame,CancellationToken.None);
            foreach(var packet in nativeSession.TakeCompletedBitstreams()){ completedPackets++; totalBytes+=packet.Length; first??=packet; }
        } finally { await compositor.ReleaseComposedFrameAsync(frame,CancellationToken.None); }
    }
    await encoder.FinalizeAsync(CancellationToken.None);
    foreach(var packet in nativeSession.TakeCompletedBitstreams()){ completedPackets++; totalBytes+=packet.Length; first??=packet; }
    if(completedPackets!=frames)Fail($"Expected {frames} completed bitstreams, got {completedPackets}.");
    if(totalBytes<=0||first is null)Fail("NVENC produced no H.264 bytes.");
    bool annexB=first.AsSpan().IndexOf(new byte[]{0,0,1})>=0;
    if(!annexB)Fail("H.264 Annex-B start code not found.");
    var hash=Convert.ToHexString(SHA256.HashData(first));
    var details=$"Adapter={caps.AdapterName}\r\nVRAM={caps.DedicatedVideoMemoryBytes}\r\nD3D12={caps.D3D12}\r\nNVENC={caps.NvEnc}\r\nframes={frames}\r\nh264Bytes={totalBytes}\r\nfirstSHA256={hash}\r\nPipeline=D3D12 -> FP16 -> GPU BGRA -> NVENC H.264 -> completion -> release";
    SaveResult("PASS",details);
    Pause();
    return 0;
}
catch(Exception ex){Fail(ex.ToString());return 2;}
