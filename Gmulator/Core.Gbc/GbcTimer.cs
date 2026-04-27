
using Gmulator.Interfaces;

namespace Gmulator.Core.Gbc;

public class GbcTimer : ISaveState
{
    private int _divideRegister;
    private int _timerCounter;
    private bool _overflow;
    private bool _updateTIMA;

    private byte _div;
    private byte _tima;
    private byte _tma;
    private byte _tac;

    public GbcTimer(Gbc gbc)
    {
        gbc.CpuMap.Set(0x00, 0x00, 0xff04, 0xff07, Read, Write, RamType.Register, 1);
    }

    public byte Read(int a) => a switch
    {
        0xff04 => (byte)(_div | 0xad),
        0xff05 => _tima,
        0xff06 => _tma,
        0xff07 => (byte)(_tac | 0xf8),
        _ => 0,
    };

    public void Write(int a, byte v)
    {
        switch (a)
        {
            case 0xff04: _div = v; break;
            case 0xff05: _tima = v; break;
            case 0xff06: _tma = v; break;
            case 0xff07: _tac = v; break;
        }
    }

    public void Step(GbcCpu cpu, int cycles)
    {
        _divideRegister += cycles;
        if (_divideRegister >= 256)
        {
            _divideRegister -= 256;
            _div++;
        }

        if ((_tac & 0x04) > 0)
        {
            int div = 0;

            _ = (_tac & 3) switch
            {
                0 => div = 1024,
                1 => div = 16,
                2 => div = 64,
                3 => div = 256,
                _ => 0,
            };

            if (_overflow)
            {
                _tima = _tma;
                cpu.RequestIF(IntTimer);
                _overflow = false;
            }

            if (_updateTIMA)
            {
                _tima++;
                _updateTIMA = false;
            }

            _timerCounter += cycles;
            while (_timerCounter >= div)
            {

                _timerCounter -= div;
                _updateTIMA = true;
                if (_tima == 0xff)
                    _overflow = true;
            }
        }
    }

    public void Reset()
    {
        _timerCounter = 0;
        _divideRegister = 0;
        _div = _tima = _tma = _tac = 0;
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(_divideRegister); bw.Write(_timerCounter); bw.Write(_overflow); bw.Write(_updateTIMA);
        bw.Write(_div); bw.Write(_tima); bw.Write(_tma); bw.Write(_tac);
    }

    public void Load(BinaryReader br)
    {
        _divideRegister = br.ReadInt32(); _timerCounter = br.ReadInt32(); _overflow = br.ReadBoolean(); _updateTIMA = br.ReadBoolean();
        _div = br.ReadByte(); _tima = br.ReadByte(); _tma = br.ReadByte(); _tac = br.ReadByte();
    }
}
