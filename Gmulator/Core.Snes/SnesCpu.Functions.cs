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
                b = (ra & 0xf) + (v & 0xf) + (PS & FC);
                if (b > 0x09) b += 0x06;
                SetFlagC(b >= 0x10);
                b = (A & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf);
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

    public virtual void Nmi(int type, bool sa1=false)
    {
        if (!E)
        {
            Push((byte)PB);
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            if (sa1)
                PC = type;
            else
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
            if (sa1)
                PC = type;
            else
                PC = ReadWord(type);
            PB = 0;
        }
    }

    public void NmiSa1(int type)
    {
        if (!E)
        {
            Push((byte)PB);
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            PC = type;
            PB = 0;
            PS |= FI;
            Idle();
        }
        else
        {
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            PC = type;
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
            Write(a, v & 0xff & ~ra);
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
                var a = Read(pbr | PC++) | Read(pbr | PC++) << 8 | Read(pbr | PC++) << 16;
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

    public virtual void Reset(bool isSa1)
    {
        SP = 0x0200;
        Read(0);
        Idle();
        Read(0x100 | SP--);
        Read(0x100 | SP--);
        if (!isSa1)
            PC = Read(RESETe) | Read(RESETe + 1) << 8;

        ra = rx = ry = 0;
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
}
