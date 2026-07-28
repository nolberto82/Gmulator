namespace Gmulator.Core.Nes.Mappers;

internal class Mapper003 : BaseMapper
{
    public Mapper003(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override int ReadPrg(int addr) => base.ReadPrg(0x4000 * ((addr & 0x4000) >> 14) + addr % 0x4000);

    public override int ReadChr(int addr) => base.ReadChr(0x2000 * Chr[0] + addr % 0x2000);

    public override void WritePrg(int addr, int value) => base.WritePrg(0x4000 * ((addr & 0x4000) >> 14) + addr % 0x4000, value);

    public override void Write(int addr, int value)
    {
        Chr[0] = (byte)(value & 3);
        base.Write(addr, value);
    }

    public override void Reset()
    {
        Prg = [0];
        Chr = [0];
        base.Reset();
    }

    public override void Scanline() => base.Scanline();

    public override void SetLatch(int addr, int value) => base.SetLatch(addr, value);
}
