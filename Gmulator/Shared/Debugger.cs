using Gmulator.Interfaces;
using System.Buffers;

namespace Gmulator.Shared;

public class Debugger(IConsole console)
{
    private readonly List<Breakpoint> _breakpoints = console.Breakpoints;

    public bool Execute(int addr, CpuType cpuType)
    {
        var bp = GetBreakpoint(addr, cpuType);
        if (bp != null)
        {
            if (bp.Enabled)
            {
                if (bp.Type == BpType.CodeExec || bp.Type == BpType.SpcExec || bp.Type == BpType.GsuExec)
                    return true;
            }
        }
        return false;
    }

    public void Watchpoint(int addr, int value, CpuType cpuType, bool write)
    {
        var bp = GetBreakpoint(addr, cpuType);
        if (bp != null && bp.Enabled && bp.Type != BpType.CodeExec)
        {
            if (((bp.Type & Access.Write) != 0) && write ||
                (bp.Type & Access.Read) != 0 && !bp.Write && !write)
            {
                if ((bp.Condition == -1) || (bp.Condition == (value & 0xff)))
                    console.DbgState = DebugState.Break;
            }
        }
    }

    private Breakpoint GetBreakpoint(int addr, CpuType cpuType)
    {
        for (int i = 0; i < _breakpoints.Count; i++)
        {
            if (cpuType == _breakpoints[i].CpuType)
            {
                if (_breakpoints[i].Enabled && (_breakpoints[i].Addr == addr || _breakpoints[i].Addr == (addr & 0xffff)))
                    return _breakpoints[i];
            }
        }
        return null;
    }
}
