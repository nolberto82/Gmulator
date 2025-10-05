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
    public const string FontName = "Assets/naga10.ttf";

    private LuaApi LuaApi;
    private Audio Audio { get; set; }
    private Cheat Cheat { get; set; }
    private Menu Menu;
    public float MenuHeight { get; set; }
    public RenderTexture2D Screen { get; private set; }
    public Emulator Emulator { get; private set; } = new();
    public ImFontPtr[] DebugFont { get; private set; }
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

            Emulator?.Execute(Menu.Opened);
            Emulator?.Render(MenuHeight);
            Emulator?.Update();
            LuaApi?.Update(Menu.Opened);

            Input.UpdateGuiInput(Emulator, Menu);

            ImGui.PushFont(DebugFont[0]);
            Menu.Update(isdeck);
            Menu.Render(isdeck, Cheat);
            if (!isdeck)
                RenderMenuBar();
            ImGui.PopFont();

            rlImGui.End();
            Raylib.EndDrawing();
        }

        Emulator?.Close();
        Emulator?.Config.Save();

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

        Emulator?.Config = new();
        Config.CreateDirectories(isdeck);
        Emulator?.Config.Load();

        Menu = new();
        Menu.Init(isdeck, [".gb", ".gbc", ".nes", ".sfc", ".smc"]);
        Menu.Open(Emulator);

        if (File.Exists(FontName))
        {
            DebugFont = [null, null];
            DebugFont[0] = io.Fonts.AddFontFromFileTTF(FontName, 23f);
            DebugFont[1] = io.Fonts.AddFontFromFileTTF(FontName, 15f);
            GuiFont = Raylib.LoadFont(FontName);
            rlImGui.ReloadFonts();
            Notifications.SetFont(DebugFont[0], GuiFont);
        }

        if (File.Exists("Assets/GBC_1.png"))
            Raylib.SetWindowIcon(Raylib.LoadImage("Assets/GBC_1.png"));

        Cheat = new();

        Menu.Reset = Reset;
    }

    public virtual void Reset(string name)
    {
        if (name != "")
        {
            switch (Path.GetExtension(name).ToLowerInvariant())
            {
                case ".gb" or ".gbc":
                    Gbc gbc = new Gbc();
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
            Emulator.Config = new();
            Emulator?.Config.Load();
            LuaApi?.Reset();
        }

        Emulator?.Reset(name, false);
        Emulator?.Config?.Load();

        string gameName = Emulator?.GameName;
        Cheat?.Load(Emulator, gameName);
        LuaApi?.Load(gameName);
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
                        Menu.Open(Emulator);
                        Emulator.State = DebugState.Paused;
                        ImGui.OpenPopup("Menu");
                    }
                }
                if (ImGui.MenuItem("Reset"))
                    Emulator.Reset("", true);

                ImGui.EndMenu();
            }

            ImGui.BeginDisabled(Emulator == null);
            if (ImGui.BeginMenu("Debug"))
            {
                if (ImGui.MenuItem("Show Debugger", "", Emulator.Debug))
                {
                    Emulator.Debug = !Emulator.Debug;
                    LuaApi.SetDebug(Emulator.Debug);
                }

                ImGui.BeginDisabled(Emulator.Console != SnesConsole);
                if (ImGui.MenuItem("Show Sa1", "", Emulator.DebugWindow?.ShowSa1 == true))
                    Emulator.DebugWindow.ShowSa1 = !Emulator.DebugWindow.ShowSa1;

                if (ImGui.MenuItem("Show Spc", "", Emulator.DebugWindow?.ShowSpc == true))
                    Emulator.DebugWindow.ShowSpc = !Emulator.DebugWindow.ShowSpc;
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
