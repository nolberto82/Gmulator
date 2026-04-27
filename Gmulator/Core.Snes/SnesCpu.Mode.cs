namespace Gmulator.Core.Snes;

public partial class SnesCpu
{
    public int GetAddressMode(int mode)
    {
        int pbr = _pbr << 16;
        switch (mode)
        {
            case Absolute:
            {
                var a = Read(pbr | _pc++) | Read(pbr | _pc++) << 8;
                return _dbr << 16 | a;
            }
            case AbsoluteIndexedIndirect:
            {
                var a = (Read(pbr | _pc++) | Read(pbr | _pc++) << 8) + _rx & 0xffff;
                return pbr | a;
            }
            case AbsoluteIndexedX:
            {
                var a = (Read(pbr | _pc++) | Read(pbr | _pc++) << 8) & 0xffff;
                if ((a + _rx & 0xff00) != (a & 0xff00)) Idle();
                if (!XMem) Idle();
                return _dbr << 16 | a + (XMem ? (byte)_rx : _rx);
            }
            case AbsoluteIndexedY:
            {
                Idle();
                var a = (Read(pbr | _pc++) | Read(pbr | _pc++) << 8) & 0xffff;
                if ((a + _rx & 0xff00) != (a & 0xff00)) Idle();
                if (!XMem) Idle();
                return _dbr << 16 | a + _ry;
            }
            case AbsoluteIndirect:
            {
                var a = Read(pbr | _pc++) | Read(pbr | _pc++) << 8;
                return a;
            }
            case AbsoluteIndirectLong:
            {
                var a = Read(pbr | _pc++) | Read(pbr | _pc++) << 8;
                return a;
            }
            case AbsoluteLong:
            {
                var a = Read(pbr | _pc++) | Read(pbr | _pc++) << 8 | Read(pbr | _pc++) << 16;
                return a;
            }
            case AbsoluteLongIndexedX:
            {
                var a = (Read(pbr | _pc++) | Read(pbr | _pc++) << 8 | Read(pbr | _pc++) << 16) + _rx;
                return a;
            }
            case Accumulator: return 0;
            case DPIndexedIndirectX:
            {
                int a = Read(pbr | _pc++);
                var b = (a + _rx) & 0xffff;
                if ((dpr & 0xff) != 0) Idle();
                Idle();
                if (_emulationMode && (dpr & 0xff) == 0)
                {
                    int d = (dpr & 0xff00) | b & 0xff;
                    a = Read(d);
                    a |= Read((d & 0xff) == 0xff ? b + 1 : b + 1) << 8;
                }
                else
                {
                    int d = (b + dpr) & 0xffff;
                    a = Read(d);
                    a |= Read((d & 0xff) == 0xff ? (d & 0xff00) : d + 1) << 8;
                }
                return _dbr << 16 | a;
            }
            case DPIndexedX:
            {
                if ((dpr & 0xff) != 0) Idle();
                Idle();
                var a = (Read(pbr | _pc++) + dpr + _rx) & 0xffff;

                return (_emulationMode && a > 0xff ? a & 0xff | 0x100 : a) & 0xffff;;
            }
            case DPIndexedY:
            {
                int a = Read(pbr | _pc++);
                var b = (a + _ry) & 0xffff;
                if (_emulationMode && (dpr & 0xff) == 0)
                    a = (dpr & 0xff00) | b & 0xff;
                else
                    a = (b + dpr) & 0xffff;
                return a;
            }
            case DPIndirect:
            {
                var b = Read(pbr | _pc++) & 0xffff;
                if ((dpr & 0xff) != 0) Idle();
                var a = _dbr << 16 | ReadWord(b + dpr);
                return a;
            }
            case DPIndirectIndexedY:
            {
                var b = Read(pbr | _pc++) & 0xffff;
                if ((dpr & 0xff) != 0) Idle();
                Idle();
                int a = (_dbr << 16) | ReadWord(b + dpr) + _ry;
                return a;
            }
            case DPIndirectLong:
            {
                int a = ReadLong((Read(pbr | _pc++) + dpr) & 0xffff);
                return a;
            }
            case DPIndirectLongIndexedY:
            {
                int b = Read(pbr | _pc++) & 0xffff;
                if ((dpr & 0xff) != 0) Idle();
                return ReadLong(b + dpr) + _ry;
            }
            case DirectPage:
            {
                int a = Read(pbr | _pc++);
                return (a + dpr) & 0xffff;
            }
            case Immediate:
            {
                int a = Read(pbr | _pc++);
                Idle();
                return a;
            }
            case ImmediateIndex:
            {
                if (!XMem)
                    return (Read(pbr | _pc++) | Read(pbr | _pc++) << 8) & 0xffff;
                else
                    return Read(pbr | _pc++);
            }
            case ImmediateMemory:
            {
                if (!MMem)
                    return Read(pbr | _pc++) | Read(pbr | _pc++) << 8;
                else
                    return Read(pbr | _pc++);
            }
            case Implied:
                return 0;
            case ProgramCounterRelative:
                return pbr | (_pc + (sbyte)Read(pbr | _pc) + 1) & 0xffff;

            case ProgramCounterRelativeLong:
            {
                var a = Read(pbr | _pc) | (Read(pbr | _pc + 1) << 8);
                return pbr | _pc + a + 2 & 0xffff;
            }
            case SRIndirectIndexedY:
            {
                Idle(); Idle();
                return _dbr << 16 | ReadWord((Read(pbr | _pc++) + dpr + _sp) & 0xffff) + _ry;
            }
            case StackAbsolute:
                return Read(pbr | _pc++) | Read(pbr | _pc++) << 8;

            case StackDPIndirect:
            {
                var a = Read(pbr | _pc++);
                return (a + dpr) & 0xffff;
            }
            case StackInterrupt: return 0;
            case StackPCRelativeLong: return Read(pbr | _pc++) | Read(pbr | _pc++) << 8;
            case StackRelative:
            {
                if ((dpr & 0xff) != 0) Idle();
                Idle();
                return Read(pbr | _pc++) + _sp;
            }
        }
        return 0;
    }
}
