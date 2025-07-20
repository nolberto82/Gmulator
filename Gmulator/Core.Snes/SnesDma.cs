
namespace Gmulator.Core.Snes;
public class SnesDma : EmuState
{
    public bool Enabled { get; set; } = false;
    public bool HdmaEnabled { get; set; } = false;
    public bool Direction { get; set; } = true;
    public int Step { get; set; }
    public int Mode { get; set; } = 7;
    public int Port { get; set; }
    public int ABank { get; set; }
    public int AAddress { get => (ushort)field; set => field = (ushort)value; } = 0xffff;
    public int Size { get => (ushort)field; set => field = (ushort)value; } = 0xffff;
    public int BAddress { get => (byte)field; set => field = (byte)value; } = 0xff;
    public int RamAddress { get; set; }
    public int HBank { get => (byte)field; set => field = (byte)value; } = 0xff;
    public int HAddress { get => (ushort)field; set => field = (ushort)value; } = 0xffff;
    public int HCounter { get => (byte)field; set => field = (byte)value; } = 0xff;
    public bool Completed { get; set; }
    public bool Indirect { get; set; }
    public bool Repeat { get; set; }
    public bool TransferEnabled { get; set; }
    private readonly List<SnesDma> Dma;

    public SnesDma() { }
    public SnesDma(List<SnesDma> dma) => Dma = dma;

    public void SetSnes(Snes snes) => Snes = snes;

    public Func<int, byte> Read;
    public Action<int, int> Write;
    private Snes Snes;
    public readonly byte[] Max = [1, 2, 2, 4, 4, 4, 2, 4];
    public readonly byte[][] Offsets =
    [
        [0,0,0,0],[0,1,0,1],[0,0,0,0],[0,0,1,1],
        [0,1,2,3],[0,1,0,1],[0,0,0,0],[0,0,1,1]
    ];

    public bool Transfer(int src, int mode, int i)
    {
        var isfixed = Step == 1 || Step == 3;
        if (!Direction)
            Write(0x2100 | BAddress + Offsets[mode & 7][i], Read(src));
        else
            Write((src), Read(0x2100 | BAddress + Offsets[Mode][i]));

        return isfixed;
    }

    public static void Set(int a, byte v, ref List<SnesDma> Dma, int ramaddr)
    {
        var i = (a & 0xf0) / 0x10;
        if (i > 6)
        { }
        switch (a & 0x0f)
        {
            case 0x00:
                Dma[i].Direction = v.GetBit(7);
                Dma[i].Indirect = v.GetBit(6);
                Dma[i].Step = (v >> 3) & 3;
                Dma[i].Mode = v & 7;
                Dma[i].RamAddress = ramaddr; break;
            case 0x01: Dma[i].BAddress = v; break;
            case 0x02: Dma[i].AAddress = (Dma[i].AAddress & 0xff00) | v; break;
            case 0x03: Dma[i].AAddress = (Dma[i].AAddress & 0x00ff) | v << 8; break;
            case 0x04: Dma[i].ABank = v; break;
            case 0x05: Dma[i].Size = (ushort)((Dma[i].Size & 0xff00) | v); break;
            case 0x06: Dma[i].Size = (ushort)((Dma[i].Size & 0x00ff) | v << 8); break;
            case 0x07: Dma[i].HBank = v; break;
            case 0x08: Dma[i].HAddress = (Dma[i].HAddress & 0xff00) | v; break;
            case 0x09: Dma[i].HAddress = (Dma[i].HAddress & 0x00ff) | v << 8; break;
            case 0x0a:
                Dma[i].HCounter = v;
                Dma[i].Repeat = v.GetBit(7);
                break;
        }
    }

    public Dictionary<string, object> GetIoRegs() => new()
    {
        ["DMA Enabled"] = Enabled,
        ["HDMA Enabled"] = HdmaEnabled,
        ["Mode"] = $"{Mode:X2}",
        ["ABank"] = $"{ABank:X2}",
        ["AAddress"] = $"{AAddress:X4}",
        ["BAddress"] = $"{BAddress:X2}",
        ["Size"] = $"{Size:X4}",
        ["RamAddress"] = $"{RamAddress:X4}",
        ["Bank"] = $"{HBank:X2}",
        ["HAddress"] = $"{HAddress:X4}",
        ["LineCounter"] = $"{HCounter:X2}",
    };

    public override void Save(BinaryWriter bw)
    {
        for (int i = 0; i < 8; i++)
        {
            var d = Dma[i];
            bw.Write(Enabled); bw.Write(d.HdmaEnabled);
            bw.Write(d.Direction); bw.Write(d.Step);
            bw.Write(d.Mode); bw.Write(d.Port);
            bw.Write(d.ABank); bw.Write(d.AAddress);
            bw.Write(d.Size); bw.Write(d.BAddress);
            bw.Write(d.RamAddress); bw.Write(d.HBank);
            bw.Write(d.HAddress); bw.Write(d.HCounter);
            bw.Write(d.Completed); bw.Write(d.Indirect);
            bw.Write(d.Repeat); bw.Write(d.TransferEnabled);
        }
    }

    public override void Load(BinaryReader br)
    {
        for (int i = 0; i < 8; i++)
        {
            var d = Dma[i];
            Enabled = br.ReadBoolean(); d.HdmaEnabled = br.ReadBoolean();
            d.Direction = br.ReadBoolean(); d.Step = br.ReadInt32();
            d.Mode = br.ReadInt32(); d.Port = br.ReadInt32();
            d.ABank = br.ReadInt32(); d.AAddress = br.ReadInt32();
            d.Size = br.ReadInt32(); d.BAddress = br.ReadInt32();
            d.RamAddress = br.ReadInt32(); d.HBank = br.ReadInt32();
            d.HAddress = br.ReadInt32(); d.HCounter = br.ReadInt32();
            d.Completed = br.ReadBoolean(); d.Indirect = br.ReadBoolean();
            d.Repeat = br.ReadBoolean(); d.TransferEnabled = br.ReadBoolean();
        }
    }
}
