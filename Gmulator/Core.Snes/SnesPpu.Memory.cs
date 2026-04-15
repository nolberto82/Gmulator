namespace Gmulator.Core.Snes;

public partial class SnesPpu
{
    private readonly int[] ppuaddrinc = [1, 32, 128, 128];

    public int Read(int a)
    {
        int v;
        a &= 0xffff;
        switch (a)
        {
            case 0x2134: return _multiplyRes & 0xff;
            case 0x2135: return (_multiplyRes >> 8) & 0xff;
            case 0x2136: return (_multiplyRes >> 16) & 0xff;
            case 0x2137:
                if ((_wrIo & 0x80) != 0)
                {
                    _ophct = HPos >> 2;
                    _opvct = VPos;
                    _counterLatch = true;
                }
                return Cpu.OpenBus & 0xff;
            case 0x2138:
            {
                v = 0;
                if (_oamAddr < 0x200 && (_oamAddr & 1) == 1)
                {
                    v = _oam[_oamAddr];
                }
                else if (_oamAddr > 0x1ff)
                    v = _oam[_oamAddr % _oam.Length];
                _oamAddr++;
                return v & 0xff;
            }
            case 0x2139:
            {
                v = _vramLatch & 0xff;
                if (!_vramAddrMode)
                {
                    _vramLatch = _vram[GetVramRemap()];
                    _vramAddr += ppuaddrinc[_vramAddrIncrease];
                }
                return v & 0xff;
            }
            case 0x213a:
            {
                v = (_vramLatch >> 8) & 0xff;
                if (_vramAddrMode)
                {
                    _vramLatch = _vram[GetVramRemap()];
                    _vramAddr += ppuaddrinc[_vramAddrIncrease];
                }
                return v & 0xff;
            }
            case 0x213c:
            {
                if (!_ophctLatch)
                    v = _ophct & 0xff;
                else
                    v = _ophct >> 8;
                _ophctLatch = !_ophctLatch;
                return v & 0xff;
            }
            case 0x213d:
            {
                if (!_opvctLatch)
                    v = _opvct & 0xff;
                else
                    v = _opvct >> 8;
                _opvctLatch = !_opvctLatch;
                return v & 0xff;
            }
            case 0x213f:
                _counterLatch = false;
                _ophctLatch = false;
                _opvctLatch = false;
                return _stat78 & 0xff;
            case >= 0x2140 and <= 0x217f: return Apu.ReadFromSpu(a);
            case >= 0x2300 and <= 0x23ff:
                v = Snes.Sa1?.ReadReg(a) ?? 0;
                return v;
            case >= 0x3000 and <= 0x3fff:
                v = Snes.Sa1?.ReadIram(a) ?? 0;
                return v;
        }
        return 0x00;
    }

    public void Write(int a, int v)
    {
        v &= 0xff;
        a &= 0xffff;
        switch (a)
        {
            case 0x2100:
                _forcedBlank = (v & 0x80) != 0;
                _brightness = v & 0x0f;
                break;
            case 0x2101:
                _objTable1 = (v & 0x03) << 13;
                _objTable2 = (((v & 0x18) >> 3) + 1) << 12;
                _objSize = ((v & 0xe0) >> 5) & 7;
                break;
            case 0x2102:
                _oamAddr = v;
                _interOamAddr = v << 1;
                _objPrioIndex = v & 0xe0;
                break;
            case 0x2103:
                _oamAddr |= v << 8;
                _oamAddr = (_oamAddr & 0x1ff) << 1;
                _objPrioRotation = (v & 0x80) != 0;
                break;
            case 0x2104:
                if ((_oamAddr & 1) == 0)
                    _oamLatch = (byte)v;
                if (_oamAddr < 0x200 && (_oamAddr & 1) == 1)
                {
                    _oam[_oamAddr - 1] = _oamLatch;
                    _oam[_oamAddr++] = (byte)v;
                    break;
                }
                else if (_oamAddr > 0x1ff)
                    _oam[_oamAddr % _oam.Length] = (byte)v;
                _oamAddr++;
                break;
            case 0x2105:
                _bgMode = v & 7;
                _mode1Bg3Priority = (v & 0x08) != 0;
                _bgCharSize = [(v & 0x10) != 0, (v & 0x20) != 0, (v & 0x40) != 0, (v & 0x80) != 0];
                break;
            case 0x2106:
                _mosaicEnabled = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0];
                _mosaicSize = (v & 0xc0) >> 4;
                break;
            case 0x2107 or 0x2108 or 0x2109 or 0x210a:
                var b = v & 3;
                _bgMapbase[(a & 0xff) - 7] = (v >> 2 << 10) & 0x7fff;
                var i = (a & 0xff) - 7;
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
                _bgTilebase[a - 0xb & 0xff] = (v & 0xf) << 12;
                _bgTilebase[a - 0xb + 1 & 0xff] = (v >> 4) << 12;
                break;
            case 0x210c:
                _bgTilebase[a - 0xb + 1 & 0xff] = (v & 0xf) << 12;
                _bgTilebase[a - 0xb + 2 & 0xff] = (v >> 4) << 12;
                break;
            case 0x210d:
                _scrollXMode7 = ((v << 8) | _mode7Latch) & 0xffff;
                _mode7Latch = (byte)v;
                goto case 0x210f;
            case 0x210f:
            case 0x2111:
            case 0x2113:
                _bgScrollX[((a & 0xff) - 0xd) / 2] = ((v << 8) | (_prevScrollX & ~7) | (_currScrollX & 7)) & 0xffff;
                _prevScrollX = v;
                _currScrollX = v;
                break;
            case 0x210e:
                _scrollYMode7 = ((v << 8) | _mode7Latch) & 0xffff;
                _mode7Latch = (byte)v;
                goto case 0x2110;
            case 0x2110:
            case 0x2112:
            case 0x2114:
                _bgScrollY[((a & 0xff) - 0xe) / 2] = ((v << 8) | (_prevScrollX & 0xff)) & 0xffff;
                _prevScrollX = v;
                break;
            case 0x2115:
                _vramAddrIncrease = v & 3;
                _vramAddrRemap = (v >> 2) & 3;
                _vramAddrMode = (v & 0x80) != 0;
                break;
            case 0x2116:
                _vramAddr = _vramAddr & 0xff00 | v;
                _vramLatch = _vram[GetVramRemap()];
                break;
            case 0x2117:
                _vramAddr = _vramAddr & 0xff | (v << 8);
                _vramLatch = _vram[GetVramRemap()];
                break;
            case 0x2118:
            {
                var va = GetVramRemap();
                _vram[va] = (ushort)(_vram[va] & 0xff00 | v);
                if (!_vramAddrMode)
                    _vramAddr += ppuaddrinc[_vramAddrIncrease];
                Snes.Mmu.WriteRamType((_vramAddr * 2) & 0xffff, v, RamType.Vram);
                break;
            }
            case 0x2119:
            {
                var va = GetVramRemap();
                _vram[va] = (ushort)(_vram[va] & 0xff | v << 8);
                if (_vramAddrMode)
                    _vramAddr += ppuaddrinc[_vramAddrIncrease];
                Snes.Mmu.WriteRamType((_vramAddr * 2) & 0xffff, v, RamType.Vram);
                break;
            }

            case 0x211a:
                _mode7Settings = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x40) != 0, (v & 0x80) != 0];
                break;
            case 0x211b:
                _m7A = (v << 8) | _mode7Latch;
                _mode7Latch = (byte)v;
                break;
            case 0x211c:
                _m7B = (v << 8) | _mode7Latch;
                _mode7Latch = (byte)v;
                _multiplyRes = (short)_m7A * (sbyte)(_m7B >> 8);
                break;
            case 0x211d:
                _m7C = (v << 8) | _mode7Latch;
                _mode7Latch = (byte)v;
                break;
            case 0x211e:
                _m7D = (v << 8) | _mode7Latch;
                _mode7Latch = (byte)v;
                break;
            case 0x211f:
                _m7X = (v << 8) | _mode7Latch;
                _mode7Latch = (byte)v;
                break;
            case 0x2120:
                _m7Y = (v << 8) | _mode7Latch;
                _mode7Latch = (byte)v;
                break;
            case 0x2121: _cgAdd = v; _cgRamToggle = false; break;
            case 0x2122:
                if (!_cgRamToggle)
                    _cgBuffer = v & 0xff;
                else
                {
                    _cram[_cgAdd & 0xff] = (ushort)((v & 0x7f) << 8 | _cgBuffer);
                    _cgAdd = (_cgAdd + 1) & 0xff;
                }
                _cgRamToggle = !_cgRamToggle;
                break;
            case 0x2123:
                _win1Inverted[0] = (v & 0x01) != 0; _win1Enabled[0] = (v & 0x02) != 0;
                _win2Inverted[0] = (v & 0x04) != 0; _win2Enabled[0] = (v & 0x08) != 0;
                _win1Inverted[1] = (v & 0x10) != 0; _win1Enabled[1] = (v & 0x20) != 0;
                _win2Inverted[1] = (v & 0x40) != 0; _win2Enabled[1] = (v & 0x80) != 0;
                break;
            case 0x2124:
                _win1Inverted[2] = (v & 0x01) != 0; _win1Enabled[2] = (v & 0x02) != 0;
                _win2Inverted[2] = (v & 0x04) != 0; _win2Enabled[2] = (v & 0x08) != 0;
                _win1Inverted[3] = (v & 0x10) != 0; _win1Enabled[3] = (v & 0x20) != 0;
                _win2Inverted[3] = (v & 0x40) != 0; _win2Enabled[3] = (v & 0x80) != 0;
                break;
            case 0x2125:
                _win1Inverted[4] = (v & 0x01) != 0; _win1Enabled[4] = (v & 0x02) != 0;
                _win2Inverted[4] = (v & 0x04) != 0; _win2Enabled[4] = (v & 0x08) != 0;
                _win1Inverted[5] = (v & 0x10) != 0; _win1Enabled[5] = (v & 0x20) != 0;
                _win2Inverted[5] = (v & 0x40) != 0; _win2Enabled[5] = (v & 0x80) != 0;
                break;
            case 0x2126: _w1Left = v; break;
            case 0x2127: _w1Right = v; break;
            case 0x2128: _w2Left = v; break;
            case 0x2129: _w2Right = v; break;
            case 0x212a:
                _winLogic[0] = v & 3; _winLogic[1] = (v >> 2) & 3;
                _winLogic[2] = (v >> 4) & 3; _winLogic[3] = (v >> 6) & 3;
                break;
            case 0x212b:
                _winLogic[4] = v & 3; _winLogic[5] = (v >> 2) & 3;
                break;
            case 0x212c: _mainBgs = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0, (v & 0x10) != 0]; break;
            case 0x212d: _subBgs = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0, (v & 0x10) != 0]; break;
            case 0x212e: _winMainBgs = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0, (v & 0x10) != 0]; break; ;
            case 0x212f: _winSubBgs = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0, (v & 0x10) != 0]; break;
            case 0x2130:
                _dirColor = (v & 0x01) != 0;
                _addSub = (v & 0x02) != 0;
                _prevent = (v >> 4) & 3;
                _clip = (v >> 6) & 3;
                break;
            case 0x2131:
                _colorMath = [(v&0x01) != 0, (v&0x02) != 0, (v&0x04) != 0, (v&0x08) != 0,
                (v&0x10) != 0, (v&0x20)!= 0, (v&0x40) != 0, (v&0x80) != 0];
                break;
            case 0x2132:
                var c = v & 0x1f;
                if ((v & 0x20) != 0)
                    Fixed.Color = (Fixed.Color & 0x7fe0 | c) & 0xffff;
                if ((v & 0x40) != 0)
                    Fixed.Color = (Fixed.Color & 0x7c1f | c << 5) & 0xffff;
                if ((v & 0x80) != 0)
                    Fixed.Color = (Fixed.Color & 0x3ff | c << 10) & 0xffff;
                break;
            case 0x2133:
                _overscanMode = (v & 0x04) != 0;
                _hiResMode = (v & 0x08) != 0;
                _extBgMode = (v & 0x10) != 0;
                break;
            case >= 0x2140 and <= 0x217f: Apu.WriteToSpu(a & 0xff, v); break;
            case 0x2180:
                Snes.Mmu.WriteDma(v);
                break;
            case <= 0x2183:
                Snes.Mmu.UpdateWramAddress(a, v);
                break;
            case >= 0x2200 and <= 0x22ff: Snes.Sa1?.WriteCpuReg(a, v); break;
            case >= 0x3000 and <= 0x3fff: Snes.Sa1?.WriteIram(a, v); break;
        }
    }
}
