namespace Gmulator.Core.Nes.Mappers;

internal class Mapper004 : BaseMapper
{
    private int BankReg;

    public bool Irq { get; private set; }
    public int Reload { get; private set; }
    public int Rvalue { get; private set; }
    public bool WriteProtect { get; private set; }
    public int ChrReg { get; private set; }

    public Mapper004(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override byte ReadPrg(int addr) => base.ReadPrg(0x2000 * Prg[(addr >> 13) % 4] + addr % 0x2000);

    public override byte ReadChr(int addr) => base.ReadChr(0x0400 * Chr[addr >> 10] + addr % 0x0400);

    public override void WritePrg(int addr, byte value) => base.WritePrg(0x2000 * Prg[(addr % 0x8000) >> 13] + addr % 0x2000, value);

    public override void Write(int addr, byte value)
    {
        if (addr <= 0x9fff)
        {
            if ((addr % 2) == 0)
            {
                ChrMode = (value >> 7) & 1;
                PrgMode = (value >> 6) & 1;
                BankReg = value;
                if (PrgMode == 0)
                {
                }
                else
                {
                    if (Prg[0] != Header.PrgBanks * 2 - 2)
                        (Prg[0], Prg[2]) = ((byte)(Header.PrgBanks * 2 - 2), Prg[0]);
                    Prg[3] = (byte)((Header.PrgBanks * 2) - 1);
                }

                if (ChrMode == 1)
                {
                }
            }
            else
            {
                if (PrgMode == 0)
                {
                    if ((BankReg & 7) == 7)
                        Prg[1] = (byte)(value & (Header.PrgBanks * 2 - 1));
                    else if ((BankReg & 7) == 6)
                        Prg[0] = (byte)(value & (Header.PrgBanks * 2 - 1));
                }
                else
                {
                    if ((BankReg & 7) == 7)
                        Prg[1] = (byte)(value & (Header.PrgBanks * 2 - 1));
                    else if ((BankReg & 7) == 6)
                        Prg[2] = (byte)(value & (Header.PrgBanks * 2 - 1));
                }

                if ((BankReg & 7) < 6)
                {
                    if (ChrMode == 0)
                    {
                        switch (BankReg & 7)
                        {
                            case 0:
                                Chr[0] = (byte)(value & 0xfe);
                                Chr[1] = (byte)(Chr[0] + 1);
                                break;
                            case 1:
                                Chr[2] = (byte)(value & 0xfe);
                                Chr[3] = (byte)(Chr[2] + 1);
                                break;
                            case 2: Chr[4] = value; break;
                            case 3: Chr[5] = value; break;
                            case 4: Chr[6] = value; break;
                            case 5: Chr[7] = value; break;
                        }
                    }
                    else
                    {
                        switch (BankReg & 7)
                        {
                            case 2: Chr[0] = value; break;
                            case 3: Chr[1] = value; break;
                            case 4: Chr[2] = value; break;
                            case 5: Chr[3] = value; break;
                            case 0:
                                Chr[4] = (byte)(value & 0xfe);
                                Chr[5] = (byte)(Chr[4] + 1);
                                break;
                            case 1:
                                Chr[6] = (byte)(value & 0xfe);
                                Chr[7] = (byte)(Chr[6] + 1);
                                break;
                        }
                    }
                }
            }
        }
        else if (addr <= 0xbfff)
        {
            if ((addr % 2) == 0)
                Header.Mirror = (value & 1) + 2;
            else
            {
                WriteProtect = (value & 0x40) != 0;
                SramEnabled = (value & 0x80) != 0;
            }
        }
        else if (addr <= 0xdfff)
        {
            if ((addr % 2) == 0)
                Rvalue = value;
            else
            {
                Counter = 0;
                Reload = 1;
            }
        }
        else if (addr <= 0xffff)
        {
            if ((addr % 2) == 0)
                Irq = false;
            else
                Irq = true;
        }
        base.Write(addr, value);
    }

    public override void Reset()
    {
        int prgsize = Header.PrgBanks * 0x4000;
        Prg = [0, 1, (byte)(prgsize / 0x2000 - 2), (byte)(prgsize / 0x2000 - 1)];
        Chr = [0, 1, 2, 3, 4, 5, 6, 7];
        Fire = false;
        Reload = 0;
        Rvalue = 0;
        base.Reset();
    }

    public override void Scanline()
    {
        if (Counter == 0)
        {
            Counter = Rvalue;
            Reload = 0;
        }
        else
        {
            Counter--;
            if (Counter == 0 && Irq)
                Fire = true;
        }
    }

    public override void SetLatch(int a, byte v) => base.SetLatch(a, v);
}
