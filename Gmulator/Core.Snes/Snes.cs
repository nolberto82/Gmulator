using Gmulator.Core.Nes;
using Gmulator.Core.Snes.Mappers;
using Gmulator.Interfaces;
using Gmulator.Ui;
using Raylib_cs;
using System.Text;

namespace Gmulator.Core.Snes;

public class Snes : Emulator
{
    public SnesCpu Cpu;
    public SnesPpu Ppu;
    public SnesMmu Mmu;
    public SnesSpc Spc;
    public SnesSa1 Sa1;
    public SnesApu Apu;
    public SnesDsp Dsp;
    public SnesDma Dma;
    public BaseMapper Mapper;
    public readonly SnesJoypad Joypad;
    public SnesLogger Logger;
    public SnesSpcLogger SpcLogger;
    public readonly Breakpoint Breakpoint;

    private readonly List<float> AudioSamples = [];

    public bool IsSpc { get; set; }

    public Snes() : base(0x1000)
    {
        CheatConverter = new(Cheats);
        Breakpoint = new();
        Mmu = new(this);
        Cpu = new();
        Ppu = new(this);
        Spc = new();
        Apu = new(this);
        Dsp = new();
        Dma = new(this);
        Logger = new(this);
        SpcLogger = new();

        Buttons = new bool[16];
        Joypad = new();



#if DEBUG || DECKDEBUG
        //Debugger.Launch();
#endif
    }

    public override void LuaMemoryCallbacks()
    {
        LuaApi.InitMemCallbacks(Cpu, Mmu);
    }

    public override void RunFrame(bool opened)
    {
        if (Mapper == null) return;
        if (!opened && (State == DebugState.Running || State == DebugState.StepMain || State == DebugState.StepSa1))
        {
            bool frameready = Ppu.FrameReady;
            SnesPpu ppu = Ppu;
            LuaApi lua = LuaApi;
            SnesCpu cpu = Cpu;
            SnesSpc spc = Spc;
            BaseMapper mapper = Mapper;
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

                    if (State == DebugState.StepMain)
                    {
                        cpu.Step();
                        SetState(DebugState.Break);
                        break;
                    }
                    else if (State == DebugState.StepSa1)
                    {
                        Sa1?.Step();
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
                    else if (mapper.CoProcessor == BaseMapper.Gsu && State == DebugState.StepGsu)
                    {
                        //Gsu.Exec(state, Debug);
                        SetState(DebugState.Break);
                        break;
                    }

                    if (!Run && Breakpoints.Count > 0 && State == DebugState.Running)
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

                if (State == DebugState.Break)
                    break;
            }

            ppu.FrameReady = false;
            if (Cheats.Count != 0)
                ApplyRawCheats();

            UpdateTexture(Screen.Texture, Ppu.ScreenBuffer);

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
        return Mmu.ReadByte(a);
    }

    public int ReadMemory(int a)
    {
        int v = Mmu.ReadByte(a);
        CheckAccess(a, v, Mmu.RamType, Mmu.RamMask, false);
        if (Cheats.Count > 0 && Mmu.RamType == RamType.Rom)
        {
            lock (Cheats)
                return ApplyGameGenieCheats(a, v);
        }
        return v;
    }

    internal void WriteMemory(int a, int v)
    {
        Mmu.WriteByte(a, v);
        CheckAccess(a, v, Mmu.RamType, Mmu.RamMask, true);
    }

    public void HandleDma()
    {
        Dma.HandleDma();
    }

    public int ReadWord(int a) => (ushort)(ReadOp(a) | ReadOp(a + 1) << 8);
    public int ReadLong(int a) => ReadOp(a) | ReadOp(a + 1) << 8 | ReadOp(a + 2) << 16;
    public int ReadVram(int a) => Ppu.Read(a) & 0xff;

    public override void CheckAccess(int a, int v, RamType type, int mask, bool write)
    {
        base.CheckAccess(a, v, type, mask, write);
    }

    public override void Reset(string name, bool reset, uint[] pixels)
    {
        if (name != "")
        {
            Cpu.SetSnes(this);
            Mmu.SetSnes(this);

            Mapper = new BaseMapper().LoadRom(name, this);
            if (Mapper != null)
            {
                Patch patch = new();
                var patched = patch.Run(Mapper.Rom, name);
                if (patched != null)
                    Mapper = Mapper.Set(patched, name, this);
            }
            LastName = GameName;
            GameName = name;

            SetMemory(0x00, 0x3f, 0x0000, 0x1fff, 0x1fff, Mmu.ReadWram, Mmu.WriteWram, RamType.Wram, 0x1000);
            SetMemory(0x80, 0xbf, 0x0000, 0x1fff, 0x1fff, Mmu.ReadWram, Mmu.WriteWram, RamType.Wram, 0x1000);
            SetMemory(0x7e, 0x7f, 0x0000, 0xffff, 0x1ffff, Mmu.ReadWram, Mmu.WriteWram, RamType.Wram, 0x1000);

            if (Mapper.CoProcessor == BaseMapper.Sa1)
            {
                Sa1 = new(this);
                SetMemory(0x00, 0x3f, 0x2000, 0x3fff, 0xffff, Ppu.Read, Ppu.Write, RamType.Register, 0x1000);
                SetMemory(0x80, 0xbf, 0x2000, 0x3fff, 0xffff, Ppu.Read, Ppu.Write, RamType.Register, 0x1000);
            }
            else
            {
                SetMemory(0x00, 0x3f, 0x2100, 0x21ff, 0xffff, Ppu.Read, Ppu.Write, RamType.Register, 0x1000);
                SetMemory(0x80, 0xbf, 0x2100, 0x21ff, 0xffff, Ppu.Read, Ppu.Write, RamType.Register, 0x1000);
            }

            SetMemory(0x00, 0x3f, 0x4200, 0x42ff, 0xffff, Ppu.ReadIO, Ppu.WriteIO, RamType.Register, 0x1000);
            SetMemory(0x80, 0xbf, 0x4200, 0x42ff, 0xffff, Ppu.ReadIO, Ppu.WriteIO, RamType.Register, 0x1000);

            if (Mapper is LoRom || Mapper is Sa1Rom)
            {
                SetMemory(0x70, 0x7d, 0x0000, 0x7fff, 0x1fff, Mapper.ReadSram, Mapper.Write, RamType.Sram, 0x1000);
                SetMemory(0x00, 0x7d, 0x8000, 0xffff, 0x7fff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                SetMemory(0x80, 0xff, 0x8000, 0xffff, 0x7fff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);

                if (Mapper is Sa1Rom)
                {
                    SetMemory(0xc0, 0xcf, 0x0000, 0xffff, 0xffff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                    SetMemory(0xd0, 0xdf, 0x0000, 0xffff, 0xffff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                    SetMemory(0xe0, 0xef, 0x0000, 0xffff, 0xffff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                    SetMemory(0xf0, 0xff, 0x0000, 0xffff, 0xffff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                    SetMemory(0x00, 0x3f, 0x6000, 0x7fff, 0xffff, Mapper.ReadBwRam, Mapper.WriteBwRam, RamType.Sram, 0x1000);
                    SetMemory(0x40, 0x4f, 0x0000, 0x7fff, 0xffff, Mapper.ReadBwRam, Mapper.WriteBwRam, RamType.Sram, 0x1000);
                    SetMemory(0x60, 0x6f, 0x0000, 0x7fff, 0xffff, Mapper.ReadBwRam, Mapper.WriteBwRam, RamType.Sram, 0x1000);
                }
            }
            else if (Mapper is HiRom)
            {
                SetMemory(0x00, 0x3f, 0x8000, 0xffff, 0x7fff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                SetMemory(0x80, 0xbf, 0x8000, 0xffff, 0x7fff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                SetMemory(0x40, 0x7d, 0x0000, 0xffff, 0xffff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                SetMemory(0xc0, 0xff, 0x0000, 0xffff, 0xffff, Mapper.Read, Mapper.Write, RamType.Rom, 0x1000);
                SetMemory(0x20, 0x3f, 0x6000, 0x7fff, 0x1fff, Mapper.ReadSram, Mapper.Write, RamType.Sram, 0x1000);
                SetMemory(0xa0, 0xbf, 0x6000, 0x7fff, 0x1fff, Mapper.ReadSram, Mapper.Write, RamType.Sram, 0x1000);
            }
        }

        if (Mapper != null)
        {
#if DEBUG || RELEASE
            DebugWindow ??= new SnesDebugWindow(this);
#endif
            SetActions();
            Mapper?.LoadSram();
            Mmu.Reset();
            Dsp.Reset();
            Apu.Reset();
            Ppu.Reset();
            Cpu.Reset(false);
            Sa1?.Reset2();
            Spc.Reset();
            Dma.Reset();
            if (Mapper.CoProcessor == BaseMapper.Gsu)
            {
                //Gsu = new(this);
                //Gsu.Reset();
                //DebugWindow.SetCpu(this);
            }

            Logger.Reset();
            SpcLogger.Reset();
            LoadCheats(name);
            LoadBreakpoints(Mapper.Name);
            base.Reset(Mapper.Name, true, Ppu.ScreenBuffer);
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
                    Mapper.Sram = ReadArray<byte>(br, Mapper.Sram.Length);
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

    }

    public override void Input()
    {
        if (!Raylib.IsWindowFocused()) return;
        Joypad.Update(IsScreenWindow);
        base.Input();
    }

    public override void Close()
    {
        Mmu.Close();
        SaveBreakpoints(Mapper?.Name);
    }

    public override void Render(float MenuHeight) => base.Render(MenuHeight);

    public void SetActions()
    {
        Ppu.SetSnes(this);
        SpcLogger.SetSnes(this);
        Apu.SetSnes(this);
        Spc.SetSnes(this);
        Dsp.SetSnes(this);

        Ppu.SetNmi = Cpu.SetNmi;
        Ppu.SetIRQ = Cpu.SetIRQ;
        Ppu.AutoJoyRead = Joypad.AutoRead;

        SpcLogger.GetDp = Spc.GetPage;

        Joypad.SetJoy1L = Ppu.SetJoy1L;
        Joypad.SetJoy1H = Ppu.SetJoy1H;
    }

    public override void SetState(DebugState v) => base.SetState(v);

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
