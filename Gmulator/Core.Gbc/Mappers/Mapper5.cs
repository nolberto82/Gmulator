namespace Gmulator.Core.Gbc.Mappers;
public class Mapper5 : BaseMapper
{
    public override void Init(byte[] rom, string filename) => base.Init(rom, filename);

    public override byte ReadRom(int a)
    {
        if (Rombank > 1 && a >= 0x4000)
            return base.ReadRom(a % 0x4000 + (0x4000 * Rombank));
        else
            return base.ReadRom(a);
    }

    public override Span<byte> ReadRomBlock(int a, int size)
    {
        if (a <= 0x3fff)
            return new(Rom, a, size);
        else
            return new(Rom, a % 0x4000 + (0x4000 * Rombank), size);
    }

    public override void WriteRom0(int a, byte v, bool edit = false)
    {
        if (edit)
            Rom[a] = v;
        else
        {
            if (a <= 0x1fff)
                CartRamOn = v == 0x0a;
            else if (a <= 0x3fff)
                Rombank = v & 0xff;
        }
    }

    public override void WriteRom1(int a, byte v, bool edit = false)
    {
        if (edit)
            Rom[a % 0x4000 + (0x4000 * Rombank)] = v;
        else
        {
            if (a <= 0x3fff)
                Rombank |= (v << 9) & 0x100;
            else if (a <= 0x5fff)
                Rambank = v & 3;

        }
    }
}
