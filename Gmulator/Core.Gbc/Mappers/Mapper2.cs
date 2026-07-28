namespace Gmulator.Core.Gbc.Mappers;

public class Mapper2 : BaseMapper
{
    public Mapper2(byte[] rom, GbcMmu mmu) : base(rom, mmu)
    {
    }

    public override void Reset() => base.Reset();

    public override void Init(byte[] rom, string filename) => base.Init(rom, filename);

    public override int ReadRom(int addr)
    {
        addr = addr + (0x4000 * (Rombank - 1));
        if (Rombank > 1)
            return Rom[addr];
        else
            return Rom[addr];
    }

    public override Span<byte> ReadRomBlock(int a, int size)
    {
        if (a <= 0x3fff)
            return new(Rom, a, size);
        else
            return new(Rom, a + 0x4000 * (Rombank - 1), size);
    }

    public override void WriteRom0(int addr, int value)
    {

    }

    public override void WriteRom1(int addr, int value)
    {
        //if (edit)
        //    Rom[addr + (0x4000 * (Rombank - 1))] = (byte)value;
        //else
        {
            if (addr >= 0x2000)
                Rombank = value & 0x1f;
        }
    }
}
