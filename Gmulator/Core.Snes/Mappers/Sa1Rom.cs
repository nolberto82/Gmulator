using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Snes.Mappers;

internal class Sa1Rom(BaseMapper.Header header, MemoryMap Map) : BaseMapper(header, Map)
{
    public override int Read(int a)
    {
        //int bank = a >> 16;
        //int addr = (bank & 0x40) != 0 ? (a & 0x1f0000) | a & 0xffff : (a & 0xff0000) >> (bank < 0x80 ? 1 : 2) | a & 0x7fff;
        int addr = MemoryHandler[a >> 12].Offset + (a & 0xfff);
        return base.Read(addr);
    }

    public override int ReadSram(int a)
    {
        int addr = MemoryHandler[a >> 12].Offset + (a & 0xfff);
        return base.ReadSram(addr);
    }

    public override void WriteSram(int a, int v)
    {
        int addr = MemoryHandler[a >> 12].Offset + (a & 0xfff);
        base.WriteSram(addr, v);
    }

    public override int ReadSa1(int a)
    {
        int addr = (a & 0x7f0000) >> 1 | a & 0x7fff;
        return base.Read(addr);
    }

    public override void Reset(Snes snes)
    {
        var Sa1Map = snes.Sa1.Sa1Map;
        var CpuMap = snes.CpuMap;
        Sa1Map.Rom(0x00, 0x7d, 0x8000, 0xffff, Read, Write);
        Sa1Map.Rom(0x80, 0xff, 0x8000, 0xffff, Read, Write);

        CpuMap.Rom(0x00, 0x3f, 0x8000, 0xffff, Read, Write);
        CpuMap.Rom(0x80, 0xbf, 0x8000, 0xffff, Read, Write);
        //CpuMap.Sram(0x40, 0x4f, 0x0000, 0xffff, ReadSram, WriteSram);
    }

    public override void Init(Header header, List<MemoryHandler> mh) => base.Init(header, mh);
}
