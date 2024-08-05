using System.Numerics;
using System.Security.Cryptography;

namespace Gmulator;
public static class ByteExtensions
{
    public static string HexConverter(this int v)
    {
        return $"{v:X4}";
    }

    public static Vector4 Color4(this uint v)
    {
        return new Vector4(
            (v & 0xff) / 255f,
            (v >> 8 & 0xff) / 255f,
            (v >> 16 & 0xff) / 255f,
            (v >> 24 & 0xff) / 255f);
    }

    public static int Int24(this byte[] data)
    {
        return data[0] << 16 | data[1] << 8 | data[2];
    }

    public static int Int16(this byte[] data)
    {
        return data[0] << 8 | data[1];
    }

    public static byte[] GetBytes(this object b)
    {
        if (b.GetType() == typeof(bool))
            return BitConverter.GetBytes((bool)b);
        else if (b.GetType() == typeof(ushort))
            return BitConverter.GetBytes((ushort)b);
        else if (b.GetType() == typeof(float))
            return BitConverter.GetBytes((float)b);
        else
            return BitConverter.GetBytes((int)b);
    }

    public static byte Ror(this byte v, int c) => (byte)(v >> c | v << 8 - c);
    public static bool GetBit(this byte v, byte b) => (v & 1 << b) != 0;
    public static bool GetBit(this int v, byte b) => (v & 1 << b) != 0;

    public static byte GetBits(this byte v, byte b)
    {
        byte res = 0;
        for (int i = 0; i < b; i++)
            res |= (byte)(v & 1 << 1);
        return res;
    }

    public static void SetBit(ref byte v, byte b, bool o)
    {
        if (o)
            v = (byte)(v | 1 << b);
        else
            v = (byte)(v & ~(1 << b));
    }

    public static byte ClearBit(this byte v, byte b)
    {
        v = (byte)(v & ~(1 << b));
        return v;
    }
}
