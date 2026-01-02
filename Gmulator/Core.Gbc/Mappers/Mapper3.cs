namespace Gmulator.Core.Gbc.Mappers;

public class Mapper3 : BaseMapper
{
    public Mapper3(byte[] rom, GbcMmu mmu) : base(rom, mmu)
    {
    }

    public override void Reset() => base.Reset();

    public override void Init(byte[] rom, string filename)
    {
        base.Init(rom, filename);
    }

    public override int ReadRom(int a)
    {
        if (a <= 0x3fff)
            return Rom[a];
        else
            return Rom[a + (0x4000 * (Rombank - 1))];
    }

    public override Span<byte> ReadRomBlock(int a, int size)
    {
        if (a <= 0x3fff)
            return new(Rom, a, size);
        else
            return new(Rom, a + 0x4000 * (Rombank - 1), size);
    }

    public override void WriteRom0(int a, int v)
    {
        //if (edit)
        //    Rom[a] = v;
        // else
        {
            if (a <= 0x1fff)
                CartRamOn = v == 0x0a;
            else if (a <= 0x3fff)
                Rombank = v == 0 ? 1 : v & 0x7f;
        }
    }

    public override void WriteRom1(int a, int v)
    {
        //if (edit)
        //    Rom[a + (0x4000 * (Rombank - 1))] = v;
        //else
        {
            if (a <= 0x5fff)
                Rambank = v & 3;
        }
    }
}
