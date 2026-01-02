using Gmulator.Core.Gbc;
using Gmulator.Core.Snes.Mappers;
using Gmulator.Interfaces;
using System.Runtime.Intrinsics.Arm;
namespace Gmulator.Core.Snes;

public partial class SnesCpu : ISaveState, ICpu
{
    public const int FC = 1 << 0;
    public const int FZ = 1 << 1;
    public const int FI = 1 << 2;
    public const int FD = 1 << 3;
    public const int FX = 1 << 4;
    public const int FM = 1 << 5;
    public const int FV = 1 << 6;
    private const int FN = 1 << 7;
    private const int COPn = 0xFFE4;
    private const int BRKn = 0xFFE6;
    private const int IRQn = 0xFFEE;
    private const int NMIn = 0xFFEA;
    private const int COPe = 0xFFF4;
    private const int RESETe = 0xFFFC;
    private const int BRKe = 0xFFFE;

    private int pc, sp, ra, rx, ry, ps, db, pb, dr;
    private bool Imme;

    public int PC
    {
        get => pc & 0xffff;
        set => pc = value & 0xffff;
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
    public bool NmiEnabled { get; set; }
    public bool IrqEnabled { get; set; }
    public int StepOverAddr;
    public int Cycles { get => cycles; private set => cycles = value; }

    public bool XMem => (ps & FX) != 0;
    public bool MMem => (ps & FM) != 0;

    private Snes Snes;
    private SnesPpu Ppu;
    private BaseMapper Mapper;
    private int C => ps & FC;
    private bool I => (ps & FI) != 0;
    public Action<int> SetState;
    private int cycles;
    public bool Sa1Interrupt { get; set; }

    public int TestAddr { get; private set; }
    public Action Tick { get; set; }

    public SnesCpu()
    {
        CreateOpcodes();
    }

    public void SetSnes(Snes snes)
    {
        Snes = snes;
        Ppu = snes.Ppu;
        Mapper = snes.Mapper;
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

    public virtual int Read(int a)
    {
        var c = GetClockSpeed(a);
        Ppu?.Step(c);
        Snes?.HandleDma();
        Snes?.Sa1?.Step();
        int v = Snes?.ReadMemory(a) ?? 0;
        return OpenBus = v & 0xff;
    }

    public virtual void Write(int a, int v)
    {
        var c = GetClockSpeed(a);
        Ppu?.Step(c);
        Snes?.HandleDma();
        Snes?.Sa1?.Step();
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

    public void SetIRQ() => IrqEnabled = true;

    public virtual void Step()
    {
        if (NmiEnabled)
        {
            Nmi(NMIn);
            NmiEnabled = false;
            return;
        }
        if (!I && IrqEnabled)
        {
            Nmi(IRQn);
            IrqEnabled = false;
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
            case WAI: GetMode(mode, true); Idle(); Idle(); break;
            case WDM: PC++; break;
            case XBA: Xba(); break;
            case XCE: Xce(); break;
        }
    }

    public List<RegisterInfo> GetFlags() =>
    [
        new("","C",$"{(ps & FC) != 0}"),
        new("","Z",$"{(ps & FZ) != 0}"),
        new("","I",$"{(ps & FI) != 0}"),
        new("","D",$"{(ps & FD) != 0}"),
        new("","X",$"{(ps & FX) != 0}"),
        new("","M",$"{(ps & FM) != 0}"),
        new("","V",$"{(ps & FV) != 0}"),
        new("","N",$"{(ps & FN) != 0}"),
        new("","E",$"{E}"),
    ];

    public List<RegisterInfo> GetRegisters() =>
    [
        new("","A ",$"{A:X4}"),
        new("","X ",$"{X:X4}"),
        new("","Y ",$"{Y:X4}"),
        new("","SP",$"{SP:X4}"),
        new("","D ",$"{D:X4}"),
        new("","P ",$"{PS:X4}"),
        new("","DB",$"{DB:X2}"),
        new("","PB",$"{PB:X2}"),
    ];

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

    public void Save(BinaryWriter bw)
    {
        bw.Write(PC); bw.Write(SP);
        bw.Write(A); bw.Write(X);
        bw.Write(Y); bw.Write(PS);
        bw.Write(PB); bw.Write(DB);
        bw.Write(E); bw.Write(D);
        bw.Write(FastMem); bw.Write(NmiEnabled);
        bw.Write(IrqEnabled);
    }

    public void Load(BinaryReader br)
    {
        PC = br.ReadInt32(); SP = br.ReadInt32();
        A = br.ReadInt32(); X = br.ReadInt32();
        Y = br.ReadInt32(); PS = br.ReadInt32();
        PB = br.ReadInt32(); DB = br.ReadInt32();
        E = br.ReadBoolean(); D = br.ReadInt32();
        FastMem = br.ReadBoolean(); NmiEnabled = br.ReadBoolean();
        IrqEnabled = br.ReadBoolean();
    }
}
