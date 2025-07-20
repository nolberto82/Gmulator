using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Gmulator.Core.Nes.Mappers;

internal class Mapper009 : BaseMapper
{
    private bool UpdateChr;
    private int Latch1;
    private int Latch2;

    public Mapper009(Header cart) : base(cart)
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

    public override byte ReadChr(int a) => base.ReadChr(0x1000 * Chr[a >> 12] + a % 0x1000);

    public override void WritePrg(int a, byte v) => base.WritePrg(0x2000 * Prg[(a % 0x8000) >> 13] + a % 0x2000, v);

    public override void Write(int a, byte v)
    {
        if (a >= 0xa000 && a <= 0xafff)
        {
            Prg[0] = (byte)(v & 0xf);
            Prg[1] = (byte)((Header.PrgBanks * 2) - 3);
            Prg[2] = (byte)((Header.PrgBanks * 2) - 2);
            Prg[3] = (byte)((Header.PrgBanks * 2) - 1);
        }
        else if (a <= 0xbfff)
                LChr[0] = (byte)(v & 0x1f);
        else if (a <= 0xcfff)
                LChr[1] = (byte)(v & 0x1f);
        else if (a <= 0xdfff)
                LChr[2] = (byte)(v & 0x1f);
        else if (a <= 0xefff)
                LChr[3] = (byte)(v & 0x1f);
        else if (a <= 0xffff)
            Header.Mirror = (v & 1) + 2;
        base.Write(a, v);
    }

    public override byte ReadVram(int a) => base.ReadVram(a);

    public override void Reset()
    {
        var bank = (Header.PrgBanks * 2);
        Prg = [0, (byte)(bank - 3), (byte)(bank - 2), (byte)(bank - 1)];
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
            Chr[0] = (byte)(Latch1 == 0xfd ? LChr[0] : LChr[1]);
            Chr[1] = (byte)(Latch2 == 0xfd ? LChr[2] : LChr[3]);

            UpdateChr = false;
        }
    }
}
