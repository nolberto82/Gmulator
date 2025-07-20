using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using ImGuiNET;
using KeraLua;
using NLua;
using NLua.Exceptions;
using Raylib_cs;
using System.Numerics;
using Color = Raylib_cs.Color;
using Lua = NLua.Lua;
using LuaFunction = NLua.LuaFunction;

namespace Gmulator.Shared;
public class LuaApi(Texture2D screen, ImFontPtr[] consolas, Font font, float menuheight, bool debug)
{
    public Lua Lua { get; private set; } = new();
    public Texture2D Screen { get; } = screen;
    private readonly List<Texture2D> Textures = [];
    public ImFontPtr[] Consolas { get; } = consolas;
    private Font GuiFont = font;
    public float MenuHeight { get; } = menuheight;
    public bool Debug { get; private set; } = debug;
    private readonly FileSystemWatcher Watcher = new(CheatDirectory, "*.lua");
    public List<LuaCallback> Callbacks { get; private set; } = [];

    private string LuaCwd;
    private string Error;
    private string LuaFile = "";
    private float OldLeftThumbY;
    private bool FileChanged;

    public Func<int, byte> Read;
    public Action<int, int> Write;
    public Func<Dictionary<string, string>> GetRegister;
    public Action<string, int> SetRegister;

    public void Init()
    {
        Watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
        Watcher.Changed += OnChanged;
        Watcher.EnableRaisingEvents = true;
    }

    public void SetDebug(bool v) => Debug = v;
    public byte ReadByte(int a) => Read(a);
    public ushort ReadWord(int a) => (ushort)(Read(a) | Read(a + 1) << 8);
    public void WriteByte(int a, int v) => Write(a, v);
    public void WriteWord(int a, int v)
    {
        Write(a, (byte)v);
        Write(a + 1, v >> 8);
    }

    public int GetReg(string reg) => Convert.ToInt32(GetRegister()[reg], 16);
    public void SetReg(string reg, int v) => SetRegister(reg, v);

    public static string GetVersion() => EmulatorName;

    public void OnExec(int a)
    {
        try
        {
            foreach (var call in Callbacks)
            {
                if (call.Addr == a)
                    call?.Action(a);
            }
        }
        catch (LuaScriptException e)
        {
            Notifications.Init(e.Message);
        }
    }

    public static void Log(object msg)
    {
        if (msg != null)
        {
            ImGui.SetWindowPos(new(0, 0), ImGuiCond.Once);
            ImGui.SetNextWindowSize(new(200, 0), ImGuiCond.Once);
            if (ImGui.Begin("log"))
            {
                ImGui.Text($"{msg}");
            }
            ImGui.End();
        }
    }

    public void AddCallback(Action<int> func, int a)
    {
        Callbacks.Add(new(func, a));
    }

    private void RemoveCallbacks()
    {
        Callbacks.Clear();
    }

    public void InitMemCallbacks(Emulator emu)
    {
        switch (emu.Console)
        {
            case GbcConsole:
                Read = emu.GetConsole<Gbc>().Mmu.Read;
                Write = emu.GetConsole<Gbc>().Mmu.Write;
                break;
            case NesConsole:
                Read = emu.GetConsole<Nes>().Mmu.Read;
                Write = emu.GetConsole<Nes>().Mmu.Write;
                break;
            case SnesConsole:
                Read = emu.GetConsole<Snes>().ReadMemory;
                Write = emu.GetConsole<Snes>().WriteMemory;
                GetRegister = emu.GetConsole<Snes>().Cpu.GetRegs;
                SetRegister = emu.GetConsole<Snes>().Cpu.SetReg;
                break;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
            return;

        FileChanged = true;
    }

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

        var fontsize = args.Length > 3 && args[3] != null ? Convert.ToInt32(args[3]) : 32;

        uint textcolor = 0xffffffff;
        if (args.Length > 4 && args[4] != null)
            textcolor = Convert.ToUInt32(args[4]);

        var text = $"{args[2]}";
        (var x, var y, _) = GetScaledPosition(args[0], args[1], Screen);

        var tz = Raylib.MeasureTextEx(GuiFont, text, fontsize, 1);
        var textrect = new Rectangle(x, y, tz.X, tz.Y);
        if (args.Length == 6 && args[5] != null)
        {

            uint bgcolor = Convert.ToUInt32(args[5]);
            Raylib.DrawRectangleRec(textrect, GetColor(bgcolor));
        }
        Raylib.DrawTextEx(GuiFont, text, new(x + textrect.Width / 2 - tz.X / 2, y), fontsize, 0f, GetColor(textcolor));
    }

    public void DrawTextCircled(params object[] args)
    {
        if (args.Length < 3)
            return;

        var fontsize = args.Length > 3 && args[3] != null ? Convert.ToInt32(args[3]) : 32;

        uint textcolor = 0xffffffff;
        if (args.Length > 4 && args[4] != null)
            textcolor = Convert.ToUInt32(args[4]);

        uint bgcolor = 0xffffff;
        if (args.Length > 5 && args[5] != null)
            bgcolor = Convert.ToUInt32(args[5]);

        var text = $"{args[2]}";
        (var x, var y, _) = GetScaledPosition(args[0], args[1], Screen);

        var tz = Raylib.MeasureTextEx(GuiFont, text, fontsize, 1);
        Raylib.DrawCircle((int)(x + tz.X / 2), (int)(y + tz.Y / 2), fontsize / 2, GetColor(bgcolor));
        Raylib.DrawTextEx(GuiFont, text, new(x, y), fontsize, 0f, GetColor(textcolor));
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

    public void DrawImage(params object[] args)
    {
        if (args.Length < 3)
            return;

        if (args[0] is not Texture2D)
            return;

        var t = (Texture2D)(args[0]);
        (var x, var y, var scale) = GetScaledPosition(args[1], args[2], Screen);

        //Raylib.DrawTexture(t, x, y, Color.White);
        Raylib.DrawTexturePro(t, new Rectangle(0, 0, t.Width, t.Height),
        new Rectangle(x, y, t.Width * scale,
        t.Height * scale - MenuHeight),
        Vector2.Zero, 0, Color.White);
    }

    public static Texture2D LoadImage(params object[] args)
    {
        if (args.Length != 1 && !File.Exists(($"{args[0]}")))
            return new();

        var path = $"{AppDomain.CurrentDomain.BaseDirectory}{CheatDirectory}";
        return Raylib.LoadTexture(($"{path}\\{args[0]}"));
    }

    public static Color GetColor(object color)
    {
        var c = Convert.ToUInt32(color);
        var r = (byte)(c);
        var g = (byte)(c >> 8);
        var b = (byte)(c >> 16);
        var a = (byte)(c >> 24) == 0 ? 0xff : (byte)(c >> 24);
        return new Color(r, g, b, a);
    }

    public (int, int, float) GetScaledPosition(object vx, object vy, Texture2D texture)
    {
        var width = Raylib.GetRenderWidth();
        var height = Raylib.GetRenderHeight();
        var scale = Math.Min((float)width / texture.Width, (float)height / texture.Height);
        var left = (width - texture.Width * scale) / 2;
        var top = ((height - texture.Height * scale) / 2) + MenuHeight;
        var x = (int)(Convert.ToInt64(vx) * scale + left);
        var y = (int)(Convert.ToInt64(vy) * scale + top);
        return (x, y, scale);
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

        var newleftstickY = Raylib.GetGamepadAxisMovement(0, GamepadAxis.LeftY);
        if (newleftstickY > -0.1f && newleftstickY < 0.1f) newleftstickY = 0.0f; //Deadzone
        if ((newleftstickY > 0 && OldLeftThumbY == 0 || FileChanged) && !opened)
        {
            Load(LuaFile);
            if (Lua.State != null)
                Notifications.Init("Lua File Loaded Successfully");
            FileChanged = false;
        }
        OldLeftThumbY = newleftstickY;
    }

    public void Load(string filename)
    {
        LuaFile = filename;

        Lua = new();
        RemoveCallbacks();

        Lua.NewTable("emu");
        ((LuaTable)Lua["emu"])["update"] = null;

        Lua.RegisterFunction("emu.callback", this, typeof(LuaApi).GetMethod("AddCallback"));
        Lua.RegisterFunction("emu.log", this, typeof(LuaApi).GetMethod("Log"));
        Lua.RegisterFunction("emu.getregister", this, typeof(LuaApi).GetMethod("GetReg"));
        Lua.RegisterFunction("emu.setregister", this, typeof(LuaApi).GetMethod("SetReg"));

        Lua.NewTable("client");

        Lua.RegisterFunction("client.getversion", this, typeof(LuaApi).GetMethod("GetVersion"));

        Lua.NewTable("gui");

        Lua.RegisterFunction("gui.drawwin", this, typeof(LuaApi).GetMethod("DrawWindow"));
        Lua.RegisterFunction("gui.drawtext", this, typeof(LuaApi).GetMethod("DrawText"));
        Lua.RegisterFunction("gui.drawtextcircled", this, typeof(LuaApi).GetMethod("DrawTextCircled"));
        Lua.RegisterFunction("gui.drawrect", this, typeof(LuaApi).GetMethod("DrawRectangle"));
        Lua.RegisterFunction("gui.loadimage", this, typeof(LuaApi).GetMethod("LoadImage"));
        Lua.RegisterFunction("gui.drawimage", this, typeof(LuaApi).GetMethod("DrawImage"));

        Lua.NewTable("mem");

        Lua.RegisterFunction("mem.readbyte", this, typeof(LuaApi).GetMethod("ReadByte"));
        Lua.RegisterFunction("mem.readword", this, typeof(LuaApi).GetMethod("ReadWord"));
        Lua.RegisterFunction("mem.writebyte", this, typeof(LuaApi).GetMethod("WriteByte"));
        Lua.RegisterFunction("mem.writeword", this, typeof(LuaApi).GetMethod("WriteWord"));


        Lua.RegisterFunction("mem.onexec", this, typeof(LuaApi).GetMethod("OnExec"));

        if (LuaCwd == "" || LuaCwd is null)
        {
            Directory.SetCurrentDirectory(@$"{Environment.CurrentDirectory}");
            LuaCwd = Environment.CurrentDirectory;
            LuaCwd = LuaCwd.Replace("\\", "/");
            LuaCwd = $"{LuaCwd}/Cheats";
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
            name = $"{Environment.CurrentDirectory}\\{CheatDirectory}\\{Path.GetFileNameWithoutExtension(name)}.lua";
            var text = File.ReadAllText(LuaFile);
            File.WriteAllText(name, text);
        }
    }

    public void CheckLuaFile(string name)
    {
        name = $"{Environment.CurrentDirectory}\\{CheatDirectory}\\{Path.GetFileNameWithoutExtension(name)}.lua";
        if (File.Exists(name))
            Load(name);
    }

    public static void LuaPrint(object text) => System.Console.WriteLine($"{text}");

    public void Reset()
    {
        Lua.Close();
        LuaFile = "";
        RemoveCallbacks();
    }
    public void Unload()
    {
        Lua.Close();
        Raylib.UnloadTexture(Screen);
        foreach (var t in Textures)
            Raylib.UnloadTexture(t);
    }

    public record LuaCallback(Action<int> Action, int Addr);
}