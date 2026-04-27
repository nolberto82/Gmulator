namespace Gmulator.Core.Snes;

public partial class SnesCpu
{
    private ushort SetValue(ushort register, int value, bool memoryMode)
    {
        if (memoryMode)
            return (ushort)((register & 0xff00) | value & 0xff);
        else
            return (ushort)value;
    }

    private void Adc(int addr, int op)
    {
        int value, b;
        bool decimalMode = (_ps & FD) != 0;
        bool mmem = MMem;
        if (mmem)
        {
            value = GetGet8bitImm(addr, op);

            if (decimalMode)
            {
                b = (_ra & 0xf) + (value & 0xf) + (_ps & FC);
                if (b > 0x09) b += 0x06;
                SetFlagC(b >= 0x10);
                b = (_ra & 0xf0) + (value & 0xf0) + (b & 0x10) + (b & 0xf);
            }
            else
                b = (byte)_ra + value + (_ps & FC);

            SetFlagV((~(_ra & 0xff ^ value) & (_ra & 0xff ^ b) & 0x80) == 0x80);

            if (decimalMode && (byte)b > 0x9f) b += 0x60;

            SetFlagC(b >= 0x100);
            _ra = (ushort)((_ra & 0xff00) | (b & 0xff));
        }
        else
        {
            value = GetGet16bitImm(addr, op);
            if (decimalMode)
            {
                b = (_ra & 0xf) + (value & 0xf) + (_ps & FC);
                if (b > 0x09) b += 0x06;
                SetFlagC(b >= 0x10);
                b = (_ra & 0xf0) + (value & 0xf0) + (b & 0x10) + (b & 0xf);
                if (b > 0x9f) b += 0x60;
                b = (_ra & 0xf00) + (value & 0xf00) + (b & 0x100) + (b & 0xff);
                if (b > 0x9ff) b += 0x600;
                b = (_ra & 0xf000) + (value & 0xf000) + (b & 0x1000) + (b & 0xfff);
            }
            else
            {
                b = _ra + value + (_ps & FC);
            }

            SetFlagV((~(_ra ^ value) & (_ra ^ b) & 0x8000) == 0x8000);

            if (decimalMode && b > 0x9fff) b += 0x6000;

            SetFlagC(b >= 0x10000);
            _ra = (ushort)(b & 0xffff);
        }
        SetFlagZN(b, mmem);
    }

    private void And(int a, int op)
    {
        bool mmem = MMem;
        if (Disasm[op].Immediate)
        {
            if (mmem)
                _ra = (ushort)((_ra & 0xff00) | _ra & a);
            else
                _ra &= (ushort)a;
        }
        else
        {
            if (mmem)
                _ra = (ushort)(_ra & 0xff00 | Read(a) & _ra);
            else
                _ra &= ReadWord(a);
        }
        SetFlagZN(_ra, mmem);
    }

    private void Asl(int value, int op)
    {
        int result;
        bool mmem = MMem;
        if (op == 0x0a)
        {
            if (mmem)
            {
                SetFlagC((_ra & 0x80) != 0);
                result = _ra & 0xff00 | ((_ra & 0xff) << 1) & 0xfe;
            }
            else
            {
                SetFlagC((_ra & 0x8000) != 0);
                result = (_ra << 1) & 0xfffe;
            }
            _ra = (ushort)result;
        }
        else
        {
            if (mmem)
            {
                result = Read(value);
                SetFlagC((result & 0x80) != 0);
                result = (result << 1) & 0xfe;
                Write(value, (byte)result);
            }
            else
            {
                result = ReadWord(value);
                SetFlagC((result & 0x8000) == 0x8000);
                result = (result << 1) & 0xfffe;
                WriteWord(value, result);
                Idle(); Idle();
            }
        }
        SetFlagZN(result, mmem);
    }

    private void Brp(int m, int f)
    {
        if ((_ps & (1 << f)) != 0)
        {
            Bra(m);
            Idle();
        }
        else
        {
            _pc++;
            Read(_pc);
        }
    }

    private void Brn(int m, int f)
    {
        if ((_ps & (1 << f)) == 0)
        {
            Bra(m);
            Idle();
        }
        else
        {
            _pc++;
            Read(_pc);
        }
    }

    private void Bra(int op)
    {
        _pc = (ushort)GetAddressMode(Disasm[op].Mode);
        if (_emulationMode)
            Idle();
    }

    private void Bit(int a, int op)
    {
        int v, b;
        bool mmem = MMem;
        if (Disasm[op].Immediate)
        {
            if (mmem)
            {
                b = GetGet8bitImm(a, op);
                v = (byte)_ra & b;
                _ps = (byte)(((v & 0xFF) == 0) ? (_ps | FZ) : (_ps & ~FZ));
            }
            else
            {
                b = GetGet16bitImm(a, op);
                v = _ra & b;
                _ps = (byte)(((v & 0xFFFF) == 0) ? (_ps | FZ) : (_ps & ~FZ));
            }
        }
        else
        {
            if (mmem)
            {
                b = Read(a);
                v = (byte)_ra & b;
                _ps = (byte)(((v & 0xFF) == 0) ? (_ps | FZ) : (_ps & ~FZ));
                _ps = (byte)(((b & FN) != 0) ? (_ps | FN) : (_ps & ~FN));
                SetFlagV((b & FV) != 0);
            }
            else
            {
                b = ReadWord(a);
                v = _ra & b;
                _ps = (byte)(((v & 0xFFFF) == 0) ? (_ps | FZ) : (_ps & ~FZ));
                _ps = (byte)(((b & 0x8000) != 0) ? (_ps | FN) : (_ps & ~FN));
                SetFlagV((b & 0x4000) != 0);
                Idle();
            }
        }
    }

    private void Cmp(int a, int op)
    {
        int v, r;
        bool mmem = MMem;
        if (mmem)
            v = GetGet8bitImm(a, op);
        else
            v = GetGet16bitImm(a, op);

        r = _ra - v;
        SetFlagC(mmem ? (_ra & 0xff) >= v : (_ra & 0xffff) >= v);
        SetFlagZN(r, mmem);
    }

    private void Cpx(int a, int op)
    {
        int v, r;
        bool xmem = XMem;
        if (xmem)
            v = GetGet8bitImm(a, op);
        else
            v = GetGet16bitImm(a, op);

        r = _rx - v;
        SetFlagC(_rx >= v);
        SetFlagZN(r, xmem);
    }

    private void Cpy(int a, int op)
    {
        int v, r;
        bool xmem = XMem;
        if (xmem)
            v = GetGet8bitImm(a, op);
        else
            v = GetGet16bitImm(a, op);

        r = _ry - v;
        SetFlagC(_ry >= v);
        SetFlagZN(r, xmem);
    }

    private void Dec(int a, int op)
    {
        int b;
        var mmem = MMem;
        if (op == 0x3a)
        {
            if (mmem)
                _ra = (ushort)(_ra & 0xff00 | (_ra - 1) & 0xff);
            else
                _ra--;
            SetFlagZN(_ra, mmem);
        }
        else
        {
            if (mmem)
            {
                b = (byte)(Read(a) - 1);
                Write(a, (byte)b);
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
        _rx = SetValue(_rx, _rx - 1, XMem);
        SetFlagZN(_rx, XMem);
    }

    private void Dey()
    {
        _ry = SetValue(_ry, _ry - 1, XMem);
        SetFlagZN(_ry, XMem);
    }

    private void Eor(int addr, int op)
    {
        var mmem = MMem;
        if (Disasm[op].Immediate)
        {
            addr = _ra ^ addr;
            _ra = (ushort)addr;
        }
        else
        {
            if (mmem)
                _ra ^= GetGet8bitImm(addr, op);
            else
                _ra ^= GetGet16bitImm(addr, op);
        }
        SetFlagZN(_ra, mmem);
    }

    private void Inc(int a, int op)
    {
        var mmem = MMem;
        if (op == 0x1a)
        {
            if (mmem)
            {
                int v = _ra + 1;
                _ra = (ushort)((_ra & 0xff00) | (byte)v);
            }
            else
                _ra++;
            SetFlagZN(_ra, mmem);
        }
        else
        {
            int b;
            if (mmem)
            {
                b = (byte)(Read(a) + 1);
                Write(a, (byte)b);
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
        _rx = SetValue(_rx, _rx + 1, XMem);
        SetFlagZN(_rx, XMem);
    }

    private void Iny()
    {
        _ry = SetValue(_ry, _ry + 1, XMem);
        SetFlagZN(_ry, XMem);
    }

    private void Jml(int value, int op)
    {
        if (op == 0x5c)
        {
            _pbr = (byte)(value >> 16);
            _pc = (ushort)value;
        }
        else if (op == 0x6c)
        {
            var addr = ReadLong(value);
            _pc = (ushort)addr;
            _pbr = (byte)(addr >> 16);
        }

    }

    private void Jmp(int value, int op)
    {
        if (op == 0x4c)
            _pc = (ushort)value;
        else if (op == 0xdc)
        {
            var addr = ReadLong(value);
            _pc = (ushort)addr;
            _pbr = (byte)(addr >> 16);
        }
        else
            _pc = ReadWord(value);
    }

    private void Jsr(int v, int op)
    {
        _pc--;
        if (op == 0x20 || op == 0x22)
        {
            PushWord(_pc, true);
            _pc = (ushort)(_pbr << 16 | (ushort)v);
        }
        else
        {
            PushWord(_pc);
            _pc = ReadWord(v);
            WrapStackPointer();
        }
    }

    private void Jsl(int v)
    {
        var pc = this._pc - 1;
        Push(_pbr);
        Push((byte)(pc >> 8));
        Push((byte)pc);
        this._pc = (ushort)v;
        _pbr = (byte)(v >> 16);
        WrapStackPointer();
    }

    private void Lda(int value, int op)
    {
        var mmem = MMem;
        if (Disasm[op].Immediate)
            _ra = SetValue(_ra, value, MMem);
        else
        {
            if (mmem)
                _ra = (ushort)((_ra & 0xff00) | GetGet8bitImm(value, op));
            else
                _ra = GetGet16bitImm(value, op);
        }
        SetFlagZN(_ra, mmem);
    }

    private void Ldx(int value, int op)
    {
        var xmem = XMem;
        if (Disasm[op].Immediate)
            _rx = SetValue(_rx, value, xmem);
        else
            _rx = xmem ? SetValue(_rx, Read(value), xmem) : ReadWord(value);
        SetFlagZN(_rx, xmem);
    }

    private void Ldy(int value, int op)
    {
        var xmem = XMem;
        if (Disasm[op].Immediate)
            _ry = SetValue(_ry, value, xmem);
        else
            _ry = xmem ? SetValue(_ry, Read(value), xmem) : ReadWord(value);

        SetFlagZN(_ry, xmem);
    }

    private void Lsr(int value, int op)
    {
        int result;
        bool mmem = MMem;
        if (op == 0x4a)
        {
            SetFlagC(((byte)_ra & 0x01) != 0);
            if (mmem)
            {
                result = _ra & 0xff;
                result = (result >> 1) & 0x7f;
            }
            else
                result = (_ra >> 1) & 0x7fff;
            _ra = SetValue(_ra, result, mmem);
        }
        else
        {
            if (mmem)
            {
                result = Read(value);
                SetFlagC((result & 0x01) != 0);
                result = (byte)((result >> 1) & 0x7f);
                Write(value, (byte)result);
            }
            else
            {
                result = ReadWord(value);
                SetFlagC((result & 0x01) != 0);
                result = (ushort)((result >> 1) & 0x7fff);
                WriteWord(value, result);
            }
        }
        SetFlagZN(result, mmem);
    }

    private void Mvn()
    {
        bool xmem = XMem;
        int src = Read(PBPC + 1);
        int dst = Read(PBPC);
        _dbr = (byte)dst;
        int v = Read((src << 16) | (xmem ? (byte)_rx : _rx));
        Write((dst << 16) | (xmem ? (byte)_ry : _ry), (byte)v);
        _rx++;
        _ry++;
        _ra = (ushort)(_ra - 1);
        _pc = (ushort)((_ra != 0xffff) ? (_pc - 1) : (_pc + 2));
        Idle();
    }

    private void Mvp()
    {
        bool xmem = XMem;
        int src = Read(_pbr << 16 | (_pc + 1));
        int dst = Read(_pbr << 16 | _pc);
        _dbr = (byte)dst;
        byte value = Read((src << 16) | (xmem ? (byte)_rx : _rx));
        Write((dst << 16) | (xmem ? (byte)_ry : _ry), value);
        _rx--;
        _ry--;
        _ra = (ushort)(_ra - 1);
        _pc = (ushort)((_ra != 0xffff) ? (_pc - 1) : (_pc + 2));
        Idle();
    }

    public virtual void Irq()
    {
        if (!_emulationMode)
        {
            Push(_pbr);
            Push((byte)(_pc >> 8));
            Push((byte)(_pc & 0xff));
            Push(_ps);
            _ps |= FI;
            Idle();
        }
        else
        {
            Push((byte)(_pc >> 8));
            Push((byte)(_pc & 0xff));
            Push(_ps);
        }
        _pc = ReadWord(IRQn);
        _pbr = 0;
    }

    public virtual void Nmi()
    {
        if (!_emulationMode)
        {
            Push(_pbr);
            Push((byte)(_pc >> 8));
            Push((byte)(_pc & 0xff));
            Push(_ps);
            _ps |= FI;
            Idle();
        }
        else
        {
            Push((byte)(_pc >> 8));
            Push((byte)(_pc & 0xff));
            Push(_ps);
        }
        _pc = ReadWord(NMIn);
        _pbr = 0;
    }

    private void Ora(int addr, int op)
    {
        bool mmem = MMem;
        if (Disasm[op].Immediate)
        {
            addr = _ra | addr;
            _ra = (ushort)addr;
        }
        else
        {
            if (mmem)
                _ra |= GetGet8bitImm(addr, op);
            else
                _ra |= GetGet16bitImm(addr, op);
        }
        SetFlagZN(_ra, mmem);
    }

    private void Pea(int v)
    {
        PushWord(v);
        WrapStackPointer();
    }

    private void Pei(int v)
    {
        PushWord(ReadWord(v));
        WrapStackPointer();
    }

    private void Per(int v)
    {
        PushWord(_pc + v);
        WrapStackPointer();
    }

    private void Pha()
    {
        PushM(_ra);
        WrapStackPointer();
    }

    private void Phd()
    {
        PushWord(dpr);
        WrapStackPointer();
    }

    private void Phk()
    {
        Push(_pbr);
        WrapStackPointer();
    }

    private void Php()
    {
        Push(_ps);
        WrapStackPointer();
    }

    private void Pla()
    {
        bool mmem = MMem;
        if (mmem)
            _ra = (ushort)(_ra & 0xff00 | Pop(true));
        else
            _ra = PopWord();
        SetFlagZN(_ra, mmem);
    }

    private void Pld()
    {
        dpr = PopWord();
        SetFlagZN(dpr, false);
        WrapStackPointer();
    }

    private void Plx()
    {
        bool xmem = XMem;
        if (xmem)
            _rx = SetValue(_rx, Pop(true), xmem);
        else
            _rx = PopWord();
        SetFlagZN(_rx, xmem);
    }

    private void Ply()
    {
        bool xmem = XMem;
        if (xmem)
            _ry = SetValue(_ry, Pop(true), xmem);
        else
            _ry = PopWord();
        SetFlagZN(_ry, xmem);
    }

    private void Plb()
    {
        _dbr = Pop();
        SetFlagZN(_dbr, true);
        WrapStackPointer();
        Idle(); Idle();
    }

    private void Plp()
    {
        if (_emulationMode)
            _ps = (byte)(Pop(true) | 0x30);
        else
            _ps = Pop();

        if (XMem)
        {
            _rx &= 0xff;
            _ry &= 0xff;
        }
        WrapStackPointer();
    }

    private void Rep(int v)
    {
        _ps &= (byte)~v;
        if (_emulationMode)
            _ps |= FX | FM;
    }

    private void Rol(int value, int op)
    {
        int msb;
        int result;
        bool mmem = MMem;
        if (op == 0x2a)
        {
            if (mmem)
            {
                msb = _ra & 0x80;
                result = _ra & 0xff;
                result <<= 1;
                if ((_ps & FC) != 0)
                    result |= 0x01;
                _ra = SetValue(_ra, result, mmem);
            }
            else
            {
                msb = _ra & 0x8000;
                _ra <<= 1;
                if ((_ps & FC) == FC)
                    _ra |= 0x01;
                result = _ra & 0xffff;
            }
        }
        else
        {
            if (mmem)
            {
                result = Read(value);
                msb = result & 0x80;
                result = (byte)(result << 1);
                if ((_ps & FC) != 0)
                    result |= 0x01;
                Write(value, (byte)result);
            }
            else
            {
                result = ReadWord(value);
                msb = result & 0x8000;
                result = (ushort)(result << 1);
                if ((_ps & FC) == FC)
                    result |= 0x01;
                WriteWord(value, result);
            }
        }
        SetFlagC(msb != 0);
        SetFlagZN(result, mmem);
    }

    private void Ror(int value, int op)
    {
        int bit0, result;
        bool mmem = MMem;
        if (op == 0x6a)
        {
            if (mmem)
            {
                result = _ra & 0xff;
                bit0 = result & 0x01;
                result >>= 1;
                if ((_ps & FC) != 0)
                    result |= 0x80;
                _ra = SetValue(_ra, result, mmem);
            }
            else
            {
                bit0 = _ra & 0x01;
                _ra >>= 1;
                if ((_ps & FC) == FC)
                    _ra |= 0x8000;
                result = _ra & 0xffff;
            }
        }
        else
        {
            if (mmem)
            {
                result = Read(value);
                bit0 = result & 0x01;
                result = (byte)(result >> 1);
                if ((_ps & FC) != 0)
                    result |= 0x80;
                Write(value, (byte)result);
            }
            else
            {
                result = ReadWord(value);
                bit0 = result & 0x01;
                result = (ushort)(result >> 1);
                if ((_ps & FC) != 0)
                    result |= 0x8000;
                WriteWord(value, result);
            }

        }
        SetFlagC(bit0 != 0);
        SetFlagZN(result, mmem);
    }

    private void Rti()
    {
        if (_emulationMode)
        {
            _ps = (byte)(Pop(true) | 0x30);
            _pc = PopWord();
        }
        else
        {
            _ps = Pop();
            _pc = PopWord();
            _pbr = Pop();
        }
    }

    private void Rtl()
    {
        var v = _pbr << 16 | PopWord();
        _pbr = Pop();
        _pc = (ushort)(v | _pbr << 16);
        _pc++;
        WrapStackPointer();
    }

    private void Rts()
    {
        _pc = Pop(true);
        _pc |= (ushort)(Pop(true) << 8);
        _pc |= (ushort)(_pbr << 16);
        _pc++;
    }

    private void Sep(int v)
    {
        _ps |= (byte)v;
        if (_emulationMode)
            _ps |= FX | FM;

        if (XMem)
        {
            _rx &= 0xFF;
            _ry &= 0xFF;
        }
    }

    private void Sbc(int a, int op)
    {
        int v, b;
        bool decimalMode = (_ps & FD) != 0;
        bool mmem = MMem;
        if (mmem)
        {
            v = ~GetGet8bitImm(a, op) & 0xff;
            if (decimalMode)
            {
                b = (ushort)((_ra & 0xf) + (v & 0xf) + FlagC);
                if (b < 0x10) b -= 0x06;
                SetFlagC(b >= 0x10);
                b = (ushort)((_ra & 0xf0) + (v & 0xf0) + (b & 0x10) + (b & 0xf));
            }
            else
                b = (_ra & 0xff) + v + FlagC;

            SetFlagV((~(_ra & 0xff ^ v) & (_ra & 0xff ^ b) & 0x80) == 0x80);
            if (decimalMode && b < 0x100) b -= 0x60;

            SetFlagC(b > 0xff);
            _ra = (ushort)(_ra & 0xff00 | b & 0xff);
        }
        else
        {
            v = (ushort)~GetGet16bitImm(a, op);
            if (decimalMode)
            {
                b = (_ra & 0xf) + (v & 0xf) + FlagC;
                if (b < 0x10) b -= 0x06;
                SetFlagC(b >= 0x10);
                b = (_ra & 0xf0) + (v & 0xf0) + (b > 0xf ? 0x10 : 0) + (b & 0xf);
                if (b < 0x100) b -= 0x60;
                b = (_ra & 0xf00) + (v & 0xf00) + (b > 0xff ? 0x100 : 0) + (b & 0xff);
                if (b < 0x1000) b -= 0x600;
                b = (_ra & 0xf000) + (v & 0xf000) + (b > 0xfff ? 0x1000 : 0) + (b & 0xfff);
            }
            else
                b = _ra + v + FlagC;

            SetFlagV((~(_ra ^ v) & (_ra ^ b) & 0x8000) == 0x8000);
            if (decimalMode && b < 0x10000) b -= 0x6000;

            SetFlagC(b > 0xffff);

            _ra = (ushort)(b & 0xffff);
        }
        SetFlagZN(b, mmem);
    }

    private void Sta(int a)
    {
        if (MMem)
            Write(a, (byte)_ra);
        else
            WriteWord(a, _ra);
    }

    private void Stx(int a)
    {
        if (XMem)
            Write(a, (byte)_rx);
        else
            WriteWord(a, _rx);
    }

    private void Sty(int a)
    {
        if (XMem)
            Write(a, (byte)_ry);
        else
            WriteWord(a, _ry);
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
        dpr = _ra;
        Idle();
        SetFlagZN(dpr, false);
    }

    private void Tdc()
    {
        _ra = dpr;
        Idle();
        SetFlagZN(_ra, false);
    }

    private void Trb(int a)
    {
        int v;
        bool mmem = MMem;
        if (mmem)
        {
            v = Read(a);
            Write(a, (byte)(v & 0xff & ~_ra));
            v &= _ra;
        }
        else
        {
            v = ReadWord(a);
            WriteWord(a, v & ~_ra);
            v &= _ra;
        }
        SetFlagZ(v, mmem);
    }

    private void Tsb(int a)
    {
        bool mmem = MMem;
        if (mmem)
        {
            int v = Read(a);
            Write(a, (byte)(v | _ra));
            v &= _ra;
            SetFlagZ(v, mmem);
        }
        else
        {
            int v = ReadWord(a);
            WriteWord(a, v | _ra);
            v &= _ra;
            SetFlagZ(v, mmem);
        }
    }

    private void Tcs()
    {
        if (_emulationMode)
            SetSp(_ra, true);
        else
            _sp = _ra;
        Idle();
    }

    private void Tsc()
    {
        _ra = _sp;
        Idle();
        SetFlagZN(_ra, false);
    }

    private void Tax()
    {
        _rx = SetValue(_rx, _ra, XMem);
        SetFlagZN(_rx, XMem);
        Idle();
    }

    private void Tay()
    {
        _ry = SetValue(_ry, _ra, XMem); ;
        SetFlagZN(_ry, XMem);
        Idle();
    }

    private void Tsx()
    {
        _rx = _sp;
        SetFlagZN(_rx, XMem);
        Idle();
    }

    private void Txa()
    {
        _ra = MMem ? SetValue(_ra, _rx, MMem) : _rx;
        SetFlagZN(_ra, MMem);
        Idle();
    }

    private void Txs()
    {
        _sp = (ushort)(_emulationMode ? _rx | 0x100 : _rx);
        Idle();
    }

    private void Txy()
    {
        _ry = _rx;
        SetFlagZN(_rx, XMem);
        Idle();
    }

    private void Tyx()
    {
        _rx = _ry;
        SetFlagZN(_rx, XMem);
        Idle();
    }

    private void Tya()
    {
        _ra = SetValue(_ra, _ry, MMem);
        SetFlagZN(_ra, MMem);
        Idle();
    }

    private void Xba()
    {
        int al, ah;
        (al, ah) = (_ra >> 8, (byte)_ra);
        _ra = (ushort)(ah << 8 | al);
        Idle(); Idle();
        SetFlagZN(_ra, true);
    }

    private void Xce()
    {
        (_emulationMode, bool c) = (FlagC != 0, _emulationMode);
        _ps = c ? _ps |= FC : _ps &= unchecked((byte)~FC);
        if (_emulationMode)
        {
            _ps |= FX | FM;
            _rx &= 0xff;
            _ry &= 0xff;
            _sp &= 0xff | 1 << 8;
        }
        Idle();
    }

    private byte Pop(bool e = false)
    {
        SetSp(_sp + 1, e);
        return Read(_sp);
    }

    private ushort PopWord() => (ushort)(Read(++_sp) | Read(++_sp) << 8);

    public void Push(int v, bool e = false)
    {
        Idle();
        Write(_sp, (byte)(v & 0xff));
        SetSp(_sp - 1, e);
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
        WrapStackPointer();
    }

    private void Brk()
    {
        Read(_pc++);
        if (_emulationMode)
        {
            PushWord(_pc, true);
            Push(_ps | FM, true);
            _pc = ReadWord(BRKe);
        }
        else
        {
            Push(_pbr);
            PushWord(_pc);
            Push(_ps, true);
            _pc = ReadWord(BRKn);
        }
        _ps |= FI;
        _ps &= unchecked((byte)~FD);
        _pbr = 0;
    }

    private void Cop()
    {
        Read(_pc++);
        if (_emulationMode)
        {
            PushWord(_pc);
            Push(_ps | FM, true);
            _pc = ReadWord(COPe);
        }
        else
        {
            Push(_pbr);
            PushWord(_pc);
            Push(_ps, true);
            _pc = ReadWord(COPn);
            Idle();
        }
        _ps |= FI;
        _ps &= unchecked((byte)~FD);
        _pbr = 0;
    }

    public virtual void Reset(bool isSa1)
    {
        _sp = 0x0200;
        Read(0);
        Idle();
        Read(0x100 | _sp--);
        Read(0x100 | _sp--);
        if (!isSa1)
            _pc = (ushort)(Read(RESETe) | Read(RESETe + 1) << 8);

        _ra = _rx = _ry = 0;
        _ps = 0x34;
        _dbr = 0x00;
        _pbr = 0x00;
        dpr = 0x0000;
        _emulationMode = true;
        FastMem = false;
        NmiEnabled = false;
        IrqEnabled = false;
        Cycles = 0;
        StepOverAddr = -1;
        _stepCounter = 0;
    }

    private void SetFlagC(bool flag)
    {
        if (flag) _ps |= FC;
        else _ps &= unchecked((byte)~FC);
    }

    private void SetFlagZ(int v, bool isbyte)
    {
        if (isbyte)
        {
            if ((v & 0xff) == 0) _ps |= FZ;
            else _ps &= unchecked((byte)~FZ);
        }
        else
        {
            if ((v & 0xffff) == 0) _ps |= FZ;
            else _ps &= unchecked((byte)~FZ);
        }
    }

    private void SetFlagZN(int v, bool isbyte)
    {
        if (isbyte)
        {
            if ((v & 0xff) == 0) _ps |= FZ;
            else _ps &= unchecked((byte)~FZ);
            if ((v & FN) != 0) _ps |= FN;
            else _ps &= unchecked((byte)~FN);
        }
        else
        {
            if ((v & 0xffff) == 0) _ps |= FZ;
            else _ps &= unchecked((byte)~FZ);
            if ((v & FN << 8) != 0) _ps |= FN;
            else _ps &= unchecked((byte)~FN);
        }
    }

    private void SetFlagV(bool flag)
    {
        if (flag) _ps |= FV;
        else _ps &= unchecked((byte)~FV);
    }
}
