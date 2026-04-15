namespace Gmulator.Interfaces
{
    public interface IConsole
    {
        ICpu Cpu { get; }
        IPpu Ppu { get; }
        IMmu Mmu { get; }
        DebugState EmuState { get; set; }
        Debugger Debugger { get; set; }
        List<Breakpoint> Breakpoints { get; set; }
    }
}
