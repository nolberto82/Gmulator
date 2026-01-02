
using Gmulator.Interfaces;

namespace Gmulator.Core.Gbc.Sound;

public class Square2 : BaseChannel, ISaveState
{
    private int _nr21;
    private int _nr22;
    private int _nr23;
    private int _nr24;

    public Square2(Gbc gbc)
    {
        gbc.SetMemory(0x00, 0x01, 0xff16, 0xff19, 0xffff, Read, Write, RamType.Register, 1);
    }

    public int Read(int a)
    {
        return a switch
        {
            0xff16 => _nr21 | 0x3f,
            0xff17 => _nr22,
            0xff18 => _nr23 | 0xff,
            0xff19 => _nr24 | 0xbf,
            _ => 0xff,
        };
    }

    public void Write(int a, int v)
    {
        switch (a)
        {
            case 0xff16:
                Duty = (v & 0xc0) >> 6;
                LengthCounter = 64 - (v & 0x3f);
                _nr21 = v;
                break;
            case 0xff17:
                EnvVolume = (v & 0xf0) >> 4;
                EnvDirection = (v & 0x08) > 0;
                EnvPeriod = v & 0x07;
                Dac = (v & 0xf8) > 0;
                _nr22 = v;
                break;
            case 0xff18:
                Frequency = Frequency & 0xff00 | v;
                _nr23 = v;
                break;
            case 0xff19:
                Frequency = (Frequency & 0xff) | (v & 0x07) << 8;
                LengthEnabled = (v & 0x40) != 0;
                if ((v & 0x80) != 0)
                    Trigger(64, 4);
                _nr24 = v;
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

        _nr21 = 0x3f;
        _nr22 = 0x00;
        _nr23 = 0xff;
        _nr24 = 0xbf;
        Dac = false;
        Enabled = false;
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(Frequency); bw.Write(LengthCounter); bw.Write(Duty); bw.Write(EnvVolume);
        bw.Write(CurrentVolume); bw.Write(Timer); bw.Write(_nr21); bw.Write(_nr22);
        bw.Write(_nr23); bw.Write(_nr24);
    }

    public void Load(BinaryReader br)
    {
        Frequency = br.ReadInt32(); LengthCounter = br.ReadInt32(); Duty = br.ReadInt32(); EnvVolume = br.ReadInt32();
        CurrentVolume = br.ReadInt32(); Timer = br.ReadInt32(); _nr21 = br.ReadInt32(); _nr22 = br.ReadInt32();
        _nr23 = br.ReadInt32(); _nr24 = br.ReadInt32();
    }

    public List<RegisterInfo> GetState() =>
    [
        new("FF16", "Channel 2", ""),
        new("0-5", "Length", $"{LengthCounter}"),
        new("6-7", "Duty", $"{Duty}"),
        new("FF17", "", ""),
        new("0-2", "Env Period", $"{EnvPeriod}"),
        new("3", "Env Increase", $"{EnvDirection}"),
        new("4-7", "Env Volume", $"{EnvVolume}"),
        new("FF18", "", ""),
        new("0-2", "Frequency", $"{Frequency}"),
        new("FF19", "", ""),
        new("0-2", "Frequency", $"{Frequency}"),
        new("6", "Length Enabled", $"{LengthEnabled}"),
        new("7", "Enabled", $"{Enabled}"),
        new("", "Timer", $"{Timer}"),
        new("", "Duty Position", $"{Position}"),
        new("", "Env Timer", $"{Duty}"),
    ];
}
