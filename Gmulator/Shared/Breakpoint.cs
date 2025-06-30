
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gmulator;
public class Breakpoint
{
    public int Addr { get; set; }
    public int Condition { get; set; }
    public int Type { get; set; }
    public bool IsDebug { get; set; }
    public int CpuType { get; set; }
    public int RamType { get; set; }
    public bool Write { get; set; }
    public bool Enabled { get; set; }

    public Breakpoint() { }
    public Breakpoint(int addr, int condition, int type, bool write, bool enabled, int index = 0)
    {
        Addr = addr;
        Condition = condition;
        Type = type;
        RamType = index;
        Write = write;
        Enabled = enabled;
    }
}

public static class AccessTypes
{
    public const int Reads = BPType.Read | BPType.VideoRead | BPType.RegRead | BPType.SpcRead;
    public const int Writes = BPType.Write | BPType.VideoWrite | BPType.RegWrite | BPType.SpcWrite;
    public const int RamReads = 0xff;
    public const int RamWrites = RamReads & ~RamType.Rom;
}

public static class CpuType
{
    public const int Main = 1;
    public const int Spc = 2;
};

public static class BPType
{
    public const int Read = 1;
    public const int Write = 2;
    public const int Exec = 4;
    public const int VideoWrite = 8;
    public const int VideoRead = 16;
    public const int RegWrite = 32;
    public const int RegRead = 64;
    public const int SpcWrite = 128;
    public const int SpcRead = 256;
    public const int SpcExec = 512;
};

public static class RamType
{
    public const int Wram = 0;
    public const int Sram = 1;
    public const int Vram = 2;
    public const int Oram = 3;
    public const int Rom = 4;
    public const int Cram = 5;
    public const int SpcRam = 6;
    public const int SpcRom = 7;
    public const int Register = 8;
};
