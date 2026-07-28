using System.Runtime.InteropServices.Marshalling;

namespace Gmulator.Shared;

public class Breakpoint
{
    public int Addr { get; set; }
    public int Condition { get; set; }
    public BpType Type { get; set; }
    public RamType RamType { get; set; }
    public CpuType CpuType { get; set; }
    public int Index { get; set; }
    public string Access { get; set; }
    public bool Write { get; set; }
    public bool Enabled { get; set; }

    public Breakpoint() { }
    public Breakpoint(int addr, int condition, BpType type, RamType ramType, CpuType cpuType, int index, string access, bool write, bool enabled)
    {
        Addr = addr;
        Condition = condition;
        Type = type;
        RamType = ramType;
        CpuType = cpuType;
        Index = index;
        Access = access;
        Write = write;
        Enabled = enabled;
    }
}

public static class Access
{
    public const BpType Write = BpType.WramWrite | BpType.VramWrite | BpType.RegWrite |
        BpType.SpcWrite | BpType.Sa1Write | BpType.SramWrite | BpType.CramWrite | BpType.OramWrite |
        BpType.GsuWrite;

    public const BpType Read = BpType.WramRead | BpType.VramRead | BpType.RegRead |
        BpType.SpcRead | BpType.Sa1Read | BpType.SramRead | BpType.CramRead | BpType.OramRead |
        BpType.GsuRead;

    public const BpType Exec = BpType.CodeExec | BpType.SpcExec | BpType.GsuExec;
}

public enum BpType : int
{
    WramWrite = 1,
    WramRead = 1 << 1,
    CodeExec = 1 << 2,
    VramWrite = 1 << 3,
    VramRead = 1 << 4,
    RegWrite = 1 << 5,
    RegRead = 1 << 6,
    SpcWrite = 1 << 7,
    SpcRead = 1 << 8,
    SpcExec = 1 << 9,
    Sa1Write = 1 << 10,
    Sa1Read = 1 << 11,
    GsuWrite = 1 << 12,
    GsuRead = 1 << 13,
    GsuExec = 1 << 14,
    SramWrite = 1 << 15,
    SramRead = 1 << 16,
    CramWrite = 1 << 17,
    CramRead = 1 << 18,
    OramWrite = 1 << 19,
    OramRead = 1 << 20,
};

public enum RamType : int
{
    Wram, Sram, Vram, Oram, Cram, SpcRam, Iram, GsuRam, Rom,
    GsuRom, Register, None
}

public enum CpuType
{
    Gbc, Nes, Snes, Spc, Sa1, Gsu
}