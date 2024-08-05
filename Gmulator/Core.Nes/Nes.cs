using Gmulator.Shared;
using GNes.Core;
using GNes.Core.Mappers;
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
    public NesCpu Cpu { get; private set; }
    public NesPpu Ppu { get; private set; }
    public NesApu Apu { get; private set; }
    public NesMmu Mmu { get; private set; }
    public BaseMapper Mapper { get; private set; }
    public Header Cart { get; private set; }
    public Timer Timer { get; private set; }
    public NesLogger Logger { get; private set; }
    public Audio Audio { get; }
    public Dictionary<int, Breakpoint> Breakpoints { get; }
    public Cheat Cheat { get; }
    public NesJoypad Joypad1 { get; private set; } = new();
    public NesJoypad Joypad2 { get; private set; } = new();

    public Nes() { }
    public Nes(Cheat cheat, Dictionary<int, Cheat> Cheats, Dictionary<int, Breakpoint> breakpoints, ref LuaApi LuaApi)
    {
        Breakpoints = breakpoints;
        Cheat = cheat;
        Cheat.Init(Cheats, NesSystem);
        Mmu = new(Cheats);
        Cpu = new();
        Ppu = new(Mmu);
        Apu = new();
        Logger = new(Cpu);
        Ppu.Init(Apu);
        Mmu.Init(Joypad1, Joypad2);
        Input.Init(NesJoypad.GetButtons());
        DebugWindow = new(this, Breakpoints, NesSystem);

        DebugWindow.Read = Mmu.Read;
        DebugWindow.Write = Mmu.Write;
        DebugWindow.CpuStep = Cpu.Step;
        DebugWindow.OnBreakpointHit += Program.BreakpointTriggered;
        DebugWindow.OnReset += Program.Reset;
        DebugWindow.SetState += Program.SetState;
        DebugWindow.OnDisassemble += Logger.Disassemble;
        DebugWindow.OnToggle += Logger.Toggle;
        Logger.OnGetFlags += Cpu.GetFlags;
        Logger.OnGetRegs += Cpu.GetRegs;
        Logger.ReadByte += Mmu.Read;
        LuaApi.Read = Mmu.Read;
        Cpu.SetState = Program.SetState;

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

    public override void Execute(int State, bool debug)
    {
        if (Mapper != null && !Menu.Opened && State == Running)
        {
            var cyclesframe = Header.Region == 0 ? NesNtscCycles : NesPalCycles;
            while (Ppu.Cycles < cyclesframe)
            {
                ushort pc = (ushort)Cpu.PC;
                Cpu.Step();

                if (debug)
                {
                    if (Cpu.StepOverAddr == Cpu.PC)
                    {
                        State = Debugging;
                        Cpu.StepOverAddr = -1;
                        return;
                    }

                    DebugWindow.Check(pc);

                    if (NesLogger.Logging)
                        Logger.Toggle();
                }

                if (State == Debugging)
                    return;
            }
            Ppu.Cycles -= cyclesframe;
        }
    }

    public override void Update()
    {
        Input.Update(NesSystem, Ppu.FrameCounter);
    }

    public override void Render(float MenuHeight, bool debug)
    {
        base.Render(MenuHeight, debug);
    }

    public override void Reset(string name, bool reset, bool debug)
    {
        if (name != "")
            Mapper = Mmu.LoadRom(name);

        if (Mapper != null)
        {
            var sram = Mmu.Ram.AsSpan(0x6000, 0x2000).ToArray();
            Cpu.Reset();
            Ppu.Reset();
            Apu.Reset(Header.Region, Header.Region == 0 ? NesNtscCpuClock : NesPalCpuClock);
            Mmu.Reset();
            Logger.Reset();
            Cheat.Load(Mapper.Name, false);
        }
    }

    public override void SaveRam() => Mmu.SaveRam();

    public override void SaveState()
    {
        if (Mapper == null) return;
        Program.State = Paused;
        var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Header.Name)}.gs";
        using (BinaryWriter bw = new(new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write)))
        {
            bw.Write(Encoding.ASCII.GetBytes(SaveStateVersion));
            Mmu.Save(bw);
            Cpu.Save(bw);
            Ppu.Save(bw);
            Apu.Save(bw);
            Mapper.Save(bw);
        }
        Notifications.Init("State Saved Successfully");
        Program.State = Running;
    }

    public override void LoadState()
    {
        if (Mapper == null) return;
        Program.State = Paused;
        var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Header.Name)}.gs";
        if (!File.Exists(name)) return;
        using (BinaryReader br = new(new FileStream(name, FileMode.Open, FileAccess.Read)))
        {
            var version = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (version == SaveStateVersion)
            {
                Mmu.Load(br);
                Cpu.Load(br);
                Ppu.Load(br);
                Apu.Load(br);
                Mapper.Load(br);
                Notifications.Init("State Loaded Successfully");
            }
            else
                Notifications.Init("Error Loading Save State");
        }
        Program.State = Running;
    }

    public override void SaveBreakpoints(Dictionary<int, Breakpoint> Breakpoints, string name)
    {
        base.SaveBreakpoints(Breakpoints, name);
    }

    public override void LoadBreakpoints(Dictionary<int, Breakpoint> Breakpoints, string name)
    {
        base.LoadBreakpoints(Breakpoints, name);
    }

    public override void Close(Dictionary<int, Breakpoint> Breakpoints)
    {
        Cheat.Save(Mapper.Name);
        SaveBreakpoints(Breakpoints, Mapper.Name);
    }

    public override Nes GetConsole()
    {
        return this;
    }

    public override T GetRam<T>()
    {
        return (T)Convert.ChangeType(Mmu.Ram, typeof(T));
    }

    public override T GetPc<T>()
    {
        return (T)Convert.ChangeType(Cpu.PC, typeof(T));
    }

    public override T GetCpuInfo<T>(int i)
    {
        return (T)Convert.ChangeType(Cpu.GetRegs(), typeof(Dictionary<dynamic, string>));
    }

    //public override T GetPpuInfo<T>()
    //{
    //    return (T)Convert.ChangeType(IO.GetLCDC(), typeof(T));
    //}
}
