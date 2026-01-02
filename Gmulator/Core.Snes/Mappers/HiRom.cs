
using Gmulator.Shared;
using static Gmulator.Core.Snes.Mappers.BaseMapper;

namespace Gmulator.Core.Snes.Mappers;

internal class HiRom : BaseMapper
{
    public HiRom(Header header) : base(header)
    {

    }

    public override int Read(int a)
    {
        //bank &= 0x7f;
        //if (bank < 0x40 && a >= 0x6000 && a < 0x8000 && Sram.Length > 0)
        //    return Sram[a % Sram.Length];
        //return base.Read(bank, );
        return base.Read((a & 0x7f0000) | a & 0xffff);
    }

    public override void Write(int a, int v)
    {
        // bank &= 0x7f;
        //if (bank < 0x40 && a >= 0x6000 && a < 0x8000 && Sram.Length > 0)
        base.Write(a, v);
    }

    public override void Init(Header header) => base.Init(header);
}
