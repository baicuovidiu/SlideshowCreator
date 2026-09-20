using System.Numerics;

namespace SlideshowCreator.Motor2.Core;

public static class PhotoLayout
{
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
