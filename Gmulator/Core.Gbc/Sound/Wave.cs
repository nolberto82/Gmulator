
namespace GBoy.Core.Sound;
public class Wave : BaseChannel
{
    public byte NR30 { get; private set; }
    public byte NR31 { get; private set; }
    public byte NR32 { get; private set; }
    public byte NR33 { get; private set; }
    public byte NR34 { get; private set; }

    private byte[] WaveRamReset =
    [
        0x84, 0x40, 0x43, 0xaa, 0x2d, 0x78, 0x92, 0x3c,
        0x60, 0x59, 0x59, 0xb0, 0x34, 0xb8, 0x2e, 0xda
    ];

    private GbcMmu Mmu;

    public Wave() { }
    public Wave(GbcMmu mmu) => Mmu = mmu;

    public override void Reset()
    {
        Frequency = 0;
        LengthCounter = 0;
        Duty = 0;
        EnvVolume = 0;
        Timer = 0;
        CurrentVolume = 0;
        Sample = 0;
        Position = 0;

        NR30 = 0x7f;
        NR31 = 0xff;
        NR32 = 0x9f;
        NR33 = 0xff;
        NR34 = 0xbf;

        Dac = false;
        Enabled = false;

        WaveRam = new byte[16];
        if (Mmu.Mapper.CGB)
            Buffer.BlockCopy(WaveRamReset, 0, WaveRam, 0, WaveRamReset.Length);
        else
        {
            Random r = new(Guid.NewGuid().GetHashCode());
            r.NextBytes(WaveRam);
        }
    }

    public override void Write(int a, byte v)
    {
        if (a == 0x1a)
        {
            Dac = v.GetBit(7);
            NR30 = v;
        }
        else if (a == 0x1b)
        {
            LengthCounter = 256 - v;
            NR31 = v;
        }
        else if (a == 0x1c)
        {
            VolumeShift = ((v & 0x60) >> 5) & 3;
            NR32 = v;
        }
        else if (a == 0x1d)
        {
            Frequency = Frequency & 0xff00 | v;
            NR33 = v;
        }
        else if (a == 0x1e)
        {
            if (Dac)
            {
                Frequency = (Frequency & 0xff) | (v & 0x07) << 8;
                LengthEnabled = v.GetBit(6);
                if (v.GetBit(7))
                    Trigger(256, 2);
            }
            NR34 = v;
        }
    }

    public byte ReadWaveRam(int a) => WaveRam[a & 0x0f];
    public void WriteWaveRam(int a, byte v)
    {
        WaveRam[a & 0x0f] = v;
        Mmu.Ram[0xff00 + a] = v;
    }

    public override byte Read(int a)
    {
        if (a == 0x1a)
            return (byte)(NR30 | 0x7f);
        else if (a == 0x1b)
            return (byte)(NR31 | 0xff);
        else if (a == 0x1c)
            return (byte)(NR32 | 0x9f);
        else if (a == 0x1d)
            return (byte)(NR33 | 0xff);
        else if (a == 0x1e)
            return (byte)(NR34 | 0xbf);
        return 0xff;
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(Frequency);
        bw.Write(LengthCounter);
        bw.Write(Duty);
        bw.Write(EnvVolume);
        bw.Write(VolumeShift);
        bw.Write(Timer);
        bw.Write(WaveRam);
        bw.Write(NR30);
        bw.Write(NR31);
        bw.Write(NR32);
        bw.Write(NR33);
        bw.Write(NR34);
    }

    public override void Load(BinaryReader br)
    {
        Frequency = br.ReadInt32();
        LengthCounter = br.ReadInt32();
        Duty = br.ReadInt32();
        EnvVolume = br.ReadInt32();
        VolumeShift = br.ReadInt32();
        Timer = br.ReadInt32();
        WaveRam = br.ReadBytes(16);
        NR30 = br.ReadByte();
        NR31 = br.ReadByte();
        NR32 = br.ReadByte();
        NR33 = br.ReadByte();
        NR34 = br.ReadByte();

        Dac = NR30.GetBit(7);
    }
}
