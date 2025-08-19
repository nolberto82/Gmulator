using static Gmulator.Shared.Cheat;

namespace Gmulator.Shared;
public class CheatConverter
{
    public static Dictionary<int, Cheat> Cheats { get; private set; }
    public static Cheat Cheat { get; private set; }

    public CheatConverter(Emulator Emu)
    {
        Cheats = Emu.Cheats;
    }

    public static void ConvertCodes(string cheatname, string cheats, ref string cheatout, bool add, Emulator emu)
    {
        if (cheats == "" || cheats == "\r\n") return;
        List<string> codes = [];
        List<RawCode> rawcodes = [];
        cheats = cheats.Replace(" ", "");
        var input = cheats.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries).ToList();
        if (input.Count == 0) return;
        int cheatype = input[0].Contains('-') ? GameGenie : ProAction;
        cheatout = string.Empty;
        for (int i = 0; i < input.Count; i++)
        {
            var c = input[i].ReplaceLineEndings("").Replace("\r", "");
            if (c == "")
                continue;
            c = c.Replace("-", "").ReplaceLineEndings("");
            if (!add)
            {
                (int addr, byte cmp, byte val, int type, int console) = DecryptCode(c, emu);
                if (addr == -1)
                    continue;
                if (type == GameGenie)
                {
                    if (console != SnesConsole)
                        cheatout += $"{addr:X4}:{cmp:X2}:{val:X2}\n";
                    else
                        cheatout += $"{addr:X6}:{val:X2}\n";
                }

                else if (type == ProAction)
                    cheatout += $"{addr:X4}:{val:X2}\n";
            }
            else
            {
                (int addr, byte cmp, byte val, int type, int console) = DecryptCode(c, emu);
                if (addr > -1)
                {
                    codes.Add($"{input[i]}\r\n");
                    rawcodes.Add(new RawCode(addr, cmp, val, type));
                }
            }
        }

        Cheat newcheat = new(cheatname, 0, 0, 0, 0, true, "");
        for (int i = 0; i < rawcodes.Count; i++)
        {
            var rc = rawcodes[0];
            var n = Cheats.Values.ToList().FindIndex(v => v.Address == rc.Address);
            var res = Cheats.TryGetValue(rc.Address, out var ch);
            if (res)
            {
                ch.Description = cheatname;
                ch.Address = rawcodes[0].Address;
                ch.Value = rawcodes[0].Value;
                ch.Codes = string.Join("", codes.ToArray());
            }
            else
            {
                foreach (var r in rawcodes)
                {
                    if (!Cheats.ContainsKey(r.Address))
                        Cheats.Add(r.Address, new(cheatname, r.Address,r.Value, r.Compare, r.Type, true, string.Join("", codes.ToArray())));
                }
            }
        }
    }

    public static void Save(string name)
    {
        if (Cheats == null || Cheats.Count == 0) return;
        var cheats = Cheats.Values.DistinctBy(c => c.Description).ToList();
        var chtfile = @$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(name)}_cheats.cht";
        using var sw = new StreamWriter(new FileStream(chtfile, FileMode.OpenOrCreate, FileAccess.Write));
        sw.Write($"cheats = {cheats.Count}\n");
        sw.Write("\n");
        for (int i = 0; i < cheats.Count; i++)
        {
            Cheat cht = cheats[i];
            sw.Write($"cheat{i}_desc = \"{cht.Description}\"\n");
            sw.Write($"cheat{i}_code = \"{cht.Codes}\"\n");
            sw.Write($"cheat{i}_enable = \"{cht.Enabled.ToString().ToLower()}\"\n");
            sw.Write("\n");
        }
    }
}
