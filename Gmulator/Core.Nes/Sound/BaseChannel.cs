namespace GNes.Core.Sound;
public abstract class BaseChannel : EmuState
{
    public bool Enabled { get; set; }
    public bool Play { get; set; } = true;

    public int Output { get; set; }

    public int EnvPeriod { get; set; }
    public int Duty { get; set; }
    public int Position { get; set; }

    public int LengthCounter { get; set; }
    public bool LengthReloadFlag { get; set; }
    public bool LengthEnabled { get; set; }

    public int Timer { get; set; }
    public int Frequency { get; set; }

    public int PeriodTimer { get; set; }
    public int CurrentVolume { get; set; }
    public int EnvVolume { get; set; }
    public bool ConstVolumeFlag { get; set; }
    public bool EnvStart { get; set; }

    public int EnvDivider { get; set; }
    public int EnvCounter { get; set; }
    public bool EnvLoop { get; set; }

    public int SweepCounter { get; set; }
    public int SweepPeriod { get; set; }
    public bool SweepNegate { get; set; }
    public int SweepShift { get; set; }
    public int SweepTimer { get; set; }
    public bool SweepEnabled { get; set; }
    public int ShadowFrequency { get; set; }
    public bool SweepReload { get; set; }

    public static int[][] WaveDuty
    {
        get =>
        [
            [0, 1, 0, 0, 0, 0, 0, 0],
            [0, 1, 1, 0, 0, 0, 0, 0],
            [0, 1, 1, 1, 1, 0, 0, 0],
            [1, 0, 0, 1, 1, 1, 1, 1]
        ];
    }

    public static int[] LengthTable
    {
        get =>
        [
            10, 254, 20,  2, 40,  4, 80,  6,
            160,  8, 60, 10, 14, 12, 26, 14,
            12,  16, 24, 18, 48, 20, 96, 22,
            192, 24, 72, 26, 16, 28, 32, 30
        ];
    }

    public static int[] TriangleIndexes
    {
        get =>
        [
            15, 14, 13, 12, 11, 10,  9,  8,
            7,  6,  5,  4,  3,  2,  1,  0,
            0,  1,  2,  3,  4,  5,  6,  7,
            8,  9, 10, 11, 12, 13, 14, 15
        ];
    }

    public static int[] NoiseTable
    {
        get =>
        [
            4, 8, 16, 32, 64, 96, 128, 160,
            202, 254, 380, 508, 762, 1016, 2034, 4068
        ];
    }

    public bool IsSweepMuted()
    {
        if (Frequency < 8)
            return true;
        else if (!SweepNegate && Frequency + (Frequency >> SweepShift) >= 0x800)
            return true;
        return false;
    }

    public virtual void Reset()
    {
        PeriodTimer = 0;
        CurrentVolume = 0;
        EnvVolume = 0;
        ConstVolumeFlag = false;
        EnvStart = true;
        LengthEnabled = true;
        EnvDivider = 0;
        EnvCounter = 0;
        LengthCounter = 0;
        Frequency = 0;
        Timer = 0;
        Output = 0;
        Enabled = false;
    }
}
