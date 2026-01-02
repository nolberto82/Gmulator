using Gmulator.Core.Nes.Mappers;
using Gmulator.Core.Snes;
using Gmulator.Interfaces;
using System.Text;
using static Gmulator.Interfaces.IMmu;

namespace Gmulator.Core.Nes;

public class NesMmu(Dictionary<int, Cheat> cheats) : IMmu, ISaveState
{
    private NesJoypad Joypad1;
    private NesJoypad Joypad2;
    private NesApu Apu;

    public byte[] Wram { get; private set; } = new byte[0x800];
    public byte[] Vram { get; private set; }
    public byte[] Oram { get; private set; }

    private List<MemoryHandler> MemoryHandlers;

    public BaseMapper Mapper { get; private set; }

    public bool RomLoaded { get; private set; }
    public string RomName { get; internal set; }
    public Dictionary<int, Cheat> Cheats { get; private set; } = cheats;
    public RamType RamType { get; private set; }
    public int RamMask { get; private set; }
    public ReadDel[] Read { get; set; }
    public WriteDel[] Write { get; set; }
    public RamType[] RamTypes { get; set; }

    public void Init(Nes nes)
    {
        Apu = nes.Apu;
        Joypad1 = nes.Joypad1;
        Joypad2 = nes.Joypad2;
        Vram = nes.Ppu.Vram;
        Oram = nes.Ppu.Oram;
        MemoryHandlers = nes.MemoryHandlers;
    }

    public void Reset()
    {
        Array.Fill<byte>(Wram, 0x00);
        Mapper.Reset();
    }

    private byte ApplyGameGenieCheats(int a, byte v)
    {
        if (Cheats.Count == 0) return v;
        if (Cheats.TryGetValue(a, out Cheat value) && value.Enabled && value.Type == GameGenie)
        {
            if (v == Cheats[a].Compare)
                return Cheats[a].Value;
        }
        return v;
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

    public int ReadWram(int a) => Wram[a & 0x7ff];
    public void WriteWram(int a, int v) => Wram[a & 0x7ff] = (byte)v;
    public int ReadSram(int a) => Mapper.Sram[a & 0x1fff];
    public void WriteSram(int a, int v) => Wram[a & 0xffff] = (byte)v;
    public static int ReadNone(int a) => 0;
    public static void WriteNone(int a, int v) { }

    public int ReadByte(int a)
    {
        RamType = MemoryHandlers[a].Type;
        int v = MemoryHandlers[a].Read(a);
        if (Cheats.Count > 0 && RamType == RamType.Rom)
            return ApplyGameGenieCheats(a, (byte)v);
        return v;
    }

    public void WriteByte(int a, int v)
    {
        RamType = MemoryHandlers[a].Type;
        MemoryHandlers[a].Write(a, v);
    }

    public byte ReadDebug(int addr) => Wram[addr];

    public int ReadWord(int addr)
    {
        return ReadByte(addr) | ReadByte(addr + 1) << 8;
    }

    public BaseMapper LoadRom(string filename)
    {
        if (!File.Exists(filename))
            return null;

        Header header = new();
        var data = File.ReadAllBytes(filename).Take(16).ToArray();

        string nes = Encoding.Default.GetString([.. data.Take(4)]);

        if (nes != "NES\u001a")
            return null;

        bool mapper20 = (data[7] & 0x0c) == 8;

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
