using Gmulator.Interfaces;

namespace Gmulator.Shared;

public class Debugger(IConsole console)
{
    private readonly List<Breakpoint> _breakpoints = console.Breakpoints;

    public bool Execute(int a)
    {
        var bp = _breakpoints.Find(b => b.Addr == a);
        if (bp != null)
        {
            if (bp.Enabled)
            {
                if (a == bp.Addr)
                {
                    if (bp.Type == BPType.Exec || bp.Type == BPType.SpcExec || bp.Type == BPType.GsuExec)
                        return true;
                }
            }
        }
        return false;
    }

    public void Access(int a, int v, MemoryHandler handler, bool write)
    {
        var bp = _breakpoints.Find(b => b.Addr == a);
        bp ??= _breakpoints.Find(b => b.Addr == (handler?.Offset | a & 0xfff));
        var type = handler == null ? RamType.Vram : handler.Type;
        if (bp != null && bp.Enabled && bp.Type > 0)
        {
            if ((bp.Type == BPType.Write && bp.Write && write) ||
                (bp.Type == BPType.Read && !bp.Write && !write))
            {
                if ((bp.Condition == -1) || (bp.Condition == (v & 0xff)))
                    console.EmuState = DebugState.Break;
            }
        }
    }

    public void AccessSpc(int a, int v, RamType memtype, bool write)
    {
        var bp = _breakpoints.Find(b => b.Addr == a);
        if (bp != null && bp.Enabled && (bp.Type & (BPType.Read | BPType.Write)) > 0)
        {
            if ((bp.Type == BPType.Write && bp.Write && write) ||
                (bp.Type == BPType.Read && !bp.Write && !write))
            {
                if ((a == bp.Addr && bp.Condition == -1) || (a == bp.Addr && bp.Condition == v))
                    console.EmuState = DebugState.Break;
            }
        }
    }
}
