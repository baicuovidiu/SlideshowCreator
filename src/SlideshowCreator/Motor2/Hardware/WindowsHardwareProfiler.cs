using System.Collections.Immutable;
using System.Diagnostics;

namespace SlideshowCreator.Motor2.Core;

public sealed class WindowsHardwareProfiler : IHardwareProfiler
{
    public async ValueTask<HardwareCapabilities> ProbeAsync(CancellationToken ct)
    {
        var nvidia = await TryNvidiaSmiAsync(ct);
        var d3d12 = TryD3D12Probe();
        return new HardwareCapabilities(
            d3d12?.Name ?? nvidia?.Name ?? "Windows graphics adapter",
            d3d12?.VramBytes ?? nvidia?.VramBytes ?? 0,
            D3D12:d3d12 is not null, Cuda:false, NvDec:false, NvEnc:false,
            ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
    }

    private static unsafe (string Name,long VramBytes)? TryD3D12Probe()
    {
        if(!OperatingSystem.IsWindows()) return null;
        try { Motor2Native.AdapterInfo info=default; var hr=Motor2Native.Probe(&info); return hr>=0 ? (info.GetName(),checked((long)info.DedicatedVideoMemory)) : null; }
        catch { return null; }
    }

    private static async Task<(string Name,long VramBytes)?> TryNvidiaSmiAsync(CancellationToken ct)
    {
        try {
            var psi=new ProcessStartInfo("nvidia-smi"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            psi.ArgumentList.Add("--query-gpu=name,memory.total"); psi.ArgumentList.Add("--format=csv,noheader,nounits");
            using var p=Process.Start(psi); if(p is null)return null;
            var text=await p.StandardOutput.ReadToEndAsync(ct); await p.WaitForExitAsync(ct);
            if(p.ExitCode!=0)return null;
            var first=text.Split('\n',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(); if(first is null)return null;
            var parts=first.Split(','); if(parts.Length<2)return null;
            return (parts[0].Trim(), long.TryParse(parts[1].Trim(),out var mib)?mib*1024L*1024L:0);
        } catch { return null; }
    }
}
