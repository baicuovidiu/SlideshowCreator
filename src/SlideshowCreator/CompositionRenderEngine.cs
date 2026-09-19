using System.Globalization;

namespace SlideshowCreator;

public static class CompositionRenderEngine
{
    static string F(double n) { var s=n.ToString("0.###",CultureInfo.InvariantCulture); return s.StartsWith(".") ? "0"+s : s.StartsWith("-.") ? "-0"+s[1..] : s; }

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
            "2 Orizontale Alternante" => BuildAlternatingTwo(d),
            "2 Orizontale Fluide" => $"{top};{bot};{bg};[bg][top]overlay=x='if(lt(t,{e}),-1720+(1820)*t/{e},if(lt(t,{F(d-enter)}),100,100+(1820)*(t-{F(d-enter)})/{e}))':y=55:shortest=1[t1];[t1][bot]overlay=x='if(lt(t,{de}),1920,if(lt(t,{F(delay+enter)}),1920-(1820)*(t-{de})/{e},if(lt(t,{F(d-enter)}),100,100-(1820)*(t-{F(d-enter)})/{e})))':y=555:shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]",
            _ => $"[0:v][1:v]xfade=transition=fade:duration=0.65:offset={F(Math.Max(.7,d-.65))},format=yuv420p[outv]"
        };
    }

    // Two complete horizontal planes meet from opposite sides, then alternate
    // visual priority for about two seconds. No crop; each plane remains independent.
    static string BuildAlternatingTwo(double d)
    {
        d=Math.Max(4.4,d); double en=.65, meet=.85;
        double a1=meet+.45, a2=a1+.58, a3=a2+.47, a4=a3+.62;
        double exit=Math.Max(a4+.45,d-.65);
        var dur=F(d);
        var top="[0:v]scale=1720:470:force_original_aspect_ratio=decrease,pad=1720:470:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[top]";
        var bot="[1:v]scale=1720:470:force_original_aspect_ratio=decrease,pad=1720:470:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[bot]";
        var bg=$"color=c=black:s=1920x1080:r=30:d={dur}[bg]";
        // Alternation is created by small opposing vertical shifts: each image gets
        // a clean, readable turn rather than remaining permanently eclipsed.
        var yTop=$"55+if(between(t,{F(a1)},{F(a2)}),70,if(between(t,{F(a3)},{F(a4)}),70,0))";
        var yBot=$"555-if(between(t,{F(a2)},{F(a3)}),70,if(between(t,{F(a4)},{F(exit)}),70,0))";
        return $"{top};{bot};{bg};"+
          $"[bg][top]overlay=x='if(lt(t,{F(en)}),-1720+1820*t/{F(en)},if(lt(t,{F(exit)}),100,100+1820*(t-{F(exit)})/{F(en)}))':y='{yTop}':shortest=1[t1];"+
          $"[t1][bot]overlay=x='if(lt(t,.18),1920,if(lt(t,{F(en+.18)}),1920-1820*(t-.18)/{F(en)},if(lt(t,{F(exit)}),100,100-1820*(t-{F(exit)})/{F(en)})))':y='{yBot}':shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]";
    }


    // Engine 2: dense uncropped photo wall with animated entrance and optional pulse.
    public static string BuildWall(IReadOnlyList<MediaItem> items,double duration,bool pulse=false)
    {
        int n=Math.Min(items.Count,60); if(n<2) return BuildTwoPlan("2 Orizontal Opus",duration);
        double d=Math.Max(4.5,duration); int cols=(int)Math.Ceiling(Math.Sqrt(n*16.0/9.0));
        int rows=(int)Math.Ceiling(n/(double)cols); int gap=12;
        int cw=Math.Max(120,(1920-gap*(cols+1))/cols), ch=Math.Max(90,(1080-gap*(rows+1))/rows);
        var parts=new List<string>(); var dur=F(d);
        for(int i=0;i<n;i++) parts.Add($"[{i}:v]scale={cw}:{ch}:force_original_aspect_ratio=decrease,pad={cw}:{ch}:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[p{i}]");
        parts.Add($"color=c=black:s=1920x1080:r=30:d={dur}[w0]");
        string last="w0";
        for(int i=0;i<n;i++)
        {
            int col=i%cols,row=i/cols,x=gap+col*(cw+gap),y=gap+row*(ch+gap);
            double st=.045*i, en=.42; string next=$"w{i+1}";
            string sx=(i%4) switch {0=>$"-{cw}+({x+cw})*(t-{F(st)})/{F(en)}",1=>$"1920-({1920-x})*(t-{F(st)})/{F(en)}",2=>$"{x}",_=>$"{x}"};
            string sy=(i%4) switch {2=>$"-{ch}+({y+ch})*(t-{F(st)})/{F(en)}",3=>$"1080-({1080-y})*(t-{F(st)})/{F(en)}",_=>$"{y}"};
            parts.Add($"[{last}][p{i}]overlay=x='if(lt(t,{F(st)}),-3000,if(lt(t,{F(st+en)}),{sx},{x}))':y='if(lt(t,{F(st)}),-3000,if(lt(t,{F(st+en)}),{sy},{y}))':shortest=1[{next}]");
            last=next;
        }
        if(pulse)
        {
            parts.Add($"[{last}]scale=w='1920*(1+0.018*sin(2*PI*t*1.25))':h='1080*(1+0.018*sin(2*PI*t*1.25))':eval=frame,crop=1920:1080,trim=duration={dur},setpts=PTS-STARTPTS[outv]");
        }
        else parts.Add($"[{last}]trim=duration={dur},setpts=PTS-STARTPTS[outv]");
        return string.Join(";",parts);
    }



    // Engine 2: fast horizontal photo train. Complete photos travel as independent cars.
    public static string BuildSpeedTrain(IReadOnlyList<MediaItem> items,double duration,bool reverse=false)
    {
        int n=Math.Min(items.Count,24); double d=Math.Max(2.8,Math.Min(6.0,duration));
        int carW=520,carH=760,gap=26; var parts=new List<string>(); string dur=F(d);
        for(int i=0;i<n;i++) parts.Add($"[{i}:v]scale={carW}:{carH}:force_original_aspect_ratio=decrease,pad={carW}:{carH}:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[p{i}]");
        parts.Add($"color=c=black:s=1920x1080:r=30:d={dur}[t0]");
        string last="t0"; double travel=d*.82;
        for(int i=0;i<n;i++)
        {
            double st=i*.075; string next=$"t{i+1}"; int laneY=160+(i%2)*40;
            string x=reverse
              ? $"if(lt(t,{F(st)}),-3000,-{carW}+({1920+carW+gap*n})*(t-{F(st)})/{F(travel)})"
              : $"if(lt(t,{F(st)}),3000,1920-({1920+carW+gap*n})*(t-{F(st)})/{F(travel)})";
            parts.Add($"[{last}][p{i}]overlay=x='{x}':y={laneY}:shortest=1[{next}]"); last=next;
        }
        parts.Add($"[{last}]trim=duration={dur},setpts=PTS-STARTPTS[outv]");
        return string.Join(";",parts);
    }

    // Engine 2: burst from centre, readable hold, then radial/diagonal dispersal.
    public static string BuildSpinBurst(IReadOnlyList<MediaItem> items,double duration,bool reverse=false)
    {
        int n=Math.Min(items.Count,20); double d=Math.Max(4.8,duration), enter=.75, hold=Math.Max(1.2,d*.38), exit=.85;
        int cols=(int)Math.Ceiling(Math.Sqrt(n*16.0/9.0)), rows=(int)Math.Ceiling(n/(double)cols), gap=10;
        int cw=Math.Max(150,(1920-gap*(cols+1))/cols), ch=Math.Max(110,(1080-gap*(rows+1))/rows);
        var parts=new List<string>(); string dur=F(d);
        for(int i=0;i<n;i++) parts.Add($"[{i}:v]scale={cw}:{ch}:force_original_aspect_ratio=decrease,pad={cw}:{ch}:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[p{i}]");
        parts.Add($"color=c=black:s=1920x1080:r=30:d={dur}[s0]"); string last="s0";
        for(int i=0;i<n;i++)
        {
            int col=i%cols,row=i/cols,tx=gap+col*(cw+gap),ty=gap+row*(ch+gap);
            double st=(i%7)*.055, outAt=Math.Max(st+enter+hold,d-exit);
            int dx=((i%3)-1)*1500,dy=(((i/3)%3)-1)*1000;
            string x=$"if(lt(t,{F(st)}),960,if(lt(t,{F(st+enter)}),960+({tx}-960)*(t-{F(st)})/{F(enter)},if(lt(t,{F(outAt)}),{tx},{tx}+({dx})*(t-{F(outAt)})/{F(exit)})))";
            string y=$"if(lt(t,{F(st)}),540,if(lt(t,{F(st+enter)}),540+({ty}-540)*(t-{F(st)})/{F(enter)},if(lt(t,{F(outAt)}),{ty},{ty}+({dy})*(t-{F(outAt)})/{F(exit)})))";
            string next=$"s{i+1}"; parts.Add($"[{last}][p{i}]overlay=x='{x}':y='{y}':shortest=1[{next}]"); last=next;
        }
        parts.Add($"[{last}]trim=duration={dur},setpts=PTS-STARTPTS[outv]");
        return string.Join(";",parts);
    }



    // Engine 2: true heart made from independent complete photos.
    public static string BuildPhotoHeart(IReadOnlyList<MediaItem> items,double duration)
    {
        int n=Math.Min(items.Count,36); double d=Math.Max(5.2,duration);
        int cell=150; var parts=new List<string>(); string dur=F(d);
        for(int i=0;i<n;i++) parts.Add($"[{i}:v]scale={cell}:{cell}:force_original_aspect_ratio=decrease,pad={cell}:{cell}:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[p{i}]");
        parts.Add($"color=c=black:s=1920x1080:r=30:d={dur}[h0]"); string last="h0";
        for(int i=0;i<n;i++)
        {
            double a=2*Math.PI*i/n;
            // Parametric heart, normalized into a safe 1920x1080 area.
            double hx=16*Math.Pow(Math.Sin(a),3);
            double hy=13*Math.Cos(a)-5*Math.Cos(2*a)-2*Math.Cos(3*a)-Math.Cos(4*a);
            int x=(int)Math.Round(960+hx*43-cell/2.0);
            int y=(int)Math.Round(535-hy*31-cell/2.0);
            double st=(i%9)*.055, en=.62;
            string px=$"if(lt(t,{F(st)}),960,if(lt(t,{F(st+en)}),960+({x}-960)*(t-{F(st)})/{F(en)},{x}+10*sin(2*PI*t*1.15)))";
            string py=$"if(lt(t,{F(st)}),540,if(lt(t,{F(st+en)}),540+({y}-540)*(t-{F(st)})/{F(en)},{y}+7*sin(2*PI*t*1.15)))";
            string next=$"h{i+1}";
            parts.Add($"[{last}][p{i}]overlay=x='{px}':y='{py}':shortest=1[{next}]"); last=next;
        }
        // Gentle ensemble pulse; individual photos stay uncropped.
        parts.Add($"[{last}]scale=w='1920*(1+0.022*sin(2*PI*t*1.15))':h='1080*(1+0.022*sin(2*PI*t*1.15))':eval=frame,crop=1920:1080,trim=duration={dur},setpts=PTS-STARTPTS[outv]");
        return string.Join(";",parts);
    }



    // Engine 2: uncropped diagonal slide parade, suitable for short energetic accents.
    public static string BuildDiagonalParade(IReadOnlyList<MediaItem> items,double duration,bool reverse=false)
    {
        int n=Math.Min(items.Count,16); double d=Math.Max(4.0,duration); int w=430,h=300;
        var parts=new List<string>(); string dur=F(d);
        for(int i=0;i<n;i++) parts.Add($"[{i}:v]scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[p{i}]");
        parts.Add($"color=c=black:s=1920x1080:r=30:d={dur}[d0]"); string last="d0";
        for(int i=0;i<n;i++)
        {
            double st=i*.09, travel=Math.Max(2.2,d-.4); int lane=i%4;
            string x=reverse?$"-{w}+({1920+w})*(t-{F(st)})/{F(travel)}":$"1920-({1920+w})*(t-{F(st)})/{F(travel)}";
            string y=reverse?$"{80+lane*210}+520*(t-{F(st)})/{F(travel)}":$"{600-lane*150}-420*(t-{F(st)})/{F(travel)}";
            string next=$"d{i+1}";
            parts.Add($"[{last}][p{i}]overlay=x='if(lt(t,{F(st)}),-3000,{x})':y='{y}':shortest=1[{next}]"); last=next;
        }
        parts.Add($"[{last}]trim=duration={dur},setpts=PTS-STARTPTS[outv]");
        return string.Join(";",parts);
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

    // Four independent complete images assemble, then the composition breaks to
    // two favored planes and finally one survivor. The survivor is revealed Full Frame.
    public static string BuildFourToTwoToOne(double duration)
    {
        var d=Math.Max(5.4,duration); var en=.55; var st=.16;
        var breakAt=Math.Max(2.15,d*.48); var twoAt=breakAt+.65; var oneAt=Math.Max(twoAt+.85,d-1.05);
        var dur=F(d);
        string P(int i,string l)=>$"[{i}:v]scale=820:390:force_original_aspect_ratio=decrease,pad=820:390:(ow-iw)/2:(oh-ih)/2:color=black@0,format=rgba[{l}]";
        var full="[0:v]scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black,format=rgba[full]";
        var bg=$"color=c=black:s=1920x1080:r=30:d={dur}[bg]";
        return $"{P(0,"p0")};{P(1,"p1")};{P(2,"p2")};{P(3,"p3")};{full};{bg};"+
          $"[bg][p0]overlay=x='if(lt(t,{F(en)}),-820+900*t/{F(en)},80)':y=105:shortest=1[a];"+
          $"[a][p1]overlay=x='if(lt(t,{F(st)}),1920,if(lt(t,{F(st+en)}),1920-900*(t-{F(st)})/{F(en)},1020))':y=105:shortest=1[b];"+
          $"[b][p2]overlay=x='if(lt(t,{F(st*2)}),-820,if(lt(t,{F(st*2+en)}),-820+900*(t-{F(st*2)})/{F(en)},if(lt(t,{F(breakAt)}),80,80-900*(t-{F(breakAt)})/.65)))':y=585:shortest=1[c];"+
          $"[c][p3]overlay=x='if(lt(t,{F(st*3)}),1920,if(lt(t,{F(st*3+en)}),1920-900*(t-{F(st*3)})/{F(en)},if(lt(t,{F(breakAt)}),1020,1020+900*(t-{F(breakAt)})/.65)))':y=585:shortest=1[d];"+
          $"[d][p1]overlay=x='if(lt(t,{F(oneAt)}),1020,1020+900*(t-{F(oneAt)})/.55)':y=345:enable='gte(t,{F(twoAt)})':shortest=1[e];"+
          $"[e][full]overlay=x=0:y=0:enable='gte(t,{F(oneAt+.45)})':shortest=1,trim=duration={dur},setpts=PTS-STARTPTS[outv]";
    }
}
