using ImGuiNET;
using Microsoft.VisualBasic.FileIO;
using Raylib_cs;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace Gmulator.Shared;

public class Menu
{
    private const int MaxTabs = 5;
    private const int TabGames = 0;
    private const int TabCheats = 1;
    private const int TabLua = 2;
    private const int TabOptions = 3;
    private const int TabCheatsBrowser = 4;

    private int TabIndex;
    private Config Config;
    private List<FileDetails> GameFiles { get; set; } = [];
    private List<FileDetails> CheatFiles { get; set; } = [];
    private List<FileDetails> LuaFiles { get; set; } = [];
    private readonly string[] TabNames = ["Games", "Cheats", "Lua", "Options"];

    private List<Option> Options;
    public string LastName { get; private set; } = "";
    private string GameName;
    private string CheatName;
    private string CheatCodes;
    private string CheatsOut;
    public Action<Emulator> ReloadCheats { get; set; }
    public Action<string, string> Reset { get; set; }
    public bool Opened;
    public bool OpenDialog;
    public bool CheatDialog;
    public string WorkingDir { get; set; }
    public string[] Extensions { get; set; }
    private Emulator Emu { get; set; }
    private readonly bool[] Initial = new bool[MaxTabs];
    private readonly int[] ItemSelected = new int[MaxTabs];

    public Menu()
    {
        GameName = CheatName = CheatCodes = CheatsOut = "";
    }

    public void Init(bool isdeck, string[] exts)
    {
        if (isdeck)
            SetFocus();

        Extensions = exts;

        var style = ImGui.GetStyle();
        style.Colors[(int)ImGuiCol.HeaderHovered] = new(0.0f, 0.5f, 0.0f, 1f);
        style.Colors[(int)ImGuiCol.NavCursor] = new(0.0f, 0.5f, 0.0f, 1f);
        style.Colors[(int)ImGuiCol.Header] = new(0.0f, 0.5f, 0.0f, 1f);
        style.Colors[(int)ImGuiCol.FrameBg] = new(0.06f, 0.06f, 0.06f, 0.16f);
    }

    public void Open(Emulator emu)
    {
        if (Opened)
        {
            Opened = false;
            CheatDialog = false;
            return;
        }

        this.Emu = emu;
        Config = emu.Config;
        WorkingDir = Config.WorkingDir;
        Opened = true;
        OpenDialog = true;

        Options =
        [
            new("Frameskip", [Config.FrameSkip, 1, 2, 9], null, false, ChangeOption, null),
            new("Volume", [Config.Volume, 0.1f, 0, 1],null, false, ChangeOption, null),
            new("Rotate AB Buttons", [Config.RotateAB, 1, 0, 1],["OFF","ON"], true, ChangeOption, null),
            new("Copy Hacks", [0, 0, 0, 0], [""], true, null, CopyHacks),
        ];
    }

    private float ChangeOption(Option o)
    {
        var io = ImGui.GetIO();
        if (Raylib.IsGamepadButtonPressed(0, BtnLeft) || io.MouseWheel < 0)
        {
            o.Value -= o.Add;
            if (o.Value <= o.Min)
                o.Value = o.Min;
        }
        else if (Raylib.IsGamepadButtonPressed(0, BtnRight) || io.MouseWheel > 0)
        {
            o.Value += o.Add;
            if (o.Value > o.Max)
                o.Value = o.Max;
        }

        return MathF.Round(o.Value, 2);
    }

    private unsafe void SetFocus()
    {
        var io = ImGui.GetIO();
        ImGui.SetNextWindowFocus();
        io.ClearInputMouse();

        io.NativePtr->KeysData_121.Down = 0;

        io.ClearInputMouse();

        if (!Initial[(int)TabIndex] && io.NativePtr->KeysData_127.Down == 1)
        {
            io.NativePtr->KeysData_127.Down = 0;
            if ((TabIndex == 0 && GameFiles.Count > 0 || TabIndex == 1 && Emu.Cheats.Count > 0 || TabIndex == 2 && LuaFiles.Count > 0) || TabIndex == 3 || CheatDialog)
                Initial[TabIndex] = true;
        }

        if (!Initial[TabIndex])
        {
            if ((TabIndex == 0 && GameFiles.Count > 0 || TabIndex == 1 && Emu.Cheats.Count > 0 || TabIndex == 2 && LuaFiles.Count > 0) || TabIndex == 3 || CheatDialog)
                io.NativePtr->KeysData_127.Down = 1;
        }
    }

    private void SetActive(int t, int i)
    {
        if (ImGui.IsItemHovered())
            ItemSelected[t] = i;
    }

    public void Update(bool isdeck)
    {
        if (Opened)
        {
            if (isdeck)
            {
                if (Raylib.IsGamepadButtonPressed(0, GamepadButton.LeftTrigger1))
                    TabIndex = (TabIndex - 1) < 0 ? MaxTabs - 2 : TabIndex - 1;
                else if (Raylib.IsGamepadButtonPressed(0, GamepadButton.RightTrigger1))
                    TabIndex = (TabIndex + 1) % (MaxTabs - 1);
            }

            switch (TabIndex)
            {
                case TabGames:

                    break;
                case TabCheats:
                    if (Raylib.IsGamepadButtonPressed(0, BtnY) && Emu?.GameName != "")
                        CheatDialog = !CheatDialog;
                    break;
                case TabLua:
                    break;
                case TabOptions:
                    Option o = Options[ItemSelected[TabOptions]];
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
    }

    public void Render(bool isdeck, Cheat Cheat)
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(new(vp.Size.X, vp.Size.Y));
        ImGui.SetNextWindowPos(new(0, 0));
        int bottom = -105;

        if (OpenDialog)
        {
            ImGui.OpenPopup("Menu");
            OpenDialog = false;
        }

        ImGui.PushStyleColor(ImGuiCol.FrameBg, 0x00000000);
        if (ImGui.BeginPopupModal("Menu", ref Opened, NoScrollFlags))
        {
            ImGui.Columns(TabNames.Length, "", false);
            for (int i = 0; i < TabNames.Length; i++)
            {
                ImGui.SetColumnWidth(i, 120);

                ImGui.PushStyleColor(ImGuiCol.WindowBg, i == TabIndex ? RED : WHITE);
                ImGui.PushStyleColor(ImGuiCol.Text, i == TabIndex ? GREEN : WHITE);

                if (isdeck)
                    ImGui.Text(TabNames[i]);
                else
                {
                    if (ImGui.Selectable(TabNames[i], false, ImGuiSelectableFlags.NoAutoClosePopups, new(110, 0)))
                        TabIndex = i;
                }
                ImGui.PopStyleColor(2);
                ImGui.NextColumn();
            }
            ImGui.Columns(1);

            if (isdeck)
                SetFocus();

            switch (TabIndex)
            {
                case TabGames:
                    GameFiles.Clear();
                    Enumerate(isdeck ? RomDirectory : "");
                    GameFiles = [.. GameFiles.OrderBy(f => Path.GetExtension(f.Name))];

                    if (!isdeck)
                    {
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
                                        Config.WorkingDir = WorkingDir = Path.GetFullPath(@$"{WorkingDir}/..");
                                        Config.Save();
                                        Enumerate("");
                                    }
                                    else
                                        Enumerate(WorkingDir = file.Name);
                                    break;
                                }
                                if (i < GameFiles.Count(d => d.IsDrive))
                                    ImGui.SameLine();
                            }
                            ImGui.EndChild();
                        }
                    }

                    if (ImGui.BeginChild("##gamefiles", new(0, bottom), ImGuiChildFlags.FrameStyle))
                    {
                        var start = isdeck ? 0 : GameFiles.Count(d => d.IsDrive) + 1;
                        for (int i = start; i < GameFiles.Count; i++)
                        {
                            var file = GameFiles[i];
                            var name = Path.GetFileName(file.Name);
                            if (name == "")
                                name = file.Name;

                            ImGui.PushStyleColor(ImGuiCol.Text, !file.IsFile ? YELLOW : WHITE);
                            if (ImGui.Selectable($"{name}", i == ItemSelected[TabGames], ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.NoAutoClosePopups))
                            {
                                if ((ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) || ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown)))
                                {
                                    if (File.Exists(file.Name))
                                    {
                                        ImGui.CloseCurrentPopup();
                                        Config.Save();
                                        Opened = false;
                                        Reset(GameName = file.Name, LastName);
                                        LastName = file.Name;
                                    }
                                    else
                                    {
                                        Config.WorkingDir = WorkingDir = file.Name;
                                        Config.Save();
                                        GameFiles.Clear();
                                        Enumerate("");
                                        ImGui.PopStyleColor();
                                        break;
                                    }
                                }
                            }
                            SetActive(TabGames, i);
                            ImGui.PopStyleColor();
                        }
                        ImGui.EndChild();
                    }
                    break;
                case TabCheats:
                    if (CheatDialog)
                    {
                        if (ImGui.BeginChild("CheatDialog", new(0, bottom), ImGuiChildFlags.FrameStyle))
                        {
                            CheatFiles.Clear();
                            Enumerate(CheatDirectory);
                            for (int i = 0; i < CheatFiles.Count; i++)
                            {
                                var c = CheatFiles[i];
                                if (ImGui.Selectable($"{Path.GetFileName(c.Name)}", i == ItemSelected[TabCheatsBrowser], ImGuiSelectableFlags.AllowDoubleClick))
                                {
                                    if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown) || ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                    {
                                        Cheat.Load(Emu, true, c.Name);
                                        CheatDialog = false;
                                    }
                                }
                                SetActive(TabCheatsBrowser, i);
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

                            for (int i = 0; i < Emu.Cheats?.Count;)
                            {
                                var res = Emu.Cheats.Values.ToList();
                                var cht = res.Where(c => c.Description == res[i].Description).ToList();
                                if (cht.Count > 0)
                                {
                                    ImGui.PushID(i);
                                    if (ImGui.Selectable($"{cht[0].Description.Replace(@"""", "")}", i == ItemSelected[TabCheats], ImGuiSelectableFlags.SpanAllColumns))
                                    {

                                    }

                                    SetActive(TabCheats, i);

                                    ImGui.TableNextColumn();

                                    if (i == ItemSelected[TabCheats] && (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown, true)))
                                    {
                                        var r = res.Where(c => c.Description == res[ItemSelected[TabCheats]].Description).ToList();
                                        if (r.Count > 0)
                                            r[0].Enabled = !r[0].Enabled;
                                        CheatConverter.Save(GameName);
                                    }

                                    ImGui.Text(cht[0].Enabled ? "ON" : "OFF");
                                    //ImGui.Text($"{string.Join("+", cht[0].Codes.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))}");

                                    ImGui.TableNextColumn();

                                    if (!isdeck)
                                    {
                                        ImGui.SameLine();
                                        if (ImGui.Button("x"))
                                        {
                                            Emu.Cheats.TryGetValue(cht[0].Address, out Cheat c);
                                            if (c != null)
                                                Emu.Cheats.Remove(c.Address);
                                            CheatConverter.Save(GameName);
                                        }
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
                                ImGui.InputText($"##cheatinput1", ref CheatName, 256);
                                ImGui.PopItemWidth();
                                ImGui.Separator();
                                ImGui.InputTextMultiline($"##cheatinput2", ref CheatCodes, 32768, new(150, 0));
                                OpenCopyContext("Address", ref CheatCodes);
                                CheatConverter.ConvertCodes(CheatName, CheatCodes, ref CheatsOut, false, Emu);
                                ImGui.SameLine();
                                ImGui.InputTextMultiline("##cheatoutput", ref CheatsOut, 32768, new(150, 0));
                                ImGui.Separator();
                                ImGui.SetCursorPosX(150);
                                if (ImGui.Button("OK", new(80, 0)))
                                {
                                    CheatConverter.ConvertCodes(CheatName, CheatCodes, ref CheatsOut, true, Emu);
                                    CheatConverter.Save(GameName);
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
                case TabLua:
                    LuaFiles.Clear();
                    Enumerate(LuaDirectory);
                    if (ImGui.BeginChild("##luafiles", new(0, bottom), ImGuiChildFlags.FrameStyle))
                    {
                        for (int i = 0; i < LuaFiles.Count; i++)
                        {
                            var file = LuaFiles[i];
                            if (ImGui.Selectable(Path.GetFileName($"{file.Name}"), i == ItemSelected[TabLua], ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.NoAutoClosePopups))
                            {
                                if ((ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) || ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown)))
                                {
                                    if (File.Exists(file.Name))
                                    {
                                        Opened = false;
                                        Emu?.LuaApi?.Load(file.Name);
                                        Emu?.LuaApi?.Save(Emu.GameName);
                                    }
                                }
                            }
                            SetActive(TabLua, i);
                        }
                        ImGui.EndChild();
                    }
                    break;

                case TabOptions:
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
                                if (TableRowSelect(o.Name, v, i == ItemSelected[TabOptions]))
                                {
                                    ItemSelected[TabOptions] = i;
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
                TableRow("L/R", "Switch Tabs");
                if (TabIndex < 3)
                {
                    if (!CheatDialog && TabIndex == TabCheats)
                        TableRow("Cross", "Toggle Cheats");
                    else
                        TableRow("Cross", "Select File");
                }

                if (TabIndex == TabCheats)
                    TableRow("Square", !CheatDialog ? "Open Cheats Browser" : "Close Cheats Browser");
                else if (TabIndex == TabLua)
                    TableRow("L", "Reload Lua File In Game");
                else if (TabIndex == TabOptions)
                {
                    if (isdeck)
                        TableRow("Left/Right", "Change Options");
                    else
                        TableRow("Mouse Wheel", "Change Options");
                }

                ImGui.EndTable();
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();

            ImGui.EndPopup();
        }

        ImGui.PopStyleColor();
    }

    private void CopyHacks(bool isdeck)
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
        //using StreamReader sr = process.StandardError;
        //File.WriteAllText($"{Environment.CurrentDirectory}/error.txt", sr.ReadToEnd());
    }

    private void Enumerate(string path)
    {
        DirectoryInfo di;
        if (path == "")
        {
            foreach (var file in DriveInfo.GetDrives())
            {
                if (file.IsReady)
                    GameFiles.Add(new(file.Name, file.IsReady, false));
            }
            di = new(WorkingDir);
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
            else if (Extensions.Contains(ext))
                GameFiles.Add(new(file.FullName, false, true));

        }
    }

    private struct FileDetails(string name, bool isDrive, bool isFile)
    {
        public string Name = name;
        public bool IsDrive = isDrive;
        public bool IsFile = isFile;
    }

    private class Option(string name, float[] values, string[] status, bool press, Func<Option, float> func, Action<bool> action)
    {
        public string Name { get; set; } = name;
        public float Value { get; set; } = values[0];
        public float Add { get; set; } = values[1];
        public float Min { get; set; } = values[2];
        public float Max { get; set; } = values[3];
        public string[] Status { get; set; } = status;
        public bool Press { get; } = press;
        public Func<Option, float> Func { get; set; } = func;
        public Action<bool> Action { get; set; } = action;
    }

    private class Status(string name, bool enabled)
    {
        public string Name { get; set; } = name;
        public bool Enabled { get; set; } = enabled;
    }
}