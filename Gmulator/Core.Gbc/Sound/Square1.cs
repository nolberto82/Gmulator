using Gmulator.Interfaces;
using System.ComponentModel;
using static Gmulator.Interfaces.IMmu;

namespace Gmulator.Core.Gbc.Sound;

public class Square1 : BaseChannel, ISaveState
{
    private int _sweepPeriod;
    private int _sweepNegate;
    private int _sweepShift;
    private int _sweepTimer;
    private bool _sweepEnabled;
    private int _shadowFrequency;

    private int _nr10;
    private int _nr11;
    private int _nr12;
    private int _nr13;
    private int _nr14;

    public Square1(Gbc gbc)
    {
        gbc.SetMemory(0x00, 0x01, 0xff10, 0xff14, 0xffff, Read, Write, RamType.Register, 1);
    }

    public int Read(int a)
    {
        return a switch
        {
            0xff10 => _nr10 | 0x80,
            0xff11 => _nr11 | 0x3f,
            0xff12 => _nr12,
            0xff13 => _nr13 | 0xff,
            0xff14 => _nr14 | 0xbf,
            _ => 0xff,
        };
    }

    public void Write(int a, int v)
    {
        switch (a)
        {
            case 0xff10:
                _sweepPeriod = (v & 0x70) >> 4;
                _sweepNegate = (v & 0x08) > 0 ? -1 : 1;
                _sweepShift = v & 0x07;
                _nr10 = v;
                break;
            case 0xff11:
                Duty = (v & 0xc0) >> 6;
                LengthCounter = 64 - (v & 0x3f);
                _nr11 = v;
                break;
            case 0xff12:
                EnvVolume = (v & 0xf0) >> 4;
                EnvDirection = (v & 0x08) > 0;
                EnvPeriod = v & 0x07;
                Dac = (v & 0xf8) > 0;
                _nr12 = v;
                break;
            case 0xff13:
                Frequency = Frequency & 0x0700 | v;
                _nr13 = v;
                break;
            case 0xff14:
                Frequency = (Frequency & 0xff) | (v & 0x07) << 8;
                _shadowFrequency = Frequency;
                LengthEnabled = (v & 0x40) != 0;
                _sweepEnabled = _sweepPeriod > 0 || _sweepShift > 0;

                if (_sweepShift > 0)
                    UpdateFrequency();

                if ((v & 0x80) != 0)
                    Trigger(64, 4);
                _nr14 = v;
                break;
        }
    }

    public void Sweep()
    {
        if (_sweepTimer > 0)
            _sweepTimer--;

        if (_sweepTimer == 0)
        {
            if (PeriodTimer > 0)
                _sweepTimer = _sweepPeriod;
            else
                _sweepTimer = 8;

            if (_sweepEnabled && _sweepPeriod > 0)
            {
                UpdateFrequency();
                var freq = UpdateFrequency();
                if (freq < 2048 && _sweepShift > 0)
                    Frequency = _shadowFrequency = freq;
            }
        }
    }

    private int UpdateFrequency()
    {
        var freq = _shadowFrequency + _sweepNegate * (_shadowFrequency >> _sweepShift);
        if (freq > 2047)
            Enabled = false;
        return freq;
    }

    public override void Reset()
    {
        Frequency = 0;
        LengthCounter = 0;
        Duty = 0;
        EnvVolume = 0;
        Timer = 0;
        _nr10 = 0x80;
        _nr11 = 0x3f;
        _nr12 = 0x00;
        _nr13 = 0xff;
        _nr14 = 0xbf;
        Dac = false;
        Enabled = false;
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(_sweepPeriod); bw.Write(_sweepNegate); bw.Write(_sweepShift); bw.Write(_sweepTimer);
        bw.Write(_sweepEnabled); bw.Write(_shadowFrequency); bw.Write(Frequency); bw.Write(LengthCounter);
        bw.Write(Duty); bw.Write(EnvVolume); bw.Write(CurrentVolume); bw.Write(Timer);
        bw.Write(_nr10); bw.Write(_nr11); bw.Write(_nr12); bw.Write(_nr13);
        bw.Write(_nr14);
    }

    public void Load(BinaryReader br)
    {
        _sweepPeriod = br.ReadInt32(); _sweepNegate = br.ReadInt32(); _sweepShift = br.ReadInt32(); _sweepTimer = br.ReadInt32();
        _sweepEnabled = br.ReadBoolean(); _shadowFrequency = br.ReadInt32(); Frequency = br.ReadInt32(); LengthCounter = br.ReadInt32();
        Duty = br.ReadInt32(); EnvVolume = br.ReadInt32(); CurrentVolume = br.ReadInt32(); Timer = br.ReadInt32();
        _nr10 = br.ReadInt32(); _nr11 = br.ReadInt32(); _nr12 = br.ReadInt32(); _nr13 = br.ReadInt32();
        _nr14 = br.ReadInt32();
    }

    public List<RegisterInfo> GetState() => [
        new("FF10", "Channel 1", ""),
        new("0-2", "Sweep Shift", $"{_sweepShift}"),
        new("3", "Sweep Negate", $"{_sweepNegate}"),
        new("4-7", "Sweep Period", $"{_sweepPeriod}"),
        new("FF11", "", ""),
        new("0-5", "Length", $"{LengthCounter}"),
        new("6-7", "Duty", $"{Duty}"),
        new("FF12", "", ""),
        new("0-2", "Env Period", $"{EnvPeriod}"),
        new("3", "Env Increase", $"{EnvDirection}"),
        new("4-7", "Env Volume", $"{EnvVolume}"),
        new("FF13/4", "", ""),
        new("0-2", "Frequency", $"{Frequency}"),
        new("FF14", "", ""),
        new("6", "Length Enabled", $"{LengthEnabled}"),
        new("7", "Enabled", $"{Enabled}"),
        new("", "Timer", $"{Timer}"),
        new("", "Duty Position", $"{Position}"),
        new("", "Sweep Enabled", $"{_sweepPeriod > 0}"),
        new("", "Sweep Frequency", $"{_shadowFrequency}"),
        new("", "Sweep Timer", $"{_sweepTimer}"),
        new("", "Env Timer", $"{Duty}"),
    ];
}
