using GBoy.Core;
using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using System.Diagnostics;
using Color = Raylib_cs.Color;

public class Program
{
    private readonly string WindowTitle = "Gmulator";
    private const string FontName = "Assets/consola.ttf";

    public Breakpoint Breakpoint { get; private set; }
    private static LuaApi LuaApi;
    public Audio Audio { get; private set; }
    public static Cheat Cheat { get; private set; }

    private float OldRightThumbUp;
    private float OldRightThumbDown;

    public static bool IsDeck { get; private set; } = false;
    public static bool IsDeckDebug { get; private set; } = false;
    public static bool DebuggerEnabled { get; set; } = true;
    private static bool ShowPpuDebug;
    private static bool RomLoaded;

    public static float MenuHeight { get; private set; }
    public static Dictionary<int, Cheat> Cheats { get; private set; } = [];
    public static Dictionary<int, Breakpoint> Breakpoints { get; private set; } = [];
    public static int System { get; private set; }
    public static RenderTexture2D Screen { get; private set; }

    public ImFontPtr[] Consolas { get; private set; }
    public static int State { get; set; }
    public static bool FastForward { get; private set; }

    public static Emulator Emu { get; private set; }

    public Program() => Init();
    static void Main() => new Program().Run();
    private void Run()
    {
        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.F4))
                Raylib.TakeScreenshot($"screenshot{DateTime.Now.Ticks}.png");

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.DarkGray);

            rlImGui.Begin();

            if (Emu != null)
            {
                Emu.Execute(State, DebuggerEnabled);
                Emu.Render(MenuHeight, DebuggerEnabled);
                Emu.Update();
                LuaApi.Update();
            }
            UpdateInput();
            RenderMenuBar();

            ImGui.PushFont(Consolas[0]);
            Menu.Render(IsDeck, RomLoaded, MenuHeight, Cheats);
            ImGui.PopFont();

            rlImGui.End();
            Raylib.EndDrawing();
        }

        if (Emu != null)
            Emu.Close(Breakpoints);
        Config.Save();

        Raylib.UnloadRenderTexture(Screen);
        LuaApi.Unload();
        Audio.Unload();
        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }

    private void UpdateInput()
    {
        DisableInputs();

        if (!Raylib.IsWindowFocused()) return;

        var NewRightStickUp = Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightY);
        var NewRightStickDown = Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightY);

        if (Raylib.IsGamepadButtonPressed(0, BtnL2))
            Menu.Open(IsDeck);

        if ((Menu.Opened || DebuggerEnabled || !RomLoaded) && !FastForward)
            Raylib.SetTargetFPS(60);
        else
        {
            Raylib.SetTargetFPS(0);

            if (State == Paused)
                State = Running;
        }

        if (Raylib.IsGamepadButtonDown(0, GamepadButton.RightTrigger2) && !FastForward)
        {
            FastForward = true;
            Raylib.ClearWindowState(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        }
        else if (!Raylib.IsGamepadButtonDown(0, GamepadButton.RightTrigger2) && FastForward)
        {
            FastForward = false;
            Raylib.SetWindowState(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
        }

        if (Emu != null)
        {
            var shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
            if (shift && Raylib.IsKeyPressed(KeyboardKey.F1) || NewRightStickUp < 0 && OldRightThumbUp == 0)
                Emu.SaveState();
            else if (Raylib.IsKeyPressed(KeyboardKey.F1) || NewRightStickDown > 0 && OldRightThumbDown == 0)
                Emu.LoadState();
        }

        OldRightThumbUp = NewRightStickUp;
        OldRightThumbDown = NewRightStickDown;
    }

    private static void RenderMenuBar()
    {
        if (IsDeck) return;
        if (ImGui.BeginMainMenuBar())
        {
            MenuHeight = ImGui.GetWindowHeight();
            if (ImGui.BeginMenu("Browser", !IsDeck))
            {
                if (ImGui.MenuItem("Open Menu"))
                {
                    if (!Menu.Opened)
                    {
                        Menu.Open(IsDeck);
                        State = Paused;
                        ImGui.OpenPopup("Menu");
                    }
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Debug"))
            {
                if (ImGui.MenuItem("Show Debugger", "", DebuggerEnabled))
                    DebuggerEnabled = !DebuggerEnabled;

                if (ImGui.MenuItem("Show Ppu Debug", "", ShowPpuDebug))
                    ShowPpuDebug = !ShowPpuDebug;

                ImGui.EndMenu();
            }

            //if (ImGui.BeginMenu("Game Info", Cart != null))
            //{
            //    ImGui.BeginTable("##rominfo", 2);
            //    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None, 100);
            //    ImGui.TableSetupColumn("");
            //    ImGui.TableHeadersRow();
            //    ImGui.TableNextRow();
            //    TableRow("Name", Path.GetFileNameWithoutExtension(Cart.Name));
            //    //TableRow("Mapper Number", $"{Cart.MapperId:D3}");
            //    ImGui.EndTable();

            //    ImGui.EndMenu();
            //}

            if (ImGui.BeginMenu("Audio"))
            {
                //if (ImGui.MenuItem("Record", "", Apu.Recording))
                //    Apu.Recording = !Apu.Recording;
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }

    public static void RenderText(string[] text, int x, int y, int width, int height, Color c)
    {
        var scale = Math.Min((float)width / NesWidth, (float)height / NesHeight);
        var fontsize = DebuggerEnabled ? 15 : 30;
        Raylib.DrawRectangle(x, y, (int)(NesWidth * scale) + 2, fontsize * text.Length, new(0, 0, 0, 192));
        foreach (var item in text)
        {
            Raylib.DrawText(item, x + 5, y, fontsize, c);
            y += fontsize;
        }
    }

    private static void DisableInputs()
    {
        var io = ImGui.GetIO();
        if ((io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad) > 0)
        {
            unsafe
            {
                //io.NativePtr->KeysData_123.Down = 0; //disable triangle/Y
            }

            //GetPressedKey(io);
        }
    }
    private static void GetPressedKey(ImGuiIOPtr io)
    {
        for (int i = 0; i < io.KeysData.Count; i++)
        {
            if (io.KeysData[i].Down > 0)
                ImGui.Text($"{i}");
        }
    }

    private void Init()
    {
        Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow | ConfigFlags.HighDpiWindow);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, WindowTitle);

#if DEBUG || DECKDEBUG
        Raylib.SetWindowPosition(10, 30);
        Raylib.ClearWindowState(ConfigFlags.VSyncHint);
#endif

        var deckres = Raylib.GetMonitorWidth(0) == 1280 && Raylib.GetMonitorHeight(0) == 800;
        if (deckres && !Raylib.IsWindowMaximized())
            Raylib.SetWindowState(ConfigFlags.MaximizedWindow);

#if DECKDEBUG || DECKRELEASE
        IsDeck = true;
#if DECKDEBUG
        IsDeckDebug = true;
#endif
#endif

#if !DEBUG
        DebuggerEnabled = false;
#endif

        GraphicsWindow.Init();
        rlImGui.Setup(true, true);
        Audio = new(4096);

        Menu.Init(IsDeck, [".gb", ".gbc", ".nes"]);
        Config.CreateDirectories();
        Config.Load();

        var io = ImGui.GetIO();
        if (IsDeck)
        {
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            io.ConfigFlags |= ImGuiConfigFlags.NoMouse;
            io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange;
            Menu.Open(IsDeck);
        }

        if (File.Exists(FontName))
        {
            Consolas = [null, null];
            Consolas[0] = io.Fonts.AddFontFromFileTTF(FontName, 20f);
            Consolas[1] = io.Fonts.AddFontFromFileTTF(FontName, 15f);
            rlImGui.ReloadFonts();
        }

        if (File.Exists("Assets/GBC_1.png"))
            Raylib.SetWindowIcon(Raylib.LoadImage("Assets/GBC_1.png"));

        Cheat = new();
        LuaApi = new(Consolas);

        Menu.Reset = Reset;
        Menu.ReloadCheats += Cheat.ReloadCheats;
        Menu.LoadLua = LuaApi.LoadFile;
    }

    public static void Reset(string name, string lastname)
    {
        if (name != "")
        {
            Cheat?.Save(lastname);
            Emu?.SaveBreakpoints(Breakpoints, lastname);
            Emu?.SaveRam();
            switch (Path.GetExtension(name).ToLowerInvariant())
            {
                case ".gb" or ".gbc":
                    Emu = new Gbc(Cheat, Cheats, Breakpoints, ref LuaApi);
                    Emu.Console = GbcSystem;
                    LuaApi.ConsoleHeight = GbHeight;
                    Screen = Raylib.LoadRenderTexture(GbWidth, GbHeight);
                    break;
                case ".nes":
                    Emu = new Nes(Cheat, Cheats, Breakpoints, ref LuaApi);
                    Emu.Console = NesSystem;
                    LuaApi.ConsoleHeight = NesHeight;
                    Screen = Raylib.LoadRenderTexture(NesWidth, NesHeight);
                    break;
                default: return;
            }
            LuaApi.Reset();
        }

        Emu?.Reset(name, false, DebuggerEnabled);

        if (!DebuggerEnabled)
            State = Running;
        else
            State = Debugging; ;
    }

    public static void BreakpointTriggered() => State = Debugging;
    public static void SetState(int v) => State = v;
    public static bool IsRomLoaded() => RomLoaded;
}

