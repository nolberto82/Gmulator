using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GNes.Core.Mappers;

internal class Mapper007 : BaseMapper
{
    public Mapper007(Header cart) : base(cart)
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

    public override byte ReadPrg(int a)
    {
        return base.ReadPrg(0x8000 * Prg[0] + a % 0x8000);
    }

    public override byte ReadChr(int a)
    {
        return base.ReadChr(0x2000 * Chr[0] + a % 0x2000);
    }

    public override void WritePrg(int a, byte v)
    {
        base.WritePrg(0x8000 * Prg[0] + a % 0x8000, v);
    }

    public override void Write(int a, byte v)
    {
        Prg[0] = (byte)(v & 7);
        Chr[0] = (byte)(v >> 4);
        Header.Mirror = ((v >> 4) & 1);
        base.Write(a, v);
    }

    public override byte ReadVram(int a)
    {
        return base.ReadVram(a);
    }

    public override void Reset()
    {
        Prg = [0];
        Chr = [0];
        base.Reset();
    }

    public override void Scanline()
    {
        base.Scanline();
    }

    public override void SetLatch(int a, byte v)
    {
        base.SetLatch(a, v);
    }
}
