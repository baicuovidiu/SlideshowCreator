using System.Collections.Immutable;
using System.Diagnostics;

namespace SlideshowCreator.Motor2.Core;

public sealed class WindowsHardwareProfiler : IHardwareProfiler
{
    public async ValueTask<HardwareCapabilities> ProbeAsync(CancellationToken ct)
    {
        var nvidia = await TryNvidiaSmiAsync(ct);
        // Gate A reports only capabilities we can actually prove. D3D12/CUDA probing
        // will be upgraded to native API checks; no capability is inferred from a brand name.
        return new HardwareCapabilities(
            nvidia?.Name ?? "Windows graphics adapter (native probe pending)",
            nvidia?.VramBytes ?? 0,
            D3D12:false, Cuda:false, NvDec:false, NvEnc:false,
            ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
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
