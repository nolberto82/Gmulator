namespace Gmulator.Core.Gbc
{
    public partial class GbcPpu
    {
        public int Read(int a) => a switch
        {
            0xff40 => _lcdc,
            0xff41 => _stat,
            0xff42 => _scy,
            0xff43 => _scx,
            0xff44 => _ly,
            0xff45 => _lyc,
            0xff46 => _oamDma,
            0xff47 => _bgp,
            0xff48 => _obp0,
            0xff49 => _obp1,
            0xff4A => _wy,
            0xff4B => _wx,
            0xff4d => _key1,
            0xff4f => Mmu.VramBank,
            0xff51 => _hdma1,
            0xff52 => _hdma2,
            0xff53 => _hdma3,
            0xff54 => _hdma4,
            0xff55 => Read55(),
            0xff68 => _bgpi,
            0xff69 => Read69(),
            0xff6a => _obpi,
            0xff6b => Read6b(),
            0xff70 => Mmu.WramBank,
            _ => 0xff,
        };

        public void Write(int addr, int value)
        {
            switch (addr)
            {
                case 0xff40: _lcdc = value; break;
                case 0xff41:
                    _stat = (byte)(value & 0x78 | _stat & 7 | 0x80);
                    if (((value & 0x08) != 0 || (value & 0x10) != 0 || (value & 0x20) != 0) && (value & 0x40) == 0)
                        Gbc.Cpu.RequestIF(IntLcd);
                    break;
                case 0xff42: _scy = value; break;
                case 0xff43: _scx = value; break;
                case 0xff44: _ly = value; break;
                case 0xff45: _lyc = value; break;
                case 0xff46:
                    Mmu.WriteDMA(value);
                    _oamDma = value;
                    break;
                case 0xff47:
                    _bgp = value;
                    break;
                case 0xff48:
                    _obp0 = value;
                    break;
                case 0xff49:
                    _obp1 = value;
                    break;
                case 0xff4A:
                    _wy = value;
                    break;
                case 0xff4B:
                    _wx = value;
                    break;
                case 0xff4d: _key1 = value; break;
                case 0xff4f:
                    if (Mmu.Mapper.CGB)
                        Mmu.VramBank = (byte)(value & 1);
                    break;
                case 0xff51: _hdma1 = value; break;
                case 0xff52: _hdma2 = value; break;
                case 0xff53: _hdma3 = value; break;
                case 0xff54: _hdma4 = value; break;
                case 0xff55:
                    _hdma5 = (byte)(value & 0x7f);
                    if (!DMAactive)
                    {
                        DMAHBlank = (value & 0x80) != 0;
                        if (!DMAHBlank)
                        {
                            var src = (_hdma1 << 8 | _hdma2) & 0xfff0;
                            var dst = ((_hdma3 << 8 | _hdma4) & 0x1ff0) | 0x8000;
                            Mmu.WriteBlock(src, dst, (_hdma5 + 1) * 16);
                        }
                    }
                    break;
                case 0xff68: _bgpi = value; break;
                case 0xff69:
                    _bgpd = value;
                    SetBkgPalette(_bgpi, value);
                    _bgpi += (byte)((_bgpi & 0x80) != 0 ? 1 : 0);
                    break;
                case 0xff6a: _obpi = value; break;
                case 0xff6b:
                    _obpd = value;
                    SetObjPalette(_obpi, value);
                    _obpi += (byte)((_obpi & 0x80) != 0 ? 1 : 0);
                    break;
                case 0xff70:
                {
                    if (Mmu.Mapper.CGB)
                        Mmu.WramBank = (byte)(value == 0 ? 1 : value & 7);
                    break;
                }
            }
        }

        private byte Read55()
        {
            byte v = (byte)(_hdma5 == 0 ? 0xff : _hdma5);
            if (v == 0xff)
                DMAHBlank = false;
            return v;
        }

        int Read69()
        {
            _bgpd = _cgbBkgPal[_bgpi & 0x3f];
            _bgpi += (byte)((_bgpi & 0x80) != 0 ? 1 : 0);
            return _bgpd;
        }

        int Read6b()
        {
            _obpd = _cgbObjPal[_obpi & 0x3f];
            _obpi += (byte)((_obpi & 0x80) != 0 ? 1 : 0);
            return _obpd;
        }
    }
}
