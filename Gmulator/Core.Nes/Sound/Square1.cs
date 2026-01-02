using System.Threading.Channels;

namespace Gmulator.Core.Nes.Sound;

public class Square1 : BaseChannel
{
    public Square1(Nes nes)
    {
        nes.SetMemory(0x00, 0x00, 0x4000, 0x4003, 0xffff, (int a) => 0xff, Write, RamType.Register, 1);
    }

    public void Write(int a, int v)
    {
        switch (a)
        {
            case 0x4000:
                EnvVolume = v & 0x0f;
                ConstVolumeFlag = (v & 0x10) != 0;
                EnvLoop = (v & 0x20) != 0;
                LengthEnabled = !((v & 0x20) != 0);
                Duty = (v & 0xc0) >> 6;
                EnvStart = true;
                break;
            case 0x4001:
                SweepShift = v & 0x07;
                SweepNegate = (v & 0x08) != 0;
                SweepPeriod = ((v & 0x70) >> 4) + 1;
                SweepEnabled = (v & 0x80) != 0 && SweepShift > 0;
                SweepReload = true;
                break;
            case 0x4002:
                Frequency = (ushort)(Frequency & 0x0700 | v);
                break;
            case 0x4003:
                Frequency = (ushort)((Frequency & 0xff) | (v & 0x07) << 8);
                if (Enabled)
                    LengthCounter = LengthTable[v >> 3];
                Position = 0;
                EnvStart = true;
                break;
        }
    }

    public override void Reset()
    {
        SweepEnabled = false;
        base.Reset();
    }

    public int GetSample()
    {
        if (Enabled)
        {
            if (LengthCounter > 0)
            {
                if (WaveDuty[Duty][Position] == 1)
                {
                    if (!IsSweepMuted())
                    {
                        if (ConstVolumeFlag)
                            return EnvVolume;
                        else
                            return EnvCounter;
                    }
                }
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
                Position = (Position + 1) & 7;
            }
            else
                Timer--;
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

    public void Sweep()
    {
        if (SweepReload)
        {
            if (SweepEnabled && !IsSweepMuted())
                UpdateSweep();

            SweepReload = false;
            SweepCounter = SweepPeriod;
        }
        else if (SweepCounter > 0)
            SweepCounter--;
        else
        {
            SweepCounter = SweepPeriod;
            if (SweepEnabled && !IsSweepMuted())
                UpdateSweep();
        }
    }

    private void UpdateSweep()
    {
        var v = Frequency >> SweepShift;
        if (SweepNegate)
        {
            Frequency -= v;
            Frequency--;
        }
        else
            Frequency += v;
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(Enabled); bw.Write(Play); bw.Write(Output); bw.Write(EnvPeriod);
        bw.Write(Duty); bw.Write(Position); bw.Write(LengthCounter); bw.Write(LengthReloadFlag);
        bw.Write(LengthEnabled); bw.Write(Timer); bw.Write(Frequency); bw.Write(PeriodTimer);
        bw.Write(CurrentVolume); bw.Write(EnvVolume); bw.Write(ConstVolumeFlag); bw.Write(EnvStart);
        bw.Write(EnvDivider); bw.Write(EnvCounter); bw.Write(EnvLoop); bw.Write(SweepCounter);
        bw.Write(SweepPeriod); bw.Write(SweepNegate); bw.Write(SweepShift); bw.Write(SweepTimer);
        bw.Write(SweepEnabled); bw.Write(ShadowFrequency); bw.Write(SweepReload);
    }

    public void Load(BinaryReader br)
    {
        Enabled = br.ReadBoolean(); Play = br.ReadBoolean(); Output = br.ReadInt32(); EnvPeriod = br.ReadInt32();
        Duty = br.ReadInt32(); Position = br.ReadInt32(); LengthCounter = br.ReadInt32(); LengthReloadFlag = br.ReadBoolean();
        LengthEnabled = br.ReadBoolean(); Timer = br.ReadInt32(); Frequency = br.ReadInt32(); PeriodTimer = br.ReadInt32();
        CurrentVolume = br.ReadInt32(); EnvVolume = br.ReadInt32(); ConstVolumeFlag = br.ReadBoolean(); EnvStart = br.ReadBoolean();
        EnvDivider = br.ReadInt32(); EnvCounter = br.ReadInt32(); EnvLoop = br.ReadBoolean(); SweepCounter = br.ReadInt32();
        SweepPeriod = br.ReadInt32(); SweepNegate = br.ReadBoolean(); SweepShift = br.ReadInt32(); SweepTimer = br.ReadInt32();
        SweepEnabled = br.ReadBoolean(); ShadowFrequency = br.ReadInt32(); SweepReload = br.ReadBoolean();
    }
}
