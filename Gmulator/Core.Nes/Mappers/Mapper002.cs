using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Nes.Mappers;

internal class Mapper002 : BaseMapper
{
    public Mapper002(Header header, NesMmu mmu) : base(header, mmu)
    {
        Reset();
    }

    public override int ReadPrg(int a)
    {
        return base.ReadPrg(0x4000 * Prg[a >> 14 & 1] + a % 0x4000);
    }

    public override int ReadChr(int a) => base.ReadChr(0x4000 * Prg[0] + a % 0x4000);

    public override void WritePrg(int a, int v)
    {
        base.WritePrg(0x4000 * Prg[a >> 14 & 1] + a % 0x4000, v);
    }

    public override void Write(int a, int v)
    {
        Prg[0] = (byte)(v & 7);
    }

    public override void Reset()
    {
        Prg = [0, 7];
        Chr = [0, 1];
        base.Reset();
    }

    public override void Scanline() => base.Scanline();

    public override void SetLatch(int a, byte v) => base.SetLatch(a, v);
}
