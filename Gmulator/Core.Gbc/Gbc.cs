
using Gmulator.Core.Gbc.Mappers;
using System.Runtime.InteropServices;
using System.Text;

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

        public Gbc()
        {
            CheatConverter = new(this);
            Timer = new();
            IO = new(Timer);
            Mmu = new(IO, Cheats);
            Cpu = new(this);
            Ppu = new(this);
            Apu = new(Mmu, GbcCpuClock);
            Logger = new(Cpu);

            Mmu.Init(this);
            IO?.Init(Mmu, Ppu, Apu);
            Input.Init(GbcJoypad.GetButtons());

            Cpu.Tick = Tick;

            Logger.GetFlags += Cpu.GetFlags;
            Logger.OnGetRegs += Cpu.GetRegs;
            Logger.ReadByte += Mmu.Read;
        }

        public void LuaMemoryCallbacks()
        {
            LuaApi.InitMemCallbacks(Mmu.Read, Mmu.Write, Cpu.GetReg, Cpu.SetReg);
        }

        public override void Execute(bool opened)
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
                        if (Breakpoints?.Count > 0)
                        {
                            if (DebugWindow.ExecuteCheck(pc))
                            {
                                State = DebugState.Break;
                                return;
                            }
                        }

                        if (Logger?.Logging == true)
                            Logger?.Log(pc);
                    }

                    Cpu?.Step();
                    if (State == DebugState.Break)
                        return;
                }
                Cpu.Cycles -= cyclesframe;
                Mmu.ApplyParCheats();
            }
        }

        public override void Update() => Input.Update(this, GbcConsole, Ppu.FrameCounter);

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
                GameName = name;
                DebugWindow ??= new GbcDebugWindow(this);
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
