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
}
