using System;
using System.Collections.Generic;
using System.Text;

namespace Gmulator.Core.Snes
{
    public partial class SnesPpu
    {
        private readonly int[] ppuaddrinc = [1, 32, 128, 128];

        public int Read(int a)
        {
            a &= 0xffff;
            switch (a)
            {
                case 0x2134: return MultiplyRes & 0xff;
                case 0x2135: return (MultiplyRes >> 8) & 0xff;
                case 0x2136: return (MultiplyRes >> 16) & 0xff;
                case 0x2137:
                    if ((WRIO & 0x80) != 0)
                    {
                        OPHCT = HPos >> 2;
                        OPVCT = VPos;
                        CounterLatch = true;
                    }
                    return Cpu.OpenBus & 0xff;
                case 0x2138:
                {
                    var v = 0;
                    if (OamAddr < 0x200 && (OamAddr & 1) == 1)
                    {
                        v = Oam[OamAddr];
                    }
                    else if (OamAddr > 0x1ff)
                        v = Oam[OamAddr % Oam.Length];
                    OamAddr++;
                    return v & 0xff;
                }
                case 0x2139:
                {
                    var v = VramLatch & 0xff;
                    if (!VramAddrMode)
                    {
                        VramLatch = Vram[GetVramRemap()];
                        VramAddr += ppuaddrinc[VramAddrInc];
                    }
                    return v & 0xff;
                }
                case 0x213a:
                {
                    var v = (VramLatch >> 8) & 0xff;
                    if (VramAddrMode)
                    {
                        VramLatch = Vram[GetVramRemap()];
                        VramAddr += ppuaddrinc[VramAddrInc];
                    }
                    return v & 0xff;
                }
                case 0x213c:
                {
                    int v;
                    if (!OphctLatch)
                        v = OPHCT & 0xff;
                    else
                        v = OPHCT >> 8;
                    OphctLatch = !OphctLatch;
                    return v & 0xff;
                }
                case 0x213d:
                {
                    int v;
                    if (!OpvctLatch)
                        v = OPVCT & 0xff;
                    else
                        v = OPVCT >> 8;
                    OpvctLatch = !OpvctLatch;
                    return v & 0xff;
                }
                case 0x213f:
                    CounterLatch = false;
                    OphctLatch = false;
                    OpvctLatch = false;
                    return STAT78 & 0xff;
                case >= 0x2140 and <= 0x217f: return Apu.ReadFromSpu(a);
                case >= 0x2300 and <= 0x23ff: return Snes.Sa1.ReadReg(a);
                case >= 0x3000 and <= 0x3fff: return Snes.Sa1.ReadRam(a);
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
                    ForcedBlank = (v & 0x80) != 0;
                    Brightness = v & 0x0f;
                    break;
                case 0x2101:
                    ObjTable1 = (v & 0x03) << 13;
                    ObjTable2 = ((v & 0x18) >> 3) + 1 << 12;
                    ObjSize = ((v & 0xe0) >> 5) & 7;
                    break;
                case 0x2102:
                    OamAddr = v;
                    InterOamAddr = v << 1;
                    ObjPrioIndex = v & 0xe0;
                    break;
                case 0x2103:
                    OamAddr |= v << 8;
                    OamAddr = (OamAddr & 0x1ff) << 1;
                    ObjPrioRotation = (v & 0x80) != 0;
                    break;
                case 0x2104:
                    if ((OamAddr & 1) == 0)
                        OamLatch = (byte)v;
                    if (OamAddr < 0x200 && (OamAddr & 1) == 1)
                    {
                        Oam[OamAddr - 1] = OamLatch;
                        Oam[OamAddr++] = (byte)v;
                        break;
                    }
                    else if (OamAddr > 0x1ff)
                        Oam[OamAddr % Oam.Length] = (byte)v;
                    OamAddr++;
                    break;
                case 0x2105:
                    BgMode = v & 7;
                    Mode1Bg3Prio = (v & 0x08) != 0;
                    BgCharSize = [(v & 0x10) != 0, (v & 0x20) != 0, (v & 0x40) != 0, (v & 0x80) != 0];
                    break;
                case 0x2106:
                    MosaicEnabled = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0];
                    MosaicSize = (v & 0xc0) >> 4;
                    break;
                case 0x2107 or 0x2108 or 0x2109 or 0x210a:
                    var b = v & 3;
                    BgMapbase[(a & 0xff) - 7] = (v >> 2 << 10) & 0x7fff;
                    var i = (a & 0xff) - 7;
                    switch (b)
                    {
                        case 0:
                            BgSizeX[i] = 255; BgSizeY[i] = 255;
                            break;
                        case 1:
                            BgSizeX[i] = 511; BgSizeY[i] = 255;
                            break;
                        case 2:
                            BgSizeX[i] = 255; BgSizeY[i] = 511;
                            break;
                        case 3:
                            BgSizeX[i] = 511; BgSizeY[i] = 511;
                            break;
                    }
                    break;
                case 0x210b:
                    BgTilebase[a - 0xb & 0xff] = (v & 0xf) << 12;
                    BgTilebase[a - 0xb + 1 & 0xff] = (v >> 4) << 12;
                    break;
                case 0x210c:
                    BgTilebase[a - 0xb + 1 & 0xff] = (v & 0xf) << 12;
                    BgTilebase[a - 0xb + 2 & 0xff] = (v >> 4) << 12;
                    break;
                case 0x210d:
                    ScrollXMode7 = ((v << 8) | Mode7Latch) & 0xffff;
                    Mode7Latch = (byte)v;
                    goto case 0x210f;
                case 0x210f:
                case 0x2111:
                case 0x2113:
                    BgScrollX[((a & 0xff) - 0xd) / 2] = (((v << 8) | (PrevScrollX & ~7) | (CurrScrollX & 7)) & 0xffff);
                    PrevScrollX = v;
                    CurrScrollX = v;
                    break;
                case 0x210e:
                    ScrollYMode7 = ((v << 8) | Mode7Latch) & 0xffff;
                    Mode7Latch = (byte)v;
                    goto case 0x2110;
                case 0x2110:
                case 0x2112:
                case 0x2114:
                    BgScrollY[((a & 0xff) - 0xe) / 2] = (((v << 8) | (PrevScrollX & 0xff)) & 0xffff);
                    PrevScrollX = v;
                    break;
                case 0x2115:
                    VramAddrInc = v & 3;
                    VramAddrRemap = (v >> 2) & 3;
                    VramAddrMode = (v & 0x80) != 0;
                    break;
                case 0x2116:
                    VramAddr = VramAddr & 0xff00 | v;
                    VramLatch = Vram[GetVramRemap()];
                    break;
                case 0x2117:
                    VramAddr = VramAddr & 0xff | (v << 8);
                    VramLatch = Vram[GetVramRemap()];
                    break;
                case 0x2118:
                {
                    var va = GetVramRemap();
                    Vram[va] = (ushort)(Vram[va] & 0xff00 | v);
                    if (!VramAddrMode)
                        VramAddr += ppuaddrinc[VramAddrInc];
                    Snes.Mmu.WriteRamType((VramAddr * 2) & 0xffff, v, RamType.Vram);
                    break;
                }
                case 0x2119:
                {
                    var va = GetVramRemap();
                    Vram[va] = (ushort)(Vram[va] & 0xff | v << 8);
                    if (VramAddrMode)
                        VramAddr += ppuaddrinc[VramAddrInc];
                    Snes.Mmu.WriteRamType((VramAddr * 2) & 0xffff, v, RamType.Vram);
                    break;
                }

                case 0x211a:
                    Mode7Settings = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x40) != 0, (v & 0x80) != 0];
                    break;
                case 0x211b:
                    M7A = (v << 8) | Mode7Latch;
                    Mode7Latch = (byte)v;
                    break;
                case 0x211c:
                    M7B = (v << 8) | Mode7Latch;
                    Mode7Latch = (byte)v;
                    MultiplyRes = (short)M7A * (sbyte)(M7B >> 8);
                    break;
                case 0x211d:
                    M7C = (v << 8) | Mode7Latch;
                    Mode7Latch = (byte)v;
                    break;
                case 0x211e:
                    M7D = (v << 8) | Mode7Latch;
                    Mode7Latch = (byte)v;
                    break;
                case 0x211f:
                    M7X = (v << 8) | Mode7Latch;
                    Mode7Latch = (byte)v;
                    break;
                case 0x2120:
                    M7Y = (v << 8) | Mode7Latch;
                    Mode7Latch = (byte)v;
                    break;
                case 0x2121: CGADD = v; CgRamToggle = false; break;
                case 0x2122:
                    if (!CgRamToggle)
                        _cgBuffer = v & 0xff;
                    else
                    {
                        Cram[CGADD & 0xff] = (ushort)((v & 0x7f) << 8 | _cgBuffer);
                        CGADD = (CGADD + 1) & 0xff;
                    }
                    CgRamToggle = !CgRamToggle;
                    break;
                case 0x2123:
                    Win1Inverted[0] = (v & 0x01) != 0; Win1Enabled[0] = (v & 0x02) != 0;
                    Win2Inverted[0] = (v & 0x04) != 0; Win2Enabled[0] = (v & 0x08) != 0;
                    Win1Inverted[1] = (v & 0x10) != 0; Win1Enabled[1] = (v & 0x20) != 0;
                    Win2Inverted[1] = (v & 0x40) != 0; Win2Enabled[1] = (v & 0x80) != 0;
                    break;
                case 0x2124:
                    Win1Inverted[2] = (v & 0x01) != 0; Win1Enabled[2] = (v & 0x02) != 0;
                    Win2Inverted[2] = (v & 0x04) != 0; Win2Enabled[2] = (v & 0x08) != 0;
                    Win1Inverted[3] = (v & 0x10) != 0; Win1Enabled[3] = (v & 0x20) != 0;
                    Win2Inverted[3] = (v & 0x40) != 0; Win2Enabled[3] = (v & 0x80) != 0;
                    break;
                case 0x2125:
                    Win1Inverted[4] = (v & 0x01) != 0; Win1Enabled[4] = (v & 0x02) != 0;
                    Win2Inverted[4] = (v & 0x04) != 0; Win2Enabled[4] = (v & 0x08) != 0;
                    Win1Inverted[5] = (v & 0x10) != 0; Win1Enabled[5] = (v & 0x20) != 0;
                    Win2Inverted[5] = (v & 0x40) != 0; Win2Enabled[5] = (v & 0x80) != 0;
                    break;
                case 0x2126: W1Left = v; break;
                case 0x2127: W1Right = v; break;
                case 0x2128: W2Left = v; break;
                case 0x2129: W2Right = v; break;
                case 0x212a:
                    WinLogic[0] = v & 3; WinLogic[1] = (v >> 2) & 3;
                    WinLogic[2] = (v >> 4) & 3; WinLogic[3] = (v >> 6) & 3;
                    break;
                case 0x212b:
                    WinLogic[4] = v & 3; WinLogic[5] = (v >> 2) & 3;
                    break;
                case 0x212c: MainBgs = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0, (v & 0x10) != 0]; break;
                case 0x212d: SubBgs = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0, (v & 0x10) != 0]; break;
                case 0x212e: WinMainBgs = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0, (v & 0x10) != 0]; break; ;
                case 0x212f: WinSubBgs = [(v & 0x01) != 0, (v & 0x02) != 0, (v & 0x04) != 0, (v & 0x08) != 0, (v & 0x10) != 0]; break;
                case 0x2130:
                    DirColor = (v & 0x01) != 0;
                    AddSub = (v & 0x02) != 0;
                    Prevent = (v >> 4) & 3;
                    Clip = (v >> 6) & 3;
                    break;
                case 0x2131:
                    ColorMath = [(v&0x01)!=0, (v&0x02)!=0, (v&0x04)!=0, (v&0x08)!=0,
                    (v&0x10)!=0, (v&0x20)!=0, (v&0x40)!=0, (v&0x80)!=0];
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
                    OverscanMode = (v & 0x04) != 0;
                    HiResMode = (v & 0x08) != 0;
                    ExtBgMode = (v & 0x10) != 0;
                    break;
                case >= 0x2140 and <= 0x217f: Apu.WriteToSpu(a & 0xff, v); break;
                case 0x2180:
                    Snes.Mmu.WriteDma(v);
                    break;
                case <= 0x2183:
                    Snes.Mmu.UpdateWramAddress(a, v);
                    break;
                case >= 0x2200 and <= 0x22ff: Snes.Sa1.WriteCpuReg(a, v); break;
                case >= 0x3000 and <= 0x3fff: Snes.Sa1.WriteRam(a, v); break;
            }
        }
    }
}
