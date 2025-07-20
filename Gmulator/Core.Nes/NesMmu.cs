using Gmulator.Core.Nes.Mappers;
using System.Xml.Linq;
using System.Runtime.Intrinsics.Arm;

namespace Gmulator.Core.Nes;
public class NesMmu(Dictionary<int, Cheat> cheats) : EmuState
{
    private NesJoypad Joypad1;
    private NesJoypad Joypad2;

    public byte[] Ram { get; private set; } = new byte[0x10000];
    public byte[] Sram { get; private set; } = new byte[0x2000];
    public byte[] Vram { get; private set; } = new byte[0x4000];
    public byte[] Oram { get; private set; } = new byte[0x100];

    public BaseMapper Mapper { get; private set; }

    public bool RomLoaded { get; private set; }
    public string RomName { get; internal set; }
    public Dictionary<int, Cheat> Cheats { get; private set; } = cheats;

    public Func<byte> StatusR;
    public Func<byte> DataR;
    public Action<byte> ControlW;
    public Action<byte> MaskW;
    public Action<byte> OamAddressW;
    public Action<byte> OamDataW;
    public Action<byte> ScrollW;
    public Action<byte> AddressDataW;
    public Action<byte> DataW;
    public Action<byte> OamDamyCopy;
    public Func<int, byte> ApuRead;
    public Action<int, byte> ApuWrite;

    public void Init(NesJoypad joy1, NesJoypad joy2)
    {
        Joypad1 = joy1;
        Joypad2 = joy2;
    }

    public void Reset()
    {
        Array.Fill<byte>(Ram, 0x00, 0x0000, 0x6000);
        Array.Fill<byte>(Vram, 0x00);
        Mapper.Reset();
    }

    public void ApplyCheats(int a, ref byte v)
    {
        var found = Cheats?.ContainsKey(a);
        if (found == true)
        {
            var cht = Cheats[a];
            if (cht.Enabled)
            {
                if (a >= 0x8000 && a == cht.Address && cht.Compare == v)
                    v = cht.Value;
                else if (cht.Address <= 0x07ff || (cht.Address >= 0x6000 && cht.Address <= 0x7fff))
                    Ram[cht.Address & 0xffff] = cht.Value;
            }
        }
    }

    public byte[] ReadWram() => Ram;
    public byte[] ReadVram() => Vram;
    public byte[] ReadOram() => Oram;
    public byte[] ReadPrg() => Mapper.Prg;
    public byte[] ReadChr() => Mapper.Chr;

    public byte Read(int a)
    {
        byte v = 0;
        if (a < 0x2000)
            v = Ram[a & 0x7ff];
        else if (a >= 0x2000 && a <= 0x3fff)
        {
            if ((a & 0x7) == 0x02)
                return StatusR();
            else if ((a & 0x7) == 0x07)
                return DataR();
        }
        else if (a <= 0x4015)
            return ApuRead(a);
        else if (a == 0x4016)
            v = Joypad1.Read();
        else if (a == 0x4017)
            v = 0;
        else if (a <= 0x5fff)
            v = 0xff;
        else if (a <= 0x7fff && Mapper.Sram)
            v = Sram[a - 0x6000];
        else if (a >= 0x8000)
            v = Mapper.ReadPrg(a);
        else
            v = Ram[a & 0x7ff];

        ApplyCheats(a, ref v);
        return v;
    }

    public void Write(int a, int val)
    {
        byte v = (byte)val;

        if (a >= 0x0000 && a <= 0x1fff)
            Ram[a & 0x7ff] = v;
        else if (a >= 0x2000 && a <= 0x3fff)
        {
            if (a == 0x2000)
                ControlW(v);
            else if (a == 0x2001)
                MaskW(v);
            else if (a == 0x2003)
                OamAddressW(v);
            else if (a == 0x2004)
                OamDataW(v);
            else if (a == 0x2005)
                ScrollW(v);
            else if (a == 0x2006)
                AddressDataW(v);
            else if (a == 0x2007)
                DataW(v);
        }
        else if (a == 0x4014)
            OamDamyCopy(v);
        else if ((a >= 0x4000 && a <= 0x4013) || a == 0x4015 || a == 0x4017)
        {
            ApuWrite(a, v);
            Ram[a] = v;
        }
        else if (a == 0x4016)
        {
            Joypad1.Write(v);
            //Joypad2.Write(v);
        }
        //else if (a == 0x4017)

        else if (a <= 0x5fff)
            Mapper.Write(a, v);
        else if (a <= 0x7fff && Mapper.Sram)
            Ram[a] = Sram[a - 0x6000] = v;
        else if (a >= 0x8000)
            Mapper.Write(a, v);
    }

    public byte ReadDebug(int addr) => Ram[addr];

    public int ReadWord(int addr)
    {
        if (addr >= 0x8000)
            return (ushort)(Mapper.ReadPrg(addr) | Mapper.ReadPrg(addr + 1) << 8);
        return (ushort)(ReadDebug(addr + 0) | ReadDebug(addr + 1) << 8);
    }

    public void CopyRam(byte[] src, int dstoffset, int size)
    {
        Span<byte> dst = new(Ram, dstoffset, size);
        if (dst.Length > 0)
            src.CopyTo(dst);
    }

    public void CopyVram(byte[] src, int dstoffset, int size) =>
        src.CopyTo(new Span<byte>(Vram, dstoffset, size));

    public BaseMapper LoadRom(string filename)
    {
        if (!File.Exists(filename))
            return null;

        Header header = new();

        using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
        {
            using var reader = new BinaryReader(fs);
            char[] c = new char[4];
            reader.Read(c, 0, 4);
            string nes = new(c);

            if (nes != "NES\u001a")
                return null;

            header.Name = filename;
            header.PrgBanks = reader.Read();
            header.ChrBanks = reader.Read();
            var b6 = reader.Read();
            var b7 = reader.Read();
            Header.MapperId = (b6 & 0xf0) >> 4 | (b7 & 0xf0);
            Header.Mirror = b6.GetBit(0) ? Vertical : Horizontal;
            header.Battery = b6.GetBit(1);
            header.Trainer = b6.GetBit(2);
            reader.BaseStream.Seek(9, SeekOrigin.Begin);
            var b9 = reader.Read() & 1;
            reader.BaseStream.Seek(12, SeekOrigin.Begin);
            var b12 = BitConverter.ToInt32(reader.ReadBytes(4)) & 3;
            if (b12 > 0)
            {
                if (b12 == 0 || b12 == 2)
                    Header.Region = 0;
                else
                    Header.Region = 1;
            }
            else
                Header.Region = b9 & 1;
            Header.Region = 0;

            header.Mmu = this;
        }

        var rom = File.ReadAllBytes(filename);
        rom = new Patch().Run(rom, filename);

        header.Prom = [.. rom.Take(header.PrgBanks * 0x4000 + 0x10).Skip(0x10)];
        header.Vrom = [.. rom.Skip(header.Prom.Length + 0x10).Take(header.ChrBanks * 0x2000)];

        Mapper = Header.MapperId switch
        {
            0 => Mapper = new Mapper000(header),
            1 => Mapper = new Mapper001(header),
            2 => Mapper = new Mapper002(header),
            3 => Mapper = new Mapper003(header),
            4 => Mapper = new Mapper004(header),
            5 => Mapper = new Mapper005(header),
            7 => Mapper = new Mapper007(header),
            15 => Mapper = new Mapper009(header),
            _ => null
        };
        LoadRam();
        return Mapper;
    }

    public void LoadRam()
    {
        if (Mapper != null && Mapper.Sram)
        {
            var name = $"{Environment.CurrentDirectory}\\{SaveDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Name)}.sav";
            if (File.Exists(name))
            {
                var v = File.ReadAllBytes($"{name}");
                v.CopyTo(Sram, 0x0000);
            }
        }
    }

    public void SaveRam()
    {
        if (Mapper != null && Mapper.Sram)
        {
            var name = Path.GetFullPath($"{SaveDirectory}/{Path.GetFileNameWithoutExtension(Mapper.Name)}.sav");
            if (Directory.Exists(SaveDirectory))
                File.WriteAllBytes($"{name}", [.. Sram]);
        }
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(Vram);
        bw.Write(Sram);
        bw.Write(Oram);
        bw.Write(Ram);
    }

    public override void Load(BinaryReader br)
    {
        br.ReadBytes(Vram.Length).CopyTo(Vram, 0x0000);
        br.ReadBytes(Sram.Length).CopyTo(Sram, 0x0000);
        br.ReadBytes(Oram.Length).CopyTo(Oram, 0x0000);
        br.ReadBytes(Ram.Length).CopyTo(Ram, 0x0000);
    }
}

public struct Header
{
    public NesMmu Mmu { get; set; }
    public byte[] Prom { get; set; }
    public byte[] Vrom { get; set; }

    public int PrgBanks { get; set; }
    public int ChrBanks { get; set; }
    public static int MapperId { get; set; }
    public static int Mirror { get; set; }
    public bool Battery { get; set; }
    public bool Trainer { get; set; }
    public string Name { get; set; }
    public static int Region { get; set; }
}
