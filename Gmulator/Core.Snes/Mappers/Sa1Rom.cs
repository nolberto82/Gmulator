using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Snes.Mappers;

internal class Sa1Rom(BaseMapper.Header header) : BaseMapper(header)
{
    public override int Read(int a)
    {
        int bank = a >> 16;
        a = (bank & 0x40) != 0 ? (a & 0x1f0000) | a & 0xffff : (a & 0x7f0000) >> 1 | a & 0x7fff;
        return base.Read(a);
    }

    public override void Write(int a, int v)
    {
        //bank &= 0x7f;
        //if (bank >= 0x70 && bank <= 0x7d && a < 0x6000 && Sram.Length > 0)
        //    base.Write(bank, a, v);
    }

    public new byte ReadBwRam(int a)
    {
        return Sram[a % Sram.Length];
    }

    public void WriteBwRam(int a, byte v)
    {
        Sram[a % Sram.Length] = v;
    }

    public override void Init(Header header)
    {
        base.Init(header);
    }
}
