namespace Gmulator.Core.Snes;

public sealed partial class SnesPpu
{
    private readonly int[] ppuaddrinc = [1, 32, 128, 128];

    public int Read(int a)
    {
        int value;
        a &= 0xffff;
        switch (a)
        {
            case 0x2134: return (byte)(_multiplyRes & 0xff);
            case 0x2135: return (byte)((_multiplyRes >> 8) & 0xff);
            case 0x2136: return (byte)((_multiplyRes >> 16) & 0xff);
            case 0x2137:
                if ((_wrIo & 0x80) != 0)
                {
                    _ophct = HPos >> 2;
                    _opvct = VPos;
                    _counterLatch = true;
                }
                return (byte)(Cpu.OpenBus & 0xff);
            case 0x2138:
            {
                value = 0;
                if (_oamAddr < 0x200 && (_oamAddr & 1) == 1)
                {
                    value = _oam[_oamAddr];
                }
                else if (_oamAddr > 0x1ff)
                    value = _oam[_oamAddr % _oam.Length];
                _oamAddr++;
                return (byte)(value & 0xff);
            }
            case 0x2139:
            {
                value = (byte)(_vramLatch & 0xff);
                if (!_vramAddrMode)
                {
                    _vramLatch = _vram[GetVramRemap()];
                    _vramAddr += ppuaddrinc[_vramAddrIncrease];
                }
                return (byte)(value & 0xff);
            }
            case 0x213a:
            {
                value = (byte)((_vramLatch >> 8) & 0xff);
                if (_vramAddrMode)
                {
                    _vramLatch = _vram[GetVramRemap()];
                    _vramAddr += ppuaddrinc[_vramAddrIncrease];
                }
                return (byte)(value & 0xff);
            }
            case 0x213c:
            {
                if (!_ophctLatch)
                    value = (byte)(_ophct & 0xff);
                else
                    value = (byte)(_ophct >> 8);
                _ophctLatch = !_ophctLatch;
                return (byte)(value & 0xff);
            }
            case 0x213d:
            {
                if (!_opvctLatch)
                    value = (byte)(_opvct & 0xff);
                else
                    value = (byte)(_opvct >> 8);
                _opvctLatch = !_opvctLatch;
                return (byte)(value & 0xff);
            }
            case 0x213f:
                _counterLatch = false;
                _ophctLatch = false;
                _opvctLatch = false;
                return (byte)(_stat78 & 0xff);
            case >= 0x2140 and <= 0x217f: return (byte)Apu.ReadFromSpu(a);
            case >= 0x2300 and <= 0x23ff:
                value = Snes.Sa1?.ReadRegister(a) ?? 0;
                return value;
            case >= 0x3000 and <= 0x3fff:
                value = (byte)(Snes.Sa1?.ReadIram(a) ?? 0);
                return value;
        }
        return 0x00;
    }

    public void Write(int addr, int val)
    {
        addr &= 0xffff;
        byte value = (byte)val;
        switch (addr)
        {
            case 0x2100:
                _forcedBlank = (value & 0x80) != 0;
                _brightness = value & 0x0f;
                break;
            case 0x2101:
                _objTable1 = (value & 0x03) << 13;
                _objTable2 = (((value & 0x18) >> 3) + 1) << 12;
                _objSize = ((value & 0xe0) >> 5) & 7;
                break;
            case 0x2102:
                _oamAddr = value;
                _interOamAddr = value << 1;
                _objPrioIndex = value & 0xe0;
                break;
            case 0x2103:
                _oamAddr |= value << 8;
                _oamAddr = (_oamAddr & 0x1ff) << 1;
                _objPrioRotation = (value & 0x80) != 0;
                break;
            case 0x2104:
                if ((_oamAddr & 1) == 0)
                    _oamLatch = value;
                if (_oamAddr < 0x200 && (_oamAddr & 1) == 1)
                {
                    _oam[_oamAddr - 1] = _oamLatch;
                    _oam[_oamAddr] = value;
                }
                else if (_oamAddr > 0x1ff)
                    _oam[_oamAddr % _oam.Length] = value;
                _oamAddr = (_oamAddr + 1) & 0x3ff;
                break;
            case 0x2105:
                _bgMode = value & 7;
                _mode1Bg3Priority = (value & 0x08) != 0;
                _bgCharSize = [(value & 0x10) != 0, (value & 0x20) != 0, (value & 0x40) != 0, (value & 0x80) != 0];
                break;
            case 0x2106:
                _mosaicEnabled = [(value & 0x01) != 0, (value & 0x02) != 0, (value & 0x04) != 0, (value & 0x08) != 0];
                _mosaicSize = (value & 0xc0) >> 4;
                break;
            case 0x2107 or 0x2108 or 0x2109 or 0x210a:
                var b = value & 3;
                _bgMapbase[(addr & 0xff) - 7] = (value >> 2 << 10) & 0x7fff;
                var i = (addr & 0xff) - 7;
                switch (b)
                {
                    case 0:
                        _bgSizeX[i] = 255; _bgSizeY[i] = 255;
                        break;
                    case 1:
                        _bgSizeX[i] = 511; _bgSizeY[i] = 255;
                        break;
                    case 2:
                        _bgSizeX[i] = 255; _bgSizeY[i] = 511;
                        break;
                    case 3:
                        _bgSizeX[i] = 511; _bgSizeY[i] = 511;
                        break;
                }
                break;
            case 0x210b:
                _bgTilebase[addr - 0xb & 0xff] = (value & 0xf) << 12;
                _bgTilebase[addr - 0xb + 1 & 0xff] = (value >> 4) << 12;
                break;
            case 0x210c:
                _bgTilebase[addr - 0xb + 1 & 0xff] = (value & 0xf) << 12;
                _bgTilebase[addr - 0xb + 2 & 0xff] = (value >> 4) << 12;
                break;
            case 0x210d:
                _scrollXMode7 = ((value << 8) | _mode7Latch) & 0x1fff;
                _mode7Latch = value;
                goto case 0x210f;
            case 0x210f:
            case 0x2111:
            case 0x2113:
                _bgScrollX[((addr & 0xff) - 0xd) / 2] = ((value << 8) | (_prevScrollX & ~7) | (_currScrollX & 7)) & 0x3ff;
                _prevScrollX = value;
                _currScrollX = value;
                break;
            case 0x210e:
                _scrollYMode7 = ((value << 8) | _mode7Latch) & 0xffff;
                _mode7Latch = value;
                goto case 0x2110;
            case 0x2110:
            case 0x2112:
            case 0x2114:
                _bgScrollY[((addr & 0xff) - 0xe) / 2] = ((value << 8) | (_prevScrollX & 0xff)) & 0x3ff;
                _prevScrollX = value;
                break;
            case 0x2115:
                _vramAddrIncrease = value & 3;
                _vramAddrRemap = (value >> 2) & 3;
                _vramAddrMode = (value & 0x80) != 0;
                break;
            case 0x2116:
                _vramAddr = _vramAddr & 0xff00 | value;
                _vramLatch = _vram[GetVramRemap()];
                break;
            case 0x2117:
                _vramAddr = _vramAddr & 0xff | (value << 8);
                _vramLatch = _vram[GetVramRemap()];
                break;
            case 0x2118:
            {
                var va = GetVramRemap();
                _vram[va] = (ushort)(_vram[va] & 0xff00 | value);
                if (!_vramAddrMode)
                    _vramAddr += ppuaddrinc[_vramAddrIncrease];
                if (_vramAddr == 0x7400)
                { }
                if (Snes.Debug)
                    Snes.Mmu.WriteRamType((_vramAddr * 2) & 0xffff, value, RamType.Vram);
                break;
            }
            case 0x2119:
            {
                var va = GetVramRemap();
                _vram[va] = (ushort)(_vram[va] & 0xff | value << 8);
                if (_vramAddrMode)
                    _vramAddr += ppuaddrinc[_vramAddrIncrease];
                if (Snes.Debug)
                    Snes.Mmu.WriteRamType((_vramAddr * 2) & 0xffff, value, RamType.Vram);
                break;
            }

            case 0x211a:
                _flipXMode7 = (value & 0x01) != 0;
                _flipYMode7 = (value & 0x02) != 0;
                _fill0Mode7 = (value & 0x40) != 0;
                _largeMapMode7 = (value & 0x80) != 0;
                break;
            case 0x211b:
                _m7A = (value << 8) | _mode7Latch;
                _mode7Latch = value;
                break;
            case 0x211c:
                _m7B = (value << 8) | _mode7Latch;
                _mode7Latch = value;
                _multiplyRes = (short)_m7A * (sbyte)(_m7B >> 8);
                break;
            case 0x211d:
                _m7C = (value << 8) | _mode7Latch;
                _mode7Latch = value;
                break;
            case 0x211e:
                _m7D = (value << 8) | _mode7Latch;
                _mode7Latch = value;
                break;
            case 0x211f:
                _m7X = (value << 8) | _mode7Latch;
                _mode7Latch = value;
                break;
            case 0x2120:
                _m7Y = (value << 8) | _mode7Latch;
                _mode7Latch = value;
                break;
            case 0x2121: _cgAdd = value; _cgRamToggle = false; break;
            case 0x2122:
                if (!_cgRamToggle)
                    _cgBuffer = value & 0xff;
                else
                {
                    _cram[_cgAdd & 0xff] = (ushort)((value & 0x7f) << 8 | _cgBuffer);
                    _cgAdd = (_cgAdd + 1) & 0xff;
                }
                _cgRamToggle = !_cgRamToggle;
                break;
            case 0x2123:
                _win1Inverted[0] = (value & 0x01) != 0; _win1Enabled[0] = (value & 0x02) != 0;
                _win2Inverted[0] = (value & 0x04) != 0; _win2Enabled[0] = (value & 0x08) != 0;
                _win1Inverted[1] = (value & 0x10) != 0; _win1Enabled[1] = (value & 0x20) != 0;
                _win2Inverted[1] = (value & 0x40) != 0; _win2Enabled[1] = (value & 0x80) != 0;
                break;
            case 0x2124:
                _win1Inverted[2] = (value & 0x01) != 0; _win1Enabled[2] = (value & 0x02) != 0;
                _win2Inverted[2] = (value & 0x04) != 0; _win2Enabled[2] = (value & 0x08) != 0;
                _win1Inverted[3] = (value & 0x10) != 0; _win1Enabled[3] = (value & 0x20) != 0;
                _win2Inverted[3] = (value & 0x40) != 0; _win2Enabled[3] = (value & 0x80) != 0;
                break;
            case 0x2125:
                _win1Inverted[4] = (value & 0x01) != 0; _win1Enabled[4] = (value & 0x02) != 0;
                _win2Inverted[4] = (value & 0x04) != 0; _win2Enabled[4] = (value & 0x08) != 0;
                _win1Inverted[5] = (value & 0x10) != 0; _win1Enabled[5] = (value & 0x20) != 0;
                _win2Inverted[5] = (value & 0x40) != 0; _win2Enabled[5] = (value & 0x80) != 0;
                break;
            case 0x2126: _w1Left = value; break;
            case 0x2127: _w1Right = value; break;
            case 0x2128: _w2Left = value; break;
            case 0x2129: _w2Right = value; break;
            case 0x212a:
                _winLogic[0] = value & 3; _winLogic[1] = (value >> 2) & 3;
                _winLogic[2] = (value >> 4) & 3; _winLogic[3] = (value >> 6) & 3;
                break;
            case 0x212b:
                _winLogic[4] = value & 3; _winLogic[5] = (value >> 2) & 3;
                break;
            case 0x212c: _mainBgs = [(value & 0x01) != 0, (value & 0x02) != 0, (value & 0x04) != 0, (value & 0x08) != 0, (value & 0x10) != 0]; break;
            case 0x212d: _subBgs = [(value & 0x01) != 0, (value & 0x02) != 0, (value & 0x04) != 0, (value & 0x08) != 0, (value & 0x10) != 0]; break;
            case 0x212e: _winMainBgs = [(value & 0x01) != 0, (value & 0x02) != 0, (value & 0x04) != 0, (value & 0x08) != 0, (value & 0x10) != 0]; break; ;
            case 0x212f: _winSubBgs = [(value & 0x01) != 0, (value & 0x02) != 0, (value & 0x04) != 0, (value & 0x08) != 0, (value & 0x10) != 0]; break;
            case 0x2130:
                _dirColor = (value & 0x01) != 0;
                _addSub = (value & 0x02) != 0;
                _prevent = (value >> 4) & 3;
                _clip = (value >> 6) & 3;
                break;
            case 0x2131:
                _colorMath = [(value&0x01) != 0, (value&0x02) != 0, (value&0x04) != 0, (value&0x08) != 0,
                (value&0x10) != 0, (value&0x20)!= 0, (value&0x40) != 0, (value&0x80) != 0];
                break;
            case 0x2132:
                var c = value & 0x1f;
                if ((value & 0x20) != 0)
                    Fixed.Color = (Fixed.Color & 0x7fe0 | c) & 0xffff;
                if ((value & 0x40) != 0)
                    Fixed.Color = (Fixed.Color & 0x7c1f | c << 5) & 0xffff;
                if ((value & 0x80) != 0)
                    Fixed.Color = (Fixed.Color & 0x3ff | c << 10) & 0xffff;
                break;
            case 0x2133:
                _overscanMode = (value & 0x04) != 0;
                _hiResMode = (value & 0x08) != 0;
                _extBgMode = (value & 0x10) != 0;
                break;
            case >= 0x2140 and <= 0x217f: Apu.WriteToSpu(addr & 0xff, value); break;
            case 0x2180:
                Snes.Mmu.WriteDma(value);
                break;
            case <= 0x2183:
                Snes.Mmu.UpdateWramAddress(addr, value);
                break;
            case >= 0x2200 and <= 0x22ff: Snes.Sa1?.WriteSnesRegister(addr, value); break;
            case >= 0x3000 and <= 0x3fff:
                Snes.Sa1?.WriteIram(addr, value);
                Snes.Gsu?.WriteIO(addr, value);
                break;
        }
    }
}
