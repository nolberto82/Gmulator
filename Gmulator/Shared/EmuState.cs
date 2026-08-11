using System.Runtime.InteropServices;

namespace Gmulator.Shared;

public class EmuState
{
    public const string Version = "1.30";

    public static void WriteArray<T>(BinaryWriter bw, T[] v) where T : unmanaged
    {
        var b = MemoryMarshal.AsBytes(v.AsSpan());
        bw.Write(b);
    }

    public static T[] ReadArray<T>(BinaryReader br, int size) where T : unmanaged
    {
        T[] v = new T[size];
        var b = MemoryMarshal.AsBytes(v.AsSpan());
        br.Read(b);
        return v;
    }
}
