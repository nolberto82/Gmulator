using Gmulator.Core.Snes.Mappers;
using Gmulator.Core.Snes.Sa1;
using Gmulator.Core.Snes.Spc;
using Gmulator.Interfaces;
using Gmulator.Ui;

namespace Gmulator.Core.Snes;

public class Snes : Emulator, IConsole
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
    public MemoryMap CpuMap;

    private readonly List<float> AudioSamples = [];

    public bool IsSpc => field;
    ICpu IConsole.Cpu => Cpu;
    IPpu IConsole.Ppu => Ppu;
    IMmu IConsole.Mmu => Mmu;

    public Debugger Debugger { get; set; }
    public DebugState EmuState { get; set; }

    public Snes()
    {
        CpuMap = new(0x1000);
        Console = this;
        CheatConverter = new(Cheats);
        Debugger = new(this);
        Mmu = new(this);
        Cpu = new();
        Ppu = new();
        Spc = new();
        Apu = new();
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

    public override void LuaMemoryCallbacks() => LuaApi.InitMemCallbacks(Cpu, Mmu);

    public override void RunFrame(bool opened)
    {
        if (Mapper == null) return;
        if (!opened && (EmuState == DebugState.Running || EmuState == DebugState.StepMain || EmuState == DebugState.StepSa1 || EmuState == DebugState.StepSpc))
        {
            bool frameready = Ppu.FrameReady;
            SnesPpu ppu = Ppu;
            LuaApi lua = LuaApi;
            SnesSpc spc = Spc;
            BaseMapper mapper = Mapper;
            //Sa1?.State = DebugState.Running;
            while (!ppu.FrameReady)
            {
                int pc = Cpu.PBPC;
#if DEBUG || DECKDEBUG || RELEASE
                if (Debug)
                {
                    if (Cpu.StepEnd(EmuState) || Spc.StepEnd(EmuState))
                    {
                        EmuState = DebugState.Break;
                        break;
                    }

                    Logger.Log(ppu.HPos);

                    if (mapper.CoProcessor == BaseMapper.Gsu && EmuState == DebugState.StepGsu)
                    {
                        //Gsu.Exec(state, Debug);
                        EmuState = DebugState.Break;
                        break;
                    }

                    if (!Run && Breakpoints.Count > 0 && EmuState == DebugState.Running)
                    {
                        if (Debugger.Execute(pc))
                        {
                            EmuState = DebugState.Break;
                            break;
                        }
                    }
                }

                if (EmuState == DebugState.Break)
                    break;
#endif

                lua?.OnExec(pc);
                Cpu.Step();
                Run = false;
            }

            //ppu.Step(262 * 1364);

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
        a &= 0xffffff;
        int v = Mmu.ReadByte(a);
        Debugger.Access(a, v, CpuMap.Handlers[a >> 12], false);
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
        Debugger.Access(a, v, CpuMap.Handlers[a >> 12], true);
    }

    public void HandleDma() => Dma.HandleDma();

    public int ReadWord(int a) => (ushort)(ReadOp(a) | ReadOp(a + 1) << 8);
    public int ReadLong(int a) => ReadOp(a) | ReadOp(a + 1) << 8 | ReadOp(a + 2) << 16;
    public int ReadVram(int a) => Ppu.Read(a) & 0xff;

    public override void Reset(string name, bool reset, uint[] pixels)
    {
        if (name != "")
        {
            Cpu.SetSnes(this);

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
        }

        if (Mapper != null)
        {
            CpuMap.Register(0x00, 0x3f, 0x2000, 0x21ff, Ppu.Read, Ppu.Write);
            CpuMap.Register(0x80, 0xbf, 0x2000, 0x21ff, Ppu.Read, Ppu.Write);
            CpuMap.Register(0x00, 0x3f, 0x4200, 0x42ff, Ppu.ReadIO, Ppu.WriteIO);
            CpuMap.Register(0x80, 0xbf, 0x4200, 0x42ff, Ppu.ReadIO, Ppu.WriteIO);

            CpuMap.Wram(0x00, 0x3f, 0x0000, 0x1fff, Mmu.ReadWram, Mmu.WriteWram);
            CpuMap.Wram(0x80, 0xbf, 0x0000, 0x1fff, Mmu.ReadWram, Mmu.WriteWram);
            CpuMap.Wram(0x7e, 0x7f, 0x0000, 0xffff, Mmu.ReadWram, Mmu.WriteWram);

            if (Mapper.CoProcessor == BaseMapper.Sa1)
                Sa1 ??= new(this);

            Mapper.Reset(this);


#if DEBUG || RELEASE
            if (DebugWindow == null)
            {
                DebugWindow = new SnesDebugWindow(this);
                Sa1?.DebugWindow = DebugWindow;
                DebugWindow.Reset(this);
            }
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
                bw.Write(Encoding.ASCII.GetBytes(Shared.EmuState.Version));
                Cpu.Save(bw);
                Ppu.Save(bw);
                Mmu.Save(bw);
                Spc.Save(bw);
                Apu.Save(bw);
                Dsp.Save(bw);
                Dma.Save(bw);
                Sa1?.Save(bw);
                Sa1?.Cpu.Save(bw);
                Sa1?.Mmu.Save(bw);
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

                if (Encoding.ASCII.GetString(br.ReadBytes(4)) == Shared.EmuState.Version)
                {
                    Cpu.Load(br);
                    Ppu.Load(br);
                    Mmu.Load(br);
                    Spc.Load(br);
                    Apu.Load(br);
                    Dsp.Load(br);
                    Dma.Load(br);
                    Sa1?.Load(br);
                    Sa1?.Cpu.Load(br);
                    Sa1?.Mmu.Load(br);
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

    public override void Close() => SaveBreakpoints(Mapper?.Name);

    public override void Render(float MenuHeight) => base.Render(MenuHeight);

    public void SetActions()
    {
        Ppu.SetSnes(this);
        SpcLogger.SetSnes(this);
        Apu.SetSnes(this);
        Spc.SetSnes(this);
        Dsp.SetSnes(this);

        Ppu.SetNmi = Cpu.SetNmi;
        Ppu.SetIRQ = Cpu.SetIrq;
        Ppu.AutoJoyRead = Joypad.AutoRead;

        SpcLogger.GetDp = Spc.GetPage;

        Joypad.SetJoy1L = Ppu.SetJoy1L;
        Joypad.SetJoy1H = Ppu.SetJoy1H;
    }

    public int ApplyGameGenieCheats(int a, int v)
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
