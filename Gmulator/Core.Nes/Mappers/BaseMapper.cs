
namespace Gmulator.Core.Nes.Mappers;
public class BaseMapper(Header Header) : EmuState
{
    public byte[] Prg { get; set; }
    public byte[] Chr { get; set; }
    public byte[] LChr { get; set; } = [];
    public byte[] Prom { get; set; }
    public byte[] Vrom { get; set; }
    public byte[] Sram { get; set; }
    public int PrgMode { get; set; }
    public int ChrMode { get; set; }
    public int PrgSize { get; set; }
    public int ChrSize { get; set; }
    public bool SramEnabled { get; set; }
    public static bool Fire { get; set; }
    public int Counter { get; set; }
    public bool SpriteSize { get; set; }
    public string Name { get; set; } = Header.Name;
    private Timer Timer;

    public Header Header { get; set; } = Header;

    public int ReadSram(int a)
    {
        return Sram[a - 0x6000];
    }

    public void WriteSram(int a, int v)
    {
        Sram[a - 0x6000] = (byte)v;
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

    public virtual byte ReadPrg(int a) => Prom[a % Prom.Length];

    public virtual byte ReadChr(int a)
    {
        if (Vrom.Length == 0) return 0;
        return Vrom[a % Vrom.Length];
    }

    public virtual void WritePrg(int a, byte v) => Prom[a % Prom.Length] = v;

    public virtual void Write(int a, byte v)
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
        SramEnabled = false;
        Fire = false;
        SramEnabled = true;
    }

    public virtual void Scanline()
    {

    }

    public static void SetFire(bool v) => Fire = v;

    public override void Save(BinaryWriter bw)
    {
        bw.Write(Prg);
        bw.Write(Chr);
        bw.Write(LChr);
        bw.Write(PrgMode);
        bw.Write(ChrMode);
        bw.Write(PrgSize);
        bw.Write(ChrSize);
        bw.Write(SramEnabled);
        bw.Write(Fire);
        bw.Write(Counter);
    }

    public override void Load(BinaryReader br)
    {
        Prg = br.ReadBytes(Prg.Length);
        Chr = br.ReadBytes(Chr.Length);
        LChr = br.ReadBytes(LChr.Length);
        PrgMode = br.ReadInt32();
        ChrMode = br.ReadInt32();
        PrgSize = br.ReadInt32();
        ChrSize = br.ReadInt32();
        SramEnabled = br.ReadBoolean();
        Fire = br.ReadBoolean();
        Counter = br.ReadInt32();
    }
}
