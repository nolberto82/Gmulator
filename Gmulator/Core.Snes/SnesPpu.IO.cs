namespace Gmulator.Core.Snes;

public partial class SnesPpu
{
    private int _nmiTimEn; ///4200
    private int _wrIo; //4201
    private int _hTimeLow; //4207
    private int _hTimeHigh; //4208
    private int _vTimeLow; //4209
    private int _vTimeHigh; //420A
    private int _mdmaEn; //420B
    private int _hdmaEn; //420C
    private int _rdNmi; //4210
    private int _timeUp; //4211
    private int _hvbJoy; //4212
    private int _rdIo; //4213
    private int _joy1L; //4218
    private int _joy1H; //4219
    private int _joy2L; //421A
    private int _joy2H; //421B
    private int _joy3L; //421C
    private int _joy3H; //421D
    private int _joy4L; //421E
    private int _joy4H; //421F
    private bool _counterLatch;
    private bool _ophctLatch;
    private bool _opvctLatch;
    private int _multiplyRes;

    public int ReadIO(int a)
    {
        switch (a & 0xffff)
        {
            case 0x4016: return Snes.Joypad.Read4016();
            case 0x4210:
            {
                var v = _rdNmi;
                v |= Cpu.OpenBus & 0x70;
                _rdNmi &= 0x7f;
                return v & 0xff;
            }
            case 0x4211:
            {
                var v = _timeUp;
                v |= Cpu.OpenBus & 0x7f;
                _hvbJoy &= 0x3f;
                _timeUp &= 0x7f;
                return v & 0xff;
            }
            case 0x4212:
            {
                var v = _hvbJoy & 0x01;
                v |= (HPos < 4 || HPos >= 1096) ? 0x40 : 0x00;
                v |= _hvbJoy & 0x80;
                v |= Cpu.OpenBus & 0x3e;
                return v & 0xff;
            }
            case 0x4214: return MulDivResult & 0xff;
            case 0x4215: return (MulDivResult >> 8) & 0xff;
            case 0x4216: return MulDivRemainder & 0xff;
            case 0x4217: return (MulDivRemainder >> 8) & 0xff;
            case 0x4218: return _joy1L & 0xff;
            case 0x4219: return _joy1H & 0xff;
            case >= 0x4300 and <= 0x437f: return Dma.Read(a);
        }
        return 0x00;
    }

    public void WriteIO(int a, int v)
    {
        v &= 0xff;
        switch (a & 0xffff)
        {
            case 0x4200:
                _nmiTimEn = v;
                if ((_rdNmi & 0x80) != 0)
                    SetNmi();
                break;
            case 0x4201: _wrIo = v; break;
            case 0x4202: _multiplyA = v; break;
            case 0x4203: MulDivRemainder = _multiplyA * v; break;
            case 0x4204: _dividend = (_dividend & 0xff00) | v; break;
            case 0x4205: _dividend = (_dividend & 0x00ff) | v << 8; break;
            case 0x4206: Division(v); break;
            case 0x4207: _hTimeLow = v; break;
            case 0x4208: _hTimeHigh = v; break;
            case 0x4209: _vTimeLow = v; break;
            case 0x420a: _vTimeHigh = v; break;
            case 0x420b or 0x420c: Dma.WriteDma(a, v); break;
            case 0x420d: Cpu.FastMem = (v & 1) != 0; break;
            case >= 0x4300 and <= 0x437f: Dma.Write(a, v); break;
        }
    }
}
