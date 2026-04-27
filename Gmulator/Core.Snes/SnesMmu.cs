using Gmulator.Interfaces;

namespace Gmulator.Core.Snes;

public sealed class SnesMmu(Snes snes) : IMmu, ISaveState
{
    private byte[] _wram { get; set; } = new byte[0x20000];
    public int RamAddr { get; private set; }
    public RamType RamType { get; private set; }
    private MemoryMap CpuMap => Snes.CpuMap;

    private readonly Snes Snes = snes;

    private byte[] _sram;

    public int GetOffset(int a)
    {
        var handler = CpuMap.Handlers[(a >> 12) & 0xfff];
        return handler.Offset + (a & 0xfff);
    }

    public byte ReadWram(int addr) => _wram[CpuMap.Handlers[addr>>12].Offset + (addr & 0xfff) & 0x1ffff];

    public void WriteWram(int addr, byte value) => _wram[CpuMap.Handlers[addr >> 12].Offset + (addr & 0xfff) & 0x1ffff] = value;

    public byte[] GetWram() => _wram;
    public byte[] GetSram() => _sram;
    public byte ReadVram(int addr) => Snes.Ppu.ReadVram(addr);

    public void Reset()
    {
        _sram = Snes.Mapper.Sram;
    }

    public byte ReadByte(int a)
    {
        a &= 0xffffff;
        int b = a >> 12;
        RamType = CpuMap.Handlers[b].Type;
        return CpuMap.Handlers[b].Read(a);
    }

    public void WriteByte(int a, byte v)
    {
        a &= 0xffffff;
        int b = a >> 12;
        RamType = CpuMap.Handlers[b].Type;
        CpuMap.Handlers[b].Write(a, v);
    }

    public void WriteDma(int v)
    {
        WriteWram(0x7e0000 | RamAddr, (byte)v);
#if DEBUG || RELEASE
        Snes.Debugger.Watchpoint(RamAddr, v, CpuMap.Handlers[RamAddr >> 12], true);
#endif
        RamAddr = (RamAddr + 1) & 0xffffff;
    }

    public void WriteRamType(int a, int v, RamType type)
    {
        RamType = type;
        Snes.Debugger.Watchpoint(a, v, null, true);
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

    public void Load(BinaryReader br) => _wram = ReadArray<byte>(br, _wram.Length);

    public void Save(BinaryWriter bw) => bw.Write(_wram);

    public RegisterInfo GetRegisters() => new("2181-3", "Wram Address", $"{RamAddr:X6}");

    public int ReadWord(int a)
    {
        int low = ReadByte(a);
        int high = ReadByte(a + 1);
        return low | high << 8;
    }

    public void WriteWord(int a, int v)
    {
        WriteByte(a, (byte)v);
        WriteByte(a + 1, (byte)(v >> 8));
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
        WriteByte(a, (byte)v);
        WriteByte(a + 1, (byte)(v >> 8));
        WriteByte(a + 2, (byte)(v >> 16));
    }
}
