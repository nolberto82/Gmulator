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

    public Nes()
    {
        CheatConverter = new(this);
        Mmu = new(Cheats);
        Cpu = new();
        Apu = new();
        Ppu = new();
        Logger = new(this);
        Joypad1 = new(new bool[8]);
        Joypad2 = new(new bool[8]);
        Ppu.Init(this);
        Mmu.Init(Joypad1, Joypad2);
        //Input.Init(NesJoypad.GetButtons());
        Buttons = new bool[8];

        Logger.ReadByte += Mmu.Read;

        Mmu.StatusR = Ppu.StatusR;
        Mmu.DataR = Ppu.DataR;
        Mmu.ControlW = Ppu.ControlW;
        Mmu.MaskW = Ppu.MaskW;
        Mmu.OamAddressW = Ppu.OamAddressW;
        Mmu.OamDataW = Ppu.OamDataW;
        Mmu.ScrollW = Ppu.ScrollW;
        Mmu.AddressDataW = Ppu.AddressDataW;
        Mmu.DataW = Ppu.DataW;
        Mmu.OamDamyCopy = Ppu.OamDamyCopy;
        Mmu.ApuRead = Apu.Read;
        Mmu.ApuWrite = Apu.Write;
        Cpu.PpuStep = Ppu.Step;
        Cpu.ReadByte = Mmu.Read;
        Cpu.WriteByte = Mmu.Write;
        Cpu.ReadWord = Mmu.ReadWord;
        Apu.Dmc.Read = Cpu.ReadDmc;
    }

    public void LuaMemoryCallbacks()
    {
        LuaApi.InitMemCallbacks(Mmu.Read, Mmu.Write, Cpu.GetReg, Cpu.SetReg);
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
                Mmu.ApplyRawCheats();
        }
    }

    //public override void Update() => Input.Update(this, NesConsole, Ppu.FrameCounter);
    public override void Input()
    {
        Joypad1.Update(IsScreenWindow);
    }

    public override void Render(float MenuHeight) => base.Render(MenuHeight);

    public override void Reset(string name, bool reset)
    {
        if (name != "")
            Mapper = Mmu.LoadRom(name);

        if (Mapper != null)
        {
            GameName = name;
            LastName = GameName;
            Mmu.Ram.AsSpan(0x6000, 0x2000).ToArray();
            Cpu.Reset();
            Ppu.Reset();
            Apu.Reset(Header.Region, Header.Region == 0 ? NesNtscCpuClock : NesPalCpuClock);
            Mmu.Reset();
            Logger.Reset();
            Cheat.Load(this);
            LoadBreakpoints(Mapper.Name);
            base.Reset(name, true);

#if DEBUG || RELEASE
            DebugWindow ??= new NesDebugWindow(this, Cpu, Ppu, Mmu, Logger);
#endif
        }
    }

    public override void SaveState(int slot, StateResult res)
    {
        if (Mapper == null) return;

        lock (StateLock)
        {
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

    public override void Close() => SaveBreakpoints(Mapper.Name);

    public override void SetState(DebugState v)
    {
        if (v == DebugState.Running)
            Run = true;
        base.SetState(v);
    }
}
