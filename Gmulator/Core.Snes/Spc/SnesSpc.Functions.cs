namespace Gmulator.Core.Snes;
public partial class SnesSpc
{
    private void Asla() => _state.A = Asl(_state.A);

    private void Asld(int c) => Write(c, Asl(Read(c)));

    private int Asl(int c)
    {
        int r = (c << 1) & 0xfe;
        SetC((c & 0x80) > 0);
        SetZN(r);
        return r & 0xff;
    }

    private void Lsra() => _state.A = Lsr(_state.A);

    private void Lsrd(int c) => Write(c, Lsr(Read(c)));

    private int Lsr(int c)
    {
        SetC((c & 0x01) > 0);
        int r = (c >> 1) & 0x7f;
        SetZN(r);
        return r & 0xff;
    }

    private void Rola() => _state.A = Rol(_state.A);

    private void Rold(int c) => Write(c, Rol(Read(c)));

    private int Rol(int c)
    {
        int r = (c << 1) | _state.CF;
        SetC((c & 0x80) > 0);
        SetZN(r);
        return r & 0xff;
    }

    private void Rora() => _state.A = Ror(_state.A);

    private void Rord(int c) => Write(c, Ror(Read(c)));

    private int Ror(int c)
    {
        int bit0 = c & 1;
        int r = (c >> 1) | (_state.CF == 1 ? 0x80 : 0x00);
        SetC(bit0 == 1);
        SetZN(r);
        return r & 0xff;
    }

    private void Bbc((int a, int b) t, int bit)
    {
        if ((t.a & bit) == 0)
            _state.PC += (sbyte)t.b;
    }

    private void Bbs((int a, int b) t, int bit)
    {
        if ((t.a & bit) == bit)
            _state.PC += (sbyte)t.b;
    }

    private void Dbnzy(int b)
    {
        _state.Y--;
        Dbnz(_state.Y, b);
    }

    private void Dbnzd((int a, int b) t)
    {
        int da = t.a | GetPage();
        int v = (Read(da) - 1) & 0xff;
        Dbnz(v, t.b);
        Write(da, v);
    }
    private void Dbnz(int r, int b)
    {
        if (r > 0)
            _state.PC += (sbyte)b;
    }

    private void Brk()
    {
        Push(_state.PC >> 8);
        Push(_state.PC);
        Push(_state.PS);
        _state.PS |= FB;
        _state.PS = _state.PS & ~FI;
        _state.PC = Read(0xffde) | Read(0xffdf) << 8;
        Idle(); Idle();
    }

    private void Inca() => _state.A = Inc(_state.A);

    private void Incx() => _state.X = Inc(_state.X);

    private void Incy() => _state.Y = Inc(_state.Y);

    private void Incd(int b) => Write(b, Inc(Read(b)));

    private int Inc(int c)
    {
        int r = c + 1;
        SetZN(r);
        return r & 0xff;
    }

    private void IncW(int c)
    {
        int v = Read(c) | Read((c + 1 & 0xff) + GetPage()) << 8;
        int r = (v + 1) & 0xffff;
        SetZN_W(r);
        Write(c, (byte)r);
        Write((c + 1 & 0xff) + GetPage(), r >> 8);
    }

    private void Deca() => _state.A = Dec(_state.A);

    private void Decx() => _state.X = Dec(_state.X);

    private void Decy() => _state.Y = Dec(_state.Y);

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
        Write(c, r);
        Write((c + 1 & 0xff) + GetPage(), (byte)(r >> 8));
    }

    private void Call(int v)
    {
        Push(_state.PC >> 8);
        Push(_state.PC);
        Idle();
        _state.PC = v;
        Idle(); Idle();
    }

    private void Ret()
    {
        _state.PC = Pop();
        _state.PC |= Pop() << 8;
        Idle(); Idle();
    }

    private void Ret1()
    {
        _state.PS = Pop();
        _state.PC = Pop();
        _state.PC |= Pop() << 8;
        Idle(); Idle();
    }

    private void PCall(int op)
    {
        Push((byte)(_state.PC >> 8));
        Push((byte)(_state.PC + 1));
        Idle();
        _state.PC = 0xff00 | ReadOp();
        Idle(); Idle();
    }

    private void TCall(int n)
    {
        Push((byte)(_state.PC >> 8));
        Push((byte)_state.PC);
        Idle();
        int a1 = 0xff00 | (0xde - n * 2);
        int v = Read(a1 & 0xffff);
        v |= Read((a1 + 1) & 0xffff) << 8;
        _state.PC = v;
        Idle(); Idle();
    }

    private void Adci(int c) => _state.A = Adc(_state.A, c);

    private void Adca(int c) => _state.A = Adc(_state.A, Read(c));

    private void Adcxy((int a, int b) t) => Write(t.b, Adc(Read(t.b), t.a));

    private byte Adc(int c, int v)
    {
        int r = c + v + _state.CF;
        SetH((c & 0xf) + (v & 0xf) + _state.CF > 0xf);
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
        int ya = (_state.A | _state.Y << 8) & 0xffff;
        int r = ya + v;
        SetH((v & 0xfff) + (ya & 0xfff) > 0xfff);
        SetC(r > 0xffff);
        SetV((~(ya ^ v) & (ya ^ r) & 0x8000) > 0);
        _state.PS = (r & 0x8000) > 0 ? _state.PS |= FN : _state.PS &= ~FN;
        _state.PS = (r & 0xffff) == 0 ? _state.PS |= FZ : _state.PS &= ~FZ;
        _state.A = (byte)r;
        _state.Y = (byte)(r >> 8);
    } //YA,d

    private void Sbci(int c) => _state.A = Sbc(_state.A, c);

    private void Sbca(int c) => _state.A = Sbc(_state.A, Read(c));

    private void Sbcxy((int a, int b) t) => Write(t.b, Sbc(Read(t.b), t.a));

    private byte Sbc(int c, int v)
    {
        v = ~v;
        int r = c + (byte)v + _state.CF;
        SetH(((c & 0xf) + (v & 0xf) + _state.CF) > 0xf);
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
        int ya = (_state.A | _state.Y << 8) & 0xffff;
        int r = ya + v + 1;
        SetH(((v & 0xfff) + (ya & 0xfff) + 1) > 0xfff);
        SetC(r > 0xffff);
        SetV((~(ya ^ v) & (ya ^ r) & 0x8000) > 0);
        _state.PS = (r & 0x8000) > 0 ? _state.PS |= FN : _state.PS &= ~FN;
        _state.PS = (r & 0xffff) == 0 ? _state.PS |= FZ : _state.PS &= ~FZ;
        _state.A = r;
        _state.Y = r >> 8;
    } //YA,d

    private void Anda(int c) => AndI(_state.A, Read(c));

    private void Andi(int c) => AndI(_state.A, Read(c));

    private void Andxy((int a, int b) t) => Write(t.b, And(t.b, t.a));

    private int And(int a1, int c)
    {
        int r = c & Read(a1);
        SetZN(r);
        return r & 0xff;
    }

    private void AndI(int a1, int c)
    {
        int r = c & a1;
        SetZN(_state.A = r & 0xff);
    }

    private void And1((int a, int b) t, bool reverse = false)
    {
        int c = _state.PS & FC;
        if (!reverse)
            c &= (Read(t.a) >> t.b) & 1;
        else
            c &= ~(Read(t.a) >> t.b) & 1;
        _state.PS = (_state.PS & 0xfe) | c;
    }

    private void Ora(int c) => _state.A = Or(_state.A, c);

    private void Ord(int c) => _state.A = Or(_state.A, Read(c));

    private void Orxy((int a, int b) t) => Write(t.b, Or(Read(t.b), t.a));

    private int Or(int a1, int c)
    {
        int r = c | a1;
        SetZN(r);
        return r & 0xff;
    }

    private void Eora(int c) => _state.A = Eor(_state.A, c);

    private void Eord(int c) => _state.A = Eor(_state.A, Read(c));

    private void Eorxy((int a, int b) t) => Write(t.b, Eor(Read(t.b), t.a));

    private int Eor(int a1, int c)
    {
        int r = c ^ a1;
        SetZN(r);
        return r & 0xff;
    }

    private void Eor1((int a, int b) t, bool reverse = false)
    {
        int c = _state.PS & FC;
        if (!reverse)
            c ^= (Read(t.a) >> t.b) & 1;
        else
            c ^= ~(Read(t.a) >> t.b) & 1;
        _state.PS = (_state.PS & 0xfe) | c;
    }

    private void Cmpi(int b) => Cmp(_state.A, Read(b));

    private void Cmpxy((int a, int b) t) => Cmp(Read(t.b), t.a);

    private void Cmp(int c, int v)
    {
        int r = c - v;
        SetC(c >= v);
        SetZN(r);
    }

    private void CmpW(int c)
    {
        int ya = _state.A | _state.Y << 8;
        int v = Read(c) | Read((c + 1 & 0xff) + GetPage()) << 8;
        int r = ya - v;
        SetC(ya >= v);
        SetZN_W(r);
    }

    private void Movia(int c) => _state.A = Mov(c);

    private void Movix(int c) => _state.X = Mov(c);

    private void Moviy(int c) => _state.Y = Mov(c);

    private void Movda(int c) => _state.A = Mov(Read(c));

    private void Movdx(int c) => _state.X = Mov(Read(c));

    private void Movdy(int c) => _state.Y = Mov(Read(c));

    private int Mov(int c)
    {
        SetZN(c);
        return c & 0xff;
    }

    private void MovStda(int c, int r) => Movst(c, _state.A);
    private void Movst(int a, int v) => Write(a, v);

    private void Bra(int a) => _state.PC += (sbyte)a;

    private void Brc(int a, int op)
    {
        bool flag = op switch
        {
            0x10 => (_state.PS & FN) == 0,
            0x30 => (_state.PS & FN) != 0,
            0x50 => (_state.PS & FV) == 0,
            0x70 => (_state.PS & FV) != 0,
            0x90 => (_state.PS & FC) == 0,
            0xb0 => (_state.PS & FC) != 0,
            0xd0 => (_state.PS & FZ) == 0,
            0xf0 => (_state.PS & FZ) != 0,
            _ => false,
        };

        if (flag)
            Bra(a);
    }

    private void Cbne((int b, int c) t)
    {
        if (_state.A != t.b)
            Bra(t.c);
    }

    private void Clr1(int v, int bit) => Write(v, Read(v) & ~bit);

    private void Set1(int v, int bit) => Write(v, Read(v) | bit);

    private void Mov1((int a, int b) t, bool reverse = false)
    {
        int c = _state.PS & FC;
        if (!reverse)
        {
            int v = Read(t.a);
            c = v >> t.b & 1;
            _state.PS = (_state.PS & 0xfe) | c;
        }
        else
        {
            int v = Read(t.a);
            int b = c != 0 ? v | 1 << t.b : v & ~(1 << t.b);
            Write(t.a, b);
        }
    }

    private void Div()
    {
        int ya = (_state.Y << 8 | _state.A) & 0xffff;
        int r = _state.X != 0 ? ya / _state.X : 0x1ff;
        int m = _state.X != 0 ? ya % _state.X : _state.Y;
        SetH((_state.Y & 0xf) >= (_state.X & 0xf));
        SetV(r > 0xff);
        _state.A = r;
        _state.Y = m;
        SetZN(_state.A);
    }

    private void Mul()
    {
        int v = _state.Y * _state.A;
        _state.A = v;
        _state.Y = v >> 8;
        SetZN(_state.Y);
    }

    private void Not1((int a, int b) t)
    {
        int c = Read(t.a);
        c ^= (1 << t.b) & 0xff;
        Write(t.a, c);
    }

    private void Or1((int a, int b) t, bool reverse = false)
    {
        int c = _state.PS & FC;
        if (!reverse)
        {
            int v = Read(t.a);
            c |= v >> t.b & 1;
            _state.PS = (_state.PS & 0xfe) | c;
        }
        else
        {
            int v = Read(t.a);
            c |= ~(v >> t.b) & 1;
            _state.PS = (_state.PS & 0xfe) | c;
        }
    }

    private void Tclr1(int b)
    {
        int v = Read(b);
        int r = v & (byte)~_state.A;
        SetZN(_state.A - v);
        Write(b, r);
    }

    private void TSet1(int b)
    {
        int v = Read(b);
        int r = v | _state.A;
        SetZN(_state.A - v);
        Write(b, r);
    }

    private void Xcn()
    {
        _state.A = (_state.A & 0x0f) << 4 | (_state.A & 0xf0) >> 4;
        SetZN(_state.A);
    }

    private void Daa()
    {
        int v = _state.A;
        int cf = _state.CF;
        if (_state.HF > 0 || (_state.A & 0xf) > 9)
            v += 6;
        if (cf > 0 || _state.A > 0x99)
        {
            v += 0x60;
            _state.CF = 1;
        }

        SetZN(v);
        _state.A = v;
    }

    private void Das()
    {
        int v = _state.A;
        int cf = _state.HF;
        if (_state.HF == 0 || (_state.A & 0xf) > 9)
            v -= 6;
        if (cf == 0 || _state.A > 0x99)
        {
            v -= 0x60;
            _state.CF = 0;
        }

        SetZN(v);
        _state.A = v;
    }
}
