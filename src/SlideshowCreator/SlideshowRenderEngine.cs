using System.Globalization;
using System.Text;

namespace SlideshowCreator;

/// <summary>
/// MVP 0.1.5 render graph builder. Builds one continuous FFmpeg filter graph so
/// transitions are rendered between originals instead of concatenating hard cuts.
/// No zoom, blur or destructive crop is introduced.
/// </summary>
public static class SlideshowRenderEngine
{
    public static string BuildPhotoFilter(IReadOnlyList<MediaItem> items, int width = 1920, int height = 1080, int fps = 30)
    {
        if (items.Count == 0) throw new ArgumentException("Nu există imagini pentru randare.");
        var sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            sb.Append($"[{i}:v]scale={width}:{height}:force_original_aspect_ratio=decrease,");
            sb.Append($"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black,");
            sb.Append($"fps={fps},format=yuv420p,settb=AVTB,setpts=PTS-STARTPTS[v{i}];");
        }

        if (items.Count == 1) { sb.Append("[v0]null[outv]"); return sb.ToString(); }

        double timeline = Math.Max(.1, items[0].Duration);
        string previous = "v0";
        for (int i = 1; i < items.Count; i++)
        {
            var prior = items[i - 1];
            double td = Math.Min(Math.Max(0.05, prior.TransitionDuration), Math.Min(prior.Duration, items[i].Duration) * .45);
            string transition = MapTransition(prior.Transition);
            double offset = Math.Max(0, timeline - td);
            string output = i == items.Count - 1 ? "outv" : $"x{i}";
            sb.Append($"[{previous}][v{i}]xfade=transition={transition}:duration={F(td)}:offset={F(offset)}[{output}]");
            if (i != items.Count - 1) sb.Append(';');
            timeline = timeline + Math.Max(.1, items[i].Duration) - td;
            previous = output;
        }
        return sb.ToString();
    }

    public static double OutputDuration(IReadOnlyList<MediaItem> items)
    {
        if (items.Count == 0) return 0;
        double total = items.Sum(x => Math.Max(.1, x.Duration));
        for (int i = 0; i < items.Count - 1; i++)
            total -= Math.Min(Math.Max(.05, items[i].TransitionDuration), Math.Min(items[i].Duration, items[i + 1].Duration) * .45);
        return Math.Max(.1, total);
    }

    static string MapTransition(string? name) => name switch
    {
        "Fade Black" => "fadeblack",
        "Slide Stânga" => "slideleft",
        "Slide Dreapta" => "slideright",
        "Wipe Stânga" => "wipeleft",
        "Wipe Dreapta" => "wiperight",
        "Wipe Sus" => "wipeup",
        "Wipe Jos" => "wipedown",
        "Diagonal TL" => "diagtl",
        "Diagonal TR" => "diagtr",
        "Diagonal BL" => "diagbl",
        "Diagonal BR" => "diagbr",
        "Dissolve" => "dissolve",
        _ => "fade"
    };

    static string F(double value) { var s=value.ToString("0.###",CultureInfo.InvariantCulture); return s.StartsWith(".")?"0"+s:s.StartsWith("-.")?"-0"+s[1..]:s; }
}
