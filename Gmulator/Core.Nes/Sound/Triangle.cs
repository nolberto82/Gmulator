
namespace Gmulator.Core.Nes.Sound;
public class Triangle : BaseChannel
{
    public int LinearCounter { get; set; }
    public int LinearLoad { get; set; }
    public bool LinearReload { get; set; }
    public bool LinearControl { get; set; }

    public void Write(int a, int v)
    {
        if (a == 0x4008)
        {
            LinearControl = (v & 0x80) != 0;
            LengthEnabled = (v & 0x80) == 0;
            LinearLoad = v & 0x7f;
        }
        else if (a == 0x400a)
        {
            Frequency = (ushort)(Frequency & 0x0700 | v);
        }
        else if (a == 0x400b)
        {
            Frequency = (ushort)((Frequency & 0xff) | (v & 0x07) << 8);
            if (Enabled)
                LengthCounter = LengthTable[v >> 3];
            LinearReload = true;
        }
    }

    public override void Reset()
    {
        LinearCounter = 0;
        LinearLoad = 0;
        LinearReload = false;
        LinearControl = false;
        base.Reset();
    }

    public float GetSample()
    {
        if (Enabled && LengthCounter > 0)
        {
            if (LinearCounter > 0)
            {
                if (Timer < 2 && Frequency == 0)
                    return 7.5f;
                return TriangleIndexes[Position];
            }
        }
        return 0;
    }

    public void Step()
    {
        if (Enabled)
        {
            if (Timer == 0)
            {
                Timer = Frequency;
                if (LinearCounter > 0 && LengthCounter > 0)
                    Position = (Position + 1) & 31;
            }
            else
                Timer--;
        }
    }

    public void Linear()
    {
        if (LinearReload)
            LinearCounter = LinearLoad;
        else if (LinearCounter > 0)
            LinearCounter--;

        if (!LinearControl)
            LinearReload = false;
    }

    public void Length()
    {
        if (LengthEnabled && LengthCounter > 0)
            LengthCounter--;
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
