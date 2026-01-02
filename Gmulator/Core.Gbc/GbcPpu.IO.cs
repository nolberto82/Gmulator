using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Gmulator.Core.Gbc
{
    public partial class GbcPpu
    {
        public int Read(int a)
        {
            return a switch
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
        }

        public void Write(int a, int v)
        {
            switch (a)
            {
                case 0xff40: _lcdc = v; break;
                case 0xff41:
                    _stat = (byte)(v & 0x78 | _stat & 7 | 0x80);
                    if (((v & 0x08) != 0 || (v & 0x10) != 0 || (v & 0x20) != 0) && (v & 0x40) == 0)
                        Gbc.Cpu.RequestIF(IntLcd);
                    break;
                case 0xff42: _scy = v; break;
                case 0xff43: _scx = v; break;
                case 0xff44: _ly = v; break;
                case 0xff45: _lyc = v; break;
                case 0xff46:
                    Mmu.WriteDMA(v);
                    _oamDma = v;
                    break;
                case 0xff47:
                    _bgp = v;
                    break;
                case 0xff48:
                    _obp0 = v;
                    break;
                case 0xff49:
                    _obp1 = v;
                    break;
                case 0xff4A:
                    _wy = v;
                    break;
                case 0xff4B:
                    _wx = v;
                    break;
                case 0xff4d: _key1 = v; break;
                case 0xff4f:
                    if (Mmu.Mapper.CGB)
                        Mmu.VramBank = v & 1;
                    break;
                case 0xff51: _hdma1 = v; break;
                case 0xff52: _hdma2 = v; break;
                case 0xff53: _hdma3 = v; break;
                case 0xff54: _hdma4 = v; break;
                case 0xff55:
                    _hdma5 = (byte)(v & 0x7f);
                    if (!DMAactive)
                    {
                        DMAHBlank = (v & 0x80) != 0;
                        if (!DMAHBlank)
                        {
                            var src = (_hdma1 << 8 | _hdma2) & 0xfff0;
                            var dst = ((_hdma3 << 8 | _hdma4) & 0x1ff0) | 0x8000;
                            Mmu.WriteBlock(src, dst, (_hdma5 + 1) * 16);
                        }
                    }
                    break;
                case 0xff68: _bgpi = v; break;
                case 0xff69:
                    _bgpd = v;
                    SetBkgPalette(_bgpi, v);
                    _bgpi += (byte)((_bgpi & 0x80) != 0 ? 1 : 0);
                    break;
                case 0xff6a: _obpi = v; break;
                case 0xff6b:
                    _obpd = v;
                    SetObjPalette(_obpi, v);
                    _obpi += (byte)((_obpi & 0x80) != 0 ? 1 : 0);
                    break;
                case 0xff70:
                {
                    if (Mmu.Mapper.CGB)
                        Mmu.WramBank = v == 0 ? 1 : v & 7;
                    break;
                }
            }
        }

        int Read55()
        {
            var v = _hdma5 == 0 ? 0xff : _hdma5;
            if (v == 0xff)
                DMAHBlank = false;
            return v;
        }

        int Read69()
        {
            _bgpd = CGBBkgPal[_bgpi & 0x3f];
            _bgpi += (byte)((_bgpi & 0x80) != 0 ? 1 : 0);
            return _bgpd;
        }

        int Read6b()
        {
            _obpd = CGBObjPal[_obpi & 0x3f];
            //if (!editor)
            _obpi += (byte)((_obpi & 0x80) != 0 ? 1 : 0);
            return _obpd;
        }
    }
}
