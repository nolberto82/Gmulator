using Gmulator.Core.Gbc.Mappers;
using Gmulator.Interfaces;
using System.Xml.Linq;
using static Gmulator.Interfaces.IMmu;

namespace Gmulator.Core.Gbc;

public class GbcMmu : IMmu, ISaveState
{
    public string Title { get; private set; } = "";

    public int Type { get; private set; }
    public int RomSize { get; private set; }
    public bool IsBios { get; private set; }

    private byte[] _ram;
    private byte[] _vram;
    private byte[] _wram;
    private byte[] _rom;

    public int VramBank { get; set; }
    public int WramBank { get; set; }

    public RamType RamType { get; private set; }
    public int RamMask { get; private set; }

    public BaseMapper Mapper { get; private set; }
    public Dictionary<int, Cheat> Cheats { get; }

    private List<MemoryHandler> MemoryHandlers;

    private Gbc Gbc;

    public GbcMmu(Gbc gbc, Dictionary<int, Cheat> cheats)
    {
        Cheats = cheats;
        MemoryHandlers = gbc.MemoryHandlers;
        Gbc = gbc;
        _ram = new byte[0x10000];
        _vram = new byte[0x4000];
        _wram = new byte[0x8000];
    }

    public int ReadByte(int a)
    {
        a &= 0xffff;
        RamType = MemoryHandlers[a].Type;
        int v = MemoryHandlers[a].Read(a);
        if (RamType == RamType.Rom && Cheats.Count > 0)
            return ApplyGameGenieCheats(a, (byte)v);
        return v;
    }

    public void WriteByte(int a, int v)
    {
        a &= 0xffff;
        RamType = MemoryHandlers[a].Type;
        MemoryHandlers[a].Write(a, v);
    }

    public int ReadRam(int a) => _ram[a];
    public void WriteRam(int a, int v) => _ram[a] = (byte)v;
    public int ReadIo(int a)
    {
        return MemoryHandlers[0xff00 + (a & 0xff)].Read(0xff00 + (a & 0xff));
    }
    public void WriteIo(int a, int v) => _ram[0xff00 + a & 0xff] = (byte)v;
    public int ReadHram(int a)
    {
        return MemoryHandlers[0xff80 + (a & 0xff)].Read(0xff80 + (a & 0xff));
    }

    public void WriteHram(int a, int v) => _ram[0xff80 + a & 0xff] = (byte)v;

    public int ReadVramBank(int a)
    {
        return _vram[(a & 0x1fff) + (0x2000 * VramBank) & 0x3fff];
    }

    public int ReadVram(int a) => _vram[a & 0x3fff];

    public int ReadAttribute(int a)
    {
        return _vram[(a & 0x1fff) + 0x2000];
    }

    public void WriteVramBank(int a, int v)
    {
        _vram[(a & 0x1fff) + (0x2000 * VramBank)] = (byte)v;
    }

    public int ReadOam(int a) => _ram[a];

    public int ReadSram(int a)
    {
        if (Mapper.CartRamOn)
            return Mapper.Sram[(a - 0xa000 + (0x2000 * Mapper.Rambank)) & 0x1fff];
        return 0xff;
    }

    public void WriteSram(int a, int v)
    {
        if (Mapper.CartRamOn)
        {
            Mapper.Sram[(a - 0xa000 + 0x2000 * Mapper.Rambank) & 0x1fff] = (byte)v;
            _ram[a] = (byte)v;
            Mapper.Write();
        }
    }

    public int ReadWram(int a)
    {
        return _wram[a < 0xd000 ? a % 0x1000 : a % 0x1000 + (0x1000 * (WramBank == 0 ? 1 : WramBank))];
    }

    public void WriteWram(int a, int v)
    {
        _wram[a < 0xd000 ? a % 0x1000 : a % 0x1000 + (0x1000 * (WramBank == 0 ? 1 : WramBank))] = (byte)v;
        _ram[a] = (byte)v;
    }

    public static int ReadOpen(int a) => 0xff;
    public static void WriteOpen(int a, int v) { }

    public void WriteBlock(int src, int dst, int size)
    {
        Span<byte> srcbytes;
        Span<byte> dstbytes;
        if (src <= 0x7fff)
            srcbytes = Mapper.ReadRomBlock(src, size);
        else if (src >= 0xa000 && src <= 0xbfff)
            srcbytes = new Span<byte>(Mapper.Sram, src, size);
        else if (src >= 0xd000 && src <= 0xdfff)
            srcbytes = new Span<byte>(_wram, src - 0xd000 + (WramBank * 0x1000), size);
        else
            srcbytes = new Span<byte>(_ram, src, size);

        dstbytes = new Span<byte>(_vram, (dst - 0x8000 + VramBank * 0x2000) & 0x3fff, size);
        srcbytes.CopyTo(dstbytes);

    }

    public void WriteVramBanks(int a, byte v)
    {
        _vram[(ushort)a] = v;
    }

    public int ReadDMA(int a)
    {
        a &= 0xffff;
        if (a <= 0x7fff)
            return Mapper.ReadRom(a) & 0xff;
        else if (a <= 0x9fff)
            return _vram[a - 0x8000 + (0x2000 * VramBank)];
        else if (a >= 0xd000 && a <= 0xdfff)
            return _wram[a - 0xd000 + (0x1000 * WramBank)];
        else
            return _ram[a];
    }

    public void WriteDMA(int a, byte v)
    {
        a &= 0xffff;
        if (a >= 0x8000 && a <= 0x9fff)
        {
            _vram[a - 0x8000 + 0x2000 * VramBank] = v;
            _ram[a] = v;
        }
        else if (a >= 0xd000 && a <= 0xdfff)
        {
            _wram[a - 0xd000 + (WramBank * 0x1000)] = v;
            _ram[a] = v;
        }
        else if (a >= 0xfe00 && a <= 0xfe9f)
            _ram[a] = v;
    }

    public ushort ReadWord(int a)
    {
        return (ushort)(_ram[a] | _ram[a + 1] << 8);
    }

    public byte[] ReadWram() => _ram;
    public byte[] ReadVram() => _vram;
    public byte[] ReadSram() => Mapper.Sram;
    public byte[] ReadOram() => _ram.AsSpan(0xfe00, 0x100).ToArray();
    public byte[] ReadRom() => _rom;

    public void WriteDMA(int v)
    {
        var a = v << 8;
        Span<byte> srcbytes = (a >> 12) switch
        {
            <= 0x07 => Mapper.ReadRomBlock(a, 0xa0),
            <= 0x09 => Mapper.CGB ? new Span<byte>(_vram, a, 0xa0) : new(_ram, a, 0xa0),
            <= 0x0b => new Span<byte>(Gbc.Mapper.Sram, a - 0xa000 + (Mapper.Rambank * 0x2000), 0xa0),
            <= 0x0c => Mapper.CGB ? new Span<byte>(_ram, a, 0xa0) : new(_ram, a, 0xa0),
            <= 0x0d => Mapper.CGB ? new Span<byte>(_wram, (a - 0xd000) + WramBank * 0x1000, 0xa0) : new(_ram, a, 0xa0),
            _ => null,
        };

        if (!srcbytes.IsEmpty)
        {
            Span<byte> dstbytes = new(_ram, 0xfe00, 0xa0);
            srcbytes.CopyTo(dstbytes);
        }
    }

    public BaseMapper LoadRom(string filename)
    {
        _rom = LoadFile(filename);
        if (_rom == null)
            return null;
        Mapper = _rom[0x147] switch
        {
            0x00 => new Mapper0(_rom, this),
            >= 1 and <= 0x03 => new Mapper1(_rom, this),
            0x05 or 0x06 => new Mapper2(_rom, this),
            >= 0x0f and <= 0x13 => new Mapper3(_rom, this),
            >= 0x19 and <= 0x1e => new Mapper5(_rom, this),
            _ => null,
        };

        Mapper?.Init(_rom, filename);
        return Mapper;
    }

    public void LoadRam()
    {
        var name = $"{Environment.CurrentDirectory}\\{SaveDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}.srm";
        if (File.Exists(name))
        {
            var v = File.ReadAllBytes($"{name}");
            v.CopyTo(Mapper.Sram, 0x0000);
        }
    }

    public void SaveRam()
    {
        if (Mapper != null && Mapper.Ramsize != 0)
        {
            var name = Path.GetFullPath($"{SaveDirectory}/{Path.GetFileNameWithoutExtension(Mapper.Name)}.srm");
            if (Directory.Exists(SaveDirectory))
                File.WriteAllBytes($"{name}", Mapper.Sram.AsSpan(0x0000, 0x2000).ToArray());
        }
    }

    public void Reset(string filename)
    {
        Array.Fill<byte>(_ram, 0x00, 0xc000, 0x4000);
        WramBank = VramBank = 0;
    }

    public byte[] LoadFile(string filename)
    {
        byte[] rom = File.ReadAllBytes(filename);

        if (Path.GetFileName(filename.ToLower()) != "dmg_boot.gb")
        {
            IsBios = false;
            Type = rom[0x147];
            rom = new Patch().Run(rom, filename);
        }
        else
            IsBios = true;
        return rom;
    }

    private byte ApplyGameGenieCheats(int ba, byte v)
    {
        var cht = Cheats.ContainsKey(ba) && Cheats[ba].Enabled && Cheats[ba].Compare == v && Cheats[ba].Type == GameGenie;
        if (cht)
            return Cheats[ba].Value;
        return v;
    }

    public void ApplyParCheats()
    {
        foreach (var c in from c in Cheats
                          where c.Value.Enabled && c.Value.Type == ProAction
                          select c)
        {
            if (c.Value.Address <= 0xbfff)
                Mapper.Sram[c.Value.Address & 0xfff] = c.Value.Value;
            else if (c.Value.Address <= 0xcfff)
                _ram[c.Value.Address] = c.Value.Value;
            else if (c.Value.Address <= 0xdfff)
                _wram[(c.Value.Address & 0xfff) + c.Value.Bank * 0x1000] = c.Value.Value;
        }
    }

    public void Save(BinaryWriter bw)
    {
        WriteArray(bw, _ram); WriteArray(bw, _vram); WriteArray(bw, _wram);
    }

    public void Load(BinaryReader br)
    {
        _ram = ReadArray<byte>(br, _ram.Length); _vram = ReadArray<byte>(br, _vram.Length); _wram = ReadArray<byte>(br, _wram.Length);
    }
}
