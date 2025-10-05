using Gmulator.Core.Snes;
using Gmulator.Core.Snes.Mappers;
using ImGuiNET;
using Raylib_cs;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.Arm;
using System.Text;

namespace Gmulator.Core.Snes;
public class Snes : Emulator
{
    private SnesCpu Cpu;
    private SnesPpu Ppu;
    private SnesMmu Mmu;
    private SnesSpc Spc;
    private SnesSa1 Sa1;
    private SnesApu Apu;
    private SnesDsp Dsp;
    private SnesDma Dma;
    private BaseMapper Mapper;
    private readonly SnesJoypad Joypad = new();
    private SnesLogger Logger;
    private SnesSpcLogger SpcLogger;
    private readonly Breakpoint Breakpoint;

    private List<SnesDma> _dma;

    private int RamAddr;
    private bool DmaEnabled;
    private int DmaState;

    private readonly List<float> AudioSamples = [];

    private bool Run;

    public bool IsSpc { get; set; }

    public void SetRun(bool v) => Run = v;

    public Snes()
    {
        CheatConverter = new(this);
        Breakpoint = new();
        Cpu = new();
        Ppu = new();
        Mmu = new();
        Sa1 = new(this);
        Spc = new();
        Apu = new();
        Dsp = new();
        Dma = new(this, Cpu);
        Logger = new(this, Cpu);
        SpcLogger = new(Cpu, Spc, Apu);

#if DEBUG || DECKDEBUG
        //Debugger.Launch();
#endif
    }
    public void LuaMemoryCallbacks()
    {
        LuaApi.InitMemCallbacks(Mmu.Read, Mmu.Write, Cpu.GetReg, Cpu.SetReg);
    }

    public override void Execute(bool opened)
    {
        if (Mapper == null) return;
        if (!opened && (State == DebugState.Running || State == DebugState.StepMain))
        {
            bool frameready = Ppu.FrameReady;
            SnesPpu ppu = Ppu;
            LuaApi lua = LuaApi;
            SnesCpu cpu = Cpu;
            SnesSpc spc = Spc;
            BaseMapper mapper = Mapper;
            DebugState state = State;
            while (!ppu.FrameReady)
            {
                int pc = cpu.PB << 16 | cpu.PC;
#if DEBUG || DECKDEBUG || RELEASE
                if (Debug)
                {
                    int s_pc = spc.PC;
                    if (cpu.StepOverAddr == pc)
                    {
                        SetState(DebugState.Break);
                        cpu.StepOverAddr = -1;
                        break;
                    }

                    if (spc.StepOverAddr == s_pc)
                    {
                        SetState(DebugState.Break);
                        spc.StepOverAddr = -1;
                        break;
                    }

                    Logger.Log(ppu.HPos);

                    if (state == DebugState.StepMain)
                    {
                        cpu.Step();
                        SetState(DebugState.Break);
                        break;
                    }
                    else if (State == DebugState.StepSpc)
                    {
                        cpu.Step();
                        spc.Step();
                        SetState(DebugState.Break);
                        break;
                    }
                    else if (mapper.CoProcessor == BaseMapper.CoprocessorGsu && State == DebugState.StepGsu)
                    {
                        //Gsu.Exec(state, Debug);
                        SetState(DebugState.Break);
                        break;
                    }

                    if (!Run && Breakpoints.Count > 0 && state == DebugState.Running)
                    {
                        if (DebugWindow.ExecuteCheck(pc))
                        {
                            SetState(DebugState.Break);
                            break;
                        }
                    }
                    Run = false;
                }
#endif

                lua?.OnExec(pc);
                cpu.Step();
            }

            ppu.FrameReady = false;
            if (Cheats.Count != 0)
                ApplyRawCheats();

            float[] dspSamples = Dsp.GetSamples();
            int totalSamples = AudioSamples.Count + dspSamples.Length;
            if (totalSamples >= SnesMaxSamples)
            {
                float[] outputSamples = new float[totalSamples];
                AudioSamples.CopyTo(outputSamples, 0);
                Array.Copy(dspSamples, 0, outputSamples, AudioSamples.Count, dspSamples.Length);
                Audio.Update(outputSamples);
                AudioSamples.Clear();
            }
            else
            {
                AudioSamples.AddRange(dspSamples);
            }
        }
    }

    public int ReadOp(int a)
    {
        if (Mapper == null) return 0;
        return Mmu.Read(a) & 0xff;
    }

    public int ReadMemory(int a)
    {
        int v = Mmu.Read(a) & 0xff;

        if (Debug && DebugWindow.AccessCheck(a, -1, Mmu.MemType, false))
            State = DebugState.Break;

        if (Cheats.Count == 0)
            return v;

        lock (Cheats)
            return ApplyGameGenieCheats(a, v);
    }

    internal void WriteMemory(int a, int v)
    {
        Mmu.Write(a, v);
        if (Debug && DebugWindow.AccessCheck(a, (byte)v, Mmu.MemType, true))
            State = DebugState.Break;
    }

    public void HandleDma()
    {
        Dma.HandleDma();
    }

    public int ReadWord(int a) => (ushort)(ReadOp(a) | ReadOp(a + 1) << 8);
    public int ReadLong(int a) => ReadOp(a) | ReadOp(a + 1) << 8 | ReadOp(a + 2) << 16;
    public int ReadVram(int a) => Ppu.Read(a) & 0xff;

    public override void Reset(string name, bool reset)
    {
        if (name != "")
        {
            Mapper = new BaseMapper().LoadRom(name);
            if (Mapper != null)
            {
                Patch patch = new();
                var patched = patch.Run(Mapper.Rom, name);
                if (patched != null)
                    Mapper = Mapper.Set(patched, name);
            }
            LastName = GameName;
            GameName = name;
        }

        if (Mapper != null)
        {
#if DEBUG || RELEASE
            DebugWindow ??= new SnesDebugWindow(this, Cpu, Ppu, Spc, Apu, Sa1, Mapper, Dma, Logger, SpcLogger);
#endif
            Cpu.SetSnes(this, Ppu, Mapper);
            Mmu.SetSnes(Cpu, Ppu, Apu, Sa1, Mapper, Dma, Joypad);
            SetActions();
            Mapper?.LoadSram();
            Mmu.Reset();
            Dsp.Reset();
            Apu.Reset();
            Ppu.Reset();
            Cpu.Reset();
            Spc.Reset();
            Dma.Reset();
            if (Mapper.CoProcessor == BaseMapper.CoprocessorGsu)
            {
                //Gsu = new(this);
                //Gsu.Reset();
                //DebugWindow.SetCpu(this);
            }

            Logger.Reset();
            SpcLogger.Reset(); ;
            LoadBreakpoints(Mapper.Name);
            DmaEnabled = false;
            base.Reset(LastName, true);
        }
    }

    public override void SaveBreakpoints(string name) => base.SaveBreakpoints(name);

    public override void SaveState(int slot, StateResult res)
    {
        if (Mapper == null) return;

        lock (StateLock)
        {
            var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}{slot}.gs";
            if (name != "")
            {
                using BinaryWriter bw = new(new FileStream(name, FileMode.Create, FileAccess.Write));
                bw.Write(Encoding.ASCII.GetBytes(EmuState.Version));
                Cpu.Save(bw);
                Ppu.Save(bw);
                Mmu.Save(bw);
                Spc.Save(bw);
                Apu.Save(bw);
                Dsp.Save(bw);
                Dma.Save(bw);
                bw.Write(Mapper.Sram);
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
            var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}{slot}.gs";
            if (File.Exists(name))
            {
                using FileStream fs = new(name, FileMode.Open, FileAccess.Read);
                using Stream stream = fs;
                using BinaryReader br = new(fs);

                if (Encoding.ASCII.GetString(br.ReadBytes(4)) == EmuState.Version)
                {
                    Cpu.Load(br);
                    Ppu.Load(br);
                    Mmu.Load(br);
                    Spc.Load(br);
                    Apu.Load(br);
                    Dsp.Load(br);
                    Dma.Load(br);
                    Mapper.Sram = EmuState.ReadArray<byte>(br, Mapper.Sram.Length);
                    base.LoadState(slot, StateResult.Success);
                }
                else
                    base.LoadState(slot, StateResult.Mismatch);
            }
            else
                base.LoadState(slot, StateResult.Failed);
        }
    }

    public override void Update()
    {
        if (!Raylib.IsWindowFocused()) return;
        Input.Update(this, SnesConsole, Ppu.FrameCounter);
    }

    public override void Close()
    {
        //Cheat.Save(GameName);
        SaveBreakpoints(Mapper?.Name);
    }

    public override void Render(float MenuHeight) => base.Render(MenuHeight);

    public void Continue()
    {
        if (State != DebugState.Paused)
        {
            SetState(DebugState.Running);
            Cpu.Step();
            Spc.Step();
            Logger.Log(Ppu.HPos);
            //if (SpcLogger.Logging)
            //    SpcLogger.Log(Cpu.PB << 16 | Spc.PC);
        }
    }

    public void StepInto(bool scanline)
    {
        if (!scanline)
        {
            //Logger.Log(Cpu.PB << 16 | Cpu.PC);
            //SpcLogger.Log(Spc.PC);
            //if (!IsSpc)
            Cpu.Step();
            Spc.Step();
            SetState(DebugState.Break);
        }
        else
        {
            var old = Ppu.VPos;
            while (old == Ppu.VPos)
            {
                //Logger.Log(Cpu.PB << 16 | Cpu.PC);
                Cpu.Step();
                Spc.Step(true);
            }
        }
    }

    public void StepOver()
    {
        if (!IsSpc)
        {
            int op = ReadMemory(Cpu.PB << 16 | Cpu.PC) & 0xff;
            if (op == 0x20 || op == 0x22 || op == 0xfc)
            {
                Cpu.StepOverAddr = (ushort)(Cpu.PC + Cpu.Disasm[op].Size);
            }
            else
                SetState(DebugState.StepMain);
        }
        else
        {
            int opspc = Spc.Read(Spc.PC) & 0xff;
            if (IsSpc && opspc == 0x3f)
            {
                Spc.StepOverAddr = (ushort)(Spc.PC + Spc.Disasm[opspc].Size);
            }
            else
                SetState(DebugState.StepMain);
        }
    }

    public void SetActions()
    {
        Ppu.SetSnes(this, Cpu, Apu, Dma);
        SpcLogger.SetSnes(this, Cpu, Spc, Apu);
        Apu.SetSnes(this, Cpu, Ppu, Spc, Dsp, SpcLogger);
        Spc.SetSnes(this, Apu);
        Dsp.SetSnes(this, Apu);

        Ppu.SetNmi = Cpu.SetNmi;
        Ppu.SetIRQ = Cpu.SetIRQ;
        Ppu.AutoJoyRead = Joypad.AutoRead;

        Logger.GetFlags = Cpu.GetFlags;
        Logger.GetRegs = Cpu.GetRegisters;
        SpcLogger.GetDp = Spc.GetPage;

        Joypad.SetJoy1L = Ppu.SetJoy1L;
        Joypad.SetJoy1H = Ppu.SetJoy1H;

        Input.SetButtons = Joypad.SetButtons;
    }

    public override void SetState(DebugState v) => base.SetState(v);

    public byte[] GetWram() => Mmu.GetWram();
    public byte[] GetSram() => Mapper.Sram;
    public byte[] GetVram() => Ppu.GetVram().ToByteArray();
    public byte[] GetCram() => Ppu.GetCram().ToByteArray();
    public byte[] GetOram() => Ppu.GetOam().ToByteArray();
    public byte[] GetSpc() => Apu.Ram;
    public byte[] GetRom() => Mapper.Rom;
    public byte[] GetIram() => Sa1?.Iram;
    //public byte[] GetGsuRam() => Gsu?.GetRam();

    public byte ReadWram(int a) => Mmu.ReadRam(a);
    public byte ReadSram(int a) => Mapper.Sram[a];
    public byte ReadCram(int a) => (byte)Ppu.ReadCram(a);
    public byte ReadRom(int a) => Mapper.Rom[a];

    private int ApplyGameGenieCheats(int a, int v)
    {
        var cht = Cheats.ContainsKey(a) && Cheats[a].Enabled && Cheats[a].Type == GameGenie;
        if (cht)
            return Cheats[a].Value;
        return v;
    }

    public void ApplyRawCheats()
    {
        foreach (var c in from c in Cheats
                          where c.Value.Enabled && c.Value.Type == ProAction
                          select c)
        {
            Mmu.GetWram()[c.Value.Address & 0x1ffff] = c.Value.Value;
        }
    }
}
