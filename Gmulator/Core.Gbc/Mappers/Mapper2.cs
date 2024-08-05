namespace GBoy.Core.Mappers;
public class Mapper2 : BaseMapper
{
    public override void Init(byte[] rom, string filename) => base.Init(rom, filename);

    public override byte ReadRom(int a)
    {
        var addr = a + (0x4000 * (Rombank - 1));
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

    public override void WriteRom0(int a, byte v, bool edit = false)
    {
        if (edit)
            Rom[a] = v;
    }

    public override void WriteRom1(int a, byte v, bool edit = false)
    {
        if (edit)
            Rom[a + (0x4000 * (Rombank - 1))] = v;
        else
        {
            if (a >= 0x2000)
                Rombank = v & 0x1f;
        }
    }
}
