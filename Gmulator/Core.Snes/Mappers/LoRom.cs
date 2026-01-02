
namespace Gmulator.Core.Snes.Mappers;

public class LoRom : BaseMapper
{
    public LoRom(Header header) : base(header)
    {

    }

    public override int Read(int a)
    {
        return base.Read((a & 0x7f0000) >> 1 | a & 0x7fff);
    }

    public override int ReadSram(int a)
    {
        return base.ReadSram(a);
    }

    public override void Write(int a, int v)
    {
        base.Write(a, v);
    }

    public override void Init(Header header) => base.Init(header);
}
