using ImGuiNET;
using Raylib_cs;
using static Gmulator.DebugWindow;

namespace Gmulator;
public class Menu
{
    private static List<FileDetails> GameFiles { get; set; } = [];
    private static List<FileDetails> LuaFiles { get; set; } = [];
    public static string WorkingDir { get; set; }

    private static bool opened;
    public static bool Opened { get => opened; set => opened = value; }

    private static int TabIndex;
    private const int MaxTabs = 4;
    private static readonly string[] TabNames = ["Games", "Cheats", "Lua", "Options"];
    public static Action<string> LoadLua { get; set; }
    public static Action<string, string> Reset { get; set; }
    public static Action ReloadCheats { get; set; }
    public static string[] Extensions { get; set; }
    private static string Lastname { get; set; } = "";

    public static void Init(bool isdeck, string[] exts)
    {
        if (isdeck)
        {
            var style = ImGui.GetStyle();
            style.Colors[(int)ImGuiCol.HeaderHovered] = new(0.26f, 0.59f, 0.98f, 0.1f);
        }
        Extensions = exts;
        WorkingDir = "C:";
    }

    public static void Open(bool isdeck)
    {
        if (Opened)
        {
            Opened = false;
            return;
        }

        Opened = true;
        GameFiles.Clear();
        LuaFiles.Clear();
        Enumerate(isdeck ? RomDirectory : "");
        Enumerate(LuaDirectory);
    }

    private static void SetFocus(bool isdeck)
    {
        if (isdeck)
        {
            ImGui.SetNextWindowFocus();
        }
    }

    public static void Render(bool isdeck, bool romloaded, float menuheight, Dictionary<int, Cheat> Cheats)
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(new(vp.Size.X, vp.Size.Y));
        ImGui.SetNextWindowPos(new(0, menuheight));
        int BottomHeight = isdeck ? 0 : -51;

        if (Opened)
            ImGui.OpenPopup("Menu");

        if (ImGui.BeginPopupModal("Menu", ref opened))
        {
            if (isdeck)
            {
                if (Raylib.IsGamepadButtonPressed(0, GamepadButton.LeftTrigger1))
                {
                    TabIndex = (TabIndex - 1) < 0 ? MaxTabs - 1 : TabIndex - 1;
                }
                else if (Raylib.IsGamepadButtonPressed(0, GamepadButton.RightTrigger1))
                {
                    TabIndex = (TabIndex + 1) % MaxTabs;
                }
            }

            ImGui.Columns(MaxTabs, "", false);
            for (int i = 0; i < TabNames.Length; i++)
            {
                ImGui.SetColumnWidth(i, 120);

                if (i == TabIndex)
                    ImGui.PushStyleColor(ImGuiCol.Text, GREEN);
                if (isdeck)
                    ImGui.Text(TabNames[i]);
                else
                {
                    if (ImGui.Selectable(TabNames[i], false, ImGuiSelectableFlags.DontClosePopups, new(110, 0)))
                    {
                        if (i != TabIndex)
                            TabIndex = i;
                    }
                }
                if (i == TabIndex)
                    ImGui.PopStyleColor();
                ImGui.NextColumn();
            }
            ImGui.Columns(1);

            if (TabIndex == 0)
            {
                SetFocus(isdeck);
                if (ImGui.BeginChild("##gamefiles", new(-1, BottomHeight), ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AlwaysAutoResize))
                {
                    var drives = GameFiles.Count(d => d.IsDrive);
                    for (int i = 0; i < GameFiles.Count; i++)
                    {
                        var file = GameFiles[i];
                        var name = Path.GetFileName(file.Name);
                        if (name == "")
                            name = file.Name;

                        if (file.IsDrive || file.Name == "..")
                        {
                            if (ImGui.Button(file.Name))
                            {
                                GameFiles.Clear();
                                if (file.Name == "..")
                                {
                                    WorkingDir = Path.GetFullPath(@$"{WorkingDir}/..");
                                    Enumerate("");
                                }
                                else
                                    Enumerate(WorkingDir = file.Name);
                                break;
                            }
                            if (i < drives)
                                ImGui.SameLine();
                        }
                        else
                        {
                            if (Directory.Exists(file.Name))
                                name = $"{name}\\";

                            if (ImGui.Selectable($"{name}", false, ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.DontClosePopups))
                            {
                                if ((ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) || ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown)))
                                {
                                    if (File.Exists(file.Name))
                                    {
                                        ImGui.CloseCurrentPopup();
                                        Opened = false;
                                        Reset(file.Name, Lastname);
                                        Lastname = file.Name;
                                    }
                                    else
                                    {
                                        WorkingDir = file.Name;
                                        GameFiles.Clear();
                                        Enumerate("");
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    ImGui.EndChild();
                }
            }

            if (TabIndex == 1)
            {
                SetFocus(isdeck);
                if (ImGui.BeginChild("##cheats", new(-1, BottomHeight), ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AlwaysAutoResize))
                {
                    if (isdeck && Cheats.Count > 0 && Raylib.IsGamepadButtonPressed(0, GamepadButton.RightFaceLeft))
                        ReloadCheats();

                    for (int i = 0; i < Cheats.Count;)
                    {
                        var res = Cheats.Values.ToList();
                        var cht = res.Where(c => c.Description == res[i].Description).ToList();
                        if (cht.Count > 0)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, cht[0].Enabled ? WHITE : GRAY);
                            if (ImGui.Selectable($"{cht[0].Description.Replace(@"""", "")}"))
                            {
                                if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown))
                                    cht[0].Enabled = !cht[0].Enabled;
                            }
                            ImGui.PopStyleColor();

                            if (!cht[0].Enabled)
                                res.ForEach(x => { if (x.Description == cht[0].Description) x.Enabled = false; });
                            else
                                res.ForEach(x => { if (x.Description == cht[0].Description) x.Enabled = true; });
                            i += cht.Count;
                        }
                    }
                }
                ImGui.EndChild();
            }

            if (TabIndex == 2)
            {
                SetFocus(isdeck);
                if (ImGui.BeginChild("##luafiles", new(-1, BottomHeight), ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AlwaysAutoResize))
                {
                    for (int i = 0; i < LuaFiles.Count; i++)
                    {
                        var file = LuaFiles[i];
                        if (ImGui.Selectable(Path.GetFileName($"{file.Name}"), false, ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.DontClosePopups))
                        {
                            if ((ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) || ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown)))
                            {
                                if (File.Exists(file.Name))
                                {
                                    ImGui.CloseCurrentPopup();
                                    Opened = false;
                                    LoadLua(file.Name);
                                }
                            }
                        }
                    }
                    ImGui.EndChild();
                }
            }

            if (TabIndex == 3)
            {
                SetFocus(isdeck);
                if (ImGui.BeginChild("##emulatoroptions", new(-1, BottomHeight), ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AlwaysAutoResize))
                {
                    if (ImGui.Selectable("Reset", false))
                    {
                        Reset("", Lastname);
                        Opened = false;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.PushStyleColor(ImGuiCol.Text, Input.RotateAB ? WHITE : GRAY);
                    if (ImGui.Selectable("Rotate AB Buttons", false))
                        Input.RotateAB = !Input.RotateAB;
                    ImGui.PopStyleColor();

                    var vol = Raylib.GetMasterVolume();
                    if (ImGui.SliderFloat("Volume", ref vol, 0, 1))
                        Audio.SetVolume(vol);

                    ImGui.EndChild();
                }
            }

            if (ImGui.BeginChild("##buttons", new(-1, 0), ImGuiChildFlags.FrameStyle | (ImGuiChildFlags)NoScrollFlags))
            {
                if (ImGui.Button("Close"))
                {
                    Opened = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndChild();
            }

            ImGui.EndPopup();
        }
    }

    private static void Enumerate(string path)
    {
        DirectoryInfo di;
        if (path != RomDirectory && path != LuaDirectory)
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
}
