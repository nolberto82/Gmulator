using System.Text;

namespace Gmulator;
public class Patch
{
    private byte[] PatchFile { get; set; }

    private string Header { get; set; }
    private record Record
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public byte[] Data { get; set; }
    }

    public void Run(byte[] rom, string filename)
    {
        var name = Path.GetFullPath($"Roms/{Path.GetFileNameWithoutExtension(filename)}.ips");
        if (!File.Exists(name)) return;
        using (BinaryReader br = new(new FileStream(name, FileMode.Open, FileAccess.Read)))
        {
            Header = Encoding.Default.GetString(br.ReadBytes(5));
            while (br.BaseStream.Position < br.BaseStream.Length - 3)
            {
                var offset = br.ReadBytes(3).Int24();
                var size = br.ReadBytes(2).Int16();
                if (size == 0)
                {
                    size = br.ReadBytes(2).Int16();
                    var b = br.ReadByte();
                    Array.Fill(rom, b, offset, size);
                    continue;
                }
                var data = br.ReadBytes(size);
                Array.Copy(data, 0, rom, offset, data.Length);
            }
        }
    }
}