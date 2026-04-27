using Gmulator.Core.Gbc.Sound;
using Gmulator.Interfaces;
using Wave = Gmulator.Core.Gbc.Sound.Wave;

namespace Gmulator.Core.Gbc;

public class GbcApu : ISaveState
{
    private byte _nr50;
    private byte _nr51;
    private byte _nr52;

    private int _frameSequencer;
    private int _frameSequencerCycles;

    private int _nextSampleTimer;
    private int _bufferPosition;

    private byte _volumeLeft;
    private byte _volumeRight;

    public Square1 Square1 { get; private set; }
    public Square2 Square2 { get; private set; }
    public Wave Wave { get; private set; }
    public Noise Noise { get; private set; }

    public float[] AudioBuffer { get; private set; } = new float[MaxSamples * 2];

    public const int MaxSamples = 4096;
    public const int SampleRate = 44100;
    public readonly int SamplesCpu;

    public GbcApu(Gbc gbc, int cpuclock)
    {
        Square1 = new(gbc);
        Square2 = new(gbc);
        Wave = new(gbc);
        Noise = new(gbc);
        SamplesCpu = cpuclock / SampleRate;

        gbc.CpuMap.Set(0x00, 0x01, 0xff24, 0xff26, Read, Write, RamType.Register, 1);
    }

    public void Step(int cycles)
    {
        if ((_nr52 & 0x80) == 0) return;
        Square1.Step(1, cycles);
        Square2.Step(2, cycles);
        Wave.Step(3, cycles);
        Noise.Step(4, cycles);

        _frameSequencerCycles -= (byte)cycles;
        if (_frameSequencerCycles == 0)
        {
            switch (_frameSequencer)
            {
                case 0 or 2 or 4 or 6:
                    if (_frameSequencer == 2 || _frameSequencer == 6)
                        Square1.Sweep();
                    Square1.Length();
                    Square2.Length();
                    Wave.Length();
                    Noise.Length();
                    break;
                case 7:
                    Square1.Envelope();
                    Square2.Envelope();
                    Noise.Envelope();
                    break;
            }
            _frameSequencerCycles = 8192;
            if (++_frameSequencer == 8)
                _frameSequencer = 0;
        }

        _nextSampleTimer -= (byte)cycles;
        if (_nextSampleTimer <= 0)
        {
            _nextSampleTimer = (byte)SamplesCpu;

            float l = 0, r = 0;

            if (Square1.Enabled && Square1.Play)
            {
                l = Square1.LeftOn ? Square1.GetSample(1) * (_volumeLeft + 1) / 7.5f : 0;
                r = Square1.RightOn ? Square1.GetSample(1) * (_volumeRight + 1) / 7.5f : 0;
            }

            if (Square2.Enabled && Square2.Play)
            {
                l += Square2.LeftOn ? Square2.GetSample(2) * (_volumeLeft + 1) / 7.5f : 0;
                r += Square2.RightOn ? Square2.GetSample(2) * (_volumeRight + 1) / 7.5f : 0;
            }

            if (Wave.Enabled && Wave.Play)
            {
                l += Wave.LeftOn ? Wave.GetSample(3) * (_volumeLeft + 1) / 7.5f : 0;
                r += Wave.RightOn ? Wave.GetSample(3) * (_volumeRight + 1) / 7.5f : 0;
            }

            if (Noise.Enabled && Noise.Play)
            {
                l += Noise.LeftOn ? Noise.GetSample(4) * (_volumeLeft + 1) / 7.5f : 0;
                r += Noise.RightOn ? Noise.GetSample(4) * (_volumeRight + 1) / 7.5f : 0;
            }

            if (Raylib.IsWindowResized())
            {
                AudioBuffer[_bufferPosition++] = 0;
                AudioBuffer[_bufferPosition++] = 0;
            }
            else
            {
                AudioBuffer[_bufferPosition++] = l / 64;
                AudioBuffer[_bufferPosition++] = r / 64;
            }

            if (_bufferPosition >= AudioBuffer.Length)
            {
                Audio.Update(AudioBuffer);
                _bufferPosition = 0;
            }
        }
    }

    public byte Read(int a) => a switch
    {
        0xff24 => _nr50,
        0xff25 => _nr51,
        0xff26 => (byte)(_nr52 | 0x70),
        _ => 0,
    };

    public void Write(int a, byte v)
    {
        switch (a)
        {
            case 0xff24:
                _volumeRight = (byte)(v & 0x07);
                _volumeLeft = (byte)((v & 0x70) >> 4);
                _nr50 = v;
                break;
            case 0xff25:
                Square1.RightOn = (v & 0x01) != 0;
                Square2.RightOn = (v & 0x02) != 0;
                Wave.RightOn = (v & 0x04) != 0;
                Noise.RightOn = (v & 0x08) != 0;
                Square1.LeftOn = (v & 0x10) != 0;
                Square2.LeftOn = (v & 0x20) != 0;
                Wave.LeftOn = (v & 0x40) != 0;
                Noise.LeftOn = (v & 0x80) != 0;
                _nr51 = v;
                break;
            case 0xff26:
                _nr52 = (byte)(v & 0x80);
                if ((v & 0x80) == 0)
                    Reset();
                break;
        }
    }

    public void Reset()
    {
        Square1.Reset();
        Square2.Reset();
        Wave.Reset();
        Noise.Reset();
        _nr50 = _nr51 = 0x00; _nr52 = 0x70;
        _frameSequencerCycles = 8192;
        //Array.Fill<short>(AudioBuffer, 0);
    }

    public static byte[] GetSamples(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var s = (short)(samples[i] * short.MaxValue);
            bytes[i * 2] = (byte)((byte)s & 0xff);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return bytes;
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(_nr50); bw.Write(_nr51); bw.Write(_nr52); bw.Write(_frameSequencer);
        bw.Write(_frameSequencerCycles); bw.Write(_nextSampleTimer); bw.Write(_bufferPosition); bw.Write(_volumeLeft);
        bw.Write(_volumeRight);
        Square1.Save(bw); Square2.Save(bw); Wave.Save(bw); Noise.Save(bw);
    }

    public void Load(BinaryReader br)
    {
        _nr50 = br.ReadByte(); _nr51 = br.ReadByte(); _nr52 = br.ReadByte(); _frameSequencer = br.ReadInt32();
        _frameSequencerCycles = br.ReadInt32(); _nextSampleTimer = br.ReadInt32(); _bufferPosition = br.ReadInt32(); _volumeLeft = br.ReadByte();
        _volumeRight = br.ReadByte();
        Square1.Load(br); Square2.Load(br); Wave.Load(br); Noise.Load(br);
    }

    public List<RegisterInfo> GetState()
    {
        List<RegisterInfo> list =
        [
            new("FF24","Apu",""),
            new("0-2","Volume Right",$"{_volumeRight}"),
            new("4-6","Volume Left",$"{_volumeLeft}"),
            new("FF25","",""),
            new("0","Square 1 Right",$"{Square1.RightOn}"),
            new("4","Square 1 Left",$"{Square1.LeftOn}"),
            new("1","Square 2 Right",$"{Square2.RightOn}"),
            new("5","Square 2 Left",$"{Square2.LeftOn}"),
            new("2","Wave Right",$"{Wave.RightOn}"),
            new("6","Wave Left",$"{Wave.LeftOn}"),
            new("3","Noise Right",$"{Noise.RightOn}"),
            new("7","Noise Left",$"{Noise.LeftOn}"),
            .. Square1.GetState(),
            .. Square2.GetState(),
            .. Wave.GetState(),
            .. Noise.GetState(),
        ];
        return list;
    }
}
