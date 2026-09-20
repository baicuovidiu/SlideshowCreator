using System.IO;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace SlideshowCreator.Motor2.Core;

/// <summary>
/// Gate-A probe. ffprobe is deliberately used only as an inspection helper;
/// it is not the compositor. RAW parsing will move to the dedicated RAW backend.
/// </summary>
public sealed class FfprobeMediaProbe : IMediaProbe
{
    private readonly string _ffprobePath;
    public FfprobeMediaProbe(string ffprobePath) => _ffprobePath = ffprobePath;

    public async ValueTask<MediaAsset> ProbeAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Media source not found.", path);
        var fingerprint = await FingerprintAsync(path, ct);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var kind = ext switch {
            ".arw" or ".dng" or ".nef" or ".cr2" or ".cr3" => MediaKind.Raw,
            ".mp4" or ".mov" or ".mxf" or ".mkv" => MediaKind.Video,
            ".wav" or ".mp3" or ".flac" or ".m4a" => MediaKind.Audio,
            _ => MediaKind.Still
        };

        var json = await RunProbeAsync(path, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var streams = root.TryGetProperty("streams", out var s) ? s : default;
        JsonElement? video = null;
        var audio = ImmutableArray.CreateBuilder<string>();

        if (streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var type = GetString(stream, "codec_type");
                if (type == "video" && video is null) video = stream;
                if (type == "audio") audio.Add(GetString(stream, "codec_name") ?? "unknown");
            }
        }

        PixelSize? size = null; string? codec = null; string? profile = null; double? fps = null;
        if (video is JsonElement v)
        {
            if (TryInt(v, "width", out var w) && TryInt(v, "height", out var h)) size = new(w,h);
            codec = GetString(v, "codec_name"); profile = GetString(v, "profile");
            fps = ParseRate(GetString(v, "avg_frame_rate"));
        }

        TimeSpan? duration = null;
        if (root.TryGetProperty("format", out var fmt) &&
            double.TryParse(GetString(fmt,"duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec))
            duration = TimeSpan.FromSeconds(sec);

        // EXIF/RAW-specific orientation and DateTimeOriginal are intentionally not guessed here.
        return new MediaAsset(new AssetId(fingerprint.Value[..16]), path, kind, fingerprint, size, 1, null, null,
            duration, codec, profile, fps, null, audio.ToImmutable());
    }

    private async Task<string> RunProbeAsync(string path, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(_ffprobePath) { RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
        foreach (var a in new[]{"-v","error","-show_streams","-show_format","-of","json",path}) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start ffprobe.");
        var stdout = p.StandardOutput.ReadToEndAsync(ct); var stderr = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new InvalidOperationException("ffprobe failed: " + await stderr);
        return await stdout;
    }

    private static async Task<AssetFingerprint> FingerprintAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024*1024, FileOptions.Asynchronous|FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return new("SHA-256", Convert.ToHexString(hash));
    }
    private static string? GetString(JsonElement e,string n)=>e.TryGetProperty(n,out var x)?x.GetString():null;
    private static bool TryInt(JsonElement e,string n,out int v){v=0;return e.TryGetProperty(n,out var x)&&x.TryGetInt32(out v);}
    private static double? ParseRate(string? s){if(string.IsNullOrWhiteSpace(s))return null;var p=s.Split('/');if(p.Length==2&&double.TryParse(p[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var a)&&double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var b)&&b!=0)return a/b;return null;}
}
