using System.Globalization;

namespace SlideshowCreator;

public static class CompositionRenderEngine
{
    static string F(double n)=>n.ToString("0.###",CultureInfo.InvariantCulture);

    // 0.1.8: every source remains COMPLETE. We scale-to-contain inside an independent
    // transparent plane; no crop, no diagonal mask cutting people. Each plane has
    // its own delayed entrance, Canva-style.
    public static string BuildTwoPlan(string kind,double duration)
    {
        var d=Math.Max(2.0,duration);
        var enter=Math.Min(.75,d*.22);
        var delay=Math.Min(.45,d*.12);
        var e=F(enter); var de=F(delay); var dur=F(d);

        // Horizontal planes are 1720x470, leaving breathing room and a visible gap.
        var top="[0:v]scale=1720:470:force_original_aspect_ratio=decrease,pad=1720:470:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[top]";
        var bot="[1:v]scale=1720:470:force_original_aspect_ratio=decrease,pad=1720:470:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[bot]";
        // Diagonal layout uses two complete, smaller photographs. The diagonal is
        // created by placement/overlap, never by slicing either photograph.
        var a="[0:v]scale=1180:780:force_original_aspect_ratio=decrease,pad=1180:780:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[a]";
        var b="[1:v]scale=1180:780:force_original_aspect_ratio=decrease,pad=1180:780:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[b]";
        var bg=$"color=c=black:s=1920x1080:r=30:d={dur}[bg]";

        return kind switch
        {
            "2 Orizontal Opus" =>
                $"{top};{bot};{bg};[bg][top]overlay=x='if(lt(t,{e}),-1720+(1820)*t/{e},100)':y=55:shortest=1[t1];[t1][bot]overlay=x='if(lt(t,{de}),1920,if(lt(t,{F(delay+enter)}),1920-(1820)*(t-{de})/{e},100))':y=555:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            "2 Orizontal Paralel" =>
                $"{top};{bot};{bg};[bg][top]overlay=x='if(lt(t,{e}),-1720+(1820)*t/{e},100)':y=55:shortest=1[t1];[t1][bot]overlay=x='if(lt(t,{de}),-1720,if(lt(t,{F(delay+enter)}),-1720+(1820)*(t-{de})/{e},100))':y=555:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            "Diagonal / Opus" =>
                $"{a};{b};{bg};[bg][a]overlay=x='if(lt(t,{e}),-1180+(1300)*t/{e},120)':y=70:shortest=1[t1];[t1][b]overlay=x='if(lt(t,{de}),1920,if(lt(t,{F(delay+enter)}),1920-(1300)*(t-{de})/{e},620))':y=230:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            "Diagonal \\ Opus" =>
                $"{a};{b};{bg};[bg][a]overlay=x='if(lt(t,{e}),1920-(1300)*t/{e},620)':y=70:shortest=1[t1];[t1][b]overlay=x='if(lt(t,{de}),-1180,if(lt(t,{F(delay+enter)}),-1180+(1300)*(t-{de})/{e},120))':y=230:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            _ => $"[0:v][1:v]xfade=transition=fade:duration=.65:offset={F(Math.Max(.7,d-.65))},format=yuv420p[outv]"
        };
    }
}
