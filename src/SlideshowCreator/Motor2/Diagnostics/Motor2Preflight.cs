namespace SlideshowCreator.Motor2.Core;

public sealed record PreflightResult(bool Success,IReadOnlyList<string> Errors,IReadOnlyList<string> Warnings);

public static class Motor2Preflight
{
    public static PreflightResult Validate(HardwareCapabilities hw,IEnumerable<MediaAsset> assets)
    {
        var errors=new List<string>(); var warnings=new List<string>();
        if(!hw.D3D12)errors.Add("D3D12 device/compositor capability not proven.");
        if(!hw.NvEnc)warnings.Add("NVENC not proven; a validated fallback encoder is required.");
        var list=assets.ToList();
        if(list.Count==0)errors.Add("No media assets.");
        if(list.Any(x=>x.Kind==MediaKind.Raw && x.NativeSize is null))
            warnings.Add("One or more RAW assets still require dedicated RAW metadata probing.");
        return new(!errors.Any(),errors,warnings);
    }
}
