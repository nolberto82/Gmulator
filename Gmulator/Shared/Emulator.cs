using Gmulator.Interfaces;
using Gmulator.Shared.LuaScript;
using ImGuiNET;
using rlImGui_cs;
using System.Numerics;
using System.Text.Json;
using static Gmulator.Interfaces.IMmu;
using static Gmulator.Shared.Cheat;

namespace Gmulator.Shared;

public class Emulator
{
    public string GameName { get; set; }
    public string LastName { get; set; }
    public ReadDel[] Read { get; set; }
    public WriteDel[] Write { get; set; }
    public RamType[] RamTypes { get; set; }
    public int Offset { get; set; }
    public Config Config { get; set; }
    public RenderTexture2D Screen { get; set; }
    public Dictionary<(int, int), Cheat> Cheats { get; set; } = [];
    public Cheat Cheat { get; set; } = new();
    public LuaManager Lua { get; set; }
    public DebugWindow DebugWindow { get; set; }
    public CheatConverter CheatConverter { get; set; }
    public IConsole Console { get; set; }
    public bool Run { get; set; }
    public bool FastForward { get; set; }
    public bool Debug { get; set; }
    public bool IsScreenWindow { get; set; }
    public bool IsDeck { get; set; }
    public bool[] Buttons { get; set; }
    public Vector2 Dimensions { get; set; } = new(GbWidth, GbHeight);
    public object StateLock { get; set; } = new object();
    public List<Breakpoint> Breakpoints { get; set; } = [];
    public int SystemType { get; set; }

    public enum StateResult
    {
        Success, Failed, Mismatch
    }

    public Emulator()
    {
    }


    public void Init(int width, int height, float menuheight, ImFontPtr[] imguifont, Font raylibfont, int system)
    {
        Screen = Raylib.LoadRenderTexture(width, height);
        Dimensions = new(width, height);
        SystemType = system;
        Lua = new(Screen, imguifont, raylibfont, menuheight, Debug);
    }

    public virtual void LuaMemoryCallbacks() { }

    public virtual void Reset(string name, bool reset)
    {
#if DEBUG
        Debug = true;
#endif
        if (!Debug)
            Console.EmuState = DebugState.Running;
        else
            Console.EmuState = DebugState.Break;
        Lua?.SetDebug(Debug);
        if (name != "")
            GameName = name;
    }

    public virtual void RunFrame(bool opened) { }

    public virtual void Render(float MenuHeight)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        IsScreenWindow = false;
        if (Debug)
        {
            DebugWindow?.Draw(Screen.Texture);
            if (DebugWindow != null)
                IsScreenWindow = DebugWindow.IsScreenWindow;
        }
        else
        {
            var texwidth = Dimensions.X;
            var texheight = Dimensions.Y;
            var scale = Math.Min(width / texwidth, height / texheight);
            var posx = (int)((width - texwidth * scale) / 2);
            var posy = (int)(((height - texheight * scale) / 2) + MenuHeight);

            if (Raylib.IsWindowFocused())
                IsScreenWindow = true;

            Raylib.BeginTextureMode(Screen);
            Lua?.Update(false);
            Raylib.EndTextureMode();

            Raylib.DrawTexturePro(
                Screen.Texture,
                new Rectangle(0, 0, texwidth, -texheight),
                new Rectangle(posx, posy,
                texwidth * scale,
                texheight * scale + MenuHeight),
                Vector2.Zero, 0, Color.White);
            Notifications.Render(posx, (int)MenuHeight, (int)(texwidth * scale), Debug);
        }
        Raylib.DrawFPS(width - 80, (int)(5 + MenuHeight));
    }

    public virtual unsafe void UpdateTexture(Texture2D texture, uint[] buffer)
    {
        if (buffer == null) return;
        if (!Debug)
        {
            //Array.Reverse(buffer);
            uint[] flippedBuffer = new uint[texture.Width * texture.Height];
            for (int x = 0; x < texture.Width; x++)
            {
                for (int y = 0; y < texture.Height; y++)
                {
                    flippedBuffer[y * texture.Width + x] = buffer[(texture.Height - 1 - y) * texture.Width + x];
                }
            }

            fixed (uint* pixels = &flippedBuffer[0])
                Raylib.UpdateTexture(texture, pixels);
        }
        else
        {
            fixed (uint* pixels = &buffer[0])
                Raylib.UpdateTexture(texture, pixels);
        }
    }

    public void UpdateScreen(uint[] buffer) => UpdateTexture(Screen.Texture, buffer);

    public virtual void Update() { }

    public virtual void Input() { }

    public virtual void Close() { }

    public string ConvertCodes(string description, string cheats, bool add)
    {
        if (cheats == "" || cheats == "\r\n" || cheats == null) return string.Empty;
        List<string> codes = [];
        List<RawCode> rawcodes = [];
        cheats = cheats.Replace(" ", "");
        var input = cheats.Split(["\r\n", "\r", "\n", "+"], StringSplitOptions.RemoveEmptyEntries).ToList();
        if (input.Count == 0) return string.Empty;
        int cheatype = input[0].Contains('-') ? GameGenie : ProAction;
        string cheatout = string.Empty;
        for (int i = 0; i < input.Count; i++)
        {
            var c = input[i].ReplaceLineEndings("").Replace("\r", "");
            if (c == "")
                continue;
            c = c.Replace("-", "").ReplaceLineEndings("");
            if (!add)
            {
                (int addr, byte cmp, byte val, int type, int console) = Cheat.DecryptCode(c, SystemType);
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
                (int addr, byte cmp, byte val, int type, int console) = Cheat.DecryptCode(c, SystemType);
                if (addr > -1)
                {
                    codes.Add($"{input[i]}\r\n");
                    rawcodes.Add(new(addr, cmp, val, type, true));
                }
            }
        }

        if (cheatout != string.Empty)
            return cheatout;

        Cheat newcheat = new(description, 0, 0, 0, 0, true, "");
        for (int i = 0; i < rawcodes.Count; i++)
        {
            var rc = rawcodes[0];
            var n = Cheats.Values.ToList().FindIndex(v => v.Address == rc.Address);
            var res = Cheats.TryGetValue((rc.Address, rc.Address80), out var ch);
            if (res)
            {
                ch.Description = description;
                ch.Address = rawcodes[0].Address;
                ch.Value = rawcodes[0].Value;
                ch.Codes = string.Join("", codes.ToArray());
            }
            else
            {
                foreach (var r in rawcodes)
                {
                    if (!Cheats.ContainsKey((r.Address, r.Address80)))
                        Cheats.Add((r.Address, r.Address80), new(description, r.Address, r.Value, r.Compare, r.Type, true, string.Join("", codes.ToArray())));
                }
            }
        }
        SaveCheats(GameName);
        return string.Empty;
    }

    public virtual void LoadCheats(string filename)
    {
        if (filename.Contains("alttpr - "))
            filename = "alttpr";

        Cheats?.Clear();
        var name = filename == "" ? GameName : filename;
        name = @$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(name)}";
        var libretrocht = File.Exists($"{name}.cht") ? $"{name}.cht" : "";

        if (libretrocht != "")
        {
            var txt = File.ReadAllText(libretrocht).Split(["\n\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < txt.Length; i++)
            {
                if (!txt[0].Contains("cheats =", StringComparison.InvariantCultureIgnoreCase))
                {
                    Notifications.Init("Cheat File is not in Libretro Format");
                    return;
                }

                Cheat cht = new();
                List<RawCode> rawcodes = [];
                var lines = txt[i].Split("\n", StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 3) continue;
                for (int j = 0; j < lines.Length; j++)
                {
                    var beg = lines[j].IndexOf("= ") + 2;
                    if (beg == -1) break;
                    cht.Description = lines[j][beg..].Replace(@"""", "");
                    beg = lines[j + 1].IndexOf("= ") + 2;
                    if (beg == -1) break;
                    cht.Codes = lines[j + 1][beg..].Replace(@"""", "");
                    beg = lines[j + 2].IndexOf("= ") + 2;
                    if (beg == -1) break;
                    cht.Enabled = Convert.ToBoolean(lines[j + 2][beg..].Replace(@"""", ""));
                    j += 2;
                }

                foreach (var line in cht.Codes.Split("+"))
                {
                    var c = line.ReplaceLineEndings("").Replace("\r", "").Replace("-", "").Trim();
                    if (c == "")
                        continue;
                    (int addr, byte cmp, byte val, int type, int console) = Cheat.DecryptCode(c, SystemType);
                    rawcodes.Add(new(addr, cmp, val, type, cht.Enabled));
                }

                foreach (var r in rawcodes)
                {
                    if (!Cheats.ContainsKey((r.Address, r.Address80)))
                        Cheats.Add((r.Address, r.Address80), new(cht.Description, r.Address, r.Value, r.Compare, r.Type, r.Enabled, cht.Codes));
                }
            }
        }

        if (name != "Cheats/alttpr" && Cheats.Count > 0)
            SaveCheats(name);
    }

    public virtual void SaveCheats(string filename)
    {
        if (Cheats == null || Cheats.Count == 0) return;
        var cheats = Cheats.Values.DistinctBy(c => c.Description).ToList();
        var chtfile = Path.GetFullPath(@$"{CheatDirectory}/{Path.GetFileNameWithoutExtension(filename)}.cht");
        using var sw = new StreamWriter(new FileStream(chtfile, FileMode.Create, FileAccess.Write, FileShare.Write));
        sw.Write($"cheats = {cheats.Count}\n");
        sw.Write("\n");
        for (int i = 0; i < cheats.Count; i++)
        {
            Cheat cht = cheats[i];
            string codes = cht.Codes.Replace("\r\n", "+");
            if (codes.Length > 0 && codes[^1] == '+')
                codes = codes.TrimEnd('+');
            sw.Write($"cheat{i}_desc = \"{cht.Description}\"\n");
            sw.Write($"cheat{i}_code = \"{codes}\"\n");
            sw.Write($"cheat{i}_enable = \"{cht.Enabled.ToString().ToLower()}\"\n");
            sw.Write("\n");
        }
    }

    public virtual void SaveState(int slot, StateResult res)
    {
        switch (res)
        {
            case StateResult.Success:
                Notifications.Init($"State {slot} Saved Successfully");
                break;
            case StateResult.Failed:
                Notifications.Init($"Error Saving Save State {slot}");
                break;
        }
    }

    public virtual void LoadState(int slot, StateResult res)
    {
        switch (res)
        {
            case StateResult.Success:
                Notifications.Init($"State {slot} Loaded Successfully");
                break;
            case StateResult.Failed:
                Notifications.Init($"Save State {slot} Doesn't Exist");
                break;
            case StateResult.Mismatch:
                Notifications.Init($"Save State {slot} Version Mismatch");
                break;
        }
    }

    public virtual void LoadBreakpoints(string name)
    {
        Breakpoints.Clear();
        var file = @$"{DebugDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
        if (File.Exists(file))
        {
            var res = JsonSerializer.Deserialize<List<Breakpoint>>(File.ReadAllText(file), GEmuJsonContext.Default.Options);
            foreach (var bp in res)
            {
                Breakpoints.Add(bp);
            }
        }
    }

    public virtual void SaveBreakpoints(string name)
    {
        if (!Directory.Exists(DebugDirectory)) return;
        if (name == null || name == "") return;
        var bps = Breakpoints.DistinctBy(c => c.Addr).ToList();
        var file = @$"{DebugDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
        var json = JsonSerializer.Serialize(bps, GEmuJsonContext.Default.Options);
        File.WriteAllText(file, json);
    }
}

public class MemoryHandler(int mask, IMmu.ReadDel read, IMmu.WriteDel write, RamType type)
{
    public int Offset { get; set; }
    public int Mask { get; set; } = mask;
    public byte[] Ram { get; set; }
    public ReadDel Read { get; set; } = read;
    public WriteDel Write { get; set; } = write;
    public RamType Type { get; set; } = type;
    public Func<int, int> ReadByte { get; set; }
}
