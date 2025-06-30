using Gmulator.Core.Gbc.Mappers;

namespace Gmulator.Core.Gbc;
public class GbcMmu(GbcIO io, Dictionary<int, Cheat> cheats) : EmuState
{
    public string Title { get; private set; } = "";

    public int Type { get; private set; }
    public int RomSize { get; private set; }

    public string RomName { get; private set; } = "";
    public bool IsBios { get; private set; }

    public byte[] Ram { get; private set; } = new byte[0x10000];
    public byte[] Rom { get; private set; }
    public byte[] Vram { get; private set; } = new byte[0x4000];
    public byte[] Sram { get; private set; } = new byte[0x8000];
    public byte[] Wram { get; private set; } = new byte[0x8000];

    public GbcCpu Cpu { get; private set; }
    public BaseMapper Mapper { get; private set; }
    public GbcIO IO { get; private set; } = io;
    public Dictionary<int, Cheat> Cheats { get; } = cheats;

    public delegate void ReadBreakpoint(int a);
    public delegate void WriteBreakpoint(int a);

    public void Init(Gbc gbc)
    {
        Cpu = gbc.Cpu;
    }

    public byte[] ReadWram() => Ram;
    public byte[] ReadVram() => Ram;
    public byte[] ReadOram() => Ram.AsSpan(0xfe00, 0x100).ToArray();
    public byte[] ReadRom() => Rom;

    public byte Read(int a)
    {
        a &= 0xffff;
        byte v = 0xff;
        
        if (a <= 0x7fff)
        {
            if (a <= 0x7fff)
                v = Mapper.ReadRom(a);

            lock (Cheats)
            {
                var cht = Cheats.ContainsKey(a);
                if (cht)
                {
                    if (Cheats[a].Enabled && Cheats[a].Compare == v)
                        ApplyCheats(Cheats[a], a, ref v);
                }
            }
            return v;
        }
        else if (a <= 0x9fff)
            return Vram[a - 0x8000 + (0x2000 * IO.VBK)];
        else if (a <= 0xbfff)
        {
            if (Mapper.CartRamOn)
                return Sram[a - 0xa000 + (0x2000 * Mapper.Rambank)];
            return 0xff;
        }
        else if (a <= 0xcfff)
            return Ram[a];
        else if (a <= 0xdfff)
        {
            var wa = a - 0xd000 + (0x1000 * (IO.SVBK == 0 ? 1 : IO.SVBK));
            return Wram[wa];
        }
        else if (a <= 0xfeff || a >= 0xff80)
            return Ram[a];
        else if (a >= 0xff00 && a <= 0xff7f || a == 0xfffe || a == 0xffff)
            return IO.Read(a);

        return 0xff;
    }

    public void Write(int a, int val)
    {
        byte v = (byte)val;
        a &= 0xffff;
        if (a <= 0x3fff)
            Mapper.WriteRom0(a, v);
        else if (a <= 0x7fff)
            Mapper.WriteRom1(a, v);
        else if (a <= 0x9fff)
        {
            Vram[a - 0x8000 + 0x2000 * IO.VBK] = v;
            Ram[a] = v;
        }
        else if (a <= 0xbfff)
        {
            if (Mapper.CartRamOn)
            {
                Sram[a - 0xa000 + (0x2000 * IO.Mmu.Mapper.Rambank)] = v;
                Ram[a] = v;
            }
        }
        else if (a <= 0xcfff)
        {
            Wram[a - 0xc000] = v;
            Ram[a] = v;
        }
        else if (a <= 0xdfff)
        {
            Wram[a - 0xd000 + (0x1000 * (IO.SVBK == 0 ? 1 : IO.SVBK))] = v;
            Ram[a] = v;
        }
        else if (a <= 0xfeff)
            Ram[a] = v;
        else if (a <= 0xffff)
        {
            if (a <= 0xff7f)
                IO.Write(a, v);
            else
                Ram[a] = v;
        }
    }

    public byte ReadDirect(int a) => Ram[(ushort)a];
    public void WriteDirect(int a, byte v) => Ram[(ushort)a] = v;

    public void WriteBlock(int src, int dst, int size)
    {
        Span<byte> srcbytes;
        Span<byte> dstbytes;
        if (src <= 0x7fff)
            srcbytes = Mapper.ReadRomBlock(src, size);
        else if (src >= 0xa000 && src <= 0xbfff)
            srcbytes = new Span<byte>(Sram, src, size);
        else if (src >= 0xd000 && src <= 0xdfff)
            srcbytes = new Span<byte>(Wram, src - 0xd000 + (IO.SVBK * 0x1000), size);
        else
            srcbytes = new Span<byte>(Ram, src, size);

        dstbytes = new Span<byte>(Vram, (dst - 0x8000 + IO.VBK * 0x2000) & 0x3fff, size);
        srcbytes.CopyTo(dstbytes);

    }

    public void WriteVramBanks(int a, byte v) => Vram[(ushort)a] = v;

    public byte ReadDMA(int a)
    {
        a &= 0xffff;
        if (a <= 0x7fff)
            return Mapper.ReadRom(a);
        else if (a <= 0x9fff)
            return Vram[a - 0x8000 + (0x2000 * IO.VBK)];
        else if (a >= 0xd000 && a <= 0xdfff)
            return Wram[a - 0xd000 + (0x1000 * IO.SVBK)];
        else
            return Ram[a];
    }

    public void WriteDMA(int a, byte v)
    {
        a &= 0xffff;
        if (a >= 0x8000 && a <= 0x9fff)
        {
            Vram[a - 0x8000 + 0x2000 * IO.VBK] = v;
            Ram[a] = v;
        }
        else if (a >= 0xd000 && a <= 0xdfff)
        {
            Wram[a - 0xd000 + (IO.SVBK * 0x1000)] = v;
            Ram[a] = v;
        }
        else if (a >= 0xfe00 && a <= 0xfe9f)
            Ram[a] = v;
    }

    public ushort ReadWord(int a) => (ushort)(Ram[a] | Ram[a + 1] << 8);
    public BaseMapper LoadRom(string filename)
    {
        Rom = LoadFile(filename);
        if (Rom == null)
            return null;

        Mapper = Type switch
        {
            0x00 => new Mapper0(),
            >= 1 and <= 0x03 => new Mapper1(),
            0x05 or 0x06 => new Mapper2(),
            >= 0x0f and <= 0x13 => new Mapper3(),
            >= 0x19 and <= 0x1e => new Mapper5(),
            _ => null,
        };

        if (Mapper != null)
        {
            Mapper.Init(Rom, filename);
            RomName = filename;
            LoadRam();
        }
        return Mapper;
    }

    public void LoadRam()
    {
        var name = $"{Environment.CurrentDirectory}\\{SaveDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}.srm";
        if (File.Exists(name))
        {
            var v = File.ReadAllBytes($"{name}");
            v.CopyTo(Sram, 0x0000);
        }
    }

    public void SaveRam()
    {
        if (Mapper != null && Mapper.Ramsize > 0)
        {
            var name = Path.GetFullPath($"{SaveDirectory}/{Path.GetFileNameWithoutExtension(Mapper.Name)}.srm");
            if (Directory.Exists(SaveDirectory))
                File.WriteAllBytes($"{name}", Sram.AsSpan(0x0000, 0x2000).ToArray());
        }
    }

    public void Reset(string filename) => Mapper.Init(Rom, filename);

    public byte[] LoadFile(string filename)
    {
        byte[] rom = File.ReadAllBytes(filename);
        RomName = filename;

        if (Path.GetFileName(filename.ToLower()) != "dmg_boot.gb")
        {
            IsBios = false;
            Type = rom[0x147];
            if (MapperTypes.TryGetValue(Type, out string value))
                Console.WriteLine($"Mapper: {value}");
            rom = new Patch().Run(rom, filename);
        }
        else
            IsBios = true;
        return rom;
    }

    private void ApplyCheats(Cheat cht, int addr, ref byte v)
    {
        if (cht.Type == GameGenie)
        {
            if (cht.Compare == v)
                v = cht.Value;
        }
        else
        {
            if (addr <= 0xbfff)
                Sram[cht.Address & 0xfff] = cht.Value;
            else if (addr <= 0xcfff)
                Ram[cht.Address] = cht.Value;
            else if (addr <= 0xdfff)
                Wram[(cht.Address & 0xfff) + cht.Bank * 0x1000] = cht.Value;
        }
    }

    public override void Save(BinaryWriter bw)
    {
        ;
        bw.Write(Vram);
        bw.Write(Sram);
        bw.Write(Wram);
        bw.Write(Ram);
    }

    public override void Load(BinaryReader br)
    {
        br.ReadBytes(Vram.Length).CopyTo(Vram, 0x0000);
        br.ReadBytes(Sram.Length).CopyTo(Sram, 0x0000);
        br.ReadBytes(Wram.Length).CopyTo(Wram, 0x0000);
        br.ReadBytes(Ram.Length).CopyTo(Ram, 0x0000);
    }
}
