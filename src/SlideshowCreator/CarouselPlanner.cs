namespace SlideshowCreator;

public enum CarouselGeometry { Split5050, Split6040, Split7030, LargeSmall, TwoByTwo, Asymmetric, DiagonalSafe, Wall, FilmStrip, Heart, Arc, Spiral, Ribbon, MovingContactSheet }
public enum CarouselMotion { Opposed, Cascade, Exchange, RevealDance, BreakApart, ReturnFull, DiagonalParade, RotationFlow, Wave, Conveyor, SpinBurst, ExplodeReassemble, HeartPulse, SpeedTrain, WallShatter, CornerSwarm, CenterBloom, DiagonalRain, MultiAxisParade }

public sealed record CarouselRecipe(string Name,int Planes,CarouselGeometry Geometry,CarouselMotion Motion,bool Reverse,double Stagger,int Survivor);

public sealed class CarouselPlanner
{
    readonly Random rng=new();
    readonly Queue<CarouselGeometry> recentGeometry=new();
    readonly Queue<CarouselMotion> recentMotion=new();

    // Engine 2 automatic selection exposes only families that have distinct renderers.
    // Planned families stay in the enum/specification but cannot silently masquerade as WALL.
    static readonly CarouselMotion[] ImplementedMotions =
    {
        CarouselMotion.DiagonalParade, CarouselMotion.DiagonalRain, CarouselMotion.MultiAxisParade,
        CarouselMotion.SpinBurst, CarouselMotion.ExplodeReassemble, CarouselMotion.WallShatter,
        CarouselMotion.HeartPulse, CarouselMotion.SpeedTrain
    };

    public CarouselRecipe Next(int available)
    {
        int planes=available>=12 && rng.NextDouble()<.55 ? Math.Min(available,new[]{8,12,16,20,24,30,40,60}[rng.Next(8)])
          : available>=8 ? Math.Min(available,new[]{8,12,16}[rng.Next(3)])
          : available>=4 ? 4 : available>=3 ? 3 : 2;
        var compatibleGeometry = new[]{CarouselGeometry.Wall,CarouselGeometry.FilmStrip,CarouselGeometry.Heart,CarouselGeometry.DiagonalSafe,CarouselGeometry.Asymmetric,CarouselGeometry.MovingContactSheet};
        var gs=compatibleGeometry.Where(x=>!recentGeometry.Contains(x)).ToArray();
        if(gs.Length==0) gs=compatibleGeometry;
        var ms=ImplementedMotions.Where(x=>!recentMotion.Contains(x)).ToArray();
        if(ms.Length==0) ms=ImplementedMotions;
        var g=gs[rng.Next(gs.Length)]; var m=ms[rng.Next(ms.Length)];
        if(planes<6 && (g==CarouselGeometry.Wall||g==CarouselGeometry.FilmStrip||g==CarouselGeometry.Heart||g==CarouselGeometry.MovingContactSheet)) g=CarouselGeometry.Asymmetric;
        Remember(recentGeometry,g); Remember(recentMotion,m);
        return new CarouselRecipe($"{planes}P {g} {m}",planes,g,m,rng.Next(2)==1,new[]{.12,.16,.19,.24,.31}[rng.Next(5)],rng.Next(planes));
    }

    static void Remember<T>(Queue<T> q,T v){q.Enqueue(v);while(q.Count>3)q.Dequeue();}
}
