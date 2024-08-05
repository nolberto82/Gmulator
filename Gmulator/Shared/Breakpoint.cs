
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
    public bool Enabled { get; set; }

    public Breakpoint() { }
    public Breakpoint(int addr, int condition, int type, bool enabled)
    {
        Addr = (ushort)addr;
        Condition = condition;
        Type = type;
        Enabled = enabled;
    }
}

public static class BPType
{
    public const int Read = 1;
    public const int Write = 2;
    public const int Exec = 4;
};
