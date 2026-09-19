namespace SlideshowCreator;

public enum CarouselGeometry { Split5050, Split6040, Split7030, LargeSmall, TwoByTwo, Asymmetric, DiagonalSafe, Wall, FilmStrip }
public enum CarouselMotion { Opposed, Cascade, Exchange, RevealDance, BreakApart, ReturnFull, DiagonalParade, RotationFlow, Wave, Conveyor }

public sealed record CarouselRecipe(string Name,int Planes,CarouselGeometry Geometry,CarouselMotion Motion,bool Reverse,double Stagger,int Survivor);

public sealed class CarouselPlanner
{
    readonly Random rng=new();
    readonly Queue<CarouselGeometry> recentGeometry=new();
    readonly Queue<CarouselMotion> recentMotion=new();

    public CarouselRecipe Next(int available)
    {
        int planes=available>=12 && rng.NextDouble()<.18 ? Math.Min(available,new[]{8,12,16,20,24,30}[rng.Next(6)])
          : available>=4 && rng.NextDouble()<.42 ? 4 : available>=3 && rng.NextDouble()<.30 ? 3 : 2;
        var gs=Enum.GetValues<CarouselGeometry>().Where(x=>!recentGeometry.Contains(x)).ToArray();
        if(gs.Length==0) gs=Enum.GetValues<CarouselGeometry>();
        var ms=Enum.GetValues<CarouselMotion>().Where(x=>!recentMotion.Contains(x)).ToArray();
        if(ms.Length==0) ms=Enum.GetValues<CarouselMotion>();
        var g=gs[rng.Next(gs.Length)]; var m=ms[rng.Next(ms.Length)];
        if(planes<6 && (g==CarouselGeometry.Wall||g==CarouselGeometry.FilmStrip)) g=CarouselGeometry.Asymmetric;
        if(planes==2 && m==CarouselMotion.BreakApart) m=CarouselMotion.RevealDance;
        Remember(recentGeometry,g); Remember(recentMotion,m);
        return new CarouselRecipe($"{planes}P {g} {m}",planes,g,m,rng.Next(2)==1,new[]{.12,.16,.19,.24,.31}[rng.Next(5)],rng.Next(planes));
    }

    static void Remember<T>(Queue<T> q,T v){q.Enqueue(v);while(q.Count>3)q.Dequeue();}
}
