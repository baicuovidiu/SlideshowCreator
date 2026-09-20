using System.Numerics;

namespace SlideshowCreator.Motor2.Core;

public static class PhotoLayout
{
    /// <summary>Converts a pixel-space affine transform into the NDC convention consumed by the D3D12 quad shader.</summary>
    public static Matrix3x2 PixelToNdc(Matrix3x2 pixelTransform, PixelSize source, PixelSize output)
    {
        if(source.Width<=0||source.Height<=0||output.Width<=0||output.Height<=0) throw new ArgumentOutOfRangeException();
        // Shader source quad spans [-1,+1]. Map it first to source pixels, apply the scene pixel transform,
        // then map output pixels to D3D NDC. Y is inverted because pixel Y grows down while NDC Y grows up.
        var sourcePixels=Matrix3x2.CreateScale(source.Width*.5f,source.Height*.5f)*Matrix3x2.CreateTranslation(source.Width*.5f,source.Height*.5f);
        var toNdc=Matrix3x2.CreateScale(2f/output.Width,-2f/output.Height)*Matrix3x2.CreateTranslation(-1f,1f);
        return sourcePixels*pixelTransform*toNdc;
    }

    /// <summary>Returns a centered CONTAIN transform. It never crops or changes aspect ratio.</summary>
    public static Matrix3x2 Contain(PixelSize source,PixelSize viewport)
    {
        if(source.Width<=0||source.Height<=0||viewport.Width<=0||viewport.Height<=0)throw new ArgumentOutOfRangeException();
        var scale=Math.Min((float)viewport.Width/source.Width,(float)viewport.Height/source.Height);
        var w=source.Width*scale; var h=source.Height*scale;
        var x=(viewport.Width-w)*.5f; var y=(viewport.Height-h)*.5f;
        return Matrix3x2.CreateScale(scale)*Matrix3x2.CreateTranslation(x,y);
    }
}
