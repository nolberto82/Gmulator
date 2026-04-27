using Gmulator.Core.Snes.Sa1;
using Gmulator.Core.Snes.Spc;
using Gmulator.Interfaces;
using System.Runtime.CompilerServices;

namespace Gmulator.Core.Snes;

public sealed class Snes : Emulator, IConsole
{
    public SnesCpu Cpu;
    public SnesPpu Ppu;
    public SnesMmu Mmu;
    public SnesSpc Spc;
    public SnesSa1 Sa1;
    public SnesApu Apu;
    public SnesDsp Dsp;
    public SnesDma Dma;
    public SnesMapper Mapper;
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

    public Snes() : base()
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

    public override void LuaMemoryCallbacks() => Lua.InitMemCallbacks(this);

    public override void RunFrame(bool opened)
    {
        if (!opened && (EmuState == DebugState.Running || EmuState == DebugState.StepMain || EmuState == DebugState.StepSa1 || EmuState == DebugState.StepSpc))
        {
            SnesPpu ppu = Ppu;
            while (!Ppu.FrameReady)
            {
                int pc = Cpu.PBPC;
                if (Debug)
                {
                    if (Cpu.StepEnd(EmuState) || Spc.StepEnd(EmuState))
                    {
                        EmuState = DebugState.Break;
                        break;
                    }

                    Logger.Log(ppu.HPos);

                    if (Mapper.Coprocessor == SnesMapper.Gsu && EmuState == DebugState.StepGsu)
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
                    if (EmuState == DebugState.Break)
                        break;
                }

                Cpu.Step();
                Lua?.OnExec(pc);
                Run = false;
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
                AudioSamples.AddRange(dspSamples);
        }
    }

    public int ReadOp(int a)
    {
        return Mmu.ReadByte(a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadMemory(int addr)
    {
        addr &= 0xffffff;
        byte value = Mmu.ReadByte(addr);
        if (Debug)
            Debugger.Watchpoint(addr, value, CpuMap.Handlers[addr >> 12], false);
        return ApplyGameGenieCheats(addr, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteMemory(int a, byte v)
    {
        Mmu.WriteByte(a, v);
        if (Debug)
            Debugger.Watchpoint(a, v, CpuMap.Handlers[a >> 12], true);
    }

    public void HandleDma() => Dma.HandleDma();

    public int ReadWord(int a) => (ushort)(ReadOp(a) | ReadOp(a + 1) << 8);
    public int ReadLong(int a) => ReadOp(a) | ReadOp(a + 1) << 8 | ReadOp(a + 2) << 16;
    public int ReadVram(int a) => Ppu.Read(a) & 0xff;

    public override void Reset(string name, bool reset)
    {
        Mapper = new(CpuMap);
        if (!Mapper.LoadRom(name)) return;
        Mapper.GetMapper(this, Mapper.Rom);

        Cpu.SetSnes(this);

        if (Mapper != null)
        {
            Patch patch = new();
            var patched = patch.Run(Mapper.Rom, name);
            if (patched != null)
                Mapper.GetMapper(this, patched);
        }

        if (!reset)
        {
            GameName = name;
            LastName = GameName;
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

            if (Mapper.Coprocessor == SnesMapper.Sa1)
                Sa1 ??= new(this);

            Mapper.Reset(this);

#if DEBUG || RELEASE
            DebugWindow = new SnesDebugWindow(this);
            DebugWindow.Reset(this);
#endif
            SetActions();
            Mapper?.LoadSram();
            Mmu.Reset();
            Dsp.Reset();
            Apu.Reset();
            Sa1?.Reset();
            Ppu.Reset();
            Cpu.Reset(false);
            Spc.Reset();
            Dma.Reset();
            Logger.Reset();
            SpcLogger.Reset();
            LoadCheats(name);
            LoadBreakpoints(Mapper.Name);
            UpdateTexture(Screen.Texture, Ppu.ScreenBuffer);
            base.Reset(Mapper.Name, true);
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
                Sa1?.Mmu.Save(bw);
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
                    Sa1?.Mmu.Load(br);
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
        Ppu.SetIRQ = Cpu.SetIrq;
        Ppu.AutoJoyRead = Joypad.AutoRead;

        SpcLogger.GetDp = Spc.GetPage;

        Joypad.SetJoy1Low = Ppu.SetJoy1Low;
        Joypad.SetJoy1High = Ppu.SetJoy1High;
    }

    public byte ApplyGameGenieCheats(int addr, byte value)
    {
        if (Cheats.Count == 0 && Mmu.RamType != RamType.Rom) return value;
        var addr00 = addr & 0xfffff;
        var addr80 = addr | 0x800000;
        var cht = Cheats.ContainsKey((addr00, addr80)) &&
            Cheats[(addr00, addr80)].Enabled && Cheats[(addr00, addr80)].Type == GameGenie;
        if (cht)
            return Cheats[(addr00, addr80)].Value;
        return value;
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
