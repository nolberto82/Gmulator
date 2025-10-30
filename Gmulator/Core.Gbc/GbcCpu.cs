using Gmulator.Shared;
using System.Diagnostics;

namespace Gmulator.Core.Gbc;

public partial class GbcCpu : EmuState
{
    public int Cycles { get; set; }
    public bool Halt { get; set; }
    public bool IME { get; set; }
    public int Stop { get; private set; }
    public int IMEDelay { get; set; }
    public int SerialCounter { get; private set; }

    public string Error { get; set; }

    public bool FlagZ { get => (F & FZ) == FZ; }
    public bool FlagN { get => (F & FN) == FN; }
    public bool FlagH { get => (F & FH) == FZ; }
    public bool FlagC { get => (F & FC) == FC; }

    public int PC { get => pc & 0xffff; set => pc = value & 0xffff; }
    public int SP { get => sp & 0xffff; set => sp = value & 0xffff; }
    public int PrevPC { get; set; }
    public int StepOverAddr { get; set; } = 1;

    private int A, F, B, C, D, E, H, L, pc, sp;

    public int CyclesInstruction { get; private set; }

    private Func<int, int, RamType, bool, bool> AccessCheck;
    private Action<DebugState> SetState;

    public static int GetWord(int low, int high) => (ushort)(high << 8 | low);
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

    public GbcMmu Mmu { get; private set; }
    public GbcIO IO { get; private set; }
    public Action Tick { get; set; }

    public GbcCpu(Gbc gbc)
    {
        Mmu = gbc.Mmu;
        IO = gbc.IO;
        GenerateOpInfo();
    }

    public void SetAccess(Gbc gbc)
    {
        if (gbc.DebugWindow != null)
            AccessCheck = gbc.DebugWindow.AccessCheck;
        SetState = gbc.SetState;
    }

    public void Reset(bool isbios, bool cgb)
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
        IME = false;

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
        return Mmu.Read(a);
    }

    public void WriteCycle(int a, int v)
    {
        Tick();
        Mmu.Write(a, v);
#if DEBUG || RELEASE
        if (AccessCheck(a, v & 0xff, RamType.Wram, true))
            SetState(DebugState.Break);
#endif
    }

    public void CheckInterrupts()
    {
        byte ie = IO.IE;
        byte @if = IO.IF;
        if ((ie & @if) == 0)
            return;

        if (Halt)
            PC++;
        Halt = false;

        if (!IME)
            return;

        IME = false;
        for (byte i = 0; i < 5; i++)
        {
            byte mask = (byte)(1 << i);
            if ((ie & mask) != 0 && (@if & mask) != 0)
            {
                IO.IF &= (byte)~mask;
                Tick();
                OpPush(PC);
                PC = 0x40 + (i * 8);
                ReadCycle(PC);
                Tick();
                break;
            }
        }
    }

    public void Serial(int cycles)
    {
        var maxcycles = 8192;
        SerialCounter += cycles;
        switch (IO.SC & 1)
        {
            case 0:
                if (IO.SpeedMode == 1)
                    maxcycles *= 2;
                break;
            case 1:
                if (IO.SpeedMode == 0)
                    maxcycles *= 16;
                else
                    maxcycles *= 32;
                break;
        }
        if (SerialCounter >= maxcycles)
        {
            SerialCounter = 0;
            if ((IO.SC & 0x80) != 0)
                IO.IF |= IntSerial;
        }
    }

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
                    Mmu.Write(0xff4d, 0x80);
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

        if (IMEDelay != 0)
            IMEDelay--;
        else
            CheckInterrupts();
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(PC);
        bw.Write(AF);
        bw.Write(BC);
        bw.Write(DE);
        bw.Write(HL);
        bw.Write(SP);
        bw.Write(Halt);
        bw.Write(IME);
        bw.Write(Cycles);
        bw.Write(IMEDelay);
    }

    public override void Load(BinaryReader br)
    {
        PC = br.ReadInt32();
        AF = br.ReadInt32();
        BC = br.ReadInt32();
        DE = br.ReadInt32();
        HL = br.ReadInt32();
        SP = br.ReadInt32();
        Halt = br.ReadBoolean();
        IME = br.ReadBoolean();
        Cycles = br.ReadInt32();
        IMEDelay = br.ReadInt32();
    }

    #region 00 Instructions
    private void OpAdc8(int r1)
    {
        int c = (byte)(F & FC) >> 4;
        int v = A + r1 + c;
        SetF((v & 0xff) == 0, FZ); SetF(false, FN);
        SetF((((A & 0xf) + (r1 & 0xf) + c) & 0x10) != 0, FH);
        SetF(v > 0xff, FC);
        A = (byte)v;
    }

    private void OpAdd8(int r1)
    {
        int v = A + r1;
        SetF((v & 0xff) == 0, FZ); SetF(false, FN);
        SetF(((A & 0xf) + (r1 & 0xf) & 0x10) != 0, FH);
        SetF(v > 0xff, FC);
        A = (byte)v;
    }

    private ushort OpAdd(int r1, int r2)
    {
        int v = r1 + r2;
        SetF(false, FN); SetF(v > 0xffff, FC);
        SetF((((r1 & 0xfff) + (r2 & 0xfff)) & 0x1000) != 0, FH);
        Tick();
        return (ushort)v;
    }

    private ushort OpAddSP(int r1, int r2, bool f8 = false)
    {
        int v = r1 + r2;
        if (!f8)
        {
            Tick(); Tick();
        }
        else
            Tick();

        SetF(false, FZ); SetF(false, FN);
        SetF(((r1 & 0xf) + (r2 & 0xf) & 0x10) != 0, FH);
        SetF((byte)r1 + (byte)r2 > 0xff, FC);
        return (ushort)v;
    }

    private void OpAnd(int r1)
    {
        int v = A & r1;
        SetF(v == 0, FZ); SetF(false, FN);
        SetF(true, FH); SetF(false, FC);

        A = (byte)v;
    }

    private void OpCall(bool flag)
    {
        if (flag)
        {
            OpPush((ushort)(PC + 2));
            PC = OpLdImm16();
        }
        else
        {
            PC += 2;
            Tick();
        }
        Tick();
    }

    private void OpCcf()
    {
        int c = (F ^ FC) & FC;
        SetF(false, FN); SetF(false, FH); SetF(c != 0, FC);
    }

    private void OpCp(int r1)
    {
        int v = A - r1;
        SetF(v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
        SetF(((A & 0xf) - (r1 & 0xf) & 0x10) != 0, FH);
    }

    private void OpCpl()
    {
        int r1 = A ^ 0xff;
        SetF(true, FN); SetF(true, FH);
        A = (byte)r1;
    }

    private void OpDaa()
    {
        int v = A;
        if ((F & FN) != 0)
        {
            if ((F & FH) != 0)
                v -= 6;
            if ((F & FC) != 0)
                v -= 0x60;
        }
        else
        {
            if ((F & FH) != 0 || (A & 0xf) > 9)
                v += 6;
            if ((F & FC) != 0 || A > 0x99)
            {
                v += 0x60;
                SetF(true, FC);
            }
        }

        SetF((v & 0xff) == 0, FZ); SetF(false, FH);

        A = (byte)v;
    }

    private byte OpDec8(int r1)
    {
        int v = r1 - 1;
        SetF((v & 0xff) == 0, FZ); SetF(true, FN);
        SetF((v & 0x0f) == 0x0f, FH);
        return (byte)v;
    }

    private ushort OpDec16(int r1)
    {
        Tick();
        return (ushort)(r1 - 1);
    }

    private void OpDI() => IME = false;

    private void OpEI()
    {
        IMEDelay = 1;
        IME = true;
    }

    private byte OpInc8(int r1)
    {
        int o = r1;
        int v = r1 + 1;
        SetF(false, FN); SetF((o & 0xf) == 0xf, FH);
        SetF((v & 0xff) == 0, FZ);

        return (byte)v;
    }

    private ushort OpInc16(int r1)
    {
        Tick();
        return (ushort)(r1 + 1);
    }

    private void OpJp(bool flag)
    {
        if (flag)
            PC = OpLdImm16();
        else
        {
            PC += 2;
            Tick();
        }
        Tick();
    }

    private void OpJr(bool flag)
    {
        if (flag)
            PC += (ushort)((sbyte)ReadCycle(PC) + 1);
        else
            PC++;
        Tick();
    }

    private ushort OpLdHLSP(int r1, int r2) => OpAddSP(r1, r2, true);
    private int OpLdReg(int a) => ReadCycle(a);
    private int OpLdImm8() => ReadCycle(PC++);
    private int OpLdImm16() => (ReadCycle(PC++) | ReadCycle(PC++) << 8) & 0xffff;
    private void OpLdWr(int a, int v) => WriteCycle(a, (byte)v);

    private void OpLdWr16(int v)
    {
        int a = GetWord(OpLdImm8(), OpLdImm8());
        WriteCycle(a, (byte)v);
        WriteCycle(a + 1, (byte)(v >> 8));
    }

    private void OpOr(int r1)
    {
        int v = A | r1;
        SetF(v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(false, FC);
        A = (byte)v;
    }

    private ushort OpPop(bool af = false)
    {
        int l = ReadCycle(SP++);
        int h = ReadCycle(SP++);

        if (af)
        {
            SetF((l & FZ) != 0, FZ);
            SetF((l & FN) != 0, FN);
            SetF((l & FH) != 0, FH);
            SetF((l & FC) != 0, FC);
            l = F;
        }
        return (ushort)(h << 8 | l);
    }

    public void OpPush(int r1)
    {
        WriteCycle(--SP, (byte)(r1 >> 8));
        WriteCycle(--SP, (byte)(r1 & 0xff));
    }

    private void OpRet(bool flag, bool c3 = false)
    {
        if (flag)
        {
            PC = OpPop();
            if (!c3)
                Tick();
        }
        Tick();
    }

    private void OpReti()
    {
        IME = true;
        PC = OpPop();
        //OpRet(true);
        Tick();
    }

    private byte OpRl(int r1)
    {
        int c = (F & FC) >> 4;
        int v = r1 << 1 | c;

        SetF((byte)v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF((r1 >> 7) != 0, FC);
        return (byte)(v);
    }

    private void OpRla()
    {
        int v = (ushort)(A << 1);
        int oc = (byte)(F & FC) >> 4;
        int c = (byte)(v >> 8);

        SetF(false, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c != 0, FC);
        A = (byte)(v | oc);
    }

    private void OpRlca()
    {
        int v = (ushort)(A << 1);
        int c = (byte)(v >> 8);

        SetF(false, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c != 0, FC);
        A = (byte)(v | c);
    }

    private byte OpRr(int r1)
    {
        int oc = (F & FC) >> 4;
        int v = r1 >> 1 | (oc << 7);

        SetF(v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF((r1 & 1) != 0, FC);
        return (byte)v;
    }

    private void OpRra()
    {
        int oc = (F & FC) >> 4;
        int v = A >> 1;

        SetF(false, FZ); SetF(false, FN);
        SetF(false, FH); SetF((A & 1) != 0, FC);
        A = (byte)(v | (oc << 7));
    }

    private void OpRrca()
    {
        int c = (byte)(A & 1);
        A = (byte)(A >> 1);

        SetF(false, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c != 0, FC);
        A = (byte)(A | (c << 7));
    }

    private void OpRst(ushort r1, bool interrupt = false)
    {
        Tick();
        if (interrupt)
            OpPush(PC);
        else
            OpPush(PC++);
        PC = r1;
    }

    private void OpSbc8(int r1)
    {
        int c = (byte)(F & FC) >> 4;
        int v = A - r1 - c;

        SetF((byte)v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
        SetF((((A & 0xf) - (r1 & 0xf) - c) & 0x10) != 0, FH);
        A = (byte)v;
    }

    private void OpScf()
    {
        SetF(false, FN); SetF(false, FH); SetF(true, FC);
    }

    private void OpSub8(int r1)
    {
        int v = A - r1;

        SetF(v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
        SetF((((A & 0xf) - (r1 & 0xf)) & FH) != 0, FH);
        A = (byte)v;
    }

    private void OpXor(int r1)
    {
        int v = A ^ r1;

        SetF(v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(false, FC);
        A = (byte)v;
    }
    #endregion

    #region CB Instructions
    private void OpBit(int r1, int r2)
    {
        int v = r2 & (1 << r1);
        SetF((v & 0xff) == 0, FZ); SetF(false, FN); SetF(true, FH);
    }

    private byte OpRlc(int r1)
    {
        int c;
        int v = r1;
        c = (byte)(v >> 7);
        v <<= 1;

        SetF(v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c != 0, FC);
        return (byte)(v | c);
    }

    private byte OpRrc(int r1)
    {
        int v = r1;
        int c = (byte)(v & 1);
        v = (v >> 1) | (c << 7);

        SetF(v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c != 0, FC);
        return (byte)v;
    }

    private byte OpSla(int r1)
    {
        int v = r1;
        int c = (byte)(v >> 7);
        v <<= 1;

        SetF((v & 0xff) == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c != 0, FC);
        return (byte)v;
    }

    private byte OpSra(int r1)
    {
        int v = r1;
        int c = (byte)(v & 1);
        v = (v >> 1) | (v & 0x80);

        SetF((v & 0xff) == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c != 0, FC);
        return (byte)v;
    }

    private byte OpSwap(int r1)
    {
        int v = r1;
        int n1;
        int n2;
        (n1, n2) = (v & 0x0f, v >> 4);
        v = (n1 << 4 | n2);

        SetF((byte)v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(false, FC);
        return (byte)v;
    }

    private byte OpSrl(int r1)
    {
        int v = r1;
        int c = (byte)(r1 & 1);
        v >>= 1;

        SetF((v & 0xff) == 0, FZ);
        SetF(false, FN);
        SetF(false, FH);
        SetF(c != 0, FC);
        return (byte)v;
    }

    private static int OpRes(int r1, int r2) => (r2 & ~(1 << r1)) & 0xff;
    private void OpResHL(int r1) => OpLdWr(HL, (OpLdReg(HL) & ~(1 << r1)));
    private static int OpSet(int r1, int r2) => (r2 | (1 << r1)) & 0xff;
    private void OpSetHL(int r1) => OpLdWr(HL, (OpLdReg(HL) | (1 << r1)));
    #endregion

    public List<RegisterInfo> GetFlags() => new()
    {
        new("","C",$"{(F & FC) != 0}"),
        new("","N",$"{(F & FN) != 0}"),
        new("","H",$"{(F & FH) != 0}"),
        new("","Z",$"{(F & FZ) != 0}"),
    };

    public List<RegisterInfo> GetRegisters() => new()
    {
        new("","AF",$"{AF:X4}"),
        new("","BC",$"{BC:X4}"),
        new("","DE",$"{DE:X4}"),
        new("","HL",$"{HL:X4}"),
        new("","SP",$"{SP:X4}"),
    };

    public int GetReg(string reg)
    {
        switch (reg.ToLowerInvariant())
        {
            case "af": return AF;
            case "bc": return BC;
            case "de": return DE;
            case "hl": return HL;
            case "sp": return SP;
            case "pc": return PC;
            default: return 0;
        }
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