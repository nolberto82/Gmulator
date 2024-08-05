using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gmulator;
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
    [JsonIgnore]
    public Dictionary<int, Cheat> Cheats { get; set; }
    [JsonIgnore]
    public string Filename { get; private set; }
    [JsonIgnore]
    public int System { get; set; }

    public Cheat(string description, int address, byte compare, byte value, int type, bool enabled, string codes)
    {
        Description = description;
        Address = address;
        Compare = compare;
        Value = value;
        Type = type;
        Enabled = enabled;
        Codes = codes;
        if (type == GameShark)
            Bank = compare;
    }
    public Cheat() { }

    public record RawCode
    {
        public int Address { get; set; }
        public byte Compare { get; set; }
        public byte Value { get; set; }
        public int Type { get; set; }

        public RawCode(int address, byte compare, byte value, int type)
        {
            Address = address;
            Compare = compare;
            Value = value;
            Type = type;
        }
    }


    public void Init(Dictionary<int, Cheat> cheats, int system)
    {
        Cheats = cheats;
        System = system;
    }

    public void ConvertCodes(string cheatname, string cheats, ref string cheatout, bool add)
    {
        List<string> codes = new();
        List<RawCode> rawcodes = new();
        var input = cheats.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        int cheatype = input[0].Contains("-") ? GameGenie : GameShark;
        for (int i = 0; i < input.Count; i++)
        {
            var c = input[i].ReplaceLineEndings("").Replace("\r", "");
            if (c == "")
                continue;
            c = c.Replace("-", "").ReplaceLineEndings("");
            if (!add)
            {
                (int addr, byte cmp, byte val, int type) = DecryptCode(c);
                if (addr == -1)
                    continue;
                if (type == GameGenie)
                    cheatout += $"{addr:X4}:{cmp:X2}:{val:X2}\n";
                else if (type == GameShark)
                    cheatout += $"{addr:X4}:{val:X2}\n";
            }
            else
            {
                (int addr, byte cmp, byte val, int type) = DecryptCode(c);
                codes.Add($"{input[i]}\r\n");
                rawcodes.Add(new RawCode(addr, cmp, val, type));
            }
        }

        Cheat newcheat = new(cheatname, 0, 0, 0, 0, true, "");
        for (int i = 0; i < rawcodes.Count; i++)
        {
            var rc = rawcodes[i];
            var n = Cheats.Values.ToList().FindIndex(v => v.Address == rc.Address);
            if (n > -1)
            {
                Cheats[n].Description = cheatname;
                Cheats[n].Address = rawcodes[0].Address;
                Cheats[n].Value = rawcodes[0].Value;
                Cheats[n].Codes = string.Join("", codes.ToArray());
            }
            else
            {
                foreach (var r in rawcodes)
                {
                    if (!Cheats.ContainsKey(r.Address))
                        Cheats.Add(r.Address, new(cheatname, r.Address, r.Compare, r.Value, r.Type, false, string.Join("", codes.ToArray())));
                }
            }
        }
    }

    public (int, byte, byte, int) DecryptCode(string c)
    {
        if (System == GbcSystem)
        {
            if (c.Length == 9)
            {
                if (c.Replace("\r", "").ToUpper().All("01234567890ABCDEF".Contains))
                {
                    var addr = Convert.ToUInt16($"{c[5]}{c[2]}{c[3]}{c[4]}", 16) ^ 0xf000;
                    byte cmp = (byte)(Convert.ToByte($"{c[6]}{c[8]}", 16).Ror(2) ^ 0xba);
                    var val = Convert.ToByte(c[..2], 16);
                    return (addr, cmp, val, GameGenie);
                }
            }
            else if (c.Length == 8)
            {
                var addr = Convert.ToUInt16($"{c.Substring(6, 2)}{c.Substring(4, 2)}", 16);
                var cmp = Convert.ToByte($"{c.Substring(0, 2)}", 16);
                var val = Convert.ToByte($"{c.Substring(2, 2)}", 16);
                return (addr, cmp, val, GameShark);
            }
            return (-1, 0, 0, -1);
        }
        else if (System == NesSystem)
        {
            if (c.Length == 8)
            {
                if (c.Replace("\r", "").ToUpper().All("APZLGITYEOXUKSVN".Contains))
                {
                    List<int> r = new();
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

                    return (addr, cmp, val, GameGenie);
                }
            }
            else if (c.Length == 7)
            {
                var addr = Convert.ToInt16(c.Substring(0, 4), 16);
                var val = Convert.ToByte(c.Substring(5, 2), 16);
                return (addr, 0, val, GameShark);
            }
        }
        return (-1, 0, 0, -1);
    }

    public void ReloadCheats() => Load(Filename, true);
    public void Load(string name, bool reloadlibreto)
    {
        Cheats.Clear();
        Filename = name;
        var json = @$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
        var libretrocht = @$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(name)}.cht";
        if (File.Exists(json) && !reloadlibreto)
        {
            var res = JsonSerializer.Deserialize<List<Cheat>>(File.ReadAllText(json), GEmuJsonContext.Default.Options);
            foreach (var cht in res)
            {
                List<RawCode> rawcodes = new();
                foreach (var line in cht.Codes.Split("+"))
                {
                    var c = line.ReplaceLineEndings("").Replace("\r", "").Replace("-", "");
                    if (c == "")
                        continue;
                    (int addr, byte cmp, byte val, int type) = DecryptCode(c);
                    rawcodes.Add(new(addr, cmp, val, type));
                }

                foreach (var r in rawcodes)
                {
                    if (!Cheats.ContainsKey(r.Address))
                        Cheats.Add(r.Address, new(cht.Description, r.Address, r.Compare, r.Value, r.Type, cht.Enabled, cht.Codes));
                }
            }
        }
        else
        {
            if (File.Exists(libretrocht))
            {
                List<string> chtcodes = new();
                var txt = File.ReadAllText(libretrocht).Split(new string[] { "\n\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < txt.Length; i++)
                {
                    Cheat cht = new();
                    List<RawCode> rawcodes = new();
                    var lines = txt[i].Split("\n", StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < lines.Length; j++)
                    {
                        var beg = lines[j].IndexOf("= ") + 2;
                        cht.Description = lines[j].Substring(beg).Replace(@"""", "");
                        cht.Codes = lines[j + 1].Substring(beg).Replace(@"""", "");
                        cht.Enabled = true;
                        j += 2;
                    }

                    rawcodes = new();
                    foreach (var line in cht.Codes.Split("+"))
                    {
                        var c = line.ReplaceLineEndings("").Replace("\r", "").Replace("-", "").Trim();
                        if (c == "")
                            continue;
                        (int addr, byte cmp, byte val, int type) = DecryptCode(c);
                        rawcodes.Add(new(addr, cmp, val, type));
                    }

                    foreach (var r in rawcodes)
                    {
                        if (!Cheats.ContainsKey(r.Address))
                            Cheats.Add(r.Address, new(cht.Description, r.Address, r.Compare, r.Value, r.Type, true, cht.Codes));
                    }
                }
            }
        }
    }

    public void Save(string name)
    {
        if (Cheats == null || Cheats.Count == 0) return;
        var cht = Cheats.Values.DistinctBy(c => c.Description).ToList();
        var chtfile = @$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
        JsonSerializerOptions options = new() { WriteIndented = true };
        var json = JsonSerializer.Serialize(cht, GEmuJsonContext.Default.Options);
        File.WriteAllText(chtfile, json);
    }
}
