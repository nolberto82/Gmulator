using ImGuiNET;
using KeraLua;
using NLua;
using NLua.Exceptions;
using Raylib_cs;
using Color = Raylib_cs.Color;
using Lua = NLua.Lua;

namespace Gmulator.Shared;
public class LuaApi(Texture2D screen, ImFontPtr[] consolas, Font font, float menuheight, bool debug)
{
    public Lua Lua { get; private set; } = new();
    public Texture2D Screen { get; } = screen;
    public ImFontPtr[] Consolas { get; } = consolas;
    private Font GuiFont = font;
    public float MenuHeight { get; } = menuheight;
    public bool Debug { get; private set; } = debug;

    private string LuaCwd;
    private string Error;
    private string LuaFile = "";

    public Func<int, byte> Read;

    public void SetDebug(bool v) => Debug = v;
    public byte ReadByte(int a) => Read(a);
    public ushort ReadWord(int a) => (ushort)(Read(a) | Read(a + 1) << 8);

    public void DrawWindow(string name, LuaTable text)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var scale = Math.Min((float)width / Screen.Width, (float)height / Screen.Height);
        var leftpos = (width - Screen.Width * scale) / 2;
        var t = text.Values;

        ImGui.SetNextWindowPos(new(0, !Debug ? MenuHeight : 360));
        ImGui.SetNextWindowSize(new(!Debug ? leftpos : 230, !Debug ? height : 130));
        ImGui.PushFont(Consolas[0]);
        if (ImGui.Begin(name, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoNavFocus))
        {
            foreach (var v in t)
            {
                ImGui.Text(v.ToString());
            }
            ImGui.End();
        }
        ImGui.PopFont();
    }

    public void DrawText(params object[] args)
    {
        if (args.Length < 3)
            return;

        int c = 0xffffff;
        if (args.Length == 4 && $"{args[3]}".Length == 8)
            c = Convert.ToInt32(args[3]);

        var width = Raylib.GetRenderWidth();
        var height = Raylib.GetRenderHeight();
        var scale = Math.Min((float)width / Screen.Width, (float)height / Screen.Height);
        var left = (width - Screen.Width * scale) / 2;
        var top = ((height - Screen.Height * scale) / 2) + MenuHeight;
        var x = (int)(Convert.ToInt32(args[0]) * scale + left);
        var y = (int)(Convert.ToInt32(args[1]) * scale + top);
        var text = $"{args[2]}";
        var fontsize = 32;

        Raylib.DrawRectangle(x - 0, y - 4, (int)(Raylib.MeasureText(text, fontsize)) + 10, fontsize - 2, Color.Black);
        Raylib.DrawTextEx(GuiFont, text, new(x, y), fontsize, 1f, GetColor(c));
    }

    public static void DrawRectangle(params object[] args)
    {
        if (args.Length != 5)
            return;

        var x = Convert.ToInt32(args[0]);
        var y = Convert.ToInt32(args[1]);
        var w = Convert.ToInt32(args[2]);
        var h = Convert.ToInt32(args[3]);
        if ($"{args[4]}".Length != 8)
            return;

        Raylib.DrawRectangle(x, y, w, h, GetColor(args[4]));
    }

    public static Color GetColor(object color)
    {
        var c = Convert.ToInt32(color);
        return new((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
    }

    public void Update(bool opened)
    {
        if (Lua.State != null && Lua.Globals.Any())
        {
            try
            {
                Lua.GetFunction("emu.update").Call();
            }
            catch (LuaScriptException e)
            {
                Notifications.Init(e.Message);
            }
        }

        if (Raylib.IsGamepadButtonPressed(0, GamepadButton.LeftTrigger1) && !opened)
        {
            Load(LuaFile);
            if (Lua.State != null)
                Notifications.Init("Lua File Loaded Successfully");
        }
    }

    public void Load(string filename)
    {
        LuaFile = filename;

        Lua = new();

        Lua.NewTable("emu");
        ((LuaTable)Lua["emu"])["update"] = null;

        Lua.NewTable("gui");

        Lua.RegisterFunction("gui.drawwin", this, typeof(LuaApi).GetMethod("DrawWindow"));
        Lua.RegisterFunction("gui.drawtext", this, typeof(LuaApi).GetMethod("DrawText"));
        Lua.RegisterFunction("gui.drawrect", this, typeof(LuaApi).GetMethod("DrawRectangle"));

        Lua.NewTable("mem");

        Lua.RegisterFunction("mem.readbyte", this, typeof(LuaApi).GetMethod("ReadByte"));
        Lua.RegisterFunction("mem.readword", this, typeof(LuaApi).GetMethod("ReadWord"));

        if (LuaCwd == "" || LuaCwd is null)
        {
            Directory.SetCurrentDirectory(@$"{Environment.CurrentDirectory}");
            LuaCwd = Environment.CurrentDirectory;
            LuaCwd = LuaCwd.Replace('\\', '/');
        }

        Lua.DoString(@"package.path = package.path ..';" + LuaCwd + "/?.lua'");
        try
        {
            if (File.Exists(@$"{filename}"))
            {
                Lua.DoFile(@$"{filename}");
                Notifications.Init("Lua File Loaded Successfully");
            }
            else
            {
                Lua.Close();
                return;
            }
        }
        catch (LuaScriptException e)
        {
            Lua.Close();
            Error = e.Message;
            Error += e.Source;
            LuaPrint(Error);
            Notifications.Init(Path.GetFileName(Error));
            return;
        }

        Error = "";
    }

    public void Save(string name)
    {
        if (File.Exists(LuaFile))
        {
            name = $"{Environment.CurrentDirectory}\\{LuaDirectory}\\{Path.GetFileNameWithoutExtension(name)}.lua";
            var text = File.ReadAllText(LuaFile);
            File.WriteAllText(name, text);
        }
    }

    public void CheckLuaFile(string name)
    {
        name = $"{Environment.CurrentDirectory}\\{LuaDirectory}\\{Path.GetFileNameWithoutExtension(name)}.lua";
        if (File.Exists(name))
            Load(name);
    }

    public static void LuaPrint(object text) => System.Console.WriteLine($"{text}");

    public void Reset()
    {
        Lua.Close();
        LuaFile = "";
    }
    public void Unload() => Lua.Close();
}