using Gmulator.Interfaces;

namespace Gmulator.Core.Snes;
public partial class SnesSpc : ISaveState
{
    private const int FC = 1 << 0;
    private const int FZ = 1 << 1;
    private const int FI = 1 << 2;
    private const int FH = 1 << 3;
    private const int FB = 1 << 4;
    private const int FP = 1 << 5;
    private const int FV = 1 << 6;
    private const int FN = 1 << 7;
    private int pc, sp, a, x, y, p;
    private bool[] flags;

    public int PC { get => pc & 0xffff; set => pc = value & 0xffff; }
    public int SP { get => sp; set => sp = value & 0xff; }
    public int A { get => a & 0xff; set => a = value & 0xff; }
    public int X { get => x & 0xff; set => x = value & 0xff; }
    public int Y { get => y & 0xff; set => y = value & 0xff; }
    public int PS { get => p & 0xff; set => p = value & 0xff; }

    public bool[] Flags { get => flags; set => flags = value; }
    public int CF { get => (p & FC) & 0xff; set => p = ((p & 0xfe) | value) & 0xff; }
    public int HF { get => (p & FH) & 0xff; }

    public int StepOverAddr { get; set; }
    public int TestAddr { get; set; }
    public bool Stepped { get; set; }

    private readonly Snes Snes;
    private SnesApu Apu;

    public SnesSpc()
    {
        CreateOpcodes();
        flags = new bool[8];
        StepOverAddr = -1;
    }

    public void SetSnes(Snes snes)
    {
        Apu = snes.Apu;
    }

    public ushort GetPage() => (ushort)(((PS & FP) != 0 ? 0x100 : 0));

    private void Idle() => Apu.Idle();

    public int ReadOp(bool debug = false)
    {
        if (!debug)
            Apu.Cycle();

        pc = (pc + 1) & 0xffff;
        return Read(pc - 1);
    }

    public int Read(int a) => Apu.Read(a);

    public void Write(int a, int v) => Apu.Write(a, v);

    private int ReadWord(int a) => (Read(a) | Read(a + 1) << 8) & 0xffff;

    public void Step()
    {
#if DEBUG
        if (PC == 0x316)
        {
            var a = (Read(0x100 + SP + 1) | Read(0x100 + SP + 2) << 8);
            if (a > 0)
                TestAddr = a;
        }
#endif

        int a1, a2;
        int op = Read(PC++);
        switch (op)
        {
            //8-bit Data Transmission (Read)
            case 0xE8: Movia(Imm()); break;
            case 0xE6: Movda(x); break;
            case 0xBF: Movda(x++); break;
            case 0xE4: Movda(Dir()); break;
            case 0xF4: Movda(DirIdX()); break;
            case 0xE5: Movda(Abs()); break;
            case 0xF5: Movda(AbsX()); break;
            case 0xF6: Movda(AbsY()); break;
            case 0xE7: Movda(DirIdXInd()); break;
            case 0xF7: Movda(DirIndY()); break;
            case 0xCD: Movix(Imm()); break;
            case 0xF8: Movdx(Dir()); break;
            case 0xF9: Movdx(DirIdY()); break;
            case 0xE9: Movdx(Abs()); break;
            case 0x8D: Moviy(Imm()); break;
            case 0xEB: Movdy(Dir()); break;
            case 0xFB: Movdy(DirIdX()); break;
            case 0xEC: Movdy(Abs()); break;

            //8-bit Data Transmission (Write)
            case 0xC6: Write(x, a); break;
            case 0xAF: Write(x++, a); break;
            case 0xC4: Write(Dir(), a); break;
            case 0xD4: MovStda(DirIdX(), 0); break;
            case 0xC5: Write(Abs(), a); break;
            case 0xD5: Write(AbsX(), a); break;
            case 0xD6: Write(AbsY(), a); break;
            case 0xC7: MovStda(DirIdXInd(), x); break;
            case 0xD7: Write(DirIndY(), a); break;
            case 0xD8: Write(Dir(), x); break;
            case 0xD9: Write(DirIdY(), x); break;
            case 0xC9: Write(Abs(), x); break;
            case 0xCB: Write(Dir(), y); break;
            case 0xDB: Write(DirIdX(), y); break;
            case 0xCC: Write(Abs(), y); break;

            //8-bit Data Transmission (Reg->Reg, Mem->Mem)
            case 0x7D: a = x; SetZN(a); break;
            case 0xDD: a = y; SetZN(a); break;
            case 0x5D: x = a; SetZN(x); break;
            case 0xFD: y = a; SetZN(y); break;
            case 0x9D: x = sp; SetZN(x); break;
            case 0xBD: sp = x; break;
            case 0xFA:
                (a1, a2) = DirDir();
                Write(a2, a1);
                break;
            case 0x8F:
                (a1, a2) = DirImm();
                Write(a2, a1);
                break;

            //8-bit Arithmetic
            case 0x88: Adci(Imm()); break;
            case 0x86: Adca(x); break;
            case 0x84: Adca(Dir()); break;
            case 0x94: Adca(DirIdX()); break;
            case 0x85: Adca(Abs()); break;
            case 0x95: Adca(AbsX()); break;
            case 0x96: Adca(AbsY()); break;
            case 0x87: Adca(DirIdXInd()); break;
            case 0x97: Adca(DirIndY()); break;
            case 0x99: Adcxy(IndXY()); break;
            case 0x89: Adcxy(DirDir()); break;
            case 0x98: Adcxy(DirImm()); break;
            case 0xA8: Sbci(Imm()); break;
            case 0xA6: Sbca(x); break;
            case 0xA4: Sbca(Dir()); break;
            case 0xB4: Sbca(DirIdX()); break;
            case 0xA5: Sbca(Abs()); break;
            case 0xB5: Sbca(AbsX()); break;
            case 0xB6: Sbca(AbsY()); break;
            case 0xA7: Sbca(DirIdXInd()); break;
            case 0xB7: Sbca(DirIndY()); break;
            case 0xB9: Sbcxy(IndXY()); break;
            case 0xA9: Sbcxy(DirDir()); break;
            case 0xB8: Sbcxy(DirImm()); break;
            case 0x68: Cmp(a, Imm()); break;
            case 0x66: Cmp(a, Read(x)); break;
            case 0x64: Cmpi(Dir()); break;
            case 0x74: Cmpi(DirIdX()); break;
            case 0x65: Cmpi(Abs()); break;
            case 0x75: Cmpi(AbsX()); break;
            case 0x76: Cmpi(AbsY()); break;
            case 0x67: Cmpi(DirIdXInd()); break;
            case 0x77: Cmpi(DirIndY()); break;
            case 0x79: Cmpxy(IndXY()); break;
            case 0x69: Cmpxy(DirDir()); break;
            case 0x78: (a1, a2) = DirImm(); Cmp(Read(a2), a1); break;
            case 0xC8: Cmp(x, Imm()); break;
            case 0x3E: Cmp(x, Read(Dir())); break;
            case 0x1E: Cmp(x, Read(Abs())); break;
            case 0xAD: Cmp(y, Imm()); break;
            case 0x7E: Cmp(y, Read(Dir())); break;
            case 0x5E: Cmp(y, Read(Abs())); break;

            //8-bit Logical Operations
            case 0x28: AndI(Imm(), a); break;
            case 0x26: Anda(x); break;
            case 0x24: Andi(Dir()); break;
            case 0x34: Anda(DirIdX()); break;
            case 0x25: Anda(Abs()); break;
            case 0x35: Anda(AbsX()); break;
            case 0x36: Anda(AbsY()); break;
            case 0x27: Andi(DirIdXInd()); break;
            case 0x37: Andi(DirIndY()); break;
            case 0x39: Andxy(IndXY()); break;
            case 0x29: Andxy(DirDir()); break;
            case 0x38: Andxy(DirImm()); break;
            case 0x08: Ora(Imm()); break;
            case 0x06: Ora(x); break;
            case 0x04: Ord(Dir()); break;
            case 0x14: Ora(DirIdX()); break;
            case 0x05: Ord(Abs()); break;
            case 0x15: Ora(AbsX()); break;
            case 0x16: Ora(AbsY()); break;
            case 0x07: Ora(DirIdXInd()); break;
            case 0x17: Ora(DirIndY()); break;
            case 0x19: Orxy(IndXY()); break;
            case 0x09: Orxy(DirDir()); break;
            case 0x18: Orxy(DirImm()); break;
            case 0x48: Eora(Imm()); break;
            case 0x46: Eord(x); break;
            case 0x44: Eord(Dir()); break;
            case 0x54: Eord(DirIdX()); break;
            case 0x45: Eord(Abs()); break;
            case 0x55: Eord(AbsX()); break;
            case 0x56: Eord(AbsY()); break;
            case 0x47: Eord(DirIdXInd()); break;
            case 0x57: Eord(DirIndY()); break;
            case 0x59: Eorxy(IndXY()); break;
            case 0x49: Eorxy(DirDir()); break;
            case 0x58: Eorxy(DirImm()); break;

            //8-bit Increment / Decrement Operations
            case 0xBC: Inca(); break;
            case 0xAB: Incd(Dir()); break;
            case 0xBB: Incd(DirIdX()); break;
            case 0xAC: Incd(Abs()); break;
            case 0x3D: Incx(); break;
            case 0xFC: Incy(); break;
            case 0x9C: Deca(); break;
            case 0x8B: Decd(Dir()); break;
            case 0x9B: Decd(DirIdX()); break;
            case 0x8C: Decd(Abs()); break;
            case 0x1D: Decx(); break;
            case 0xDC: Decy(); break;

            //8-bit Shift / Rotation Operations
            case 0x1C: Asla(); break;
            case 0x0B: Asld(Dir()); break;
            case 0x1B: Asld(DirIdX()); break;
            case 0x0C: Asld(Abs()); break;
            case 0x5C: Lsra(); break;
            case 0x4B: Lsrd(Dir()); break;
            case 0x5B: Lsrd(DirIdX()); break;
            case 0x4C: Lsrd(Abs()); break;
            case 0x3C: Rola(); break;
            case 0x2B: Rold(Dir()); break;
            case 0x3B: Rold(DirIdX()); break;
            case 0x2C: Rold(Abs()); break;
            case 0x7C: Rora(); break;
            case 0x6B: Rord(Dir()); break;
            case 0x7B: Rord(DirIdX()); break;
            case 0x6C: Rord(Abs()); break;
            case 0x9F: Xcn(); break;

            //16-bit Data Transmission Operations
            case 0xBA:
                a1 = Dir();
                a = Read(a1);
                y = Read((a1 + 1 & 0xff) | GetPage());
                SetZN(y << 8 | a);
                break;
            case 0xDA:
                a1 = Dir();
                Write(a1, a); Write(a1 + 1 & 0xff | GetPage(), y);
                break;

            //16-bit Arithmetic Operations
            case 0x3A: IncW(Dir()); break;
            case 0x1A: DecW(Dir()); break;
            case 0x7A: AddW(Dir()); break;
            case 0x9A: SubW(Dir()); break;
            case 0x5A: CmpW(Dir()); break;

            //Multiplication / Division Operations
            case 0xCF: Mul(); break;
            case 0x9E: Div(); break;

            //Decimal Compensation Operations
            case 0xDF: Daa(); break;
            case 0xBE: Das(); break;

            //Program Flow Operations
            case 0x2F: Bra(Rel()); break;
            case 0xF0 or 0xD0 or 0xB0 or 0x90 or
                 0x70 or 0x50 or 0x30 or 0x10:
                Brc(Rel(), op); break;
            case 0x03 or 0x23 or 0x43 or 0x63 or
                 0x83 or 0xa3 or 0xc3 or 0xe3:
                Bbs(DirBit(), 1 << (op >> 5));
                break;
            case 0x13 or 0x33 or 0x53 or 0x73 or
                 0x93 or 0xb3 or 0xd3 or 0xf3:
                Bbc(DirBit(), 1 << (op >> 5));
                break;
            case 0x2E: Cbne(DirBit()); break;
            case 0xDE: Cbne((DirBit(0xde))); break;
            case 0x6E: Dbnzd(DirImm()); break;
            case 0xFE: Dbnzy(Imm()); break;
            case 0x5F: pc = Abs(); break;
            case 0x1F: pc = AbsIndX(); break;

            //Subroutine Operations
            case 0x3F: Call(Abs()); break;
            case 0x4F: PCall(op); break;
            case 0x01 or 0x11 or 0x21 or 0x31 or
                 0x41 or 0x51 or 0x61 or 0x71 or
                 0x81 or 0x91 or 0xa1 or 0xb1 or
                 0xc1 or 0xd1 or 0xe1 or 0xf1:
                TCall((op >> 4) & 0xf);
                break;
            case 0x0F: Brk(); break;
            case 0x6F: Ret(); break;
            case 0x7F: Ret1(); break;

            //Stack Operations
            case 0x2D: Push(a); break;
            case 0x4D: Push(x); break;
            case 0x6D: Push(y); break;
            case 0x0D: Push(p); break;
            case 0xAE: a = Pop(); break;
            case 0xCE: x = Pop(); break;
            case 0xEE: y = Pop(); break;
            case 0x8E: p = Pop(); break;

            //Bit Operations
            case 0x02 or 0x22 or 0x42 or 0x62 or
                 0x82 or 0xa2 or 0xc2 or 0xe2:
                Set1(Dir(), 1 << (op >> 5));
                break;
            case 0x12 or 0x32 or 0x52 or 0x72 or
                 0x92 or 0xb2 or 0xd2 or 0xf2:
                Clr1(Dir(), 1 << (op >> 5));
                break;
            case 0x0E: TSet1(Abs()); break;
            case 0x4E: Tclr1(Abs()); break;
            case 0x4A: And1(Mbit()); break;
            case 0x6A: And1(Mbit(), true); break;
            case 0x0A: Or1(Mbit()); break;
            case 0x2A: Or1(Mbit(), true); break;
            case 0x8A: Eor1(Mbit()); break;
            case 0xEA: Not1(Mbit()); break;
            case 0xAA: Mov1(Mbit()); break;
            case 0xCA: Mov1(Mbit(), true); break;

            //PSW Operations
            case 0x60: PS &= ~FC; break;
            case 0x80: PS |= FC; break;
            case 0xED: PS ^= FC; break;
            case 0xE0: PS &= ~(FV | FH); break;
            case 0x20: PS &= ~FP; break;
            case 0x40: PS |= FP; break;
            case 0xA0: PS |= FI; break;
            case 0xC0: PS &= ~FI; break;

            //Other Commands
            case 0xEF: break;
            case 0xFF: break;
        }
        Stepped = true;
    }

    public int Imm() => ReadOp();

    public sbyte Rel() => (sbyte)ReadOp();

    public int Dir() => (ReadOp() | GetPage()) & 0xffff;

    public int DirIdX()
    {
        var v = (ReadOp() + x & 0xff) | GetPage();
        Idle();
        return v & 0xffff;
    }

    public int DirIdY()
    {
        var v = (ReadOp() + y & 0xff) | GetPage();
        Idle();
        return v & 0xffff;
    }

    public int DirIdXInd()
    {
        var v = ReadOp() | GetPage();
        Idle();
        int a = Read((v + x & 0xff));
        a |= (Read(v + x + 1 & 0xff) << 8);
        return a & 0xffff;
    }

    public int DirIndY()
    {
        var v = ReadOp() | GetPage();
        v = Read(v) | (Read(v + 1) << 8);
        return (v + y) & 0xffff;
    }

    public (int, int) IndXY()
    {
        Idle();
        var a1 = Read(y | GetPage());
        var a2 = x | GetPage();
        return (a1, a2);
    }

    public (int, int) DirImm()
    {
        int a1 = ReadOp();
        int a2 = (ReadOp() | GetPage()) & 0xffff;
        return (a1 & 0xff, a2 & 0xffff);
    }

    public (int, int) DirDir()
    {
        var a1 = Read(ReadOp() | GetPage());
        int a2 = (ReadOp() | GetPage()) & 0xffff;
        return (a1 & 0xff, a2 & 0xffff);
    }

    public int Abs()
    {
        int v = ReadOp();
        v |= ReadOp() << 8;
        return v & 0xffff;
    }

    public int AbsX()
    {
        int v = ReadOp();
        v |= ReadOp() << 8;
        return (v + x) & 0xffff;
    }

    public int AbsIndX()
    {
        var (a1, a2) = (ReadOp(), ReadOp());
        var b = a1 | a2 << 8;
        Idle();
        var v = Read(b + x) | Read(b + x + 1 & 0xffff) << 8;
        return v & 0xffff;
    }

    public int AbsY()
    {
        int v = ReadOp();
        v |= ReadOp() << 8;
        return (v + y) & 0xffff;
    }

    public (int, int) Mbit()
    {
        int low = ReadOp();
        int high = ReadOp();
        int a1 = high << 8 | low;
        int a2 = a1 >> 13;
        return (a1 & 0x1fff, a2);
    }

    public (int, int) DirBit(int op = -1)
    {
        int low = ReadOp();
        int high = ReadOp();
        int v = (low + (op == -1 ? 0 : x) & 0xff) + GetPage();
        int a1 = Read(v & 0xffff);
        Idle();
        return (a1, high);
    }

    private int Pop() => Read(++SP | 0x100);

    private void Push(int v) => Write(0x100 | SP--, v);

    private void SetC(bool v)
    {
        if (v) PS |= FC;
        else PS &= ~FC;
    }

    private void SetH(bool flag)
    {
        if (flag) PS |= FH;
        else PS &= ~FH;
    }

    private void SetV(bool flag)
    {
        if (flag) PS |= FV;
        else PS &= ~FV;
    }

    private void SetZN(int v)
    {
        if ((v & 0xff) == 0) PS |= FZ;
        else PS &= ~FZ;
        if ((v & FN) != 0) PS |= FN;
        else PS &= ~FN;
    }

    private void SetZN_W(int v)
    {
        if ((v & 0xffff) == 0) PS |= FZ;
        else PS &= ~FZ;
        if ((v & 0x8000) == 0x8000) PS |= FN;
        else PS &= ~FN;
    }

    public void Reset()
    {
        flags[2] = true;
        flags[5] = true;
        Stepped = false;

        SP = 0x00;
        PS = 0x00;
        X = 0x00;
        A = 0x00;
        Y = 0x00;
        StepOverAddr = -1;
        TestAddr = 0;

        Read(PC); Read(PC);
        Read(0x100 | SP--); Read(0x100 | SP--); Read(0x100 | SP--);
        Idle();
        PS &= ~FI;
        PC = ReadWord(0xfffe);
    }

    public List<RegisterInfo> GetFlags() =>
    [
        new("","C",$"{(PS&0x01) != 0}"),
        new("","Z",$"{(PS&0x02) != 0}"),
        new("","I",$"{(PS&0x04) != 0}"),
        new("","H",$"{(PS&0x08) != 0}"),
        new("","B",$"{(PS&0x10) != 0}"),
        new("","P",$"{(PS&0x20) != 0}"),
        new("","V",$"{(PS&0x40) != 0}"),
        new("","N",$"{(PS&0x80) != 0}"),
    ];

    public List<RegisterInfo> GetRegisters() =>
    [
        new("","A",$"{A:X2}"),
        new("","X",$"{X:X2}"),
        new("","Y",$"{Y:X2}"),
        new("","P",$"{PS:X2}"),
        new("","S",$"{SP:X2}"),
    ];

    public void Save(BinaryWriter bw)
    {
        bw.Write(PC); bw.Write(SP);
        bw.Write(A); bw.Write(X);
        bw.Write(Y); bw.Write(PS);
    }

    public void Load(BinaryReader br)
    {
        PC = br.ReadInt32(); SP = br.ReadInt32();
        A = br.ReadInt32(); X = br.ReadInt32();
        Y = br.ReadInt32(); PS = br.ReadInt32();
    }
}
