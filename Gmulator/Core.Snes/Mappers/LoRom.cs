
namespace Gmulator.Core.Snes.Mappers;

public class LoRom(BaseMapper.Header header, MemoryMap Map) : BaseMapper(header, Map)
{
    public override int Read(int a)
    {
        var addr = MemoryHandler[a >> 12].Offset + (a & 0xfff);
        return base.Read(addr);
    }

    public override int ReadSram(int a) => base.ReadSram(a);

    public override void WriteSram(int a, int v) => base.WriteSram(a, v);

    public override void Reset(Snes snes)
    {
        var CpuMap = snes.CpuMap;
        CpuMap.Rom(0x00, 0x7d, 0x8000, 0xffff, Read, Write);
        CpuMap.Rom(0x80, 0xff, 0x8000, 0xffff, Read, Write);
        CpuMap.Sram(0x70, 0x7d, 0x0000, 0xffff, ReadSram, WriteSram);
        CpuMap.Sram(0xf0, 0xff, 0x0000, 0xffff, ReadSram, WriteSram);
    }

    public override void Init(Header header, List<MemoryHandler> mh) => base.Init(header, mh);
}
