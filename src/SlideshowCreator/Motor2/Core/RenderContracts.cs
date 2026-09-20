using System.Collections.Immutable;
using System.Numerics;

namespace SlideshowCreator.Motor2.Core;

public sealed record SceneObject(
    Guid Id,
    AssetId Asset,
    Matrix3x2 Transform,
    float Opacity,
    float Z,
    TimeSpan In,
    TimeSpan Out);

public sealed record EvaluatedScene(
    TimeSpan Time,
    PixelSize OutputSize,
    ImmutableArray<SceneObject> Objects);

public sealed record MusicalEvent(TimeSpan Time, string Kind, float Strength);

public sealed record ChoreographyRequest(
    ImmutableArray<MediaAsset> Assets,
    ImmutableArray<MusicalEvent> Music,
    PixelSize OutputSize,
    ulong Seed,
    TimeSpan Duration);

public interface ISceneGraph
{
    EvaluatedScene Evaluate(TimeSpan time, PixelSize outputSize);
}

public interface IChoreographyEngine
{
    ISceneGraph Generate(ChoreographyRequest request);
}

public sealed record ConstraintViolation(Guid? ObjectId, string Rule, string Message);

public interface IConstraintEngine
{
    ImmutableArray<ConstraintViolation> Validate(ISceneGraph scene, ChoreographyRequest request);
}

public sealed record FramePlan(
    TimeSpan Time,
    ImmutableArray<DecodeRequest> RequiredAssets,
    EvaluatedScene Scene);

public interface IFramePlanner
{
    FramePlan Plan(ISceneGraph scene, TimeSpan time, PixelSize outputSize, bool exportQuality);
}

public interface IGraphicsBackend
{
    ValueTask InitializeAsync(HardwareCapabilities capabilities, CancellationToken ct);
    ValueTask<object> ComposeAsync(FramePlan frame, IReadOnlyDictionary<AssetId, GpuTextureHandle> textures, CancellationToken ct);
}

public interface IVideoEncodeBackend
{
    ValueTask InitializeAsync(PixelSize size, double fps, HardwareCapabilities capabilities, CancellationToken ct);
    ValueTask EncodeAsync(object gpuFrame, TimeSpan pts, CancellationToken ct);
    ValueTask FinalizeAsync(CancellationToken ct);
}

public interface IRenderScheduler
{
    Task RenderAsync(ISceneGraph scene, PixelSize outputSize, double fps, TimeSpan duration, CancellationToken ct);
}
