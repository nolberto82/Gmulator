using System;
using System.Text.Json;

namespace Gmulator.Shared;
public class Cheat
{
    public string Description { get; set; }
    public int Address { get; set; }
    public byte Compare { get; set; }
    public byte Value { get; set; }
    public int Type { get; set; }
    public int Bank { get; set; }
    public bool Enabled { get; set; }
    public string Codes { get; set; }
    public string Filename { get; private set; }

    public Cheat() { }
    public Cheat(string description, int address, byte compare, byte value, int type, bool enabled, string codes)
    {
        Description = description == "" ? "Cheat" : description;
        Address = address;
        Compare = compare;
        Value = value;
        Type = type;
        Enabled = enabled;
        Codes = codes;
        if (type == GameShark)
            Bank = compare;
    }

    public class RawCode(int address, byte compare, byte value, int type)
    {
        public int Address { get; set; } = address;
        public byte Compare { get; set; } = compare;
        public byte Value { get; set; } = value;
        public int Type { get; set; } = type;
    }

    public static (int, byte, byte, int, int) DecryptCode(string c, Emulator Emu)
    {
        switch (Emu?.Console)
        {
            case GbcConsole:
            {
                if (c.Length == 9)
                {
                    if (c.Replace("\r", "").ToUpper().All("01234567890ABCDEF".Contains))
                    {
                        var addr = Convert.ToUInt16($"{c[5]}{c[2]}{c[3]}{c[4]}", 16) ^ 0xf000;
                        byte cmp = (byte)(Convert.ToByte($"{c[6]}{c[8]}", 16).Ror(2) ^ 0xba);
                        var val = Convert.ToByte(c[..2], 16);
                        return (addr, cmp, val, GameGenie, GbcConsole);
                    }
                }
                else if (c.Length == 8)
                {
                    var addr = Convert.ToUInt16($"{c.Substring(6, 2)}{c.Substring(4, 2)}", 16);
                    var cmp = Convert.ToByte($"{c[..2]}", 16);
                    var val = Convert.ToByte($"{c.Substring(2, 2)}", 16);
                    return (addr, cmp, val, GameShark, GbcConsole);
                }
                return (-1, 0, 0, -1, GbcConsole);
            }
            case NesConsole:
            {
                if (c.Length == 8)
                {
                    if (c.Replace("\r", "").ToUpper().All("APZLGITYEOXUKSVN".Contains))
                    {
                        List<int> r = [];
                        foreach (var l in c)
                            r.Add("APZLGITYEOXUKSVN".IndexOf(l));

                        var addr = (r[3] & 7) << 12 | (r[5] & 7) << 8 |
                        (r[2] & 7) << 4 | r[4] & 7 | (r[4] & 8) << 8 |
                        (r[1] & 8) << 4 | r[3] & 8 | 0x8000;
                        var val = (byte)((r[1] & 7) << 4 | (r[0] & 8) << 4 |
                        r[0] & 7);
                        byte cmp = 0;
                        if (c.Length == 8)
                        {
                            cmp = (byte)((r[7] & 7) << 4 | (r[6] & 8) << 4 |
                            r[6] & 7 | r[5] & 8);
                            val += (byte)(r[7] & 8);
                        }
                        else
                            val += (byte)(r[5] & 8);

                        return (addr, cmp, val, GameGenie, NesConsole);
                    }
                }
                else if (c.Length == 7)
                {
                    var addr = Convert.ToInt16(c[..4], 16);
                    var val = Convert.ToByte(c.Substring(5, 2), 16);
                    return (addr, 0, val, GameShark, NesConsole);
                }
                break;
            }
            case SnesConsole:
            {
                if (c.StartsWith("7E") || c.StartsWith("7F"))
                {
                    var addr = Convert.ToInt32(c[..6], 16);
                    var val = Convert.ToByte(c.Substring(7, 2), 16);
                    return (addr, 0, val, GameShark, SnesConsole);
                }
                else if (c.Contains(':'))
                {
                    var addr = Convert.ToInt32(c[..6], 16);
                    var val = Convert.ToByte(c.Substring(7, 2), 16);
                    return (addr, 0, val, GameGenie, SnesConsole);
                }
                else if (c.Replace("\r", "").ToUpper().All("0123456789ABCDEF".Contains))
                {
                    List<byte> r = [];
                    int d = 0;
                    for (int i = 0; i < c.Length; i++)
                    {
                        d <<= 4;
                        d |= ((byte)"DF4709156BC8A23E".IndexOf(c[i]));
                    }

                    byte val = (byte)(d >> 24);
                    var addr = (d >> 10) & 0xc | (d >> 10) & 3;
                    var temp = (d >> 2) & 0xc | (d >> 2) & 0x3;
                    addr <<= 4; addr |= temp;
                    temp = (d >> 20) & 0xf;
                    addr <<= 4; addr |= temp;
                    temp = (d << 2) & 0xc | (d >> 14) & 3;
                    addr <<= 4; addr |= temp;
                    temp = (d >> 16) & 0xf;
                    addr <<= 4; addr |= temp;
                    temp = (d >> 6) & 0xc | (d >> 6) & 3;
                    addr <<= 4; addr |= temp;

                    return (addr, 0, val, GameGenie, SnesConsole);
                }
                break;
            }
        }
        return (-1, 0, 0, -1, NoConsole);
    }

    public void ReloadCheats(Emulator Emu) => Load(Emu, true);
    public void Load(Emulator Emu, bool reloadlibreto, string chtname = "")
    {
        Emu.Cheats?.Clear();
        var name = Filename = chtname == "" ? Emu.GameName : chtname;
        var json = @$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
        var libretrocht = @$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(name)}.cht";
        if (File.Exists(json) && !reloadlibreto)
        {
            var res = JsonSerializer.Deserialize<List<Cheat>>(File.ReadAllText(json), GEmuJsonContext.Default.Options);
            foreach (var cht in res)
            {
                List<RawCode> rawcodes = [];
                foreach (var line in cht.Codes.Split("+"))
                {
                    var c = line.ReplaceLineEndings("").Replace("\r", "").Replace("-", "");
                    if (c == "")
                        continue;
                    (int addr, byte cmp, byte val, int type, int console) = DecryptCode(c, Emu);
                    rawcodes.Add(new(addr, cmp, val, type));
                }

                foreach (var r in rawcodes)
                {
                    if (!Emu.Cheats.ContainsKey(r.Address))
                        Emu.Cheats.Add(r.Address, new(cht.Description, r.Address, r.Compare, r.Value, r.Type, cht.Enabled, cht.Codes));
                }
            }
        }
        else
        {
            if (File.Exists(libretrocht))
            {
                var txt = File.ReadAllText(libretrocht).Split(["\n\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < txt.Length; i++)
                {
                    if (!txt[0].Contains("cheats =", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Notifications.Init("Cheat File is Not in Libretro Format");
                        return;
                    }

                    Cheat cht = new();
                    List<RawCode> rawcodes = [];
                    var lines = txt[i].Split("\n", StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < lines.Length; j++)
                    {
                        var beg = lines[j].IndexOf("= ") + 2;
                        cht.Description = lines[j][beg..].Replace(@"""", "");
                        cht.Codes = lines[j + 1][beg..].Replace(@"""", "");
                        cht.Enabled = true;
                        j += 2;
                    }

                    foreach (var line in cht.Codes.Split("+"))
                    {
                        var c = line.ReplaceLineEndings("").Replace("\r", "").Replace("-", "").Trim();
                        if (c == "")
                            continue;
                        (int addr, byte cmp, byte val, int type, int console) = DecryptCode(c, Emu);
                        rawcodes.Add(new(addr, cmp, val, type));
                    }

                    foreach (var r in rawcodes)
                    {
                        if (!Emu.Cheats.ContainsKey(r.Address))
                            Emu.Cheats.Add(r.Address, new(cht.Description, r.Address, r.Compare, r.Value, r.Type, true, cht.Codes));
                    }
                }
            }
        }

        if (Emu?.Cheats.Count > 0)
            Save(Emu.GameName, Emu?.Cheats);
    }

    public static void Save(string name, Dictionary<int, Cheat> Cheats)
    {
        if (Cheats == null || Cheats.Count == 0) return;
        var cht = Cheats.Values.DistinctBy(c => c.Description).ToList();
        var chtfile = @$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
        JsonSerializerOptions options = new() { WriteIndented = true };
        var json = JsonSerializer.Serialize(cht, GEmuJsonContext.Default.Options);
        File.WriteAllText(chtfile, json);
    }
}
