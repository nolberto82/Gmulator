
namespace Gmulator.Core.Snes.Mappers;
public class LoRom(BaseMapper.Header header) : BaseMapper(header)
{
    public override byte Read(int bank, int a)
    {
        bank &= 0x7f;
        if (bank >= 0x70 && bank <= 0x7d && a < 0x6000 && Sram.Length > 0)
            return Sram[a % Sram.Length];
        return base.Read(bank, ((bank << 16 | a) & 0x7f0000) >> 1 | a & 0x7fff);
    }

    public override void Write(int bank, int a, int v)
    {
        bank &= 0x7f;
        if (bank >= 0x70 && bank <= 0x7d && a < 0x6000 && Sram.Length > 0)
            base.Write(bank, a, v);
    }

    public override void Init(Header header) => base.Init(header);
}
