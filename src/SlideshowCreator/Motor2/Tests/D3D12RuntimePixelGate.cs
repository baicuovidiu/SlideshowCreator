using System.Numerics;

namespace SlideshowCreator.Motor2.Core;

/// <summary>
/// Runtime-only Gate A validation. It proves that D3D12 produces actual pixels.
/// Readback is diagnostic only and is forbidden from the production export hot path.
/// </summary>
public static class D3D12RuntimePixelGate
{
    public static async Task ValidateAsync(D3D12NativeDeviceContext device,CancellationToken ct)
    {
        await device.InitializeAsync(ct);
        var size=new PixelSize(16,16);
        var target=await device.CreateRenderTargetAsync(size,ct);
        try
        {
            await device.BeginFrameAsync(target,ct);
            await device.DrawTexturedQuadsAsync(target,Array.Empty<GpuDrawCommand>(),ct);
            await device.EndFrameAsync(target,ct);
            var bytes=await device.ReadbackRgba16fAsync(target,size,ct);
            if(bytes.Length!=16*16*8) throw new InvalidOperationException("D3D12 readback returned an unexpected byte count.");
            // Clear is FP16 black with alpha=1. Half(1.0) == 0x3C00 little-endian.
            for(var i=0;i<bytes.Length;i+=8)
            {
                if(bytes[i]!=0||bytes[i+1]!=0||bytes[i+2]!=0||bytes[i+3]!=0||bytes[i+4]!=0||bytes[i+5]!=0||bytes[i+6]!=0x00||bytes[i+7]!=0x3C)
                    throw new InvalidOperationException($"D3D12 clear pixel verification failed at byte {i}.");
            }
        }
        finally { await device.ReleaseRenderTargetAsync(target,CancellationToken.None); }
    }
}
