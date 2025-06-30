using Gmulator;
using GNes.Core.Sound;
using Raylib_cs;

namespace GNes.Core;
public class NesApu : EmuState
{
    public int Status { get; private set; }
    public int FrameCounter { get; private set; }
    public bool IrqEnabled { get; private set; }

    public const int MaxSamples = 4096;
    public const int SampleRate = 44100;
    private int SamplesTotal;

    public float[] AudioBuffer { get; private set; } = new float[MaxSamples * 2];

    public List<float> AudioRecord { get; private set; } = new(5000000);
    public bool Recording { get; set; }

    private int BufPos;

    public int Cycles { get; private set; }

    public Square1 Square1 { get; private set; }
    public Square2 Square2 { get; private set; }
    public Triangle Triangle { get; private set; }
    public Noise Noise { get; private set; }
    public Dmc Dmc { get; private set; }

    private int CyclesSamples;

    public int FrameSequencerCycles { get; set; }
    public int FrameStep { get; set; }
    public readonly float[] SquareTable;
    public readonly float[] TndTable;

    private int Region;

    public NesApu()
    {
        SquareTable = new float[31];
        for (int i = 0; i < 31; i++)
            SquareTable[i] = 95.52f / (8128f / i + 100f);

        TndTable = new float[203];
        for (int i = 0; i < TndTable.Length; i++)
            TndTable[i] = 163.67f / (24329.0f / i + 100.0f);

        Square1 = new();
        Square2 = new();
        Triangle = new();
        Noise = new();
        Dmc = new();
    }

    public void Reset(int region, int cpuclock)
    {
        FrameCounter = 0;
        Square1.Reset();
        Square2.Reset();
        Triangle.Reset();
        Noise.Reset();
        Dmc.Reset();

        Region = region;
        Cycles = 0;
        SamplesTotal = cpuclock / SampleRate;
        Array.Clear(AudioBuffer);
    }

    public void Step(int c)
    {
        while (c-- > 0)
        {
            Triangle.Step();
            Cycles++;
            if ((Cycles % 2) == 0)
            {
                Square1.Step();
                Square2.Step();
                Noise.Step();
                Dmc.Step();
            }

            if (Region == 0)
            {
                switch (Cycles)
                {
                    case 7457:
                        QuarterFrame();
                        break;
                    case 14913:
                        QuarterFrame();
                        HalfFrame();
                        break;
                    case 22371:
                        QuarterFrame();
                        break;
                    case 29829:
                        QuarterFrame();
                        HalfFrame();
                        break;
                    case 29830:
                        if (!FrameCounter.GetBit(7))
                            Cycles = 0;
                        break;
                    case 37281:
                        QuarterFrame();
                        HalfFrame();
                        Cycles = 0;
                        break;
                }
            }
            else if (Region == 1)
            {
                switch (Cycles)
                {
                    case 8313:
                        QuarterFrame();
                        break;
                    case 16627:
                        QuarterFrame();
                        HalfFrame();
                        break;
                    case 24939:
                        QuarterFrame();
                        break;
                    case 33252:
                        if (!FrameCounter.GetBit(7))
                            Cycles = 0;
                        break;
                    case 41565:
                        QuarterFrame();
                        HalfFrame();
                        Cycles = 0;
                        break;
                }
            }

            if (CyclesSamples == 0)
            {
                CyclesSamples = SamplesTotal;
                int outsquare1 = 0, outsquare2 = 0, outnoise = 0, outdmc = 0;
                float outtriangle = 0;

                if (Square1.Enabled && Square1.Play)
                    outsquare1 = Square1.GetSample();
                if (Square2.Enabled && Square2.Play)
                    outsquare2 = Square2.GetSample();
                if (Triangle.Enabled && Triangle.Play)
                    outtriangle = Triangle.GetSample();
                if (Noise.Enabled && Noise.Play)
                    outnoise = Noise.GetSample();
                if (Dmc.Enabled && Dmc.Play)
                    outdmc = Dmc.GetSample();

                if (Raylib.IsWindowResized())
                {
                    AudioBuffer[BufPos++] = 0;
                    AudioBuffer[BufPos++] = 0;
                }
                else
                {
                    var pulse = SquareTable[outsquare1 + outsquare2];
                    var tnd = TndTable[(3 * (int)outtriangle) + (2 * outnoise + outdmc)];
                    var output = (float)HighPass(pulse + tnd);

                    AudioBuffer[BufPos++] = output;
                    AudioBuffer[BufPos++] = output;
                }

                if (BufPos >= AudioBuffer.Length)
                {
                    Audio.Update(AudioBuffer);
                    BufPos = 0;
                }
            }
            else
                CyclesSamples--;
        }
    }

    private readonly double adjust = 0.9975f;
    private double old = 0, res = 0;
    private double HighPass(float input)
    {
        double delta = input - old;
        old = input;
        return res = res * adjust + delta;
    }

    private void QuarterFrame()
    {
        Square1.Envelope();
        Square2.Envelope();
        Triangle.Linear();
        Noise.Envelope();
    }

    private void HalfFrame()
    {
        Square1.Length();
        Square1.Sweep();
        Square2.Length();
        Square2.Sweep();
        Triangle.Length();
        Noise.Length();
    }

    public byte Read(int a)
    {
        if (a == 0x4015)
        {
            var res = 0;
            if (Square1.LengthCounter > 0)
                res |= 0x01;
            if (Square2.LengthCounter > 0)
                res |= 0x02;
            if (Triangle.LengthCounter > 0)
                res |= 0x04;
            if (Noise.LengthCounter > 0)
                res |= 0x08;
            if (Dmc.LengthValue > 0)
                res |= 0x10;

            if (IrqEnabled)
                res |= 0x40;
            IrqEnabled = false;
            return (byte)res;
        }

        return 0xff;
    }

    public void Write(int a, byte v)
    {
        if (a <= 0x4003)
            Square1.Write(a, v);
        else if (a <= 0x4007)
            Square2.Write(a, v);
        else if (a <= 0x400b)
            Triangle.Write(a, v);
        else if (a <= 0x400f)
            Noise.Write(a, v);
        else if (a <= 0x4013)
            Dmc.Write(a, v);

        else if (a == 0x4015)
        {
            Square1.Enabled = v.GetBit(0);
            Square2.Enabled = v.GetBit(1);
            Triangle.Enabled = v.GetBit(2);
            Noise.Enabled = v.GetBit(3);
            Dmc.Enabled = v.GetBit(4);

            if (!Square1.Enabled)
                Square1.LengthCounter = 0;
            if (!Square2.Enabled)
                Square2.LengthCounter = 0;
            if (!Triangle.Enabled)
                Triangle.LengthCounter = 0;
            if (!Noise.Enabled)
                Noise.LengthCounter = 0;

            Dmc.OutputLevel = 0;

            if (Dmc.Enabled)
            {
                Dmc.Write(a, v);
            }

            Status = v;
        }

        else if (a == 0x4017)
        {
            FrameCounter = v;

            IrqEnabled = v == 0 && !v.GetBit(6) && !v.GetBit(7);

            if (v.GetBit(7))
            {
                QuarterFrame();
                HalfFrame();

                //Dmc.LengthCounter = 0;
            }
        }
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(FrameCounter);
        bw.Write(BufPos);
        Square1.Save(bw);
        Square2.Save(bw);
        Triangle.Save(bw);
        Noise.Save(bw);
        Dmc.Save(bw);
    }

    public override void Load(BinaryReader br)
    {
        FrameCounter = br.ReadInt32();
        BufPos = br.ReadInt32();
        Square1.Load(br);
        Square2.Load(br);
        Triangle.Load(br);
        Noise.Load(br);
        Dmc.Load(br);
    }

    public Dictionary<string, dynamic> GetChannel1() => new()
    {
        ["Env Volume"] = Square1.EnvVolume,
        ["Env Constant"] = Square1.ConstVolumeFlag,
        ["Length Halted"] = !Square1.LengthEnabled,
        ["Duty"] = Square1.Position,
        ["Sweep Shift"] = Square1.SweepShift,
        ["Sweep Negate"] = Square1.SweepNegate,
        ["Sweep Period"] = Square1.SweepPeriod,
        ["Period"] = Square1.Frequency,
        ["Length Counter"] = Square1.LengthCounter,
        ["Enabled"] = Square1.Enabled,
        ["Timer"] = Square1.Timer,
        ["Env Divider"] = Square1.EnvDivider,
        ["Volume"] = Square1.EnvCounter,
        ["Apu Cycles"] = Cycles,
    };

    public Dictionary<string, dynamic> GetChannel2() => new()
    {
        ["Sweep Shift"] = Square2.SweepShift,
        ["Sweep Negate"] = Square2.SweepNegate,
        ["Sweep Period"] = Square2.SweepPeriod,
        ["Length Counter"] = Square2.LengthCounter,
        ["Duty"] = Square2.Duty,
        ["Env Volume"] = Square2.EnvVolume,
        ["Frequency"] = Square2.Frequency,
        ["Enabled"] = Square2.Enabled,
        ["Timer"] = Square2.Timer,
        ["Duty Position"] = Square2.Position,
        ["Env Divider"] = Square2.EnvDivider,
        ["Volume"] = Square2.EnvCounter,
    };

    public Dictionary<string, dynamic> GetChannel3() => new()
    {
        ["Length Counter"] = Triangle.LengthCounter,
        ["Linear Counter"] = Triangle.LinearCounter,
        ["Duty"] = Triangle.Duty,
        ["Env Volume"] = Triangle.EnvVolume,
        ["Frequency"] = Triangle.Frequency,
        ["Enabled"] = Triangle.Enabled,
        ["Timer"] = Triangle.Timer,
        ["Duty Position"] = Triangle.Position,
        ["Env Divider"] = Triangle.EnvDivider,
        ["Volume"] = Triangle.EnvCounter,
    };

    public Dictionary<string, dynamic> GetChannel4() => new()
    {
        ["Length Counter"] = Noise.LengthCounter,
        ["LSFR"] = $"{Noise.ShiftReg:X4}",
        ["Duty"] = Noise.Duty,
        ["Env Volume"] = Noise.EnvVolume,
        ["Frequency"] = Noise.Frequency,
        ["Enabled"] = Noise.Enabled,
        ["Timer"] = Noise.Timer,
        ["Duty Position"] = Noise.Position,
        ["Env Divider"] = Noise.EnvDivider,
        ["Volume"] = Noise.EnvCounter,
    };

    public Dictionary<string, dynamic> GetChannel5() => new()
    {
        ["Period"] = Dmc.Frequency,
        ["Loop"] = $"{Dmc.Loop}",
        //["Irq"] = Apu.Dmc.,
        ["Output"] = Dmc.OutputLevel,
        ["Sample Address"] = $"{Dmc.SampleAddress:X4}",
        ["Sample Length"] = $"{Dmc.SampleLength:X4}",
        ["Timer"] = Dmc.Timer,
    };
}