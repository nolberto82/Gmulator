
using Gmulator.Core.Gbc.Mappers;
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
            Cpu = new(Mmu, IO);
            Ppu = new(this);
            Apu = new(Mmu, GbcCpuClock);
            Logger = new();

            Mmu.Init(this);
            IO?.Init(Mmu, Ppu, Apu);
            Input.Init(GbcJoypad.GetButtons());

            Cpu.SetState = SetState;
            Cpu.Tick = Tick;

            Logger.GetFlags += Cpu.GetFlags;
            Logger.OnGetRegs += Cpu.GetRegs;
            Logger.ReadByte += Mmu.Read;
        }

        public override void Execute(bool opened, int times)
        {
            if (Mapper != null && State == Running && !opened)
            {
                var cyclesframe = GbcCycles;
                while (times-- > 0)
                {
                    while (Cpu?.Cycles < cyclesframe)
                    {
                        ushort pc = Cpu.PC;
                        if (Debug)
                        {
                            if (Breakpoints?.Count > 0)
                            {
                                if (DebugWindow.ExecuteCheck(pc))
                                {
                                    State = Break;
                                    return;
                                }
                            }

                            if (Logger?.Logging == true)
                                Logger?.LogToFile(pc);
                        }

                        Cpu?.Step();
                        if (State == Break)
                            return;
                    }
                    Cpu.Cycles -= cyclesframe;
                }
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

        public override void Reset(string name, string lastname, bool reset)
        {
            if (name != "")
                Mapper = Mmu?.LoadRom(name);

            if (reset)
                SaveBreakpoints(lastname);

            if (Mapper != null)
            {
                GameName = name;
                DebugWindow ??= new GbcDebugWindow(this);
                Cpu?.Reset(Mmu.IsBios, Mapper.CGB);
                Ppu?.Reset(Mapper.CGB);
                Apu?.Reset();
                IO?.Reset();
                Logger?.Reset();
                LoadBreakpoints(Mapper.Name);
                Cheat?.Load(this, false);
                base.Reset(name, lastname, true);
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

                    bw.Write(Encoding.ASCII.GetBytes(SaveStateVersion));
                    Mmu?.Save(bw);
                    Cpu?.Save(bw);
                    Ppu?.Save(bw);
                    Apu?.Save(bw);
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
                    if (version == SaveStateVersion)
                    {
                        Mmu?.Load(br);
                        Cpu?.Load(br);
                        Ppu?.Load(br);
                        Apu?.Load(br);
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
            Mmu?.SaveRam();
            Cheat.Save(GameName, Cheats);
            SaveBreakpoints(GameName);
        }

        public override void SetState(int v) => base.SetState(v);
    }
}
