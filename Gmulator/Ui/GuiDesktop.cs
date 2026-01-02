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

    private string _cheatOut;

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
        base.Unload();
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
                        Open(Emulator.Config);
                        Emulator.State = DebugState.Paused;
                        ImGui.OpenPopup("Menu");
                    }
                }
                if (ImGui.MenuItem("Reset"))
                    Emulator.Reset("", true, null);

                ImGui.EndMenu();
            }

            ImGui.BeginDisabled(Emulator == null);
            if (ImGui.BeginMenu("Debug"))
            {
                if (ImGui.MenuItem("Show Debugger", "", Emulator.Debug))
                {
                    Emulator.Debug = !Emulator.Debug;
                    LuaApi?.SetDebug(Emulator.Debug);
                }

                ImGui.BeginDisabled(Emulator is not Snes);
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
        if (!Opened) return;

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
                                    Emulator.Config.WorkingDir = WorkingDirectory = Path.GetFullPath(@$"{WorkingDirectory}/..");
                                    Emulator.Config.Save();
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
                                    DeleteFile(file);
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
                                            Emulator.Config.WorkingDir = WorkingDirectory = file.Name;
                                            Emulator.Config.Save();
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
                                        LoadCheats(c.Name);
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

                            for (int i = 0; i < Cheats?.Count;)
                            {
                                var res = Cheats.Values.ToList();
                                var cht = res.Where(c => c.Description == res[i].Description).ToList();
                                if (cht.Count > 0)
                                {
                                    ImGui.PushID(i);
                                    if (ImGui.Selectable($"{cht[0].Description.Replace(@"""", "")}", i == SelOption[ScrCheats], ImGuiSelectableFlags.AllowDoubleClick))
                                    {
                                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                        {

                                            ImGui.OpenPopup("Add/Edit Cheat");
                                        }
                                    }

                                    if (ImGui.BeginPopupModal("Add/Edit Cheat"))
                                    {
                                        ImGui.PushItemWidth(308);
                                        ImGui.InputText($"##cheatinput1", ref cht[0].Description, 256);
                                        ImGui.PopItemWidth();
                                        ImGui.Separator();
                                        string cheatstr = string.Join("\n", cht[0].Codes.Split("+"));
                                        if (ImGui.InputTextMultiline($"##cheatinput2", ref cheatstr, 32768, new(150, 0)))
                                        {
                                            _cheatOut = cheatstr;
                                            cht[0].Codes = cheatstr.Replace("\r\n", "+");
                                        }
                                        OpenCopyContext("Address", ref _cheatOut);
                                        Emulator.ConvertCodes(cht[0], ref _cheatOut, false);
                                        ImGui.SameLine();
                                        ImGui.InputTextMultiline("##cheatoutput", ref _cheatOut, 32768, new(150, 0));
                                        ImGui.Separator();
                                        ImGui.SetCursorPosX(150);
                                        if (ImGui.Button("OK", new(80, 0)))
                                        {
                                            Emulator.ConvertCodes(cht[0], ref _cheatOut, true);
                                            ImGui.CloseCurrentPopup();
                                        }
                                        ImGui.SameLine(235);
                                        if (ImGui.Button("Cancel", new(80, 0)))
                                            ImGui.CloseCurrentPopup();
                                        ImGui.EndPopup();
                                    }

                                    SetActive(ScrCheats, i);

                                    ImGui.TableNextColumn();
                                    var enabled = cht[0].Enabled;
                                    if (ImGui.Checkbox("", ref enabled))
                                    {
                                        ToggleCheat(cht);
                                    }
                                    ImGui.TableNextColumn();

                                    ImGui.SameLine();
                                    if (ImGui.Button("x"))
                                    {
                                        foreach (var c in cht)
                                            Cheats.Remove(c.Address);
                                        Emulator.SaveCheats(CurrentName);
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
                                        LuaApi?.Load(file.Name);
                                        LuaApi?.Save(Emulator.GameName);
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
        _cheatOut = "";
        Open(Emulator.Config);
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
