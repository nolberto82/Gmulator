namespace Gmulator.Core.Gbc.Mappers;

public class Mapper0(byte[] rom, GbcMmu mmu) : BaseMapper(rom, mmu)
{
    public override void Reset() => base.Reset();

    public override void Init(byte[] rom, string filename) => base.Init(rom, filename);

    public override int ReadRom(int addr) => Rom[addr % 0x4000];

    public override Span<byte> ReadRomBlock(int addr, int size) => new();

    public override void WriteRom0(int addr, int value)
    {
        //Rom[addr] = (byte)value;
    }

    public override void WriteRom1(int addr, int value)
    {
        //if (edit)
        ///    Rom[addr] = (byte)value;
    }
}
