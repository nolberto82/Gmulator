namespace Gmulator.Core.Snes;
public partial class SnesSpc
{
    private void Asla() => a = Asl(a);

    private void Asld(int c) => Write(c, Asl(Read(c)));

    private int Asl(int c)
    {
        int r = (c << 1) & 0xfe;
        SetC((c & 0x80) > 0);
        SetZN(r);
        return r & 0xff;
    }

    private void Lsra() => a = Lsr(a);

    private void Lsrd(int c) => Write(c, Lsr(Read(c)));

    private int Lsr(int c)
    {
        SetC((c & 0x01) > 0);
        int r = (c >> 1) & 0x7f;
        SetZN(r);
        return r & 0xff;
    }

    private void Rola() => a = Rol(a);

    private void Rold(int c) => Write(c, Rol(Read(c)));

    private int Rol(int c)
    {
        int r = (c << 1) | CF;
        SetC((c & 0x80) > 0);
        SetZN(r);
        return r & 0xff;
    }

    private void Rora() => a = Ror(a);

    private void Rord(int c) => Write(c, Ror(Read(c)));

    private int Ror(int c)
    {
        int bit0 = c & 1;
        int r = (c >> 1) | (CF == 1 ? 0x80 : 0x00);
        SetC(bit0 == 1);
        SetZN(r);
        return r & 0xff;
    }

    private void Bbc((int a, int b) t, int bit)
    {
        if ((t.a & bit) == 0)
            pc += (sbyte)t.b;
    }

    private void Bbs((int a, int b) t, int bit)
    {
        if ((t.a & bit) == bit)
            pc += (sbyte)t.b;
    }

    private void Dbnzy(int b)
    {
        Y--;
        Dbnz(y, b);
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
            pc += (sbyte)b;
    }

    private void Brk()
    {
        Push(pc >> 8);
        Push(pc);
        Push(p);
        p |= FB;
        p = p & ~FI;
        pc = Read(0xffde) | Read(0xffdf) << 8;
        Idle(); Idle();
    }

    private void Inca() => a = Inc(a);

    private void Incx() => x = Inc(x);

    private void Incy() => y = Inc(y);

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

    private void Deca() => a = Dec(a);

    private void Decx() => x = Dec(x);

    private void Decy() => y = Dec(y);

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
        Push(pc >> 8);
        Push(pc);
        Idle();
        pc = v;
        Idle(); Idle();
    }

    private void Ret()
    {
        pc = Pop();
        pc |= Pop() << 8;
        Idle(); Idle();
    }

    private void Ret1()
    {
        p = Pop();
        pc = Pop();
        pc |= Pop() << 8;
        Idle(); Idle();
    }

    private void PCall(int op)
    {
        Push((byte)(pc >> 8));
        Push((byte)(pc + 1));
        Idle();
        pc = 0xff00 | ReadOp();
        Idle(); Idle();
    }

    private void TCall(int n)
    {
        Push((byte)(pc >> 8));
        Push((byte)pc);
        Idle();
        int a1 = 0xff00 | (0xde - n * 2);
        int v = Read((a1) & 0xffff);
        v |= Read((a1 + 1) & 0xffff) << 8;
        pc = v;
        Idle(); Idle();
    }

    private void Adci(int c) => a = Adc(a, c);

    private void Adca(int c) => a = Adc(a, Read(c));

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
        int ya = (a | y << 8) & 0xffff;
        int r = ya + v;
        SetH((v & 0xfff) + (ya & 0xfff) > 0xfff);
        SetC(r > 0xffff);
        SetV((~(ya ^ v) & (ya ^ r) & 0x8000) > 0);
        PS = (r & 0x8000) > 0 ? PS |= FN : PS &= ~FN;
        PS = (r & 0xffff) == 0 ? PS |= FZ : PS &= ~FZ;
        a = (byte)r;
        y = (byte)(r >> 8);
    } //YA,d

    private void Sbci(int c) => a = Sbc(a, c);

    private void Sbca(int c) => a = Sbc(a, Read(c));

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
        int ya = (a | y << 8) & 0xffff;
        int r = ya + v + 1;
        SetH(((v & 0xfff) + (ya & 0xfff) + 1) > 0xfff);
        SetC(r > 0xffff);
        SetV((~(ya ^ v) & (ya ^ r) & 0x8000) > 0);
        PS = (r & 0x8000) > 0 ? PS |= FN : PS &= ~FN;
        PS = (r & 0xffff) == 0 ? PS |= FZ : PS &= ~FZ;
        A = r;
        Y = (r >> 8);
    } //YA,d

    private void Anda(int c) => AndI(a, Read(c));

    private void Andi(int c) => AndI(a, Read(c));

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
        SetZN(a = r & 0xff);
    }

    private void And1((int a, int b) t, bool reverse = false)
    {
        int c = p & FC;
        if (!reverse)
            c &= (Read(t.a) >> t.b) & 1;
        else
            c &= ~(Read(t.a) >> t.b) & 1;
        PS = (PS & 0xfe) | c;
    }

    private void Ora(int c) => a = Or(a, c);

    private void Ord(int c) => a = Or(a, Read(c));

    private void Orxy((int a, int b) t) => Write(t.b, Or(Read(t.b), t.a));

    private int Or(int a1, int c)
    {
        int r = c | a1;
        SetZN(r);
        return r & 0xff;
    }

    private void Eora(int c) => a = Eor(a, c);

    private void Eord(int c) => a = Eor(a, Read(c));

    private void Eorxy((int a, int b) t) => Write(t.b, Eor(Read(t.b), t.a));

    private int Eor(int a1, int c)
    {
        int r = c ^ a1;
        SetZN(r);
        return r & 0xff;
    }

    private void Eor1((int a, int b) t, bool reverse = false)
    {
        int c = p & FC;
        if (!reverse)
            c ^= (Read(t.a) >> t.b) & 1;
        else
            c ^= ~(Read(t.a) >> t.b) & 1;
        PS = (PS & 0xfe) | c;
    }

    private void Cmpi(int b) => Cmp(a, Read(b));

    private void Cmpxy((int a, int b) t) => Cmp(Read(t.b), t.a);

    private void Cmp(int c, int v)
    {
        int r = c - v;
        SetC(c >= v);
        SetZN(r);
    }

    private void CmpW(int c)
    {
        int ya = a | y << 8;
        int v = Read(c) | Read((c + 1 & 0xff) + GetPage()) << 8;
        int r = ya - v;
        SetC(ya >= v);
        SetZN_W(r);
    }

    private void Movia(int c) => a = Mov(c);

    private void Movix(int c) => x = Mov(c);

    private void Moviy(int c) => y = Mov(c);

    private void Movda(int c) => a = Mov(Read(c));

    private void Movdx(int c) => x = Mov(Read(c));

    private void Movdy(int c) => y = Mov(Read(c));

    private int Mov(int c)
    {
        SetZN(c);
        return c & 0xff;
    }

    private void MovStda(int c, int r) => Movst(c, a);
    private void Movst(int a, int v) => Write(a, v);

    private void Bra(int a) => PC += (sbyte)a;

    private void Brc(int a, int op)
    {
        bool flag = op switch
        {
            0x10 => (PS & FN) == 0,
            0x30 => (PS & FN) != 0,
            0x50 => (PS & FV) == 0,
            0x70 => (PS & FV) != 0,
            0x90 => (PS & FC) == 0,
            0xb0 => (PS & FC) != 0,
            0xd0 => (PS & FZ) == 0,
            0xf0 => (PS & FZ) != 0,
            _ => false,
        };

        if (flag)
            Bra(a);
    }

    private void Cbne((int b, int c) t)
    {
        if (a != t.b)
            Bra(t.c);
    }

    private void Clr1(int v, int bit) => Write(v, Read(v) & ~bit);

    private void Set1(int v, int bit) => Write(v, Read(v) | bit);

    private void Mov1((int a, int b) t, bool reverse = false)
    {
        int c = p & FC;
        if (!reverse)
        {
            int v = Read(t.a);
            c = (v >> t.b & 1);
            PS = (PS & 0xfe) | c;
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
        int ya = (y << 8 | a) & 0xffff;
        int r = x != 0 ? ya / x : 0x1ff;
        int m = (x != 0 ? ya % x : y);
        SetH((y & 0xf) >= (x & 0xf));
        SetV(r > 0xff);
        A = r;
        Y = m;
        SetZN(a);
    }

    private void Mul()
    {
        int v = (y * a);
        A = v;
        Y = v >> 8;
        SetZN(y);
    }

    private void Not1((int a, int b) t)
    {
        int c = Read(t.a);
        c ^= (1 << t.b) & 0xff;
        Write(t.a, c);
    }

    private void Or1((int a, int b) t, bool reverse = false)
    {
        int c = p & FC;
        if (!reverse)
        {
            int v = Read(t.a);
            c |= (v >> t.b & 1);
            PS = (PS & 0xfe) | c;
        }
        else
        {
            int v = Read(t.a);
            c |= ~(v >> t.b) & 1;
            PS = (PS & 0xfe) | c;
        }
    }

    private void Tclr1(int b)
    {
        int v = Read(b);
        int r = v & (byte)~a;
        SetZN(a - v);
        Write(b, r);
    }

    private void TSet1(int b)
    {
        int v = Read(b);
        int r = v | a;
        SetZN(a - v);
        Write(b, r);
    }

    private void Xcn()
    {
        a = (a & 0x0f) << 4 | (a & 0xf0) >> 4;
        SetZN(a);
    }

    private void Daa()
    {
        int v = a;
        int cf = CF;
        if (HF > 0 || (a & 0xf) > 9)
            v += 6;
        if (cf > 0 || a > 0x99)
        {
            v += 0x60;
            CF = 1;
        }

        SetZN(v);
        A = v;
    }

    private void Das()
    {
        int v = a;
        int cf = HF;
        if (HF == 0 || (a & 0xf) > 9)
            v -= 6;
        if (cf == 0 || a > 0x99)
        {
            v -= 0x60;
            CF = 0;
        }

        SetZN(v);
        A = v;
    }
}
