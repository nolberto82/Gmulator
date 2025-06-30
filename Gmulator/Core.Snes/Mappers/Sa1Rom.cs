using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Snes.Mappers;

internal class Sa1Rom(BaseMapper.Header header) : BaseMapper(header)
{
    public override byte Read(int bank, int a)
    {
        if (bank < 0xc0)
        {
            bank &= 0x7f;
            if (bank >= 0x70 && bank <= 0x7d && a < 0x6000 && Sram.Length > 0)
                return Sram[a % Sram.Length];
            return base.Read(bank, ((bank << 16 | a) & 0x7f0000) >> 1 | a & 0x7fff);
        }
        else
            return base.Read(bank, ((bank << 16 | a) & 0x1f0000) | a);
    }

    public override void Write(int bank, int a, int v)
    {
        bank &= 0x7f;
        if (bank >= 0x70 && bank <= 0x7d && a < 0x6000 && Sram.Length > 0)
            base.Write(bank, a, v);
    }

    public new byte ReadBwRam(int a)
    {
        return Sram[a % Sram.Length];
    }

    public new void WriteBwRam(int a, byte v)
    {
        Sram[a % Sram.Length] = (byte)v;
    }

    public override void Init(Header header)
    {
        base.Init(header);
    }
}
