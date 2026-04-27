using rlImGui_cs;

namespace Gmulator.Ui;

internal class GuiDeck : Gui
{
    private const string FontButtons = "Assets/buttons.ttf";
    private Font _fontButtons;

    public override void Run()
    {
        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.DarkGray);

            rlImGui.Begin();

            Emulator?.RunFrame(Opened);
            Emulator?.Render(MenuHeight);
            Emulator?.Update();
            Emulator?.Input();

            Input.UpdateGuiInput(Emulator, this);

            rlImGui.End();

            Update(true);
            Render();

            Raylib.EndDrawing();
        }
        Unload();
    }

    public override void Render()
    {
        if (!Opened) return;

        var texwidth = Screen.Texture.Width == 0 ? 256 : Screen.Texture.Width;
        var texheight = Screen.Texture.Height == 0 ? 240 : Screen.Texture.Height;
        var scale = Math.Min((float)Raylib.GetRenderWidth() / texwidth, (float)Raylib.GetRenderHeight() / texheight);
        var left = (Raylib.GetRenderWidth() - texwidth * scale) / 2;
        var renderwidth = Raylib.GetRenderWidth();
        int maxgamesview = Raylib.GetScreenHeight() / FontSize;
        int statusBarY = Raylib.GetScreenHeight() - 30;
        int y = 1, posx = 0;// (int)(x * scale + left);
        List<FileDetails> list = [];

        Rectangle rectName = new(posx, y, renderwidth, FontSize);
        Raylib.DrawRectangle((int)rectName.X + 1, (int)rectName.Y, (int)rectName.Width, (int)rectName.Height, Color.LightGray);
        Raylib.DrawTextEx(GuiFont, $"{EmulatorName} v{EmuState.Version}", new(posx + 1, y), FontSize, 0, Color.Black);

        Rectangle menuRect = new(posx, y + FontSize, renderwidth, Raylib.GetRenderHeight());
        Raylib.DrawRectangle((int)menuRect.X, (int)menuRect.Y, (int)menuRect.Width, (int)menuRect.Height - 5, new(0, 0, 0, 200));
        Raylib.BeginScissorMode((int)menuRect.X, (int)menuRect.Y, (int)menuRect.Width, maxgamesview * FontSize);

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
                    DrawCheats([.. Cheats.Values], posx, y, renderwidth);
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
        Rectangle rectInfo = new(posx + 1, Raylib.GetRenderHeight() - 30, renderwidth, 30);
        Raylib.DrawRectangle((int)rectInfo.X, (int)rectInfo.Y, (int)rectInfo.Width, (int)rectInfo.Height, Color.LightGray);

        var color = Color.Black;
        Raylib.DrawTextEx(_fontButtons, "X", new(5, statusBarY + 1), FontSize + 10, 1, color);
        Raylib.DrawTextEx(GuiFont, ":Ok", new(5 + 30, statusBarY + 3), FontSize, 1, color);
        Raylib.DrawTextEx(_fontButtons, "C", new(5 + 70 + Raylib.MeasureText(":Ok", FontSize), statusBarY + 1), FontSize + 10, 1, color);
        Raylib.DrawTextEx(GuiFont, ":Cancel", new(5 + 100 + Raylib.MeasureText(":Ok", FontSize), statusBarY + 3), FontSize, 1, color);
        if (TabIndex == ScrCheats)
        {
            Raylib.DrawTextEx(_fontButtons, "S", new(5 + 170 + Raylib.MeasureText(":Cancel", FontSize), statusBarY + 1), FontSize + 10, 1, color);
            Raylib.DrawTextEx(GuiFont, ":Load Cheats", new(5 + 200 + Raylib.MeasureText(":Cancel", FontSize), statusBarY + 3), FontSize, 1, color);
        }

        base.Render();
    }

    public override void Update(bool isdeck)
    {
        if (Raylib.IsGamepadButtonPressed(0, BtnL2))
        {
            Raylib.SetTargetFPS(60);
            Raylib.SetWindowState(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
            Open(Emulator.Config);
        }

        if (!Opened) return;

        const int Delay = 10;
        int maxgamesview = Raylib.GetScreenHeight() / FontSize;
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
            DpadCounter = Delay;

        if (oldUpPressed || newUpPressed && DpadCounter == 0)
            SelOption[TabIndex]--;
        else if (oldDownPressed || newDownPressed && DpadCounter == 0)
            SelOption[TabIndex]++;

        var total = TabIndex switch
        {
            ScrMain => MainEntries.Length,
            ScrGames => GameFiles.Count,
            ScrCheats => Emulator.Cheats.DistinctBy(c => c.Value.Description).Count(),
            ScrLua => LuaFiles.Count,
            ScrOptions => Options.Count,
            ScrBrowser => CheatFiles.Count,
            _ => 0,
        };

        if (SelOption[TabIndex] >= total)
            SelOption[TabIndex] = total - 1;

        if (SelOption[TabIndex] <= 0)
            SelOption[TabIndex] = 0;

        if (SelOption[TabIndex] > MenuScroll[TabIndex] + maxgamesview - 3)
            MenuScroll[TabIndex]++;

        if (SelOption[TabIndex] < MenuScroll[TabIndex] && MenuScroll[TabIndex] > 0)
            MenuScroll[TabIndex]--;

        OldTotal[TabIndex] = total;

        if (TabIndex != ScrMain && Raylib.IsGamepadButtonPressed(0, BtnA))
        {
            TabIndex = ScrMain;
            CheatDialog = false;
        }

        base.Update(true);
    }

    public override void Init(bool isdeck)
    {
        base.Init(isdeck);
        MenuScroll = new int[MaxTabs];
        SelOption = new int[MaxTabs];
        OldTotal = new int[MaxTabs];
        Opened = true;

        if (File.Exists(FontButtons))
        {
            _fontButtons = Raylib.LoadFont(FontButtons);
        }

        var deckres = Raylib.GetMonitorWidth(0) == 1280 && Raylib.GetMonitorHeight(0) == 800;
        if (deckres && !Raylib.IsWindowMaximized())
            Raylib.SetWindowState(ConfigFlags.MaximizedWindow);
    }

    public override void Unload()
    {
        Raylib.UnloadFont(_fontButtons);
        Raylib.UnloadRenderTexture(MenuTarget);
        base.Unload();
    }

    public override void ResetGame(string name) => base.ResetGame(name);
}
