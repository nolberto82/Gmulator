namespace Gmulator.Core.Nes.Mappers;
public class Mapper030 : BaseMapper
{
    public Mapper030(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override int ReadChr(int a)
    {
        return base.ReadChr(a);
    }

    public override int ReadPrg(int a)
    {
        return base.ReadPrg(0x4000 * Prg[a >> 14 & 1] + a % 0x4000);
    }

    public override byte ReadVram(int a)
    {
        return base.ReadVram(a);
    }

    public override void Reset()
    {
        Prg = [0, Header.PrgBanks - 1];
        Chr = [0];
    }

    public override void Write(int a, int v)
    {
        if (a >= 0xc000)
        {
            Prg[0] = v & 0x1f;
            Chr[0] = v & 0x5f;
        }
    }

    public override void WritePrg(int a, int v)
    {
        base.WritePrg(a, v);
    }
}
