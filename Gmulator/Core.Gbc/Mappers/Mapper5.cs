namespace Gmulator.Core.Gbc.Mappers;

public class Mapper5 : BaseMapper
{
    public Mapper5(byte[] rom, GbcMmu mmu) : base(rom, mmu)
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

    public override Span<byte> ReadRomBlock(int a, int size)
    {
        if (a <= 0x3fff)
            return new(Rom, a, size);
        else
            return new(Rom, a % 0x4000 + (0x4000 * Rombank), size);
    }

    public override void WriteRom0(int a, int value)
    {
        //if (edit)
        //    Rom[a] = (byte)value;
        //else
        {
            if (a <= 0x1fff)
                CartRamEnabled = value == 0x0a;
            else if (a <= 0x3fff)
                Rombank = value & 0xff;
        }
    }

    public override void WriteRom1(int a, int value)
    {
        //if (edit)
        //    Rom[a % 0x4000 + (0x4000 * Rombank)] = (byte)value;
        //else
        {
            if (a <= 0x3fff)
                Rombank |= (value << 9) & 0x100;
            else if (a <= 0x5fff)
                Rambank = value  & 3;

        }
    }
}
