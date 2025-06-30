
using Gmulator.Shared;
using static Gmulator.Core.Snes.Mappers.BaseMapper;

namespace Gmulator.Core.Snes.Mappers;
internal class HiRom(Header header) : BaseMapper(header)
{
    public override byte Read(int bank, int a)
    {
        bank &= 0x7f;
        if (bank < 0x40 && a >= 0x6000 && a < 0x8000 && Sram.Length > 0)
            return Sram[a % Sram.Length];
        return base.Read(bank, (bank << 16 | a));
    }

    public override void Write(int bank, int a, int v)
    {
        bank &= 0x7f;
        if (bank < 0x40 && a >= 0x6000 && a < 0x8000 && Sram.Length > 0)
            base.Write(bank, a, v);
    }

    public override void Init(Header header) => base.Init(header);
}
