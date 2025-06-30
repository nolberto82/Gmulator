using Gmulator.Core.Snes.Mappers;
using Raylib_cs;
using Gmulator.Core.Snes;
using ImGuiNET;
using System.Reflection.Metadata.Ecma335;
using System.ComponentModel.Design;
using System.Text;

namespace Gmulator.Core.Snes;
public class Snes : Emulator
{
    public SnesCpu Cpu { get; private set; }
    public SnesPpu Ppu { get; private set; }
    public SnesSa1 Sa1 { get; private set; }
    public SnesSpc Spc { get; private set; }
    public SnesApu Apu { get; private set; }
    public SnesDsp Dsp { get; private set; }
    public List<SnesDma> Dma { get => _dma; private set => _dma = value; }
    public BaseMapper Mapper { get; private set; }
    public SnesJoypad Joypad { get; private set; } = new();
    public SnesLogger Logger { get; private set; }
    public SnesSpcLogger SpcLogger { get; private set; }
    public Breakpoint Breakpoint { get; private set; }

    private List<SnesDma> _dma;
    public byte[] Ram = new byte[0x20000];
    public Int64 CpuSpc { get; set; }

    private readonly List<float> AudioSamples = [];

    public bool Run { get; set; }

    public bool IsSpc { get; set; }

    public Snes()
    {
        CheatConverter = new(this);
        Breakpoint = new();
        Cpu = new();
        Ppu = new();
        Sa1 = new(this);
        Spc = new();
        Apu = new();
        Dsp = new();
        Logger = new(this);
        SpcLogger = new();
        SetActions();
    }

    public override void Execute(bool opened, int times)
    {
        if (Mapper == null) return;
        if (!opened && (State == Running || State == StepMain))
        {
            while (times-- > 0)
            {
                while (!Ppu.FrameReady)
                {
                    int pc = Cpu.PB << 16 | Cpu.PC;
#if DEBUG || RELEASE
                    if (Debug)
                    {
                        switch (DebugStep(pc))
                        {
                            case 1: return;
                            case 2: continue;
                        }
                    }
#endif

                    Cpu.Step();

                    if (State == StepMain)
                        State = Break;
                }
                Ppu.FrameReady = false;
            }

            AudioSamples.AddRange(Dsp.GetSamples());
            if (AudioSamples.Count >= SnesMaxSamples)
            {
                Audio.Update(AudioSamples.ToArray());
                AudioSamples.Clear();
            }
        }
    }

    private int DebugStep(int pc)
    {
        if (Cpu.StepOverAddr == (Cpu.PB << 16 | Cpu.PC))
        {
            SetState(Break);
            Cpu.StepOverAddr = -1;
            return 1;
        }

        if (Spc.StepOverAddr == Spc.PC)
        {
            SetState(Break);
            Spc.StepOverAddr = -1;
            return 1;
        }

        Logger.Log(Cpu.PB << 16, (ushort)pc);

        if (State == StepMain)
        {
            return -1;
        }

        if (State == StepSpc)
        {
            Cpu.Step();
            Spc.Step();
            SetState(Break);
            return 2;
        }

        if (!Run && Breakpoints.Count > 0 && State == Running)
        {
            if (DebugWindow.ExecuteCheck(Cpu.PB << 16 | Cpu.PC))
                State = Break;
        }

        Run = false;

        if (State == Break)
            return 1;
        return 0;
    }

    private int MemType;
    public byte ReadOp(int a)
    {
        if (Mapper == null) return 0;
        return Read(a >> 16, a & 0xffff);
    }

    public byte ReadMemory(int a)
    {
        var bank = a >> 16;
        a &= 0xffff;

        byte v = Read(bank, a);

        if (Debug && DebugWindow.AccessCheck(a, -1, MemType, false))
            State = Break;

        lock (Cheats)
            return ApplyCheats(bank << 16, a, v);
    }

    int RamAddr;
    public bool DmaEnabled { get; private set; }
    private int DmaState;

    internal void WriteMemory(int a, int v)
    {
        var bank = a >> 16;
        a &= 0xffff;

        Write(bank, a, (byte)v);

        if (Debug && DebugWindow.AccessCheck(a, (byte)v, MemType, true))
            State = Break;
    }

    private byte Read(int bank, int a)
    {
        if (bank == 0x7e || bank == 0x7f)
        {
            MemType = RamType.Wram;
            return Ram[(((bank & 1) << 16) | a & 0xffff)];
        }
        else if (a < 0x8000 && (bank < 0x40 || bank >= 0x80 && bank < 0xc0))
        {
            switch (a)
            {
                case <= 0x1fff: MemType = RamType.Wram; return Ram[a];
                case >= 0x2100 and <= 0x213f: return Ppu.Read((byte)a);
                case <= 0x217f:
                    MemType = RamType.Register;
                    Apu.Step();
                    return Apu.ReadFromSpu(a);
                case >= 0x2300 and < 0x2400: MemType = RamType.Register; return SnesSa1.ReadReg(a);
                case >= 0x3000 and <= 0x37ff: MemType = RamType.Register; return Sa1.Read(a);
                case 0x4016: MemType = RamType.Register; return Joypad.Read4016();
                case >= 0x4200 and <= 0x421f: MemType = RamType.Register; return Ppu.ReadIO(a & 0x1f);
                case >= 0x4300 and <= 0x437f:
                {
                    var i = (a & 0xf0) / 0x10;
                    switch (a & 0x0f)
                    {
                        case 0x00:
                            return (byte)((Dma[i].Direction ? 0x80 : 0x00) |
                            Dma[i].Step << 3 |
                            Dma[i].Mode & 7 |
                            Dma[i].RamAddress);
                        case 0x01: return (byte)Dma[i].BAddress;
                        case 0x02: return (byte)Dma[i].AAddress;
                        case 0x03: return (byte)(Dma[i].AAddress >> 8);
                        case 0x04: return (byte)(Dma[i].AAddress >> 16);
                        case 0x05: return (byte)Dma[i].Size;
                        case 0x06: return (byte)(Dma[i].Size >> 8);
                        case 0x07: break;
                    }
                    break;
                }
                case >= 0x6000 and <= 0x7fff: MemType = RamType.Sram; return Mapper.ReadBwRam(a);
            }
        }
        MemType = RamType.Rom;
        return Mapper.Read(bank, a);
    }

    private void Write(int bank, int a, byte v)
    {
        if (bank == 0x7e || bank == 0x7f)
        {
            MemType = RamType.Wram;
            Ram[(((bank & 1) << 16) | a & 0xffff)] = v;
        }
        else if (a < 0x8000 && (bank < 0x40 || bank >= 0x80 && bank < 0xc0))
        {
            switch (a)
            {
                case <= 0x1fff: MemType = RamType.Wram; Ram[a] = v; break;
                case >= 0x2100 and <= 0x213f: MemType = RamType.Register; Ppu.Write(a, v); break;
                case <= 0x217f:
                    MemType = RamType.Register;
                    Apu.Step();
                    Apu.WriteToSpu(a & 3, v);
                    break;
                case 0x2180: MemType = RamType.Register; Ram[((RamAddr & 0x10000) | RamAddr++ & 0xffff)] = v; break;
                case 0x2181: MemType = RamType.Register; RamAddr = RamAddr & 0xffff00 | v; break;
                case 0x2182: MemType = RamType.Register; RamAddr = RamAddr & 0xff00ff | v << 8; break;
                case 0x2183: MemType = RamType.Register; RamAddr = RamAddr & 0x00ffff | v << 16; break;
                case >= 0x2200 and < 0x2300: MemType = RamType.Register; Sa1.WriteReg(a, v); return;
                case >= 0x3000 and <= 0x37ff: MemType = RamType.Register; Sa1.Write(a, v); break;
                case >= 0x4200 and <= 0x420a: MemType = RamType.Register; Ppu.WriteIO(a, v); break;
                case 0x420b:
                {
                    DmaEnabled = v > 0;
                    DmaState = v > 0 ? 1 : 0;
                    for (int i = 0; i < Dma.Count; i++)
                        Dma[i].Enabled = v.GetBit((byte)i);
                    break;
                }
                case 0x420c:
                {
                    for (int i = 0; i < Dma.Count; i++)
                        Dma[i].HdmaEnabled = v.GetBit((byte)i);
                    break;
                }
                case 0x420d: Cpu.FastMem = v.GetBit(0); break;
                case >= 0x4300 and <= 0x437f: SnesDma.Set(a, v, ref _dma, RamAddr); break;
                case >= 0x6000 and <= 0x7fff: MemType = RamType.Sram; Mapper.WriteBwRam(a, v); break;
            }
        }
        Mapper.Write(bank, a, v);
    }

    public void HandleDma()
    {
        if (!DmaEnabled) return;
        if (DmaState == 1)
        {
            DmaState = 2;
            return;
        }

        Cpu.Idle8();
        for (int i = 0; i < 8; i++)
        {
            if (Dma[i].Enabled)
            {
                var src = Dma[i].ABank << 16 | Dma[i].AAddress;
                int count = 0;
                do
                {
                    if (!Dma[i].Transfer(src, Dma[i].Mode, count))
                    {
                        if (Dma[i].Step == 0)
                            src++;
                        else if (Dma[i].Step == 2)
                            src--;
                    }
                    Dma[i].Size--;
                    count = (count + 1) & 3;
                } while (Dma[i].Size != 0);
                Dma[i].AAddress = (ushort)src;
                Dma[i].Enabled = false;
            }
        }
        DmaEnabled = false;
        DmaState = 0;
    }

    public int ReadWord(int a) => (ushort)(ReadOp(a) | ReadOp(a + 1) << 8);
    public int ReadLong(int a) => ReadOp(a) | ReadOp(a + 1) << 8 | ReadOp(a + 2) << 16;
    public byte ReadVram(int a) => (byte)Ppu.Vram[a];

    public void WriteVram(int a, int v)
    {
        if ((a & 1) == 0)
        {
            var s = Ppu.Vram[a >> 1];
            Ppu.Vram[a >> 1] = (ushort)(s & 0xff00 | v);
        }
        else
        {
            var s = Ppu.Vram[a >> 1];
            Ppu.Vram[a >> 1] = (ushort)(s & 0x00ff | v << 8);
        }
    }

    public override void Reset(string name, string lastname, bool reset)
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
        }

        if (reset)
            SaveBreakpoints(lastname);

        if (Mapper != null)
        {
            GameName = name;
            SetDmaActions();
            Mapper?.LoadSram();
            Array.Fill<byte>(Ram, 0);
            DebugWindow ??= new SnesDebugWindow(this);
            Dsp.Reset();
            Apu.Reset();
            Ppu.Reset();
            Cpu.Reset();
            Spc.Reset();
            Logger.Reset();
            SpcLogger.Reset();
            Cheat.Load(this, false);
            LoadBreakpoints(Mapper.Name);
            DmaEnabled = false;
            base.Reset(name, lastname, true);
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
                SnesDma Dma = new(_dma);
                using BinaryWriter bw = new(new FileStream(name, FileMode.Create, FileAccess.Write));
                bw.Write(Encoding.ASCII.GetBytes(SaveStateVersion));
                Cpu.Save(bw);
                Ppu.Save(bw);
                Spc.Save(bw);
                Apu.Save(bw);
                Dsp.Save(bw);
                Dma.Save(bw);
                bw.Write(Ram);
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
                SnesDma Dma = new(_dma);
                using FileStream fs = new(name, FileMode.Open, FileAccess.Read);
                using Stream stream = fs;
                using BinaryReader br = new(fs);

                if (Encoding.ASCII.GetString(br.ReadBytes(4)) == SaveStateVersion)
                {
                    Cpu.Load(br);
                    Ppu.Load(br);
                    Spc.Load(br);
                    Apu.Load(br);
                    Dsp.Load(br);
                    Dma.Load(br);
                    Ram = EmuState.ReadArray<byte>(br, Ram.Length);
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
        Cheat.Save(GameName, Cheats);
        SaveBreakpoints(Mapper?.Name);
    }

    public override void Render(float MenuHeight) => base.Render(MenuHeight);

    public void Continue()
    {
        if (State != 2)
        {
            SetState(Running);
            Cpu.Step();
            Spc.Step();
            Logger.Log(Cpu.PB << 16, Cpu.PC);
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
            SetState(Break);
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
            byte op = ReadMemory(Cpu.PB << 16 | Cpu.PC);
            if (op == 0x20 || op == 0x22 || op == 0xfc)
            {
                Cpu.StepOverAddr = (ushort)(Cpu.PC + Cpu.Disasm[op].Size);
            }
            else
                SetState(StepMain);
        }
        else
        {
            byte opspc = Spc.Read(Spc.PC);
            if (IsSpc && opspc == 0x3f)
            {
                Spc.StepOverAddr = (ushort)(Spc.PC + Spc.Disasm[opspc].Size);
            }
            else
                SetState(StepMain);
        }
    }

    public void SetActions()
    {
        Cpu.SetSnes(this);
        Ppu.SetSnes(this);
        SpcLogger.SetSnes(this);
        Apu.SetSnes(this);
        Spc.SetSnes(this);
        Dsp.SetSnes(this);

        Ppu.SetNmi = Cpu.SetNmi;
        Ppu.SetIRQ = Cpu.SetIRQ;
        Ppu.AutoJoyRead = Joypad.AutoRead;

        Logger.GetFlags = Cpu.GetFlags;
        Logger.GetRegs = Cpu.GetRegs;
        SpcLogger.GetDp = Spc.GetPage;

        Joypad.SetJoy1L = Ppu.SetJoy1L;
        Joypad.SetJoy1H = Ppu.SetJoy1H;

        Input.SetButtons = Joypad.SetButtons;

        SetDmaActions();
    }

    private void SetDmaActions()
    {
        Dma = [];
        for (int i = 0; i < 8; i++)
        {
            Dma.Add(new());
            Dma[i].Read = ReadMemory;
            Dma[i].Write = WriteMemory;
            Dma[i].SetSnes(this);
        }
    }

    public override void SetState(int v) => base.SetState(v);

    public byte[] GetWram() => Ram;
    public byte[] GetSram() => Mapper.Sram;
    public byte[] GetVram() => Ppu.Vram.ToByteArray();
    public byte[] GetCram() => Ppu.Cram.ToByteArray();
    public byte[] GetOram() => Ppu.Oam;
    public byte[] GetSpc() => Apu.Ram;
    public byte[] GetRom() => Mapper.Rom;
    public byte[] GetIram() => Sa1?.Iram;

    public byte ReadWram(int a) => Ram[a];
    public byte ReadSram(int a) => Mapper.Sram[a];
    public byte ReadCram(int a) => Ppu.Cram.ToByteArray()[a];
    public byte ReadRom(int a) => Mapper.Rom[a];

    private byte ApplyCheats(int bank, int a, byte v)
    {
        var ba = bank | a;
        var cht = Cheats.ContainsKey(ba);
        if (cht)
        {
            if (Cheats[ba].Type == GameGenie)
            {
                if (Cheats[ba].Enabled)
                    return Cheats[ba].Value;
            }
        }

        //if (bank == 0x7e || bank == 0x7f)
        {
            foreach (var c in Cheats)
            {
                if (c.Value.Enabled && c.Value.Type == GameShark)
                    Ram[(((bank & 1) << 16) | c.Value.Address & 0xffff)] = c.Value.Value;
            }
        }
        return v;
    }
}
