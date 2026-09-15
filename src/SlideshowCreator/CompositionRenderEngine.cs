using System.Globalization;

namespace SlideshowCreator;

public static class CompositionRenderEngine
{
    static string F(double n)=>n.ToString("0.###",CultureInfo.InvariantCulture);

    // Returns an FFmpeg filter graph for two already-normalized 1920x1080 inputs.
    // Photos remain aspect-correct in the normalized source; composition only masks/positions them.
    public static string BuildTwoPlan(string kind,double duration)
    {
        var d=Math.Max(1.2,duration);
        var move=Math.Min(.85,d*.28);
        var frames=Math.Max(1,(int)Math.Round(move*30));
        var hold=Math.Max(1,d-move);
        return kind switch
        {
            "2 Orizontal Opus" => $"[0:v]crop=1920:540:0:0[top];[1:v]crop=1920:540:0:540[bot];color=c=black:s=1920x1080:r=30:d={F(d)}[bg];[bg][top]overlay=x='if(lt(n,{frames}),-1920+1920*n/{frames},0)':y=0:shortest=1[t];[t][bot]overlay=x='if(lt(n,{frames}),1920-1920*n/{frames},0)':y=540:shortest=1,trim=duration={F(d)},setpts=PTS-STARTPTS[outv]",
            "2 Orizontal Paralel" => $"[0:v]crop=1920:540:0:0[top];[1:v]crop=1920:540:0:540[bot];color=c=black:s=1920x1080:r=30:d={F(d)}[bg];[bg][top]overlay=x='if(lt(n,{frames}),-1920+1920*n/{frames},0)':y=0:shortest=1[t];[t][bot]overlay=x='if(lt(n,{frames}),-1920+1920*n/{frames},0)':y=540:shortest=1,trim=duration={F(d)},setpts=PTS-STARTPTS[outv]",
            "Diagonal / Opus" => $"[0:v]format=rgba,geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='if(lte(Y,1080-X*1080/1920),255,0)'[a];[1:v]format=rgba,geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='if(gt(Y,1080-X*1080/1920),255,0)'[b];color=c=black:s=1920x1080:r=30:d={F(d)}[bg];[bg][a]overlay=x='if(lt(n,{frames}),-1920+1920*n/{frames},0)':y=0:shortest=1[t];[t][b]overlay=x='if(lt(n,{frames}),1920-1920*n/{frames},0)':y=0:shortest=1,trim=duration={F(d)},setpts=PTS-STARTPTS[outv]",
            "Diagonal \\ Opus" => $"[0:v]format=rgba,geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='if(lte(Y,X*1080/1920),255,0)'[a];[1:v]format=rgba,geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='if(gt(Y,X*1080/1920),255,0)'[b];color=c=black:s=1920x1080:r=30:d={F(d)}[bg];[bg][a]overlay=x='if(lt(n,{frames}),-1920+1920*n/{frames},0)':y=0:shortest=1[t];[t][b]overlay=x='if(lt(n,{frames}),1920-1920*n/{frames},0)':y=0:shortest=1,trim=duration={F(d)},setpts=PTS-STARTPTS[outv]",
            _ => $"[0:v][1:v]xfade=transition=fade:duration=.65:offset={F(hold)},format=yuv420p[outv]"
        };
    }
}
