using ImGuiNET;
using rlImGui_cs;
using System.Numerics;
using static Gmulator.Ui.Gui;

namespace Gmulator.Ui;

internal class GuiDeck : Gui
{
    private Dictionary<int, Action<int>> _tabActions;
    private bool[] _initial;
    private ImFontPtr _buttonFont;

    public override void Init(bool isdeck)
    {
        base.Init(isdeck);

        Opened = true;
        _initial = new bool[MaxTabs];
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigFlags |= ImGuiConfigFlags.NoMouse;
        //io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange;

        _buttonFont = io.Fonts.AddFontFromFileTTF("Assets/buttons.ttf", 30f);
        rlImGui.ReloadFonts();

        var style = ImGui.GetStyle();
        style.Colors[(int)ImGuiCol.HeaderHovered] = new(0.0f, 0.5f, 0.0f, 1f);
        style.Colors[(int)ImGuiCol.NavCursor] = new(0.0f, 0.5f, 0.0f, 1f);
        style.Colors[(int)ImGuiCol.Header] = new(0.0f, 0.5f, 0.0f, 1f);
        style.Colors[(int)ImGuiCol.TabSelected] = new(0.0f, 0.5f, 0.0f, 1f);

        _tabActions = new Dictionary<int, Action<int>>
        {
            { (int)Tab.Games, (i) => DrawGames(i) },
            { (int)Tab.Cheats, (i) => DrawCheats(i) },
            { (int)Tab.ChtBrowser, (i) => DrawCheatBrowser(i) },
            { (int)Tab.Lua, (i) => DrawLua(i) },
            { (int)Tab.Options, (i) => DrawOptions(i) },
            { (int)Tab.About, (i) => DrawAbout(i) }
        };
    }

    public override void Run()
    {
        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.DarkGray);

            rlImGui.Begin();

            Emulator?.RunFrame(Opened);
            Emulator?.Update();
            Emulator?.Render(MenuHeight);
            Emulator?.Input();

            Input.UpdateGuiInput(Emulator, this);

            ImGui.PushFont(DebugFont[0]);

            Update(false);
            Draw();

            ImGui.PopFont();

            rlImGui.End();
            Raylib.EndDrawing();
        }

        rlImGui.Shutdown();
        base.Unload();
    }

    public override void Draw()
    {
        if (!Opened) return;

        ImGuiViewportPtr vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(new(vp.Size.X, vp.Size.Y));
        ImGui.SetNextWindowPos(new(0, 0));
        ImGui.Begin("Menu", NoScrollFlags | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoCollapse);

        if (ImGui.BeginTabBar("MainTabBar"))
        {
            for (int i = 0; i < MainEntries.Length; i++)
            {
                bool open = true;
                ImGuiTabItemFlags flags = (int)TabIndex == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;

                if (ImGui.BeginTabItem(MainEntries[i], ref open, flags | ImGuiTabItemFlags.NoPushId))
                {
                    _tabActions.TryGetValue(i, out Action<int> action);
                    ImGui.SetNextWindowFocus();
                    ImGui.BeginChild($"tab_{i}", new(0, -_buttonFont.FontSize), ImGuiChildFlags.FrameStyle);
                    if (!_initial[i])
                    {
                        ImGui.SetKeyboardFocusHere(0);
                        _initial[i] = true;
                    }

                    action?.Invoke(i);
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }

        ImGui.BeginChild("Footer", new(vp.Size.X, _buttonFont.FontSize));


        ImGui.PushFont(_buttonFont); ImGui.Text("X"); ImGui.PopFont(); ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
        ImGui.Text("Choose  "); ImGui.SameLine();
        if (TabIndex == Tab.Games)
        {
            ImGui.PushFont(_buttonFont); ImGui.Text("T"); ImGui.PopFont(); ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
            ImGui.Text("Toggle Delete  "); ImGui.SameLine();
        }

        ImGui.PushFont(_buttonFont); ImGui.Text("lr"); ImGui.PopFont(); ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
        ImGui.Text("Switch Tabs  "); ImGui.SameLine();

        ImGui.PushFont(_buttonFont); ImGui.Text("L"); ImGui.PopFont(); ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
        ImGui.Text("Close Menu"); ImGui.SameLine();
        ImGui.EndChild();

        ImGui.End();
    }

    public override void Update(bool isdeck)
    {
        base.Update(isdeck);
    }

    private void DrawGames(int index)
    {
        Enumerate(RomDirectory);

        for (int i = 0; i < GameFiles.Count; i++)
        {
            FileDetails file = GameFiles[i];
            ImGui.PushStyleColor(ImGuiCol.Text, !file.IsFile ? YELLOW : DeleteFileMode ? RED : WHITE);
            if (ImGui.Selectable(Path.GetFileName(GameFiles[i].Name), SelectedItem[index] == i))
            {
                if (DeleteFileMode && ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown,false))
                    DeleteFile(file);
                else if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown))
                {
                    if (File.Exists(file.Name))
                    {
                        ImGui.CloseCurrentPopup();
                        Opened = false;
                        LoadGame(file.Name);
                    }
                }
            }
            ImGui.PopStyleColor();

            if (ImGui.IsItemFocused())
                SelectedItem[index] = i;
        }
    }

    private void DrawCheats(int index)
    {
        Enumerate(CheatDirectory);
        List<Cheat> cheats = [.. Cheats.Values];

        if (cheats.Count == 0)
        {
            ImGui.Selectable("No cheats loaded.", true);
            return;
        }

        ImGui.BeginTable("##cheattable", 2);
        ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthFixed, ImGui.GetMainViewport().Size.X - Raylib.MeasureText("OFF ", (int)ImGui.GetFontSize() * 2));
        ImGui.TableSetupColumn("Enabled");
        ImGui.TableNextColumn();

        for (int i = 0; i < cheats.Count;)
        {
            var colorstatus = 0xffffffff;
            var cht = cheats.Where(c => c.Description == cheats[i].Description).ToList();
            if (cht != null)
            {
                colorstatus = cht[0].Enabled ? 0xffffffff : 0xffadadad;
                ImGui.PushID(i);
                ImGui.PushStyleColor(ImGuiCol.Text, colorstatus);

                if (ImGui.Selectable(cht[0].Description, SelectedItem[index] == i))
                {
                    if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown))
                    {
                        cht[0].Enabled = !cht[0].Enabled;
                    }
                }

                if (ImGui.IsItemFocused())
                    SelectedItem[index] = i;

                ImGui.PopStyleColor();
                ImGui.PopID();


                i += cht.Count;
            }

            ImGui.TableNextColumn();

            ImGui.PushStyleColor(ImGuiCol.Text, colorstatus);
            ImGui.Text($"{(cht[0].Enabled ? "ON" : "OFF")}");
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
        }
        ImGui.EndTable();
    }

    private void DrawCheatBrowser(int index)
    {
        for (int i = 0; i < CheatFiles.Count; i++)
        {
            FileDetails file = CheatFiles[i];
            if (ImGui.Selectable(Path.GetFileName(file.Name), SelectedItem[index] == i))
            {
                if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown))
                {
                    if (File.Exists(file.Name))
                    {
                        ImGui.CloseCurrentPopup();
                        Opened = false;
                        LoadCheats(file.Name);
                    }
                }
            }

            if (ImGui.IsItemFocused())
                SelectedItem[index] = i;
        }
    }

    private void DrawLua(int index)
    {
        Enumerate(CheatDirectory);
        for (int i = 0; i < LuaFiles.Count; i++)
        {
            FileDetails file = LuaFiles[i];
            if (ImGui.Selectable(Path.GetFileName(file.Name), SelectedItem[index] == i))
            {
                if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown))
                {
                    if (File.Exists(file.Name))
                    {
                        ImGui.CloseCurrentPopup();
                        Opened = false;
                        LoadLua(file.Name);
                    }
                }
            }

            if (ImGui.IsItemFocused())
                SelectedItem[index] = i;
        }
    }

    private void DrawOptions(int index)
    {
        if (string.IsNullOrEmpty(Emulator.GameName))
        {
            ImGui.Selectable("No game loaded.", true);
            return;
        }

        ImGui.BeginTable("options", 2);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, ImGui.GetMainViewport().Size.X - Raylib.MeasureText("OFF", (int)ImGui.GetFontSize() * 2));
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
        for (int i = 0; i < Options.Count; i++)
        {
            ImGui.TableNextColumn();
            ImGui.PushID(i);
            var option = Options[i];
            if (ImGui.Selectable(option.Name, SelectedItem[index] == i))
            {

            }

            if (ImGui.IsItemFocused())
                SelectedItem[index] = i;

            ImGui.TableNextColumn();
            ImGui.Text(option.Status != null ? $"{option.Status[option.Value],3}" : $"{option.Value,3}");
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawAbout(int index)
    {
        ImGui.Text($"Gmulator v{EmuState.Version}");
        ImGui.Text("Created by nolberto82");
    }
}
