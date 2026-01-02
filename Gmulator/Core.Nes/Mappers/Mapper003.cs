using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Nes.Mappers;

internal class Mapper003 : BaseMapper
{
    public Mapper003(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override int ReadPrg(int a) => base.ReadPrg(0x4000 * ((a & 0x4000) >> 14) + a % 0x4000);

    public override int ReadChr(int a) => base.ReadChr(0x2000 * Chr[0] + a % 0x2000);

    public override void WritePrg(int a, int v) => base.WritePrg(0x4000 * ((a & 0x4000) >> 14) + a % 0x4000, v);

    public override void Write(int a, int v)
    {
        Chr[0] = (byte)(v & 3);
        base.Write(a, v);
    }

    public override void Reset()
    {
        Prg = [0];
        Chr = [0];
        base.Reset();
    }

    public override void Scanline() => base.Scanline();

    public override void SetLatch(int a, byte v) => base.SetLatch(a, v);
}
