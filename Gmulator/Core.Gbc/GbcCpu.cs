using Gmulator.Shared;

namespace GBoy.Core;

public partial class GbcCpu : SaveState
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

    public ushort PC { get; set; }
    public ushort SP { get; set; }
    public ushort PrevPC { get; set; }
    public int StepOverAddr { get; set; } = 1;

    public byte A, F, B, C, D, E, H, L;

    public int CyclesInstruction { get; private set; }

    public ushort GetWord(byte l, byte h) => (ushort)(l | h << 8);
    public ushort AF
    {
        get { return (ushort)(A << 8 | F); }
        set { A = (byte)(value >> 8); F = (byte)value; }
    }

    public ushort BC
    {
        get { return (ushort)(B << 8 | C); }
        set { B = (byte)(value >> 8); C = (byte)value; }
    }

    public ushort DE
    {
        get { return (ushort)(D << 8 | E); }
        set { D = (byte)(value >> 8); E = (byte)value; }
    }

    public ushort HL
    {
        get { return (ushort)(H << 8 | L); }
        set { H = (byte)(value >> 8); L = (byte)value; }
    }

    public GbcMmu Mmu { get; private set; }
    public GbcIO IO { get; private set; }
    public Action Tick { get; set; }
    public Action<int> SetState { get; internal set; }

    public GbcCpu(GbcMmu mmu, GbcIO io)
    {
        Mmu = mmu;
        IO = io;

        GenerateOpInfo();
    }
    public GbcCpu() { }

    public void Reset(bool isbios, bool cgb, bool debug)
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

    public byte ReadCycle(int a)
    {
        Tick();
        return Mmu.Read(a);
    }

    public void WriteCycle(int a, byte v)
    {
        Tick();
        Mmu.Write(a, v);
    }

    public void CheckInterrupts()
    {
        if ((IO.IE & IO.IF) > 0)
        {
            if (Halt)
                PC++;
            Halt = false;

            if (IME)
            {
                IME = false;
                for (byte i = 0; i < 5; i++)
                {
                    if (IO.IE.GetBit(i) && IO.IF.GetBit(i))
                    {
                        IO.IF &= (byte)(~(1 << i));
                        Tick();
                        OpPush(PC);
                        PC = (ushort)(0x40 + (i * 8));
                        ReadCycle(PC);
                        Tick();
                        return;
                    }
                }
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
            if (IO.SC.GetBit(7))
                IO.IF |= IntSerial;
        }
    }

    public void Step()
    {
        if (Halt)
        {
            byte op = ReadCycle(PC);
            if (!Halt)
                PC++;

            if (Stop > 0)
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
            byte op = ReadCycle(PC++);

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

        if (IMEDelay > 0)
            IMEDelay--;
        else
            CheckInterrupts();
    }

    public Dictionary<string, bool> GetFlags() => new()
    {
        ["C"] = F.GetBit(4),
        ["N"] = F.GetBit(5),
        ["H"] = F.GetBit(6),
        ["Z"] = F.GetBit(7),
    };

    public Dictionary<string, byte> GetRegs() => new()
    {
        ["A:"] = A,
        ["B:"] = B,
        ["C:"] = C,
        ["D:"] = D,
        ["E:"] = E,
        ["H:"] = H,
        ["L:"] = L,
    };

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
        PC = br.ReadUInt16();
        AF = br.ReadUInt16();
        BC = br.ReadUInt16();
        DE = br.ReadUInt16();
        HL = br.ReadUInt16();
        SP = br.ReadUInt16();
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
        SetF((((A & 0xf) + (r1 & 0xf) + c) & 0x10) > 0, FH);
        SetF(v > 0xff, FC);
        A = (byte)v;
    }

    private ushort OpAdc(int r1, int r2)
    {
        int c = F & FC;
        int v = r1 + r2 + c;
        SetF((v & 0xffff) == 0, FZ); SetF(false, FN);
        SetF((((r1 & 0xfff) + (r2 & 0xfff) + c) & 0x1000) > 0, FH);
        SetF(v > 0xffff, FC);
        return (ushort)v;
    }

    private void OpAdd8(int r1)
    {
        int v = A + r1;
        SetF((v & 0xff) == 0, FZ); SetF(false, FN);
        SetF(((A & 0xf) + (r1 & 0xf) & 0x10) > 0, FH);
        SetF(v > 0xff, FC);
        A = (byte)v;
    }

    private ushort OpAdd(int r1, int r2)
    {
        int v = r1 + r2;
        SetF(false, FN); SetF(v > 0xffff, FC);
        SetF((((r1 & 0xfff) + (r2 & 0xfff)) & 0x1000) > 0, FH);
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
        SetF(((r1 & 0xf) + (r2 & 0xf) & 0x10) > 0, FH);
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
        SetF(false, FN); SetF(false, FH); SetF(c > 0, FC);
    }

    private void OpCp(int r1)
    {
        int v = A - r1;
        SetF(v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
        SetF(((A & 0xf) - (r1 & 0xf) & 0x10) > 0, FH);
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
        if ((F & FN) > 0)
        {
            if ((F & FH) > 0)
                v -= 6;
            if ((F & FC) > 0)
                v -= 0x60;
        }
        else
        {
            if ((F & FH) > 0 || (A & 0xf) > 9)
                v += 6;
            if ((F & FC) > 0 || A > 0x99)
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
        int o = r1;
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

    private void OpDecHL(int r1, int r2)
    {
        ushort a = GetAddr(r1, r2);
        WriteCycle(a, OpDec8(ReadCycle(a)));
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

    private void OpIncHL(int r1, int r2)
    {
        ushort a = GetAddr(r1, r2);
        WriteCycle(a, OpInc8(ReadCycle(a)));
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
    private byte OpLdReg(int a) => ReadCycle(a);
    private byte OpLdImm8() => ReadCycle(PC++);
    private ushort OpLdImm16() => (ushort)(ReadCycle(PC++) | ReadCycle(PC++) << 8);
    private void OpLdWr(int a, int v) => WriteCycle(a, (byte)v);

    private void OpLdWr16(int v)
    {
        ushort a = GetWord(OpLdImm8(), OpLdImm8());
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
        int h = 0, l = 0;
        l = ReadCycle(SP++);
        h = ReadCycle(SP++);

        if (af)
        {
            SetF((l & FZ) > 0, FZ);
            SetF((l & FN) > 0, FN);
            SetF((l & FH) > 0, FH);
            SetF((l & FC) > 0, FC);
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
        SetF(false, FH); SetF((r1 >> 7) > 0, FC);
        return (byte)(v);
    }

    private void OpRla()
    {
        int v = (ushort)(A << 1);
        int oc = (byte)(F & FC) >> 4;
        int c = (byte)(v >> 8);

        SetF(false, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c > 0, FC);
        A = (byte)(v | oc);
    }

    private void OpRlca()
    {
        int v = (ushort)(A << 1);
        int c = (byte)(v >> 8);

        SetF(false, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c > 0, FC);
        A = (byte)(v | c);
    }

    private byte OpRr(int r1)
    {
        int oc = (F & FC) >> 4;
        int v = r1 >> 1 | (oc << 7);

        SetF(v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF((r1 & 1) > 0, FC);
        return (byte)v;
    }

    private void OpRra()
    {
        int oc = (F & FC) >> 4;
        int v = A >> 1;

        SetF(false, FZ); SetF(false, FN);
        SetF(false, FH); SetF((A & 1) > 0, FC);
        A = (byte)(v | (oc << 7));
    }

    private void OpRrca()
    {
        int c = (byte)(A & 1);
        int v = A = (byte)(A >> 1);

        SetF(false, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c > 0, FC);
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
        SetF((((A & 0xf) - (r1 & 0xf) - c) & 0x10) > 0, FH);
        A = (byte)v;
    }

    private ushort OpSbc(int r1, int r2)
    {
        int cf = (byte)(F & 1);
        int v = r1 - r2 - cf;

        SetF(v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
        SetF((((r1 & 0xfff) - (r2 & 0xfff) - cf) & 0x1000) > 0, FH);
        return (ushort)v;
    }

    private void OpScf()
    {
        SetF(false, FN); SetF(false, FH); SetF(true, FC);
    }

    private void OpSub8(int r1)
    {
        int o = A;
        int v = A - r1;

        SetF(v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
        SetF((((A & 0xf) - (r1 & 0xf)) & FH) > 0, FH);
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
    private void OpBit(int r1, int r2, int addr = -1)
    {
        int v = r2 & (1 << r1);

        SetF((v & 0xff) == 0, FZ); SetF(false, FN); SetF(true, FH);
    }

    private byte OpRlc(int r1)
    {
        int c;
        int v = r1;
        c = (byte)(v >> 7);
        v = v << 1;

        SetF(v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c > 0, FC);
        return (byte)(v | c);
    }

    private byte OpRrc(int r1)
    {
        int v = r1;
        int c = (byte)(v & 1);
        v = (v >> 1) | (c << 7);

        SetF(v == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c > 0, FC);
        return (byte)v;
    }

    private byte OpSla(int r1)
    {
        int v = r1;
        int c = (byte)(v >> 7);
        v <<= 1;

        SetF((v & 0xff) == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c > 0, FC);
        return (byte)v;
    }

    private byte OpSra(int r1)
    {
        int v = r1;
        int c = (byte)(v & 1);
        v = (v >> 1) | (v & 0x80);

        SetF((v & 0xff) == 0, FZ); SetF(false, FN);
        SetF(false, FH); SetF(c > 0, FC);
        return (byte)v;
    }

    private byte OpSwap(int r1)
    {
        int n1 = 0, n2 = 0;
        int v = r1;
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
        v = (v >> 1);

        SetF((v & 0xff) == 0, FZ);
        SetF(false, FN);
        SetF(false, FH);
        SetF(c > 0, FC);
        return (byte)v;
    }

    private byte OpRes(int r1, byte r2) => (byte)(r2 & ~(1 << r1));
    private void OpResHL(int r1) => OpLdWr(HL, (OpLdReg(HL) & ~(1 << r1)));
    private byte OpSet(int r1, byte r2) => (byte)(r2 | (1 << r1));
    private void OpSetHL(int r1) => OpLdWr(HL, (OpLdReg(HL) | (1 << r1)));
    private ushort GetAddr(int a1, int a2) => (ushort)(a1 + ReadCycle((ushort)a2));
    #endregion
}