using Gmulator.Core.Nes;
using Gmulator.Core.Nes.Mappers;
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
    private NesCpu Cpu;
    private NesPpu Ppu;
    private NesApu Apu;
    private NesMmu Mmu;
    private BaseMapper Mapper;
    private NesLogger Logger;
    private NesJoypad Joypad1 = new();
    private NesJoypad Joypad2 = new();

    public Nes()
    {
        CheatConverter = new(this);
        Mmu = new(Cheats);
        Cpu = new();
        Apu = new();
        Ppu = new(Mmu, Apu);
        Logger = new(Cpu);
        Ppu.Init(this);
        Mmu.Init(Joypad1, Joypad2);
        Input.Init(NesJoypad.GetButtons());

        Logger.OnGetFlags += Cpu.GetFlags;
        Logger.OnGetRegs += Cpu.GetRegisters;
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

    public override void Execute(bool opened)
    {
        if (Mapper != null && State == DebugState.Running && !opened)
        {
            var cyclesframe = Header.Region == 0 ? NesNtscCycles : NesPalCycles;
            while (Ppu.Cycles < cyclesframe)
            {
                ushort pc = (ushort)Cpu.PC;
                Cpu.Step();

                if (Debug)
                {
                    if (Cpu.StepOverAddr == Cpu.PC)
                    {
                        State = DebugState.Break;
                        Cpu.StepOverAddr = -1;
                        return;
                    }

                    if (Breakpoints.Count > 0)
                    {
                        if (DebugWindow.ExecuteCheck(pc))
                        {
                            State = DebugState.Break;
                            return;
                        }
                    }

                    if (Logger.Logging)
                        Logger.Log(pc);
                }

                if (State == DebugState.Break)
                    return;
            }
            Ppu.Cycles -= cyclesframe;

            if (Cheats.Count > 0)
                Mmu.ApplyRawCheats();
        }
    }

    public override void Update() => Input.Update(this, NesConsole, Ppu.FrameCounter);

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
            DebugWindow ??= new NesDebugWindow(this, Cpu, Mmu, Logger);
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

    public override void SetState(DebugState v) => base.SetState(v);
}
