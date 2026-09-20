using System.Collections.Immutable;
using System.Numerics;

namespace SlideshowCreator.Motor2.Core;

public sealed class RetainedFramePlanner : IFramePlanner
{
    public FramePlan Plan(ISceneGraph scene,TimeSpan time,PixelSize outputSize,bool exportQuality)
    {
        var evaluated=scene.Evaluate(time,outputSize);
        var requests=evaluated.Objects
            .Where(o=>o.Opacity>0 && time>=o.In && time<o.Out)
            .Select(o=>new DecodeRequest(o.Asset,RequiredFootprint(o,outputSize),null,exportQuality))
            .Distinct()
            .ToImmutableArray();
        return new(time,requests,evaluated);
    }

    private static PixelSize RequiredFootprint(SceneObject o,PixelSize output)
    {
        // Approximate projected size from retained transform. This is intentionally
        // footprint-driven: a small WALL tile does not request a full-resolution RAW.
        var sx=Math.Sqrt(o.Transform.M11*o.Transform.M11+o.Transform.M12*o.Transform.M12);
        var sy=Math.Sqrt(o.Transform.M21*o.Transform.M21+o.Transform.M22*o.Transform.M22);
        var w=Math.Clamp((int)Math.Ceiling(output.Width*Math.Max(.05,sx)),64,output.Width);
        var h=Math.Clamp((int)Math.Ceiling(output.Height*Math.Max(.05,sy)),64,output.Height);
        return new(w,h);
    }
}
