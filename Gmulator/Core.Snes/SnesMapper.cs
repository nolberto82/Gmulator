using Gmulator.Interfaces;
using System.ComponentModel.Design;

namespace Gmulator.Core.Snes;

public sealed class SnesMapper : ISaveState
{
    public string Name { get; private set; }
    public int Map { get; private set; }
    public bool Speed { get; private set; }
    public bool SramEnabled { get; set; }
    public int Coprocessor { get; private set; }
    public byte[] Rom { get; private set; }
    public int Romsize { get; private set; }
    public int Ramsize { get; private set; }
    public byte[] Sram { get; private set; }
    public SnesMmu Mmu { get; private set; }

    public List<MemoryHandler> MemoryHandler;
    private Timer Timer;
    private Mapmode Mapper;
    public enum Mapmode
    {
        LoROM, HiROM, ExHiROM = 5
    }

    public const int Gsu = 1;
    public const int Sa1 = 3;

    private readonly int[] offsets = [0x00000, 0x000200, 0x008000, 0x008200, 0x408000, 0x408200];

    public SnesMapper(MemoryMap map)
    {
        MemoryHandler = map.Handlers;
        Coprocessor = 0;
    }

    public void Reset(Snes snes)
    {
        var CpuMap = snes.CpuMap;
        if (Mapper == Mapmode.LoROM)
        {
            CpuMap.LoRom(0x00, 0x7d, 0x8000, 0xffff, Read, Write);
            CpuMap.LoRom(0x80, 0xff, 0x8000, 0xffff, Read, Write);
            CpuMap.Sram(0x70, 0x7d, 0x0000, 0x7fff, ReadSram, WriteSram);
            CpuMap.Sram(0xf0, 0xff, 0x0000, 0x7fff, ReadSram, WriteSram);
        }
        else
        {
            CpuMap.HiRom(0x00, 0x3f, 0x8000, 0xffff, Read, Write);
            CpuMap.HiRom(0x80, 0xbf, 0x8000, 0xffff, Read, Write);
            CpuMap.HiRom(0x40, 0x7d, 0x0000, 0xffff, Read, Write);
            CpuMap.HiRom(0xc0, 0xff, 0x0000, 0xffff, Read, Write);
            CpuMap.Sram(0x20, 0x3f, 0x6000, 0x7fff, ReadSram, WriteSram);
            CpuMap.Sram(0xa0, 0xbf, 0x6000, 0x7fff, ReadSram, WriteSram);
        }

        if (Coprocessor == Sa1)
        {
            var Sa1Map = snes.Sa1.Sa1Map;
            Sa1Map.LoRom(0x00, 0x7d, 0x8000, 0xffff, Read, Write);
            Sa1Map.LoRom(0x80, 0xff, 0x8000, 0xffff, Read, Write);

            CpuMap.LoRom(0x00, 0x3f, 0x8000, 0xffff, Read, Write);
            CpuMap.LoRom(0x80, 0xbf, 0x8000, 0xffff, Read, Write);
        }
    }

    public byte Read(int a)
    {
        if (Rom == null) return 0;
        var addr = MemoryHandler[a >> 12].Offset + (a & 0xfff);
        return Rom[addr % Rom.Length];
    }

    public void Write(int a, byte v)
    {

    }

    public byte ReadSram(int a)
    {
        if (!SramEnabled || Sram == null)
            return 0;
        int addr = MemoryHandler[a >> 12].Offset + (a & 0xfff);
        return Sram[addr & (Sram.Length - 1)];
    }

    public void WriteSram(int a, byte v)
    {
        if (!SramEnabled || Sram == null)
            return;
        int addr = MemoryHandler[a >> 12].Offset + (a & 0xfff);
        Sram[addr & (Sram.Length - 1)] = v;
        Timer ??= new Timer(SaveSram, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public byte ReadBwRam(int a) => Sram[a & Sram.Length - 1];

    public void WriteBwRam(int a, byte v) => Sram[a & Sram.Length - 1] = v;

    public void LoadSram()
    {
        var name = Path.GetFileNameWithoutExtension($"{Name}");
        if (File.Exists($"{SaveDirectory}/{name}.srm"))
        {
            var data = File.ReadAllBytes($"{SaveDirectory}/{name}.srm");
            if (data?.Length > 0)
                Sram = data;
        }
    }

    private void SaveSram(object state)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension($"{Name}");
            File.WriteAllBytes($"{SaveDirectory}/{name}.srm", Sram);
        }
        catch (IOException)
        {
            Timer?.Dispose();
            Timer = null;
        }
    }

    public bool LoadRom(string name)
    {
        if (name == string.Empty) return false;
        Name = name;
        Rom = File.ReadAllBytes(Name);
        return Rom != null;
    }

    public bool GetMapper(Snes snes, byte[] rom)
    {
        int highscore = -1;
        for (int i = 0; i < offsets.Length; i++)
        {
            int score = -1;
            int o = offsets[i];
            if (rom.Length < o + 0x7fff)
                continue;

            Rom = [.. rom.Skip(rom.Length % 1024)];
            Map = rom[o + 0x7fd5] & 0xef;
            Speed = (rom[o + 0x7fd5] & 0x10) != 0;
            SramEnabled = rom[o + 0x7fd6] != 0;
            Coprocessor = (rom[o + 0x7fd6] & 0xf0) >> 4;
            Romsize = 0x400 << rom[o + 0x7fd7];
            Ramsize = 0x400 << rom[o + 0x7fd8];
            Mmu = snes.Mmu;

            var map = Map & 0x37;

            var reset = rom[o + 0x7ffc] | rom[o + 0x7ffd] << 8;
            if (reset < 0x8000)
                continue;

            var complement = rom[o + 0x7fdc] | rom[o + 0x7fdd] << 8;
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
                Sram = new byte[Ramsize];
                if (o <= 0x0200)
                {
                    if (map == 0x23)
                    {
                        Coprocessor = Sa1;
                        Mapper = Mapmode.LoROM;
                        return true;
                    }
                    else
                    {
                        Mapper = Mapmode.LoROM;
                        return true;
                    }
                }
                else if (o <= 0x8200)
                {
                    Mapper = Mapmode.HiROM;
                    return true;
                }
                highscore = score;
            }
        }
        return false;
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(Sram);
    }

    public void Load(BinaryReader br)
    {
        Sram = ReadArray<byte>(br, Sram.Length);
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

    public Dictionary<string, string> GetCartInfo() => new()
    {
        ["Game"] = Path.GetFileNameWithoutExtension(Name),
        ["Type"] = $"{RomTypes.TryGetValue(Map, out _)}",
        ["Id"] = $"{Map:X2}"
    };
}
