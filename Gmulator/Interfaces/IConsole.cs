namespace Gmulator.Interfaces;

public interface IConsole
{
    ICpu Cpu { get; }
    IPpu Ppu { get; }
    IMmu Mmu { get; }
    DebugState DbgState { get; set; }
    List<Breakpoint> Breakpoints { get; set; }
    string GameName { get; }
    void Reset(string name, bool reset);
}
