
using Gmulator.Core.Nes.Mappers;

namespace Gmulator.Core.Nes;
public partial class NesCpu : EmuState
{
    private const int FC = 1 << 0;
    private const int FZ = 1 << 1;
    private const int FI = 1 << 2;
    private const int FD = 1 << 3;
    private const int FB = 1 << 4;
    private const int FU = 1 << 5;
    private const int FV = 1 << 6;
    private const int FN = 1 << 7;

    private const int INT_NMI = 0xfffa;
    private const int INT_RESET = 0xfffc;
    private const int INT_BRK = 0xfffe;
    private int pc, sp, a, x, y, ps;
    private bool[] flags;

    public int PC { get => (ushort)pc; set => pc = (ushort)value; }
    public int SP { get => (byte)sp; set => sp = value & 0xff; }
    public int A { get => a&0xff; set => a = value & 0xff; }
    public int X { get => (byte)x; set => x = value & 0xff; }
    public int Y { get => y & 0xff; set => y = (byte)value; }
    public int Instructions { get; private set; }
    public int Cycles { get; set; }
    public int CycleTotal { get; set; }
    public int PS { get => ps & 0xff; set => ps = (ushort)value; }
    public static int NmiTriggered { get; set; }
    public bool[] Flags { get => flags; set => flags = value; }
    private int StallCycles;

    public int StepOverAddr;
    public Action<int> PpuStep;
    public Func<int, int> ReadByte;
    public Action<int, int> WriteByte;
    public Func<int, int> ReadWord;
    public Action<int> SetState { get; internal set; }

    public NesCpu()
    {
        flags = new bool[8];

        CreateOpcodes();
    }

    public int ReadDmc(int a)
    {
        StallCycles += 4;
        return ReadByte(a);
    }

    private int TickRead(int a, int opbit = 0)
    {
        Cycles++;
        PpuStep(3);
        var v = ReadByte(a);
#if DEBUG || RELEASE
        
#endif
        return v;
    }

    private void TickWrite(int a, int v)
    {
        Cycles++;
        PpuStep(3);
        WriteByte(a, v);
#if DEBUG || RELEASE
        
#endif
    }

    public void Step()
    {
        if (StallCycles > 0)
        {
            StallCycles--;
            return;
        }

        int op = TickRead(PC++);
        int mode = Disasm[op].Mode;

        //if (State == Stopped)
        //    return;

        switch (Disasm[op].Id)
        {
            case ADC: Adc(mode); break;
            case AND: And(mode); break;
            case ASL: Asl(mode); break;
            case BCC: Brn(mode, FC); break;
            case BCS: Brp(mode, FC); break;
            case BEQ: Brp(mode, FZ); break;
            case BIT: Bit(mode); break;
            case BMI: Brp(mode, FN); break;
            case BNE: Brn(mode, FZ); break;
            case BPL: Brn(mode, FN); break;
            case BRK: Brk(PC); break;
            case BVC: Brn(mode, FV); break;
            case BVS: Brp(mode, FV); break;
            case CLC: Set(mode, ~FC); break;
            case CLD: Set(mode, ~FD); break;
            case CLI: Set(mode, ~FI); break;
            case CLV: Set(mode, ~FV); break;
            case CMP: Cmp(mode); break;
            case CPX: Cpx(mode); break;
            case CPY: Cpy(mode); break;
            case DEC: Dec(mode); break;
            case DEX: Dex(mode); break;
            case DEY: Dey(mode); break;
            case EOR: Eor(mode); break;
            case INC: Inc(mode); break;
            case INX: Inx(mode); break;
            case INY: Iny(mode); break;
            case JMP: PC = AddrModeR(mode); break;
            case JSR: Jsr(mode); break;
            case LDA: Lda(mode); break;
            case LDX: Ldx(mode); break;
            case LDY: Ldy(mode); break;
            case LSR: Lsr(mode); break;
            case NOP: AddrModeR(mode); break;
            case ORA: Ora(mode); break;
            case PHA: Pha(mode); break;
            case PHP: Php(mode); break;
            case PLA: Pla(mode); break;
            case PLP: Plp(mode); break;
            case ROL: Rol(mode); break;
            case ROR: Ror(mode); break;
            case RTI: Rti(mode); break;
            case RTS: Rts(mode); break;
            case SBC: Sbc(mode); break;
            case SEC: Set(mode, FC); break;
            case SED: Set(mode, FD); break;
            case SEI: Set(mode, FI); break;
            case STA: Sta(mode); break;
            case STX: Stx(mode); break;
            case STY: Sty(mode); break;
            case TAX: Tax(mode); break;
            case TAY: Tay(mode); break;
            case TSX: Tsx(mode); break;
            case TXA: Txa(mode); break;
            case TXS: Txs(mode); break;
            case TYA: Tya(mode); break;
            case ISB: Isb(mode); break;
            case ANC: Anc(mode); break;
            case TOP: Top(mode); break;
            case LAX: Lax(mode); break;
            case AAX: Aax(mode); break;
            case DCP: Dcp(mode); break;
            case SLO: Slo(mode); break;
            case RLA: Rla(mode); break;
            case SRE: Sre(mode); break;
            case RRA: Rra(mode); break;
            case -1:
            {
                //if (!state_loaded)
                //State = EmuState.Debug;
                //printf("%04X\n", Pc);
                //state_loaded = false;
            }
            break;
        }

        if (BaseMapper.Fire)
        {
            BaseMapper.Fire = false;
            Irq();
        }

        if (NmiTriggered == 1)
        {
            NmiTriggered = 2;
        }
        else if (NmiTriggered == 2)
        {
            Nmi();
            NmiTriggered = 0;
        }
    }

    private void Adc(int mode)
    {
        int v = TickRead(AddrModeR(mode));
        int b = A + v + (PS & FC);

        SetFlag(b >= 0x100, FC);
        SetFlag((byte)b == 0, FZ);
        SetFlag(b & 0x80, FN);
        SetFlag(~(A ^ v) & (A ^ b) & 0x80, FV);

        A = b;
    }

    private void And(int mode)
    {
        A &= TickRead(AddrModeR(mode));
        SetFlag(A == 0, FZ);
        SetFlag(A & 0x80, FN);
    }

    private void Asl(int mode)
    {
        int b; TickRead(PC);
        if (mode == ACCU)
        {
            SetFlag(A & 0x80, FC);
            A = b = (A << 1) & 0xfe;
        }
        else
        {
            int addr = AddrModeW(mode);
            b = TickRead(addr);
            SetFlag(b & 0x80, FC);
            b = (b << 1) & 0xfe;
            TickWrite(addr, b);
        }
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Brp(int mode, int f)
    {
        int v = (sbyte)AddrModeR(mode);
        if ((PS & f) == f)
        {
            ushort addr = (ushort)(PC + v);
            TickRead(PC);
            if ((addr & 0xff00) != (PC & 0xff00))
                TickRead(PC++);
            PC = addr;
        }
    }

    private void Brn(int mode, int f)
    {
        int v = (sbyte)AddrModeR(mode);
        if ((PS & f) != f)
        {
            ushort addr = (ushort)(PC + v);
            TickRead(PC);
            if ((addr & 0xff00) != (PC & 0xff00))
                TickRead(PC++);
            PC = addr;
        }
    }

    private void Bit(int mode)
    {
        int v = TickRead(AddrModeR(mode), 1);
        var b = A & v;
        SetFlag(b == 0, FZ);
        SetFlag(v & 0x80, FN);
        SetFlag(v & 0x40, FV);
    }

    private void Cmp(int mode)
    {
        int v = TickRead(AddrModeR(mode));
        sbyte b = (sbyte)(A - v);
        SetFlag(A >= v, FC);
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Cpx(int mode)
    {
        int v = TickRead(AddrModeR(mode));
        sbyte b = (sbyte)(X - v);
        SetFlag(X >= v, FC);
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Cpy(int mode)
    {
        int v = TickRead(AddrModeR(mode));
        sbyte b = (sbyte)(Y - v);
        SetFlag(Y >= v, FC);
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Dec(int mode)
    {
        int addr = AddrModeW(mode);
        TickRead(PC);
        byte b = (byte)(TickRead(addr) - 1);
        TickWrite(addr, b);
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Dex(int mode)
    {
        X--;
        TickRead(PC);
        SetFlag(X == 0, FZ);
        SetFlag(X & 0x80, FN);
    }

    private void Dey(int mode)
    {
        Y--;
        TickRead(PC);
        SetFlag(Y == 0, FZ);
        SetFlag(Y & 0x80, FN);
    }

    private void Eor(int mode)
    {
        A ^= TickRead(AddrModeR(mode));
        SetFlag(A == 0, FZ);
        SetFlag(A & 0x80, FN);
    }

    private void Inc(int mode)
    {
        int a = AddrModeW(mode); TickRead(PC);
        byte b = (byte)(TickRead(a) + 1);
        TickWrite(a, b);
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Inx(int mode)
    {
        X++;
        TickRead(PC);
        SetFlag(X == 0, FZ);
        SetFlag(X & 0x80, FN);
    }

    private void Iny(int mode)
    {
        Y++;
        TickRead(PC);
        SetFlag(Y == 0, FZ);
        SetFlag(Y & 0x80, FN);
    }

    private void Jsr(int mode)
    {
        int addr = AddrModeR(mode);
        PC--;
        Push(SP--, PC >> 8);
        Push(SP--, PC & 0xff);
        TickRead(PC);
        PC = addr;
    }

    private void Lda(int mode)
    {
        A = TickRead(AddrModeR(mode));
        SetFlag(A == 0, FZ);
        SetFlag(A & 0x80, FN);
    }

    private void Ldx(int mode)
    {
        X = TickRead(AddrModeR(mode));
        SetFlag(X == 0, FZ);
        SetFlag(X & 0x80, FN); ;
    }

    private void Ldy(int mode)
    {
        Y = TickRead(AddrModeR(mode));
        SetFlag(Y == 0, FZ);
        SetFlag(Y & 0x80, FN); ;
    }

    private void Lsr(int mode)
    {
        int b; TickRead(PC);
        if (mode == ACCU)
        {
            SetFlag(A & 0x01, FC);
            A = b = (A >> 1) & 0x7f;
        }
        else
        {
            int a = AddrModeW(mode);
            b = TickRead(a);
            SetFlag(b & 0x01, FC);
            b = (b >> 1) & 0x7f;
            TickWrite(a, b);
        }
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Ora(int mode)
    {
        A |= TickRead(AddrModeR(mode));
        SetFlag(A == 0, FZ);
        SetFlag(A & 0x80, FN);
    }

    private void Pha(int mode)
    {
        TickRead(PC);
        Push(SP--, A);
    }

    private void Php(int mode)
    {
        TickRead(PC);
        Push(SP--, PS | 0x30);
    }

    private void Pla(int mode)
    {
        TickRead(PC); TickRead(PC);
        A = Pop();
        SetFlag(A == 0, FZ);
        SetFlag(A & 0x80, FN);
    }

    private void Plp(int mode)
    {
        TickRead(PC); TickRead(PC);
        PS = Pop() & ~0x30;// & ~0x10 | 0x20;
        //SetFlags();
    }

    private void Rol(int mode)
    {
        int b, bit7;
        TickRead(PC);

        if (mode == ACCU)
        {
            bit7 = A & 0x80;
            A <<= 1 & 0xff;
            if ((PS & FC) != 0)
                A |= 0x01;
            b = (byte)A;
        }
        else
        {
            int a = AddrModeW(mode);
            b = TickRead(a);
            bit7 = b & 0x80;
            b = (b << 1) & 0xff;
            if ((PS & FC) != 0)
                b |= 0x01;

            TickWrite(a, b);
        }
        SetFlag(bit7, FC);
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Ror(int mode)
    {
        int b, bit0;
        TickRead(PC);

        if (mode == ACCU)
        {
            bit0 = A & 0x01;
            A >>= 1;
            if ((PS & FC) != 0)
                A |= 0x80;
            b = (byte)A;
        }
        else
        {
            int a = AddrModeW(mode);
            b = TickRead(a);
            bit0 = b & 0x01;
            b = (byte)(b >> 1);
            if ((PS & FC) != 0)
                b |= 0x80;

            TickWrite(a, b);
        }
        SetFlag(bit0, FC);
        SetFlag(b == 0, FZ);
        SetFlag(b & 0x80, FN);
    }

    private void Rti(int mode)
    {
        TickRead(PC); TickRead(PC);
        PS = Pop() & ~0x10 | 0x20;
        PC = Pop();
        PC |= Pop() << 8;
        SetFlags();
    }

    private void Rts(int mode)
    {
        TickRead(PC); TickRead(PC); TickRead(PC);
        PC = Pop();
        PC |= Pop() << 8;
        PC++;
    }

    private void Sbc(int mode)
    {
        int v = TickRead(AddrModeR(mode));
        int b = A + ~v + (PS & FC);
        SetFlag((b & 0xff00) == 0, FC);
        SetFlag((b & 0xff) == 0, FZ);
        SetFlag(b & 0x80, FN);
        SetFlag((A ^ v) & (A ^ b) & 0x80, FV);

        A = b;
    }

    private void Sta(int mode) => TickWrite(AddrModeW(mode), A);
    private void Stx(int mode) => TickWrite(AddrModeW(mode), X);
    private void Sty(int mode) => TickWrite(AddrModeW(mode), Y);

    private void Tax(int mode)
    {
        X = A; TickRead(PC);
        SetFlag(X == 0, FZ);
        SetFlag(X & 0x80, FN);
    }

    private void Tay(int mode)
    {
        Y = A; TickRead(PC);
        SetFlag(Y == 0, FZ);
        SetFlag(Y & 0x80, FN);
    }

    private void Tsx(int mode)
    {
        X = SP; TickRead(PC);
        SetFlag(X == 0, FZ);
        SetFlag(X & 0x80, FN);
    }

    private void Txa(int mode)
    {
        A = X;
        TickRead(PC);
        SetFlag(A == 0, FZ);
        SetFlag(A & 0x80, FN);
    }

    private void Txs(int mode)
    {
        SP = X;
        TickRead(PC);
    }

    private void Tya(int mode)
    {
        A = Y;
        TickRead(PC);
        SetFlag(A == 0, FZ);
        SetFlag(A & 0x80, FN); ;
    }

    private void Isb(int mode)
    {
        var m = PC;
        Inc(mode);
        PC = m;
        Sbc(mode);
    }

    private void Anc(int mode)
    {
        And(mode);
        A <<= 1;
        if ((PS & FC) != 0)
            A |= 0x01;
    }

    private void Top(int mode)
    {
        PC++;
        PC++;
    }

    private void Lax(int mode)
    {
        var m = PC;
        Lda(mode);
        PC = m;
        Ldx(mode);
    }

    private void Aax(int mode)
    {
        var b = X & A;
        var a = AddrModeR(mode);
        TickWrite(a, b);
    }

    private void Dcp(int mode)
    {
        var m = PC;
        Dec(mode);
        PC = m;
        Cmp(mode);
    }

    private void Slo(int mode)
    {
        var m = PC;
        Asl(mode);
        PC = m;
        Ora(mode);
    }

    private void Rla(int mode)
    {
        var m = PC;
        Rol(mode);
        PC = m;
        And(mode);
    }

    private void Sre(int mode)
    {
        var m = PC;
        Lsr(mode);
        PC = m;
        Eor(mode);
    }

    private void Rra(int mode)
    {
        var m = PC;
        Ror(mode);
        PC = m;
        Adc(mode);
    }

    private int AddrModeR(int mode)
    {
        switch (mode)
        {
            case IMPL:
            case ACCU:
                TickRead(PC);
                break;
            case IMME:
                return (ushort)PC++;
            case ZERP:
                return TickRead(PC++);
            case ZERX:
                TickRead(PC); return (TickRead(PC++) + X) & 0xff;
            case ZERY:
                TickRead(PC); return TickRead(PC++) + Y & 0xff;
            case ABSO:
                return TickRead(PC++) | TickRead(PC++) << 8;
            case ABSX:
            {
                int oldaddr = TickRead(PC++) | TickRead(PC++) << 8;
                int newaddr = oldaddr + X;
                if ((newaddr & 0xff00) != (oldaddr & 0xff00)) TickRead(PC);
                return (ushort)(newaddr & 0xffff);
            }
            case ABSY:
            {
                int oldaddr = TickRead(PC++) | TickRead(PC++) << 8;
                int newaddr = oldaddr + Y;
                if ((newaddr & 0xff00) != (oldaddr & 0xff00)) TickRead(PC);
                return newaddr & 0xffff;
            }
            case INDX:
            {
                int b1 = TickRead(PC++);
                int lo = TickRead(b1 + X & 0xff);
                int hi = TickRead(b1 + 1 + X & 0xff);
                TickRead(PC);
                return (ushort)((hi << 8) | lo);
            }
            case INDY:
            {
                int b1 = TickRead(PC++);
                int lo = TickRead(b1 & 0xff);
                int hi = TickRead(b1 + 1 & 0xff);
                ushort oldaddr = ((ushort)(hi << 8 | lo));
                ushort addr = (ushort)(oldaddr + Y);
                if ((addr & 0xff00) != (oldaddr & 0xff00)) TickRead(PC);
                return addr;
            }
            case INDI:
            {
                int addr = TickRead(PC++) | TickRead(PC++) << 8;
                int high, low;
                if ((addr & 0xff) == 0xff)
                {
                    low = TickRead(addr++);
                    high = TickRead(addr - 0x100 & 0xff00);
                    addr = (ushort)((high << 8) | low);
                }
                else
                {
                    low = TickRead(addr++);
                    high = TickRead(addr);
                    addr = (ushort)((high << 8) | low);
                }
                return addr;
            }
            case RELA:
            {
                return TickRead(PC++);
            }
            case ERRO:
                //	//State = ERROR;
                //	printf("%04X\n", Pc);
                break;
        }
        return 0;
    }

    private int AddrModeW(int mode)
    {
        switch (mode)
        {
            case ZERP:
                return TickRead(PC++);
            case ZERX:
                TickRead(PC); return (TickRead(PC++) + X) & 0xff;
            case ZERY:
                TickRead(PC); return TickRead(PC++) + Y & 0xff;
            case ABSO:
                return TickRead(PC++) | TickRead(PC++) << 8;
            case ABSX:
            {
                int addr = (TickRead(PC++) | TickRead(PC++) << 8) + X;
                TickRead(PC);
                return (ushort)addr;
            }
            case ABSY:
            {
                var addr = (TickRead(PC++) | TickRead(PC++) << 8) + Y;
                TickRead(PC);
                return (ushort)addr;
            }
            case INDX:
            {
                int b1 = TickRead(PC++);
                int lo = TickRead(b1 + X & 0xff);
                int hi = TickRead(b1 + 1 + X & 0xff);
                TickRead(PC);
                return (ushort)((hi << 8) | lo);
            }
            case INDY:
            {
                int b1 = TickRead(PC++);
                int lo = TickRead(b1 & 0xff);
                int hi = TickRead(b1 + 1 & 0xff);
                TickRead(PC);
                return (ushort)(((hi << 8) | lo) + Y);
            }
        }
        return 0;
    }

    public void Reset()
    {
        flags[2] = true;
        flags[5] = true;

        PC = ReadWord(INT_RESET);
        SP = 0xfd;
        PS = 0x04;
        X = 0x00;
        A = 0x00;
        Y = 0x00;
        Instructions = 0;
        Cycles = 7;
        StepOverAddr = -1;
    }

    private void Nmi()
    {
        TickRead(PC);
        TickRead(PC);
        Push(SP--, PC >> 8);
        Push(SP--, PC & 0xff);
        Push(SP--, PS);
        PC = ReadWord(INT_NMI);
    }

    private int Pop() => TickRead(++SP | 0x100);

    private void Push(int addr, int v) => TickWrite(addr | 0x100, (byte)v);

    private void Brk(int pc)
    {
        PS |= FB | FU;
        TickRead(PC++);
        Push(SP--, pc >> 8);
        Push(SP--, (pc + 1) & 0xff);
        Push(SP--, PS);
        PS |= FI;
        PC = ReadWord(INT_BRK);
        TickRead(PC);
        TickRead(PC);
    }

    private void Irq()
    {
        Push(SP--, PC >> 8);
        Push(SP--, PC & 0xff);
        PS &= ~(FB | FU);
        Push(SP--, PS | FB | FU);
        PC = ReadWord(INT_BRK);
        PS |= FI;
    }

    private void SetFlag(bool flag, int v)
    {
        if (flag)
            PS |= v;
        else
            PS &= ~v;
    }

    private void SetFlag(int flag, int v)
    {
        if (flag != 0)
            PS |= v;
        else
            PS &= ~v;
    }

    public bool GetFlag(int v) => (PS & v) != 0;

    private void Set(int mode, int v)
    {
        AddrModeR(mode);
        if (v > 0)
            PS |= v;
        else
            PS &= v;
    }

    public byte GetFlag()
    {
        int v = 0;
        for (int i = 0; i < Flags.Length; i++)
            v |= Flags[i] ? 1 << i : 0;

        return (byte)v;
    }

    public void SetFlags()
    {
        for (int i = 0; i < Flags.Length; i++)
            Flags[i] = (PS & (1 << i)) != 0;
    }

    public Dictionary<string, bool> GetFlags() => new()
    {
        ["C"] = Flags[0],
        ["Z"] = Flags[1],
        ["I"] = Flags[2],
        ["D"] = Flags[3],
        ["B"] = Flags[4],
        ["U"] = Flags[5],
        ["V"] = Flags[6],
        ["N"] = Flags[7],
    };

    public Dictionary<string, byte> GetRegisters() => new()
    {
        ["A:"] = (byte)A,
        ["X:"] = (byte)X,
        ["Y:"] = (byte)Y,
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
        bw.Write(PC);
        bw.Write(SP);
        bw.Write(A);
        bw.Write(X);
        bw.Write(Y);
        bw.Write(Instructions);
        bw.Write(Cycles);
        bw.Write(CycleTotal);
        bw.Write(PS);
        bw.Write(NmiTriggered);
        bw.Write(StepOverAddr);
    }

    public override void Load(BinaryReader br)
    {
        PC = br.ReadInt32();
        SP = br.ReadInt32();
        A = br.ReadInt32();
        X = br.ReadInt32();
        Y = br.ReadInt32();
        Instructions = br.ReadInt32();
        Cycles = br.ReadInt32();
        CycleTotal = br.ReadInt32();
        PS = br.ReadInt32();
        NmiTriggered = br.ReadInt32();
        StepOverAddr = br.ReadInt32();
    }
}
