using Gmulator.Interfaces;

namespace Gmulator.Core.Gbc.Sound;

public class Wave : BaseChannel, ISaveState
{
    private byte _wave;
    private int _nr30;
    private int _nr31;
    private int _nr32;
    private int _nr33;
    private int _nr34;

    private readonly Gbc Gbc;

    private readonly byte[] WaveRamReset =
    [
        0x84, 0x40, 0x43, 0xaa, 0x2d, 0x78, 0x92, 0x3c,
        0x60, 0x59, 0x59, 0xb0, 0x34, 0xb8, 0x2e, 0xda
    ];

    private readonly Func<bool> CGBEnabled;

    public Wave() { }
    public Wave(Gbc gbc)
    {
        CGBEnabled = gbc.GetCGBEnabled;
        Gbc = gbc;
        gbc.CpuMap.Set(0x00, 0x00, 0xff1a, 0xff1e, Read, Write, RamType.Register, 1);
    }

    public int Read(int addr) => addr switch
    {
        0xff1a => (byte)(_nr30 | 0x7f),
        0xff1b => (byte)(_nr31 | 0xff),
        0xff1c => (byte)(_nr32 | 0x9f),
        0xff1d => (byte)(_nr33 | 0xff),
        0xff1e => (byte)(_nr34 | 0xbf),
        _ => 0xff,
    };

    public void Write(int addr, int value)
    {
        switch (addr)
        {
            case 0xff1a:
                Dac = (value & 0x80) != 0;
                _nr30 = value;
                break;
            case 0xff1b:
                LengthCounter = 256 - value;
                _nr31 = value;
                break;
            case 0xff1c:
                VolumeShift = ((value & 0x60) >> 5) & 3;
                _nr32 = value;
                break;
            case 0xff1d:
                Frequency = Frequency & 0xff00 | value;
                _nr33 = value;
                break;
            case 0xff1e:
                if (Dac)
                {
                    Frequency = (Frequency & 0xff) | (value & 0x07) << 8;
                    LengthEnabled = (value & 0x40) != 0;
                    if ((value & 0x80) != 0)
                        Trigger(256, 2);
                }
                _nr34 = value;
                break;
        }
    }

    private int ReadWaveRam(int addr) => WaveRam[addr & 0x0f];
    private void WriteWaveRam(int addr, int value) => WaveRam[addr & 0x0f] = (byte)value;//Mmu.WriteByte(0xff00 + addr, value);

    public override void Reset()
    {
        Frequency = 0;
        LengthCounter = 0;
        Duty = 0;
        EnvVolume = 0;
        Timer = 0;
        CurrentVolume = 0;
        Sample = 0;
        Position = 0;

        _nr30 = 0x7f;
        _nr31 = 0xff;
        _nr32 = 0x9f;
        _nr33 = 0xff;
        _nr34 = 0xbf;

        Dac = false;
        Enabled = false;
        Timer = 8192;

        WaveRam = new byte[16];
        if (CGBEnabled())
            Buffer.BlockCopy(WaveRamReset, 0, WaveRam, 0, WaveRamReset.Length);
        else
        {
            Random r = new(Guid.NewGuid().GetHashCode());
            r.NextBytes(WaveRam);
        }

        Gbc.CpuMap.Set(0x00, 0x00, 0xff30, 0xff3f, ReadWaveRam, WriteWaveRam, RamType.Register, 1);
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(Frequency); bw.Write(LengthCounter); bw.Write(Duty); bw.Write(EnvVolume);
        bw.Write(CurrentVolume); bw.Write(Timer); bw.Write(VolumeShift); bw.Write(_wave);
        WriteArray(bw, WaveRam); bw.Write(_nr30); bw.Write(_nr31); bw.Write(_nr32);
        bw.Write(_nr33); bw.Write(_nr34);
    }

    public void Load(BinaryReader br)
    {
        Frequency = br.ReadInt32(); LengthCounter = br.ReadInt32(); Duty = br.ReadInt32(); EnvVolume = br.ReadInt32();
        CurrentVolume = br.ReadInt32(); Timer = br.ReadInt32(); VolumeShift = br.ReadInt32(); _wave = br.ReadByte();
        WaveRam = ReadArray<byte>(br, WaveRam.Length); _nr30 = br.ReadInt32(); _nr31 = br.ReadInt32(); _nr32 = br.ReadInt32();
        _nr33 = br.ReadInt32(); _nr34 = br.ReadInt32();
        Dac = (_nr30 & 0x80) != 0;
    }

    public List<RegisterInfo> GetState() =>
    [
        new("FF1A","Channel 3",""),
        new("7","Sound Enabled", $"{Dac}"),
        new("FF1B","",""),
        new("0-7","Length", $"{LengthCounter}"),
        new("FF1C","",""),
        new("5-6","Volume", $"{VolumeShift}"),
        new("FF1D","",""),
        new("0-2","Frequency", $"{Frequency}"),
        new("FF1E","",""),
        new("0-2","Frequency", $"{Frequency}"),
        new("6","Length Enabled", $"{LengthEnabled}"),
        new("7","Enabled", $"{Enabled}"),
        new("","Timer", $"{Timer}"),
        new("","Position", $"{Position}"),
    ];
}
