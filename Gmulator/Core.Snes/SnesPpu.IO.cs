using System.Runtime.CompilerServices;

namespace Gmulator.Core.Snes;

public sealed partial class SnesPpu
{
    public int ReadIO(int addr)
    {
        switch (addr & 0xffff)
        {
            case 0x4016: return Snes.Joypad.Read4016();
            case 0x4210:
            {
                var v = _rdNmi;
                v |= Cpu.OpenBus & 0x70;
                _rdNmi &= 0x7f;
                return (byte)(v & 0xff);
            }
            case 0x4211:
            {
                var v = _timeUp;
                v |= Cpu.OpenBus & 0x7f;
                _hvbJoy &= 0x3f;
                _timeUp &= 0x7f;
                Cpu.IrqEnabled = false;
                Cpu.IrqActive = false;
                return (byte)(v & 0xff);
            }
            case 0x4212:
            {
                var v = _hvbJoy & 0x01;
                v |= (HPos < 4 || HPos >= 1096) ? 0x40 : 0x00;
                v |= _hvbJoy & 0x80;
                v |= Cpu.OpenBus & 0x3e;
                return (byte)(v & 0xff);
            }
            case 0x4214: return (byte)(MulDivResult & 0xff);
            case 0x4215: return (byte)((MulDivResult >> 8) & 0xff);
            case 0x4216: return (byte)(MulDivRemainder & 0xff);
            case 0x4217: return (byte)((MulDivRemainder >> 8) & 0xff);
            case 0x4218: return (byte)(_joy1L & 0xff);
            case 0x4219: return (byte)(_joy1H & 0xff);
            case >= 0x4300 and <= 0x437f: return (byte)Dma.Read(addr);
        }
        return 0x00;
    }

    public void WriteIO(int addr, int value)
    {
        switch (addr & 0xffff)
        {
            case 0x4200:
                if ((_nmiTimEn & 0x80) == 0 && (value & 0x80) != 0 && _vblank)
                    SetNmi();
                _nmiTimEn = value;
                break;
            case 0x4201: _wrIo = value; break;
            case 0x4202: _multiplyA = value; break;
            case 0x4203: MulDivRemainder = _multiplyA * value; break;
            case 0x4204: _dividend = (_dividend & 0xff00) | value; break;
            case 0x4205: _dividend = (_dividend & 0x00ff) | value << 8; break;
            case 0x4206: Division(value); break;
            case 0x4207: _hTimeLow = value; break;
            case 0x4208: _hTimeHigh = value; break;
            case 0x4209: _vTimeLow = value; break;
            case 0x420a: _vTimeHigh = value; break;
            case 0x420b or 0x420c: Dma.WriteDma(addr, value); break;
            case 0x420d: Cpu.FastMem = (value & 1) != 0; break;
            case >= 0x4300 and <= 0x437f: Dma.Write(addr, value); break;
        }
    }

    public List<RegisterInfo> GetState() =>
    [
        new("","HClock",$"{HPos}"),
        new("","Scanline", $"{VPos}"),
        new("","Nmi/Irq/Autojoy", $""),
        new("4200|0","Auto Joy", $"{(_hvbJoy & 0x01) != 0}"),
        new("4200|4","H Irq", $"{GetHIrq}"),
        new("4200|5","V Irq", $"{GetVIrq}"),
        new("4200|7","Nmi", $"{(_nmiTimEn & 0x80) != 0}"),
        new("","Irq Timers", $""),
        new("4207/8","HTIME", $"{GetHTime:X4}"),
        new("4209/A","VTIME", $"{GetVTime:X4}"),
        new("4212","HVBJOY", $"{_hvbJoy:X2}"),
        new("2105","BGMode", $"{_bgMode:X2}"),
        new("2100|0-3","Brightness", $"{_brightness:X2}"),
        new("2100|7","Forced Blank", $"{_forcedBlank}"),
        new("2132","Fixed Color", $"{Fixed.Color:X4}"),
        new("4216/7","Remainder", $"{MulDivRemainder:X4}"),
        new("2101|0-2","Oam Table",$"{_objTable1:X4}"),
        new("2101|3-5","Oam Table 2",$"{_objTable2:X4}"),
        new("","ObjAddr", $"{_oamAddr:X4}"),
        new("2102/3","ObjPrioIndex", $"{_objPrioIndex:X4}"),
        new("2103.7","ObjPrioRotation", $"{_objPrioRotation}"),
        new("2115-7","Vram Addr", $"{(_vramAddr * 2)&0xffff:X4}"),
        new("2126","W1 Left", $"{_w1Left:X2}"),
        new("2127","W1 Right", $"{_w1Right:X2}"),
        new("2128","W2 Left", $"{_w2Left:X2}"),
        new("2129","W2 Right", $"{_w2Right:X2}"),
        new("211B-20","Mode 7",""),
        new("211B","M7A", $"{_m7A:X4}"),
        new("211C","M7B", $"{_m7B:X4}"),
        new("211D","M7C", $"{_m7C:X4}"),
        new("211E","M7D", $"{_m7D:X4}"),
        new("211F","M7X", $"{_m7X:X4}"),
        new("2120","M7Y", $"{_m7Y:X4}"),
        new("2107-0A","Tilemaps",""),
        new("07|0-1","BG1 Size", $"{_bgCharSize[0]:X2}"),
        new("07|2-6","BG1 Addr", $"{_bgMapbase[0]:X4}"),
        new("08|0-1","BG2 Size", $"{_bgCharSize[1]:X2}"),
        new("08|2-6","BG2 Addr", $"{_bgMapbase[1]:X4}"),
        new("09|0-1","BG3 Size", $"{_bgCharSize[2]:X2}"),
        new("09|2-6","BG3 Addr", $"{_bgMapbase[2]:X4}"),
        new("0A|0-1","BG4 Size", $"{_bgCharSize[3]:X2}"),
        new("0A|2-6","BG4 Addr", $"{_bgMapbase[3]:X4}"),
        new("210B-0C","Tiles",""),
        new("0B|0-2","BG1 Tile Addr", $"{_bgTilebase[0]:X4}"),
        new("0B|4-6","BG2 Tile Addr", $"{_bgTilebase[1]:X4}"),
        new("0C|0-2","BG3 Tile Addr", $"{_bgTilebase[2]:X4}"),
        new("0C|4-6","BG3 Tile Addr", $"{_bgTilebase[3]:X4}"),
        new("210D-2114","Scroll", ""),
        new("0D","BG1 X", $"{_bgScrollX[0]:X4}"),
        new("0E","BG1 Y", $"{_bgScrollY[0]:X4}"),
        new("0F","BG2 X", $"{_bgScrollX[1]:X4}"),
        new("10","BG2 Y", $"{_bgScrollY[1]:X4}"),
        new("11","BG3 X", $"{_bgScrollX[2]:X4}"),
        new("12","BG3 Y", $"{_bgScrollY[2]:X4}"),
        new("13","BG4 X", $"{_bgScrollX[3]:X4}"),
        new("14","BG4 Y", $"{_bgScrollY[3]:X4}"),
        new("212C","Main Layers", ""),
        new("|0","BG1 Enabled", $"{_mainBgs[0]}"),
        new("|1","BG2 Enabled", $"{_mainBgs[1]}"),
        new("|2","BG3 Enabled", $"{_mainBgs[2]}"),
        new("|3","BG4 Enabled", $"{_mainBgs[3]}"),
        new("|4","OAM Enabled", $"{_mainBgs[4]}"),
        new("212D","Sub Layers", ""),
        new("|0","BG1 Enabled", $"{_subBgs[0]}"),
        new("|1","BG2 Enabled", $"{_subBgs[1]}"),
        new("|2","BG3 Enabled", $"{_subBgs[2]}"),
        new("|3","BG4 Enabled", $"{_subBgs[3]}"),
        new("|4","OAM Enabled", $"{_subBgs[4]}"),
    ];
}
