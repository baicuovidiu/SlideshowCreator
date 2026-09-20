using System.Runtime.InteropServices;

namespace SlideshowCreator.Motor2.Core;

/// <summary>
/// Native D3D12 bootstrap boundary. COM ownership stays isolated from choreography.
/// Gate A will replace placeholders with concrete ID3D12Device/queues/fences.
/// </summary>
public sealed class D3D12NativeDeviceContext : ID3D12DeviceContext
{
    private nint _device;
    private nint _directQueue;
    private nint _copyQueue;
    private bool _initialized;

    public string AdapterName { get; private set; } = "Uninitialized";
    public long DedicatedVideoMemoryBytes { get; private set; }

    public ValueTask InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("D3D12 requires Windows.");
        // Do not claim capability until the native probe creates a real device.
        // Native bootstrap is deliberately isolated here for deterministic fail-fast behavior.
        _initialized = true;
        AdapterName = "D3D12 native bootstrap pending real adapter selection";
        return ValueTask.CompletedTask;
    }

    public ValueTask<object> CreateTextureAsync(PixelSize size,string pixelFormat,CancellationToken ct)
    {
        EnsureReady(); ct.ThrowIfCancellationRequested();
        throw new NotSupportedException("Gate A native texture creation is not wired yet.");
    }

    public ValueTask UploadTextureAsync(object texture,DecodedSurface source,CancellationToken ct)
    {
        EnsureReady(); ct.ThrowIfCancellationRequested();
        throw new NotSupportedException("Gate A native copy queue is not wired yet.");
    }

    public ValueTask ReleaseAsync(object resource,CancellationToken ct){ct.ThrowIfCancellationRequested();return ValueTask.CompletedTask;}
    public ValueTask DisposeAsync(){_device=_directQueue=_copyQueue=0;_initialized=false;return ValueTask.CompletedTask;}
    private void EnsureReady(){if(!_initialized)throw new InvalidOperationException("D3D12 context is not initialized.");}
}
