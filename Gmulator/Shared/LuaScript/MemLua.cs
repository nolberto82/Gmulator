using Gmulator.Interfaces;
using NLua;

namespace Gmulator.Shared.LuaScript;

public partial class MemLua
{
    private readonly Lua _state;
    private readonly Func<int, byte> Read;
    private readonly Action<int, byte> Write;
    private readonly Func<int, byte> ReadVramByte;
    private readonly Func<string, int> GetRegister;
    private readonly Action<string, int> SetRegister;

    public MemLua(Lua state, IConsole console)
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

    public int GetReg(string register)
    {
        if (register == null) return 0;
        return GetRegister(register);
    }

    public void SetReg(string register, int value) => SetRegister(register, (byte)value);
}
