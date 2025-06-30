using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Font = Raylib_cs.Font;

namespace Gmulator.Shared;

public abstract class Gui
{
    public readonly string WindowTitle = "Gmulator";
    public const string FontName = "Assets/naga10.ttf";

    public LuaApi LuaApi { get; private set; }
    public Audio Audio { get; set; }
    public Cheat Cheat { get; set; }
    public Menu Menu { get; set; }

    public float MenuHeight { get; set; }
    public Dictionary<int, Breakpoint> Breakpoints { get; private set; } = [];
    public int System { get; private set; }
    public RenderTexture2D Screen { get; private set; }
    public Emulator Emu { get; private set; } = new();
    public ImFontPtr[] DebuFont { get; set; }
    private Font GuiFont;

    private bool ShowPpuDebug;

    public virtual void Run(bool isdeck)
    {
        while (!Raylib.WindowShouldClose())
        {
            if (!isdeck && Raylib.IsKeyPressed(KeyboardKey.F10))
                Raylib.TakeScreenshot($"screenshot{DateTime.Now.Ticks}.png");

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.DarkGray);

            rlImGui.Begin();

            Emu?.Execute(Menu.Opened, 1);
            Emu?.Render(MenuHeight);
            Emu?.Update();
            LuaApi?.Update(Menu.Opened);

            Input.UpdateGuiInput(Emu, Menu, isdeck);

            ImGui.PushFont(DebuFont[0]);
            Menu.Update(isdeck);
            Menu.Render(isdeck, Cheat);
            if (!isdeck)
                RenderMenuBar();
            ImGui.PopFont();

            rlImGui.End();
            Raylib.EndDrawing();
        }

        Emu?.Close();
        Emu?.Config.Save();

        LuaApi?.Unload();
        Raylib.UnloadFont(GuiFont);
        Raylib.UnloadRenderTexture(Screen);
        Audio.Unload();
        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }

    public virtual void Init(bool isdeck)
    {
        Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow | ConfigFlags.HighDpiWindow);
        Raylib.InitWindow(DeckWidth, DeckHeight, WindowTitle);
        Raylib.SetTargetFPS(60);
        Audio = new();

#if DEBUG || RELEASE
        Raylib.SetWindowSize(1400, 980);
        Raylib.SetWindowPosition(10, 30);
        Raylib.ClearWindowState(ConfigFlags.VSyncHint);
#if RELEASE
        Emu.Debug = false;
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

        GraphicsWindow.Init();
        rlImGui.Setup(true, true);

        var io = ImGui.GetIO();
        if (isdeck)
        {
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            io.ConfigFlags |= ImGuiConfigFlags.NoMouse;
            io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange;
        }

        Emu.Config = new();
        Config.CreateDirectories(isdeck);
        Emu?.Config.Load();

        Menu = new();
        Menu.Init(isdeck, [".gb", ".gbc", ".nes", ".sfc", ".smc"]);
        Menu.Open(Emu);

        if (File.Exists(FontName))
        {
            DebuFont = [null, null];
            DebuFont[0] = io.Fonts.AddFontFromFileTTF(FontName, 23f);
            DebuFont[1] = io.Fonts.AddFontFromFileTTF(FontName, 15f);
            GuiFont = Raylib.LoadFont(FontName);
            rlImGui.ReloadFonts();
            Notifications.SetFont(DebuFont[0], GuiFont);
        }

        if (File.Exists("Assets/GBC_1.png"))
            Raylib.SetWindowIcon(Raylib.LoadImage("Assets/GBC_1.png"));

        Cheat = new();

        Menu.Reset = Reset;
    }

    public virtual void Reset(string name, string lastname)
    {
        if (name != "")
        {
            Cheat.Save(lastname, Emu?.Cheats);
            Emu?.SaveBreakpoints(lastname);
            switch (Path.GetExtension(name).ToLowerInvariant())
            {
                case ".gb" or ".gbc":
                    Emu = new Gbc();
                    Emu.Init(GbWidth, GbHeight, GbcConsole, MenuHeight, DebuFont, GuiFont);
                    LuaApi = Emu.LuaApi;
                    LuaApi.Read = Emu.GetConsole<Gbc>().Mmu.Read;
                    Audio.Init(GbcAudioFreq, 4096, 4096, 32);
                    break;
                case ".nes":
                    Emu = new Nes();
                    Emu.Init(NesWidth, NesHeight, NesConsole, MenuHeight, DebuFont, GuiFont);
                    LuaApi = Emu.LuaApi;
                    LuaApi.Read = Emu.GetConsole<Nes>().Mmu.Read;
                    Audio.Init(NesAudioFreq, 4096, 4096, 32);
                    break;
                case ".sfc" or ".smc":
                    Emu = new Snes();
                    Emu.Init(SnesWidth, SnesHeight, SnesConsole, MenuHeight, DebuFont, GuiFont);
                    LuaApi = Emu.LuaApi;
                    LuaApi.Read = Emu.GetConsole<Snes>().ReadMemory;
                    Audio.Init(SnesAudioFreq, SnesMaxSamples / 2, SnesMaxSamples, 32);
                    break;
                default: return;
            }

            //Emu?.SetLua();
            Emu.Config = new();
            Emu?.Config.Load();
            LuaApi?.Reset();
        }

        Emu?.Reset(name, Menu.LastName, false);
        LuaApi?.CheckLuaFile(name);
        Emu?.Config?.Load();
    }

    private void RenderMenuBar()
    {
        if (ImGui.BeginMainMenuBar())
        {
            MenuHeight = ImGui.GetWindowHeight();
            if (ImGui.BeginMenu("Browser"))
            {
                if (ImGui.MenuItem("Open Menu"))
                {
                    if (!Menu.Opened)
                    {
                        Menu.Open(Emu);
                        Emu.State = Paused;
                        ImGui.OpenPopup("Menu");
                    }
                }
                if (ImGui.MenuItem("Reset"))
                    Emu.Reset("", Menu.LastName, true);

                ImGui.EndMenu();
            }

            ImGui.BeginDisabled(Emu == null);
            if (ImGui.BeginMenu("Debug"))
            {
                if (ImGui.MenuItem("Show Debugger", "", Emu.Debug))
                {
                    Emu.Debug = !Emu.Debug;
                    LuaApi.SetDebug(Emu.Debug);
                }

                ImGui.BeginDisabled(Emu.Console != SnesConsole);
                if (ImGui.MenuItem("Show Sa1", "", Emu.DebugWindow?.ShowSa1 == true))
                    Emu.DebugWindow.ShowSa1 = !Emu.DebugWindow.ShowSa1;

                if (ImGui.MenuItem("Show Spc", "", Emu.DebugWindow?.ShowSpc == true))
                    Emu.DebugWindow.ShowSpc = !Emu.DebugWindow.ShowSpc;
                ImGui.EndDisabled();

                if (ImGui.MenuItem("Show Ppu Debug", "", ShowPpuDebug))
                    ShowPpuDebug = !ShowPpuDebug;

                ImGui.EndMenu();
            }
            ImGui.EndDisabled();

            ImGui.EndMainMenuBar();
        }
    }
}
