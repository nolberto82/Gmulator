using Gmulator.Interfaces;

namespace Gmulator.Core.Snes;
public class SnesMmu : IMmu, ISaveState
{
    private byte[] Wram;
    public int RamAddr { get; protected set; }
    public RamType RamType { get; private set; }
    public int RamMask { get; private set; }

    private List<MemoryHandler> MemoryHandlers;

    private Snes Snes;

    public SnesMmu(Snes snes)
    {
        MemoryHandlers = snes.MemoryHandlers;
        Snes = snes;
    }

    public void SetSnes(Snes snes)
    {
        Wram = new byte[0x20000];
    }

    public int ReadWram(int a)
    {
        return Wram[a & MemoryHandlers[a >> 12].Mask];
    }

    public void WriteWram(int a, int v) => Wram[a & MemoryHandlers[a >> 12].Mask] = (byte)v;
    public byte[] GetWram() => Wram;


    public void Reset()
    {
        Array.Fill<byte>(Wram, 0);
    }

    public int ReadByte(int a)
    {
        a &= 0xffffff;
        int b = a >> 12;
        RamType = MemoryHandlers[b].Type;
        RamMask = MemoryHandlers[b].Mask;
        return MemoryHandlers[b].Read(a) & 0xff;
    }

    public void WriteByte(int a, int v)
    {
        a &= 0xffffff;
        int b = a >> 12;
        RamType = MemoryHandlers[b].Type;
        RamMask = MemoryHandlers[b].Mask;
        MemoryHandlers[b].Write(a, v);
    }

    public void WriteDma(int v)
    {
        WriteWram(0x7e0000 | RamAddr, v);
        Snes.CheckAccess(RamAddr, v, RamType = RamType.Wram, RamMask, true);
        RamAddr = (RamAddr + 1) & 0xffffff;
    }

    public void WriteRamType(int a, int v, RamType type)
    {
#if DEBUG || RELEASE
        RamType = type;
        RamMask = MemoryHandlers[a >> 12].Mask;
        Snes.CheckAccess(a, v, type, RamMask, true);
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

    public void Load(BinaryReader br)
    {
        Wram = ReadArray<byte>(br, Wram.Length);
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(Wram);
    }

    public void Close()
    {
    }

    public RegisterInfo GetRegisters()
    {
        return new("2181-3", "Wram Address", $"{RamAddr:X6}");
    }
}
