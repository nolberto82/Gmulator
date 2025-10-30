using Gmulator;
using Gmulator.Core.Nes.Sound;
using Raylib_cs;

namespace Gmulator.Core.Nes;

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
                        if ((FrameCounter & 0x80) == 0)
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
                        if ((FrameCounter & 0x80) == 0)
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

    public int Read(int a)
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
            return res & 0xff;
        }

        return 0xff;
    }

    public void Write(int a, int v)
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
            Square1.Enabled = (v & 0x01) != 0;
            Square2.Enabled = (v & 0x02) != 0;
            Triangle.Enabled = (v & 0x04) != 0;
            Noise.Enabled = (v & 0x08) != 0;
            Dmc.Enabled = (v & 0x10) != 0;

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

            IrqEnabled = v == 0 && !((v & 0x40) == 0) && !((v & 0x80) == 0);

            if ((v & 0x80) != 0)
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

    public List<RegisterInfo> GetState() => new()
    {
        new("4000","Channel 1",""),
        new("0-3","Env Volume",$"{Square1.EnvVolume:X2}"),
        new("4","Env Constant",$"{Square1.ConstVolumeFlag}"),
        new("5","Length Halted",$"{!Square1.LengthEnabled}"),
        new("6-7","Duty",$"{Square1.Position:X2}"),
        new("4001","",""),
        new("0-2","Sweep Shift",$"{Square1.SweepShift:X2}"),
        new("3","Sweep Negate",$"{Square1.SweepNegate:X2}"),
        new("4-6","Sweep Period",$"{Square1.SweepPeriod:X2}"),
        new("7","Sweep Enabled",$"{Square1.SweepEnabled}"),
        new("4002/3","",""),
        new("7","Period",$"{Square1.Frequency:X4}"),
        new("4003","",""),
        new("3-7","Length Counter",$"{Square1.LengthCounter:X4}"),
        new("","Enabled",$"{Square1.Enabled}"),
        new("","Timer",$"{Square1.Timer:X4}"),
        new("","Env Divider",$"{Square1.EnvDivider:X2}"),
        new("","Volume",$"{Square1.EnvCounter:X2}"),
        new("4004","Channel 2",""),
        new("0-3","Env Volume",$"{Square2.EnvVolume:X2}"),
        new("4","Env Constant",$"{Square2.ConstVolumeFlag}"),
        new("5","Length Halted",$"{!Square2.LengthEnabled}"),
        new("6-7","Duty",$"{Square2.Position:X2}"),
        new("4005","",""),
        new("0-2","Sweep Shift",$"{Square2.SweepShift:X2}"),
        new("3","Sweep Negate",$"{Square2.SweepNegate:X2}"),
        new("4-6","Sweep Period",$"{Square2.SweepPeriod:X2}"),
        new("7","Sweep Enabled",$"{Square2.SweepEnabled}"),
        new("4006/7","",""),
        new("0-2","Period",$"{Square2.Frequency:X4}"),
        new("4007","",""),
        new("3-7","Length Counter",$"{Square2.LengthCounter:X4}"),
        new("","Enabled",$"{Square2.Enabled}"),
        new("","Timer",$"{Square2.Timer:X4}"),
        new("","Env Divider",$"{Square2.EnvDivider:X2}"),
        new("","Volume",$"{Square2.EnvCounter:X2}"),
        new("4008","Channel 3",""),
        new("0-6","Linear Counter",$"{Triangle.LinearCounter:X2}"),
        new("7","Length Halted",$"{Triangle.LengthEnabled}"),
        new("400A/B","",""),
        new("0-2","Period",$"{Triangle.Frequency:X4}"),
        new("400B","",""),
        new("3-7","Length Reload",$"{Triangle.LengthCounter:X4}"),
        new("","Frequency",$"{Triangle.Frequency:X4}"),
        new("","Enabled",$"{Triangle.Enabled}"),
        new("","Timer",$"{Triangle.Timer:X4}"),
        new("","Duty Position",$"{Triangle.Position:X2}"),
        new("","Env Divider",$"{Triangle.EnvDivider:X2}"),
        new("","Volume",$"{Triangle.EnvCounter:X2}"),
        new("400C","Noise",""),
        new("0-3","Env Volume",$"{Noise.EnvVolume:X2}"),
        new("4","Env Constant Volume",$"{Noise.ConstVolumeFlag}"),
        new("5","Length Counter",$"{Noise.LengthCounter:X2}"),
        new("400E","",""),
        new("0-3","Env Volume",$"{Noise.PeriodTimer:X4}"),
        new("7","Env Constant Volume",$"{Noise.Mode}"),
        new("400F","",""),
        new("3-7","Length Counter",$"{Noise.LengthCounter:X2}"),
        new("","LSFR",$"{$"{Noise.ShiftReg:X4}"}"),
        new("","Frequency",$"{Noise.Frequency:X4}"),
        new("","Enabled",$"{Noise.Enabled}"),
        new("","Timer",$"{Noise.Timer:X4}"),
        new("","Duty Position",$"{Noise.Position:X2}"),
        new("","Env Divider",$"{Noise.EnvDivider:X2}"),
        new("","Volume",$"{Noise.EnvCounter:X2}"),
        new("4010","Dmc",""),
        new("0-3","Period",$"{Dmc.Frequency:X4}"),
        new("6","Loop",$"{$"{Dmc.Loop}"}"),
        new("7","Irq",$"{$"{Dmc.Irq}"}"),
        new("4011","",""),
        new("","Output",$"{Dmc.OutputLevel:X2}"),
        new("4012","",""),
        new("","Sample Address",$"{$"{Dmc.SampleAddress:X4}"}"),
        new("4013","",""),
        new("","Sample Length",$"{$"{Dmc.SampleLength:X4}"}"),
        new("","Timer",$"{Dmc.Timer:X4}"),

    };
}