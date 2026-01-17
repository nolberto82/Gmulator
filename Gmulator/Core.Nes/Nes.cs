using Gmulator.Core.Nes;
using Gmulator.Core.Nes.Mappers;
using Gmulator.Ui;
using ImGuiNET;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Nes;

public class Nes : Emulator
{
    public NesCpu Cpu;
    public NesPpu Ppu;
    public NesApu Apu;
    public NesMmu Mmu;
    public BaseMapper Mapper;
    public NesLogger Logger;
    public NesJoypad Joypad1;
    public NesJoypad Joypad2;

    public Nes() : base(0x10000)
    {
        CheatConverter = new(Cheats);
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

        SetMemory(0x00, 0x00, 0x0000, 0x1fff, 0x07ff, Mmu.ReadWram, Mmu.WriteWram, RamType.Wram, 1);
        SetMemory(0x00, 0x00, 0x2000, 0x2fff, 0x2007, Ppu.ReadRegister, Ppu.WriteRegister, RamType.Register, 1);
        SetMemory(0x00, 0x00, 0x4015, 0x4015, 0xffff, Apu.Read, Apu.Write, RamType.Register, 1);
        SetMemory(0x00, 0x00, 0x4014, 0x4014, 0xffff, (int a) => 0, Ppu.Write4014, RamType.Register, 1);
        SetMemory(0x00, 0x00, 0x4016, 0x4016, 0xffff, Joypad1.Read, Joypad1.Write, RamType.Register, 1);
        SetMemory(0x00, 0x00, 0x4017, 0x4017, 0xffff, Joypad2.Read, Apu.Write, RamType.Register, 1);

    }

    public override void LuaMemoryCallbacks()
    {
        LuaApi.InitMemCallbacks(Cpu, Mmu);
    }

    public override void RunFrame(bool opened)
    {
        if (Mapper != null && (State == DebugState.Running || State == DebugState.StepMain) && !opened)
        {
            var cyclesframe = Header.Region == 0 ? NesNtscCycles : NesPalCycles;
            while (Ppu.Cycles < cyclesframe)
            {
                ushort pc = (ushort)Cpu.PC;

                if (Debug)
                {
                    if (State == DebugState.StepMain)
                    {
                        Cpu.Step();
                        State = DebugState.Break;
                        return;
                    }

                    if (Cpu.StepOverAddr == Cpu.PC)
                    {
                        State = DebugState.Break;
                        Cpu.StepOverAddr = -1;
                        return;
                    }

                    if (!Run && Breakpoints.Count > 0)
                    {
                        if (DebugWindow.ExecuteCheck(pc))
                        {
                            State = DebugState.Break;
                            return;
                        }
                    }

                    if (Logger.Logging)
                        Logger.Log(pc);

                    Run = false;
                }

                if (State == DebugState.Break)
                    return;

                Cpu.Step();
            }
            Ppu.Cycles -= cyclesframe;

            UpdateTexture(Screen.Texture, Ppu.ScreenBuffer);

            if (Cheats.Count > 0)
                Mmu.ApplyParCheats();
        }
    }

    //public override void Update() => Input.Update(this, NesConsole, Ppu.FrameCounter);
    public override void Input()
    {
        if (Raylib.IsGamepadAvailable(0))
            Joypad1.Update(IsScreenWindow, Ppu.FrameCounter);
    }

    public override void Render(float MenuHeight) => base.Render(MenuHeight);

    public override void Reset(string name, bool reset, uint[] pixels)
    {
        if (name != "")
            Mapper = Mmu.LoadRom(name);

        if (Mapper != null)
        {
            SetMemory(0x00, 0x00, 0x6000, 0x7fff, 0x1fff, Mapper.ReadSram, Mapper.WriteSram, RamType.Sram, 1);
            SetMemory(0x00, 0x00, 0x8000, 0xffff, 0xffff, Mapper.ReadPrg, Mapper.Write, RamType.Rom, 1);

            GameName = Mapper.Name;
            LastName = GameName;
            Cpu.Reset();
            Ppu.Reset();
            Apu.Reset(Header.Region, Header.Region == 0 ? NesNtscCpuClock : NesPalCpuClock);
            Mmu.Reset();
            Logger.Reset();
            LoadCheats(name);
            LoadBreakpoints(Mapper.Name);
            base.Reset(name, true, Ppu.ScreenBuffer);

#if DEBUG || RELEASE
            DebugWindow ??= new NesDebugWindow(this, Cpu, Ppu, Mmu, Logger);
            Cpu.AccessCheck = DebugWindow.AccessCheck;
            Cpu.SetState = SetState;
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

            bw.Write(Encoding.ASCII.GetBytes(EmuState.Version));
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

                if (Encoding.ASCII.GetString(br.ReadBytes(4)) == EmuState.Version)
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

    public override void SetState(DebugState v)
    {
        base.SetState(v);
    }
}
