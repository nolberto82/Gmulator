
namespace Gmulator.Core.Gbc.Sound;
public class Noise : BaseChannel
{
    public byte NR41 { get; private set; }
    public byte NR42 { get; private set; }
    public byte NR43 { get; private set; }
    public byte NR44 { get; private set; }

    public Noise() { }

    public override byte Read(int a)
    {
        if (a == 0x20)
            return (byte)(NR41 | 0xff);
        else if (a == 0x21)
            return NR42;
        else if (a == 0x22)
            return NR43;
        else if (a == 0x23)
            return (byte)(NR44 | 0xbf);
        return 0xff;
    }

    public override void Write(int a, byte v)
    {
        if (a == 0x20)
        {
            LengthCounter = 64 - (v & 0x3f);
            NR41 = v;
        }
        else if (a == 0x21)
        {
            EnvVolume = (v & 0xf0) >> 4;
            EnvDirection = (v & 0x08) > 0;
            EnvPeriod = v & 0x07;
            Dac = (v & 0xf8) > 0;
            NR42 = v;
        }
        else if (a == 0x22)
        {
            Shift = (v & 0xf0) >> 4;
            Width = (v & 0x08) >> 3;
            Divisor = v & 0x07;
            NR43 = v;
        }
        else if (a == 0x23)
        {
            LengthEnabled = v.GetBit(6);
            if (v.GetBit(7))
            {
                Frequency = (Frequency & 0xff) | (v & 0x07) << 8;
                Trigger(64, 4);
                LFSR = 0x7fff;
            }
            NR44 = v;
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
        NR41 = 0xff;
        NR42 = 0x00;
        NR43 = 0x00;
        NR44 = 0xbf;
        Dac = false;
        Enabled = false;
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(Width);
        bw.Write(Divisor);
        bw.Write(LFSR);
        bw.Write(Frequency);
        bw.Write(LengthCounter);
        bw.Write(Duty);
        bw.Write(EnvVolume);
        bw.Write(CurrentVolume);
        bw.Write(Timer);
        bw.Write(NR41);
        bw.Write(NR42);
        bw.Write(NR43);
        bw.Write(NR44);
    }

    public override void Load(BinaryReader br)
    {
        Width = br.ReadInt32();
        Divisor = br.ReadInt32();
        LFSR = br.ReadInt32();
        Frequency = br.ReadInt32();
        LengthCounter = br.ReadInt32();
        Duty = br.ReadInt32();
        EnvVolume = br.ReadInt32();
        CurrentVolume = br.ReadInt32();
        Timer = br.ReadInt32();
        NR41 = br.ReadByte();
        NR42 = br.ReadByte();
        NR43 = br.ReadByte();
        NR44 = br.ReadByte();
    }
}
