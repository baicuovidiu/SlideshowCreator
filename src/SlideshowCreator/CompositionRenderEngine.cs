using System.Globalization;

namespace SlideshowCreator;

public static class CompositionRenderEngine
{
    static string F(double n)=>n.ToString("0.###",CultureInfo.InvariantCulture);

    // Two independent complete-media planes. No automatic crop.
    public static string BuildTwoPlan(string kind,double duration)
    {
        var d=Math.Max(2.0,duration);
        var enter=Math.Min(.75,d*.22);
        var delay=Math.Min(.45,d*.12);
        var e=F(enter); var de=F(delay); var dur=F(d);
        var top="[0:v]scale=1720:470:force_original_aspect_ratio=decrease,pad=1720:470:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[top]";
        var bot="[1:v]scale=1720:470:force_original_aspect_ratio=decrease,pad=1720:470:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[bot]";
        var a="[0:v]scale=1180:780:force_original_aspect_ratio=decrease,pad=1180:780:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[a]";
        var b="[1:v]scale=1180:780:force_original_aspect_ratio=decrease,pad=1180:780:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[b]";
        var bg=$"color=c=black:s=1920x1080:r=30:d={dur}[bg]";
        return kind switch
        {
            "2 Orizontal Opus" => $"{top};{bot};{bg};[bg][top]overlay=x='if(lt(t,{e}),-1720+(1820)*t/{e},100)':y=55:shortest=1[t1];[t1][bot]overlay=x='if(lt(t,{de}),1920,if(lt(t,{F(delay+enter)}),1920-(1820)*(t-{de})/{e},100))':y=555:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            "2 Orizontal Paralel" => $"{top};{bot};{bg};[bg][top]overlay=x='if(lt(t,{e}),-1720+(1820)*t/{e},100)':y=55:shortest=1[t1];[t1][bot]overlay=x='if(lt(t,{de}),-1720,if(lt(t,{F(delay+enter)}),-1720+(1820)*(t-{de})/{e},100))':y=555:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            "Diagonal / Opus" => $"{a};{b};{bg};[bg][a]overlay=x='if(lt(t,{e}),-1180+(1300)*t/{e},120)':y=70:shortest=1[t1];[t1][b]overlay=x='if(lt(t,{de}),1920,if(lt(t,{F(delay+enter)}),1920-(1300)*(t-{de})/{e},620))':y=230:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            "Diagonal \\ Opus" => $"{a};{b};{bg};[bg][a]overlay=x='if(lt(t,{e}),1920-(1300)*t/{e},620)':y=70:shortest=1[t1];[t1][b]overlay=x='if(lt(t,{de}),-1180,if(lt(t,{F(delay+enter)}),-1180+(1300)*(t-{de})/{e},120))':y=230:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            "2 Orizontale Fluide" => $"{top};{bot};{bg};[bg][top]overlay=x='if(lt(t,{e}),-1720+(1820)*t/{e},if(lt(t,{F(d-enter)}),100,100+(1820)*(t-{F(d-enter)})/{e}))':y=55:shortest=1[t1];[t1][bot]overlay=x='if(lt(t,{de}),1920,if(lt(t,{F(delay+enter)}),1920-(1820)*(t-{de})/{e},if(lt(t,{F(d-enter)}),100,100-(1820)*(t-{F(d-enter)})/{e})))':y=555:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            _ => $"[0:v][1:v]xfade=transition=fade:duration=.65:offset={F(Math.Max(.7,d-.65))},format=yuv420p[outv]"
        };
    }

    // Four COMPLETE independent planes enter one-by-one. Near the end, three leave
    // independently and the selected survivor (input 0) grows into a complete
    // Full Frame. No source is cropped at any point.
    public static string BuildFourToOne(double duration)
    {
        var d=Math.Max(4.2,duration);
        var enter=.55; var stagger=.18;
        var leaveStart=Math.Max(2.35,d-1.35); var leave=.55;
        var growStart=leaveStart+.35; var grow=Math.Max(.65,d-growStart);
        var dur=F(d);
        string P(int i,string label)=>$"[{i}:v]scale=820:390:force_original_aspect_ratio=decrease,pad=820:390:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[{label}]";
        var p0=P(0,"p0"); var p1=P(1,"p1"); var p2=P(2,"p2"); var p3=P(3,"p3");
        var full="[0:v]scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black,format=rgba[full]";
        var bg=$"color=c=black:s=1920x1080:r=30:d={dur}[bg]";
        string t1=F(stagger),t2=F(stagger*2),t3=F(stagger*3),en=F(enter),ls=F(leaveStart),le=F(leave),gs=F(growStart),gr=F(grow);
        return $"{p0};{p1};{p2};{p3};{full};{bg};"+
          $"[bg][p0]overlay=x='if(lt(t,{en}),-820+900*t/{en},80)':y=105:shortest=1[a];"+
          $"[a][p1]overlay=x='if(lt(t,{t1}),1920,if(lt(t,{F(stagger+enter)}),1920-900*(t-{t1})/{en},1020))':y=105:shortest=1[b];"+
          $"[b][p2]overlay=x='if(lt(t,{t2}),-820,if(lt(t,{F(stagger*2+enter)}),-820+900*(t-{t2})/{en},if(lt(t,{ls}),80,80-900*(t-{ls})/{le})))':y=585:shortest=1[c];"+
          $"[c][p3]overlay=x='if(lt(t,{t3}),1920,if(lt(t,{F(stagger*3+enter)}),1920-900*(t-{t3})/{en},if(lt(t,{ls}),1020,1020+900*(t-{ls})/{le})))':y=585:shortest=1[d];"+
          $"[d][full]overlay=x=0:y=0:enable='gte(t,{gs})':alpha='straight':shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]";
    }
}
