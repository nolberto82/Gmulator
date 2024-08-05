using ImGuiNET;
using KeraLua;
using NLua;
using NLua.Exceptions;
using Raylib_cs;
using Color = Raylib_cs.Color;
using Lua = NLua.Lua;

namespace Gmulator;
public class LuaApi(ImFontPtr[] consolas)
{
    public Lua Lua { get; private set; } = new();
    public ImFontPtr[] Consolas { get; } = consolas;
    public int ConsoleHeight { get; set; }

    private string LuaCwd;
    private string Error;
    private string LuaFile = "";

    public Func<int, byte> Read;

    public byte ReadByte(int a) => Read(a);
    public ushort ReadWord(int a) => (ushort)(Read(a) | Read(a + 1) << 8);
    public void AddCheat(string name, string code)
    {
        if (code.Length == 6 || code.Length == 8)
        {
            //if (name == null || Cheat.ConvertCodes == null)
            //     return;

            string cheatout = "";
            //Cheat.ConvertCodes(name, code, ref cheatout, true);
        }
    }

    public void DrawWindow(string name, LuaTable text)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var texwidth = Program.Screen.Texture.Width;
        var scale = Math.Min((float)width / texwidth, (float)height / ConsoleHeight);
        var leftpos = (width - texwidth * scale) / 2;
        var t = text.Values;

        ImGui.SetNextWindowPos(new(0, Program.MenuHeight));
        ImGui.SetNextWindowSize(new(leftpos, Program.DebuggerEnabled ? height / 2 : height));
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

    public static void DrawText(params object[] args)
    {
        if (args.Length < 3)
            return;

        var c = "ffffffff";
        if (args.Length == 4 && $"{args[3]}".Length == 8)
            c = $"{args[3]}";

        var x = Convert.ToInt32(args[0]);
        var y = Convert.ToInt32(args[1]);
        var text = $"{args[2]}";

        Raylib.DrawText(text, x, y, 14, GetColor(c));
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

    public static Color GetColor(object hexstr)
    {
        var c = Convert.ToInt32($"{hexstr}", 16);
        return new((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
    }

    public void Update()
    {
        if (Lua.State != null && Lua.Globals.Count() > 0)
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

        if (Raylib.IsGamepadButtonPressed(0, GamepadButton.LeftTrigger1) && !Menu.Opened)
        {
            LoadFile(LuaFile);
            if (Lua.State != null)
                Notifications.Init("Lua File Loaded Successfully");
        }
    }

    public void LoadFile(string filename)
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

        Lua.RegisterFunction("mem.addcheat", this, typeof(LuaApi).GetMethod("AddCheat"));
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

    public void CheckLuaFile(string name)
    {
        name = $"{Environment.CurrentDirectory}\\{LuaDirectory}\\{Path.GetFileNameWithoutExtension(name)}.lua";
        if (File.Exists(name))
            LoadFile(name);
    }

    public static void LuaPrint(object text) => System.Console.WriteLine($"{text}");

    public void Reset()
    {
        Lua.Close();
        LuaFile = "";
    }
    public void Unload() => Lua.Close();
}