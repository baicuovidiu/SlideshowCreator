using System.IO;
using System.Globalization;

namespace SlideshowCreator;

/// <summary>
/// 0.1.11 direct export pipeline.
/// Full-frame photos/videos are NOT pre-encoded to one MP4 per source.
/// Only CARUSEL segments that genuinely need compositing are rendered as temporary
/// segments; the main timeline consumes originals directly and is encoded once.
/// </summary>
public static class DirectExportEngine
{
    public static void Export(
        IReadOnlyList<MediaItem> source, string destination, string? music,
        string encoder, string preset, Action<string> ffmpeg,
        Func<string,double> probe, Action<double,string>? progress)
    {
        if(source.Count==0) throw new ArgumentException("Nu există media.");
        var dir=Path.Combine(Path.GetTempPath(),"SlideshowCreatorDirect",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            progress?.Invoke(2,"0.1.11 • Motor direct: analizez timeline-ul...");
            var sequence=new List<MediaItem>();
            int carouselCount=0;
            var planner=new CarouselPlanner();
            for(int i=0;i<source.Count;i++)
            {
                var m=source[i];

                // Engine 2 automatic dense accent: periodically replace a plain automatic
                // section with a many-photo animated WALL. Sources remain complete/uncropped.
                if(m.Composition=="CARUSEL AUTOMAT" && source.Count-i>=8 && (i==0 || i%24==0))
                {
                    var recipe=planner.Next(source.Count-i);
                    int take=Math.Min(source.Count-i,Math.Max(8,recipe.Planes));
                    if(take>=8)
                    {
                        var inputs=source.Skip(i).Take(take).ToArray();
                        var dur=Math.Max(5.0,Math.Min(9.0,4.5+take*.06));
                        var outp=Path.Combine(dir,$"engine2_{carouselCount++:000}.mp4");
                        var graph=recipe.Motion switch
                        {
                            CarouselMotion.SpeedTrain => CompositionRenderEngine.BuildSpeedTrain(inputs,dur,recipe.Reverse),
                            CarouselMotion.HeartPulse => CompositionRenderEngine.BuildPhotoHeart(inputs,dur),
                            CarouselMotion.DiagonalParade or CarouselMotion.DiagonalRain or CarouselMotion.MultiAxisParade => CompositionRenderEngine.BuildDiagonalParade(inputs,dur,recipe.Reverse),
                            CarouselMotion.SpinBurst or CarouselMotion.ExplodeReassemble or CarouselMotion.WallShatter => CompositionRenderEngine.BuildSpinBurst(inputs,dur,recipe.Reverse),
                            _ => CompositionRenderEngine.BuildWall(inputs,dur,recipe.Motion==CarouselMotion.HeartPulse)
                        };
                        ffmpeg($"-y {Inputs(inputs)} -filter_complex \"{graph}\" -map \"[outv]\" -an -c:v {encoder} -preset {preset} -pix_fmt yuv420p \"{outp}\"");
                        sequence.Add(CloneRendered(m,outp,dur));
                        i+=take-1;
                        progress?.Invoke(5+30.0*(i+1)/source.Count,$"CARUSEL Engine 2 • {recipe.Name}");
                        continue;
                    }
                }
                bool four=(m.Composition=="4 Cadrane → 1"||m.Composition=="4 → 2 → 1") && i+3<source.Count;
                bool two=m.Composition!="Full Frame" && !four && i+1<source.Count;
                if(four)
                {
                    var inputs=source.Skip(i).Take(4).ToArray();
                    var dur=Math.Max(4.2,inputs.Min(x=>x.Duration));
                    var outp=Path.Combine(dir,$"carousel_{carouselCount++:000}.mp4");
                    var graph=m.Composition=="4 → 2 → 1"?CompositionRenderEngine.BuildFourToTwoToOne(dur):CompositionRenderEngine.BuildFourToOne(dur);
                    ffmpeg($"-y {Inputs(inputs)} -filter_complex \"{graph}\" -map \"[outv]\" -an -c:v {encoder} -preset {preset} -pix_fmt yuv420p \"{outp}\"");
                    sequence.Add(CloneRendered(m,outp,dur)); i+=3;
                }
                else if(two)
                {
                    var inputs=new[]{m,source[i+1]};
                    var dur=Math.Max(2.0,Math.Min(inputs[0].Duration,inputs[1].Duration));
                    var outp=Path.Combine(dir,$"carousel_{carouselCount++:000}.mp4");
                    var graph=CompositionRenderEngine.BuildTwoPlan(m.Composition,dur);
                    ffmpeg($"-y {Inputs(inputs)} -filter_complex \"{graph}\" -map \"[outv]\" -an -c:v {encoder} -preset {preset} -pix_fmt yuv420p \"{outp}\"");
                    sequence.Add(CloneRendered(m,outp,dur)); i++;
                }
                else sequence.Add(m);

                progress?.Invoke(5+30.0*(i+1)/source.Count,$"Motor direct • CARUSEL {Math.Min(i+1,source.Count)} / {source.Count}");
            }

            progress?.Invoke(38,$"Randare directă din originale • {sequence.Count} segmente...");
            var visual=Path.Combine(dir,"visual.mp4");
            var inputArgs=Inputs(sequence);
            var graphMain=SlideshowRenderEngine.BuildPhotoFilter(sequence);
            var graphFile=Path.Combine(dir,"timeline.ffscript");
            File.WriteAllText(graphFile,graphMain);
            ffmpeg($"-y {inputArgs} -filter_complex_script \"{graphFile}\" -map \"[outv]\" -an -c:v {encoder} -preset {preset} -pix_fmt yuv420p \"{visual}\"");

            progress?.Invoke(86,"Finalizare într-o singură trecere...");
            var total=probe(visual);
            var intro=Math.Min(1.25,Math.Max(.25,total*.12));
            var outro=Math.Min(1.8,Math.Max(.35,total*.15));
            var outStart=Math.Max(intro,total-outro);
            if(string.IsNullOrWhiteSpace(music))
                ffmpeg($"-y -i \"{visual}\" -vf \"fade=t=in:st=0:d={F(intro)},fade=t=out:st={F(outStart)}:d={F(outro)}\" -an -c:v {encoder} -preset {preset} -pix_fmt yuv420p -movflags +faststart \"{destination}\"");
            else
                ffmpeg($"-y -i \"{visual}\" -stream_loop -1 -i \"{music}\" -vf \"fade=t=in:st=0:d={F(intro)},fade=t=out:st={F(outStart)}:d={F(outro)}\" -filter:a \"afade=t=in:st=0:d={F(intro)},afade=t=out:st={F(outStart)}:d={F(outro)}\" -map 0:v -map 1:a -c:v {encoder} -preset {preset} -pix_fmt yuv420p -c:a aac -b:a 256k -t {F(total)} -movflags +faststart \"{destination}\"");
            progress?.Invoke(100,"Export 0.1.11 finalizat");
        }
        finally { try{Directory.Delete(dir,true);}catch{} }
    }

    static MediaItem CloneRendered(MediaItem src,string path,double duration)=>new()
    {
        Path=path,Type="Video",Duration=duration,SourceDuration=duration,
        Transition=src.Transition,TransitionDuration=src.TransitionDuration,Composition="Full Frame"
    };

    static string Inputs(IEnumerable<MediaItem> items)=>string.Join(" ",items.Select(Input));

    static string Input(MediaItem m)
    {
        if(m.Type=="Foto") return $"-loop 1 -t {F(m.Duration)} -i \"{m.Path}\"";
        return $"-ss {F(m.TrimIn)} -t {F(m.Duration)} -i \"{m.Path}\"";
    }

    static string F(double n)=>n.ToString("0.###",CultureInfo.InvariantCulture);
}
