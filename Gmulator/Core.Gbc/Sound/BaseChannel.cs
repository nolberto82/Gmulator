using Gmulator.Shared;

namespace GBoy.Core.Sound;
public abstract class BaseChannel : SaveState
{
    public bool Enabled { get; set; }

    public bool Dac { get; set; }

    public bool RightOn { get; set; }
    public bool LeftOn { get; set; }

    public int Output { get; set; }

    public int EnvPeriod { get; set; }
    public bool EnvDirection { get; set; }
    public int Shift { get; set; }
    public int Duty { get; set; }
    public int LengthCounter { get; set; }
    public bool LengthEnabled { get; set; }

    public int Timer { get; set; } = 8192;

    public int Position { get; set; }

    public int Frequency { get; set; }

    public int PeriodTimer { get; set; }
    public int CurrentVolume { get; set; }
    public int EnvVolume { get; set; }

    public int Wave { get; private set; }
    public int VolumeShift { get; set; }

    public int Width { get; set; }
    public int Divisor { get; set; }
    public int LFSR { get; set; } = 0x7fff;

    public float Sample { get; set; }

    public int FrameSequencerCycles { get; set; }
    public int FrameSequencer { get; set; }

    public bool Play = true;

    public byte[] WaveRam { get; set; }

    public abstract void Write(int a, byte v);
    public abstract byte Read(int a);
    public abstract void Reset();

    public readonly int[][] WaveDuty =
    [
        [0, 0, 0, 0, 0, 0, 0, 1], //12.5%
        [1, 0, 0, 0, 0, 0, 0, 1], //25%
        [1, 0, 0, 0, 0, 1, 1, 1], //50%
        [0, 1, 1, 1, 1, 1, 1, 0]  //75%
    ];
    private byte[] shifts = [4, 0, 1, 2];

    public virtual short GetSample(int channel)
    {
        if (Dac && Enabled)
        {
            if (channel < 3)
            {
                var v = WaveDuty[Duty][Position];
                return (short)(v * CurrentVolume);
            }
            else if (channel == 3)
            {
                return (short)(Wave >> shifts[VolumeShift]);
            }
            else
            {
                if (Dac && Enabled)
                    return (short)((~LFSR & 1) * CurrentVolume);
            }
        }
        return 0;
    }

    public virtual void Step(int channel, int cycles)
    {
        if (Dac && Enabled)
        {
            Timer -= cycles;
            if (Timer <= 0)
            {

                if (channel < 3)
                {
                    Timer = (2048 - Frequency) * 4;
                    Position = (Position + 1) & 7;
                }
                else if (channel == 3)
                {
                    Timer = (2048 - Frequency) * 2;
                    Position = (Position + 1) & 0x1f;
                    var index = Position / 2;
                    Wave = WaveRam[index];
                    if (Position % 2 == 0)
                        Wave = (Wave & 0xf0) >> 4;
                    else
                        Wave &= 0x0f;
                }
                else
                {
                    Timer = (Divisor > 0 ? (Divisor) << 4 : 8) << Shift;
                    var res = (LFSR & 1) ^ ((LFSR & 2) >> 1);
                    LFSR = (LFSR >> 1) | (res << 14);

                    if (Width == 1)
                    {
                        LFSR &= ~(1 << 6);
                        LFSR |= res << 6;
                    }
                }
            }
        }
    }

    public virtual void Length()
    {
        if (LengthEnabled)
        {
            LengthCounter--;
            if (LengthCounter == 0)
            {
                LengthCounter = 0;
                Enabled = false;
                LengthEnabled = false;
            }
        }
    }

    public virtual void Envelope()
    {
        if (EnvPeriod > 0)
        {
            PeriodTimer--;
            if (PeriodTimer <= 0)
            {
                PeriodTimer = EnvPeriod == 0 ? 8 : EnvPeriod;
                if (CurrentVolume > 0 && CurrentVolume < 16)
                    CurrentVolume += EnvDirection ? 1 : -1;
            }
        }
    }

    public virtual void Trigger(int length, int multiplier)
    {
        CurrentVolume = EnvVolume;
        Timer = (2048 - Frequency) * multiplier;
        Enabled = true;
        if (LengthCounter <= 0)
            LengthCounter = length;
    }
}
