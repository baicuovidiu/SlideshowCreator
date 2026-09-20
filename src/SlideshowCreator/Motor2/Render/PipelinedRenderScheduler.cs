namespace SlideshowCreator.Motor2.Core;

/// <summary>
/// First pipeline scheduler: decode/cache and GPU residency are requested ahead of compose.
/// No still decode is tied to output-frame count.
/// </summary>
public sealed class PipelinedRenderScheduler : IRenderScheduler
{
    private readonly IFramePlanner _planner;
    private readonly IDecodeService _decode;
    private readonly IGpuResourceCache _gpu;
    private readonly IGraphicsBackend _graphics;
    private readonly IVideoEncodeBackend _encoder;
    private readonly IRenderTelemetry _telemetry;

    public PipelinedRenderScheduler(IFramePlanner planner,IDecodeService decode,IGpuResourceCache gpu,
        IGraphicsBackend graphics,IVideoEncodeBackend encoder,IRenderTelemetry telemetry)
        =>(_planner,_decode,_gpu,_graphics,_encoder,_telemetry)=(planner,decode,gpu,graphics,encoder,telemetry);

    public async Task RenderAsync(ISceneGraph scene,PixelSize outputSize,double fps,TimeSpan duration,CancellationToken ct)
    {
        if(fps<=0)throw new ArgumentOutOfRangeException(nameof(fps));
        var frames=(long)Math.Ceiling(duration.TotalSeconds*fps);
        for(long i=0;i<frames;i++)
        {
            ct.ThrowIfCancellationRequested();
            var pts=TimeSpan.FromSeconds(i/fps);
            var plan=_planner.Plan(scene,pts,outputSize,true);
            var surfaces=new Dictionary<AssetId,DecodedSurface>();
            foreach(var req in plan.RequiredAssets)
            {
                var decoded=await _decode.DecodeAsync(req,ct);
                await _gpu.GetOrUploadAsync(req,decoded,ct);
                surfaces[req.Asset]=decoded;
            }
            object composed;
            using(_telemetry.Measure("gpu.compose"))
                composed=await _graphics.ComposeAsync(plan,surfaces,ct);
            using(_telemetry.Measure("encode.submit"))
                await _encoder.EncodeAsync(composed,pts,ct);
            _telemetry.Counter("render.frame",i+1);
        }
        await _encoder.FinalizeAsync(ct);
    }
}
