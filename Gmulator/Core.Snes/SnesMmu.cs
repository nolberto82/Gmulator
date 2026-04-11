using Gmulator.Interfaces;

namespace Gmulator.Core.Snes;

public class SnesMmu : IMmu, ISaveState
{
    public byte[] Wram { get; set; } = new byte[0x20000];
    public int RamAddr { get; protected set; }
    public RamType RamType { get; private set; }
    private MemoryMap CpuMap => Snes.CpuMap;

    private readonly Snes Snes;

    private byte[] _sram;

    public SnesMmu(Snes snes)
    {
        Snes = snes;
    }

    public int GetOffset(int a)
    {
        var handler = CpuMap.Handlers[(a >> 12) & 0xfff];
        return handler.Offset + (a & 0xfff);
    }

    public int ReadWram(int a) => Wram[CpuMap.Handlers[a >> 12].Offset + (a & 0xfff) & 0x1ffff];

    public void WriteWram(int a, int v)
    {
        int mask = Wram.Length - 1;
        Wram[(CpuMap.Handlers[a >> 12].Offset | (a & 0xfff)) & mask] = (byte)v;
    }

    public byte[] GetWram() => Wram;
    public byte[] GetSram() => _sram;

    public void Reset()
    {
        Array.Fill<byte>(Wram, 0);
        _sram = Snes.Mapper.Sram;
    }

    public int ReadByte(int a)
    {
        a &= 0xffffff;
        int b = a >> 12;
        RamType = CpuMap.Handlers[b].Type;
        return CpuMap.Handlers[b].Read(a) & 0xff;
    }

    public void WriteByte(int a, int v)
    {
        a &= 0xffffff;
        int b = a >> 12;
        RamType = CpuMap.Handlers[b].Type;
        CpuMap.Handlers[b].Write(a, v);
    }

    public void WriteDma(int v)
    {
        WriteWram(0x7e0000 | RamAddr, v);
#if DEBUG || RELEASE
        Snes.Debugger.Access(RamAddr, v, CpuMap.Handlers[RamAddr >> 12], true);
#endif
        RamAddr = (RamAddr + 1) & 0xffffff;
    }

    public void WriteRamType(int a, int v, RamType type)
    {
#if DEBUG || RELEASE
        RamType = type;
        Snes.Debugger.Access(a, v, null, true);
#endif
    }

    public void UpdateWramAddress(int a, int v)
    {
        switch (a)
        {
            case 0x2181:
                RamAddr = (RamAddr & 0xffff00) | v;
                break;
            case 0x2182:
                RamAddr = (RamAddr & 0xff00ff) | (v << 8);
                break;
            case 0x2183:
                RamAddr = (RamAddr & 0x00ffff) | (v << 16);
                break;
            default:
                break;
        }
    }

    public void Load(BinaryReader br) => Wram = ReadArray<byte>(br, Wram.Length);

    public void Save(BinaryWriter bw) => bw.Write(Wram);

    public RegisterInfo GetRegisters() => new("2181-3", "Wram Address", $"{RamAddr:X6}");

    public int ReadWord(int a)
    {
        int low = ReadByte(a);
        int high = ReadByte(a + 1);
        return low | high << 8;
    }

    public void WriteWord(int a, int v)
    {
        WriteByte(a, v);
        WriteByte(a + 1, v);
    }

    public int ReadLong(int a)
    {
        int low = ReadByte(a);
        int mid = ReadByte(a + 1);
        int high = ReadByte(a + 2);
        return low | mid << 8 | high << 16;
    }

    public void WriteLong(int a, int v)
    {
        WriteByte(a, v);
        WriteByte(a + 1, v >> 8);
        WriteByte(a + 2, v >> 16);
    }
}
