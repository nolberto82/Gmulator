using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using System.Data;
using System.Diagnostics;
using Font = Raylib_cs.Font;

namespace Gmulator.Ui;

public abstract class Gui
{
    public const string FontName = "Assets/naga10.ttf";
    public const int MaxTabs = 6;

    public const int ScrGames = 0;
    public const int ScrCheats = 1;
    public const int ScrLua = 2;
    public const int ScrOptions = 3;
    public const int ScrBrowser = 4;
    public const int ScrMain = 5;

    public LuaApi LuaApi { get; private set; }
    private Audio Audio { get; set; }
    private Cheat Cheat { get; set; }
    public float MenuHeight { get; set; }
    public RenderTexture2D Screen { get; private set; }
    public RenderTexture2D MenuTarget { get; set; }
    public Emulator Emulator { get; private set; } = new();
    public ImFontPtr[] DebugFont { get; private set; }
    public Font GuiFont { get; private set; }

    public const int FontSize = 28;

    public ulong FrameCounter { get; set; }
    public int TabIndex { get; set; }
    public int[] SelOption { get; set; } = new int[MaxTabs];
    public int[] OldTotal { get; set; } = new int[MaxTabs];
    public int[] MenuScroll { get; set; } = new int[MaxTabs];

    public Config Config { get; set; }
    public List<FileDetails> GameFiles { get; set; } = [];
    public List<FileDetails> CheatFiles { get; set; } = [];
    public List<FileDetails> LuaFiles { get; set; } = [];

    public List<Option> Options { get; set; } = [];
    public string CurrentName { get; set; }
    public string PreviousName { get; set; }
    public Action<string> Reset { get; set; }
    public bool OpenDialog { get; set; }
    public bool CheatDialog { get; set; }
    public bool ToggleAllCheats { get; set; }
    public bool DeleteFileMode { get; set; }
    public bool Opened;

    public string WorkingDirectory { get; set; }
    public string[] FileExtensions { get; set; }

    public virtual void Run()
    { }

    public virtual void Render()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F12))
            Raylib.TakeScreenshot($"screenshot{DateTime.Now.Ticks}.png");
    }

    public virtual void Update(bool isdeck)
    {
        switch (TabIndex)
        {
            case ScrMain:
                if (Raylib.IsGamepadButtonPressed(0, BtnB))
                {
                    TabIndex = SelOption[ScrMain] switch
                    {
                        0 => ScrGames,
                        1 => ScrCheats,
                        2 => ScrLua,
                        3 => ScrOptions,
                        _ => CopyHacks(true),
                    };
                }
                break;
            case ScrGames:
                if (Raylib.IsGamepadButtonPressed(0, BtnB))
                    LoadGame(GameFiles[SelOption[ScrGames]].Name);
                else if (Raylib.IsGamepadButtonPressed(0, BtnX))
                    DeleteFileMode = !DeleteFileMode;
                break;
            case ScrCheats or ScrBrowser:
                if (Raylib.IsGamepadButtonPressed(0, BtnY) && Emulator?.GameName != "")
                    CheatDialog = !CheatDialog;

                if (Raylib.IsGamepadButtonPressed(0, BtnX) && Emulator?.GameName != "")
                    EnableAllCheats();

                if (TabIndex == ScrCheats)
                {
                    int j = 0;
                    var cheats = Emulator.Cheats.Values.ToList();
                    for (int i = 0; i < cheats.Count;)
                    {
                        var res = Emulator.Cheats.Values.ToList();
                        var cht = res.Where(c => c.Description == res[i].Description).ToList();
                        if (Raylib.IsGamepadButtonPressed(0, BtnB) && j == SelOption[ScrCheats])
                        {
                            ToggleCheat(cht, i);
                            break;
                        }
                        i += cht.Count;
                        j++;
                    }
                }
                ToggleAllCheats = false;
                break;
            case ScrLua:
                break;
            case ScrOptions:
                if (Options.Count == 0) break;
                Option o = Options[SelOption[ScrOptions]];
                if (o.Func == null)
                {
                    if (Raylib.IsGamepadButtonPressed(0, BtnB))
                        o.Action(isdeck);
                    break;
                }

                var old = o.Value;
                var v = o.Func(o);
                if (v != old)
                {
                    o.Value = v;
                    Config.FrameSkip = Options[0].Value;
                    Config.Volume = Options[1].Value;
                    Config.RotateAB = Options[2].Value;
                    Audio.SetVolume(Config.Volume);
                    Config.Save();
                }
                break;
        }
    }

    public virtual void Unload(bool isdeck)
    {
        Emulator?.Close();
        Emulator?.Config.Save();
        LuaApi?.Unload();
        Raylib.UnloadFont(GuiFont);
        Raylib.UnloadRenderTexture(Screen);
        Audio.Unload();
        Raylib.CloseWindow();
    }

    public virtual void Init(bool isdeck)
    {
        Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow | ConfigFlags.HighDpiWindow);
        Raylib.InitWindow(DeckWidth, DeckHeight, EmulatorName);
        Raylib.SetTargetFPS(60);
        Audio = new();

#if DEBUG || RELEASE
        Raylib.SetWindowSize(1400, 980);
        Raylib.SetWindowPosition(10, 30);
        Raylib.ClearWindowState(ConfigFlags.VSyncHint);
#if RELEASE
        Emulator.Debug = false;
#endif
#endif

#if DECKDEBUG
        Raylib.SetWindowSize(1400, 900);
        Raylib.SetWindowPosition(10, 30);
        Raylib.ClearWindowState(ConfigFlags.VSyncHint);
#endif

        var deckres = Raylib.GetMonitorWidth(0) == 1280 && Raylib.GetMonitorHeight(0) == 800;
        if (deckres && !Raylib.IsWindowMaximized())
            Raylib.SetWindowState(ConfigFlags.MaximizedWindow);

        if (isdeck)
        {
            MenuTarget = Raylib.LoadRenderTexture(1280, 800);

            if (File.Exists(FontName))
            {
                GuiFont = Raylib.LoadFont(FontName);
                Notifications.SetFont(null, GuiFont);
            }
        }
        else
        {
            rlImGui.Setup(true);
            GraphicsWindow.Init();
            var io = ImGui.GetIO();

            if (File.Exists(FontName))
            {
                DebugFont = [null, null];
                DebugFont[0] = io.Fonts.AddFontFromFileTTF(FontName, 23f);
                DebugFont[1] = io.Fonts.AddFontFromFileTTF(FontName, 15f);
                GuiFont = Raylib.LoadFont(FontName);
                rlImGui.ReloadFonts();
                Notifications.SetFont(DebugFont[0], GuiFont);
            }
        }

        Emulator?.Config = new();
        Config.CreateDirectories(isdeck);
        Emulator?.Config.Load();
        FileExtensions = [".gb", ".gbc", ".nes", ".sfc", ".smc", ".sms", ".gg"];
        CurrentName = PreviousName = "";

        if (File.Exists("Assets/GBC_1.png"))
            Raylib.SetWindowIcon(Raylib.LoadImage("Assets/GBC_1.png"));

        Cheat = new();
    }

    public void Open(Emulator emu)
    {
        if (Opened)
        {
            Opened = false;
            CheatDialog = false;
            DeleteFileMode = false;
            return;
        }

        Emulator = emu;
        Config = emu.Config;
        WorkingDirectory = Config.WorkingDir;
        Opened = true;
        OpenDialog = true;

        Options =
        [
            new("Frameskip", [Config.FrameSkip, 1, 1, 10], null, false, ChangeOption, null),
            new("Volume", [Config.Volume, 1, 0, 100], null, false, ChangeOption, null),
            new("Rotate AB Buttons", [Config.RotateAB, 1, 0, 1],["OFF","ON"], true, ChangeOption, null),
            //new("Copy Hacks", [0, 0, 0, 0], [""], true, null, CopyHacks),
        ];
    }

    private int ChangeOption(Option o)
    {
        var mousewheel = Raylib.GetMouseWheelMove();
        if (Raylib.IsGamepadButtonPressed(0, BtnLeft) || mousewheel < 0)
        {
            o.Value -= o.Add;
            if (o.Value <= o.Min)
                o.Value = o.Min;
        }
        else if (Raylib.IsGamepadButtonPressed(0, BtnRight) || mousewheel > 0)
        {
            o.Value += o.Add;
            if (o.Value > o.Max)
                o.Value = o.Max;
        }
        return o.Value;
    }

    public virtual void ResetGame(string name)
    {
        if (name != "")
        {
            switch (Path.GetExtension(name).ToLowerInvariant())
            {
                case ".gb" or ".gbc":
                    Gbc gbc = new();
                    Emulator = gbc;
                    Emulator.Init(GbWidth, GbHeight, GbcConsole, MenuHeight, DebugFont, GuiFont);
                    LuaApi = Emulator.LuaApi;
                    gbc.LuaMemoryCallbacks();
                    Audio.Init(GbcAudioFreq, 4096, 4096, 32);
                    break;
                case ".nes":
                    Nes nes = new();
                    Emulator = nes;
                    Emulator.Init(NesWidth, NesHeight, NesConsole, MenuHeight, DebugFont, GuiFont);
                    LuaApi = Emulator.LuaApi;
                    nes.LuaMemoryCallbacks();
                    Audio.Init(NesAudioFreq, 4096, 4096, 32);
                    break;
                case ".sfc" or ".smc":
                    Snes snes = new();
                    Emulator = snes;
                    Emulator.Init(SnesWidth, SnesHeight, SnesConsole, MenuHeight, DebugFont, GuiFont);
                    LuaApi = Emulator.LuaApi;
                    snes.LuaMemoryCallbacks();
                    Audio.Init(SnesAudioFreq, SnesMaxSamples / 2, SnesMaxSamples, 32);
                    break;
                default: return;
            }

            LuaApi.Init();
            Emulator.Reset(name, false);
            Emulator.Config = new();
            Emulator?.Config.Load();
            LuaApi?.Reset();
        }

        Emulator?.Config?.Load();

        string gameName = Emulator?.GameName;
        Cheat?.Load(Emulator, gameName);
        LuaApi?.Load(gameName);
    }

    public void DisplayFiles(List<FileDetails> list, int x, int y, int width, Font font)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var file = list[i];
            if (!file.IsDrive && file.IsFile)
            {
                DrawHighlight(x, y, width, i);
                Raylib.DrawTextEx(font, Path.GetFileName(file.Name), new(x, y), FontSize, 0, DeleteFileMode ? new(128, 0, 0, 255) : Color.White);
            }
            y += FontSize;
        }
    }

    public void DrawCheats(List<Cheat> cheats, int x, int y, int width)
    {
        int j = 0;
        for (int i = 0; i < cheats.Count;)
        {
            DrawHighlight(x, y, width, j);
            var cht = cheats.Where(c => c.Description == cheats[i].Description).ToList();
            if (cht != null)
            {
                var chtstatus = cht[0].Enabled ? "ON" : "OFF";
                var colorstatus = cht[0].Enabled ? Color.White : new(173, 173, 173, 255);
                if (cht[0].Description.Length > 60)
                    cht[0].Description = cht[0].Description.Substring(0, 60);
                Raylib.DrawTextEx(GuiFont, cht[0].Description, new(x, y), FontSize, 0, colorstatus);
                Raylib.DrawTextEx(GuiFont, $"{chtstatus,-3}", new(x + width - 45, y), FontSize, 0, colorstatus);
                i += cht.Count;
            }
            y += FontSize;
            j++;
        }
    }

    public void DrawOptions(List<Option> options, int x, int y, int width)
    {
        int j = 0;
        for (int i = 0; i < options.Count; i++)
        {
            DrawHighlight(x, y, width, j);
            Raylib.DrawTextEx(GuiFont, $"{options[i].Name}", new(x + 5, y), FontSize, 0, Color.White);
            if (options[i].Status != null)
                Raylib.DrawTextEx(GuiFont, $"{options[i].Status[options[i].Value],3}", new(x + width - 55, y), FontSize, 0, Color.White);
            else
                Raylib.DrawTextEx(GuiFont, $"{options[i].Value,3}", new(x + width - 55, y), FontSize, 0, Color.White);
            y += FontSize;
            j++;
        }
    }

    public void DrawHighlight(int x, int y, int width, int i)
    {
        if (i == SelOption[TabIndex])
            Raylib.DrawRectangle(x, y + 1, width - 1, FontSize - 1, new(0, 128, 0, 255));
    }

    public void Enumerate(string path)
    {
        DirectoryInfo di;
        if (!Directory.Exists(WorkingDirectory))
            WorkingDirectory = "C:";
        if (path == "")
        {
            foreach (var file in DriveInfo.GetDrives())
            {
                if (file.IsReady)
                    GameFiles.Add(new(file.Name, file.IsReady, false));
            }
            di = new(WorkingDirectory);
            foreach (var file in di.EnumerateDirectories())
                GameFiles.Add(new(file.FullName, false, false));
            GameFiles.Insert(0, new("..", false, false));
        }
        else
            di = new(path);

        foreach (var file in di.EnumerateFiles())
        {
            var ext = file.Extension.ToLower();
            if (ext == ".lua")
                LuaFiles.Add(new(file.FullName, false, true));
            else if (ext == ".cht")
                CheatFiles.Add(new(file.FullName, false, true));
            else if (FileExtensions.Contains(ext))
                GameFiles.Add(new(file.FullName, false, true));

        }
    }

    public static int CopyHacks(bool isdeck)
    {
        var src = @$"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\Downloads";
        var dst = @$"D:\MyEmulators2\SNES-master\Games";
        if (isdeck)
            dst = $@"{Environment.CurrentDirectory}\Roms";

        var psi = new ProcessStartInfo("cmd.exe")
        {
            FileName = "robocopy",
            Arguments = $"{src} {dst} /mov *.gb *.gbc *.nes *.sfc *.smc",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi);
        return ScrMain;
    }

    public void LoadGame(string filename)
    {
        Config?.Save();
        Opened = false;
        ResetGame(CurrentName = filename);
    }

    public void LoadCheat(string filename)
    {
        Emulator.Cheat.Load(Emulator, filename);
        CheatDialog = false;
    }

    public void ToggleCheat(List<Cheat> cht, int i)
    {
        if (cht == null) return;
        foreach (var c in cht)
            c.Enabled = !c.Enabled;
        CheatConverter.Save(CurrentName);
    }

    public void EnableAllCheats()
    {
        foreach (var c in Emulator.Cheats.Values)
            c.Enabled = true;
        CheatConverter.Save(CurrentName);
    }

    public readonly string[] MainEntries = ["Games", "Cheats", "Lua", "Options", "Copy Hacks"];

    public readonly Dictionary<int, Info[][]> TabInfo = new()
    {
        [ScrMain] = [
            [new("Cross", "Select File")],
            [new("", "")]
        ],
        [ScrGames] = [
            [new("Cross", "Select File"), new("Triangle", "Delete Mode")],
            [new("Cross", "Select File"), new("Triangle", "Delete Mode")]
        ],
        [ScrCheats] = [
            [new("Cross", "Select File"),new("Triangle", "Enable All Cheats"), new("Square", "Open Browser")],
            [new("Cross", "Select File"), new("Triangle", "Enable All Cheats"), new("Square", "Open Browser")],
        ],
        [ScrLua] = [
            [new("Cross", "Select File")],
            [new("Cross", "Select File")],
        ],
        [ScrOptions] = [
            [new("Mouse Wheel", "Change Options")],
            [new("Left/Right", "Change Options")]
        ],
        [ScrBrowser] = [
            [new("Cross", "Select File")],
            [new("Cross", "Select File")]
        ],
    };

    public record Info(string Button, string Description);

    public struct FileDetails(string name, bool isDrive, bool isFile)
    {
        public string Name = name;
        public bool IsDrive = isDrive;
        public bool IsFile = isFile;
        public bool IsDelete;
    }

    public class Option(string name, int[] values, string[] status, bool press, Func<Option, int> func, Action<bool> action)
    {
        public string Name { get; set; } = name;
        public int Value { get; set; } = values[0];
        public int Add { get; set; } = values[1];
        public int Min { get; set; } = values[2];
        public int Max { get; set; } = values[3];
        public string[] Status { get; set; } = status;
        public bool Press { get; } = press;
        public Func<Option, int> Func { get; set; } = func;
        public Action<bool> Action { get; set; } = action;
    }

    public class Status(string name, bool enabled)
    {
        public string Name { get; set; } = name;
        public bool Enabled { get; set; } = enabled;
    }

    public enum TabState
    {
        TabGames, TabCheats, TabLua, TabOptions, TabCheatsBrowser
    }
}
