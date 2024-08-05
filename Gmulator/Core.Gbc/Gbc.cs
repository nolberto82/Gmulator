using GBoy.Core.Mappers;
using GBoy.Core;
using Gmulator.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbcTimer = GBoy.Core.GbcTimer;
using Raylib_cs;
using ImGuiNET;

namespace Gmulator.Core.Gbc
{
    public class Gbc : Emulator
    {
        public GbcIO IO { get; private set; }
        public GbcCpu Cpu { get; private set; }
        public GbcPpu Ppu { get; private set; }
        public GbcApu Apu { get; private set; }
        public GbcMmu Mmu { get; private set; }
        public BaseMapper Mapper { get; private set; }
        public GbcTimer Timer { get; private set; }
        public GbcLogger Logger { get; private set; }
        public Dictionary<int, Breakpoint> Breakpoints { get; }
        public Cheat Cheat { get; }

        public Gbc() { }
        public Gbc(Cheat cheat, Dictionary<int, Cheat> Cheats, Dictionary<int, Breakpoint> breakpoints, ref LuaApi LuaApi)
        {
            Breakpoints = breakpoints;
            Cheat = cheat;
            Cheat.Init(Cheats, GbcSystem);
            Timer = new();
            IO = new(Timer);
            Mmu = new(IO, Cheats);
            Cpu = new(Mmu, IO);
            Ppu = new(Mmu, IO);
            Apu = new(Mmu, GbcCpuClock);
            Logger = new();
            LuaApi.Read = Mmu.Read;

            //Mapper = new Mapper0();
            //Mapper.Init(new byte[0x8000], "");
            Mmu.Init(Cpu);
            IO.Init(Mmu, Ppu, Apu);
            Input.Init(GbcJoypad.GetButtons());

            DebugWindow = new(this, Breakpoints, GbcSystem);
            Cpu.Tick = Tick;
            Cpu.SetState = Program.SetState;

            DebugWindow.Read = Mmu.Read;
            DebugWindow.Write = Mmu.Write;
            DebugWindow.CpuStep = Cpu.Step;
            DebugWindow.OnBreakpointHit += Program.BreakpointTriggered;
            DebugWindow.OnReset += Program.Reset;
            DebugWindow.SetState += Program.SetState;
            DebugWindow.OnDisassemble += Logger.Disassemble;
            DebugWindow.OnToggle += Logger.Toggle;
            Logger.GetFlags += Cpu.GetFlags;
            Logger.OnGetRegs += Cpu.GetRegs;
            Logger.ReadByte += Mmu.Read;
        }

        public override void Execute(int State, bool debug)
        {
            if (Mapper != null && State == Running && !Menu.Opened)
            {
                var cyclesframe = GbcCycles;
                while (Cpu.Cycles < cyclesframe)
                {
                    ushort pc = Cpu.PC;
                    if (debug)
                    {
                        if (Breakpoints.Count > 0)
                            DebugWindow.Check(pc);
                        if (Program.State == Debugging)
                            return;

                        if (Logger.Logging)
                            Logger.LogToFile(pc);
                    }

                    Cpu.Step();
                    if (State == Debugging)
                        return;
                }
                Cpu.Cycles -= cyclesframe;
            }
        }

        public override void Update()
        {
            Input.Update(GbcSystem, Ppu.FrameCounter);
        }

        public override void Render(float MenuHeight, bool debug)
        {
            if (debug)
            {
                if (ImGui.Begin("Debugger"))
                {
                    //DebugWindow.Render(DebugWindow.Breakpoints, false);
                    ImGui.End();
                }
            }
            base.Render(MenuHeight, debug);
        }

        public void Tick()
        {
            var c = 4 / IO.SpeedMode;
            Cpu.Cycles += c;
            Timer.Step(IO, Cpu, 4);
            Ppu.Step(c);
            Apu.Step(c);
        }

        public override void Reset(string name, bool reset, bool debug)
        {
            if (name != "")
                Mapper = Mmu.LoadRom(name);

            if (Mapper != null)
            {
                var sram = Mmu.Sram.AsSpan(0x0000, 0x2000).ToArray();
                Cpu.Reset(Mmu.IsBios, Mapper.CGB, debug);
                Ppu.Reset(Mapper.CGB);
                Apu.Reset();
                IO.Reset();
                Logger.Reset();
                LoadBreakpoints(Breakpoints, Mapper.Name);
                Cheat.Load(Mapper.Name, false);
            }
        }

        public override void SaveRam() => Mmu.SaveRam();

        public override void SaveState()
        {
            if (Mapper == null) return;
            var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}.gs";
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
        }

        public override void LoadState()
        {
            if (Mapper == null) return;
            var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}.gs";
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
        }

        public override void LoadBreakpoints(Dictionary<int, Breakpoint> Breakpoints, string name)
        {
            base.LoadBreakpoints(Breakpoints, name);
        }

        public override void SaveBreakpoints(Dictionary<int, Breakpoint> Breakpoints, string name)
        {
            base.SaveBreakpoints(Breakpoints, Mapper.Name);
        }

        public override void Close(Dictionary<int, Breakpoint> Breakpoints)
        {
            Mmu.SaveRam();
            Cheat.Save(Mapper.Name);
            SaveBreakpoints(Breakpoints, Mapper.Name);
        }

        public override Gbc GetConsole()
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
            return (T)Convert.ChangeType(Cpu, typeof(object));
        }

        public override T GetPpuInfo<T>()
        {
            return (T)Convert.ChangeType(IO.GetLCDC(), typeof(T));
        }
    }
}
