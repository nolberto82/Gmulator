using System.Runtime.InteropServices.Marshalling;

namespace Gmulator.Shared;

public class Breakpoint
{
    public int Addr { get; set; }
    public int Condition { get; set; }
    public BpType Type { get; set; }
    public bool Write { get; set; }
    public bool Enabled { get; set; }

    public Breakpoint() { }
    public Breakpoint(int addr, int condition, BpType type, bool write, bool enabled)
    {
        Addr = addr;
        Condition = condition;
        Type = type;
        Write = write;
        Enabled = enabled;
    }
}

public static class Access
{
    public const BpType Write = BpType.WramWrite | BpType.VramWrite | BpType.RegWrite |
        BpType.SpcWrite | BpType.Sa1Write | BpType.SramWrite | BpType.CramWrite | BpType.OramWrite;
    public const BpType Read = BpType.WramRead | BpType.VramRead | BpType.RegRead |
        BpType.SpcRead | BpType.Sa1Read | BpType.SramRead | BpType.CramRead | BpType.OramRead;
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
    GsuExec = 1 << 12,
    SramWrite = 1 << 13,
    SramRead = 1 << 14,
    CramWrite = 1 << 15,
    CramRead = 1 << 16,
    OramWrite = 1 << 15,
    OramRead = 1 << 16,
};

public enum RamType : int
{
    Wram, Sram, Vram, Oram, Cram, SpcRam, Iram, Rom,
    GsuRom, Register, None
}