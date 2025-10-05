namespace Gmulator.Core.Gbc.Mappers;
public abstract class BaseMapper : EmuState
{
    public byte[] Rom { get; set; }
    public int Rombank { get; set; }
    public int Rambank { get; set; }
    public bool CartRamOn { get; set; }
    public bool CGB { get; set; }
    public int Ramsize { get; set; }
    public string Name { get; set; }
    public string MapperType { get; set; }
    public byte[] Sram { get; set; }
    private Timer Timer;

    public virtual void Reset()
    {
        Rombank = 1;
    }

    public virtual void Init(byte[] rom, string name)
    {
        Rom = rom;
        Sram = new byte[0x8000];
        Rombank = 1;
        CGB = (rom[0x143] & 0x80) != 0;
        Ramsize = rom[0x149];
        Name = name;
        if (MapperTypes.TryGetValue(rom[0x147], out var mapper))
            MapperType = mapper;
        else
            MapperType = "Unknown";
    }

    public virtual byte ReadRom(int a) => Rom[a % Rom.Length];
    public abstract Span<byte> ReadRomBlock(int a, int size);
    public abstract void WriteRom0(int a, byte v, bool edit = false);
    public abstract void WriteRom1(int a, byte v, bool edit = false);

    public virtual void Write()
    {
        //Sram[a % Sram.Length] = v;
        Timer ??= new Timer(SaveSram, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void LoadSram()
    {
        var name = Path.GetFileNameWithoutExtension($"{Name}");
        if (File.Exists($"{SaveDirectory}/{name}.srm"))
        {
            var data = File.ReadAllBytes($"{SaveDirectory}/{name}.srm");
            if (data.Length > 0)
                Sram = data;
        }
    }

    private void SaveSram(object state)
    {
        var name = Path.GetFileNameWithoutExtension($"{Name}");
        try
        {
            File.WriteAllBytes($"{SaveDirectory}/{name}.srm", Sram);
        }
        catch (IOException)
        {
            Timer?.Dispose();
            Timer = null;
        }
    }

    public Dictionary<string, string> GetInfo()
    {
        return new Dictionary<string, string>
        {
            { "Game",Path.GetFileName(Name) },
            { "Mapper", MapperType },
            { "ROM Size", $"{Rom.Length / 1024}KB" },
            { "RAM Size", $"{(Ramsize == 0 ? 0 : 512 << Ramsize)}KB" },
            { "ROM Bank", $"{Rombank}" },
            { "RAM Bank", $"{Rambank}" },
            { "CGB Mode", $"{(CGB ? "Yes" : "No")}" },
            { "Cart RAM", $"{(CartRamOn ? "On" : "Off")}" }
        };
    }

    private readonly Dictionary<int, string> MapperTypes = new()
    {
        [0x00] = "ROM ONLY",
        [0x01] = "MBC1",
        [0x02] = "MBC1RAM",
        [0x03] = "MBC1RAMBATTERY",
        [0x05] = "MBC2",
        [0x06] = "MBC2BATTERY",
        [0x08] = "ROMRAM 1",
        [0x09] = "ROMRAMBATTERY 1",
        [0x0B] = "MMM01",
        [0x0C] = "MMM01RAM",
        [0x0D] = "MMM01RAMBATTERY",
        [0x0F] = "MBC3TIMERBATTERY",
        [0x10] = "MBC3TIMERRAMBATTERY 2",
        [0x11] = "MBC3",
        [0x12] = "MBC3RAM 2",
        [0x13] = "MBC3RAMBATTERY 2",
        [0x19] = "MBC5",
        [0x1A] = "MBC5RAM",
        [0x1B] = "MBC5RAMBATTERY",
        [0x1C] = "MBC5RUMBLE",
        [0x1D] = "MBC5RUMBLERAM",
        [0x1E] = "MBC5RUMBLERAMBATTERY",
        [0x20] = "MBC6",
        [0x22] = "MBC7SENSORRUMBLERAMBATTERY",
        [0xFC] = "POCKET CAMERA",
        [0xFD] = "BANDAI TAMA5",
        [0xFE] = "HuC3",
        [0xFF] = "HuC1RAMBATTERY",
    };

    public override void Save(BinaryWriter bw)
    {
        bw.Write(Name);
        bw.Write(Ramsize);
        bw.Write(Rombank);
        bw.Write(Rambank);
        bw.Write(CartRamOn);
        bw.Write(CGB);
    }

    public override void Load(BinaryReader br)
    {
        Name = br.ReadString();
        Ramsize = br.ReadInt32();
        Rombank = br.ReadInt32();
        Rambank = br.ReadInt32();
        CartRamOn = br.ReadBoolean();
        CGB = br.ReadBoolean();
    }
}