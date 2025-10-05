using System.Data;
using System.Text;

namespace Gmulator;
public class Patch
{
    private string Header { get; set; }
    private record Ips
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public byte[] Data { get; set; }
    }

    public byte[] Run(byte[] rom, string filename)
    {
        var path = Path.GetDirectoryName(filename);
        var name = Path.GetFileNameWithoutExtension(filename);
        name = $"{path}/{name}";
        if (File.Exists($"{name}.bps"))
            return RunBPS(rom, $"{name}.bps");
        else if (File.Exists($"{name}.ips"))
            return RunIPS(rom, name);
        return rom;
    }

    private byte[] RunBPS(byte[] source, string filename)
    {
        using BinaryReader br = new(new FileStream(filename, FileMode.Open, FileAccess.Read));
        if (Encoding.Default.GetString(br.ReadBytes(4)) != "BPS1") return source;

        var outputOff = 0;
        ulong sourceRelOff = 0;
        ulong targetRelOff = 0;
        var sourcesize = new byte[Decode(br)];
        var target = new byte[Decode(br)];
        var metadatasize = Decode(br);
        string metadata = Encoding.Default.GetString(br.ReadBytes((int)metadatasize));
        //if (metadatasize > 0)
        //var pos = br.BaseStream.Position;
        //var srccrc = br.ReadUInt32();
        //var tarcrc = br.ReadUInt32();
        //var patcrc = br.ReadUInt32();
        //br.BaseStream.Position = pos;

        while (br.BaseStream.Position < br.BaseStream.Length - 12)
        {
            var data = Decode(br);
            int length = (int)((data >> 2) + 1);
            switch (data & 3)
            {
                case 0:
                    while (length-- > 0)
                        target[outputOff] = source[outputOff++];
                    break;
                case 1:
                    while (length-- > 0)
                        target[outputOff++] = br.ReadByte();
                    break;
                case 2:
                    var off = Decode(br);
                    if ((off & 1) > 0)
                        sourceRelOff -= off >> 1;
                    else
                        sourceRelOff += off >> 1;
                    while (length-- > 0)
                        target[outputOff++] = source[sourceRelOff++];
                    break;
                case 3:
                    off = Decode(br);
                    if ((off & 1) > 0)
                        targetRelOff -= off >> 1;
                    else
                        targetRelOff += off >> 1;
                    while (length-- > 0)
                        target[outputOff++] = target[targetRelOff++];
                    break;
            }
        }
        return target;
    }

    private byte[] RunIPS(byte[] rom, string filename)
    {
        var name = Path.GetFullPath($"Roms/{filename}.ips");
        if (!File.Exists(name))
        {
            name = $"{filename}.ips";
            if (!File.Exists(name))
                return rom;
        }

        using BinaryReader br = new(new FileStream(name, FileMode.Open, FileAccess.Read));
        Header = Encoding.Default.GetString(br.ReadBytes(5));
        byte[] romnew = (byte[])rom.Clone();
        int romsize = romnew.Length;
        while (br.BaseStream.Position < br.BaseStream.Length - 3)
        {
            var offset = br.ReadBytes(3).Int24();
            var size = br.ReadBytes(2).Int16();
            if (size == 0)
            {
                size = br.ReadBytes(2).Int16();
                var b = br.ReadByte();
                if (offset >= romnew.Length)
                {
                    romsize += size;
                    Array.Resize(ref romnew, romsize);
                }
                Array.Fill(romnew, b, offset, size);
                continue;
            }
            var data = br.ReadBytes(size);
            if (data.Length + br.BaseStream.Position > rom.Length)
            {
                Notifications.Init("Patch Error");
                break;
            }

            if (offset >= romnew.Length)
            {
                romsize += size;
                Array.Resize(ref romnew, romsize);
            }

            Array.Copy(data, 0, romnew, offset, data.Length);
        }
        return romnew;
    }

    private static ulong Decode(BinaryReader br)
    {
        ulong data = 0, shift = 1;
        while (true)
        {
            byte b = br.ReadByte();
            data += (ulong)(b & 0x7f) * shift;
            if ((b & 0x80) > 0)
                break;
            shift <<= 7;
            data += shift;
        }
        return data;
    }
}