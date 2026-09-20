using System.Runtime.InteropServices;

namespace SlideshowCreator.Motor2.Core;

public sealed class D3D12NativeDeviceContext : ID3D12DeviceContext
{
    private nint _nativeContext;
    private bool _initialized;

    public string AdapterName { get; private set; } = "Uninitialized";
    public long DedicatedVideoMemoryBytes { get; private set; }

    public unsafe ValueTask InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("D3D12 requires Windows.");

        Motor2Native.AdapterInfo info = default;
        var hr = Motor2Native.Probe(&info);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);

        _nativeContext = Motor2Native.Create();
        if (_nativeContext == 0) throw new InvalidOperationException("Native D3D12 device creation failed.");

        AdapterName = info.GetName();
        DedicatedVideoMemoryBytes = checked((long)info.DedicatedVideoMemory);
        _initialized = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask<object> CreateTextureAsync(PixelSize size, string pixelFormat, CancellationToken ct)
    {
        EnsureReady(); ct.ThrowIfCancellationRequested();
        throw new NotSupportedException("Gate A texture creation is the next native milestone.");
    }

    public ValueTask UploadTextureAsync(object texture, DecodedSurface source, CancellationToken ct)
    {
        EnsureReady(); ct.ThrowIfCancellationRequested();
        throw new NotSupportedException("Gate A copy-queue upload is the next native milestone.");
    }

    public ValueTask ReleaseAsync(object resource, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_nativeContext != 0) Motor2Native.Destroy(_nativeContext);
        _nativeContext = 0;
        _initialized = false;
        AdapterName = "Uninitialized";
        DedicatedVideoMemoryBytes = 0;
        return ValueTask.CompletedTask;
    }

    private void EnsureReady()
    {
        if (!_initialized || _nativeContext == 0)
            throw new InvalidOperationException("D3D12 context is not initialized.");
    }
}
