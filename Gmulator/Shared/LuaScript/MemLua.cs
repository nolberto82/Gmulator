using Gmulator.Interfaces;

namespace Gmulator.Shared.Lua;

public partial class MemLua
{
    private NLua.Lua _state;
    private Func<int, byte> Read;
    private Action<int, byte> Write;
    private Func<int, byte> ReadVramByte;
    private Func<string, int> GetRegister;
    private Action<string, int> SetRegister;

    public MemLua(NLua.Lua state, IConsole console)
    {
        _state = state;
        Read = console.Mmu.ReadByte;
        Write = console.Mmu.WriteByte;
        ReadVramByte = console.Mmu.ReadVram;
        GetRegister = console.Cpu.GetReg;
        SetRegister = console.Cpu.SetReg;

        _state.NewTable("mem");
        _state.RegisterFunction("mem.readbyte", this, typeof(MemLua).GetMethod("ReadByte"));
        _state.RegisterFunction("mem.readword", this, typeof(MemLua).GetMethod("ReadWord"));
        _state.RegisterFunction("mem.writebyte", this, typeof(MemLua).GetMethod("WriteByte"));
        _state.RegisterFunction("mem.writeword", this, typeof(MemLua).GetMethod("WriteWord"));
        _state.RegisterFunction("emu.getregister", this, typeof(MemLua).GetMethod("GetReg"));
        _state.RegisterFunction("emu.setregister", this, typeof(MemLua).GetMethod("SetReg"));
        //_state.RegisterFunction("mem.onexec", this, typeof(MemLua).GetMethod("OnExec"));
    }

    public int ReadByte(int addr) => Read(addr);

    public int ReadWord(int addr) => Read(addr) | Read(addr + 1) << 8;

    public int ReadVramWord(int addr) => ReadVramByte(addr) | ReadVramByte(addr + 1) << 8;

    public void WriteByte(int addr, byte value) => Write(addr, value);

    public void WriteWord(int addr, int value)
    {
        Write(addr, (byte)value);
        Write(addr + 1, (byte)(value >> 8));
    }

    public int GetReg(string reg)
    {
        if (reg == null) return 0;
        return GetRegister(reg);
    }

    public void SetReg(string register, int value) => SetRegister(register, (byte)value);
}
