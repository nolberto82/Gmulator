namespace Gmulator.Core.Nes.Mappers;

internal class Mapper002 : BaseMapper
{
    public Mapper002(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override int ReadPrg(int addr) => base.ReadPrg(0x4000 * Prg[addr >> 14 & 1] + addr % 0x4000);

    public override int ReadChr(int addr) => base.ReadChr(0x4000 * Prg[0] + addr % 0x4000);

    public override void WritePrg(int addr, int value) => base.WritePrg(0x4000 * Prg[addr >> 14 & 1] + addr % 0x4000, value);

    public override void Write(int addr, int value) => Prg[0] = (byte)(value & 7);

    public override void Reset()
    {
        Prg = [0, 7];
        Chr = [0, 1];
        base.Reset();
    }

    public override void Scanline() => base.Scanline();

    public override void SetLatch(int addr, int value) => base.SetLatch(addr, value);
}
