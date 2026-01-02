using System;
using System.Collections.Generic;
using System.Text;

namespace Gmulator.Core.Snes;

public partial class SnesPpu
{
    private int NMITIMEN; ///4200
    private int WRIO; //4201
    private int HTIMEL; //4207
    private int HTIMEH; //4208
    private int VTIMEL; //4209
    private int VTIMEH; //420A
    private int MDMAEN; //420B
    private int HDMAEN; //420C
    private int RDNMI; //4210
    private int TIMEUP; //4211
    private int HVBJOY; //4212
    private int RDIO; //4213
    private int JOY1L; //4218
    private int JOY1H; //4219
    private int JOY2L; //421A
    private int JOY2H; //421B
    private int JOY3L; //421C
    private int JOY3H; //421D
    private int JOY4L; //421E
    private int JOY4H; //421F
    private bool CounterLatch;
    private bool OphctLatch;
    private bool OpvctLatch;
    private int MultiplyRes;

    public int ReadIO(int a)
    {
        switch (a & 0xffff)
        {
            case 0x4016: return Snes.Joypad.Read4016();
            case 0x4210:
            {
                var v = RDNMI;
                v |= Cpu.OpenBus & 0x70;
                RDNMI &= 0x7f;
                return v & 0xff;
            }
            case 0x4211:
            {
                var v = TIMEUP;
                v |= Cpu.OpenBus & 0x7f;
                HVBJOY &= 0x3f;
                TIMEUP &= 0x7f;
                return v & 0xff;
            }
            case 0x4212:
            {
                var v = HVBJOY & 0x01;
                v |= (HPos < 4 || HPos >= 1096) ? 0x40 : 0x00;
                v |= HVBJOY & 0x80;
                v |= Cpu.OpenBus & 0x3e;
                return v & 0xff;
            }
            case 0x4214: return MulDivResult & 0xff;
            case 0x4215: return (MulDivResult >> 8) & 0xff;
            case 0x4216: return MulDivRemainder & 0xff;
            case 0x4217: return (MulDivRemainder >> 8) & 0xff;
            case 0x4218: return JOY1L & 0xff;
            case 0x4219: return JOY1H & 0xff;
            case >= 0x4300 and <= 0x437f: return Dma.Read(a);
        }
        return 0x00;
    }

    public void WriteIO(int a, int v)
    {
        v &= 0xff;
        switch (a & 0xffff)
        {
            case 0x4200: NMITIMEN = v; break;
            case 0x4201: WRIO = v; break;
            case 0x4202: MultiplyA = v; break;
            case 0x4203: MulDivRemainder = MultiplyA * v; break;
            case 0x4204: Dividend = (Dividend & 0xff00) | v; break;
            case 0x4205: Dividend = (Dividend & 0x00ff) | v << 8; break;
            case 0x4206: Division(v); break;
            case 0x4207: HTIMEL = v; break;
            case 0x4208: HTIMEH = v; break;
            case 0x4209: VTIMEL = v; break;
            case 0x420a: VTIMEH = v; break;
            case 0x420b or 0x420c: Dma.WriteDma(a, v); break;
            case 0x420d: Cpu.FastMem = (v & 1) > 0; break;
            case >= 0x4300 and <= 0x437f: Dma.Write(a, v); break;
        }
    }
}
