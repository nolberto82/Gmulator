namespace Gmulator.Core.Nes.Mappers;

internal class Mapper007 : BaseMapper
{
    public Mapper007(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override byte ReadPrg(int addr) => base.ReadPrg(0x8000 * Prg[0] + addr % 0x8000);

    public override byte ReadChr(int addr) => base.ReadChr(0x2000 * Chr[0] + addr % 0x2000);

    public override void WritePrg(int addr, byte value) => base.WritePrg(0x8000 * Prg[0] + addr % 0x8000, value);

    public override void Write(int addr, byte value)
    {
        Prg[0] = (byte)(value & 7);
        Chr[0] = (byte)(value >> 4);
        Header.Mirror = (value >> 4) & 1;
        base.Write(addr, value);
    }

    public override byte ReadVram(int addr) => base.ReadVram(addr);

    public override void Reset()
    {
        Prg = [0];
        Chr = [0];
        base.Reset();
    }

    public override void Scanline() => base.Scanline();

    public override void SetLatch(int a, byte v) => base.SetLatch(a, v);
}
