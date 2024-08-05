
namespace GNes.Core.Sound;
public class Noise : BaseChannel
{
    public ushort ShiftReg { get; private set; }
    public bool Mode { get; private set; }

    public void Write(int a, byte v)
    {
        if (a == 0x400c)
        {
            EnvVolume = v & 0x0f;
            ConstVolumeFlag = v.GetBit(4);
            EnvLoop = v.GetBit(5);
            LengthEnabled = !EnvLoop;
        }
        else if (a == 0x400e)
        {
            Frequency = (ushort)NoiseTable[v & 0x0f];
            Mode = v.GetBit(7);
        }
        else if (a == 0x400f)
        {
            if (Enabled)
                LengthCounter = LengthTable[v >> 3];
            EnvStart = true;
        }
    }

    public override void Reset()
    {
        ShiftReg = 1;
        base.Reset();
    }

    public int GetSample()
    {
        if (Enabled)
        {
            if ((ShiftReg & 1) == 0)
            {
                if (LengthCounter > 0)
                {
                    if (ConstVolumeFlag)
                        return EnvVolume;
                    else
                        return EnvCounter;
                }
            }
        }
        return 0;
    }

    public void Step()
    {
        if (Enabled)
        {
            if (--Timer <= 0)
            {
                Timer = Frequency;
                int res;
                if (Mode)
                    res = ((ShiftReg >> 6) & 1) ^ ShiftReg & 1;
                else
                    res = ((ShiftReg >> 1) & 1) ^ ShiftReg & 1;
                ShiftReg >>= 1;
                ShiftReg |= (ushort)(res << 14);
            }
        }
    }

    public void Length()
    {
        if (LengthEnabled && LengthCounter > 0)
            LengthCounter--;
    }

    public void Envelope()
    {
        if (EnvStart)
        {
            EnvStart = false;
            EnvCounter = 15;
            EnvDivider = EnvVolume;
        }
        else
        {
            if (EnvDivider > 0)
                EnvDivider--;
            else
            {
                EnvDivider = EnvVolume;
                if (EnvCounter > 0)
                    EnvCounter--;
                else if (EnvLoop)
                    EnvCounter = 15;
            }
        }
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(Output);
        bw.Write(EnvPeriod);
        bw.Write(Duty);

        bw.Write(LengthCounter);
        bw.Write(LengthReloadFlag);
        bw.Write(LengthEnabled);

        bw.Write(Timer);
        bw.Write(Position);
        bw.Write(Frequency);

        bw.Write(PeriodTimer);
        bw.Write(CurrentVolume);
        bw.Write(EnvVolume);
        bw.Write(ConstVolumeFlag);
        bw.Write(EnvStart);

        bw.Write(EnvDivider);
        bw.Write(EnvCounter);
        bw.Write(EnvLoop);
    }

    public override void Load(BinaryReader br)
    {
        Output = br.ReadInt32();
        EnvPeriod = br.ReadInt32();
        Duty = br.ReadInt32();

        LengthCounter = br.ReadInt32();
        LengthReloadFlag = br.ReadBoolean();
        LengthEnabled = br.ReadBoolean();

        Timer = br.ReadInt32();
        Position = br.ReadInt32();
        Frequency = br.ReadInt32();

        PeriodTimer = br.ReadInt32();
        CurrentVolume = br.ReadInt32();
        EnvVolume = br.ReadInt32();
        ConstVolumeFlag = br.ReadBoolean();
        EnvStart = br.ReadBoolean();

        EnvDivider = br.ReadInt32();
        EnvCounter = br.ReadInt32();
        EnvLoop = br.ReadBoolean();
    }
}
