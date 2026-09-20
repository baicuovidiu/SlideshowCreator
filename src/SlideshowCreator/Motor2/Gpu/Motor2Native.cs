using System.Runtime.InteropServices;

namespace SlideshowCreator.Motor2.Core;

internal static partial class Motor2Native
{
    private const string LibraryName = "Motor2Native";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal unsafe struct AdapterInfo
    {
        public fixed char Name[128];
        public ulong DedicatedVideoMemory;
        public uint FeatureLevel;

        public string GetName()
        {
            fixed (char* p = Name) return new string(p);
        }
    }

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_probe")]
    internal static unsafe partial int Probe(AdapterInfo* info);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_create")]
    internal static partial nint Create();

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_destroy")]
    internal static partial void Destroy(nint context);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_create_texture_rgba8")]
    internal static partial nint CreateTextureRgba8(nint context, uint width, uint height);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_upload_rgba8")]
    internal static unsafe partial int UploadRgba8(nint context, nint texture, void* pixels, uint rowPitch, uint height);

    [StructLayout(LayoutKind.Sequential)]
    internal struct DrawQuad
    {
        public nint Texture; public uint SrvIndex;
        public float M11, M12, M21, M22, M31, M32, Opacity, Z;
    }

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_create_render_target")]
    internal static partial nint CreateRenderTarget(nint context, uint width, uint height);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_begin_frame")]
    internal static partial int BeginFrame(nint context, nint target);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_draw_quads")]
    internal static unsafe partial int DrawQuads(nint context, nint target, DrawQuad* commands, uint count);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_end_frame")]
    internal static partial int EndFrame(nint context, nint target);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_readback_rgba16f")]
    internal static unsafe partial int ReadbackRgba16f(nint context,nint target,void* destination,uint destinationBytes);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_release_resource")]
    internal static partial void ReleaseResource(nint context, nint resource);

    [LibraryImport(LibraryName, EntryPoint = "motor2_d3d12_release_render_target")]
    internal static partial void ReleaseRenderTarget(nint context, nint target);
}
