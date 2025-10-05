using System.Runtime.InteropServices;
using System.Text;

namespace Gmulator.Shared;
public abstract class EmuState
{
    public const string Version = "1.09";
    public abstract void Save(BinaryWriter bw);
    public abstract void Load(BinaryReader br);

    public static int SetInt(BinaryReader br, BinaryWriter bw, int v, bool save)
    {
        if (save)
        {
            bw.Write(v);
            return v;
        }
        else
        {
            return br.ReadInt32();
        }
    }

    public static int WriteInt(BinaryReader br, BinaryWriter bw, Stream st, bool save, int v)
    {
        if (save)
        {
            bw.Write(v);
            return v;
        }
        else
        {
            return br.ReadInt32();
        }
    }

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
