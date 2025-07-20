namespace Gmulator.Core.Nes.Sound;
public class Dmc : BaseChannel
{
    public int OutputLevel { get; set; }
    public ushort OutputShift { get; private set; }
    public int RateIndex { get; private set; }
    public bool Irq { get; private set; }
    public bool Loop { get; private set; }
    public int SampleAddress { get; private set; }
    public int SampleLength { get; private set; }
    public int AddrValue { get; private set; }
    public int LengthValue { get; set; }
    public int SampleBuffer { get; private set; }
    private int OutputBits;
    public Func<int, byte> Read;

    private readonly int[] Rate =
    [
       428, 380, 340, 320, 286, 254, 226, 214,
       190, 160, 142, 128, 106,  84,  72,  54
    ];

    public void Write(int a, byte v)
    {
        if (a == 0x4010)
        {
            RateIndex = v & 0x0f;
            Irq = v.GetBit(7);
            Loop = v.GetBit(6);
            Frequency = (ushort)(Rate[v & 0x0f] / 2);
        }
        else if (a == 0x4011)
            OutputLevel = (byte)(v & 0x7f);
        else if (a == 0x4012)
            SampleAddress = 0xc000 + (v << 6);
        else if (a == 0x4013)
            SampleLength = (v << 4) + 1;
        else if (a == 0x4015)
        {
            if (Enabled)
            {
                if (LengthValue == 0)
                    Reload();
            }
            else
                LengthValue = 0;

            Irq = false;
        }
    }

    public override void Reset()
    {
        OutputBits = 0;
        OutputLevel = 0;
        base.Reset();
    }

    public byte GetSample() => (byte)OutputLevel;

    public void Step()
    {
        if (Enabled)
        {
            if (LengthValue > 0 && OutputBits == 0)
            {
                OutputBits = 8;
                OutputShift = Read(AddrValue);
                if (++AddrValue > 0xffff)
                    AddrValue = 0x8000;

                if (LengthValue == 0)
                {
                    if (Loop)
                        Reload();
                }
                else
                    LengthValue--;
            }

            if (Timer == 0)
            {
                Timer = Frequency;

                if (OutputBits > 0)
                {
                    if ((OutputShift & 1) == 1 && OutputLevel < 0x7e)
                        OutputLevel += 2;
                    else
                    {
                        if (OutputLevel > 0x01)
                            OutputLevel -= 2;
                    }
                    OutputShift >>= 1;
                    OutputBits--;
                }
            }
            else
                Timer--;
        }
    }

    public void Reload()
    {
        AddrValue = SampleAddress;
        LengthValue = SampleLength;
    }

    public static void Length()
    {

    }

    public static void Envelope()
    {

    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(OutputLevel);
        bw.Write(Frequency);
        bw.Write(Timer);
        bw.Write(RateIndex);
        bw.Write(Loop);
        bw.Write(SampleAddress);
        bw.Write(SampleLength);
    }

    public override void Load(BinaryReader br)
    {
        OutputLevel = (byte)br.ReadInt32();
        Frequency = br.ReadInt32();
        Timer = br.ReadInt32();
        RateIndex = br.ReadInt32();
        Loop = br.ReadBoolean();
        SampleAddress = br.ReadInt32();
        SampleLength = br.ReadInt32();
    }
}
