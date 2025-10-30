
using Gmulator.Core.Gbc.Mappers;
using Gmulator.Ui;
using Raylib_cs;
using System.Runtime.InteropServices;
using System.Text;

namespace Gmulator.Core.Gbc
{
    public class Gbc : ConsoleBase<Gbc>
    {
        public GbcIO IO;
        public GbcCpu Cpu;
        public GbcPpu Ppu;
        public GbcApu Apu;
        public GbcMmu Mmu;
        public BaseMapper Mapper;
        public GbcTimer Timer;
        public GbcJoypad Joypad;
        public GbcLogger Logger;

        public Gbc()
        {
            CheatConverter = new(this);
            Timer = new();
            IO = new(this);
            Mmu = new(IO, Cheats);
            Cpu = new(this);
            Ppu = new(this);
            Apu = new(Mmu, GbcCpuClock);
            Logger = new(Cpu);
            Joypad = new(new bool[8]);
            Mmu.Init(this);
            IO?.Init(this);
            //Input.Init(GbcJoypad.GetButtons());

            Cpu.Tick = Tick;

            Logger.ReadByte += Mmu.Read;
        }

        public void LuaMemoryCallbacks()
        {
            LuaApi.InitMemCallbacks(Mmu.Read, Mmu.Write, Cpu.GetReg, Cpu.SetReg);
        }

        public override void RunFrame(bool opened)
        {
            DebugState state = State;
            if (Mapper != null && state == DebugState.Running && !opened)
            {
                var cyclesframe = GbcCycles;

                while (Cpu?.Cycles < cyclesframe)
                {
                    int pc = Cpu.PC;
                    if (Debug)
                    {
                        if (!Run && Breakpoints?.Count > 0)
                        {
                            if (DebugWindow.ExecuteCheck(pc))
                            {
                                State = DebugState.Break;
                                return;
                            }
                        }

                        if (Logger?.Logging == true)
                            Logger?.Log(pc);

                        Run = false;
                    }

                    Cpu?.Step();
                    if (State == DebugState.Break)
                        return;
                }
                Cpu.Cycles -= cyclesframe;

                Mmu.ApplyParCheats();
            }
        }

        public override void Update()
        {
            //Input.Update(this, GbcConsole, Ppu.FrameCounter);
        }

        public override void Input()
        {
            if (Raylib.IsGamepadAvailable(0))
                Joypad.Update(IsScreenWindow);
        }

        public override void Render(float MenuHeight) => base.Render(MenuHeight);

        public void Tick()
        {
            var c = 4 / IO.SpeedMode;
            Cpu.Cycles += c;
            Timer?.Step(IO, Cpu, 4);
            Ppu?.Step(c);
            Apu?.Step(c);
        }

        public override void Reset(string name, bool reset)
        {
            if (name != "")
                Mapper = Mmu?.LoadRom(name);

            if (Mapper != null)
            {
#if DEBUG || RELEASE
                DebugWindow ??= new GbcDebugWindow(this);
#endif

                GameName = name;
                Cpu.SetAccess(this);
                Cpu?.Reset(Mmu.IsBios, Mapper.CGB);
                Ppu?.Reset(Mapper.CGB);
                Apu?.Reset();
                IO?.Reset();
                Mapper.Reset();
                Logger?.Reset();
                LoadBreakpoints(Mapper.Name);
                Cheat?.Load(this);
                base.Reset(name, true);
            }
        }

        public override void SaveState(int slot, StateResult res)
        {
            if (Mapper == null) return;

            lock (StateLock)
            {
                var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}.gs";
                if (name != "")
                {
                    using BinaryWriter bw = new(new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write));

                    bw.Write(Encoding.ASCII.GetBytes(EmuState.Version));
                    Mmu?.Save(bw);
                    Cpu?.Save(bw);
                    Ppu?.Save(bw);
                    Apu?.Save(bw);
                    IO?.Save(bw);
                    Mapper?.Save(bw);
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
                var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}.gs";
                if (File.Exists(name))
                {
                    using BinaryReader br = new(new FileStream(name, FileMode.Open, FileAccess.Read));

                    var version = Encoding.ASCII.GetString(br.ReadBytes(4));
                    if (version == EmuState.Version)
                    {
                        Mmu?.Load(br);
                        Cpu?.Load(br);
                        Ppu?.Load(br);
                        Apu?.Load(br);
                        IO?.Load(br);
                        Mapper?.Load(br);
                        base.LoadState(slot, StateResult.Success);
                    }
                    else
                        base.LoadState(slot, StateResult.Mismatch);
                }
                else
                    base.LoadState(slot, StateResult.Failed);
            }
        }

        public override void LoadBreakpoints(string name) => base.LoadBreakpoints(name);

        public override void SaveBreakpoints(string name) => base.SaveBreakpoints(Mapper?.Name);

        public override void Close()
        {
            //Mmu?.SaveRam();
            //Cheat.Save(GameName);
            SaveBreakpoints(GameName);
        }

        public override void SetState(DebugState v) => base.SetState(v);
    }
}
