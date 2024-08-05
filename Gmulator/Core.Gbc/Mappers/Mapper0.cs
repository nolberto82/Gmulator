﻿namespace GBoy.Core.Mappers;
public class Mapper0 : BaseMapper
{
    public override void Init(byte[] rom, string filename) => base.Init(rom, filename);

    public override byte ReadRom(int a)
    {
        return Rom[a % 0x4000];
    }

    public override Span<byte> ReadRomBlock(int a, int s)
    {
        return new();
    }

    public override void WriteRom0(int a, byte v, bool edit = false)
    {
        if (edit)
            Rom[a] = v;
    }

    public override void WriteRom1(int a, byte v, bool edit = false)
    {
        if (edit)
            Rom[a] = v;
    }
}