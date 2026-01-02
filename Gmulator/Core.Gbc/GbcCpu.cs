using Gmulator.Interfaces;
using System.Diagnostics;

namespace Gmulator.Core.Gbc;

public partial class GbcCpu : ICpu, ISaveState
{
    public int AF
    {
        get { return (A << 8 | F) & 0xffff; }
        set { A = (value >> 8) & 0xff; F = value & 0xff; }
    }

    public int BC
    {
        get { return (B << 8 | C) & 0xffff; }
        set { B = (value >> 8) & 0xff; C = value & 0xff; }
    }

    public int DE
    {
        get { return (D << 8 | E) & 0xffff; }
        set { D = (value >> 8) & 0xff; E = value & 0xff; }
    }

    public int HL
    {
        get { return (H << 8 | L) & 0xffff; }
        set { H = (value >> 8) & 0xff; L = value & 0xff; }
    }
    public int SP { get => sp & 0xffff; set => sp = value & 0xffff; }
    public int PC { get => pc & 0xffff; set => pc = value & 0xffff; }

    private int _ie;
    private int _if;
    private int _sb;
    private int _sc;
    public int Cycles { get; set; }
    private bool _halt;
    private bool _ime;
    private int _stop;
    private int _imeDelay;
    public int StepOverAddr { get; set; }

    private int A, F, B, C, D, E, H, L, pc, sp;

    public static int GetWord(int low, int high) => (ushort)(high << 8 | low);
    public bool FlagZ { get => (F & FZ) == FZ; }
    public bool FlagN { get => (F & FN) == FN; }
    public bool FlagH { get => (F & FH) == FZ; }
    public bool FlagC { get => (F & FC) == FC; }

    private int Read0F(int a) => _if;
    private int ReadFF(int a) => _ie;
    private void Write0F(int a, int v) => _if = v & 0xff;
    private void WriteFF(int a, int v) => _ie = v & 0xff;

    private int Read01() => _sb;
    private int Read02() => _sc;

    private Gbc Gbc;

    public GbcMmu Mmu { get; private set; }

    private Func<int, int, RamType, int, bool, bool> AccessCheck;
    private Action<DebugState> SetState;
    public Action Tick { get; set; }

    public GbcCpu(Gbc gbc)
    {
        Gbc = gbc;
        Mmu = gbc.Mmu;
        GenerateOpInfo();

        gbc.SetMemory(0x00, 0x01, 0xff0f, 0x0ff0f, 0xffff, Read0F, Write0F, RamType.Register, 1);
        gbc.SetMemory(0x00, 0x01, 0xffff, 0x0ffff, 0xffff, ReadFF, WriteFF, RamType.Register, 1);
    }

    public void SetAccess(Gbc gbc)
    {
        if (gbc.DebugWindow != null)
            AccessCheck = gbc.DebugWindow.AccessCheck;
        SetState = gbc.SetState;
    }

    public void Reset(bool cgb, bool isbios)
    {
        if (!cgb)
        {
            AF = 0x01b0; BC = 0x0013;
            DE = 0x00d8; HL = 0x014d;
            PC = 0x0100; SP = 0xfffe;
        }
        else
        {
            AF = 0x1180; BC = 0x0000;
            DE = 0xff56; HL = 0x000d;
            PC = 0x0100; SP = 0xfffe;
        }

        if (isbios)
            PC = 0x0000;

        _halt = false;
        _ime = false;
        _ie = _if = 0;
        StepOverAddr = -1;
        Cycles = 0;
    }

    private void SetF(bool flag, int v)
    {
        if (flag)
            F |= (byte)v;
        else
            F = (byte)(F & ~v);
    }

    public int ReadCycle(int a)
    {
        Tick();
        int v = Mmu.ReadByte(a) & 0xff;
        Gbc.CheckAccess(a, v, Mmu.RamType, Mmu.RamMask, false);
        return v;
    }

    public void WriteCycle(int a, int v)
    {
        Tick();
        Mmu.WriteByte(a, v);
        Gbc.CheckAccess(a, v, Mmu.RamType, Mmu.RamMask, true);
    }

    public void CheckInterrupts()
    {
        if ((_ie & _if) == 0)
            return;

        if (_halt)
            PC++;
        _halt = false;

        if (!_ime)
            return;

        _ime = false;
        for (byte i = 0; i < 5; i++)
        {
            byte mask = (byte)(1 << i);
            if ((_ie & mask) != 0 && (_if & mask) != 0)
            {
                _if &= (byte)~mask;
                Tick();
                OpPush(PC);
                PC = 0x40 + (i * 8);
                ReadCycle(PC);
                Tick();
                break;
            }
        }
    }

    public void RequestIE(int v)
    {
        _ie |= v;
    }

    public void RequestIF(int v)
    {
        _if |= v;
        _halt = false;
    }

    //public void Serial(int cycles)
    //{
    //    var maxcycles = 8192;
    //    SerialCounter += cycles;
    //    switch (IO.SC & 1)
    //    {
    //        case 0:
    //            if (Ppu.SpeedMode == 1)
    //                maxcycles *= 2;
    //            break;
    //        case 1:
    //            if (Ppu.SpeedMode == 0)
    //                maxcycles *= 16;
    //            else
    //                maxcycles *= 32;
    //            break;
    //    }
    //    if (SerialCounter >= maxcycles)
    //    {
    //        SerialCounter = 0;
    //        if ((IO.SC & 0x80) != 0)
    //            _if |= IntSerial;
    //    }
    //}

    public void Step()
    {
        if (_halt)
        {
            ReadCycle(PC);
            if (!_halt)
                PC++;

            if (_stop != 0)
            {
                _stop -= 4;
                if (_stop == 0)
                {
                    PC++;
                    _halt = false;
                    Mmu.WriteByte(0xff4d, 0x80);
                }
            }
        }
        else
        {
            int op = ReadCycle(PC++);

            if (op != 0xcb)
            {
                Step00(op);
            }
            else
            {
                op = ReadCycle(PC++);
                StepCB(op);
            }
        }

        if (_imeDelay != 0)
            _imeDelay--;
        else
            CheckInterrupts();
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(AF); bw.Write(BC); bw.Write(DE); bw.Write(HL);
        bw.Write(SP); bw.Write(PC); bw.Write(_ie); bw.Write(_if);
        bw.Write(_sb); bw.Write(_sc); bw.Write(Cycles); bw.Write(_halt);
        bw.Write(_ime); bw.Write(_stop); bw.Write(_imeDelay); bw.Write(StepOverAddr);
    }

    public void Load(BinaryReader br)
    {
        AF = br.ReadInt32(); BC = br.ReadInt32(); DE = br.ReadInt32(); HL = br.ReadInt32();
        SP = br.ReadInt32(); PC = br.ReadInt32(); _ie = br.ReadInt32(); _if = br.ReadInt32();
        _sb = br.ReadInt32(); _sc = br.ReadInt32(); Cycles = br.ReadInt32(); _halt = br.ReadBoolean();
        _ime = br.ReadBoolean(); _stop = br.ReadInt32(); _imeDelay = br.ReadInt32(); StepOverAddr = br.ReadInt32();
    }

    public List<RegisterInfo> GetFlags() =>
    [
        new("","C",$"{(F & FC) != 0}"),
        new("","N",$"{(F & FN) != 0}"),
        new("","H",$"{(F & FH) != 0}"),
        new("","Z",$"{(F & FZ) != 0}"),
    ];

    public List<RegisterInfo> GetRegisters() =>
    [
        new("","AF",$"{AF:X4}"),
        new("","BC",$"{BC:X4}"),
        new("","DE",$"{DE:X4}"),
        new("","HL",$"{HL:X4}"),
        new("","SP",$"{SP:X4}"),
    ];

    public List<RegisterInfo> GetInterruptState() =>
    [
        new("FF0F","IF",""),
        new("0","Vblank", (_if & 0x01) != 0 ? "Enabled" : "Disabled"),
        new("1","LCD", (_if & 0x02) != 0 ? "Enabled" : "Disabled"),
        new("2","Timer", (_if & 0x04) != 0 ? "Enabled" : "Disabled"),
        new("3","Serial", (_if & 0x08) != 0 ? "Enabled" : "Disabled"),
        new("4","Joypad", (_if & 0x10) != 0 ? "Enabled" : "Disabled"),
        new("FFFF","IE",""),
        new("0","Vblank", (_ie & 0x01) != 0 ? "Enabled" : "Disabled"),
        new("1","LCD", (_ie & 0x02) != 0 ? "Enabled" : "Disabled"),
        new("2","Timer", (_ie & 0x04) != 0 ? "Enabled" : "Disabled"),
        new("3","Serial", (_ie & 0x08) != 0 ? "Enabled" : "Disabled"),
        new("4","Joypad", (_ie & 0x10) != 0 ? "Enabled" : "Disabled"),
     ];

    public int GetReg(string reg)
    {
        return reg.ToLowerInvariant() switch
        {
            "af" => AF,
            "bc" => BC,
            "de" => DE,
            "hl" => HL,
            "sp" => SP,
            "pc" => PC,
            _ => 0,
        };
    }

    public void SetReg(string reg, int v)
    {
        switch (reg.ToLowerInvariant())
        {
            case "af": AF = v; break;
            case "bc": BC = v; break;
            case "de": DE = v; break;
            case "hl": HL = v; break;
            case "sp": SP = v; break;
            case "pc": PC = v; break;
        }
    }
}