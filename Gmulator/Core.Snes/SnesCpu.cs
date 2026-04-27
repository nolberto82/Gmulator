using Gmulator.Interfaces;
using System.Runtime.CompilerServices;

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

    #region State
    protected ushort _pc, _sp, _ra, _rx, _ry, dpr;

    protected byte _ps, _dbr, _pbr;
    protected bool _emulationMode;
    public ushort X => _rx;
    public ushort Y => _ry;
    public bool EmulationMode => _emulationMode;
    public byte DataBank => _dbr;
    public ushort DirectPageReg => dpr;
    public bool FastMem { get; set; }
    public bool NmiEnabled { get; set; }
    public bool IrqEnabled { get; set; }
    public ulong Cycles { get => cycles; set => cycles = value; }
    private ulong cycles;
    #endregion


    public bool XMem => (_ps & FX) != 0;
    public bool MMem => (_ps & FM) != 0;
    public int FlagC => _ps & FC;
    public bool I => (_ps & FI) != 0;
    public int PBPC => _pbr << 16 | _pc;
    public Action Tick { get; set; }

    public int OpenBus { get; set; }
    public int StepOverAddr { get; set; }
    private int _debugPC;
    private int _debugAddr;
    private readonly int _debugMode;

    private Snes Snes;
    private SnesPpu Ppu;
    private SnesMapper Mapper;

    public Action<int> SetState;

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
            Snes.Run = true;
            Step();
            return true;
        }
        else if (debugState == DebugState.StepSa1)
        {
            //if (Snes.Sa1?.Cpu.DebugStep() == true)
            //    StepOneCycle();
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

    public void SetSa1(Snes snes)
    {
        Snes = snes;
    }

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
                //_debugMode = Disasm[_opCode].Mode;
                _stepCounter++;
                break;
            case 2:
                _pc = (ushort)_debugPC;
                _debugAddr = GetAddressMode(Disasm[_opCode].Mode);
                ExecOp(_opCode, _debugAddr);
                _stepCounter = 0;
                break;
        }
    }

    private void PpuCycle()
    {
        var c = GetClockSpeed(PBPC);
        Ppu.Step(c);
        cycles++;
    }

    public void Idle()
    {
        Snes?.HandleDma();
        Ppu?.Step(6);
        cycles++;
    }

    public void Idle8()
    {
        Ppu.Step(8);
        cycles++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual byte Read(int a)
    {
        var c = GetClockSpeed(a);
        Ppu?.Step(c);
        Snes?.HandleDma();
        cycles++;
        return (byte)(OpenBus = Snes?.ReadMemory(a) & 0xff ?? 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Write(int a, byte v)
    {
        var c = GetClockSpeed(a);
        Ppu?.Step(c);
        Snes?.HandleDma();
        cycles++;
        Snes?.WriteMemory(a, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetClockSpeed(int a)
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

    public ushort ReadWord(int a)
    {
        int low = Read(a);
        int high = Read(a + 1);
        return (ushort)(((high << 8) | low) & 0xffff);
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
        Write(a, (byte)v);
        Write(a + 1, (byte)(v >> 8));
    }

    private byte GetGet8bitImm(int a, int op)
    {
        return (byte)(Disasm[op].Immediate ? a & 0xff : Read(a));
    }

    private ushort GetGet16bitImm(int a, int op)
    {
        return (ushort)(Disasm[op].Immediate ? a & 0xffff : ReadWord(a));
    }

    private void WrapStackPointer()
    {
        if (_emulationMode)
            _sp = (ushort)(0x100 | (_sp & 0xff));
    }

    private void SetSp(int s, bool e)
    {
        if (e && _emulationMode)
            _sp = (ushort)(0x100 | (s & 0xff));
        else
            _sp = (ushort)s;
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
        if (_pc == 0x8000 || _pc == 0x8266)
        {
            var a = (Read(_sp + 1) | Read(_sp + 2) << 8) - 2;
            if (a > 0)
                TestAddr = _pbr << 16 | a;
        }
#endif

        int op = Read(_pbr << 16 | _pc++) & 0xff;
        int addr = GetAddressMode(Disasm[op].Mode);
        ExecOp(op, addr);
    }

    public void ExecOp(int op, int addr)
    {
        switch (Disasm[op].Id)
        {
            case ADC: Adc(addr, op); break;
            case AND: And(addr, op); break;
            case ASL: Asl(addr, op); break;
            case BCC: Brn(op, 0); break;
            case BCS: Brp(op, 0); break;
            case BEQ: Brp(op, 1); break;
            case BIT: Bit(addr, op); break;
            case BMI: Brp(op, 7); break;
            case BNE: Brn(op, 1); break;
            case BPL: Brn(op, 7); break;
            case BRA: Bra(op); break;
            case BRK: Brk(); break;
            case BRL: Bra(op); break;
            case BVC: Brn(op, 6); break;
            case BVS: Brp(op, 6); break;
            case CLC: _ps = (byte)(_ps & ~FC); Idle(); break;
            case CLD: _ps = (byte)(_ps & ~FD); Idle(); break;
            case CLI: _ps = (byte)(_ps & ~FI); Idle(); break;
            case CLV: _ps = (byte)(_ps & ~FV); Idle(); break;
            case CMP: Cmp(addr, op); break;
            case COP: Cop(); break;
            case CPX: Cpx(addr, op); break;
            case CPY: Cpy(addr, op); break;
            case DEC: Dec(addr, op); break;
            case DEX: Dex(); Idle(); break;
            case DEY: Dey(); Idle(); break;
            case EOR: Eor(addr, op); break;
            case INC: Inc(addr, op); break;
            case INX: Inx(); Idle(); break;
            case INY: Iny(); Idle(); break;
            case JML: Jml(addr, op); break;
            case JMP: Jmp(addr, op); break;
            case JSR: Jsr(addr, op); break;
            case JSL: Jsl(addr); break;
            case LDA: Lda(addr, op); break;
            case LDX: Ldx(addr, op); break;
            case LDY: Ldy(addr, op); break;
            case LSR: Lsr(addr, op); break;
            case MVN: Mvn(); break;
            case MVP: Mvp(); break;
            case NOP: Idle(); break;
            case ORA: Ora(addr, op); break;
            case PEA: Pea(addr); break;
            case PEI: Pei(addr); break;
            case PER: Per(addr); break;
            case PHA: Pha(); break;
            case PHB: Push(_dbr); break;
            case PHD: Phd(); break;
            case PHK: Phk(); break;
            case PHP: Php(); break;
            case PHX: PushX(_rx); break;
            case PHY: PushX(_ry); break;
            case PLA: Pla(); break;
            case PLB: Plb(); break;
            case PLD: Pld(); break;
            case PLP: Plp(); break;
            case PLX: Plx(); break;
            case PLY: Ply(); break;
            case REP: Rep(addr); break;
            case ROL: Rol(addr, op); break;
            case ROR: Ror(addr, op); break;
            case RTI: Rti(); break;
            case RTL: Rtl(); break;
            case RTS: Rts(); break;
            case SBC: Sbc(addr, op); break;
            case SEC: _ps |= FC; Idle(); break;
            case SED: _ps |= FD; Idle(); break;
            case SEI: _ps |= FI; Idle(); break;
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
            case WDM: _pc++; break;
            case XBA: Xba(); break;
            case XCE: Xce(); break;
        }
    }

    public List<RegisterInfo> GetFlags()
    {
        return
        [
            new("","C",$"{(_ps & FC) != 0}"),
            new("","Z",$"{(_ps & FZ) != 0}"),
            new("","I",$"{(_ps & FI) != 0}"),
            new("","D",$"{(_ps & FD) != 0}"),
            new("","X",$"{(_ps & FX) != 0}"),
            new("","M",$"{(_ps & FM) != 0}"),
            new("","V",$"{(_ps & FV) != 0}"),
            new("","N",$"{(_ps & FN) != 0}"),
            new("","E",$"{_emulationMode}"),
        ];
    }

    public List<RegisterInfo> GetRegisters() =>
    [
        new("","A ",$"{_ra:X4}"),
        new("","X ",$"{_rx:X4}"),
        new("","Y ",$"{_ry:X4}"),
        new("","SP",$"{_sp:X4}"),
        new("","D ",$"{dpr:X4}"),
        new("","P ",$"{_ps:X4}"),
        new("","DB",$"{_dbr:X2}"),
        new("","PB",$"{_pbr:X2}"),
    ];

    public int GetReg(string reg) => reg.ToLowerInvariant() switch
    {
        "a" => _ra,
        "x" => _rx,
        "y" => _ry,
        "p" => _ps,
        "pc" => PBPC,
        _ => 0,
    };

    public void SetReg(string reg, int v)
    {
        switch (reg.ToLowerInvariant())
        {
            case "a": _ra = (ushort)v; break;
            case "x": _rx = (ushort)v; break;
            case "y": _ry = (ushort)v; break;
            case "p": _ps = (byte)v; break;
            case "pc": _pc = (ushort)v; break;
        }
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(_pc); bw.Write(_sp); bw.Write(_ra); bw.Write(_rx);
        bw.Write(_ry); bw.Write(_ps); bw.Write(_pbr); bw.Write(_dbr);
        bw.Write(_emulationMode); bw.Write(dpr); bw.Write(FastMem); bw.Write(NmiEnabled);
        bw.Write(IrqEnabled); bw.Write(Cycles);
    }

    public void Load(BinaryReader br)
    {
        _pc = br.ReadUInt16(); _sp = br.ReadUInt16(); _ra = br.ReadUInt16(); _rx = br.ReadUInt16();
        _ry = br.ReadUInt16(); _ps = br.ReadByte(); _pbr = br.ReadByte(); _dbr = br.ReadByte();
        _emulationMode = br.ReadBoolean(); dpr = br.ReadUInt16(); FastMem = br.ReadBoolean(); NmiEnabled = br.ReadBoolean();
        IrqEnabled = br.ReadBoolean(); Cycles = br.ReadUInt64();
    }
}
