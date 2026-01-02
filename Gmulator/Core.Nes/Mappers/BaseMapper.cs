using Gmulator.Interfaces;

namespace Gmulator.Core.Nes.Mappers;

public class BaseMapper : ISaveState
{
    public int[] Prg { get; set; }
    public int[] Chr { get; set; }
    public int[] LChr { get; set; } = [];
    public byte[] PrgRom { get; set; }
    public byte[] CharRom { get; set; }
    public byte[] Sram { get; set; }
    public int PrgMode { get; set; }
    public int ChrMode { get; set; }
    public int PrgSize { get; set; }
    public int ChrSize { get; set; }
    public bool SramEnabled { get; set; }
    public static bool Fire { get; set; }
    public int Counter { get; set; }
    public bool SpriteSize { get; set; }
    public string Name { get; set; }
    private Timer Timer;

    public BaseMapper(Header header, NesMmu mmu)
    {
        Name = header.Name;
        SramEnabled = header.Battery;
        Header = header;
        Mmu = mmu;

        PrgSize = Header.PrgBanks * 0x4000;
        ChrSize = Header.ChrBanks * 0x2000;
        PrgRom = Header.PrgRom;
        CharRom = Header.CharRom;
        Sram = new byte[0x2000];
    }

    public Header Header { get; set; }

    public NesMmu Mmu { get; set; }

    public int ReadSram(int a)
    {
        if (!SramEnabled) return 0;
        return Sram[a & 0x1fff];
    }

    public void WriteSram(int a, int v)
    {
        if (!SramEnabled) return;
        Sram[a & 0x1fff] = (byte)v;
        Timer ??= new Timer(SaveSram, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void SaveSram(object state)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension($"{Name}");
            File.WriteAllBytes($"{SaveDirectory}/{name}.sav", Sram);
        }
        catch (IOException)
        {
            Timer?.Dispose();
            Timer = null;
        }
    }

    public virtual int ReadPrg(int a) => PrgRom[a % PrgRom.Length];

    public virtual int ReadChr(int a)
    {
        if (CharRom.Length == 0) return 0;
        return CharRom[a % CharRom.Length];
    }

    public virtual void WritePrg(int a, int v) => PrgRom[a % PrgRom.Length] = (byte)v;

    public virtual void Write(int a, int v)
    {

    }

    public virtual byte ReadVram(int a) => Header.Mmu.Vram[a % 0x4000];

    public virtual void SetLatch(int a, byte v)
    {

    }

    public virtual void Reset()
    {
        PrgMode = 0;
        ChrMode = 0;
        Counter = 0;
        Fire = false;
    }

    public virtual void Scanline()
    {

    }

    public static void SetFire(bool v) => Fire = v;

    public Dictionary<string, string> GetInfo()
    {
        return new Dictionary<string, string>
        {
            { "Game",Path.GetFileName(Name) },
            { "Mapper", $"{Header.MapperId:X3}" },
            { "Prg Size", $"{PrgSize}" },
            { "Chr Size", $"{ChrSize}" },
        };
    }

    public void Save(BinaryWriter bw)
    {
        WriteArray(bw, Prg); WriteArray(bw, Chr); WriteArray(bw, LChr); WriteArray(bw, PrgRom);
        WriteArray(bw, CharRom); WriteArray(bw, Sram); bw.Write(PrgMode); bw.Write(ChrMode);
        bw.Write(PrgSize); bw.Write(ChrSize); bw.Write(SramEnabled); bw.Write(Fire);
        bw.Write(Counter); bw.Write(SpriteSize);
    }

    public void Load(BinaryReader br)
    {
        Prg = ReadArray<int>(br, Prg.Length); Chr = ReadArray<int>(br, Chr.Length); LChr = ReadArray<int>(br, LChr.Length); PrgRom = ReadArray<byte>(br, PrgRom.Length);
        CharRom = ReadArray<byte>(br, CharRom.Length); Sram = ReadArray<byte>(br, Sram.Length); PrgMode = br.ReadInt32(); ChrMode = br.ReadInt32();
        PrgSize = br.ReadInt32(); ChrSize = br.ReadInt32(); SramEnabled = br.ReadBoolean(); Fire = br.ReadBoolean();
        Counter = br.ReadInt32(); SpriteSize = br.ReadBoolean();
    }
}
