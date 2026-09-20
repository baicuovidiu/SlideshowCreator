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
        if (!string.Equals(pixelFormat, "RGBA8", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(pixelFormat, "R8G8B8A8_UNORM", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Native Gate A currently accepts RGBA8, not '{pixelFormat}'.");
        var resource = Motor2Native.CreateTextureRgba8(_nativeContext, checked((uint)size.Width), checked((uint)size.Height));
        if (resource == 0) throw new InvalidOperationException("D3D12 texture allocation failed.");
        return ValueTask.FromResult<object>(resource);
    }

    public ValueTask UploadTextureAsync(object texture, DecodedSurface source, CancellationToken ct)
    {
        EnsureReady(); ct.ThrowIfCancellationRequested();
        if (texture is not nint resource || resource == 0)
            throw new ArgumentException("Expected a native D3D12 texture handle.", nameof(texture));
        if (source.NativeHandle is not byte[] rgba)
            throw new NotSupportedException("Gate A upload requires a contiguous RGBA8 byte buffer.");
        var expected = checked(source.Size.Width * source.Size.Height * 4);
        if (rgba.Length < expected) throw new ArgumentException("RGBA8 buffer is smaller than the decoded surface.");
        unsafe
        {
            fixed (byte* pixels = rgba)
            {
                var hr = Motor2Native.UploadRgba8(_nativeContext, resource, pixels, checked((uint)(source.Size.Width * 4)), checked((uint)source.Size.Height));
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask ReleaseAsync(object resource, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (resource is nint native && native != 0) Motor2Native.ReleaseResource(native);
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
