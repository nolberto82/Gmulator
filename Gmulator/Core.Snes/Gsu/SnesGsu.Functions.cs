namespace Gmulator.Core.Snes;

public partial class SnesGsu
{
    private void Stop()
    {
        if (!_irqDisabled)
        {

        }

        _statusFlag &= ~FG;
        _stopped = true;
        _r15Changed = true;
        ResetFlags();
    }

    private void Nop() { ResetFlags(); }

    private void Cache()
    {
        if (_cacheBase != (_registers[15] & 0xfff0))
        {
            _cacheBase = _registers[15] & 0xfff0;
            InvalidateCache();
        }
        ResetFlags();
    }

    private void Branch(bool flag)
    {
        ushort v = (ushort)ReadValue();
        if (flag)
            WriteRegister(15, _registers[15] + (sbyte)v);
    }

    private void Jmp(int op)
    {
        int reg = op & 0x0f;
        if (Alt1)
        {
            _pbr = (byte)_registers[reg];
            WriteRegister(15, _registers[_srcReg]);
            _cacheBase = _registers[15] & 0xfff0;
            InvalidateCache();
        }
        else
        {
            WriteRegister(15, _registers[reg]);
        }
        ResetFlags();
    }

    private void To(int op)
    {
        int reg = op & 0x0f;
        if (Prefix)
        {
            WriteRegister(reg, _registers[_srcReg]);
            ResetFlags();
        }
        else
            _dstReg = reg;
    }

    private void From(int reg)
    {
        if (Prefix)
        {
            int value = _registers[reg];
            _statusFlag = (value & 0x80) != 0 ? _statusFlag | FV : _statusFlag & ~FV;
            _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
            _statusFlag = (value & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
            WriteRegister(_dstReg, value);
            ResetFlags();
        }
        else
            _srcReg = reg;
    }

    private void With(int op)
    {
        int reg = op & 0x0f;
        _srcReg = reg;
        _dstReg = reg;
        _statusFlag |= FB;
    }

    private void Stw(int op)
    {
        int reg = op & 0x0f;
        _ramAddr = _registers[reg];
        UpdateRam(_ramAddr, _registers[_srcReg] & 0xff);
        if (!Alt1)
            UpdateRam(_ramAddr ^ 1, (_registers[_srcReg] >> 8) & 0xff);
        ResetFlags();
    }

    private void Ldw(int op)
    {
        int reg = op & 0x0f;
        _ramAddr = _registers[reg];
        int value = ReadRamBuffer(_ramAddr);
        if (!Alt1)
            value |= ReadRamBuffer(_ramAddr ^ 1) << 8;
        WriteRegister(_dstReg, value);
        ResetFlags();
    }

    private void Loop()
    {
        _registers[12]--;
        _statusFlag = (_registers[12] & 0x8000) == 0x8000 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = _registers[12] == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        if ((_statusFlag & FZ) != FZ)
            WriteRegister(15, _registers[13]);
        ResetFlags();
    }

    private void AltOne()
    {
        _statusFlag |= FAlt1;
        _statusFlag &= ~FB;
    }

    private void AltTwo()
    {
        _statusFlag |= FAlt2;
        _statusFlag &= ~FB;
    }

    private void AltThree()
    {
        _statusFlag |= FAlt1 | FAlt2;
        _statusFlag &= ~FB;
    }

    private void Merge()
    {
        int value = ((_registers[7] >> 8) & 0xff) << 8;
        value |= (_registers[8] >> 8) & 0xff;
        WriteRegister(_dstReg, value);
        _statusFlag = (value & 0xc0c0) != 0 ? _statusFlag | FV : _statusFlag & ~FV;
        _statusFlag = (value & 0x8080) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (value & 0xe0e0) != 0 ? _statusFlag | FC : _statusFlag & ~FC;
        _statusFlag = (value & 0xf0f0) != 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();
    }

    private void Swap()
    {
        int src = _registers[_srcReg];
        int value = (src & 0xff) << 8 | ((src & 0xff00) >> 8);
        WriteRegister(_dstReg, value);
        _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (value & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();
    }

    private void Add(int op)
    {
        int value;
        int reg = op & 0x0f;
        int src = _registers[_srcReg];
        if (Alt2)
            value = reg;
        else
            value = _registers[reg];

        int result = src + value + ((_statusFlag & FAlt1) != 0 ? ((_statusFlag & FC) != 0 ? 1 : 0) : 0);
        _statusFlag = (~(src ^ value) & (value ^ result) & 0x8000) != 0 ? _statusFlag | FV : _statusFlag & ~FV;
        _statusFlag = (result & 0x8000) == 0x8000 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = result > 0xffff ? _statusFlag | FC : _statusFlag & ~FC;
        _statusFlag = (result & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        WriteRegister(_dstReg, result);
        ResetFlags();
    }

    private void Sub(int reg)
    {
        int value;
        int src = _registers[_srcReg];
        if (Alt2 & !Alt1)
            value = reg;
        else
            value = _registers[reg];

        int v = src - value - (Alt1 && !Alt2 ? ((_statusFlag & FC) == 0 ? 1 : 0) : 0);
        _statusFlag = ((src ^ value) & (src ^ v) & 0x8000) != 0 ? _statusFlag | FV : _statusFlag & ~FV;
        _statusFlag = (v & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = v >= 0 ? _statusFlag | FC : _statusFlag & ~FC;
        _statusFlag = (v & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        if (!Alt3)
            WriteRegister(_dstReg, v & 0xffff);
        ResetFlags();
    }

    private void Mult(int op)
    {
        int regValue;
        if (Alt2)
            regValue = op & 0x0f;
        else
            regValue = _registers[op & 0x0f] & 0xff;

        int src = _registers[_srcReg] & 0xff;

        int value = Alt1 ? regValue * src : (sbyte)regValue * (sbyte)src;
        WriteRegister(_dstReg, value & 0xffff);
        _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (value & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();

        StepCycle(_clockSelect ? 1 : 2);
    }

    private void Fmult()
    {
        int result = (short)_registers[_srcReg] * (short)_registers[6];
        if (Alt1)
            _registers[4] = (ushort)result;

        int value = (ushort)(result >> 16);
        WriteRegister(_dstReg, value);
        _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (result & 0x8000) != 0 ? _statusFlag | FC : _statusFlag & ~FC;
        _statusFlag = (value & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();

        StepCycle((_clockSelect ? 3 : 7) * (_clockSelect ? 1 : 2));
    }

    private void And(int op)
    {
        int regvalue, value;
        int reg = op & 0x0f;
        if (Alt2)
            regvalue = reg;
        else
            regvalue = _registers[reg];

        if (Alt1)
            value = _registers[_srcReg] & ~regvalue;
        else
            value = _registers[_srcReg] & regvalue;

        _statusFlag = (value & 0x8000) == 0x8000 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = value == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        WriteRegister(_dstReg, value);
        ResetFlags();
    }

    private void Sbk()
    {
        UpdateRam(_ramAddr, _registers[_srcReg]);
        UpdateRam(_ramAddr ^ 1, _registers[_srcReg] >> 8);
        ResetFlags();
    }

    private void Link(int op)
    {
        _registers[11] = (ushort)(_registers[15] + (op & 7));
        ResetFlags();
    }

    private void Sex()
    {
        int src = _registers[_srcReg];
        int value = (sbyte)src;
        _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (value & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        WriteRegister(_dstReg, value);
        ResetFlags();
    }

    private void Not()
    {
        int value = ~_registers[_srcReg] & 0xffff;
        _statusFlag = (value & 0x8000) == 0x8000 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (value & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        WriteRegister(_dstReg, value);
        ResetFlags();
    }

    private void Lsr()
    {
        int value;
        int src = _registers[_srcReg];
        value = ((short)src >> 1) & 0x7fff;
        _statusFlag &= ~FS;
        _statusFlag = (src & 1) != 0 ? _statusFlag | FC : _statusFlag & ~FC;
        _statusFlag = value == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        WriteRegister(_dstReg, value);
        ResetFlags();
    }

    private void Rol()
    {
        int value;
        int src = _registers[_srcReg];
        value = (src << 1) & 0xffff;
        value |= (_statusFlag & FC) != 0 ? 1 : 0;
        _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (src & 0x8000) != 0 ? _statusFlag | FC : _statusFlag & ~FC;
        _statusFlag = value == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        WriteRegister(_dstReg, value);
        ResetFlags();
    }

    private void Asr()
    {
        int dst = _dstReg;
        int src = _registers[_srcReg];
        int value = (short)src >> 1;
        if (Alt1)
            value += (src + 1) >> 16;

        WriteRegister(dst, value);
        _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (src & 1) != 0 ? _statusFlag | FC : _statusFlag & ~FC;
        _statusFlag = value == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();
    }

    private void Ror()
    {
        int value;
        int src = _registers[_srcReg];
        value = (src >> 1) & 0x7fff;
        value |= (_statusFlag & FC) != 0 ? 0x8000 : 0;
        WriteRegister(_dstReg, value);
        _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (src & 1) != 0 ? _statusFlag | FC : _statusFlag & ~FC;
        _statusFlag = value == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();
    }

    private void Lob()
    {
        int value = _registers[_srcReg] & 0xff;
        WriteRegister(_dstReg, value);
        _statusFlag = (value & 0x80) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (value & 0xff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();
    }

    private void Hib()
    {
        int value = (_registers[_srcReg] & 0xff00) >> 8;
        WriteRegister(_dstReg, value);
        _statusFlag = (value & 0x80) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (value & 0xff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();
    }

    private void Ibt(int op)
    {
        int reg = op & 0x0f;
        int value = ReadValue();

        if (Alt1)
        {
            _ramAddr = value * 2;
            int lo = ReadRamBuffer(_ramAddr);
            int hi = ReadRamBuffer(_ramAddr | 1);
            WriteRegister(reg, hi << 8 | lo);
        }
        else if (Alt2)
        {
            _ramAddr = value * 2;
            UpdateRam(_ramAddr, _registers[reg]);
            UpdateRam(_ramAddr | 1, _registers[reg] >> 8);
        }
        else
        {
            value = value > 0x7f ? value | 0xff00 : value;
            WriteRegister(reg, value);
        }
        ResetFlags();
    }

    private void Iwt(int reg)
    {
        int lo = ReadValue();
        int hi = ReadValue();
        if (Alt2)
        {
            _ramAddr = lo;
            _ramAddr |= hi << 8;
            UpdateRam(_ramAddr, _registers[reg]);
            UpdateRam(_ramAddr ^ 1, _registers[reg] >> 8);
        }
        else if (Alt1)
        {
            _ramAddr = lo;
            _ramAddr |= hi << 8;
            lo = ReadRamBuffer(_ramAddr);
            hi = ReadRamBuffer(_ramAddr ^ 1);
            WriteRegister(reg, hi << 8 | lo);
        }
        else
        {
            WriteRegister(reg, (hi << 8) | lo);
        }
        ResetFlags();
    }

    private void Or(int reg)
    {
        int b, value;
        int src = _registers[_srcReg];
        if (Alt2)
            b = reg;
        else
            b = _registers[reg];

        if (Alt1)
            value = src ^ b;
        else
            value = src | b;

        WriteRegister(_dstReg, value);
        _statusFlag = (value & 0x8000) == 0x8000 ? _statusFlag | FS : _statusFlag & ~FS;
        _statusFlag = (value & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
        ResetFlags();
    }

    private void Inc(int op)
    {
        int reg = op & 0x0f;
        ushort value = _registers[reg];
        value++;
        WriteRegister(reg, value);
        _statusFlag = (value & 0x8000) == 0x8000 ? _statusFlag | 0x08 : _statusFlag & ~0x08;
        _statusFlag = value == 0 ? _statusFlag | 0x02 : _statusFlag & ~0x02;
        ResetFlags();
    }

    private void Dec(int op)
    {
        int reg = op & 0x0f;
        ushort value = _registers[reg];
        value--;
        WriteRegister(reg, value);
        _statusFlag = (value & 0x8000) == 0x8000 ? _statusFlag | 0x08 : _statusFlag & ~0x08;
        _statusFlag = value == 0 ? _statusFlag | 0x02 : _statusFlag & ~0x02;
        ResetFlags();
    }

    private void GetC()
    {
        if (!Alt2)
            _colorReg = GetColor(ReadRomBuffer());
        else if (!Alt1)
        {
            WaitRamOperation();
            _ramBank = _registers[_srcReg] & 0x01;
        }
        else
        {
            WaitRomOperation();
            _romBank = _registers[_srcReg] & 0x7f;
        }

        ResetFlags();
    }

    private void GetB()
    {
        if (Alt3)
            WriteRegister(_dstReg, (sbyte)ReadRomBuffer());
        else if (Alt2)
            WriteRegister(_dstReg, (_registers[_srcReg] & 0xff00) | ReadRomBuffer());
        else if (Alt1)
            WriteRegister(_dstReg, (ReadRomBuffer() << 8) | _registers[_srcReg] & 0xff);
        else
            WriteRegister(_dstReg, ReadRomBuffer());

        ResetFlags();
    }

    private void Plot()
    {
        if (Alt1)
        {
            int value = ReadPixel(_registers[1], _registers[2]);
            _statusFlag = (value & 0x8000) != 0 ? _statusFlag | FS : _statusFlag & ~FS;
            _statusFlag = (value & 0xffff) == 0 ? _statusFlag | FZ : _statusFlag & ~FZ;
            WriteRegister(_dstReg, value);
        }
        else
        {
            DrawPixel(_registers[1], _registers[2]);
            _registers[1]++;
        }
        ResetFlags();
    }

    private void Color()
    {
        byte value = (byte)_registers[_srcReg];
        if (Alt1)
        {
            _plotTransparent = (value & 0x01) != 0;
            _plotDither = (value & 0x02) != 0;
            _colorHighNibble = (value & 0x04) != 0;
            _colorFreezeHigh = (value & 0x08) != 0;
            _objMode = (value & 0x10) != 0;
        }
        else
            _colorReg = GetColor(value);

        ResetFlags();
    }



    private int GetTileIndex(int x, int y)
    {
        return (_objMode ? 3 : _screenHeight) switch
        {
            1 => ((x & 0xf8) << 1) + ((x & 0xf8) >> 1) + ((y & 0xf8) >> 3),
            2 => ((x & 0xf8) << 1) + ((x & 0xf8) << 0) + ((y & 0xf8) >> 3),
            3 => ((y & 0x80) << 2) + ((x & 0x80) << 1) + ((y & 0x78) << 1) + ((x & 0x78) >> 3),
            _ => ((x & 0xf8) << 1) + ((y & 0xf8) >> 3),
        };
    }

    private int GetTileAddr(int x, int y)
    {
        int id = GetTileIndex(x, y);
        return 0x700000 | (_screenBase << 10) + (id * (_colorBpp << 3)) + (y & 7) * 2;
    }

    private int ReadPixel(int x, int y)
    {
        WritePixelCache(ref SecondaryCache);
        WritePixelCache(ref PrimaryCache);

        int tileAddr = GetTileAddr(x, y);

        x = (x & 7) ^ 7;
        int value = 0;
        for (int i = 0; i < _colorBpp; i++)
        {
            int index = ((i >> 1) << 4) + (i & 1);
            value |= ((ReadByte(tileAddr + index) >> x) & 1) << i;
            StepCycle(_clockSelect ? 5 : 6);
        }
        return value;
    }

    private bool IsTransparent()
    {
        int color = _colorFreezeHigh ? _colorReg & 0x0f : _colorReg;
        return _colorBpp switch
        {
            4 => (color & 0x0f) == 0,
            8 => color == 0,
            _ => (color & 0x03) == 0,
        };
    }

    private void DrawPixel(int x, int y)
    {
        if (!_plotTransparent && IsTransparent())
            return;

        int color = _colorReg;
        if (_plotDither && _colorBpp != 8)
        {
            if (((x ^ 1) & 1) == 1)
                color >>= 4;
            color &= 0x0f;
        }

        if (PrimaryCache.X != (x & 0xf8) || PrimaryCache.Y != y)
            FlushPrimaryCache(x, y);

        int offsetx = (x & 7) ^ 7;
        PrimaryCache.Pixels[offsetx] = color;
        PrimaryCache.ValidBits |= (byte)(1 << offsetx);
        if (PrimaryCache.ValidBits == 0xff)
            FlushPrimaryCache(x, y);
    }

    private void FlushPrimaryCache(int x, int y)
    {
        WritePixelCache(ref SecondaryCache);
        SecondaryCache = PrimaryCache.DeepCopy;
        PrimaryCache.ValidBits = 0;
        PrimaryCache.X = x & 0xf8;
        PrimaryCache.Y = y;
    }

    private void WritePixelCache(ref PixelCache cache)
    {
        if (cache.ValidBits == 0)
            return;

        int tileAddr = GetTileAddr(cache.X, cache.Y);
        for (int i = 0; i < _colorBpp; i++)
        {
            int value = 0;
            for (int x = 0; x < 8; x++)
            {
                int pixel = cache.Pixels[x];
                int bit = (pixel >> i) & 1;
                value |= bit << x;
            }

            int index = ((i >> 1) << 4) + (i & 1);

            if (cache.ValidBits != 0xff)
            {
                StepCycle(_clockSelect ? 5 : 6);
                value &= cache.ValidBits;
                value |= ReadByte(tileAddr + index) & ~cache.ValidBits;
            }

            StepCycle(_clockSelect ? 5 : 6);
            WriteByte(tileAddr + index, value);
        }
        cache.ValidBits = 0;
    }

    private byte GetColor(byte value)
    {
        if (_colorHighNibble)
            return (byte)(_colorReg & 0xf0 | (value >> 4));

        if (_colorFreezeHigh)
            return (byte)(_colorReg & 0xf0 | (value & 0x0f));

        return value;
    }

    private void InvalidateCache()
    {
        Array.Fill(_cacheValid, false);
    }
}
