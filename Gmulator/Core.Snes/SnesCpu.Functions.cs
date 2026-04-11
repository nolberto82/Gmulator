namespace Gmulator.Core.Snes;
public partial class SnesCpu
{
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
                b = (A & 0xf) + (v & 0xf) + (PS & FC);
                if (b > 0x09) b += 0x06;
                SetFlagC(b >= 0x10);
                b = (A & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf);
            }
            else
                b = (byte)A + v + (PS & FC);

            SetFlagV((~(A & 0xff ^ v) & (A & 0xff ^ b) & 0x80) == 0x80);

            if (decimalMode && (byte)b > 0x9f) b += 0x60;

            SetFlagC(b >= 0x100);
            A = (A & 0xff00) | (b & 0xff);
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
            A = b & 0xffff;
        }
        SetFlagZN(b, mmem);
    }

    private void And(int a)
    {
        bool mmem = MMem;
        if (Imme)
        {
            if (mmem)
                A = (A & 0xff00) | A & a;
            else
                A &= a;
        }
        else
        {
            if (mmem)
                A = A & 0xff00 | Read(a) & A;
            else
                A &= ReadWord(a);
        }
        SetFlagZN(A, mmem);
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
                b = A & 0xff00 | ((A & 0xff) << 1) & 0xfe;
            }
            else
            {
                SetFlagC((A & 0x8000) != 0);
                b = (A << 1) & 0xfffe;
            }
            A = b;
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

        r = A - v;
        SetFlagC(mmem ? (A & 0xff) >= v : (A & 0xffff) >= v);
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
            SetFlagZN(A, mmem);
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
        SetFlagZN(X, XMem);
    }

    private void Dey()
    {
        Y--;
        SetFlagZN(Y, XMem);
    }

    private void Eor(int v)
    {
        var mmem = MMem;
        if (Imme)
        {
            v = A ^ v;
            A = v;
        }
        else
        {
            if (mmem)
                A ^= Read(v);
            else
                A ^= ReadWord(v);
        }
        SetFlagZN(A, mmem);
    }

    private void Inc(int a, int mode)
    {
        var mmem = MMem;
        if (mode == Accumulator)
        {
            if (mmem)
            {
                int v = A + 1;
                A = (A & 0xff00) | (byte)v;
            }
            else
                A++;
            SetFlagZN(A, mmem);
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
        SetFlagZN(X, XMem);
    }

    private void Iny()
    {
        Y++;
        SetFlagZN(Y, XMem);
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
            PC = ReadWord(v);
    }

    private void Jsr(int v, int mode)
    {
        PC--;
        if (mode == Absolute || mode == AbsoluteLong)
        {
            PushWord(PC, true);
            PC = PB << 16 | (ushort)v;
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
        SetFlagZN(A, MMem);
    }

    private void Ldx(int v)
    {
        var xmem = XMem;
        if (Imme)
            X = v;
        else
        {
            if (xmem)
                X = Read(v);
            else
                X = ReadWord(v);
        }
        SetFlagZN(X, xmem);
    }

    private void Ldy(int v)
    {
        var xmem = XMem;
        if (Imme)
            Y = v;
        else
        {
            if (xmem)
                Y = Read(v);
            else
                Y = ReadWord(v);
        }
        SetFlagZN(Y, xmem);
    }

    private void Lsr(int a, int mode)
    {
        int b;
        bool mmem = MMem;
        if (mode == Accumulator)
        {
            SetFlagC(((byte)A & 0x01) != 0);
            if (mmem)
            {
                b = A & 0xff;
                b = (b >> 1) & 0x7f;
            }
            else
                b = (A >> 1) & 0x7fff;
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
        int src = Read(PBPC + 1);
        int dst = Read(PBPC);
        DB = dst;
        int v = Read((src << 16) | (xmem ? (byte)X : X));
        Write((dst << 16) | (xmem ? (byte)Y : Y), v);
        X++;
        Y++;
        _ra = (_ra - 1) & 0xffff;
        PC = (_ra != 0xffff) ? (PC - 1) : (PC + 2);
        Idle();
    }

    private void Mvp()
    {
        bool xmem = XMem;
        int src = Read(PB << 16 | (PC + 1));
        int dst = Read(PB << 16 | PC);
        DB = dst;
        int v = Read((src << 16) | (xmem ? (byte)X : X));
        Write((dst << 16) | (xmem ? (byte)Y : Y), v);
        X--;
        Y--;
        _ra = (_ra - 1) & 0xffff;
        PC = (_ra != 0xffff) ? (PC - 1) : (PC + 2);
        Idle();
    }

    public virtual void Irq()
    {
        if (!E)
        {
            Push((byte)PB);
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            PS |= FI;
            Idle();
        }
        else
        {
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
        }
        PC = ReadWord(IRQn);
        PB = 0;
    }

    public virtual void Nmi()
    {
        if (!E)
        {
            Push((byte)PB);
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            PS |= FI;
            Idle();
        }
        else
        {
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
        }
        PC = ReadWord(NMIn);
        PB = 0;
    }

    private void Ora(int v)
    {
        bool mmem = MMem;
        if (Imme)
        {
            v = A | v;
            A = v;
        }
        else
        {
            if (mmem)
                A |= Read(v);
            else
                A |= ReadWord(v);
        }
        SetFlagZN(A, mmem);
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
            A = A & 0xff00 | Pop(true);
        else
            A = PopWord();
        SetFlagZN(A, mmem);
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
            X = X & 0xff00 | Pop(true);
        else
            X = PopWord();
        SetFlagZN(X, xmem);
    }

    private void Ply()
    {
        bool xmem = XMem;
        if (xmem)
            Y = Y & 0xff00 | Pop(true);
        else
            Y = PopWord();
        SetFlagZN(Y, xmem);
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
        PS &= ~v;
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
                b = A & 0xff;
                b <<= 1;
                if ((PS & FC) != 0)
                    b |= 0x01;
                A = b;
            }
            else
            {
                msb = A & 0x8000;
                A <<= 1;
                if ((PS & FC) == FC)
                    A |= 0x01;
                b = A & 0xffff;
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
                bit0 = A & 0x01;
                A >>= 1;
                if ((PS & FC) == FC)
                    A |= 0x8000;
                b = A & 0xffff;
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
        var v = PB << 16 | PopWord();
        PB = Pop();
        PC = v | PB << 16;
        PC++;
        WrapSp();
    }

    private void Rts()
    {
        PC = Pop(true);
        PC |= Pop(true) << 8;
        PC |= PB << 16;
        PC++;
    }

    private void Sep(int v)
    {
        PS |= v;
        if (E)
            PS |= FX | FM;

        if (XMem)
        {
            X &= 0xFF;
            Y &= 0xFF;
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
                b = (ushort)((A & 0xf) + (v & 0xf) + FlagC);
                if (b < 0x10) b -= 0x06;
                SetFlagC(b >= 0x10);
                b = (ushort)((A & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf));
            }
            else
                b = (A & 0xff) + v + FlagC;

            SetFlagV((~(A & 0xff ^ v) & (A & 0xff ^ b) & 0x80) == 0x80);
            if (decimalMode && b < 0x100) b -= 0x60;

            SetFlagC(b > 0xff);
            A = A & 0xff00 | b & 0xff;
        }
        else
        {
            v = (ushort)~GetGet16bitImm(a);
            if (decimalMode)
            {
                b = (A & 0xf) + (v & 0xf) + FlagC;
                if (b < 0x10) b -= 0x06;
                SetFlagC(b >= 0x10);
                b = (A & 0xf0) + (v & 0xf0) + (b > 0xf ? 0x10 : 0) + (b & 0xf);
                if (b < 0x100) b -= 0x60;
                b = (A & 0xf00) + (v & 0xf00) + (b > 0xff ? 0x100 : 0) + (b & 0xff);
                if (b < 0x1000) b -= 0x600;
                b = (A & 0xf000) + (v & 0xf000) + (b > 0xfff ? 0x1000 : 0) + (b & 0xfff);
            }
            else
                b = A + v + FlagC;

            SetFlagV((~(A ^ v) & (A ^ b) & 0x8000) == 0x8000);
            if (decimalMode && b < 0x10000) b -= 0x6000;

            SetFlagC(b > 0xffff);

            A = b & 0xffff;
        }
        SetFlagZN(b, mmem);
    }

    private void Sta(int a)
    {
        if (MMem)
            Write(a, A);
        else
            WriteWord(a, A);
    }

    private void Stx(int a)
    {
        if (XMem)
            Write(a, X);
        else
            WriteWord(a, X);
    }

    private void Sty(int a)
    {
        if (XMem)
            Write(a, Y);
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
        _ra = D & 0xffff;
        Idle();
        SetFlagZN(A, false);
    }

    private void Trb(int a)
    {
        int v;
        bool mmem = MMem;
        if (mmem)
        {
            v = Read(a);
            Write(a, v & 0xff & ~A);
            v &= A;
        }
        else
        {
            v = ReadWord(a);
            WriteWord(a, v & ~A);
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
        _ra = SP & 0xffff;
        Idle();
        SetFlagZN(A, false);
    }

    private void Tax()
    {
        X = A;
        SetFlagZN(X, XMem);
        Idle();
    }

    private void Tay()
    {
        Y = A;
        SetFlagZN(Y, XMem);
        Idle();
    }

    private void Tsx()
    {
        X = SP;
        SetFlagZN(X, XMem);
        Idle();
    }

    private void Txa()
    {
        A = !MMem ? X & 0xffff : A & 0xff00 | X & 0xff;
        SetFlagZN(A, MMem);
        Idle();
    }

    private void Txs()
    {
        SP = E ? X | 0x100 : X;
        Idle();
    }

    private void Txy()
    {
        Y = X;
        SetFlagZN(X, XMem);
        Idle();
    }

    private void Tyx()
    {
        X = Y;
        SetFlagZN(X, XMem);
        Idle();
    }

    private void Tya()
    {
        A = Y;
        SetFlagZN(A, MMem);
        Idle();
    }

    private void Xba()
    {
        int al, ah;
        (al, ah) = (A >> 8, (byte)A);
        _ra = ah << 8 | al;
        Idle(); Idle();
        SetFlagZN(A, true);
    }

    private void Xce()
    {
        (E, bool c) = (FlagC != 0, E);
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

    private int Pop(bool e = false)
    {
        SetSp(SP + 1, e);
        return Read(SP);
    }

    private ushort PopWord() => (ushort)(Read(++SP) | Read(++SP) << 8);

    public void Push(int v, bool e = false)
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

    public virtual void Reset(bool isSa1)
    {
        SP = 0x0200;
        Read(0);
        Idle();
        Read(0x100 | SP--);
        Read(0x100 | SP--);
        if (!isSa1)
            PC = Read(RESETe) | Read(RESETe + 1) << 8;

        A = X = Y = 0;
        PS = 0x34;
        DB = 0x00;
        PB = 0x00;
        D = 0x0000;
        E = true;
        FastMem = false;
        NmiEnabled = false;
        IrqEnabled = false;
        Cycles = 0;
        StepOverAddr = -1;
        _stepCounter = 0;
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
            if ((v & 0xff) == 0) PS |= FZ;
            else PS &= ~FZ;
        }
        else
        {
            if ((v & 0xffff) == 0) PS |= FZ;
            else PS &= ~FZ;
        }
    }

    private void SetFlagZN(int v, bool isbyte)
    {
        if (isbyte)
        {
            if ((v & 0xff) == 0) PS |= FZ;
            else PS &= ~FZ;
            if ((v & FN) != 0) PS |= FN;
            else PS &= ~FN;
        }
        else
        {
            if ((v & 0xffff) == 0) PS |= FZ;
            else PS &= ~FZ;
            if ((v & FN << 8) != 0) PS |= FN;
            else PS &= ~FN;
        }
    }

    private void SetFlagV(bool flag)
    {
        if (flag) PS |= FV;
        else PS &= ~FV;
    }
}
