namespace Gmulator.Core.Snes;

public partial class SnesSpc
{
    private void Asla() => _a = Asl(_a);

    private void Asld(int c) => Write(c, (byte)Asl(Read(c)));

    private byte Asl(int c)
    {
        int r = (c << 1) & 0xfe;
        SetC((c & 0x80) > 0);
        SetZN(r);
        return (byte)r;
    }

    private void Lsra() => _a= (byte)Lsr(_a);

    private void Lsrd(int c) => Write(c, (byte)Lsr(Read(c)));

    private int Lsr(int c)
    {
        SetC((c & 0x01) > 0);
        int r = (c >> 1) & 0x7f;
        SetZN(r);
        return r & 0xff;
    }

    private void Rola() => _a= (byte)Rol(_a);

    private void Rold(int c) => Write(c, (byte)Rol(Read(c)));

    private int Rol(int c)
    {
        int r = (c << 1) | CF;
        SetC((c & 0x80) > 0);
        SetZN(r);
        return r & 0xff;
    }

    private void Rora() => _a= Ror(_a);

    private void Rord(int c) => Write(c, (byte)Ror(Read(c)));

    private byte Ror(int c)
    {
        int bit0 = c & 1;
        int r = (c >> 1) | (CF == 1 ? 0x80 : 0x00);
        SetC(bit0 == 1);
        SetZN(r);
        return (byte)r;
    }

    private void Bbc((int a, int b) t, int bit)
    {
        if ((t.a & bit) == 0)
            _pc += (ushort)(sbyte)t.b;
    }

    private void Bbs((int a, int b) t, int bit)
    {
        if ((t.a & bit) == bit)
            _pc += (ushort)(sbyte)t.b;
    }

    private void Dbnzy(int b)
    {
        _y--;
        Dbnz(_y, b);
    }

    private void Dbnzd((int a, int b) t)
    {
        int da = t.a | GetPage();
        int v = (Read(da) - 1) & 0xff;
        Dbnz(v, t.b);
        Write(da, (byte)v);
    }
    private void Dbnz(int r, int b)
    {
        if (r > 0)
            _pc += (ushort)(sbyte)b;
    }

    private void Brk()
    {
        Push((byte)(PC >> 8));
        Push((byte)PC);
        Push(_ps);
        _ps |= FB;
        _ps = (byte)(_ps & ~FI);
        _pc = (ushort)(Read(0xffde) | Read(0xffdf) << 8);
        Idle(); Idle();
    }

    private void Inca() => _a = Inc(_a);

    private void Incx() => _x = Inc(_x);

    private void Incy() => _y = Inc(_y);

    private void Incd(int b) => Write(b, (byte)Inc(Read(b)));

    private byte Inc(int c)
    {
        int r = c + 1;
        SetZN(r);
        return (byte)r;
    }

    private void IncW(int c)
    {
        int v = Read(c) | Read((c + 1 & 0xff) + GetPage()) << 8;
        int r = (v + 1) & 0xffff;
        SetZN_W(r);
        Write(c, (byte)r);
        Write((c + 1 & 0xff) + GetPage(), (byte)(r >> 8));
    }

    private void Deca() => _a = Dec(_a);

    private void Decx() => _x = Dec(_x);

    private void Decy() => _y = Dec(_y);

    private void Decd(int b) => Write(b, Dec(Read(b)));

    private byte Dec(int c)
    {
        int r = c - 1;
        SetZN(r);
        return (byte)r;
    }

    private void DecW(int c)
    {
        int v = Read(c) | Read((c + 1 & 0xff) + GetPage()) << 8;
        int r = (v - 1) & 0xffff;
        SetZN_W(r);
        Write(c, (byte)r);
        Write((c + 1 & 0xff) + GetPage(), (byte)(r >> 8));
    }

    private void Call(int value)
    {
        Push((byte)(_pc >> 8));
        Push((byte)_pc);
        Idle();
        _pc = (ushort)value;
        Idle(); Idle();
    }

    private void Ret()
    {
        _pc = Pop();
        _pc |= (ushort)(Pop() << 8);
        Idle(); Idle();
    }

    private void Ret1()
    {
        _ps = Pop();
        _pc = Pop();
        _pc |= (ushort)(Pop() << 8);
        Idle(); Idle();
    }

    private void PCall(int op)
    {
        Push((byte)(_pc >> 8));
        Push((byte)(_pc + 1));
        Idle();
        _pc = (ushort)(0xff00 | ReadOpcode());
        Idle(); Idle();
    }

    private void TCall(int n)
    {
        Push((byte)(_pc >> 8));
        Push((byte)_pc);
        Idle();
        int a1 = 0xff00 | (0xde - n * 2);
        int value = Read(a1 & 0xffff);
        value |= Read((a1 + 1) & 0xffff) << 8;
        _pc = (ushort)value;
        Idle(); Idle();
    }

    private void Adci(int c) => _a = Adc(_a, c);

    private void Adca(int c) => _a = Adc(_a, Read(c));

    private void Adcxy((int a, int b) t) => Write(t.b, Adc(Read(t.b), t.a));

    private byte Adc(int c, int v)
    {
        int r = c + v + CF;
        SetH((c & 0xf) + (v & 0xf) + CF > 0xf);
        SetC(r > 0xff);
        SetV((~((byte)v ^ c) & ((byte)r ^ c) & 0x80) == 0x80);
        SetZN(r);
        return (byte)r;
    }

    private void AddW(int b)
    {
        int l = Read(b);
        int h = Read((b + 1 & 0xff) | GetPage());
        int v = (l | h << 8) & 0xffff;
        int ya = (A | Y << 8) & 0xffff;
        int r = ya + v;
        SetH((v & 0xfff) + (ya & 0xfff) > 0xfff);
        SetC(r > 0xffff);
        SetV((~(ya ^ v) & (ya ^ r) & 0x8000) > 0);
        _ps = (byte)((r & 0x8000) > 0 ? _ps |= FN : _ps & ~FN);
        _ps = (byte)((r & 0xffff) == 0 ? _ps |= FZ : _ps & ~FZ);
        _a = (byte)r;
        _y = (byte)(r >> 8);
    } //YA,d

    private void Sbci(int c) => _a = Sbc(_a, c);

    private void Sbca(int c) => _a = Sbc(_a, Read(c));

    private void Sbcxy((int a, int b) t) => Write(t.b, Sbc(Read(t.b), t.a));

    private byte Sbc(int c, int v)
    {
        v = ~v;
        int r = c + (byte)v + CF;
        SetH(((c & 0xf) + (v & 0xf) + CF) > 0xf);
        SetC(r > 0xff);
        SetV((~((v & 0xff) ^ c) & ((r & 0xff) ^ c) & 0x80) == 0x80);
        SetZN(r);
        return (byte)r;
    }

    private void SubW(int b)
    {
        int l = Read(b);
        int h = Read((b + 1 & 0xff) | GetPage());
        ushort v = (ushort)~(l | h << 8);
        int ya = (A | Y << 8) & 0xffff;
        int r = ya + v + 1;
        SetH(((v & 0xfff) + (ya & 0xfff) + 1) > 0xfff);
        SetC(r > 0xffff);
        SetV((~(ya ^ v) & (ya ^ r) & 0x8000) > 0);
        _ps = (byte)((r & 0x8000) > 0 ? _ps |= FN : _ps & ~FN);
        _ps = (byte)((r & 0xffff) == 0 ? _ps |= FZ : _ps & ~FZ);
        _a = (byte)r;
        _y = (byte)(r >> 8);
    } //YA,d

    private void Anda(int c) => AndI(A, Read(c));

    private void Andi(int c) => AndI(A, Read(c));

    private void Andxy((int a, int b) t) => Write(t.b, (byte)And(t.b, t.a));

    private int And(int a1, int c)
    {
        int r = c & Read(a1);
        SetZN(r);
        return r & 0xff;
    }

    private void AndI(int a1, int c)
    {
        int r = c & a1;
        SetZN(_a = (byte)r);
    }

    private void And1((int a, int b) t, bool reverse = false)
    {
        int c = _ps & FC;
        if (!reverse)
            c &= (Read(t.a) >> t.b) & 1;
        else
            c &= ~(Read(t.a) >> t.b) & 1;
        _ps = (byte)((_ps & 0xfe) | c);
    }

    private void Ora(int c) => _a = Or(_a, c);

    private void Ord(int c) => _a = Or(_a, Read(c));

    private void Orxy((int a, int b) t) => Write(t.b, (byte)Or(Read(t.b), t.a));

    private byte Or(int a1, int c)
    {
        int r = c | a1;
        SetZN(r);
        return (byte)r;
    }

    private void Eora(int c) => _a = Eor(_a, c);

    private void Eord(int c) => _a = Eor(_a, Read(c));

    private void Eorxy((int a, int b) t) => Write(t.b, (byte)Eor(Read(t.b), t.a));

    private byte Eor(int a1, int c)
    {
        int r = c ^ a1;
        SetZN(r);
        return (byte)r;
    }

    private void Eor1((int a, int b) t, bool reverse = false)
    {
        int c = _ps & FC;
        if (!reverse)
            c ^= (Read(t.a) >> t.b) & 1;
        else
            c ^= ~(Read(t.a) >> t.b) & 1;
        _ps = (byte)((_ps & 0xfe) | c);
    }

    private void Cmpi(int b) => Cmp(A, Read(b));

    private void Cmpxy((int a, int b) t) => Cmp(Read(t.b), t.a);

    private void Cmp(int c, int v)
    {
        int r = c - v;
        SetC(c >= v);
        SetZN(r);
    }

    private void CmpW(int c)
    {
        int ya = A | Y << 8;
        int v = Read(c) | Read((c + 1 & 0xff) + GetPage()) << 8;
        int r = ya - v;
        SetC(ya >= v);
        SetZN_W(r);
    }

    private void Movia(int c) => _a = Mov(c);

    private void Movix(int c) => _x = Mov(c);

    private void Moviy(int c) => _y = Mov(c);

    private void Movda(int c) => _a = Mov(Read(c));

    private void Movdx(int c) => _x = Mov(Read(c));

    private void Movdy(int c) => _y = Mov(Read(c));

    private byte Mov(int value)
    {
        SetZN(value);
        return (byte)value;
    }

    private void MovStda(int c, int r) => Movst(c, A);
    private void Movst(int a, int v) => Write(a, (byte)v);

    private void Bra(int a) => _pc += (ushort)(sbyte)a;

    private void Brc(int a, int op)
    {
        bool flag = op switch
        {
            0x10 => (_ps & FN) == 0,
            0x30 => (_ps & FN) != 0,
            0x50 => (_ps & FV) == 0,
            0x70 => (_ps & FV) != 0,
            0x90 => (_ps & FC) == 0,
            0xb0 => (_ps & FC) != 0,
            0xd0 => (_ps & FZ) == 0,
            0xf0 => (_ps & FZ) != 0,
            _ => false,
        };

        if (flag)
            Bra(a);
    }

    private void Cbne((int b, int c) t)
    {
        if (A != t.b)
            Bra(t.c);
    }

    private void Clr1(int v, int bit) => Write(v, (byte)(Read(v) & ~bit));

    private void Set1(int v, int bit) => Write(v, (byte)(Read(v) | bit));

    private void Mov1((int a, int b) t, bool reverse = false)
    {
        int c = _ps & FC;
        if (!reverse)
        {
            int v = Read(t.a);
            c = v >> t.b & 1;
            _ps = (byte)((_ps & 0xfe) | c);
        }
        else
        {
            int v = Read(t.a);
            int b = c != 0 ? v | 1 << t.b : v & ~(1 << t.b);
            Write(t.a, (byte)b);
        }
    }

    private void Div()
    {
        int ya = (Y << 8 | A) & 0xffff;
        int r = X != 0 ? ya / X : 0x1ff;
        int m = X != 0 ? ya % X : Y;
        SetH((Y & 0xf) >= (X & 0xf));
        SetV(r > 0xff);
        _a = (byte)r;
        _y = (byte)m;
        SetZN(_a);
    }

    private void Mul()
    {
        int v = Y * A;
        _a = (byte)v;
        _y = (byte)(v >> 8);
        SetZN(Y);
    }

    private void Not1((int a, int b) t)
    {
        int c = Read(t.a);
        c ^= (1 << t.b) & 0xff;
        Write(t.a, (byte)c);
    }

    private void Or1((int a, int b) t, bool reverse = false)
    {
        int c = _ps & FC;
        if (!reverse)
        {
            int v = Read(t.a);
            c |= v >> t.b & 1;
            _ps = (byte)((_ps & 0xfe) | c);
        }
        else
        {
            int v = Read(t.a);
            c |= ~(v >> t.b) & 1;
            _ps = (byte)((_ps & 0xfe) | c);
        }
    }

    private void Tclr1(int b)
    {
        int v = Read(b);
        int r = v & (byte)~A;
        SetZN(A - v);
        Write(b, (byte)r);
    }

    private void TSet1(int b)
    {
        int v = Read(b);
        int r = v | A;
        SetZN(A - v);
        Write(b, (byte)r);
    }

    private void Xcn()
    {
        _a = (byte)((_a & 0x0f) << 4 | (_a & 0xf0) >> 4);
        SetZN(_a);
    }

    private void Daa()
    {
        int v = A;
        int cf = CF;
        if (HF > 0 || (A & 0xf) > 9)
            v += 6;
        if (cf > 0 || A > 0x99)
        {
            v += 0x60;
            CF = 1;
        }

        SetZN(v);
        _a = (byte)v;
    }

    private void Das()
    {
        int value = A;
        int cf = HF;
        if (HF == 0 || (A & 0xf) > 9)
            value -= 6;
        if (cf == 0 || A > 0x99)
        {
            value -= 0x60;
            CF = 0;
        }

        SetZN(value);
        _a = (byte)value;
    }
}
