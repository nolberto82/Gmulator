using Gmulator.Core.Gbc.Mappers;
using Gmulator.Interfaces;

namespace Gmulator.Core.Gbc;

public class GbcMmu(Gbc gbc, Dictionary<(int, int), Cheat> cheats) : IMmu, ISaveState
{
    public string Title { get; private set; } = "";

    public int Type { get; private set; }
    public int RomSize { get; private set; }
    public bool IsBios { get; private set; }

    private byte[] _ram = new byte[0x10000];
    private byte[] _vram = new byte[0x4000];
    public byte[] Wram { get; set; } = new byte[0x8000];
    private byte[] _rom;

    public byte VramBank { get; set; }
    public byte WramBank { get; set; }

    public RamType RamType { get; private set; }
    public int RamMask { get; private set; }

    public BaseMapper Mapper { get; private set; }
    public Dictionary<(int, int), Cheat> Cheats { get; } = cheats;

    private readonly List<MemoryHandler> MemoryHandlers = gbc.CpuMap.Handlers;

    private readonly Gbc Gbc = gbc;

    public int GetOffset(int a)
    {
        var handler = MemoryHandlers[a >> 12];
        return handler.Offset;
    }

    public int ReadByte(int addr)
    {
        addr &= 0xffff;
        RamType = MemoryHandlers[addr].Type;
        int v = MemoryHandlers[addr].Read(addr);
        if (RamType == RamType.Rom && Cheats.Count > 0)
            return ApplyGameGenieCheats(addr, v);
        return v;
    }

    public void WriteByte(int addr, int value)
    {
        addr &= 0xffff;
        RamType = MemoryHandlers[addr].Type;
        MemoryHandlers[addr].Write(addr, value);
    }

    public int ReadRam(int addr) => _ram[addr];
    public void WriteRam(int addr, int value) => _ram[addr] = (byte)value;
    public int ReadIo(int addr) => (byte)MemoryHandlers[0xff00 + (addr & 0xff)].Read(0xff00 + (addr & 0xff));
    public void WriteIo(int addr, int value) => _ram[0xff00 + addr & 0xff] = (byte)value;
    public int ReadHram(int addr) => (byte)MemoryHandlers[0xff80 + (addr & 0xff)].Read(0xff80 + (addr & 0xff));

    public void WriteHram(int addr, int value) => _ram[0xff80 + addr & 0xff] = (byte)value;

    public int ReadVramBank(int addr) => _vram[(addr & 0x1fff) + (0x2000 * VramBank) & 0x3fff];

    public int ReadVram(int addr) => _vram[addr & 0x3fff];

    public int ReadAttribute(int addr) => _vram[(addr & 0x1fff) + 0x2000];

    public void WriteVramBank(int addr, int value) => _vram[(addr & 0x1fff) + (0x2000 * VramBank)] = (byte)value;

    public int ReadOam(int addr) => _ram[addr];

    public int ReadSram(int addr)
    {
        if (Mapper.CartRamEnabled)
            return Mapper.Sram[(addr - 0xa000 + (0x2000 * Mapper.Rambank)) & 0x1fff];
        return 0xff;
    }

    public void WriteSram(int addr, int value)
    {
        if (Mapper.CartRamEnabled)
        {
            Mapper.Sram[(addr - 0xa000 + 0x2000 * Mapper.Rambank) & 0x1fff] = (byte)value;
            _ram[addr] = (byte)value;
            Mapper.Write();
        }
    }

    public int ReadWram(int addr) => Wram[addr < 0xd000 ? addr % 0x1000 : addr % 0x1000 + (0x1000 * (WramBank == 0 ? 1 : WramBank))];

    public void WriteWram(int addr, int value)
    {
        Wram[addr < 0xd000 ? addr % 0x1000 : addr % 0x1000 + (0x1000 * (WramBank == 0 ? 1 : WramBank))] = (byte)value;
        _ram[addr] = (byte)value;
    }

    public void WriteBlock(int src, int dst, int size)
    {
        Span<byte> srcbytes;
        Span<byte> dstbytes;
        if (src <= 0x7fff)
            srcbytes = Mapper.ReadRomBlock(src, size);
        else if (src >= 0xa000 && src <= 0xbfff)
            srcbytes = new Span<byte>(Mapper.Sram, src, size);
        else if (src >= 0xd000 && src <= 0xdfff)
            srcbytes = new Span<byte>(Wram, src - 0xd000 + (WramBank * 0x1000), size);
        else
            srcbytes = new Span<byte>(_ram, src, size);

        dstbytes = new Span<byte>(_vram, (dst - 0x8000 + VramBank * 0x2000) & 0x3fff, size);
        srcbytes.CopyTo(dstbytes);

    }

    public void WriteVramBanks(int addr, byte value) => _vram[(ushort)addr] = value;

    public int ReadDMA(int addr)
    {
        addr &= 0xffff;
        if (addr <= 0x7fff)
            return Mapper.ReadRom(addr) & 0xff;
        else if (addr <= 0x9fff)
            return _vram[addr - 0x8000 + (0x2000 * VramBank)];
        else if (addr >= 0xd000 && addr <= 0xdfff)
            return Wram[addr - 0xd000 + (0x1000 * WramBank)];
        else
            return _ram[addr];
    }

    public void WriteDMA(int addr, byte value)
    {
        addr &= 0xffff;
        if (addr >= 0x8000 && addr <= 0x9fff)
        {
            _vram[addr - 0x8000 + 0x2000 * VramBank] = value;
            _ram[addr] = value;
        }
        else if (addr >= 0xd000 && addr <= 0xdfff)
        {
            Wram[addr - 0xd000 + (WramBank * 0x1000)] = value;
            _ram[addr] = value;
        }
        else if (addr >= 0xfe00 && addr <= 0xfe9f)
            _ram[addr] = value;
    }

    public ushort ReadWord(int addr) => (ushort)(_ram[addr] | _ram[addr + 1] << 8);

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
            <= 0x0d => Mapper.CGB ? new Span<byte>(Wram, a - 0xd000 + WramBank * 0x1000, 0xa0) : new(_ram, a, 0xa0),
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

    public void Reset()
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

    private byte ApplyGameGenieCheats(int ba, int v)
    {
        var cht = Cheats.ContainsKey((ba, ba)) && Cheats[(ba, ba)].Enabled && Cheats[(ba, ba)].Compare == v && Cheats[(ba, ba)].Type == GameGenie;
        if (cht)
            return Cheats[(ba, ba)].Value;
        return (byte)v;
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
                Wram[(c.Value.Address & 0xfff) + c.Value.Bank * 0x1000] = c.Value.Value;
        }
    }

    public void Save(BinaryWriter bw)
    {
        WriteArray(bw, _ram); WriteArray(bw, _vram); WriteArray(bw, Wram);
    }

    public void Load(BinaryReader br)
    {
        _ram = ReadArray<byte>(br, _ram.Length); _vram = ReadArray<byte>(br, _vram.Length); Wram = ReadArray<byte>(br, Wram.Length);
    }

    int IMmu.ReadWord(int a)
    {
        return ReadWord(a);
    }

    public void WriteWord(int addr, int value)
    {
        WriteByte(addr, value & 0xff);
        WriteByte(addr + 1, value >> 8 & 0xff);
    }

    public int ReadLong(int addr)
    {
        return ReadByte(addr) | ReadByte(addr + 1) << 8 | ReadByte(addr + 2) << 16;
    }

    public void WriteLong(int addr, int value)
    {
        WriteByte(addr, value & 0xff);
        WriteByte(addr + 1, value >> 8 & 0xff);
        WriteByte(addr + 2, value >> 16 & 0xff);
    }
}
