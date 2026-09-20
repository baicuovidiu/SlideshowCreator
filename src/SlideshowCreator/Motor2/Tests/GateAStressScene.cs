using System.Collections.Immutable;
using System.Numerics;

namespace SlideshowCreator.Motor2.Core;

/// <summary>Deterministic 120-object Gate-A scene used before user-facing builds.</summary>
public sealed class GateAStressScene : ISceneGraph
{
    private readonly ImmutableArray<AssetId> _assets;
    public GateAStressScene(IEnumerable<AssetId> assets)=>_assets=assets.Take(120).ToImmutableArray();

    public EvaluatedScene Evaluate(TimeSpan time,PixelSize outputSize)
    {
        var b=ImmutableArray.CreateBuilder<SceneObject>(_assets.Length);
        var cols=12; var rows=Math.Max(1,(int)Math.Ceiling(_assets.Length/(double)cols));
        var cellW=outputSize.Width/(float)cols; var cellH=outputSize.Height/(float)rows;
        for(int i=0;i<_assets.Length;i++)
        {
            var col=i%cols; var row=i/cols;
            var phase=(float)(time.TotalSeconds*.35+i*.071);
            var dx=MathF.Sin(phase)*cellW*.08f; var dy=MathF.Cos(phase*.83f)*cellH*.08f;
            var transform=Matrix3x2.CreateScale(.075f)*Matrix3x2.CreateTranslation(col*cellW+dx,row*cellH+dy);
            b.Add(new(GuidUtility(i),_assets[i],transform,1,i,TimeSpan.Zero,TimeSpan.MaxValue));
        }
        return new(time,outputSize,b.ToImmutable());
    }
    private static Guid GuidUtility(int i){Span<byte>b=stackalloc byte[16];BitConverter.TryWriteBytes(b,i);return new Guid(b);}
}
