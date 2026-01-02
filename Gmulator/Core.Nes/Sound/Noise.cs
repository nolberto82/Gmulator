

namespace Gmulator.Core.Nes.Sound;

public class Noise : BaseChannel
{
    public ushort ShiftReg { get; private set; }
    public bool Mode { get; private set; }

    public Noise(Nes nes)
    {
        nes.SetMemory(0x00, 0x00, 0x400c, 0x400f, 0xffff, (int a) => 0xff, Write, RamType.Register, 1);
    }

    public void Write(int a, int v)
    {
        switch (a)
        {
            case 0x400c:
                EnvVolume = v & 0x0f;
                ConstVolumeFlag = (v & 0x10) != 0;
                EnvLoop = (v & 0x20) != 0;
                LengthEnabled = !EnvLoop;
                break;
            case 0x400d:
                break;
            case 0x400e:
                Frequency = NoiseTable[v & 0x0f] & 0xffff;
                Mode = (v & 0x80) != 0;
                break;
            case 0x400f:
                if (Enabled)
                    LengthCounter = LengthTable[v >> 3];
                EnvStart = true;
                break;
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

    public void Save(BinaryWriter bw)
    {
        bw.Write(Enabled); bw.Write(Play); bw.Write(Output); bw.Write(EnvPeriod);
        bw.Write(Duty); bw.Write(Position); bw.Write(LengthCounter); bw.Write(LengthReloadFlag);
        bw.Write(LengthEnabled); bw.Write(Timer); bw.Write(Frequency); bw.Write(PeriodTimer);
        bw.Write(CurrentVolume); bw.Write(EnvVolume); bw.Write(ConstVolumeFlag); bw.Write(EnvStart);
        bw.Write(EnvDivider); bw.Write(EnvCounter); bw.Write(EnvLoop); bw.Write(ShiftReg);
        bw.Write(Mode);
    }

    public void Load(BinaryReader br)
    {
        Enabled = br.ReadBoolean(); Play = br.ReadBoolean(); Output = br.ReadInt32(); EnvPeriod = br.ReadInt32();
        Duty = br.ReadInt32(); Position = br.ReadInt32(); LengthCounter = br.ReadInt32(); LengthReloadFlag = br.ReadBoolean();
        LengthEnabled = br.ReadBoolean(); Timer = br.ReadInt32(); Frequency = br.ReadInt32(); PeriodTimer = br.ReadInt32();
        CurrentVolume = br.ReadInt32(); EnvVolume = br.ReadInt32(); ConstVolumeFlag = br.ReadBoolean(); EnvStart = br.ReadBoolean();
        EnvDivider = br.ReadInt32(); EnvCounter = br.ReadInt32(); EnvLoop = br.ReadBoolean(); ShiftReg = br.ReadUInt16();
        Mode = br.ReadBoolean();
    }
}
