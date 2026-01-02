using Gmulator.Ui;
using ImGuiNET;
using Raylib_cs;
using System.Numerics;
using System.Text.Json;
using static Gmulator.Interfaces.IMmu;
using static Gmulator.Shared.Cheat;

namespace Gmulator.Shared
{
    public class Emulator
    {
        public string GameName { get; set; }
        public string LastName { get; set; }
        public List<MemoryHandler> MemoryHandlers { get; set; }
        public ReadDel[] Read { get; set; }
        public WriteDel[] Write { get; set; }
        public RamType[] RamTypes { get; set; }

        public Config Config { get; set; }
        public RenderTexture2D Screen { get; set; }
        public Dictionary<int, Cheat> Cheats { get; set; } = [];
        public Cheat Cheat { get; set; } = new();
        public LuaApi LuaApi { get; set; }
        public DebugWindow DebugWindow { get; set; }
        public CheatConverter CheatConverter { get; set; }
        public DebugState State { get; set; }
        public bool Run { get; set; }
        public bool FastForward { get; set; }
        public bool Debug { get; set; }
        public bool IsScreenWindow { get; set; }
        public bool IsDeck { get; set; }
        public bool[] Buttons { get; set; }
        public Vector2 Dimensions { get; set; } = new(GbWidth, GbHeight);
        public object StateLock { get; set; } = new object();
        public SortedDictionary<int, Breakpoint> Breakpoints { get; set; } = [];

        public enum StateResult
        {
            Success, Failed, Mismatch
        }

        public Emulator() { }

        public Emulator(int size)
        {
            Read = new ReadDel[size];
            Write = new WriteDel[size];
            RamTypes = new RamType[size];

            for (int i = 0; i < Read.Length; i++)
            {
                Read[i] = (int a) => 0;
                Write[i] = (int a, int v) => { };
                RamTypes[i] = RamType.None;
            }

            MemoryHandlers = new();
            for (int i = 0; i < size; i++)
                MemoryHandlers.Add(new(0, 0, 0, 0, 0, (int a) => 0, (int a, int v) => { }, RamType.None));
        }

        public void Init(int width, int height, int system, float menuheight, ImFontPtr[] imguifont, Font raylibfont)
        {
            Screen = Raylib.LoadRenderTexture(width, height);
            Dimensions = new(width, height);
            LuaApi = new(Screen.Texture, imguifont, raylibfont, menuheight, Debug);
        }

        public virtual void LuaMemoryCallbacks() { }

        public virtual void Reset(string name, bool reset, uint[] pixels)
        {
#if DEBUG
            Debug = true;
#endif
            if (!Debug)
                State = DebugState.Running;
            else
                State = DebugState.Break;
            LuaApi?.SetDebug(Debug);
            if (name != "")
                GameName = name;

            UpdateTexture(Screen.Texture, pixels);
        }

        public virtual void SetState(DebugState v)
        {
            State = v;
            if (v == DebugState.Running || (v == DebugState.StepOverMain && v == DebugState.StepOverSpc))
                Run = true;
        }

        public virtual void RunFrame(bool opened) { }

        public virtual void Render(float MenuHeight)
        {
            var width = Raylib.GetScreenWidth();
            var height = Raylib.GetScreenHeight();
            var texwidth = Dimensions.X;
            var texheight = Dimensions.Y;
            var scale = Math.Min(width / texwidth, height / texheight);
            var posx = (int)((width - texwidth * scale) / 2);
            var posy = (int)(((height - texheight * scale) / 2) + MenuHeight);

            IsScreenWindow = false;
            if (Debug)
            {
                DebugWindow?.Draw(Screen.Texture);
                if (DebugWindow != null)
                    IsScreenWindow = DebugWindow.IsScreenWindow;
            }
            else
            {
                if (Raylib.IsWindowFocused())
                    IsScreenWindow = true;

                Raylib.DrawTexturePro(
                    Screen.Texture,
                    new Rectangle(0, 0, texwidth, texheight),
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
            fixed (uint* pixels = &buffer[0])
                Raylib.UpdateTexture(texture, pixels);
        }

        public void UpdateScreen(uint[] buffer)
        {
            UpdateTexture(Screen.Texture, buffer);
        }

        public virtual void SetMemory(int bank_s, int bank_e, int addr_s, int addr_e, int mask, ReadDel read, WriteDel write, RamType type, int add)
        {
            for (int i = bank_s; i <= bank_e; i++)
            {
                for (int j = addr_s; j <= addr_e; j += add)
                {
                    int a = add == 0x1000 ? i << 4 | (j >> 12) : j;
                    MemoryHandlers[a].AddrStart = addr_s;
                    MemoryHandlers[a].AddrEnd = addr_e;
                    MemoryHandlers[a].BankStart = bank_s;
                    MemoryHandlers[a].BankEnd = bank_e;
                    MemoryHandlers[a].Read = read;
                    MemoryHandlers[a].Write = write;
                    MemoryHandlers[a].Mask = mask;
                    MemoryHandlers[a].Type = type;
                }
            }
        }

        public virtual void CheckAccess(int a, int v, RamType type, int mask, bool write)
        {
            if (Debug && DebugWindow.AccessCheck(a, v, type, mask, write))
                State = DebugState.Break;
        }

        public T GetConsole<T>()
        {
            return (T)Convert.ChangeType(this, typeof(T));
        }

        public virtual void Update() { }

        public virtual void Input() { }

        public virtual void Close() { }

        public void ConvertCodes(Cheat cheat, ref string cheatout, bool add)
        {
            string cheatname = cheat.Description;
            string cheats = cheat.Codes;
            if (cheats == "" || cheats == "\r\n") return;
            List<string> codes = [];
            List<RawCode> rawcodes = [];
            cheats = cheats.Replace(" ", "");
            var input = cheats.Split(["\r\n", "\r", "\n", "+"], StringSplitOptions.RemoveEmptyEntries).ToList();
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
                    (int addr, byte cmp, byte val, int type, int console) = Cheat.DecryptCode(c, this);
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
                    (int addr, byte cmp, byte val, int type, int console) = Cheat.DecryptCode(c, this);
                    if (addr > -1)
                    {
                        codes.Add($"{input[i]}\r\n");
                        rawcodes.Add(new(addr, cmp, val, type, true));
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
                            Cheats.Add(r.Address, new(cheatname, r.Address, r.Value, r.Compare, r.Type, true, string.Join("", codes.ToArray())));
                    }
                }
            }
            SaveCheats(GameName);
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
                        Notifications.Init("Cheat File is Not in Libretro Format");
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
                        (int addr, byte cmp, byte val, int type, int console) = Cheat.DecryptCode(c, this);
                        rawcodes.Add(new(addr, cmp, val, type, cht.Enabled));
                    }

                    foreach (var r in rawcodes)
                    {
                        if (!Cheats.ContainsKey(r.Address))
                            Cheats.Add(r.Address, new(cht.Description, r.Address, r.Value, r.Compare, r.Type, r.Enabled, cht.Codes));
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
                    Breakpoints.Add(bp.Addr, bp);
                }
            }
        }

        public virtual void SaveBreakpoints(string name)
        {
            if (!Directory.Exists(DebugDirectory)) return;
            if (name == null || name == "") return;
            var bps = Breakpoints.Values.DistinctBy(c => c.Addr).ToList();
            var file = @$"{DebugDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
            var json = JsonSerializer.Serialize(bps, GEmuJsonContext.Default.Options);
            File.WriteAllText(file, json);
        }
    }

    public class MemoryHandler
    {
        public int AddrStart { get; set; }
        public int AddrEnd { get; set; }
        public int BankStart { get; set; }
        public int BankEnd { get; set; }
        public ReadDel Read { get; set; }
        public WriteDel Write { get; set; }
        public int Mask { get; set; }
        public RamType Type { get; set; }

        public MemoryHandler() { }
        public MemoryHandler(int bankStart, int bankEnd, int addrStart, int addrEnd, int mask, ReadDel read, WriteDel write, RamType type)
        {
            AddrStart = addrStart;
            AddrEnd = addrEnd;
            BankStart = bankStart;
            BankEnd = bankEnd;
            Read = read;
            Write = write;
            Mask = mask;
            Type = type;
        }
    }
}
