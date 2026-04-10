using Gmulator.Core.Snes.Mappers;
using Gmulator.Interfaces;

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
    public const int COPn = 0xFFE4;
    public const int BRKn = 0xFFE6;
    public const int IRQn = 0xFFEE;
    public const int NMIn = 0xFFEA;
    public const int COPe = 0xFFF4;
    public const int RESETe = 0xFFFC;
    public const int BRKe = 0xFFFE;

    private bool Imme;

    #region State
    private int _pc, _sp, _ra, _rx, _ry, _ps, _db, _pb, _dr;
    public int PC
    {
        get => _pc & 0xffff;
        set => _pc = value & 0xffff;
    }
    public int SP
    {
        get => _sp & 0xffff;
        set => _sp = value & 0xffff;
    }
    public int A
    {
        get => MMem ? _ra & 0xff00 | _ra & 0xff : _ra & 0xffff;
        set => _ra = MMem ? _ra & 0xff00 | value & 0xff : value & 0xffff;
    }
    public int X
    {
        get => !E && !XMem ? _rx & 0xffff : _rx & 0xff;
        set => _rx = !E && !XMem ? value & 0xffff : value & 0xff;
    }
    public int Y
    {
        get => !E && !XMem ? _ry & 0xffff : _ry & 0xff;
        set => _ry = !E && !XMem ? value & 0xffff : value & 0xff;
    }
    public int PS
    {
        get => _ps & 0xff;
        set => _ps = value & 0xff;
    }
    public int PB { get => _pb & 0xff; set => _pb = value & 0xff; }
    public int DB { get => _db & 0xff; set => _db = value & 0xff; }
    public bool E { get; set; }
    public int D { get => _dr & 0xffff; set => _dr = value & 0xffff; }
    public bool FastMem { get; set; }
    public bool NmiEnabled { get; set; }
    public bool IrqEnabled { get; set; }
    public ulong Cycles { get; set; }

    #endregion

    public bool XMem => (_ps & FX) != 0;
    public bool MMem => (_ps & FM) != 0;
    public int FlagC => _ps & FC;
    public bool I => (_ps & FI) != 0;
    public int PBPC => PB << 16 | _pc;
    public Action Tick { get; set; }

    public int OpenBus { get; set; }
    public int StepOverAddr { get; set; }
    private int _debugPC;
    private int _debugAddr;
    private int _debugMode;

    private Snes Snes;
    private SnesPpu Ppu;
    private BaseMapper Mapper;

    public Action<int> SetState;
    private ulong cycles;

    private int _stepCounter;
    private int _opCode;
    public int TestAddr { get; private set; }



    public SnesCpu()
    {
        CreateOpcodes();
    }

    public bool StepEnd(DebugState debugState)
    {
        var pc = PBPC;
        if (StepOverAddr == pc)
        {
            StepOverAddr = -1;
            return true;
        }

        if (debugState == DebugState.StepMain)
        {
            Step();
            return true;
        }
        else if (debugState == DebugState.StepSa1)
        {
            if (Snes.Sa1?.Cpu.DebugStep() == true)
                StepOneCycle();
            return true;
        }
        return false;
    }

    public void SetSnes(Snes snes)
    {
        Snes = snes;
        Ppu = snes.Ppu;
        Mapper = snes.Mapper;
    }

    public void Idle()
    {
        Snes?.HandleDma();
        Ppu?.Step(6);
    }

    public void Idle8() => Ppu?.Step(8);

    public void AddCycles(int v) => Cycles++;

    public int ReadOpCycle()
    {
        _debugPC = PBPC;
        cycles++;
        PpuCycle();
        int value = Snes?.Mmu.ReadByte(_debugPC) & 0xff ?? 0;
        _debugPC++;
        return value;
    }

    public void StepOneCycle()
    {
        switch (_stepCounter)
        {
            case 0:
                _opCode = ReadOpCycle();
                _stepCounter++;
                break;
            case 1:
                _debugMode = Disasm[_opCode].Mode;
                _stepCounter++;
                break;
            case 2:
                PC = _debugPC;
                _debugAddr = GetMode(_debugMode);
                ExecOp(_opCode, _debugMode, _debugAddr);
                _stepCounter = 0;
                break;
        }
    }

    private void PpuCycle()
    {
        var c = GetClockSpeed(PBPC);
        Ppu?.Step(c);
    }

    public virtual int Read(int a)
    {
        var c = GetClockSpeed(a);
        Ppu?.Step(c);
        Snes?.HandleDma();
        Snes?.Sa1?.Step();
        cycles++;
        int v = Snes?.ReadMemory(a) ?? 0;
        return OpenBus = v & 0xff;
    }

    public virtual void Write(int a, int v)
    {
        var c = GetClockSpeed(a);
        Ppu?.Step(c);
        Snes?.HandleDma();
        Snes?.Sa1?.Step();
        cycles++;
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

    public int ReadWord(int a)
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

    private int GetGet8bitImm(int a) => Imme ? a & 0xff : Read(a);

    private int GetGet16bitImm(int a) => Imme ? a & 0xffff : ReadWord(a);

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

    public void SetIrq() => IrqEnabled = true;

    public virtual void Step()
    {
        if (NmiEnabled)
        {
            Nmi();
            NmiEnabled = false;
            return;
        }
        if (!I && IrqEnabled)
        {
            Irq();
            IrqEnabled = false;
            return;
        }

#if DEBUG
        if (PC == 0x8000 || PC == 0x8266)
        {
            var a = (Read(SP + 1) | Read(SP + 2) << 8) - 2;
            if (a > 0)
                TestAddr = PB << 16 | a;
        }
#endif

        int op = Read(PB << 16 | PC++) & 0xff;
        int mode = Disasm[op].Mode;
        int addr = GetMode(mode);
        ExecOp(op, mode, addr);
    }

    public void ExecOp(int op, int mode, int addr)
    {
        Imme = Disasm[op].Immediate;
        switch (Disasm[op].Id)
        {
            case ADC: Adc(addr); break;
            case AND: And(addr); break;
            case ASL: Asl(addr, mode); break;
            case BCC: Brn(mode, 0); break;
            case BCS: Brp(mode, 0); break;
            case BEQ: Brp(mode, 1); break;
            case BIT: Bit(addr, mode); break;
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
            case CMP: Cmp(addr); break;
            case COP: Cop(); break;
            case CPX: Cpx(addr); break;
            case CPY: Cpy(addr); break;
            case DEC: Dec(addr, mode); break;
            case DEX: Dex(); Idle(); break;
            case DEY: Dey(); Idle(); break;
            case EOR: Eor(addr); break;
            case INC: Inc(addr, mode); break;
            case INX: Inx(); Idle(); break;
            case INY: Iny(); Idle(); break;
            case JML: Jml(addr, mode); break;
            case JMP: Jmp(addr, mode); break;
            case JSR: Jsr(addr, mode); break;
            case JSL: Jsl(addr, mode); break;
            case LDA: Lda(addr); break;
            case LDX: Ldx(addr); break;
            case LDY: Ldy(addr); break;
            case LSR: Lsr(addr, mode); break;
            case MVN: Mvn(); break;
            case MVP: Mvp(); break;
            case NOP: Idle(); break;
            case ORA: Ora(addr); break;
            case PEA: Pea(addr); break;
            case PEI: Pei(addr); break;
            case PER: Per(addr); break;
            case PHA: Pha(); break;
            case PHB: Push(DB); break;
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
            case REP: Rep(addr); break;
            case ROL: Rol(addr, mode); break;
            case ROR: Ror(addr, mode); break;
            case RTI: Rti(); break;
            case RTL: Rtl(); break;
            case RTS: Rts(); break;
            case SBC: Sbc(addr); break;
            case SEC: PS |= FC; Idle(); break;
            case SED: PS |= FD; Idle(); break;
            case SEI: PS |= FI; Idle(); break;
            case SEP: Sep(addr); break;
            case STA: Sta(addr); break;
            case STP: Idle(); Idle(); break;
            case STX: Stx(addr); break;
            case STY: Sty(addr); break;
            case STZ: Stz(addr); break;
            case TAX: Tax(); break;
            case TAY: Tay(); break;
            case TCD: Tcd(); break;
            case TCS: Tcs(); break;
            case TDC: Tdc(); break;
            case TRB: Trb(addr); break;
            case TSB: Tsb(addr); break;
            case TSC: Tsc(); break;
            case TSX: Tsx(); break;
            case TXA: Txa(); break;
            case TXS: Txs(); break;
            case TXY: Txy(); break;
            case TYA: Tya(); break;
            case TYX: Tyx(); break;
            case WAI: Idle(); Idle(); break;
            case WDM: PC++; break;
            case XBA: Xba(); break;
            case XCE: Xce(); break;
        }
    }

    public List<RegisterInfo> GetFlags()
    {
        var ps = PS;
        return
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
    }

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

    public int GetReg(string reg) => reg.ToLowerInvariant() switch
    {
        "a" => A,
        "x" => X,
        "y" => Y,
        "p" => PS,
        "pc" => PBPC,
        _ => 0,
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

    public void Save(BinaryWriter bw)
    {
        bw.Write(PC); bw.Write(SP); bw.Write(A); bw.Write(X);
        bw.Write(Y); bw.Write(PS); bw.Write(PB); bw.Write(DB);
        bw.Write(E); bw.Write(D); bw.Write(FastMem); bw.Write(NmiEnabled);
        bw.Write(IrqEnabled); bw.Write(Cycles);
    }

    public void Load(BinaryReader br)
    {
        PC = br.ReadInt32(); SP = br.ReadInt32(); A = br.ReadInt32(); X = br.ReadInt32();
        Y = br.ReadInt32(); PS = br.ReadInt32(); PB = br.ReadInt32(); DB = br.ReadInt32();
        E = br.ReadBoolean(); D = br.ReadInt32(); FastMem = br.ReadBoolean(); NmiEnabled = br.ReadBoolean();
        IrqEnabled = br.ReadBoolean(); Cycles = br.ReadUInt64();
    }
}
