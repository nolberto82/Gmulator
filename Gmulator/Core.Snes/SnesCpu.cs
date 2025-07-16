namespace Gmulator.Core.Snes;
public partial class SnesCpu : EmuState, ICpu
{
    private const int FC = 1 << 0;
    private const int FZ = 1 << 1;
    private const int FI = 1 << 2;
    private const int FD = 1 << 3;
    private const int FX = 1 << 4;
    private const int FM = 1 << 5;
    private const int FV = 1 << 6;
    private const int FN = 1 << 7;
    private const int COPn = 0xFFE4;
    private const int BRKn = 0xFFE6;
    private const int IRQn = 0xFFEE;
    private const int NMIn = 0xFFEA;
    private const int COPe = 0xFFF4;
    private const int RESETe = 0xFFFC;
    private const int BRKe = 0xFFFE;

    private int pc, sp, ra, rx, ry, ps;
    private bool Imme, Wait;

    public int PC { get => (ushort)pc; set => pc = (ushort)value; }
    public int SP { get => (ushort)sp; set => sp = (ushort)value; }
    public int A { get => (ushort)ra; set => ra = (ushort)value; }
    public int X
    {
        get => !E && !XMem ? (ushort)rx : (byte)rx;
        set => rx = (ushort)value;
    }
    public int Y
    {
        get => !E && !XMem ? (ushort)ry : (byte)ry;
        set => ry = (ushort)value;
    }
    public int PS { get => (byte)ps; set => ps = (byte)value; }
    public int PB { get; set; }
    public int DB { get; set; }
    public bool E { get; set; }
    public int D { get; private set; }
    public bool FastMem { get; set; }
    public byte OpenBus { get; set; }
    private bool NmiEnabled;
    private bool IRQEnabled;
    public int StepOverAddr;
    public int Cycles { get; set; }

    public bool XMem { get => ps.GetBit(4); }
    public bool MMem { get => ps.GetBit(5); }
    private int PBR { get => PB << 16; }

    private Snes Snes;
    public int TestAddr { get; set; }
    private int C { get => PS & FC; }
    private bool I { get => (PS & FI) == FI; }
    public int Instructions { get; private set; }
    public Action<int> SetState;

    public SnesCpu() => CreateOpcodes();

    public void SetSnes(Snes snes) => Snes = snes;

    private void Idle()
    {
        Snes.HandleDma();
        Snes.Ppu?.Step(6);
    }

    public void Idle8() => Snes?.Ppu.Step(8);

    public byte Read(int a)
    {
        Cycles++;
        var c = GetClockSpeed(a);
        Snes?.Ppu.Step(c);
        Snes?.HandleDma();
        return OpenBus = Snes?.ReadMemory(a) ?? 0;
    }

    public void Write(int a, int v)
    {
        Cycles++;
        var c = GetClockSpeed(a);
        Snes?.Ppu.Step(c);
        Snes?.HandleDma();
        Snes?.WriteMemory(a, v);

    }

    private int GetClockSpeed(int a)
    {
        var bank = a >> 16;
        a &= 0xffff;
        if (bank < 0x40)
        {
            if (a < 0x2000)
                return 8;
            else if (a < 0x4000)
                return 6;
            if (a < 0x4200)
                return 12;
            if (a < 0x6000)
                return 6;
            if (a < 0x8000)
                return 8;
        }
        else if (bank < 0x80)
            return 8;
        return FastMem && bank >= 0x80 ? 6 : 8;
    }

    private ushort ReadWord(int a) => (ushort)(Read(a) | Read(a + 1) << 8);

    private int ReadLong(int a) => (Read(a) | Read(a + 1) << 8) | Read(a + 2) << 16;

    private void WriteWord(int a, int v)
    {
        Write(a, (byte)v);
        Write(a + 1, v >> 8);
    }

    private byte GetGet8bitImm(int a)
    {
        if (Imme) return (byte)a;
        else return Read(a);
    }

    private ushort GetGet16bitImm(int a)
    {
        if (Imme) return (ushort)a;
        else return ReadWord(a);
    }

    private static void SetRegValue(ref int r, int v, bool bitmode)
    {
        if (bitmode)
            r = (r & 0xff00) | v;
        else
            r = v;
    }

    private void WrapSp()
    {
        if (E)
            SP = 0x100 | (SP & 0xff);
    }

    private void SetSp(int s, bool e)
    {
        if (e && E)
            SP = 0x100 | (s & 0xff);
        else
            SP = s;
    }

    public void SetNmi() => NmiEnabled = true;

    public void SetIRQ() => IRQEnabled = true;

    public virtual void Step()
    {
        if (NmiEnabled)
        {
            Nmi(NMIn);
            NmiEnabled = Wait = false;

            return;
        }
        if (!I && IRQEnabled)
        {
            Nmi(IRQn);
            IRQEnabled = Wait = false;
            return;
        }

        var op = Read(PBR | PC++);
        ExecOp(op);
    }

    public void ExecOp(byte op)
    {
        int mode = Disasm[op].Mode;
        Imme = Disasm[op].Immediate;

        switch (Disasm[op].Id)
        {
            case ADC: Adc(GetMode(mode)); break;
            case AND: And(GetMode(mode)); break;
            case ASL: Asl(GetMode(mode), mode); break;
            case BCC: Brn(mode, 0); break;
            case BCS: Brp(mode, 0); break;
            case BEQ: Brp(mode, 1); break;
            case BIT: Bit(GetMode(mode), mode); break;
            case BMI: Brp(mode, 7); break;
            case BNE: Brn(mode, 1); break;
            case BPL: Brn(mode, 7); break;
            case BRA: Bra(mode); break;
            case BRK: Brk(); break;
            case BRL: Bra(mode); break;
            case BVC: Brn(mode, 6); break;
            case BVS: Brp(mode, 6); break;
            case CLC: GetMode(mode); PS &= ~FC; Idle(); break;
            case CLD: GetMode(mode); PS &= ~FD; Idle(); break;
            case CLI: GetMode(mode); PS &= ~FI; Idle(); break;
            case CLV: GetMode(mode); PS &= ~FV; Idle(); break;
            case CMP: Cmp(GetMode(mode)); break;
            case COP: Cop(); break;
            case CPX: Cpx(GetMode(mode)); break;
            case CPY: Cpy(GetMode(mode)); break;
            case DEC: Dec(GetMode(mode), mode); break;
            case DEX: Dex(GetMode(mode)); Idle(); break;
            case DEY: Dey(GetMode(mode)); Idle(); break;
            case EOR: Eor(GetMode(mode)); break;
            case INC: Inc(GetMode(mode), mode); break;
            case INX: Inx(GetMode(mode)); Idle(); break;
            case INY: Iny(GetMode(mode)); Idle(); break;
            case JML: Jml(GetMode(mode), mode); break;
            case JMP: Jmp(GetMode(mode), mode); break;
            case JSR: Jsr(GetMode(mode), mode); break;
            case JSL: Jsl(GetMode(mode), mode); break;
            case LDA: Lda(GetMode(mode)); break;
            case LDX: Ldx(GetMode(mode)); break;
            case LDY: Ldy(GetMode(mode)); break;
            case LSR: Lsr(GetMode(mode), mode); break;
            case MVN: Mvn(); break;
            case MVP: Mvp(); break;
            case NOP: Idle(); break;
            case ORA: Ora(GetMode(mode)); break;
            case PEA: Pea(GetMode(mode)); break;
            case PEI: Pei(GetMode(mode)); break;
            case PER: Per(GetMode(mode)); break;
            case PHA: Pha(); break;
            case PHB: Push((byte)DB); break;
            case PHD: Phd(); break;
            case PHK: Phk(); break;
            case PHP: Php(); break;
            case PHX: PushX(X); break;
            case PHY: PushX(Y); break;
            case PLA: Pla(); break;
            case PLB: Plb(); break;
            case PLD: Pld(); break;
            case PLP: Plp(); break;
            case PLX: Plx(); break;
            case PLY: Ply(); break;
            case REP: Rep(GetMode(mode)); break;
            case ROL: Rol(GetMode(mode), mode); break;
            case ROR: Ror(GetMode(mode), mode); break;
            case RTI: Rti(); break;
            case RTL: Rtl(); break;
            case RTS: Rts(); break;
            case SBC: Sbc(GetMode(mode)); break;
            case SEC: PS |= FC; Idle(); break;
            case SED: PS |= FD; Idle(); break;
            case SEI: GetMode(mode); PS |= FI; Idle(); break;
            case SEP: Sep(GetMode(Immediate)); break;
            case STA: Sta(GetMode(mode)); break;
            case STP: GetMode(mode, true); Idle(); Idle(); break;
            case STX: Stx(GetMode(mode)); break;
            case STY: Sty(GetMode(mode)); break;
            case STZ: Stz(GetMode(mode)); break;
            case TAX: GetMode(mode); Tax(); break;
            case TAY: GetMode(mode); Tay(); break;
            case TCD: GetMode(mode); Tcd(); break;
            case TCS: GetMode(mode); Tcs(); break;
            case TDC: GetMode(mode); Tdc(); break;
            case TRB: Trb(GetMode(mode)); break;
            case TSB: Tsb(GetMode(mode)); break;
            case TSC: GetMode(mode); Tsc(); break;
            case TSX: GetMode(mode); Tsx(); break;
            case TXA: GetMode(mode); Txa(); break;
            case TXS: GetMode(mode); Txs(); break;
            case TXY: GetMode(mode); Txy(); break;
            case TYA: GetMode(mode); Tya(); break;
            case TYX: GetMode(mode); Tyx(); break;
            case WAI: GetMode(mode, true); Idle(); Idle(); Wait = true; break;
            case WDM: PC++; break;
            case XBA: GetMode(mode, true); Xba(); break;
            case XCE: GetMode(mode, true); Xce(); break;
        }
    }

    private void Adc(int a)
    {
        int v, b;
        if (MMem)
        {
            v = GetGet8bitImm(a);
            if (PS.GetBit(3))
            {
                b = (ushort)((A & 0xf) + (v & 0xf) + (PS & FC));
                if (b > 0x09) b += 0x06;
                SetFlagC(b >= 0x10);
                b = (ushort)((A & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf));
            }
            else
                b = (ushort)((byte)A + v + (PS & FC));

            SetFlagV((~((byte)A ^ v) & ((byte)A ^ b) & 0x80) == 0x80);
            if (PS.GetBit(3) && (byte)b > 0x9f) b += 0x60;

            SetFlagC(b >= 0x100);
            SetRegValue(ref ra, (byte)b, MMem);
        }
        else
        {
            v = GetGet16bitImm(a);
            if (PS.GetBit(3))
            {
                b = (A & 0xf) + (v & 0xf) + (PS & FC);
                if (b > 0x09) b += 0x06;
                SetFlagC(b >= 0x10);
                b = (A & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf);
                if (b > 0x9f) b += 0x60;
                b = (A & 0xf00) + (v & 0xf00) + (b & 0x100) + (b & 0xff);
                if (b > 0x9ff) b += 0x600;
                b = (A & 0xf000) + (v & 0xf000) + (b & 0x1000) + (b & 0xfff);
            }
            else
                b = A + v + (PS & FC);

            SetFlagV((~(A ^ v) & (A ^ b) & 0x8000) == 0x8000);
            if (PS.GetBit(3) && b > 0x9fff) b += 0x6000;

            SetFlagC(b >= 0x10000);

            A = (ushort)b;
        }
        SetFlagZN(b, MMem);
    }

    private void And(int a)
    {
        int v;
        if (MMem)
        {
            v = A & GetGet8bitImm(a);
            SetRegValue(ref ra, v, MMem);
        }
        else
            A &= GetGet16bitImm(a);
        SetFlagZN(A, MMem);
    }

    private void Asl(int a, int mode)
    {
        int b;
        if (mode == Accumulator)
        {
            if (MMem)
            {
                SetFlagC((A & 0x80) > 0);
                b = (A << 1) & 0xfe;
            }
            else
            {
                SetFlagC((A & 0x8000) > 0);
                b = (A << 1) & 0xfffe;
            }
            SetRegValue(ref ra, b, MMem);
        }
        else
        {
            if (MMem)
            {
                b = Read(a);
                SetFlagC((b & 0x80) > 0);
                b = (b << 1) & 0xfe;
                Write(a, b);
            }
            else
            {
                b = ReadWord(a);
                SetFlagC((b & 0x8000) == 0x8000);
                b = (b << 1) & 0xfffe;
                WriteWord(a, b);
                Idle(); Idle();
            }
        }
        SetFlagZN(b, MMem);
    }

    private void Brp(int m, int f)
    {
        if (PS.GetBit(f))
        {
            Bra(m);
            Idle();
        }
        else
        {
            PC++;
            Read(PC);
        }
    }

    private void Brn(int m, int f)
    {
        if (!PS.GetBit(f))
        {
            Bra(m);
            Idle();
        }
        else
        {
            PC++;
            Read(PC);
        }

    }

    private void Bra(int m)
    {
        PC = GetMode(m);
        if (E)
            Idle();
    }

    private void Bit(int a, int mode)
    {
        int v;
        int b;
        if (Imme)
        {
            if (MMem)
            {
                b = GetGet8bitImm(a);
                v = (byte)A & b;
                PS = (byte)v == 0 ? PS |= FZ : PS &= ~FZ;
            }
            else
            {
                b = GetGet16bitImm(a);
                v = A & b;
                PS = (ushort)v == 0 ? PS |= FZ : PS &= ~FZ;
            }
        }
        else
        {
            if (MMem)
            {
                b = Read(a);
                v = (byte)A & b;
                PS = (byte)v == 0 ? PS |= FZ : PS &= ~FZ;
                PS = (b & FN) == FN ? PS |= FN : PS &= ~FN;
                SetFlagV((b & FV) == FV);
            }
            else
            {
                b = ReadWord(a);
                v = A & b;
                PS = (ushort)v == 0 ? PS |= FZ : PS &= ~FZ;
                PS = (b & 0x8000) == 0x8000 ? PS |= FN : PS &= ~FN;
                SetFlagV((b & 0x4000) == 0x4000);
                Idle();
            }
        }
    }

    private void Cmp(int a)
    {
        int v, r;
        if (MMem)
            v = GetGet8bitImm(a);
        else
            v = GetGet16bitImm(a);

        r = A - v;
        SetFlagC(MMem ? (byte)A >= v : A >= v);
        SetFlagZN(r, MMem);
    }

    private void Cpx(int a)
    {
        int v, r;
        if (XMem)
            v = GetGet8bitImm(a);
        else
            v = GetGet16bitImm(a);

        r = X - v;
        SetFlagC(X >= v);
        SetFlagZN(r, XMem);
    }

    private void Cpy(int a)
    {
        int v, r;
        if (XMem)
            v = GetGet8bitImm(a);
        else
            v = GetGet16bitImm(a);

        r = Y - v;
        SetFlagC(Y >= v);
        SetFlagZN(r, XMem);
    }

    private void Dec(int a, int mode)
    {
        int b;
        if (mode == Accumulator)
        {
            if (MMem)
                b = (byte)(A - 1);
            else
                b = A - 1;
            SetRegValue(ref ra, b, MMem);
            SetFlagZN(A, MMem);
        }
        else
        {
            if (MMem)
            {
                b = (byte)(Read(a) - 1);
                Write(a, b);
            }
            else
            {
                b = (ushort)(ReadWord(a) - 1);
                WriteWord(a, b);
            }
            SetFlagZN(b, MMem);
        }
    }

    private void Dex(int v)
    {
        X--;
        if (XMem)
        {
            X &= 0xff;
        }

        SetFlagZN(X, XMem);
    }

    private void Dey(int v)
    {
        Y--;
        SetFlagZN(Y, XMem);
    }

    private void Eor(int v)
    {
        if (Imme)
        {
            v = A ^ v;
            SetRegValue(ref ra, v, MMem);
        }
        else
        {
            if (MMem)
                SetRegValue(ref ra, A ^ Read(v), MMem);
            else
                SetRegValue(ref ra, A ^ ReadWord(v), MMem);
        }
        SetFlagZN(A, MMem);
    }

    private void Inc(int a, int mode)
    {
        if (mode == Accumulator)
        {
            if (MMem)
            {
                int v = A + 1;
                A = (A & 0xff00) | (byte)v;
            }
            else
                A++;
            SetFlagZN(A, MMem);
        }
        else
        {
            int b;
            if (MMem)
            {
                b = (byte)(Read(a) + 1);
                Write(a, b);
            }
            else
            {
                b = (ushort)(ReadWord(a) + 1);
                WriteWord(a, b);
            }
            SetFlagZN(b, MMem);
        }
    }

    private void Inx(int v)
    {
        X++;
        SetFlagZN(X, XMem);
    }

    private void Iny(int v)
    {
        Y++;
        SetFlagZN(Y, XMem);
    }

    private void Jml(int v, int mode)
    {

        if (mode == AbsoluteLong)
        {
            PB = v >> 16;
            PC = (ushort)v;
        }
        else if (mode == AbsoluteIndirectLong)
        {
            var a = ReadLong(v);
            PC = (short)a;
            PB = a >> 16;
        }
    }

    private void Jmp(int v, int mode)
    {
        if (mode == Absolute)
            PC = v;
        else if (mode == AbsoluteIndirectLong)
        {
            var a = ReadLong(v);
            PC = a;
            PB = a >> 16;
        }
        else
        {
            var a = ReadWord(v);
            PC = a;
        }
    }

    private void Jsr(int v, int mode)
    {
        PC--;
        if (mode == Absolute || mode == AbsoluteLong)
        {
            PushWord(PC, true);
            PC = PBR | v;
        }
        else
        {
            PushWord(PC);
            PC = ReadWord(v);
            WrapSp();
        }
    }

    private void Jsl(int v, int mode)
    {
        PC--;
        Push((byte)PB);
        Push((byte)(PC >> 8));
        Push((byte)PC);
        PC = v;
        PB = v >> 16;
        WrapSp();
    }

    private void Lda(int v)
    {
        if (Imme)
            SetRegValue(ref ra, v, MMem);
        else
        {
            if (MMem)
                SetRegValue(ref ra, Read(v), MMem);
            else
                SetRegValue(ref ra, ReadWord(v), MMem);
        }
        SetFlagZN(A, MMem);
    }

    private void Ldx(int v)
    {
        if (Imme)
            SetRegValue(ref rx, v, XMem);
        else
        {
            if (XMem)
                SetRegValue(ref rx, Read(v), XMem);
            else
                SetRegValue(ref rx, ReadWord(v), XMem);
        }
        SetFlagZN(X, XMem);
    }

    private void Ldy(int v)
    {
        if (Imme)
            SetRegValue(ref ry, v, XMem);
        else
        {
            if (XMem)
                SetRegValue(ref ry, Read(v), XMem);
            else
                SetRegValue(ref ry, ReadWord(v), XMem);
        }
        SetFlagZN(Y, XMem);
    }

    private void Lsr(int a, int mode)
    {
        int b;
        if (mode == Accumulator)
        {
            SetFlagC(((byte)A & 0x01) > 0);
            if (MMem)
                b = (byte)((A >> 1) & 0x7f);
            else
                b = (A >> 1) & 0x7fff;
            SetRegValue(ref ra, b, MMem);
        }
        else
        {
            if (MMem)
            {
                b = Read(a);
                SetFlagC((b & 0x01) > 0);
                b = (byte)((b >> 1) & 0x7f);
                Write(a, b);
            }
            else
            {
                b = ReadWord(a);
                SetFlagC((b & 0x01) > 0);
                b = (ushort)((b >> 1) & 0x7fff);
                WriteWord(a, b);
            }
        }
        SetFlagZN(b, MMem);
    }

    private void Mvn()
    {
        var src = Read(PBR | PC + 1);
        var dst = Read(PBR | PC);
        DB = dst;
        var v = Read(src << 16 | X);
        Write(dst << 16 | Y, v);
        A--;
        int b = XMem ? (byte)(X + 1) : X + 1;
        SetRegValue(ref rx, b, XMem);
        b = XMem ? (byte)(Y + 1) : Y + 1;
        SetRegValue(ref ry, b, XMem);
        if (A != 0xffff)
            PC--;
        else
            PC += 2;
        Idle();
    }

    private void Mvp()
    {
        var src = Read(PBR | PC + 1);
        var dst = Read(PBR | PC);
        DB = dst;
        var v = Read(src << 16 | X);
        Write(dst << 16 | Y, v);
        A--;
        int b = XMem ? (byte)(X - 1) : X - 1;
        SetRegValue(ref rx, b, XMem);
        b = XMem ? (byte)(Y - 1) : Y - 1;
        SetRegValue(ref ry, b, XMem);
        if (A != 0xffff)
            PC--;
        else
            PC += 2;
        Idle();
    }

    public void Nmi(int type)
    {
        if (!E)
        {
            Push((byte)PB);
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            PC = ReadWord(type);
            PB = 0;
            PS |= FI;
            Idle();
        }
        else
        {
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            PC = ReadWord(BRKe);
            PB = 0;
        }
    }

    private void Ora(int v)
    {
        if (Imme)
        {
            v = A | v;
            SetRegValue(ref ra, v, MMem);
        }
        else
        {
            if (MMem)
                SetRegValue(ref ra, A | Read(v), MMem);
            else
                SetRegValue(ref ra, A | ReadWord(v), MMem);
        }
        SetFlagZN(A, MMem);
    }

    private void Pea(int v)
    {
        PushWord(v);
        WrapSp();
    }

    private void Pei(int v)
    {
        PushWord(ReadWord(v));
        WrapSp();
    }

    private void Per(int v)
    {
        PushWord(PC + v);
        WrapSp();
    }

    private void Pha()
    {
        PushM(A);
        WrapSp();
    }

    private void Phd()
    {
        PushWord(D);
        WrapSp();
    }

    private void Phk()
    {
        Push((byte)PB);
        WrapSp();
    }

    private void Php()
    {
        Push((byte)PS);
        WrapSp();
    }

    private void Pla()
    {
        if (MMem)
            SetRegValue(ref ra, Pop(true), MMem);
        else
            SetRegValue(ref ra, PopWord(), MMem);
        SetFlagZN(A, MMem);
    }

    private void Pld()
    {
        D = PopWord();
        SetFlagZN(D, false);
        WrapSp();
    }

    private void Plx()
    {
        if (XMem)
            SetRegValue(ref rx, Pop(true), XMem);
        else
            SetRegValue(ref rx, PopWord(), XMem);
        SetFlagZN(X, XMem);
    }

    private void Ply()
    {
        if (XMem)
            SetRegValue(ref ry, Pop(true), XMem);
        else
            SetRegValue(ref ry, PopWord(), XMem);
        SetFlagZN(Y, XMem);
    }

    private void Plb()
    {
        DB = Pop();
        SetFlagZN(DB, true);
        WrapSp();
        Idle(); Idle();
    }

    private void Plp()
    {
        if (E)
            PS = Pop(true) | 0x30;
        else
            PS = Pop();

        if (XMem)
        {
            X &= 0xff;
            Y &= 0xff;
        }
        WrapSp();
    }

    private void Rep(int v)
    {
        PS &= (byte)~v;
        if (E)
            PS |= FX | FM;
    }

    private void Rol(int a, int mode)
    {
        int msb;
        int b;
        if (mode == Accumulator)
        {
            if (MMem)
            {
                msb = A & 0x80;
                b = (byte)A;
                b <<= 1;
                if ((PS & FC) > 0)
                    b |= 0x01;
                SetRegValue(ref ra, (byte)b, MMem);
            }
            else
            {
                msb = A & 0x8000;
                A <<= 1;
                if ((PS & FC) == FC)
                    A |= 0x01;
                b = (ushort)A;
            }
        }
        else
        {
            if (MMem)
            {
                b = Read(a);
                msb = b & 0x80;
                b = (byte)(b << 1);
                if ((PS & FC) > 0)
                    b |= 0x01;
                Write(a, b);
            }
            else
            {
                b = ReadWord(a);
                msb = b & 0x8000;
                b = (ushort)(b << 1);
                if ((PS & FC) == FC)
                    b |= 0x01;
                WriteWord(a, b);
            }
        }
        SetFlagC(msb > 0);
        SetFlagZN(b, MMem);
    }

    private void Ror(int a, int mode)
    {
        int bit0;

        int b;
        if (mode == Accumulator)
        {
            if (MMem)
            {
                bit0 = A & 0x01;
                b = (byte)(A) >> 1;
                if ((PS & FC) > 0)
                    b |= 0x80;
                SetRegValue(ref ra, (byte)b, MMem);
            }
            else
            {
                bit0 = A & 0x01;
                A >>= 1;
                if ((PS & FC) == FC)
                    A |= 0x8000;
                b = A;
            }
        }
        else
        {
            if (MMem)
            {
                b = Read(a);
                bit0 = b & 0x01;
                b = (byte)(b >> 1);
                if ((PS & FC) > 0)
                    b |= 0x80;
                Write(a, b);
            }
            else
            {
                b = ReadWord(a);
                bit0 = b & 0x01;
                b = (ushort)(b >> 1);
                if ((PS & FC) > 0)
                    b |= 0x8000;
                WriteWord(a, b);
            }

        }
        SetFlagC(bit0 > 0);
        SetFlagZN(b, MMem);
    }

    private void Rti()
    {
        if (E)
        {
            PS = Pop(true) | 0x30;
            PC = PopWord();
        }
        else
        {
            PS = Pop();
            PC = PopWord();
            PB = Pop();
        }
    }

    private void Rtl()
    {
        var v = PBR | PopWord();
        PB = Pop();
        PC = v | PBR;
        PC++;
        WrapSp();
    }

    private void Rts()
    {
        PC = Pop(true);
        PC |= Pop(true) << 8;
        PC |= PBR;
        PC++;
    }

    private void Sep(int v)
    {
        PS |= v;
        if (E)
            PS |= FX | FM;

        if (XMem)
        {
            X &= (byte)X;
            Y &= (byte)Y;
        }
    }

    private void Sbc(int a)
    {
        int v, b;
        if (MMem)
        {
            v = (byte)~GetGet8bitImm(a);
            if (PS.GetBit(3))
            {
                b = (ushort)((A & 0xf) + (v & 0xf) + C);
                if (b < 0x10) b -= 0x06;
                SetFlagC(b >= 0x10);
                b = (ushort)((A & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf));
            }
            else
                b = (byte)A + v + C;

            SetFlagV((~((byte)A ^ v) & ((byte)A ^ b) & 0x80) == 0x80);
            if (PS.GetBit(3) && b < 0x100) b -= 0x60;

            SetFlagC(b > 0xff);
            SetRegValue(ref ra, (byte)b, MMem);
        }
        else
        {
            v = (ushort)~GetGet16bitImm(a);
            if (PS.GetBit(3))
            {
                b = (A & 0xf) + (v & 0xf) + C;
                if (b < 0x10) b -= 0x06;
                SetFlagC(b >= 0x10);
                b = (A & 0xf0) + (v & 0xf0) + (b > 0xf ? 0x10 : 0) + (b & 0xf);
                if (b < 0x100) b -= 0x60;
                b = (A & 0xf00) + (v & 0xf00) + (b > 0xff ? 0x100 : 0) + (b & 0xff);
                if (b < 0x1000) b -= 0x600;
                b = (A & 0xf000) + (v & 0xf000) + (b > 0xfff ? 0x1000 : 0) + (b & 0xfff);
            }
            else
                b = A + v + C;

            SetFlagV((~(A ^ v) & (A ^ b) & 0x8000) == 0x8000);
            if (PS.GetBit(3) && b < 0x10000) b -= 0x6000;

            SetFlagC(b > 0xffff);

            A = (ushort)b;
        }
        SetFlagZN(b, MMem);
    }

    private void Sta(int a)
    {
        if (MMem)
            Write(a, (byte)A);
        else
            WriteWord(a, A);
    }

    private void Stx(int a)
    {
        if (XMem)
            Write(a, (byte)X);
        else
            WriteWord(a, X);
    }

    private void Sty(int a)
    {
        if (XMem)
            Write(a, (byte)Y);
        else
            WriteWord(a, Y);
    }

    private void Stz(int a)
    {
        if (MMem)
            Write(a, 0);
        else
            WriteWord(a, 0);
    }

    private void Tcd()
    {
        D = A;
        Idle();
        SetFlagZN(D, false);
    }

    private void Tdc()
    {
        A = D;
        Idle();
        SetFlagZN(A, false);
    }

    private void Trb(int a)
    {
        int v;
        if (MMem)
        {
            v = Read(a);
            Write(a, (byte)v & ~ra);
            v &= (byte)A;
        }
        else
        {
            v = ReadWord(a);
            WriteWord(a, (ushort)(v & ~ra));
            v &= A;
        }
        SetFlagZ(v, MMem);
    }

    private void Tsb(int a)
    {
        int v;
        if (MMem)
        {
            v = Read(a);
            Write(a, (byte)(v | A));
            v &= (byte)A;
        }
        else
        {
            v = ReadWord(a);
            WriteWord(a, (ushort)(v | A));
            v &= A;
        }
        SetFlagZ(v, MMem);
    }

    private void Tcs()
    {
        if (E)
            SetSp(A, true);
        else
            SP = A;
        Idle();
    }

    private void Tsc()
    {
        A = SP;
        Idle();
        SetFlagZN(A, false);
    }

    private void Tax()
    {
        if (XMem)
        {
            X = (X & 0xff00) | (byte)A;
            SetFlagZN((byte)X, XMem);
        }
        else
        {
            X = A;
            SetFlagZN(X, XMem);
        }
        Idle();
    }

    private void Tay()
    {
        if (XMem)
        {
            Y = (Y & 0xff00) | (byte)A;
            SetFlagZN(Y, XMem);
        }
        else
        {
            Y = A;
            SetFlagZN(Y, XMem);
        }
        Idle();
    }

    private void Tsx()
    {
        if (XMem)
        {
            SetRegValue(ref rx, (byte)sp, XMem);
            SetFlagZN((byte)X, XMem);
        }
        else
        {
            X = SP;
            SetFlagZN(X, XMem);
        }
        Idle();
    }

    private void Txa()
    {
        if (MMem)
        {
            SetRegValue(ref ra, (byte)rx, MMem);
            SetFlagZN(A, MMem);
        }
        else
        {
            A = X;
            SetFlagZN(A, MMem);
        }
        Idle();
    }

    private void Txs()
    {
        if (E)
            SP = (byte)X | 0x100;
        else
            SP = X;
        Idle();
    }

    private void Txy()
    {
        if (XMem)
        {
            SetRegValue(ref ry, (byte)rx, MMem);
            SetFlagZN(X, XMem);
        }
        else
        {
            Y = X;
            SetFlagZN(X, XMem);
        }
        Idle();
    }

    private void Tyx()
    {
        if (XMem)
        {
            SetRegValue(ref rx, (byte)ry, MMem);
            SetFlagZN(X, XMem);
        }
        else
        {
            X = Y;
            SetFlagZN(X, XMem);
        }
        Idle();
    }

    private void Tya()
    {
        if (MMem)
        {
            SetRegValue(ref ra, (byte)ry, MMem);
            SetFlagZN(A, MMem);
        }
        else
        {
            A = Y;
            SetFlagZN(A, MMem);
        }
        Idle();
    }

    private void Xba()
    {
        int al, ah;
        (al, ah) = (A >> 8, (byte)A);
        A = ah << 8 | al;
        Idle(); Idle();
        SetFlagZN(A, true);
    }

    private void Xce()
    {
        (E, bool c) = (PS.GetBit(0), E);
        PS = c ? PS |= FC : PS &= ~FC;
        if (E)
        {
            PS |= FX | FM;
            X &= 0xff;
            Y &= 0xff;
            SP &= 0xff | 1 << 8;
        }
        Idle();
    }

    public int GetMode(int mode, bool implied2 = false)
    {
        switch (mode)
        {
            case Absolute:
            {
                var a = Read(PBR | PC++) | Read(PBR | PC++) << 8;
                return DB << 16 | a;
            }
            case AbsoluteIndexedIndirect:
            {
                var a = (ushort)((Read(PBR | PC++) | Read(PBR | PC++) << 8) + X);
                return PBR | a;
            }
            case AbsoluteIndexedX:
            {
                var a = (ushort)(Read(PBR | PC++) | Read(PBR | PC++) << 8);
                if ((a + X & 0xff00) != (a & 0xff00)) Idle();
                if (!XMem) Idle();
                return DB << 16 | a + (XMem ? (byte)X : X);
            }
            case AbsoluteIndexedY:
            {
                Idle();
                var a = (ushort)(Read(PBR | PC++) | Read(PBR | PC++) << 8);
                if ((a + X & 0xff00) != (a & 0xff00)) Idle();
                if (!XMem) Idle();
                return DB << 16 | a + (XMem ? (byte)Y : Y);
            }
            case AbsoluteIndirect:
            {
                var a = Read(PBR | PC++) | Read(PBR | PC++) << 8;
                return a;
            }
            case AbsoluteIndirectLong:
            {
                var a = Read(PBR | PC++) | Read(PBR | PC++) << 8;
                return a;
            }
            case AbsoluteLong:
            {
                var a = (Read(PBR | PC++) | Read(PBR | PC++) << 8 | Read(PBR | PC++) << 16);
                return a;
            }
            case AbsoluteLongIndexedX:
            {
                var a = (Read(PBR | PC++) | Read(PBR | PC++) << 8 | Read(PBR | PC++) << 16) + X;
                return a;
            }
            case Accumulator: return 0;
            case DPIndexedIndirectX:
            {
                int a = Read(PBR | PC++);
                var b = (ushort)(a + X);
                if ((byte)D > 0) Idle();
                Idle();
                if (E && (byte)D == 0)
                {
                    int d = (D & 0xff00) | (byte)b;
                    a = Read(d);
                    a |= Read((d & 0xff) == 0xff ? b + 1 : b + 1) << 8;
                }
                else
                {
                    int d = (ushort)(b + D);
                    a = Read(d);
                    a |= Read((d & 0xff) == 0xff ? (d & 0xff00) : d + 1) << 8;
                }
                return DB << 16 | a;
            }
            case DPIndexedX:
            {
                if ((byte)D > 0) Idle();
                Idle();
                var a = (ushort)(Read(PBR | PC++) + D + X);
                a = (ushort)(E && a > 0xff ? a & 0xff | 0x100 : a);
                return a;
            }
            case DPIndexedY:
            {
                int a = Read(PBR | PC++);
                var b = (ushort)(a + Y);
                if (E && (byte)D == 0)
                    a = (D & 0xff00) | (byte)b;
                else
                    a = (ushort)(b + D);
                return a;
            }
            case DPIndirect:
            {
                var b = (ushort)(Read(PBR | PC++));
                if ((byte)D > 0) Idle();
                var a = DB << 16 | ReadWord(b + D);
                return a;
            }
            case DPIndirectIndexedY:
            {
                var b = (ushort)(Read(PBR | PC++));
                if ((byte)D > 0) Idle();
                Idle();
                var a = (DB << 16) | ReadWord(b + D) + Y;
                return a;
            }
            case DPIndirectLong:
            {
                var a = ReadLong((ushort)(Read(PBR | PC++) + D));
                return a;
            }
            case DPIndirectLongIndexedY:
            {
                var b = (ushort)(Read(PBR | PC++));
                if ((byte)D > 0) Idle();
                var a = ReadLong(b + D) + Y;
                return a;
            }
            case DirectPage:
            {
                var a = Read(PBR | PC++);
                return (ushort)(a + D);
            }
            case Immediate:
            {
                var a = Read(PBR | PC++);
                Idle();
                return a;
            }
            case ImmediateIndex:
            {
                if (!XMem)
                    return (ushort)(Read(PBR | PC++) | Read(PBR | PC++) << 8);
                else
                    return Read(PBR | PC++);
            }
            case ImmediateMemory:
            {
                if (!MMem)
                    return Read(PBR | PC++) | Read(PBR | PC++) << 8;
                else
                    return Read(PBR | PC++);
            }
            case Implied:
                //AddCycles(6);
                //if (implied2)
                //    AddCycles(6);
                return 0;
            case ProgramCounterRelative:
                return PBR | (ushort)(PC + (sbyte)Read(PBR | PC) + 1);

            case ProgramCounterRelativeLong:
            {
                var a = Read(PBR | PC) | (Read(PBR | PC + 1) << 8);
                return PBR | PC + (short)a + 2;
            }
            case SRIndirectIndexedY:
            {
                Idle(); Idle();
                return DB << 16 | ReadWord((ushort)(Read(PBR | PC++) + D + SP)) + Y;
            }
            case StackAbsolute:
                return Read(PBR | PC++) | Read(PBR | PC++) << 8;

            case StackDPIndirect:
            {
                var a = Read(PBR | (ushort)(PC++));
                return (ushort)(a + D);
            }
            case StackInterrupt: return 0;
            case StackPCRelativeLong: return Read(PBR | PC++) | Read(PBR | PC++) << 8;
            case StackRelative:
            {
                if ((byte)D > 0) Idle();
                Idle();
                return Read(PBR | PC++) + SP;
            }
        }
        return 0;
    }

    private byte Pop(bool e = false)
    {
        SetSp(SP + 1, e);
        return Read(SP);
    }

    private ushort PopWord() => (ushort)(Read(++SP) | Read(++SP) << 8);

    private void Push(byte v, bool e = false)
    {
        Idle();
        Write(SP, v);
        SetSp(SP - 1, e);
    }

    private void PushWord(int v, bool e = false)
    {
        Push((byte)(v >> 8), e);
        Push((byte)(v), e);
    }

    private void PushM(int v)
    {
        if (MMem)
            Push((byte)v);
        else
            PushWord(v);
    }
    private void PushX(int v)
    {
        if (XMem)
            Push((byte)v);
        else
        {
            Push((byte)(v >> 8));
            Push((byte)v);
        }
        WrapSp();
    }

    private void Brk()
    {
        Read(PC++);
        if (E)
        {
            PushWord(PC, true);
            Push((byte)(PS | FM), true);
            PC = ReadWord(BRKe);
        }
        else
        {
            Push((byte)PB);
            PushWord(PC);
            Push((byte)PS, true);
            PC = ReadWord(BRKn);
        }
        PS |= FI;
        PS &= ~FD;
        PB = 0;
    }

    private void Cop()
    {
        Read(PC++);
        if (E)
        {
            PushWord(PC);
            Push((byte)(PS | FM), true);
            PC = ReadWord(COPe);
        }
        else
        {
            Push((byte)PB);
            PushWord(PC);
            Push((byte)PS, true);
            PC = ReadWord(COPn);
            Idle();
        }
        PS |= FI;
        PS &= ~FD;
        PB = 0;
    }

    public void Reset()
    {
        SP = 0x0200;
        Read(0);
        Snes?.Cpu.Idle();
        Read(0x100 | sp--);
        Read(0x100 | sp--);
        Read(0x100 | sp--);
        PC = Read(RESETe) | Read(RESETe + 1) << 8;
        A = X = Y = 0;
        PS = 0x34;
        DB = 0x00;
        PB = 0x00;
        D = 0x0000;
        E = true;
        FastMem = false;
        NmiEnabled = false;
        IRQEnabled = false;
        Cycles = 0;
        StepOverAddr = -1;
    }

    private void SetFlagC(bool flag)
    {
        if (flag) PS |= FC;
        else PS &= ~FC;
    }

    private void SetFlagZ(int v, bool isbyte)
    {
        if (isbyte)
        {
            if ((byte)v == 0) PS |= FZ;
            else PS &= ~FZ;
        }
        else
        {
            if ((ushort)v == 0) PS |= FZ;
            else PS &= ~FZ;
        }
    }

    private void SetFlagZN(int v, bool isbyte)
    {
        if (isbyte)
        {
            if ((byte)v == 0) PS |= FZ;
            else PS &= ~FZ;
            if ((v & FN) == 0x80) PS |= FN;
            else PS &= ~FN;
        }
        else
        {
            if ((ushort)v == 0) PS |= FZ;
            else PS &= ~FZ;
            if ((v & FN << 8) == FN << 8) PS |= FN;
            else PS &= ~FN;
        }
    }

    private void SetFlagV(bool flag)
    {
        if (flag) PS |= FV;
        else PS &= ~FV;
    }



    public Dictionary<string, bool> GetFlags() => new()
    {
        ["C"] = PS.GetBit(0),
        ["Z"] = PS.GetBit(1),
        ["I"] = PS.GetBit(2),
        ["D"] = PS.GetBit(3),
        ["X"] = PS.GetBit(4),
        ["M"] = PS.GetBit(5),
        ["V"] = PS.GetBit(6),
        ["N"] = PS.GetBit(7),
        ["E"] = E
    };

    public Dictionary<string, string> GetRegs() => new()
    {
        ["A"] = $"{A:X4}",
        ["X"] = $"{X:X4}",
        ["Y"] = $"{Y:X4}",
        ["SP"] = $"{SP:X4}",
        ["D"] = $"{D:X4}",
        ["P"] = $"{PS:X4}",
        ["DB"] = $"{DB:X2}",
        ["PB"] = $"{PB:X2}",
    };

    public void SetReg(string reg, int v)
    {
        switch (reg.ToLowerInvariant())
        {
            case "a": A = v; break;
            case "x": X = v; break;
            case "y": Y = v; break;
            case "p": PS = v; break;
            case "pc": PC = v; break;
        }
    }



    public override void Save(BinaryWriter bw)
    {
        bw.Write(PC); bw.Write(SP);
        bw.Write(A); bw.Write(X);
        bw.Write(Y); bw.Write(PS);
        bw.Write(PB); bw.Write(DB);
        bw.Write(E); bw.Write(D);
        bw.Write(FastMem); bw.Write(NmiEnabled);
        bw.Write(IRQEnabled);
    }

    public override void Load(BinaryReader br)
    {
        PC = br.ReadInt32(); SP = br.ReadInt32();
        A = br.ReadInt32(); X = br.ReadInt32();
        Y = br.ReadInt32(); PS = br.ReadInt32();
        PB = br.ReadInt32(); DB = br.ReadInt32();
        E = br.ReadBoolean(); D = br.ReadInt32();
        FastMem = br.ReadBoolean(); NmiEnabled = br.ReadBoolean();
        IRQEnabled = br.ReadBoolean();
    }
}
