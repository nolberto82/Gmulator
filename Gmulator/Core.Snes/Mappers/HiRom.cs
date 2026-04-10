using static Gmulator.Core.Snes.Mappers.BaseMapper;

namespace Gmulator.Core.Snes.Mappers;

internal class HiRom(Header header, MemoryMap Map) : BaseMapper(header, Map)
{
    public override int Read(int a) =>
        //bank &= 0x7f;
        //if (bank < 0x40 && a >= 0x6000 && a < 0x8000 && Sram.Length > 0)
        //    return Sram[a % Sram.Length];
        //return base.Read(bank, );
        base.Read((a & 0x7f0000) | a & 0xffff);

    public override void WriteSram(int a, int v) =>
        // bank &= 0x7f;
        //if (bank < 0x40 && a >= 0x6000 && a < 0x8000 && Sram.Length > 0)
        base.WriteSram(a, v);

    public override void Reset(Snes snes)
    {
        var CpuMap = snes.CpuMap;
        CpuMap.Rom(0x00, 0x3f, 0x8000, 0xffff, Read, Write);
        CpuMap.Rom(0x80, 0xbf, 0x8000, 0xffff, Read, Write);
        CpuMap.Rom(0x40, 0x7d, 0x0000, 0xffff, Read, Write);
        CpuMap.Sram(0x20, 0x3f, 0x6000, 0x7fff, ReadSram, WriteSram);
        CpuMap.Sram(0xa0, 0xbf, 0x6000, 0x7fff, ReadSram, WriteSram);
    }

    public override void Init(Header header, List<MemoryHandler> mh) => base.Init(header, mh);
}
