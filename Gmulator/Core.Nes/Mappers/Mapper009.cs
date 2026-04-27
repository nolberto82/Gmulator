namespace Gmulator.Core.Nes.Mappers;

internal class Mapper009 : BaseMapper
{
    private bool UpdateChr;
    private int Latch1;
    private int Latch2;

    public Mapper009(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override byte ReadPrg(int addr) => base.ReadPrg(0x2000 * Prg[(addr >> 13) % 4] + addr % 0x2000);

    public override byte ReadChr(int addr) => base.ReadChr(0x1000 * Chr[addr >> 12] + addr % 0x1000);

    public override void WritePrg(int addr, byte value) => base.WritePrg(0x2000 * Prg[(addr % 0x8000) >> 13] + addr % 0x2000, value);

    public override void Write(int addr, byte value)
    {
        if (addr >= 0xa000 && addr <= 0xafff)
        {
            Prg[0] = (byte)(value & 0xf);
            Prg[1] = (byte)((Header.PrgBanks * 2) - 3);
            Prg[2] = (byte)((Header.PrgBanks * 2) - 2);
            Prg[3] = (byte)((Header.PrgBanks * 2) - 1);
        }
        else if (addr <= 0xbfff)
            LChr[0] = (byte)(value & 0x1f);
        else if (addr <= 0xcfff)
            LChr[1] = (byte)(value & 0x1f);
        else if (addr <= 0xdfff)
            LChr[2] = (byte)(value & 0x1f);
        else if (addr <= 0xefff)
            LChr[3] = (byte)(value & 0x1f);
        else if (addr <= 0xffff)
            Header.Mirror = (value & 1) + 2;
        base.Write(addr, value);
    }

    public override byte ReadVram(int a) => base.ReadVram(a);

    public override void Reset()
    {
        var bank = Header.PrgBanks * 2;
        Prg = [0, bank - 3, bank - 2, bank - 1];
        Chr = [0, 1];
        LChr = [0, 1, 2, 3];
        base.Reset();
    }

    public override void Scanline() => base.Scanline();

    public override void SetLatch(int a, byte v)
    {
        if (a == 0x0fd8)
        {
            Latch1 = 0xfd;
            UpdateChr = true;
        }
        else if (a == 0x0fe8)
        {
            Latch1 = 0xfe;
            UpdateChr = true;
        }
        else if (a >= 0x1fd8 && a <= 0x1fdf)
        {
            Latch2 = 0xfd;
            UpdateChr = true;
        }
        else if (a >= 0x1fe8 && a <= 0x1fef)
        {
            Latch2 = 0xfe;
            UpdateChr = true;
        }

        if (UpdateChr)
        {
            Chr[0] = Latch1 == 0xfd ? LChr[0] : LChr[1];
            Chr[1] = Latch2 == 0xfd ? LChr[2] : LChr[3];

            UpdateChr = false;
        }
    }
}
