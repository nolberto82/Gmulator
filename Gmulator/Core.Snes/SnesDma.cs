namespace Gmulator.Core.Snes;
public class SnesDma : EmuState
{
    private const int MaxChannels = 8;
    private bool[] Enabled = new bool[MaxChannels];
    private bool[] HdmaEnabled = new bool[MaxChannels];
    private bool[] Direction = new bool[MaxChannels];// = true;
    private int[] Step = new int[MaxChannels];
    private int[] Mode = new int[MaxChannels];// 7;
    private int[] Port = new int[MaxChannels];
    private int[] ABank = new int[MaxChannels];
    private int[] AAddress = new int[MaxChannels];// = 0xffff;
    private int[] Size = new int[MaxChannels];// { get => (ushort)field; set => field = (ushort)value; } = 0xffff;
    private int[] BAddress = new int[MaxChannels];// { get => (byte)field; set => field = (byte)value; } = 0xff;
    private int[] RamAddress = new int[MaxChannels];// { get; set; }
    private int[] HBank = new int[MaxChannels];// = 0xff;
    private int[] HAddress = new int[MaxChannels];// { get => (ushort)field; set => field = (ushort)value; } = 0xffff;
    private int[] HCounter = new int[MaxChannels];//= 0xff;
    private bool[] Completed = new bool[MaxChannels];
    private bool[] Indirect = new bool[MaxChannels];
    private bool[] Repeat = new bool[MaxChannels];
    private bool[] TransferEnabled = new bool[MaxChannels];

    private Action Idle8;

    public SnesDma(Snes snes)
    {
        Idle8 = snes.Cpu.Idle8;
        ReadCpu = snes.ReadMemory;
        WriteCpu = snes.WriteMemory;
    }

    public void Reset()
    {
        Array.Fill(Enabled, false);
        Array.Fill(HdmaEnabled, false);
        Array.Fill(Direction, true);
        Array.Fill(Step, 0);
        Array.Fill(Mode, 7);
        Array.Fill(Port, 0);
        Array.Fill(ABank, 0);
        Array.Fill(AAddress, 0xffff);
        Array.Fill(Size, 0xffff);
        Array.Fill(BAddress, 0xff);
        Array.Fill(RamAddress, 0);
        Array.Fill(HBank, 0xff);
        Array.Fill(HAddress, 0xffff);
        Array.Fill(HCounter, 0xff);
        Array.Fill(Completed, false);
        Array.Fill(Indirect, false);
        Array.Fill(Repeat, false);
        Array.Fill(TransferEnabled, false);
    }

    public Func<int, int> ReadCpu;
    public Action<int, int> WriteCpu;
    private bool DmaEnabled;
    private int DmaState;
    public readonly byte[] Max = [1, 2, 2, 4, 4, 4, 2, 4];
    private readonly byte[][] Offsets =
    [
        [0,0,0,0],[0,1,0,1],[0,0,0,0],[0,0,1,1],
        [0,1,2,3],[0,1,0,1],[0,0,0,0],[0,0,1,1]
    ];

    public bool Transfer(int src, int mode, int count, int c)
    {
        var isfixed = Step[c] == 1 || Step[c] == 3;
        int offset = Offsets[mode & 7][count];

        if (!Direction[c])
        {
            WriteCpu(0x2100 | (BAddress[c] + offset), ReadCpu(src));
        }
        else
        {
            int offset2 = Offsets[Mode[c] & 7][count];
            WriteCpu(src, ReadCpu(0x2100 | (BAddress[c] + offset2)));
        }

        return isfixed;
    }

    public void HandleDma()
    {
        if (!DmaEnabled) return;
        if (DmaState == 1)
        {
            DmaState = 2;
            return;
        }

        Idle8();
        for (int i = 0; i < MaxChannels; i++)
        {
            if (Enabled[i])
            {
                var src = ABank[i] << 16 | AAddress[i];
                int count = 0;
                do
                {
                    if (!Transfer(src, Mode[i], count, i))
                    {
                        if (Step[i] == 0)
                            src++;
                        else if (Step[i] == 2)
                            src--;
                    }
                    Size[i] = (Size[i] - 1) & 0xffff;
                    count = (count + 1) & 3;
                } while (Size[i] != 0);
                AAddress[i] = (ushort)src;
                Enabled[i] = false;
            }
        }
        DmaEnabled = false;
        DmaState = 0;
    }

    public void HandleHdma()
    {
        for (int i = 0; i < MaxChannels; i++)
        {
            if (HdmaEnabled[i] && !Completed[i])
            {
                if (TransferEnabled[i])
                {
                    int max = Max[Mode[i] & 7];
                    if (Indirect[i])
                    {
                        int size = Size[i];
                        for (int count = 0; count < max; count++)
                        {
                            Transfer(HBank[i] << 16 | size, Mode[i], count, i);
                            size++;
                        }
                        Size[i] = size & 0xffff;
                    }
                    else
                    {
                        int hAddress = HAddress[i];
                        for (int count = 0; count < max; count++)
                        {
                            Transfer(ABank[i] << 16 | hAddress, Mode[i], count, i);
                            hAddress++;
                        }
                        HAddress[i] = hAddress;
                    }
                }

                HCounter[i]--;
                TransferEnabled[i] = (HCounter[i] & 0x80) != 0;
                if ((HCounter[i] & 0x7f) == 0)
                {
                    int v = ReadCpu(ABank[i] << 16 | HAddress[i]++);
                    HCounter[i] = v;
                    Repeat[i] = (v & 0x80) != 0;
                    var bank = ABank[i] << 16;
                    if (Indirect[i])
                    {
                        int low = ReadCpu(bank | HAddress[i]++);
                        int high = ReadCpu(bank | HAddress[i]++);
                        Size[i] = (ushort)(low | (high << 8));
                    }

                    TransferEnabled[i] = true;
                    if (HCounter[i] == 0)
                        Completed[i] = true;
                }
            }
        }
    }

    public void InitHdma()
    {
        for (int i = 0; i < MaxChannels; i++)
        {
            if (HdmaEnabled[i])
            {
                Completed[i] = false;
                HAddress[i] = AAddress[i];
                int v = ReadCpu(ABank[i] << 16 | HAddress[i]);
                HAddress[i]++;
                HCounter[i] = v;
                Repeat[i] = (v & 0x80) != 0;
                if (Indirect[i])
                {
                    int addr = ABank[i] << 16 | HAddress[i];
                    int low = ReadCpu(addr);
                    int high = ReadCpu(addr + 1);
                    Size[i] = (ushort)(low | (high << 8));
                    HAddress[i] += 2;
                }
                TransferEnabled[i] = true;
            }
            else
            {
                TransferEnabled[i] = false;
            }
        }
    }

    public int Read(int a)
    {
        var i = (a & 0xf0) / 0x10;
        switch (a & 0x0f)
        {
            case 0x00:
                return ((Direction[i] ? 0x80 : 0x00) |
                Step[i] << 3 |
                Mode[i] & 7 |
                RamAddress[i]) & 0xff;
            case 0x01: return BAddress[i] & 0xff;
            case 0x02: return AAddress[i] & 0xff;
            case 0x03: return (AAddress[i] >> 8) & 0xff;
            case 0x04: return (AAddress[i] >> 16) & 0xff;
            case 0x05: return Size[i] & 0xff;
            case 0x06: return (Size[i] >> 8) & 0xff;
            case 0x07: break;
        }
        return 0;
    }

    public void Write(int a, byte v, int ramaddr)
    {
        var i = (a & 0xf0) / 0x10;

        switch (a & 0x0f)
        {
            case 0x00:
                Direction[i] = (v & 0x80) != 0;
                Indirect[i] = (v & 0x40) != 0;
                Step[i] = (v >> 3) & 3;
                Mode[i] = v & 7;
                RamAddress[i] = ramaddr; break;
            case 0x01: BAddress[i] = v; break;
            case 0x02: AAddress[i] = (AAddress[i] & 0xff00) | v; break;
            case 0x03: AAddress[i] = (AAddress[i] & 0x00ff) | v << 8; break;
            case 0x04: ABank[i] = v; break;
            case 0x05: Size[i] = (ushort)((Size[i] & 0xff00) | v); break;
            case 0x06: Size[i] = (ushort)((Size[i] & 0x00ff) | v << 8); break;
            case 0x07: HBank[i] = v; break;
            case 0x08: HAddress[i] = (HAddress[i] & 0xff00) | v; break;
            case 0x09: HAddress[i] = (HAddress[i] & 0x00ff) | v << 8; break;
            case 0x0a:
                HCounter[i] = v;
                Repeat[i] = (v & 0x80) != 0;
                break;
        }
    }

    public void StatusDma(int v)
    {
        DmaEnabled = v != 0;
        DmaState = DmaEnabled ? 1 : 0;
        for (int i = 0; i < 8; i++)
            Enabled[i] = ((v >> i) & 1) != 0;
    }

    public void StatusHdma(int v)
    {
        for (int i = 0; i < MaxChannels; i++)
            HdmaEnabled[i] = ((v >> i) & 1) != 0;
    }

    public List<RegisterInfo> GetIoRegs(int i) =>
    [
        new($"420B.{i}","DMA Enabled",$"{Enabled[i]}"),
        new($"420C.{i}","HDMA Enabled",$"{HdmaEnabled[i]}"),
        new($"43{i * 16:X2}.0-2","Mode",$"{Mode[i]:X2}"),
        new($"43{i * 16:X2}.3","Fixed",$"{Step[i] == 1 || Step[i] == 3:X2}"),
        new($"43{i * 16:X2}.6","Indirect",$"{Indirect[i]}"),
        new($"43{i * 16:X2}.7","Direction",$"{Direction[i]}"),
        new($"43{i * 16 + 1:X2}","BAddress",$"{BAddress[i]:X2}"),
        new($"43{i * 16 + 2:X2}/3","AAddress",$"{AAddress[i]:X4}"),
        new($"43{i * 16 + 4:X2}","ABank",$"{ABank[i]}"),
        new($"43{i * 16 + 5:X2}/6","Size",$"{Size[i]:X4}"),
        new($"43{i * 16 + 7:X2}","Bank",$"{HBank[i]:X2}"),
        new($"43{i * 16 + 8:X2}/9","HAddress",$"{HAddress[i]:X4}"),
        new($"43{i * 16 + 10:X2}","LineCounter",$"{HCounter[i]:X2}"),
        new($"","RamAddress",$"{RamAddress[i]:X4}"),
    ];

    public override void Save(BinaryWriter bw)
    {
        for (int i = 0; i < 8; i++)
        {
            bw.Write(Enabled[i]); bw.Write(HdmaEnabled[i]);
            bw.Write(Direction[i]); bw.Write(Step[i]);
            bw.Write(Mode[i]); bw.Write(Port[i]);
            bw.Write(ABank[i]); bw.Write(AAddress[i]);
            bw.Write(Size[i]); bw.Write(BAddress[i]);
            bw.Write(RamAddress[i]); bw.Write(HBank[i]);
            bw.Write(HAddress[i]); bw.Write(HCounter[i]);
            bw.Write(Completed[i]); bw.Write(Indirect[i]);
            bw.Write(Repeat[i]); bw.Write(TransferEnabled[i]);
        }
    }

    public override void Load(BinaryReader br)
    {
        for (int i = 0; i < 8; i++)
        {
            Enabled[i] = br.ReadBoolean(); HdmaEnabled[i] = br.ReadBoolean();
            Direction[i] = br.ReadBoolean(); Step[i] = br.ReadInt32();
            Mode[i] = br.ReadInt32(); Port[i] = br.ReadInt32();
            ABank[i] = br.ReadInt32(); AAddress[i] = br.ReadInt32();
            Size[i] = br.ReadInt32(); BAddress[i] = br.ReadInt32();
            RamAddress[i] = br.ReadInt32(); HBank[i] = br.ReadInt32();
            HAddress[i] = br.ReadInt32(); HCounter[i] = br.ReadInt32();
            Completed[i] = br.ReadBoolean(); Indirect[i] = br.ReadBoolean();
            Repeat[i] = br.ReadBoolean(); TransferEnabled[i] = br.ReadBoolean();
        }
    }
}
