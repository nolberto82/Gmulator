using Gmulator.Core.Nes.Mappers;
using Gmulator.Interfaces;

namespace Gmulator.Core.Nes;

public class Nes : Emulator, IConsole
{
    public NesCpu Cpu;
    public NesPpu Ppu;
    public NesApu Apu;
    public NesMmu Mmu;
    public BaseMapper Mapper;
    public NesLogger Logger;
    public NesJoypad Joypad1;
    public NesJoypad Joypad2;
    public MemoryMap CpuMap;

    ICpu IConsole.Cpu { get => Cpu; }
    IPpu IConsole.Ppu { get => Ppu; }
    IMmu IConsole.Mmu => Mmu;

    public Debugger Debugger { get; set; }
    public DebugState EmuState { get; set; }

    public Nes()
    {
        Console = this;
        CpuMap = new(0x10000);
        CheatConverter = new(Cheats);
        Debugger = new(this);
        Mmu = new(Cheats);
        Cpu = new(this);
        Apu = new(this);
        Ppu = new();
        Logger = new(this);
        Joypad1 = new();
        Joypad2 = new();
        Ppu.Init(this);

        Logger.ReadByte += Mmu.ReadByte;
        Cpu.PpuStep = Ppu.Step;
        Apu.Dmc.Read = Cpu.ReadDmc;

        Mmu.Init(this);

        CpuMap.Set(0x00, 0x00, 0x0000, 0x1fff, Mmu.ReadWram, Mmu.WriteWram, RamType.Wram, 1);
        CpuMap.Set(0x00, 0x00, 0x2000, 0x2fff, Ppu.ReadRegister, Ppu.WriteRegister, RamType.Register, 1);
        CpuMap.Set(0x00, 0x00, 0x4015, 0x4015, Apu.Read, Apu.Write, RamType.Register, 1);
        CpuMap.Set(0x00, 0x00, 0x4014, 0x4014, a => 0, Ppu.Write4014, RamType.Register, 1);
        CpuMap.Set(0x00, 0x00, 0x4016, 0x4016, Joypad1.Read, Joypad1.Write, RamType.Register, 1);
        CpuMap.Set(0x00, 0x00, 0x4017, 0x4017, Joypad2.Read, Apu.Write, RamType.Register, 1);
    }

    public override void LuaMemoryCallbacks() => Lua.InitMemCallbacks(this);

    public override void RunFrame(bool opened)
    {
        if (Mapper != null && (EmuState == DebugState.Running || EmuState == DebugState.StepMain) && !opened)
        {
            var cyclesframe = Header.Region == 0 ? NesNtscCycles : NesPalCycles;
            while (Ppu.Cycles < cyclesframe)
            {
                ushort pc = (ushort)Cpu.PC;

                if (Debug)
                {
                    if (EmuState == DebugState.StepMain)
                    {
                        Cpu.Step();
                        EmuState = DebugState.Break;
                        return;
                    }

                    if (Cpu.StepOverAddr == Cpu.PC)
                    {
                        EmuState = DebugState.Break;
                        Cpu.StepOverAddr = -1;
                        return;
                    }

                    if (!Run && Breakpoints.Count > 0)
                        Debugger.Execute(pc);

                    if (Logger.Logging)
                        Logger.Log();

                    Run = false;
                }

                if (EmuState == DebugState.Break)
                    return;

                Cpu.Step();
                Lua?.OnExec(pc);
            }
            Ppu.Cycles -= cyclesframe;

            UpdateTexture(Screen.Texture, Ppu.ScreenBuffer);

            if (Cheats.Count > 0)
                Mmu.ApplyParCheats();
        }
    }

    public override void Input()
    {
        if (Raylib.IsGamepadAvailable(0))
            Joypad1.Update(IsScreenWindow, Ppu.FrameCounter);
    }

    public override void Render(float MenuHeight) => base.Render(MenuHeight);

    public override void Reset(string name, bool reset)
    {
        if (name != "")
            Mapper = Mmu.LoadRom(name);

        if (Mapper != null)
        {
            CpuMap.Set(0x00, 0x00, 0x6000, 0x7fff, Mapper.ReadSram, Mapper.WriteSram, RamType.Sram, 1);
            CpuMap.Set(0x00, 0x00, 0x8000, 0xffff, Mapper.ReadPrg, Mapper.Write, RamType.Rom, 1);

            GameName = Mapper.Name;
            LastName = GameName;
            Cpu.Reset(CpuMap.Handlers);
            Ppu.Reset();
            Apu.Reset(Header.Region, Header.Region == 0 ? NesNtscCpuClock : NesPalCpuClock);
            Mmu.Reset();
            Logger.Reset();
            LoadCheats(name);
            LoadBreakpoints(Mapper.Name);
            UpdateTexture(Screen.Texture, Ppu.ScreenBuffer);
            base.Reset(name, true);

#if DEBUG || RELEASE
            DebugWindow ??= new NesDebugWindow(this);
#endif
        }
    }

    public override void SaveState(int slot, StateResult res)
    {
        if (Mapper == null) return;

        var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Header.Name)}.gs";
        if (name != "")
        {
            using BinaryWriter bw = new(new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write));

            bw.Write(Encoding.ASCII.GetBytes(Shared.EmuState.Version));
            Mmu.Save(bw);
            Cpu.Save(bw);
            Ppu.Save(bw);
            Apu.Save(bw);
            Mapper.Save(bw);
            base.SaveState(slot, StateResult.Success);
        }
        else
            base.SaveState(slot, StateResult.Failed);
    }

    public override void LoadState(int slot, StateResult res)
    {
        if (Mapper == null) return;

        lock (StateLock)
        {
            var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Header.Name)}.gs";
            if (File.Exists(name))
            {
                using BinaryReader br = new(new FileStream(name, FileMode.Open, FileAccess.Read));

                if (Encoding.ASCII.GetString(br.ReadBytes(4)) == Shared.EmuState.Version)
                {
                    Mmu.Load(br);
                    Cpu.Load(br);
                    Ppu.Load(br);
                    Apu.Load(br);
                    Mapper.Load(br);
                    base.LoadState(slot, StateResult.Success);
                }
                else
                    base.LoadState(slot, StateResult.Mismatch);
            }
            else
                base.LoadState(slot, StateResult.Failed);
        }
    }

    public override void SaveBreakpoints(string name) => base.SaveBreakpoints(name);

    public override void LoadBreakpoints(string name) => base.LoadBreakpoints(name);

    public override void Close() => SaveBreakpoints(Mapper?.Name);
}
