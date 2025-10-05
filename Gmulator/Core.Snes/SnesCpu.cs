using Gmulator.Core.Snes.Mappers;
using System.ComponentModel.DataAnnotations;

namespace Gmulator.Core.Snes;
public partial class SnesCpu : EmuState, ICpu
{
    private const int FC = 1 << 0;
    private const int FZ = 1 << 1;
    private const int FI = 1 << 2;
    private const int FD = 1 << 3;
    public const int FX = 1 << 4;
    public const int FM = 1 << 5;
    private const int FV = 1 << 6;
    private const int FN = 1 << 7;
    private const int COPn = 0xFFE4;
    private const int BRKn = 0xFFE6;
    private const int IRQn = 0xFFEE;
    private const int NMIn = 0xFFEA;
    private const int COPe = 0xFFF4;
    private const int RESETe = 0xFFFC;
    private const int BRKe = 0xFFFE;

    private int pc, sp, ra, rx, ry, ps, db, pb, pbr, dr;
    private bool Imme, Wait;

    public int PC
    {
        get => pc & 0xffff;
        private set => pc = value & 0xffff;
    }
    public int SP
    {
        get => sp & 0xffff;
        private set => sp = value & 0xffff;
    }
    public int A
    {
        get => MMem ? ra & 0xff00 | ra & 0xff : ra & 0xffff;
        private set => ra = MMem ? ra & 0xff00 | value & 0xff : value & 0xffff;
    }
    public int X
    {
        get => !E && !XMem ? rx & 0xffff : rx & 0xff;
        private set => rx = !E && !XMem ? value & 0xffff : value & 0xff;
    }
    public int Y
    {
        get => !E && !XMem ? ry & 0xffff : ry & 0xff;
        private set => ry = !E && !XMem ? value & 0xffff : value & 0xff;
    }
    public int PS
    {
        get => ps & 0xff;
        private set => ps = value & 0xff;
    }
    public int PB { get => pb & 0xff; set => pb = value & 0xff; }
    public int DB { get => db & 0xff; private set => db = value & 0xff; }
    public bool E { get; set; }
    public int D { get => dr & 0xffff; private set => dr = value & 0xffff; }
    public bool FastMem { get; set; }
    public int OpenBus { get; set; }
    private bool NmiEnabled;
    private bool IRQEnabled;
    public int StepOverAddr;
    public int Cycles { get => cycles; private set => cycles = value; }

    public bool XMem => (PS & FX) != 0;
    public bool MMem => (PS & FM) != 0;

    private Snes Snes;
    private SnesPpu Ppu;
    private BaseMapper Mapper;
    private int C => PS & FC;
    private bool I => (PS & FI) != 0;
    public Action<int> SetState;
    private int cycles;

    public int TestAddr { get; private set; }

    public SnesCpu() => CreateOpcodes();

    public void SetSnes(Snes snes, SnesPpu ppu, BaseMapper mapper)
    {
        Snes = snes;
        Ppu = ppu;
        Mapper = mapper;
    }

    public void ResetCycles() => cycles = 0;

    private void Idle()
    {
        Snes?.HandleDma();
        Ppu?.Step(6);
    }

    public void Idle8()
    {
        Ppu?.Step(8);
    }

    public int Read(int a)
    {
        var c = GetClockSpeed(a);
        Ppu?.Step(c);
        Snes?.HandleDma();
        int v = Snes?.ReadMemory(a) ?? 0;
        return OpenBus = v & 0xff;
    }

    public void Write(int a, int v)
    {
        var c = GetClockSpeed(a);
        Ppu?.Step(c);
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
            if (a < 0x4000)
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

    private int ReadWord(int a)
    {
        int low = Read(a);
        int high = Read(a + 1);
        return (high << 8 | low) & 0xffff;
    }

    private int ReadLong(int a)
    {
        int low = Read(a);
        int mid = Read(a + 1);
        int high = Read(a + 2);
        return (high << 16 | mid << 8 | low) & 0xffffff;
    }

    private void WriteWord(int a, int v)
    {
        Write(a, v & 0xff);
        Write(a + 1, (v >> 8) & 0xff);
    }

    private int GetGet8bitImm(int a)
    {
        return Imme ? a & 0xff : Read(a);
    }

    private int GetGet16bitImm(int a)
    {
        return Imme ? a & 0xffff : ReadWord(a);
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

        int pbr = pb << 16;
#if DEBUG
        if (PC == 0x8000 || PC == 0x8266)
        {
            var a = (Read(SP + 1) | Read(SP + 2) << 8) - 2;
            if (a > 0)
                TestAddr = pbr | a;
        }
#endif

        int op = Read(pb << 16 | PC++) & 0xff;
        ExecOp(op);
    }

    public void ExecOp(int op)
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
            case CLC: PS &= ~FC; Idle(); break;
            case CLD: PS &= ~FD; Idle(); break;
            case CLI: PS &= ~FI; Idle(); break;
            case CLV: PS &= ~FV; Idle(); break;
            case CMP: Cmp(GetMode(mode)); break;
            case COP: Cop(); break;
            case CPX: Cpx(GetMode(mode)); break;
            case CPY: Cpy(GetMode(mode)); break;
            case DEC: Dec(GetMode(mode), mode); break;
            case DEX: Dex(); Idle(); break;
            case DEY: Dey(); Idle(); break;
            case EOR: Eor(GetMode(mode)); break;
            case INC: Inc(GetMode(mode), mode); break;
            case INX: Inx(); Idle(); break;
            case INY: Iny(); Idle(); break;
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
            case PHB: Push(db); break;
            case PHD: Phd(); break;
            case PHK: Phk(); break;
            case PHP: Php(); break;
            case PHX: PushX(rx); break;
            case PHY: PushX(ry); break;
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
            case SEI: PS |= FI; Idle(); break;
            case SEP: Sep(GetMode(Immediate)); break;
            case STA: Sta(GetMode(mode)); break;
            case STP: GetMode(mode, true); Idle(); Idle(); break;
            case STX: Stx(GetMode(mode)); break;
            case STY: Sty(GetMode(mode)); break;
            case STZ: Stz(GetMode(mode)); break;
            case TAX: Tax(); break;
            case TAY: Tay(); break;
            case TCD: Tcd(); break;
            case TCS: Tcs(); break;
            case TDC: Tdc(); break;
            case TRB: Trb(GetMode(mode)); break;
            case TSB: Tsb(GetMode(mode)); break;
            case TSC: Tsc(); break;
            case TSX: Tsx(); break;
            case TXA: Txa(); break;
            case TXS: Txs(); break;
            case TXY: Txy(); break;
            case TYA: Tya(); break;
            case TYX: Tyx(); break;
            case WAI: GetMode(mode, true); Idle(); Idle(); Wait = true; break;
            case WDM: PC++; break;
            case XBA: Xba(); break;
            case XCE: Xce(); break;
        }
    }

    private void Adc(int a)
    {
        int v, b;
        bool decimalMode = (PS & FD) != 0;
        bool mmem = MMem;
        if (mmem)
        {
            v = GetGet8bitImm(a);

            if (decimalMode)
            {
                b = (ra & 0xf) + (v & 0xf) + (PS & FC);
                if (b > 0x09) b += 0x06;
                SetFlagC(b >= 0x10);
                b = ((A & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf));
            }
            else
            {
                b = (byte)A + v + (PS & FC);
            }

            SetFlagV((~(ra & 0xff ^ v) & (ra & 0xff ^ b) & 0x80) == 0x80);

            if (decimalMode && (byte)b > 0x9f) b += 0x60;

            SetFlagC(b >= 0x100);
            ra = (ra & 0xff00) | (b & 0xff);
        }
        else
        {
            v = GetGet16bitImm(a);
            if (decimalMode)
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
            {
                b = A + v + (PS & FC);
            }

            SetFlagV((~(A ^ v) & (A ^ b) & 0x8000) == 0x8000);

            if (decimalMode && b > 0x9fff) b += 0x6000;

            SetFlagC(b >= 0x10000);
            ra = b & 0xffff;
        }
        SetFlagZN(b, mmem);
    }

    private void And(int a)
    {
        bool mmem = MMem;
        if (Imme)
        {
            if (mmem)
                ra = (ra & 0xff00) | ra & a;
            else
                ra &= a;
        }
        else
        {
            if (mmem)
                ra = ra & 0xff00 | Read(a) & ra;
            else
                ra &= ReadWord(a);
        }
        SetFlagZN(ra, mmem);
    }

    private void Asl(int a, int mode)
    {
        int b;
        bool mmem = MMem;
        if (mode == Accumulator)
        {
            if (mmem)
            {
                SetFlagC((A & 0x80) != 0);
                b = ra & 0xff00 | ((ra & 0xff) << 1) & 0xfe;
            }
            else
            {
                SetFlagC((A & 0x8000) != 0);
                b = (ra << 1) & 0xfffe;
            }
            ra = b;
        }
        else
        {
            if (mmem)
            {
                b = Read(a);
                SetFlagC((b & 0x80) != 0);
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
        SetFlagZN(b, mmem);
    }

    private void Brp(int m, int f)
    {
        if ((PS & (1 << f)) != 0)
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
        if ((PS & (1 << f)) == 0)
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
        int v, b;
        bool mmem = MMem;
        if (Imme)
        {
            if (mmem)
            {
                b = GetGet8bitImm(a);
                v = (byte)A & b;
                PS = ((v & 0xFF) == 0) ? (PS | FZ) : (PS & ~FZ);
            }
            else
            {
                b = GetGet16bitImm(a);
                v = A & b;
                PS = ((v & 0xFFFF) == 0) ? (PS | FZ) : (PS & ~FZ);
            }
        }
        else
        {
            if (mmem)
            {
                b = Read(a);
                v = (byte)A & b;
                PS = ((v & 0xFF) == 0) ? (PS | FZ) : (PS & ~FZ);
                PS = ((b & FN) != 0) ? (PS | FN) : (PS & ~FN);
                SetFlagV((b & FV) != 0);
            }
            else
            {
                b = ReadWord(a);
                v = A & b;
                PS = ((v & 0xFFFF) == 0) ? (PS | FZ) : (PS & ~FZ);
                PS = ((b & 0x8000) != 0) ? (PS | FN) : (PS & ~FN);
                SetFlagV((b & 0x4000) != 0);
                Idle();
            }
        }
    }

    private void Cmp(int a)
    {
        int v, r;
        bool mmem = MMem;
        if (mmem)
            v = GetGet8bitImm(a);
        else
            v = GetGet16bitImm(a);

        r = ra - v;
        SetFlagC(mmem ? (ra & 0xff) >= v : (ra & 0xffff) >= v);
        SetFlagZN(r, mmem);
    }

    private void Cpx(int a)
    {
        int v, r;
        bool xmem = XMem;
        if (xmem)
            v = GetGet8bitImm(a);
        else
            v = GetGet16bitImm(a);

        r = X - v;
        SetFlagC(X >= v);
        SetFlagZN(r, xmem);
    }

    private void Cpy(int a)
    {
        int v, r;
        bool xmem = XMem;
        if (xmem)
            v = GetGet8bitImm(a);
        else
            v = GetGet16bitImm(a);

        r = Y - v;
        SetFlagC(Y >= v);
        SetFlagZN(r, xmem);
    }

    private void Dec(int a, int mode)
    {
        int b;
        var mmem = MMem;
        if (mode == Accumulator)
        {
            A--;
            SetFlagZN(ra, mmem);
        }
        else
        {
            if (mmem)
            {
                b = (byte)(Read(a) - 1);
                Write(a, b);
            }
            else
            {
                b = (ushort)(ReadWord(a) - 1);
                WriteWord(a, b);
            }
            SetFlagZN(b, mmem);
        }
    }

    private void Dex()
    {
        X--;
        SetFlagZN(rx, XMem);
    }

    private void Dey()
    {
        Y--;
        SetFlagZN(ry, XMem);
    }

    private void Eor(int v)
    {
        var mmem = MMem;
        if (Imme)
        {
            v = ra ^ v;
            ra = v;
        }
        else
        {
            if (mmem)
                ra ^= Read(v);
            else
                ra ^= ReadWord(v);
        }
        SetFlagZN(ra, mmem);
    }

    private void Inc(int a, int mode)
    {
        var mmem = MMem;
        if (mode == Accumulator)
        {
            if (mmem)
            {
                int v = ra + 1;
                ra = (ra & 0xff00) | (byte)v;
            }
            else
                ra++;
            SetFlagZN(ra, mmem);
        }
        else
        {
            int b;
            if (mmem)
            {
                b = (byte)(Read(a) + 1);
                Write(a, b);
            }
            else
            {
                b = (ushort)(ReadWord(a) + 1);
                WriteWord(a, b);
            }
            SetFlagZN(b, mmem);
        }
    }

    private void Inx()
    {
        X++;
        SetFlagZN(rx, XMem);
    }

    private void Iny()
    {
        Y++;
        SetFlagZN(ry, XMem);
    }

    private void Jml(int v, int mode)
    {
        if (mode == AbsoluteLong)
        {
            PB = v >> 16;
            PC = v;
        }
        else if (mode == AbsoluteIndirectLong)
        {
            var a = ReadLong(v);
            PC = a;
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
            PC = pb << 16 | (ushort)v;
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
        var pc = PC - 1;
        var pb = PB;
        Push((byte)pb);
        Push((byte)(pc >> 8));
        Push((byte)pc);
        PC = v;
        PB = v >> 16;
        WrapSp();
    }

    private void Lda(int v)
    {
        if (Imme)
            A = v;
        else
        {
            if (MMem)
                A = Read(v);
            else
                A = ReadWord(v);
        }
        SetFlagZN(ra, MMem);
    }

    private void Ldx(int v)
    {
        var xmem = XMem;
        if (Imme)
            rx = v;
        else
        {
            if (xmem)
                X = Read(v);
            else
                X = ReadWord(v);
        }
        SetFlagZN(rx, xmem);
    }

    private void Ldy(int v)
    {
        var xmem = XMem;
        if (Imme)
            ry = v;
        else
        {
            if (xmem)
                ry = Read(v);
            else
                ry = ReadWord(v);
        }
        SetFlagZN(ry, xmem);
    }

    private void Lsr(int a, int mode)
    {
        int b;
        bool mmem = MMem;
        if (mode == Accumulator)
        {
            SetFlagC(((byte)ra & 0x01) != 0);
            if (mmem)
            {
                b = A & 0xff;
                b = (b >> 1) & 0x7f;
            }
            else
                b = (ra >> 1) & 0x7fff;
            A = b;
        }
        else
        {
            if (mmem)
            {
                b = Read(a);
                SetFlagC((b & 0x01) != 0);
                b = (byte)((b >> 1) & 0x7f);
                Write(a, b);
            }
            else
            {
                b = ReadWord(a);
                SetFlagC((b & 0x01) != 0);
                b = (ushort)((b >> 1) & 0x7fff);
                WriteWord(a, b);
            }
        }
        SetFlagZN(b, mmem);
    }

    private void Mvn()
    {
        bool xmem = XMem;
        int src = Read(pb << 16 | (PC + 1));
        int dst = Read(pb << 16 | PC);
        DB = dst;
        int v = Read((src << 16) | (xmem ? (byte)rx : rx));
        Write((dst << 16) | (xmem ? (byte)ry : ry), v);
        ra = (ra - 1) & 0xffff;
        X++;
        Y++;
        PC = (ra != 0xffff) ? (PC - 1) : (PC + 2);
        Idle();
    }

    private void Mvp()
    {
        bool xmem = XMem;
        int src = Read(pb << 16 | (PC + 1));
        int dst = Read(pb << 16 | PC);
        DB = dst;
        int v = Read((src << 16) | (xmem ? (byte)rx : rx));
        Write((dst << 16) | (xmem ? (byte)ry : ry), v);
        ra = (ra - 1) & 0xffff;
        X--;
        Y--;
        PC = (ra != 0xffff) ? (PC - 1) : (PC + 2);
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
        bool mmem = MMem;
        if (Imme)
        {
            v = ra | v;
            ra = v;
        }
        else
        {
            if (mmem)
                ra |= Read(v);
            else
                ra |= ReadWord(v);
        }
        SetFlagZN(ra, mmem);
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
        Push(PB);
        WrapSp();
    }

    private void Php()
    {
        Push(PS);
        WrapSp();
    }

    private void Pla()
    {
        bool mmem = MMem;
        if (mmem)
            ra = ra & 0xff00 | Pop(true);
        else
            ra = PopWord();
        SetFlagZN(ra, mmem);
    }

    private void Pld()
    {
        D = PopWord();
        SetFlagZN(D, false);
        WrapSp();
    }

    private void Plx()
    {
        bool xmem = XMem;
        if (xmem)
            rx = rx & 0xff00 | Pop(true);
        else
            rx = PopWord();
        SetFlagZN(rx, xmem);
    }

    private void Ply()
    {
        bool xmem = XMem;
        if (xmem)
            ry = ry & 0xff00 | Pop(true);
        else
            ry = PopWord();
        SetFlagZN(ry, xmem);
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
            rx &= 0xff;
            ry &= 0xff;
        }
        WrapSp();
    }

    private void Rep(int v)
    {
        ps &= ~v;
        if (E)
            PS |= FX | FM;
    }

    private void Rol(int a, int mode)
    {
        int msb;
        int b;
        bool mmem = MMem;
        if (mode == Accumulator)
        {
            if (mmem)
            {
                msb = A & 0x80;
                b = ra & 0xff;
                b <<= 1;
                if ((PS & FC) != 0)
                    b |= 0x01;
                A = b;
            }
            else
            {
                msb = ra & 0x8000;
                ra <<= 1;
                if ((PS & FC) == FC)
                    ra |= 0x01;
                b = ra & 0xffff;
            }
        }
        else
        {
            if (mmem)
            {
                b = Read(a);
                msb = b & 0x80;
                b = (byte)(b << 1);
                if ((PS & FC) != 0)
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
        SetFlagC(msb != 0);
        SetFlagZN(b, mmem);
    }

    private void Ror(int a, int mode)
    {
        int bit0, b;
        bool mmem = MMem;
        if (mode == Accumulator)
        {
            if (mmem)
            {
                b = A & 0xff;
                bit0 = b & 0x01;
                b >>= 1;
                if ((PS & FC) != 0)
                    b |= 0x80;
                A = b;
            }
            else
            {
                bit0 = ra & 0x01;
                ra >>= 1;
                if ((PS & FC) == FC)
                    ra |= 0x8000;
                b = ra & 0xffff;
            }
        }
        else
        {
            if (mmem)
            {
                b = Read(a);
                bit0 = b & 0x01;
                b = (byte)(b >> 1);
                if ((PS & FC) != 0)
                    b |= 0x80;
                Write(a, b);
            }
            else
            {
                b = ReadWord(a);
                bit0 = b & 0x01;
                b = (ushort)(b >> 1);
                if ((PS & FC) != 0)
                    b |= 0x8000;
                WriteWord(a, b);
            }

        }
        SetFlagC(bit0 != 0);
        SetFlagZN(b, mmem);
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
        var v = pb << 16 | PopWord();
        PB = Pop();
        PC = v | pb << 16;
        PC++;
        WrapSp();
    }

    private void Rts()
    {
        PC = Pop(true);
        PC |= Pop(true) << 8;
        PC |= pb << 16;
        PC++;
    }

    private void Sep(int v)
    {
        ps |= v;
        if (E)
            PS |= FX | FM;

        if (XMem)
        {
            rx &= 0xFF;
            ry &= 0xFF;
        }
    }

    private void Sbc(int a)
    {
        int v, b;
        bool decimalMode = (PS & FD) != 0;
        bool mmem = MMem;
        if (mmem)
        {
            v = ~GetGet8bitImm(a) & 0xff;
            if (decimalMode)
            {
                b = (ushort)((A & 0xf) + (v & 0xf) + C);
                if (b < 0x10) b -= 0x06;
                SetFlagC(b >= 0x10);
                b = (ushort)((A & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf));
            }
            else
                b = (ra & 0xff) + v + C;

            SetFlagV((~(ra & 0xff ^ v) & (ra & 0xff ^ b) & 0x80) == 0x80);
            if (decimalMode && b < 0x100) b -= 0x60;

            SetFlagC(b > 0xff);
            ra = ra & 0xff00 | b & 0xff;
        }
        else
        {
            v = (ushort)~GetGet16bitImm(a);
            if (decimalMode)
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
            if (decimalMode && b < 0x10000) b -= 0x6000;

            SetFlagC(b > 0xffff);

            ra = b & 0xffff;
        }
        SetFlagZN(b, mmem);
    }

    private void Sta(int a)
    {
        if (MMem)
            Write(a, ra);
        else
            WriteWord(a, ra);
    }

    private void Stx(int a)
    {
        if (XMem)
            Write(a, rx);
        else
            WriteWord(a, rx);
    }

    private void Sty(int a)
    {
        if (XMem)
            Write(a, ry);
        else
            WriteWord(a, ry);
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
        dr = ra;
        Idle();
        SetFlagZN(dr, false);
    }

    private void Tdc()
    {
        ra = D;
        Idle();
        SetFlagZN(ra, false);
    }

    private void Trb(int a)
    {
        int v;
        bool mmem = MMem;
        if (mmem)
        {
            v = Read(a);
            Write(a, (v & 0xff) & ~ra);
            v &= A;
        }
        else
        {
            v = ReadWord(a);
            WriteWord(a, v & ~ra);
            v &= A;
        }
        SetFlagZ(v, mmem);
    }

    private void Tsb(int a)
    {
        bool mmem = MMem;
        if (mmem)
        {
            int v = Read(a);
            Write(a, v | A);
            v &= A;
            SetFlagZ(v, mmem);
        }
        else
        {
            int v = ReadWord(a);
            WriteWord(a, v | A);
            v &= A;
            SetFlagZ(v, mmem);
        }
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
        ra = sp;
        Idle();
        SetFlagZN(ra, false);
    }

    private void Tax()
    {
        X = A;
        SetFlagZN(rx, XMem);
        Idle();
    }

    private void Tay()
    {
        Y = A;
        SetFlagZN(ry, XMem);
        Idle();
    }

    private void Tsx()
    {
        rx = sp;
        SetFlagZN(rx, XMem);
        Idle();
    }

    private void Txa()
    {
        ra = !MMem ? rx & 0xffff : ra & 0xff00 | rx & 0xff;
        SetFlagZN(ra, MMem);
        Idle();
    }

    private void Txs()
    {
        sp = E ? rx | 0x100 : rx;
        Idle();
    }

    private void Txy()
    {
        Y = rx;
        SetFlagZN(rx, XMem);
        Idle();
    }

    private void Tyx()
    {
        X = ry;
        SetFlagZN(rx, XMem);
        Idle();
    }

    private void Tya()
    {
        A = ry;
        SetFlagZN(ra, MMem);
        Idle();
    }

    private void Xba()
    {
        int al, ah;
        (al, ah) = (ra >> 8, (byte)A);
        ra = ah << 8 | al;
        Idle(); Idle();
        SetFlagZN(A, true);
    }

    private void Xce()
    {
        (E, bool c) = (C != 0, E);
        PS = c ? PS |= FC : PS &= ~FC;
        if (E)
        {
            PS |= FX | FM;
            rx &= 0xff;
            ry &= 0xff;
            SP &= 0xff | 1 << 8;
        }
        Idle();
    }

    public int GetMode(int mode, bool implied2 = false)
    {
        int pbr = pb << 16;
        switch (mode)
        {

            case Absolute:
            {
                var a = Read(pbr | PC++) | Read(pbr | PC++) << 8;
                return db << 16 | a;
            }
            case AbsoluteIndexedIndirect:
            {
                var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8) + X & 0xffff;
                return pbr | a;
            }
            case AbsoluteIndexedX:
            {
                var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8) & 0xffff;
                if ((a + X & 0xff00) != (a & 0xff00)) Idle();
                if (!XMem) Idle();
                return db << 16 | a + (XMem ? (byte)X : X);
            }
            case AbsoluteIndexedY:
            {
                Idle();
                var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8) & 0xffff;
                if ((a + X & 0xff00) != (a & 0xff00)) Idle();
                if (!XMem) Idle();
                return db << 16 | a + Y;
            }
            case AbsoluteIndirect:
            {
                var a = Read(pbr | PC++) | Read(pbr | PC++) << 8;
                return a;
            }
            case AbsoluteIndirectLong:
            {
                var a = Read(pbr | PC++) | Read(pbr | PC++) << 8;
                return a;
            }
            case AbsoluteLong:
            {
                var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8 | Read(pbr | PC++) << 16);
                return a;
            }
            case AbsoluteLongIndexedX:
            {
                var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8 | Read(pbr | PC++) << 16) + X;
                return a;
            }
            case Accumulator: return 0;
            case DPIndexedIndirectX:
            {
                int a = Read(pbr | PC++);
                var b = (a + X) & 0xffff;
                if ((dr & 0xff) != 0) Idle();
                Idle();
                if (E && (D & 0xff) == 0)
                {
                    int d = (dr & 0xff00) | b & 0xff;
                    a = Read(d);
                    a |= Read((d & 0xff) == 0xff ? b + 1 : b + 1) << 8;
                }
                else
                {
                    int d = (b + dr) & 0xffff;
                    a = Read(d);
                    a |= Read((d & 0xff) == 0xff ? (d & 0xff00) : d + 1) << 8;
                }
                return db << 16 | a;
            }
            case DPIndexedX:
            {
                if ((dr & 0xff) != 0) Idle();
                Idle();
                var a = (Read(pbr | PC++) + dr + X) & 0xffff;
                a = (E && a > 0xff ? a & 0xff | 0x100 : a) & 0xffff;
                return a;
            }
            case DPIndexedY:
            {
                int a = Read(pbr | PC++);
                var b = (a + Y) & 0xffff;
                if (E && (dr & 0xff) == 0)
                    a = (dr & 0xff00) | b & 0xff;
                else
                    a = (b + D) & 0xffff;
                return a;
            }
            case DPIndirect:
            {
                var b = (Read(pbr | PC++)) & 0xffff;
                if ((dr & 0xff) != 0) Idle();
                var a = db << 16 | ReadWord(b + dr);
                return a;
            }
            case DPIndirectIndexedY:
            {
                var b = Read(pbr | PC++) & 0xffff;
                if ((dr & 0xff) != 0) Idle();
                Idle();
                int a = (db << 16) | ReadWord(b + dr) + Y;
                return a;
            }
            case DPIndirectLong:
            {
                int a = ReadLong((Read(pbr | PC++) + dr) & 0xffff);
                return a;
            }
            case DPIndirectLongIndexedY:
            {
                int b = Read(pbr | PC++) & 0xffff;
                if ((dr & 0xff) != 0) Idle();
                return ReadLong(b + dr) + Y;
            }
            case DirectPage:
            {
                int a = Read(pbr | PC++);
                return (a + dr) & 0xffff;
            }
            case Immediate:
            {
                int a = Read(pbr | PC++);
                Idle();
                return a;
            }
            case ImmediateIndex:
            {
                if (!XMem)
                    return (Read(pbr | PC++) | Read(pbr | PC++) << 8) & 0xffff;
                else
                    return Read(pbr | PC++);
            }
            case ImmediateMemory:
            {
                if (!MMem)
                    return Read(pbr | PC++) | Read(pbr | PC++) << 8;
                else
                    return Read(pbr | PC++);
            }
            case Implied:
                return 0;
            case ProgramCounterRelative:
                return pbr | (PC + (sbyte)Read(pbr | PC) + 1) & 0xffff;

            case ProgramCounterRelativeLong:
            {
                var a = Read(pbr | PC) | (Read(pbr | PC + 1) << 8);
                return pbr | PC + a + 2 & 0xffff;
            }
            case SRIndirectIndexedY:
            {
                Idle(); Idle();
                return db << 16 | ReadWord((Read(pbr | PC++) + D + SP) & 0xffff) + Y;
            }
            case StackAbsolute:
                return Read(pbr | PC++) | Read(pbr | PC++) << 8;

            case StackDPIndirect:
            {
                var a = Read(pbr | PC++);
                return (a + D) & 0xffff;
            }
            case StackInterrupt: return 0;
            case StackPCRelativeLong: return Read(pbr | PC++) | Read(pbr | PC++) << 8;
            case StackRelative:
            {
                if ((D & 0xff) != 0) Idle();
                Idle();
                return Read(pbr | PC++) + SP;
            }
        }
        return 0;
    }

    private int Pop(bool e = false)
    {
        SetSp(SP + 1, e);
        return Read(SP);
    }

    private ushort PopWord() => (ushort)(Read(++SP) | Read(++SP) << 8);

    private void Push(int v, bool e = false)
    {
        Idle();
        Write(SP, v & 0xff);
        SetSp(SP - 1, e);
    }

    private void PushWord(int v, bool e = false)
    {
        Push(v >> 8, e);
        Push(v, e);
    }

    private void PushM(int v)
    {
        if (MMem)
            Push(v);
        else
            PushWord(v);
    }
    private void PushX(int v)
    {
        if (XMem)
            Push(v);
        else
        {
            Push(v >> 8);
            Push(v);
        }
        WrapSp();
    }

    private void Brk()
    {
        Read(PC++);
        if (E)
        {
            PushWord(PC, true);
            Push(PS | FM, true);
            PC = ReadWord(BRKe);
        }
        else
        {
            Push(PB);
            PushWord(PC);
            Push(PS, true);
            PC = ReadWord(BRKn);
        }
        PS |= FI;
        PS &= ~FD;
        PB = 0;
    }

    private void Cop()
    {
        Read((ushort)PC++);
        if (E)
        {
            PushWord(PC);
            Push(PS | FM, true);
            PC = ReadWord(COPe);
        }
        else
        {
            Push(PB);
            PushWord(PC);
            Push(PS, true);
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
        Idle();
        Read(0x100 | SP--);
        Read(0x100 | SP--);
        PC = Read(RESETe) | Read(RESETe + 1) << 8;
        ra = rx = ry = 0;
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
        if (flag) ps |= FC;
        else ps &= ~FC;
    }

    private void SetFlagZ(int v, bool isbyte)
    {
        if (isbyte)
        {
            if ((v & 0xff) == 0) ps |= FZ;
            else ps &= ~FZ;
        }
        else
        {
            if ((v & 0xffff) == 0) ps |= FZ;
            else ps &= ~FZ;
        }
    }

    private void SetFlagZN(int v, bool isbyte)
    {
        if (isbyte)
        {
            if ((v & 0xff) == 0) ps |= FZ;
            else ps &= ~FZ;
            if ((v & FN) != 0) ps |= FN;
            else ps &= ~FN;
        }
        else
        {
            if ((v & 0xffff) == 0) ps |= FZ;
            else ps &= ~FZ;
            if ((v & FN << 8) != 0) ps |= FN;
            else ps &= ~FN;
        }
    }

    private void SetFlagV(bool flag)
    {
        if (flag) ps |= FV;
        else ps &= ~FV;
    }

    public Dictionary<string, bool> GetFlags() => new()
    {
        ["C"] = (ps & FC) != 0,
        ["Z"] = (ps & FZ) != 0,
        ["I"] = (ps & FI) != 0,
        ["D"] = (ps & FD) != 0,
        ["X"] = (ps & FX) != 0,
        ["M"] = (ps & FM) != 0,
        ["V"] = (ps & FV) != 0,
        ["N"] = (ps & FN) != 0,
        ["E"] = E
    };

    public Dictionary<string, string> GetRegisters() => new()
    {
        ["A"] = $"{A:X4}",
        ["X"] = $"{X:X4}",
        ["Y"] = $"{Y:X4}",
        ["SP"] = $"{SP:X4}",
        ["D"] = $"{D:X4}",
        ["P"] = $"{PS:X4}",
        ["DB"] = $"{DB:X2}",
        ["PB"] = $"{PB:X2}",
        ["PC"] = $"{PC:X4}"
    };

    public int GetReg(string reg)
    {
        switch (reg.ToLowerInvariant())
        {
            case "a": return A;
            case "x": return X;
            case "y": return Y;
            case "p": return PS;
            case "pc": return PC;
            default: return 0;
        }
    }

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
