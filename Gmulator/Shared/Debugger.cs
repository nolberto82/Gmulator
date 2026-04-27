using Gmulator.Interfaces;
using System.Buffers;

namespace Gmulator.Shared;

public class Debugger(IConsole console)
{
    private readonly List<Breakpoint> _breakpoints = console.Breakpoints;

    public bool Execute(int addr)
    {
        var bp = GetBreakpoint(addr);
        if (bp != null)
        {
            if (bp.Enabled)
            {
                if (addr == bp.Addr)
                {
                    if (bp.Type == BpType.CodeExec || bp.Type == BpType.SpcExec || bp.Type == BpType.GsuExec)
                        return true;
                }
            }
        }
        return false;
    }

    public void Watchpoint(int a, int v, MemoryHandler handler, bool write)
    {
        var bp = GetBreakpoint(a, handler);
        var type = handler == null ? RamType.Vram : handler.Type;
        if (bp != null && bp.Enabled && bp.Type > 0)
        {
            if (((bp.Type & Access.Write) == bp.Type) && write ||
                (bp.Type & Access.Read) == bp.Type && !bp.Write && !write)
            {
                if ((bp.Condition == -1) || (bp.Condition == (v & 0xff)))
                    console.EmuState = DebugState.Break;
            }
        }
    }

    public void AccessSpc(int addr, int v, RamType memtype, bool write)
    {
        var bp = GetBreakpoint(addr);
        if (bp != null && bp.Enabled && (bp.Type & (BpType.WramRead | BpType.WramWrite)) > 0)
        {
            if (((bp.Type & Access.Write) == BpType.SpcWrite) && write ||
                (bp.Type & Access.Read) == BpType.SpcRead && !bp.Write && !write)
            {
                if ((addr == bp.Addr && bp.Condition == -1) || (addr == bp.Addr && bp.Condition == v))
                    console.EmuState = DebugState.Break;
            }
        }
    }

    private Breakpoint GetBreakpoint(int addr, MemoryHandler handler)
    {
        for (int i = 0; i < _breakpoints.Count; i++)
        {
            if (_breakpoints[i].Addr == addr || _breakpoints[i].Addr == (handler?.Offset | addr & 0xfff))
                return _breakpoints[i];
        }
        return null;
    }

    private Breakpoint GetBreakpoint(int addr)
    {
        for (int i = 0; i < _breakpoints.Count; i++)
        {
            if (_breakpoints[i].Addr == addr)
                return _breakpoints[i];
        }
        return null;
    }
}
