using Timer = System.Threading.Timer;

namespace Gmulator.Core.Snes.Mappers;
public class BaseMapper
{
    public string Name { get; set; }
    public int Mode { get; set; }
    public int Map { get; set; }
    public bool Speed { get; set; }
    public bool SramEnabled { get; set; }
    public byte[] Rom { get; set; }
    public int Banks { get; set; }
    public int Romsize { get; set; }
    public int Ramsize { get; set; }
    private Timer Timer;

    public byte[] Sram { get; set; }

    public BaseMapper()
    {
    }

    public BaseMapper(Header header)
    {
        Init(header);
    }

    public virtual void Init(Header header)
    {
        Name = header.Name;
        Map = header.Map;
        Mode = header.Mode;
        Speed = header.Speed;
        SramEnabled = header.Sram;
        Sram = new byte[header.Ramsize];
        Rom = header.Rom;
        Banks = header.Rom.Length / 0x8000;
    }

    public virtual byte Read(int bank, int a)
    {
        return Rom[a % Rom.Length];
    }

    public virtual void Write(int bank, int a, int v)
    {
        Sram[a % Sram.Length] = (byte)v;
        Timer ??= new Timer(SaveSram, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public byte ReadBwRam(int a)
    {
        return Sram[a % Sram.Length];
    }

    public void WriteBwRam(int a, byte v)
    {
        Sram[a % Sram.Length] = (byte)v;
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

    public void SaveSram()
    {
        var name = Path.GetFileNameWithoutExtension($"{Name}");
        File.WriteAllBytes($"{SaveDirectory}/{name}.srm", Sram);
    }

    private readonly int[] offsets = [0x00000, 0x000200, 0x008000, 0x008200, 0x408000, 0x408200];

    public BaseMapper LoadRom(string name)
    {
        BaseMapper Mapper;
        return Mapper = Set(File.ReadAllBytes(name), name);
    }

    public BaseMapper Set(byte[] rom, string name)
    {
        int highscore = -1;
        BaseMapper Mapper = null;
        for (int i = 0; i < offsets.Length; i++)
        {
            int score = -1;
            int o = offsets[i];
            if (rom.Length < o + 0x7fff)
                continue;

            Header header = new()
            {
                Rom = [.. rom.Skip(rom.Length % 1024)],
                Name = name,
                Map = rom[o + 0x7fd5] & 0xef,
                Speed = rom[o + 0x7fd5].GetBit(4),
                Sram = rom[o + 0x7fd6] > 0,
                Romsize = 0x400 << rom[o + 0x7fd7],
                Ramsize = 0x400 << rom[o + 0x7fd8]
            };

            var map = header.Map & 0x37;

            var reset = rom[o + 0x7ffc] | rom[o + 0x7ffd] << 8;
            if (reset < 0x8000)
                continue;

            var complement = (rom[o + 0x7fdc] | rom[o + 0x7fdd] << 8);
            var checksum = rom[o + 0x7fde] | rom[o + 0x7fdf] << 8;
            if (complement + checksum == 0xffff && complement != 0 && checksum != 0)
                score += 4;

            var op = rom[o + (reset & 0x7fff)];
            switch (op)
            {
                case 0x78 or 0x18 or 0x38 or 0x9c or 0x4c or 0x5c:
                    score += 8;
                    break;
                case 0xc2 or 0xe2 or 0xad or 0xae or 0xac or 0xaf or
                    0xa9 or 0xa2 or 0xa0 or 0x20 or 0x22:
                    score += 4;
                    break;
            }

            if (o <= 0x0200 && map == 0x20 || map == 0x30)
                score += 2;

            if (o <= 0x8200 && map == 0x21 || map == 0x31)
                score += 2;

            if (score > highscore)
            {
                if (o <= 0x0200)
                {
                    if (map == 0x23)
                        Mapper = new Sa1Rom(header);
                    else
                        Mapper = new LoRom(header);
                }
                else if (o <= 0x8200)
                    Mapper = new HiRom(header);
                highscore = score;
            }
        }
        return Mapper;
    }

    public struct Header
    {
        public string Name { get; set; }
        public int Map { get; set; }
        public int Mode { get; set; }
        public bool Speed { get; set; }
        public bool Sram { get; set; }
        public int Romsize { get; set; }
        public int Ramsize { get; set; }
        public byte[] Rom { get; set; }
    }

    public enum Mapmode
    {
        LoROM, HiROM, ExHiROM = 5
    }

    private readonly Dictionary<int, string> RomTypes = new()
    {
        [0x00] = "LoRom",
        [0x01] = "HiRom",
        [0x03] = "SA-1 ROM",
        [0x10] = "LoROM + FastROM",
        [0x11] = "HiROM + FastROM",
        [0x12] = "SDD-1 ROM",
        [0x13] = "",
        [0x15] = "ExHiRom"
    };

    private readonly Dictionary<int, string> Chips = new()
    {
        [0x00] = "ROM only",
        [0x01] = "ROM + RAM",
        [0x02] = "ROM + RAM + battery",
        [0x03] = "ROM + coprocessor",
        [0x04] = "ROM + coprocessor + RAM",
        [0x05] = "ROM + coprocessor + RAM + battery",
        [0x06] = "ROM + coprocessor + battery",
        [0x00] = "Coprocessor is DSP (DSP-1, 2, 3 or 4)",
        [0x10] = "Coprocessor is GSU (SuperFX)",
        [0x20] = "Coprocessor is OBC1",
        [0x30] = "Coprocessor is SA-1",
        [0x40] = "Coprocessor is S-DD1",
        [0x50] = "Coprocessor is S-RTC",
        [0xE0] = "Coprocessor is Other (Super Game Boy/Satellaview)",
        [0xF0] = "Coprocessor is Custom (specified with [0xFFBF)"
    };

    public Dictionary<string, string> GetCartInfo() => new()
    {
        ["Game"] = Path.GetFileNameWithoutExtension(Name),
        ["Type"] = $"{RomTypes.TryGetValue(Map, out string v)}",
        ["Id"] = $"{Map:X2}"
    };
}
