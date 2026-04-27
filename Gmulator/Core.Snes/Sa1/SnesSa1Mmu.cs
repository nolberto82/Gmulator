using Gmulator.Interfaces;

namespace Gmulator.Core.Snes.Sa1;

public class SnesSa1Mmu : ISaveState
{
    private byte[] _ram;

    public SnesSa1Mmu()
    {
        _ram = new byte[0x800];
    }

    public byte[] GetIram() => _ram;



    public byte ReadIram(int a) => _ram[a & 0x7ff];



    public void WriteIram(int a, byte v) => _ram[a & 0x7ff] = v;

    internal void Reset() => Array.Fill<byte>(_ram, 0);

    public void Save(BinaryWriter bw)
    {
        WriteArray(bw, _ram);
    }

    public void Load(BinaryReader br)
    {
        _ram = ReadArray<byte>(br, _ram.Length);
    }
}
