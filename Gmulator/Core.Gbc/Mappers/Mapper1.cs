namespace Gmulator.Core.Gbc.Mappers;

public class Mapper1 : BaseMapper
{
    public Mapper1(byte[] rom, GbcMmu mmu) : base(rom, mmu)
    {
    }

    public override void Reset() => base.Reset();

    public override void Init(byte[] rom, string filename) => base.Init(rom, filename);

    public override int ReadRom(int addr)
    {
        if (Rombank > 1 && addr >= 0x4000)
            return base.ReadRom(addr % 0x4000 + (0x4000 * Rombank));
        else
            return base.ReadRom(addr);
    }

    public override Span<byte> ReadRomBlock(int addr, int size)
    {
        if (addr <= 0x3fff)
            return new(Rom, addr, size);
        else
            return new(Rom, addr + 0x4000 * (Rombank - 1), size);
    }

    public override void WriteRom0(int addr, int value)
    {
        //if (edit)
        //    Rom[a] = v;
        //else
        {
            if (addr <= 0x1fff)
                CartRamEnabled = value == 0x0a;
            else if (addr <= 0x3fff)
                Rombank = (value & 0x1f) == 0 ? 1 : value & 0x1f;
        }
    }

    public override void WriteRom1(int addr, int value)
    {
        //if (edit)
        //    Rom[a + (0x4000 * (Rombank - 1))] = v;
        //else
        {
            if (addr <= 0x5fff)
                Rambank = value & 3;
        }
    }
}
