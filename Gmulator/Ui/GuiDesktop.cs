using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using Gmulator.Shared;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace Gmulator.Ui;

internal class GuiDesktop : Gui
{
    private bool ShowPpuDebug;

    private string _cheatName;
    private string _cheatCodes;
    private string _cheatsOut;

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
            LuaApi?.Update(Opened);

            Input.UpdateGuiInput(Emulator, this);

            ImGui.PushFont(DebugFont[0]);
            
            Update(false);
            Render();
            RenderMenuBar();
            ImGui.PopFont();

            rlImGui.End();
            Raylib.EndDrawing();
        }

        rlImGui.Shutdown();
        base.Unload(false);
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
                    if (!Opened)
                    {
                        Open(Emulator);
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

                ImGui.BeginDisabled(Emulator.System != SnesConsole);
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

    public override void Render()
    {
        ImGuiViewportPtr vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(new(vp.Size.X, vp.Size.Y));
        ImGui.SetNextWindowPos(new(0, 0));
        int bottom = -105;

        if (OpenDialog)
        {
            ImGui.OpenPopup("Menu");
            OpenDialog = false;
        }

        ImGui.PushStyleColor(ImGuiCol.Header, 0xff008000);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0xff008000);
        ImGui.PushStyleColor(ImGuiCol.NavCursor, 0x00000000);
        if (ImGui.BeginPopupModal("Menu", ref Opened, NoScrollFlags))
        {
            ImGui.Columns(MainEntries.Length, "", false);
            for (int i = 0; i < MainEntries.Length; i++)
            {
                ImGui.SetColumnWidth(i, 120);

                ImGui.PushStyleColor(ImGuiCol.WindowBg, i == TabIndex ? RED : WHITE);
                ImGui.PushStyleColor(ImGuiCol.Text, i == TabIndex ? GREEN : WHITE);

                if (ImGui.Selectable(MainEntries[i], false, ImGuiSelectableFlags.NoAutoClosePopups, new(110, 0)))
                    TabIndex = i;

                ImGui.PopStyleColor(2);
                ImGui.NextColumn();
            }
            ImGui.Columns(1);

            switch (TabIndex)
            {
                case ScrGames:
                    GameFiles.Clear();
                    Enumerate("");
                    GameFiles = [.. GameFiles.OrderBy(f => Path.GetExtension(f.Name))];

                    ImGui.BeginChild("##drives", new(0, 30));
                    {
                        for (int i = 0; i < GameFiles.Count; i++)
                        {
                            var file = GameFiles[i];
                            if (!file.IsDrive && file.Name != "..")
                                break;
                            if (ImGui.Button(file.Name))
                            {
                                GameFiles.Clear();
                                if (file.Name == "..")
                                {
                                    Config.WorkingDir = WorkingDirectory = Path.GetFullPath(@$"{WorkingDirectory}/..");
                                    Config.Save();
                                    Enumerate("");
                                }
                                else
                                    Enumerate(WorkingDirectory = file.Name);
                                break;
                            }
                            if (i < GameFiles.Count(d => d.IsDrive))
                                ImGui.SameLine();
                        }
                        ImGui.EndChild();
                    }

                    if (ImGui.BeginChild("##gamefiles", new(0, bottom), ImGuiChildFlags.FrameStyle))
                    {
                        var start = GameFiles.Count(d => d.IsDrive) + 1;
                        for (int i = start; i < GameFiles.Count; i++)
                        {
                            ImGui.PushID(i);
                            var file = GameFiles[i];
                            var name = Path.GetFileName(file.Name);
                            if (name == "")
                                name = file.Name;

                            ImGui.PushStyleColor(ImGuiCol.Text, !file.IsFile ? YELLOW : DeleteFileMode ? RED : WHITE);
                            if (ImGui.Selectable($"{name}", i == SelOption[ScrGames], ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.NoAutoClosePopups))
                            {
                                if (DeleteFileMode && ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown))
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
                                else
                                {
                                    if ((ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) || ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown)))
                                    {
                                        if (File.Exists(file.Name))
                                        {
                                            ImGui.CloseCurrentPopup();
                                            LoadGame(file.Name);
                                        }
                                        else
                                        {
                                            Config.WorkingDir = WorkingDirectory = file.Name;
                                            Config.Save();
                                            GameFiles.Clear();
                                            Enumerate("");
                                            ImGui.PopStyleColor();
                                            ImGui.PopID();
                                            break;
                                        }
                                    }
                                }
                            }
                            ImGui.PopStyleColor();
                            ImGui.PopID();

                        }
                        ImGui.EndChild();
                    }
                    break;
                case ScrCheats:
                    if (CheatDialog)
                    {
                        if (ImGui.BeginChild("CheatDialog", new(0, bottom), ImGuiChildFlags.FrameStyle))
                        {
                            CheatFiles.Clear();
                            Enumerate(CheatDirectory);
                            for (int i = 0; i < CheatFiles.Count; i++)
                            {
                                var c = CheatFiles[i];
                                if (ImGui.Selectable($"{Path.GetFileName(c.Name)}", i == SelOption[ScrBrowser], ImGuiSelectableFlags.AllowDoubleClick))
                                {
                                    if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown) || ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                        LoadCheat(c.Name);
                                }
                            }
                            ImGui.EndChild();
                        }
                    }
                    else
                    {
                        if (ImGui.BeginChild("##cheats", new(0, bottom), ImGuiChildFlags.FrameStyle))
                        {
                            ImGui.BeginTable("##cheattable", 3);
                            ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthFixed, vp.Size.X - Raylib.MeasureText("OFF ", (int)ImGui.GetFontSize() * 2));
                            ImGui.TableSetupColumn("Enabled");
                            ImGui.TableSetupColumn("");
                            ImGui.TableNextColumn();

                            for (int i = 0; i < Emulator.Cheats?.Count;)
                            {
                                var res = Emulator.Cheats.Values.ToList();
                                var cht = res.Where(c => c.Description == res[i].Description).ToList();
                                if (cht.Count > 0)
                                {
                                    ImGui.PushID(i);
                                    if (ImGui.Selectable($"{cht[0].Description.Replace(@"""", "")}", i == SelOption[ScrCheats], ImGuiSelectableFlags.AllowDoubleClick))
                                    {
                                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                            ToggleCheat(res, i);
                                    }

                                    SetActive(ScrCheats, i);

                                    ImGui.TableNextColumn();
                                    var enabled = cht[0].Enabled;
                                    ImGui.Checkbox("", ref enabled);
                                    ImGui.TableNextColumn();

                                    ImGui.SameLine();
                                    if (ImGui.Button("x"))
                                    {
                                        foreach (var c in cht)
                                        {
                                            Emulator.Cheats.Remove(c.Address);
                                        }
                                        CheatConverter.Save(CurrentName);
                                    }

                                    if (!cht[0].Enabled)
                                        res.ForEach(x => { if (x.Description == cht[0].Description) x.Enabled = false; });
                                    else
                                        res.ForEach(x => { if (x.Description == cht[0].Description) x.Enabled = true; });
                                    ImGui.PopID();
                                    i += cht.Count;
                                    ImGui.TableNextColumn();
                                }
                            }
                            ImGui.EndTable();

                            if (ImGui.BeginPopupContextWindow("cheatmenu"))
                            {
                                ImGui.PushItemWidth(308);
                                ImGui.InputText($"##cheatinput1", ref _cheatName, 256);
                                ImGui.PopItemWidth();
                                ImGui.Separator();
                                ImGui.InputTextMultiline($"##cheatinput2", ref _cheatCodes, 32768, new(150, 0));
                                OpenCopyContext("Address", ref _cheatCodes);
                                CheatConverter.ConvertCodes(_cheatName, _cheatCodes, ref _cheatsOut, false, Emulator);
                                ImGui.SameLine();
                                ImGui.InputTextMultiline("##cheatoutput", ref _cheatsOut, 32768, new(150, 0));
                                ImGui.Separator();
                                ImGui.SetCursorPosX(150);
                                if (ImGui.Button("OK", new(80, 0)))
                                {
                                    CheatConverter.ConvertCodes(_cheatName, _cheatCodes, ref _cheatsOut, true, Emulator);
                                    CheatConverter.Save(CurrentName);
                                    ImGui.CloseCurrentPopup();
                                }
                                ImGui.SameLine(235);
                                if (ImGui.Button("Cancel", new(80, 0)))
                                    ImGui.CloseCurrentPopup();
                                ImGui.EndPopup();
                            }
                        }
                        ImGui.EndChild();
                    }
                    break;
                case ScrLua:
                    LuaFiles.Clear();
                    Enumerate(CheatDirectory);
                    if (ImGui.BeginChild("##luafiles", new(0, bottom), ImGuiChildFlags.FrameStyle))
                    {
                        for (int i = 0; i < LuaFiles.Count; i++)
                        {
                            var file = LuaFiles[i];
                            if (ImGui.Selectable(Path.GetFileName($"{file.Name}"), i == SelOption[ScrLua], ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.NoAutoClosePopups))
                            {
                                if ((ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) || ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown)))
                                {
                                    if (File.Exists(file.Name))
                                    {
                                        Opened = false;
                                        Emulator?.LuaApi?.Load(file.Name);
                                        Emulator?.LuaApi?.Save(Emulator.GameName);
                                    }
                                }
                            }
                        }
                        ImGui.EndChild();
                    }
                    break;

                case ScrOptions:
                    if (ImGui.BeginChild("##emulatoroptions", new(0, bottom), ImGuiChildFlags.FrameStyle))
                    {
                        ImGui.BeginTable("##options", 2);
                        {
                            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, vp.Size.X - Raylib.MeasureText("OFF", (int)ImGui.GetFontSize() * 2));
                            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);

                            for (int i = 0; i < Options.Count; i++)
                            {
                                Option o = Options[i];
                                var v = o.Status == null ? $"{o.Value}" : o.Status[(int)o.Value];
                                if (TableRowSelect(o.Name, v, i == SelOption[ScrOptions]))
                                {
                                    SelOption[ScrOptions] = i;
                                }
                            }
                            ImGui.EndTable();
                        }
                        ImGui.EndChild();
                    }
                    break;
            }

            ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xffff0000);
            ImGui.BeginChild("##info");
            ImGui.SetCursorPos(new(5, 10));
            if (ImGui.BeginTable("infobuttons", 2))
            {
                ImGui.TableSetupColumn("##info0", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("##info1");
                foreach (var t in TabInfo[TabIndex == 1 && CheatDialog ? ScrBrowser : TabIndex][1])
                {
                    TableRow(t.Button, t.Description);
                }
                ImGui.EndTable();
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();

            ImGui.EndPopup();
        }
        ImGui.PopStyleColor(3);

        base.Render();
    }

    public override void Update(bool isdeck)
    {
        base.Update(isdeck);
    }

    public override void Init(bool isdeck)
    {
        base.Init(isdeck);
        _cheatName = _cheatCodes = _cheatsOut = "";
        Open(Emulator);
        TabIndex = ScrGames;
    }

    private void SetActive(int t, int i)
    {
        if (ImGui.IsItemHovered())
            SelOption[t] = i;
    }

    public override void ResetGame(string name)
    {
        base.ResetGame(name);
    }
}
