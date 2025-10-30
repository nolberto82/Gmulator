using Raylib_cs;
using System.Numerics;

namespace Gmulator.Ui;

internal class GuiDeck : Gui
{
    private int _dpadCpunter;

    public override void Run()
    {
        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.DarkGray);

            Emulator?.RunFrame(Opened);
            Emulator?.Render(MenuHeight);
            Emulator?.Update();
            Emulator?.Input();
            LuaApi?.Update(Opened);

            Input.UpdateGuiInput(Emulator, this);

            Update(true);
            Render();

            Raylib.EndDrawing();
        }
        Unload(true);
    }

    public override void Render()
    {
        if (!Opened) return;

        var texwidth = Emulator.Screen.Texture.Width == 0 ? 256 : Emulator.Screen.Texture.Width;
        var texheight = Emulator.Screen.Texture.Height == 0 ? 240 : Emulator.Screen.Texture.Height;
        var scale = Math.Min((float)Raylib.GetRenderWidth() / texwidth, (float)Raylib.GetRenderHeight() / texheight);
        var left = (Raylib.GetRenderWidth() - texwidth * scale) / 2;
        var renderwidth = Raylib.GetRenderWidth();
        int maxgamesview = Raylib.GetScreenHeight() / FontSize - 5;
        int x = 0, y = 1, posx = 0;// (int)(x * scale + left);
        List<FileDetails> list = new();

        Rectangle rectName = new(posx, y, renderwidth, FontSize);
        Raylib.DrawRectangle((int)rectName.X + 1, (int)rectName.Y, (int)rectName.Width, (int)rectName.Height, Color.LightGray);
        Raylib.DrawTextEx(GuiFont, $"{EmulatorName} v{EmuState.Version}", new(posx + 1, y), FontSize, 0, Color.Black);

        Rectangle menuRect = new(posx, y + FontSize, renderwidth, Raylib.GetRenderHeight());
        Raylib.DrawRectangle((int)menuRect.X, (int)menuRect.Y, (int)menuRect.Width, (int)menuRect.Height - 5, new(0, 0, 0, 200));
        Raylib.BeginScissorMode((int)menuRect.X, (int)menuRect.Y, (int)menuRect.Width, (int)menuRect.Height - 5);

        y += FontSize;
        y += MenuScroll[TabIndex] * -FontSize;
        switch (TabIndex)
        {
            case ScrMain:
                for (int i = 0; i < MainEntries.Length; i++)
                {
                    DrawHighlight(posx, y, renderwidth, i);
                    Raylib.DrawTextEx(GuiFont, MainEntries[i], new(posx + 1, y), FontSize, 0, Color.White);
                    y += FontSize;
                }
                break;
            case ScrGames:
                GameFiles.Clear();
                Enumerate(RomDirectory);
                GameFiles = [.. GameFiles.OrderBy(f => Path.GetExtension(f.Name))];
                DisplayFiles(GameFiles, posx, y, renderwidth, GuiFont);
                break;
            case ScrCheats or ScrBrowser:
                if (!CheatDialog)
                    DrawCheats(Emulator.Cheats.Values.ToList(), posx, y, renderwidth);
                else
                {
                    CheatFiles.Clear();
                    Enumerate(CheatDirectory);
                    list = [.. CheatFiles.OrderBy(f => Path.GetExtension(f.Name))];
                    DisplayFiles(list, posx, y, Raylib.GetRenderWidth(), GuiFont);
                }
                break;
            case ScrLua:
                LuaFiles.Clear();
                Enumerate(CheatDirectory);
                list = [.. LuaFiles];
                DisplayFiles(list, posx, y, renderwidth, GuiFont);
                break;
            case ScrOptions:
                DrawOptions(Options, posx, y, renderwidth);
                break;
        }
        Raylib.EndScissorMode();

        y = FontSize * maxgamesview + FontSize;
        Rectangle rectInfo = new(posx + 1, y, renderwidth, Raylib.GetRenderHeight() - y);
        Raylib.DrawRectangle((int)rectInfo.X, (int)rectInfo.Y, (int)rectInfo.Width, (int)rectInfo.Height, Color.LightGray);

        var color = Color.Black;
        foreach (var t in TabInfo[TabIndex == 1 && CheatDialog ? ScrBrowser : TabIndex][1])
        {
            Raylib.DrawTextEx(GuiFont, t.Button, new(posx, y), FontSize, -1, color);
            Raylib.DrawTextEx(GuiFont, t.Description, new(posx + 200, y), FontSize, -1, color);
            y += FontSize;
        }

        if (TabIndex != ScrMain)
        {
            Raylib.DrawTextEx(GuiFont, "Circle", new(posx, y), FontSize, -1, color);
            Raylib.DrawTextEx(GuiFont, "Back", new(posx + 200, y), FontSize, -1, color);
        }

        base.Render();
    }

    public override void Update(bool isdeck)
    {
        if (Raylib.IsGamepadButtonPressed(0, BtnL2))
        {
            Raylib.SetTargetFPS(60);
            Raylib.SetWindowState(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
            Open(Emulator);
        }

        if (!Opened) return;

        const int Delay = 10;
        int maxgamesview = Raylib.GetScreenHeight() / FontSize;
        FrameCounter++;

        bool newDownPressed = Raylib.IsGamepadButtonDown(0, BtnDown);
        bool newUpPressed = Raylib.IsGamepadButtonDown(0, BtnUp);
        bool oldDownPressed = Raylib.IsGamepadButtonPressed(0, BtnDown);
        bool oldUpPressed = Raylib.IsGamepadButtonPressed(0, BtnUp);
        if (newDownPressed || newUpPressed)
        {
            _dpadCpunter--;
            if (_dpadCpunter < 0)
                _dpadCpunter = 3;
        }
        else
            _dpadCpunter = Delay;

            if (oldUpPressed || newUpPressed && _dpadCpunter == 0)
                SelOption[TabIndex]--;
            else if (oldDownPressed || newDownPressed && _dpadCpunter == 0)
                SelOption[TabIndex]++;

        var total = TabIndex switch
        {
            ScrMain => MainEntries.Length,
            ScrGames => GameFiles.Count,
            ScrCheats => Emulator.Cheats.DistinctBy(c => c.Value.Description).Count(),
            ScrLua => LuaFiles.Count,
            ScrOptions => Options.Count,
            ScrBrowser => Emulator.Cheats.Count,
            _ => 0,
        };

        if (SelOption[TabIndex] >= total)
            SelOption[TabIndex] = total - 1;

        if (SelOption[TabIndex] <= 0)
            SelOption[TabIndex] = 0;

        if (SelOption[TabIndex] > MenuScroll[TabIndex] + maxgamesview - 6)
            MenuScroll[TabIndex]++;

        if (SelOption[TabIndex] < MenuScroll[TabIndex] && MenuScroll[TabIndex] > 0)
            MenuScroll[TabIndex]--;

        OldTotal[TabIndex] = total;

        if (TabIndex != ScrMain && Raylib.IsGamepadButtonPressed(0, BtnA))
            TabIndex = ScrMain;

        base.Update(true);
    }

    public override void Init(bool isdeck)
    {
        base.Init(isdeck);
        MenuScroll = new int[MaxTabs];
        SelOption = new int[MaxTabs];
        OldTotal = new int[MaxTabs];
        Opened = true;
    }

    public override void Unload(bool isdeck)
    {
        Raylib.UnloadRenderTexture(MenuTarget);
        base.Unload(isdeck);
    }

    public override void ResetGame(string name) => base.ResetGame(name);
}
