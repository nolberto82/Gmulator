using Gmulator.Core.Snes;
using Gmulator.Interfaces;
using System.Diagnostics;

namespace Gmulator.Core.Gbc;

public partial class GbcCpu : ICpu, ISaveState
{
    #region State
    public int AF
    {
        get { return (A << 8 | _f) & 0xffff; }
        set { A = (value >> 8) & 0xff; _f = value & 0xff; }
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
    public int F { get => _f & 0xff; set => _f = value & 0xff; }
    public int SP { get => sp & 0xffff; set => sp = value & 0xffff; }
    public int PC { get => pc & 0xffff; set => pc = value & 0xffff; }
    public ulong Cycles { get; set; }
    public int IE { get => _ie; set => _ie = value; }
    public int IF { get => _if; set => _if = value; }
    public int Sb { get => _sb; set => _sb = value; }
    public int Sc { get => _sc; set => _sc = value; }
    public bool Halt { get => _halt; set => _halt = value; }
    public bool Ime { get => _ime; set => _ime = value; }
    public int Stop { get => _stop; set => _stop = value; }
    public int ImeDelay { get => _imeDelay; set => _imeDelay = value; }

    public int A, _f, B, C, D, E, H, L, pc, sp;
    private int _ie;
    private int _if;
    private int _sb;
    private int _sc;

    private bool _halt;
    private bool _ime;
    private int _stop;
    private int _imeDelay;
    #endregion

    public int StepOverAddr { get; set; }

    public static int GetWord(int low, int high) => (ushort)(high << 8 | low);
    public bool FlagZ { get => (AF & FZ) == FZ; }
    public bool FlagN { get => (AF & FN) == FN; }
    public bool FlagH { get => (AF & FH) == FZ; }
    public bool FlagC { get => (AF & FC) == FC; }

    private int Read0F(int a) => IF;
    private int ReadFF(int a) => IE;
    private void Write0F(int a, int v) => IF = v & 0xff;
    private void WriteFF(int a, int v) => IE = v & 0xff;


    private readonly Gbc Gbc;

    public GbcMmu Mmu { get; private set; }

    private List<MemoryHandler> MemoryHandlers;
    public Action Tick { get; set; }


    public GbcCpu(Gbc gbc)
    {
        Gbc = gbc;
        Mmu = gbc.Mmu;
        GenerateOpInfo();

        gbc.CpuMap.Set(0x00, 0x01, 0xff0f, 0x0ff0f, Read0F, Write0F, RamType.Register, 1);
        gbc.CpuMap.Set(0x00, 0x01, 0xffff, 0x0ffff, ReadFF, WriteFF, RamType.Register, 1);
    }

    public void SetAccess(List<MemoryHandler> handlers) => MemoryHandlers = handlers;

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

        Halt = false;
        Ime = false;
        IE = IF = 0;
        Cycles = 0;
        StepOverAddr = -1;
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
        Gbc.Debugger.Access(a, v, MemoryHandlers[a >> 12], false);
        return v;
    }

    public void WriteCycle(int a, int v)
    {
        Tick();
        Mmu.WriteByte(a, v);
        Gbc.Debugger.Access(a, v, MemoryHandlers[a >> 12], true);
    }

    public void CheckInterrupts()
    {
        if ((IE & IF) == 0)
            return;

        if (Halt)
            PC++;
        Halt = false;

        if (!Ime)
            return;

        Ime = false;
        for (byte i = 0; i < 5; i++)
        {
            byte mask = (byte)(1 << i);
            if ((IE & mask) != 0 && (IF & mask) != 0)
            {
                IF &= (byte)~mask;
                Tick();
                OpPush(PC);
                PC = 0x40 + (i * 8);
                ReadCycle(PC);
                Tick();
                break;
            }
        }
    }

    public void RequestIE(int v) => IE |= v;

    public void RequestIF(int v)
    {
        IF |= v;
        Halt = false;
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
        if (Halt)
        {
            ReadCycle(PC);
            if (!Halt)
                PC++;

            if (Stop != 0)
            {
                Stop -= 4;
                if (Stop == 0)
                {
                    PC++;
                    Halt = false;
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

        if (ImeDelay != 0)
            ImeDelay--;
        else
            CheckInterrupts();
    }

    public void Save(BinaryWriter bw)
    {

    }

    public void Load(BinaryReader br)
    {

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
        new("0","Vblank", (IF & 0x01) != 0 ? "Enabled" : "Disabled"),
        new("1","LCD", (IF & 0x02) != 0 ? "Enabled" : "Disabled"),
        new("2","Timer", (IF & 0x04) != 0 ? "Enabled" : "Disabled"),
        new("3","Serial", (IF & 0x08) != 0 ? "Enabled" : "Disabled"),
        new("4","Joypad", (IF & 0x10) != 0 ? "Enabled" : "Disabled"),
        new("FFFF","IE",""),
        new("0","Vblank", (IE & 0x01) != 0 ? "Enabled" : "Disabled"),
        new("1","LCD", (IE & 0x02) != 0 ? "Enabled" : "Disabled"),
        new("2","Timer", (IE & 0x04) != 0 ? "Enabled" : "Disabled"),
        new("3","Serial", (IE & 0x08) != 0 ? "Enabled" : "Disabled"),
        new("4","Joypad", (IE & 0x10) != 0 ? "Enabled" : "Disabled"),
     ];

    public int GetReg(string reg) => reg.ToLowerInvariant() switch
    {
        "af" => AF,
        "bc" => BC,
        "de" => DE,
        "hl" => HL,
        "sp" => SP,
        "pc" => PC,
        _ => 0,
    };

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