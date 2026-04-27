namespace Gmulator.Core.Nes.Mappers;

internal class Mapper002 : BaseMapper
{
    public Mapper002(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override byte ReadPrg(int addr) => base.ReadPrg(0x4000 * Prg[addr >> 14 & 1] + addr % 0x4000);

    public override byte ReadChr(int addr) => base.ReadChr(0x4000 * Prg[0] + addr % 0x4000);

    public override void WritePrg(int addr, byte value) => base.WritePrg(0x4000 * Prg[addr >> 14 & 1] + addr % 0x4000, value);

    public override void Write(int addr, byte value) => Prg[0] = (byte)(value & 7);

    public override void Reset()
    {
        Prg = [0, 7];
        Chr = [0, 1];
        base.Reset();
    }

    public override void Scanline() => base.Scanline();

    public override void SetLatch(int a, byte v) => base.SetLatch(a, v);
}
