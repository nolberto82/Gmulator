namespace Gmulator.Core.Nes.Mappers;

public class Mapper030 : BaseMapper
{
    public Mapper030(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override int ReadChr(int addr) => base.ReadChr(addr);

    public override int ReadPrg(int addr) => base.ReadPrg(0x4000 * Prg[addr >> 14 & 1] + addr % 0x4000);

    public override int ReadVram(int addr) => base.ReadVram(addr);

    public override void Reset()
    {
        Prg = [0, Header.PrgBanks - 1];
        Chr = [0];
    }

    public override void Write(int addr, int value)
    {
        if (addr >= 0xc000)
        {
            Prg[0] = value & 0x1f;
            Chr[0] = value & 0x5f;
        }
    }

    public override void WritePrg(int addr, int value) => base.WritePrg(addr, value);
}
