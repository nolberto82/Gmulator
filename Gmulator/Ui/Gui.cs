using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using Gmulator.Shared.LuaScript;
using ImGuiNET;
using rlImGui_cs;
using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using Font = Raylib_cs.Font;

namespace Gmulator.Ui;

public abstract class Gui
{
    public const string FontName = "Assets/naga10.ttf";
    public const int MaxTabs = 6;

    public enum Tab : int
    {
        Games,
        Cheats,
        ChtBrowser,
        Lua,
        Options,
        About,
    }

    public LuaManager LuaApi { get; private set; }
    private Audio Audio { get; set; }
    private Cheat Cheat { get; set; }
    public Dictionary<(int, int), Cheat> Cheats => Emulator?.Cheats;
    public float MenuHeight { get; set; }
    public RenderTexture2D Screen { get; private set; }
    public ImFontPtr[] DebugFont { get; private set; }
    public Font GuiFont { get; private set; }

    public const int MenuFontSize = 28;

    public ulong FrameCounter { get; set; }
    public int DpadCounter { get; set; }
    public Tab TabIndex { get; set; }
    public int[] SelectedItem { get; set; } = new int[MaxTabs];
    public int[] OldTotal { get; set; } = new int[MaxTabs];
    public int[] MenuScroll { get; set; } = new int[MaxTabs];
    public int[] MaxItems { get; set; } = new int[MaxTabs];

    public Emulator Emulator { get; private set; }
    public List<FileDetails> GameFiles { get; set; } = [];
    public List<FileDetails> CheatFiles { get; set; } = [];
    public List<FileDetails> LuaFiles { get; set; } = [];

    public List<Option> Options { get; set; } = [];
    public string CurrentName { get; set; }
    public string PreviousName { get; set; }
    public bool OpenDialog { get; set; }
    public bool CheatDialog { get; set; }
    public bool DeleteFileMode { get; set; }
    public bool Opened;
    private string _gameName;
    private int _cheatTabIndex;
    public int CheatTabIndex { get => _cheatTabIndex; set => _cheatTabIndex = value; }
    public string WorkingDirectory { get; set; }
    public string[] FileExtensions { get; set; }

    public virtual void Run()
    { }

    public virtual void Draw()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F12))
            Raylib.TakeScreenshot($"screenshot{DateTime.Now.Ticks}.png");
    }

    public virtual void Update(bool isdeck)
    {
        if (Raylib.IsGamepadButtonPressed(0, GamepadButton.LeftTrigger2))
            Open(Emulator.Config);

        if (!Opened) return;

        int maxgamesview = Raylib.GetScreenHeight() / MenuFontSize;
        FrameCounter++;

        bool newDownPressed = Raylib.IsGamepadButtonDown(0, BtnDown);
        bool newUpPressed = Raylib.IsGamepadButtonDown(0, BtnUp);
        bool newLeftPressed = Raylib.IsGamepadButtonDown(0, BtnLeft);
        bool newRightPressed = Raylib.IsGamepadButtonDown(0, BtnRight);
        bool oldDownPressed = Raylib.IsGamepadButtonPressed(0, BtnDown);
        bool oldUpPressed = Raylib.IsGamepadButtonPressed(0, BtnUp);

        if (newDownPressed || newUpPressed || newLeftPressed || newRightPressed)
        {
            DpadCounter--;
            if (DpadCounter < 0)
                DpadCounter = 3;
        }
        else
            DpadCounter = 10;


        if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceUp, false) && TabIndex == Tab.Games)
            DeleteFileMode = !DeleteFileMode;
        else if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceRight, false) && TabIndex == Tab.Games)
            CopyHacks(isdeck);

        if (ImGui.IsKeyPressed(ImGuiKey.GamepadL1, false))
            TabIndex = (TabIndex - 1) < 0 ? Tab.About : TabIndex - 1;
        else if (ImGui.IsKeyPressed(ImGuiKey.GamepadR1, false))
            TabIndex = (TabIndex + 1) > Tab.About ? Tab.Games : TabIndex + 1;

        switch (TabIndex)
        {
            case Tab.Options:
                if (Options.Count == 0) break;
                Option o = Options[SelectedItem[(int)Tab.Options]];
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
                    var config = Emulator.Config;
                    config.FrameSkip = Options[0].Value;
                    config.Volume = Options[1].Value;
                    config.RotateAB = Options[2].Value;
                    Audio.SetVolume(config.Volume);
                    config.Save();
                }
                break;
        }
    }

    public virtual void Unload()
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
        Emulator = new();
        Audio = new();

#if DEBUG || RELEASE
        Raylib.SetWindowSize(1280, 980);
        Raylib.SetWindowPosition(10, 30);
        Raylib.ClearWindowState(ConfigFlags.VSyncHint);
#if RELEASE
        Emulator.Debug = false;
#endif
#endif

#if DECKDEBUG
        Raylib.SetWindowPosition(10, 30);
        Raylib.ClearWindowState(ConfigFlags.VSyncHint);
#endif
        if (isdeck)
        {
            if (File.Exists(FontName))
            {
                GuiFont = Raylib.LoadFont(FontName);
                Notifications.SetFont(null, GuiFont);
            }
        }

        rlImGui.Setup(true);
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

        Emulator?.Config = new();
        Config.CreateDirectories(isdeck);
        Emulator.Config.Load();
        FileExtensions = [".gb", ".gbc", ".nes", ".sfc", ".smc", ".sms", ".gg"];
        CurrentName = PreviousName = "";

        if (File.Exists("Assets/GBC_1.png"))
            Raylib.SetWindowIcon(Raylib.LoadImage("Assets/GBC_1.png"));

        Cheat = new();
    }

    public void Open(Config config)
    {
        if (Opened)
        {
            Opened = false;
            CheatDialog = false;
            DeleteFileMode = false;
            return;
        }

        Emulator.Config = config;
        WorkingDirectory = Emulator.Config.WorkingDir;
        Opened = true;
        OpenDialog = true;

        Options =
        [
            new("Frameskip", [config.FrameSkip, 1, 1, 99], null, false, ChangeOption, null),
            new("Volume", [config.Volume, 1, 0, 100], null, false, ChangeOption, null),
            new("Rotate AB Buttons", [config.RotateAB, 1, 0, 1],["OFF","ON"], true, ChangeOption, null),
            //new("Copy Hacks", [0, 0, 0, 0], [""], true, null, CopyHacks),
        ];
    }

    private int ChangeOption(Option o)
    {
        var mousewheel = Raylib.GetMouseWheelMove();
        var olddpadLeft = Raylib.IsGamepadButtonPressed(0, BtnLeft);
        var olddpadRight = Raylib.IsGamepadButtonPressed(0, BtnRight);
        if (Raylib.IsGamepadButtonDown(0, BtnLeft) && DpadCounter == 0 || olddpadLeft || mousewheel < 0)
        {
            o.Value -= o.Add;
            if (o.Value <= o.Min)
                o.Value = o.Min;
        }
        else if (Raylib.IsGamepadButtonDown(0, BtnRight) && DpadCounter == 0 || olddpadRight || mousewheel > 0)
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
                {
                    Emulator = new Gbc();
                    Emulator.Init(GbWidth, GbHeight, MenuHeight, DebugFont, GuiFont, GbcConsole);
                    Audio.Init(GbcAudioFreq, 4096, 32);
                    break;
                }
                case ".nes":
                {
                    Emulator = new Nes();
                    Emulator.Init(NesWidth, NesHeight, MenuHeight, DebugFont, GuiFont, NesConsole);
                    Audio.Init(NesAudioFreq, 4096, 32);
                    break;
                }
                case ".sfc" or ".smc":
                {
                    Emulator = new Snes();
                    Emulator.Init(SnesWidth, SnesHeight, MenuHeight, DebugFont, GuiFont, SnesConsole);
                    Audio.Init(SnesAudioFreq, SnesMaxSamples / 2, 32);
                    break;
                }
                //case ".sms":
                //Sms sms = new();
                //Emulator = sms;
                //Emulator.Init(SnesWidth, SnesHeight, SnesConsole, MenuHeight, DebugFont, GuiFont);
                //LuaApi = Emulator.LuaApi;
                //sms.LuaMemoryCallbacks();
                //Audio.Init(SnesAudioFreq, SnesMaxSamples / 2, SnesMaxSamples, 32);
                //break;
                default: return;
            }

            Screen = Emulator.Screen;
            LuaApi = Emulator?.Lua;
            Emulator.Reset(name, false);
            Emulator.Config = new();
            Emulator?.Config.Load();
            LuaApi?.Reset();
        }

        Emulator?.Config?.Load();

        _gameName = Emulator?.GameName;

        LuaApi?.Load(name, Emulator.Console);
    }

    public static void DeleteFile(FileDetails file)
    {
        if (file.IsFile)
        {
            File.Delete(file.Name);
            var filename = Path.GetFileNameWithoutExtension(file.Name);
            if (File.Exists($"{CheatDirectory}/{filename}.cht"))
                File.Delete($"{CheatDirectory}/{filename}.cht");
            if (File.Exists($"{CheatDirectory}/{filename}.lua"))
                File.Delete($"{CheatDirectory}/{filename}.lua");
        }
    }

    public void DisplayFiles(List<FileDetails> list, Rectangle container, int itemheight, int index)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var file = list[i];
            if (!file.IsDrive && file.IsFile)
            {
                Rectangle highlight = new(container.X, container.Y + (i * itemheight) - MenuScroll[index], container.Width, itemheight);
                //DrawHighlight(x, y, width, i, index, MenuFontSize);
                if (i == SelectedItem[index])
                    Raylib.DrawRectangleRec(highlight, new(0, 128, 0, 255));
                Raylib.DrawTextEx(GuiFont, Path.GetFileName(file.Name), new(highlight.X + 10, highlight.Y), itemheight, 1, DeleteFileMode ? new(128, 0, 0, 255) : Color.White);
            }
            //y += MenuFontSize;
        }
    }

    public void DrawCheats(List<Cheat> cheats, int x, int y, int width, int index)
    {
        int j = 0;
        for (int i = 0; i < cheats.Count;)
        {
            DrawHighlight(x, y, width, j, index, 10);

            var cht = cheats.Where(c => c.Description == cheats[i].Description).ToList();
            if (cht != null)
            {
                var chtstatus = cht[0].Enabled ? "ON" : "OFF";
                var colorstatus = cht[0].Enabled ? Color.White : new(173, 173, 173, 255);
                int max = width / 17;
                string description = cht[0].Description;
                if (description.Length > max)
                    description = description[..max];
                Raylib.DrawTextEx(GuiFont, description, new(x + 10, y), 10, 0, colorstatus);
                Raylib.DrawTextEx(GuiFont, $"{chtstatus,-3}", new(x + width - 45, y), 10, 0, colorstatus);
                i += cht.Count;
            }
            y += 10;
            j++;
        }
    }

    public void DrawOptions(List<Option> options, int x, int y, int width)
    {
        int j = 0;
        for (int i = 0; i < options.Count; i++)
        {
            DrawHighlight(x, y, width, j, (int)Tab.Options, 10);
            Raylib.DrawTextEx(GuiFont, $"{options[i].Name}", new(x + 10, y), 10, 0, Color.White);
            if (options[i].Status != null)
                Raylib.DrawTextEx(GuiFont, $"{options[i].Status[options[i].Value],3}", new(x + width - 55, y), 10, 0, Color.White);
            else
                Raylib.DrawTextEx(GuiFont, $"{options[i].Value,3}", new(x + width - 55, y), 10, 0, Color.White);
            y += 10;
            j++;
        }
    }

    public void DrawHighlight(int x, int y, int width, int i, int index, int fontsize)
    {
        if (i == SelectedItem[index])
            Raylib.DrawRectangle(x, y, width, fontsize - 1, new(0, 128, 0, 255));
    }

    public void DrawHighlightTab(int x, int y, int width, int fontsize, int i)
    {
        if (i == (int)TabIndex)
            Raylib.DrawRectangle(x, y, width, fontsize, new(0, 0, 255, 255));
    }

    public void Enumerate(string path)
    {
        DirectoryInfo di;
        if (TabIndex == Tab.Games)
            GameFiles.Clear();
        else if (TabIndex == Tab.ChtBrowser)
            CheatFiles.Clear();
        else if (TabIndex == Tab.Lua)
            LuaFiles.Clear();

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

    public static void CopyHacks(bool isdeck)
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
    }

    public void LoadGame(string filename)
    {
        Emulator.Config?.Save();
        Opened = false;
        ResetGame(CurrentName = filename);
    }

    public void LoadCheats(string filename)
    {
        CheatDialog = false;
        Emulator.LoadCheats(filename, true);
        if (!File.Exists($"{CheatDirectory}/{Path.GetFileNameWithoutExtension(_gameName)}.cht"))
            Emulator.SaveCheats($"{CheatDirectory}/{Path.GetFileNameWithoutExtension(_gameName)}.cht");
    }

    public void ToggleCheat(List<Cheat> cht)
    {
        if (cht == null) return;
        foreach (var c in cht)
            c.Enabled = !c.Enabled;
        Emulator.SaveCheats(CurrentName);
    }

    public void ToggleAllCheats()
    {
        foreach (var c in Emulator.Cheats.Values)
            c.Enabled = !c.Enabled;
        Emulator.SaveCheats(CurrentName);
    }

    public void LoadLua(string filename)
    {
        LuaApi?.Load(filename, Emulator.Console);
        Notifications.Init("Lua File Loaded Successfully");
        //Opened = false;
    }

    public readonly string[] MainEntries = ["Games", "Cheats", "Cht", "Lua", "Options", "About"];

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
