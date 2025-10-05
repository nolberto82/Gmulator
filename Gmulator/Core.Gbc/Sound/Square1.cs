
namespace Gmulator.Core.Gbc.Sound;
public class Square1 : BaseChannel
{
    public int SweepPeriod { get; private set; }
    public int SweepNegate { get; private set; }
    public int SweepShift { get; private set; }
    public int SweepTimer { get; set; }
    public bool SweepEnabled { get; private set; }
    public int ShadowFrequency { get; set; }

    public byte NR10 { get; private set; }
    public byte NR11 { get; private set; }
    public byte NR12 { get; private set; }
    public byte NR13 { get; private set; }
    public byte NR14 { get; private set; }

    public Square1() { }

    public void Sweep()
    {
        if (SweepTimer > 0)
            SweepTimer--;

        if (SweepTimer == 0)
        {
            if (PeriodTimer > 0)
                SweepTimer = SweepPeriod;
            else
                SweepTimer = 8;

            if (SweepEnabled && SweepPeriod > 0)
            {
                UpdateFrequency();
                var freq = UpdateFrequency();
                if (freq < 2048 && SweepShift > 0)
                    Frequency = ShadowFrequency = freq;
            }
        }
    }

    public override byte Read(int a)
    {
        if (a == 0x10)
            return (byte)(NR10 | 0x80);
        else if (a == 0x11)
            return (byte)(NR11 | 0x3f);
        else if (a == 0x12)
            return NR12;
        else if (a == 0x13)
            return (byte)(NR13 | 0xff);
        else if (a == 0x14)
            return (byte)(NR14 | 0xbf);
        return 0xff;
    }

    public override void Write(int a, byte v)
    {
        if (a == 0x10)
        {
            SweepPeriod = (v & 0x70) >> 4;
            SweepNegate = (v & 0x08) > 0 ? -1 : 1;
            SweepShift = v & 0x07;
            NR10 = v;
        }
        else if (a == 0x11)
        {
            Duty = (v & 0xc0) >> 6;
            LengthCounter = 64 - (v & 0x3f);
            NR11 = v;
        }
        else if (a == 0x12)
        {
            EnvVolume = (v & 0xf0) >> 4;
            EnvDirection = (v & 0x08) > 0;
            EnvPeriod = v & 0x07;
            Dac = (v & 0xf8) > 0;
            NR12 = v;
        }
        else if (a == 0x13)
        {
            Frequency = Frequency & 0x0700 | v;
            NR13 = v;
        }
        else if (a == 0x14)
        {
            Frequency = (Frequency & 0xff) | (v & 0x07) << 8;
            ShadowFrequency = Frequency;
            LengthEnabled = (v & 0x40) != 0;
            SweepEnabled = SweepPeriod > 0 || SweepShift > 0;

            if (SweepShift > 0)
              UpdateFrequency();

            if ((v & 0x80) != 0)
                Trigger(64, 4);
            NR14 = v;
        }
    }

    private int UpdateFrequency()
    {
        var freq = ShadowFrequency + SweepNegate * (ShadowFrequency >> SweepShift);
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
        NR10 = 0x80;
        NR11 = 0x3f;
        NR12 = 0x00;
        NR13 = 0xff;
        NR14 = 0xbf;
        Dac = false;
        Enabled = false;
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(SweepPeriod);
        bw.Write(SweepNegate);
        bw.Write(SweepShift);
        bw.Write(SweepTimer);
        bw.Write(SweepEnabled);
        bw.Write(ShadowFrequency);
        bw.Write(Frequency);
        bw.Write(LengthCounter);
        bw.Write(Duty);
        bw.Write(EnvVolume);
        bw.Write(CurrentVolume);
        bw.Write(Timer);
        bw.Write(NR10);
        bw.Write(NR11);
        bw.Write(NR12);
        bw.Write(NR13);
        bw.Write(NR14);
    }

    public override void Load(BinaryReader br)
    {
        SweepPeriod = br.ReadInt32();
        SweepNegate = br.ReadInt32();
        SweepShift = br.ReadInt32();
        SweepTimer = br.ReadInt32();
        SweepEnabled = br.ReadBoolean();
        ShadowFrequency = br.ReadInt32();
        Frequency = br.ReadInt32();
        LengthCounter = br.ReadInt32();
        Duty = br.ReadInt32();
        EnvVolume = br.ReadInt32();
        CurrentVolume = br.ReadInt32();
        Timer = br.ReadInt32();
        NR10 = br.ReadByte();
        NR11 = br.ReadByte();
        NR12 = br.ReadByte();
        NR13 = br.ReadByte();
        NR14 = br.ReadByte();
    }
}
