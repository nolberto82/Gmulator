
using Gmulator.Interfaces;

namespace Gmulator.Core.Gbc.Sound;

public class Noise : BaseChannel, ISaveState
{
    private int _nr41;
    private int _nr42;
    private int _nr43;
    private int _nr44;

    public Noise(Gbc gbc)
    {
        gbc.CpuMap.Set(0x00, 0x01, 0xff20, 0xff23, Read, Write, RamType.Register, 1);
    }

    public int Read(int addr) => addr switch
    {
        0xff20 => (byte)(_nr41 | 0xff),
        0xff21 => _nr42,
        0xff22 => _nr43,
        0xff23 => (byte)(_nr44 | 0xbf),
        _ => 0xff,
    };

    public void Write(int addr, int value)
    {
        switch (addr)
        {
            case 0xff20:
                LengthCounter = 64 - (value & 0x3f);
                _nr41 = value;
                break;
            case 0xff21:
                EnvVolume = (value & 0xf0) >> 4;
                EnvDirection = (value & 0x08) > 0;
                EnvPeriod = value & 0x07;
                Dac = (value & 0xf8) > 0;
                _nr42 = value;
                break;
            case 0xff22:
                Shift = (value & 0xf0) >> 4;
                Width = (value & 0x08) >> 3;
                Divisor = value & 0x07;
                _nr43 = value;
                break;
            case 0xff23:
                LengthEnabled = (value & 0x40) != 0;
                if ((value & 0x80) != 0)
                {
                    Frequency = (Frequency & 0xff) | (value & 0x07) << 8;
                    Trigger(64, 4);
                    LFSR = 0x7fff;
                }
                _nr44 = value;
                break;
        }
    }

    public override void Reset()
    {
        Frequency = 0;
        LengthCounter = 0;
        Duty = 0;
        EnvVolume = 0;
        Timer = 0;
        CurrentVolume = 0;
        Sample = 0;
        LFSR = 0x7fff;
        _nr41 = 0xff;
        _nr42 = 0x00;
        _nr43 = 0x00;
        _nr44 = 0xbf;
        Dac = false;
        Enabled = false;
        Timer = 8192;
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(Width); bw.Write(Divisor); bw.Write(LFSR); bw.Write(Frequency);
        bw.Write(LengthCounter); bw.Write(Duty); bw.Write(EnvVolume); bw.Write(CurrentVolume);
        bw.Write(Timer); bw.Write(_nr41); bw.Write(_nr42); bw.Write(_nr43);
        bw.Write(_nr44);
    }

    public void Load(BinaryReader br)
    {
        Width = br.ReadInt32(); Divisor = br.ReadInt32(); LFSR = br.ReadInt32(); Frequency = br.ReadInt32();
        LengthCounter = br.ReadInt32(); Duty = br.ReadInt32(); EnvVolume = br.ReadInt32(); CurrentVolume = br.ReadInt32();
        Timer = br.ReadInt32(); _nr41 = br.ReadByte(); _nr42 = br.ReadByte(); _nr43 = br.ReadByte();
        _nr44 = br.ReadByte();
    }

    public List<RegisterInfo> GetState() =>
    [
        new("FF20","Channel 4",""),
        new("0-5","Length", $"{LengthCounter}"),
        new("FF21","",""),
        new("0-2","Env Period", $"{EnvPeriod}"),
        new("3","Env Increase", $"{EnvDirection}"),
        new("4-7","Env Volume", $"{EnvVolume}"),
        new("FF22","",""),
        new("0-2","Divisor", $"{Shift}"),
        new("3","Width", $"{LFSR}"),
        new("4-7","Frequency", $"{Frequency}"),
        new("FF23","",""),
        new("6","Length Enabled", $"{LengthEnabled}"),
        new("7","Enabled", $"{Enabled}"),
        new("","Timer", $"{Timer}"),
        new("","Position", $"{Position}"),
    ];
}
