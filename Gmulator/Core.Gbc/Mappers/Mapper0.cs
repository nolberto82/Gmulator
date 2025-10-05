namespace Gmulator.Core.Gbc.Mappers;
public class Mapper0 : BaseMapper
{
    public override void Reset() => base.Reset();

    public override void Init(byte[] rom, string filename) => base.Init(rom, filename);

    public override byte ReadRom(int a) => Rom[a % 0x4000];

    public override Span<byte> ReadRomBlock(int a, int s) => new();

    public override void WriteRom0(int a, byte v, bool edit = false)
    {
        if (edit)
            Rom[a] = v;
    }

    public override void WriteRom1(int a, byte v, bool edit = false)
    {
        if (edit)
            Rom[a] = v;
    }
}
