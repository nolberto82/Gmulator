using Gmulator.Interfaces;

namespace Gmulator.Core.Gbc;

public partial class GbcCpu : ICpu, ISaveState
{
    #region State
    public int AF
    {
        get { return (_a << 8 | _f) & 0xffff; }
        set { _a = (byte)((value >> 8) & 0xff); _f = (byte)(value & 0xff); }
    }

    public int BC
    {
        get { return (_b << 8 | _c) & 0xffff; }
        set { _b = (byte)((value >> 8) & 0xff); _c = (byte)(value & 0xff); }
    }

    public int DE
    {
        get { return (_d << 8 | _e) & 0xffff; }
        set { _d = (byte)((value >> 8) & 0xff); _e = (byte)(value & 0xff); }
    }

    public int HL
    {
        get { return (_h << 8 | _l) & 0xffff; }
        set { _h = (byte)(value >> 8); _l = (byte)value; }
    }
    public int F { get => _f & 0xff; set => _f = (byte)value; }
    public int SP { get => _sp & 0xffff; set => _sp = value & 0xffff; }
    public int PC { get => _pc & 0xffff; set => _pc = value & 0xffff; }
    public ulong Cycles { get; set; }
    public byte IE { get => _ie; set => _ie = value; }
    public byte IF { get => _if; set => _if = value; }
    public int Sb { get => _sb; set => _sb = (byte)value; }
    public int Sc { get => _sc; set => _sc = (byte)value; }
    public bool Halt { get => _halt; set => _halt = value; }
    public bool Ime { get => _ime; set => _ime = value; }
    public int Stop { get => _stop; set => _stop = value; }
    public int ImeDelay { get => _imeDelay; set => _imeDelay = value; }

    private int _pc, _sp;
    private byte _a, _f, _b, _c, _d, _e, _h, _l;
    private byte _ie;
    private byte _if;
    private byte _sb;
    private byte _sc;

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

    private byte Read0F(int a) => IF;
    private byte ReadFF(int a) => IE;
    private void Write0F(int a, byte v) => IF = v;
    private void WriteFF(int a, byte v) => IE = v;


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

    public byte ReadCycle(int addr)
    {
        Tick();
        byte v = Mmu.ReadByte(addr);
        Gbc.Debugger.Watchpoint(addr, v, MemoryHandlers[addr >> 12], false);
        return v;
    }

    public void WriteCycle(int addr, byte value)
    {
        Tick();
        Mmu.WriteByte(addr, value);
        Gbc.Debugger.Watchpoint(addr, value, MemoryHandlers[addr >> 12], true);
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

    public void RequestIE(int v) => IE |= (byte)v;

    public void RequestIF(int v)
    {
        IF |= (byte)v;
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
        bw.Write(_pc); bw.Write(_sp); bw.Write(_a); bw.Write(_f); bw.Write(_b); bw.Write(_c); bw.Write(_d); bw.Write(_e); bw.Write(_h); bw.Write(_l); bw.Write(_ie); bw.Write(_if);
        bw.Write(_sb); bw.Write(_sc); bw.Write(_halt); bw.Write(_ime);
        bw.Write(_stop); bw.Write(_imeDelay);
    }

    public void Load(BinaryReader br)
    {
        _pc = br.ReadInt32(); _sp = br.ReadInt32(); _a = br.ReadByte(); _f = br.ReadByte(); _b = br.ReadByte(); _c = br.ReadByte(); _d = br.ReadByte(); _e = br.ReadByte(); _h = br.ReadByte(); _l = br.ReadByte(); _ie = br.ReadByte(); _if = br.ReadByte();
        _sb = br.ReadByte(); _sc = br.ReadByte(); _halt = br.ReadBoolean(); _ime = br.ReadBoolean();
        _stop = br.ReadInt32(); _imeDelay = br.ReadInt32();
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

    public void SetReg(string reg, int value)
    {
        switch (reg.ToLowerInvariant())
        {
            case "af": AF = value; break;
            case "bc": BC = value; break;
            case "de": DE = value; break;
            case "hl": HL = value; break;
            case "sp": SP = value; break;
            case "pc": PC = value; break;
        }
    }

    public int[] GetRegValues()
    {
        throw new NotImplementedException();
    }
}