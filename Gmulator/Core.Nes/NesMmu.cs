using Gmulator.Core.Nes.Mappers;
using Gmulator.Interfaces;

namespace Gmulator.Core.Nes;

public class NesMmu(Dictionary<(int, int), Cheat> cheats) : IMmu, ISaveState
{
    private NesJoypad Joypad1;
    private NesJoypad Joypad2;
    private NesApu Apu;

    public byte[] Wram { get; set; } = new byte[0x800];
    public byte[] Vram { get; private set; }
    public byte[] Oram { get; private set; }

    private List<MemoryHandler> MemoryHandlers;

    public BaseMapper Mapper { get; private set; }

    public bool RomLoaded { get; private set; }
    public string RomName { get; internal set; }
    public Dictionary<(int, int), Cheat> Cheats { get; private set; } = cheats;
    public RamType RamType { get; private set; }

    public int GetOffset(int a)
    {
        var handler = MemoryHandlers[a >> 12];
        return handler.Offset;
    }

    public void Init(Nes nes)
    {
        Apu = nes.Apu;
        Joypad1 = nes.Joypad1;
        Joypad2 = nes.Joypad2;
        Vram = nes.Ppu.Vram;
        Oram = nes.Ppu.Oram;
        MemoryHandlers = nes.CpuMap.Handlers;
    }

    public void Reset()
    {
        Array.Fill<byte>(Wram, 0x00);
        Mapper.Reset();
    }

    private int ApplyGameGenieCheats(int addr, int value)
    {
        if (Cheats.Count == 0) return value;
        var addr80 = addr | 0x800000;
        if (Cheats.TryGetValue((addr, addr80), out Cheat cheat) && cheat.Enabled && cheat.Type == GameGenie)
        {
            if (value == Cheats[(addr, addr80)].Compare)
                return Cheats[(addr, addr80)].Value;
        }
        return value;
    }

    public void ApplyParCheats()
    {
        foreach (var c in from c in Cheats
                          where c.Value.Enabled && c.Value.Type == ProAction
                          select c)
        {
            Wram[c.Value.Address & 0xffff] = c.Value.Value;
        }
    }

    public int ReadWram(int addr) => Wram[addr & 0x7ff];
    public void WriteWram(int addr, int value) => Wram[addr & 0x7ff] = (byte)value;
    public int ReadSram(int addr) => Mapper.Sram[addr & 0x1fff];
    public void WriteSram(int addr, int value) => Wram[addr & 0xffff] = (byte)value;
    public int ReadVram(int addr) => Vram[addr & 0x3fff];
    public static int ReadNone(int addr) => 0;
    public static void WriteNone(int addr, int value) { }

    public int ReadByte(int addr)
    {
        addr &= 0xffff;
        RamType = MemoryHandlers[addr].Type;
        int v = MemoryHandlers[addr].Read(addr);
        if (Cheats.Count > 0 && RamType == RamType.Rom)
            return ApplyGameGenieCheats(addr, v);
        return v;
    }

    public void WriteByte(int addr, int value)
    {
        RamType = MemoryHandlers[addr].Type;
        MemoryHandlers[addr].Write(addr, value);
    }

    public byte ReadDebug(int addr) => Wram[addr];

    public int ReadWord(int addr) => ReadByte(addr) | ReadByte(addr + 1) << 8;

    public BaseMapper LoadRom(string filename)
    {
        if (!File.Exists(filename))
            return null;

        Header header = new();
        var data = File.ReadAllBytes(filename).Take(16).ToArray();

        string nes = Encoding.Default.GetString([.. data.Take(4)]);

        if (nes != "NES\u001a")
            return null;

        header.Name = filename;
        header.PrgBanks = data[4];
        header.ChrBanks = data[5];
        Header.MapperId = (data[6] & 0xf0) >> 4 | (data[7] & 0xf0);
        Header.Mirror = (data[6] & 0x01) != 0 ? Vertical : Horizontal;
        header.Battery = (data[6] & 0x02) != 0;
        header.Trainer = (data[6] & 0x04) != 0;
        var b12 = data[12] & 3;
        if (b12 > 0)
        {
            if (b12 == 0 || b12 == 2)
                Header.Region = 0;
            else
                Header.Region = 1;
        }
        else
            Header.Region = data[9] & 1;
        Header.Region = 0;

        header.Mmu = this;


        var rom = File.ReadAllBytes(filename);
        rom = new Patch().Run(rom, filename);

        header.PrgRom = [.. rom.Take(header.PrgBanks * 0x4000 + 0x10).Skip(0x10)];
        header.CharRom = [.. rom.Skip(header.PrgRom.Length + 0x10).Take(header.ChrBanks * 0x2000)];

        Mapper = Header.MapperId switch
        {
            0 => Mapper = new Mapper000(header, this),
            1 => Mapper = new Mapper001(header, this),
            2 => Mapper = new Mapper002(header, this),
            3 => Mapper = new Mapper003(header, this),
            4 => Mapper = new Mapper004(header, this),
            5 => Mapper = new Mapper005(header, this),
            7 => Mapper = new Mapper007(header, this),
            9 => Mapper = new Mapper009(header, this),
            30 => Mapper = new Mapper030(header, this),
            _ => null
        };
        LoadRam();
        return Mapper;
    }

    public void LoadRam()
    {
        if (Mapper != null && Mapper.SramEnabled)
        {
            Mapper.Sram = new byte[0x2000];
            var name = $"{Environment.CurrentDirectory}\\{SaveDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}.sav";
            if (File.Exists(name))
            {
                var v = File.ReadAllBytes($"{name}");
                v.CopyTo(Mapper.Sram, 0x0000);
            }
        }
    }

    public void Save(BinaryWriter bw)
    {
        WriteArray(bw, Wram); WriteArray(bw, Vram); WriteArray(bw, Oram);
    }

    public void Load(BinaryReader br)
    {
        Wram = ReadArray<byte>(br, Wram.Length); Vram = ReadArray<byte>(br, Vram.Length); Oram = ReadArray<byte>(br, Oram.Length);
    }

    public void WriteWord(int addr, int value)
    {
        WriteByte(addr, value & 0xff);
        WriteByte(addr + 1, (value >> 8) & 0xff);
    }

    public int ReadLong(int addr)
    {
        return ReadByte(addr) | ReadByte(addr + 1) << 8 | ReadByte(addr + 2) << 16;
    }

    public void WriteLong(int addr, int value)
    {
        WriteByte(addr, value & 0xff);
        WriteByte(addr + 1, (value >> 8) & 0xff);
        WriteByte(addr + 2, (value >> 16) & 0xff);
    }
}

public struct Header
{
    public NesMmu Mmu { get; set; }
    public byte[] PrgRom { get; set; }
    public byte[] CharRom { get; set; }

    public int PrgBanks { get; set; }
    public int ChrBanks { get; set; }
    public static int MapperId { get; set; }
    public static int Mirror { get; set; }
    public bool Battery { get; set; }
    public bool Trainer { get; set; }
    public string Name { get; set; }
    public static int Region { get; set; }
}
