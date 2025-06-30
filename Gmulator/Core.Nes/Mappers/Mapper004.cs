using System.Net.Sockets;

namespace GNes.Core.Mappers;
internal class Mapper004 : BaseMapper
{
    private int BankReg;

    public bool Irq { get; private set; }
    public int Reload { get; private set; }
    public int Rvalue { get; private set; }
    public int WriteProtect { get; private set; }
    public int ChrReg { get; private set; }

    public Mapper004(Header cart) : base(cart)
    {
        Header = cart;
        var Mmu = Header.Mmu;
        int prgsize = Header.PrgBanks * 0x4000;
        int chrsize = Header.ChrBanks * 0x2000;

        Prom = Header.Prom;
        Vrom = cart.Vrom;

        Buffer.BlockCopy(Prom, 0, Mmu.Ram, 0x8000, prgsize / Header.PrgBanks);
        Buffer.BlockCopy(Prom, prgsize - prgsize / Header.PrgBanks, Mmu.Ram, 0xc000, prgsize / Header.PrgBanks);

        if (Header.ChrBanks > 0)
            Buffer.BlockCopy(Vrom, 0, Mmu.Vram, 0x0000, chrsize / Header.ChrBanks);

        Reset();
    }

    public override byte ReadPrg(int a) => base.ReadPrg(0x2000 * Prg[(a % 0x8000) >> 13] + a % 0x2000);

    public override byte ReadChr(int a) => base.ReadChr(0x0400 * Chr[a >> 10] + a % 0x0400);

    public override void WritePrg(int a, byte v) => base.WritePrg(0x2000 * Prg[(a % 0x8000) >> 13] + a % 0x2000, v);

    public override void Write(int a, byte v)
    {
        if (a <= 0x9fff)
        {
            if ((a % 2) == 0)
            {
                ChrMode = (v >> 7) & 1;
                PrgMode = (v >> 6) & 1;
                BankReg = v;
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
                        Prg[1] = (byte)(v & (Header.PrgBanks * 2 - 1));
                    else if ((BankReg & 7) == 6)
                        Prg[0] = (byte)(v & (Header.PrgBanks * 2 - 1));
                }
                else
                {
                    if ((BankReg & 7) == 7)
                        Prg[1] = (byte)(v & (Header.PrgBanks * 2 - 1));
                    else if ((BankReg & 7) == 6)
                        Prg[2] = (byte)(v & (Header.PrgBanks * 2 - 1));
                }

                if ((BankReg & 7) < 6)
                {
                    if (ChrMode == 0)
                    {
                        switch (BankReg & 7)
                        {
                            case 0:
                                Chr[0] = (byte)(v & 0xfe);
                                Chr[1] = (byte)(Chr[0] + 1);
                                break;
                            case 1:
                                Chr[2] = (byte)(v & 0xfe);
                                Chr[3] = (byte)(Chr[2] + 1);
                                break;
                            case 2: Chr[4] = (byte)v; break;
                            case 3: Chr[5] = (byte)v; break;
                            case 4: Chr[6] = (byte)v; break;
                            case 5: Chr[7] = (byte)v; break;
                        }
                    }
                    else
                    {
                        switch (BankReg & 7)
                        {
                            case 2: Chr[0] = (byte)v; break;
                            case 3: Chr[1] = (byte)v; break;
                            case 4: Chr[2] = (byte)v; break;
                            case 5: Chr[3] = (byte)v; break;
                            case 0:
                                Chr[4] = (byte)(v & 0xfe);
                                Chr[5] = (byte)(Chr[4] + 1);
                                break;
                            case 1:
                                Chr[6] = (byte)(v & 0xfe);
                                Chr[7] = (byte)(Chr[6] + 1);
                                break;
                        }
                    }
                }
            }
        }
        else if (a <= 0xbfff)
        {
            if ((a % 2) == 0)
                Header.Mirror = (v & 1) + 2;
            else
            {
                WriteProtect = (v >> 6) & 1;
                Sram = v.GetBit(7);
            }
        }
        else if (a <= 0xdfff)
        {
            if ((a % 2) == 0)
                Rvalue = v;
            else
            {
                Counter = 0;
                Reload = 1;
            }
        }
        else if (a <= 0xffff)
        {
            if ((a % 2) == 0)
                Irq = false;
            else
                Irq = true;
        }
        base.Write(a, v);
    }

    public override void Reset()
    {
        int prgsize = Header.PrgBanks * 0x4000;
        int chrsize = Header.ChrBanks * 0x2000;
        Prg = [0, 1, (byte)(prgsize / 0x2000 - 2), (byte)(prgsize / 0x2000 - 1)];
        Chr = [0, 1, 2, 3, 4, 5, 6, 7];
        base.Reset();
    }

    public override void Scanline()
    {
        if (Counter == 0 || Reload == 1)
        {
            Counter = Rvalue;
            Reload = 0;
        }
        else
            Counter--;

        if (Counter == 0 && Irq)
            Fire = true;
    }

    public override void SetLatch(int a, byte v) => base.SetLatch(a, v);
}
