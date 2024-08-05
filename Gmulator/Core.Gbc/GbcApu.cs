using GBoy.Core.Sound;
using Gmulator.Shared;
using Raylib_cs;
using Wave = GBoy.Core.Sound.Wave;

namespace GBoy.Core;
public class GbcApu : SaveState
{
    public byte NR50 { get; private set; }
    public byte NR51 { get; private set; }
    public byte NR52 { get; private set; }

    public int FrameCycles { get; private set; }
    public int FrameSequencer { get; private set; }
    public int FrameSequencerCycles { get; private set; }

    public static int NextSampleTimer { get; private set; }
    public static int BufPos { get; set; }

    public static int VolumeLeft;
    public static int VolumeRight;

    public Square1 Square1 { get; private set; }
    public Square2 Square2 { get; private set; }
    public Wave Wave { get; private set; }
    public Noise Noise { get; private set; }

    public float[] AudioBuffer { get; private set; } = new float[MaxSamples * 2];

    public const int MaxSamples = 4096;
    public const int SampleRate = 44100;
    public readonly int SamplesCpu;

    public GbcApu(GbcMmu mmu, int cpuclock)
    {
        SamplesCpu = cpuclock / SampleRate;
        Square1 = new();
        Square2 = new();
        Wave = new(mmu);
        Noise = new();
    }

    public void Step(int cycles)
    {
        if (!NR52.GetBit(7)) return;
        Square1.Step(1, cycles);
        Square2.Step(2, cycles);
        Wave.Step(3, cycles);
        Noise.Step(4, cycles);

        FrameSequencerCycles -= cycles;
        if (FrameSequencerCycles == 0)
        {
            switch (FrameSequencer)
            {
                case 0 or 2 or 4 or 6:
                    if (FrameSequencer == 2 || FrameSequencer == 6)
                        Square1.Sweep();
                    Square1.Length();
                    Square2.Length();
                    Wave.Length();
                    Noise.Length();
                    break;
                case 7:
                    Square1.Envelope();
                    Square2.Envelope();
                    Noise.Envelope();
                    break;
            }
            FrameSequencerCycles = 8192;
            if (++FrameSequencer == 8)
                FrameSequencer = 0;
        }

        NextSampleTimer -= cycles;
        if (NextSampleTimer <= 0)
        {
            NextSampleTimer = SamplesCpu;

            float l = 0, r = 0;

            if (Square1.Enabled && Square1.Play)
            {
                l = Square1.LeftOn ? Square1.GetSample(1) * (VolumeLeft + 1) / 7.5f : 0;
                r = Square1.RightOn ? Square1.GetSample(1) * (VolumeRight + 1) / 7.5f : 0;
            }

            if (Square2.Enabled && Square2.Play)
            {
                l += Square2.LeftOn ? Square2.GetSample(2) * (VolumeLeft + 1) / 7.5f : 0;
                r += Square2.RightOn ? Square2.GetSample(2) * (VolumeRight + 1) / 7.5f : 0;
            }

            if (Wave.Enabled && Wave.Play)
            {
                l += Wave.LeftOn ? Wave.GetSample(3) * (VolumeLeft + 1) / 7.5f : 0;
                r += Wave.RightOn ? Wave.GetSample(3) * (VolumeRight + 1) / 7.5f : 0;
            }

            if (Noise.Enabled && Noise.Play)
            {
                l += Noise.LeftOn ? Noise.GetSample(4) * (VolumeLeft + 1) / 7.5f : 0;
                r += Noise.RightOn ? Noise.GetSample(4) * (VolumeRight + 1) / 7.5f : 0;
            }

            if (Raylib.IsWindowResized())
            {
                AudioBuffer[BufPos++] = 0;
                AudioBuffer[BufPos++] = 0;
            }
            else
            {
                AudioBuffer[BufPos++] = l / 8;
                AudioBuffer[BufPos++] = r / 8;
            }

            if (BufPos >= AudioBuffer.Length)
            {
                Audio.Update(AudioBuffer);
                BufPos = 0;
            }
        }
    }

    public byte Read(int a)
    {
        if (a <= 0x14)
            return Square1.Read(a);
        else if (a <= 0x19)
            return Square2.Read(a);
        else if (a <= 0x1e)
            return Wave.Read(a);
        else if (a <= 0x23)
            return Noise.Read(a);
        else if (a == 0x24)
            return NR50;
        else if (a == 0x25)
            return NR51;
        else if (a == 0x26)
            return (byte)(NR52 | 0x70);

        return 0xff;
    }

    public void Write(int a, byte v)
    {
        if (NR52 > 0)
        {
            if (a >= 0x10 && a <= 0x14)
                Square1.Write(a, v);
            else if (a >= 16 && a <= 0x19)
                Square2.Write(a, v);
            else if (a >= 0x1a && a <= 0x1f)
                Wave.Write(a, v);
            else if (a >= 0x20 && a <= 0x23)
                Noise.Write(a, v);
            else if (a == 0x24)
            {
                VolumeRight = (v & 0x07);
                VolumeLeft = ((v & 0x70) >> 4);
                NR50 = v;
            }
            else if (a == 0x25)
            {
                Square1.RightOn = v.GetBit(0);
                Square2.RightOn = v.GetBit(1);
                Wave.RightOn = v.GetBit(2);
                Noise.RightOn = v.GetBit(3);
                Square1.LeftOn = v.GetBit(4);
                Square2.LeftOn = v.GetBit(5);
                Wave.LeftOn = v.GetBit(6);
                Noise.LeftOn = v.GetBit(7);
                NR51 = v;
            }
            else if (a == 0x26)
            {
                NR52 = (byte)(v & 0x80);

                if (!v.GetBit(7))
                    Reset();
            }
        }
        if (a >= 0x30 && a <= 0x3f)
            Wave.WriteWaveRam(a, v);

    }

    public void Reset()
    {
        Square1.Reset();
        Square2.Reset();
        Wave.Reset();
        Noise.Reset();
        NR50 = NR51 = 0x00; NR52 = 0x70;
        FrameSequencerCycles = 8192;
        //Array.Fill<short>(AudioBuffer, 0);
    }

    public static byte[] GetSamples(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var s = (short)(samples[i] * short.MaxValue);
            bytes[i * 2] = (byte)((byte)s & 0xff);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return bytes;
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(NR50);
        bw.Write(NR51);
        bw.Write(NR52);
        bw.Write(FrameSequencerCycles);
        bw.Write(FrameSequencer);
    }

    public override void Load(BinaryReader br)
    {
        NR50 = br.ReadByte();
        NR51 = br.ReadByte();
        NR52 = br.ReadByte();
        FrameSequencerCycles = br.ReadInt32();
        FrameSequencer = br.ReadInt32();
    }
}
