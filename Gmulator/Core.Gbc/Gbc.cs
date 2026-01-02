
using Gmulator.Core.Gbc.Mappers;
using Gmulator.Interfaces;
using Gmulator.Ui;
using Raylib_cs;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;

namespace Gmulator.Core.Gbc
{
    public class Gbc : Emulator
    {
        public GbcCpu Cpu;
        public GbcPpu Ppu;
        public GbcApu Apu;
        public GbcMmu Mmu;
        public BaseMapper Mapper;
        public GbcTimer Timer;
        public GbcJoypad Joypad;
        public GbcLogger Logger;

        public Gbc() : base(0x10000)
        {
            CheatConverter = new(Cheats);
            Mmu = new GbcMmu(this, Cheats);
            Cpu = new GbcCpu(this);
            Ppu = new(this);
            Apu = new(this, GbcCpuClock);
            Timer = new(this);
            Logger = new(Cpu);
            Joypad = new(this);

            Cpu.Tick = Tick;

            Logger.ReadByte += Mmu.ReadByte;
        }

        public override void LuaMemoryCallbacks()
        {
            LuaApi.InitMemCallbacks(Cpu, Mmu);
        }

        public override void RunFrame(bool opened)
        {
            if (Mapper != null && (State == DebugState.Running || State == DebugState.StepMain) && !opened)
            {
                var cyclesframe = GbcCycles;

                while (Cpu?.Cycles < cyclesframe)
                {
                    int pc = Cpu.PC;
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

                        if (Logger?.Logging == true)
                            Logger?.Log(pc);

                        if (!Run && Breakpoints?.Count > 0)
                        {
                            if (DebugWindow.ExecuteCheck(pc))
                            {
                                State = DebugState.Break;
                                return;
                            }
                        }

                        Run = false;
                    }

                    Cpu?.Step();

                    if (State != DebugState.Running)
                        State = DebugState.Break;

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
                Joypad.Update(IsScreenWindow, Ppu.FrameCounter);
        }

        public override void Render(float MenuHeight) => base.Render(MenuHeight);

        public void Tick()
        {
            var c = 4 / Ppu.SpeedMode;
            Cpu.Cycles += c;
            Ppu?.Step(c);
            Apu?.Step(c);
            Timer?.Step(Cpu, 4);
        }

        public override void Reset(string name, bool reset, uint[] pixels)
        {
            if (name != "")
            {
                Mapper = Mmu?.LoadRom(name);

                SetMemory(0x00, 0x00, 0x0000, 0x3fff, 0x3fff, Mapper.ReadRom, Mapper.WriteRom0, RamType.Rom, 1);
                SetMemory(0x00, 0x00, 0x4000, 0x7fff, 0x3fff, Mapper.ReadRom, Mapper.WriteRom1, RamType.Rom, 1);
                SetMemory(0x00, 0x00, 0x8000, 0x9fff, 0x1fff, Mmu.ReadVramBank, Mmu.WriteVramBank, RamType.Vram, 1);
                SetMemory(0x00, 0x00, 0xa000, 0xbfff, 0x1fff, Mmu.ReadSram, Mmu.WriteSram, RamType.Sram, 1);
                SetMemory(0x00, 0x00, 0xc000, 0xdfff, 0x1fff, Mmu.ReadWram, Mmu.WriteWram, RamType.Wram, 1);
                SetMemory(0x00, 0x00, 0xfe00, 0xfe9f, 0x009f, Mmu.ReadOam, (int a, int v) => { }, RamType.Oram, 1);
                SetMemory(0x00, 0x00, 0xff80, 0xfffe, 0xffff, Mmu.ReadRam, Mmu.WriteRam, RamType.Register, 1);

            }

            if (Mapper != null)
            {
#if DEBUG || RELEASE
                DebugWindow ??= new GbcDebugWindow(this);
#endif
                Mmu.Reset(name);
                Mapper.LoadSram();
                GameName = name;
                Cpu.SetAccess(this);
                Cpu?.Reset(Mapper.CGB, Mmu.IsBios);
                Ppu?.Reset(Mapper.CGB);
                Apu?.Reset();
                Mapper.Reset();
                Logger?.Reset();
                LoadCheats(name);
                LoadBreakpoints(Mapper.Name);
                base.Reset(name, true, Ppu.ScreenBuffer);
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

        public override void Close() => SaveBreakpoints(GameName);

        public override void SetState(DebugState v) => base.SetState(v);

        public bool GetCGBEnabled() => (bool)(Mapper?.CGB);
        public bool GetBiosEnabled => (bool)(Mmu?.IsBios);
    }
}
