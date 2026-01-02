using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using Gmulator.Interfaces;
using ImGuiNET;
using KeraLua;
using NLua;
using NLua.Event;
using NLua.Exceptions;
using Raylib_cs;
using System.Collections;
using System.Numerics;
using System.Xml.Linq;
using static Gmulator.Interfaces.IMmu;
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
    public List<LuaMemCallback> MemCallbacks { get; private set; } = [];
    public List<LuaEventCallback> EventCallbacks { get; private set; } = [];

    private string LuaCwd;
    private string Error = "";
    private string LuaFile = "";
    private float OldLeftThumbY;
    private bool FileChanged;

    private Func<int,int> Read;
    private Action<int,int> Write;
    private Func<string, int> GetRegister;
    private Action<string, int> SetRegister;

    public void Init()
    {
        Watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
        Watcher.Changed += OnChanged;
        Watcher.EnableRaisingEvents = true;
    }

    public void SetDebug(bool v) => Debug = v;
    public int ReadByte(int a) => Read(a);
    public int ReadWord(int a) => (Read(a) | Read(a + 1) << 8);
    public void WriteByte(int a, int v) => Write(a, v);
    public void WriteWord(int a, int v)
    {
        Write(a, v & 0xff);
        Write(a + 1, (v >> 8) & 0xff);
    }

    public int GetReg(string reg)
    {
        if (reg == null) return 0;
        return GetRegister(reg);
    }

    public void SetReg(string reg, int v) => SetRegister(reg, v);

    public static string GetVersion() => EmulatorName;

    public void OnExec(int a = -1)
    {
        try
        {
            foreach (var call in MemCallbacks)
            {
                if (call.Action != null)
                {
                    if (call.EndAddr > -1 && (call.StartAddr <= a && a <= call.EndAddr) || call.StartAddr == a)
                        call?.Action(a);
                }
            }
        }
        catch (LuaScriptException e)
        {
            Error = $"{e.Message}\n";
            Error += $"{e.Source}\n";
        }
        catch (Exception e)
        {
            Error += $"{e.Message}\n";
            Error += $"{e.Source}\n";
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

    private void RemoveCallbacks()
    {
        MemCallbacks.Clear();
        EventCallbacks.Clear();
    }

    public void InitMemCallbacks(ICpu cpu, IMmu mmu)
    {
        Read = mmu.ReadByte;
        Write = mmu.WriteByte;
        GetRegister = cpu.GetReg;
        SetRegister = cpu.SetReg;
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

        ImGui.SetNextWindowPos(new(width - leftpos, !Debug ? MenuHeight + 35 : 360));
        ImGui.SetNextWindowSize(new(!Debug ? leftpos : 230, !Debug ? height : 130));
        ImGui.PushFont(Consolas[0]);
        if (ImGui.Begin(name, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoNavFocus))
        {
            foreach (var v in t)
            {
                ImGui.Text($"{v}");
            }
            ImGui.End();
        }
        ImGui.PopFont();
    }

    public void DrawText(params object[] args)
    {
        if (args.Length < 3)
            return;

        var fontsize = 32;// args.Length > 3 && args[3] != null ? Convert.ToInt32(args[3]) : 32;

        uint textcolor = 0xffffffff;
        if (args.Length > 4 && args[3] != null)
            textcolor = Convert.ToUInt32(args[3]);

        var text = $"{args[2]}";
        (var x, var y, _) = GetScaledPosition(args[0], args[1], Screen);

        var tz = Raylib.MeasureTextEx(GuiFont, text, fontsize, 1);
        var textrect = new Rectangle(x, y, tz.X, tz.Y);
        if (args.Length == 5)
            Raylib.DrawRectangleRec(textrect, GetColor(args[4] == null ? 0x00000000 : Convert.ToUInt32(args[4])));

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

    public void DrawRectangle(params object[] args)
    {
        if (args.Length < 5)
            return;

        (var x, var y, var scale) = GetScaledPosition(args[0], args[1], Screen);
        var w = Convert.ToInt32(args[2]);
        var h = Convert.ToInt32(args[3]);
        var bgcolor = args[5] == null || (bool)args[5] == false ? 0xff000000 : Convert.ToUInt32(args[4]);

        Raylib.DrawRectangle(x, y, (int)(w * scale), (int)(h * scale), GetColor(bgcolor));
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

    public static Color GetColor(uint color)
    {
        var c = color != 0xffffffff ? color ^ 0xff000000 : color;
        var r = (byte)(c >> 16);
        var g = (byte)(c >> 8);
        var b = (byte)(c);
        var a = (byte)(c >> 24);
        return new(r, g, b, a);
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

    private void DebugHook(object sender, DebugHookEventArgs e)
    {
        if (ImGui.Begin("Lua Debug"))
        {
            ImGui.Text($"{e.LuaDebug}");
        }
    }

    public static void LogLua(string message)
    {
        if (ImGui.Begin("Lua Debug"))
        {
            ImGui.Text($"{message}");
        }
        ImGui.End();
    }

    public void Update(bool opened)
    {
        if (LuaFile == "") return;
        {
            try
            {
                foreach (var call in EventCallbacks)
                {

                    if (call.Action != null)
                        call?.Action();
                }
            }
            catch (LuaScriptException e)
            {
                Error += $"{e.Message}\n";
                Error += $"{e.Source}\n";
            }
            catch (Exception e)
            {
                Error += $"{e.Message}\n";
                Error += $"{e.Source}\n";
            }
        }

        if (Error != "")
        {
            ImGui.SetWindowPos(new(0, 0));
            ImGui.SetNextWindowSize(new(500, 200));
            if (ImGui.Begin("Errors"))
            {
                ImGui.TextWrapped(Error);
            }
            ImGui.End();
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

    public void AddMemCallback(Action<int> func, Type type, int start, int end = -1)
    {
        if (type == Type.Exec)
            MemCallbacks.Add(new(func, type, start, end));
    }

    public void AddEventCallback(Action func, Type type)
    {
        if (type == Type.Frame)
            EventCallbacks.Add(new(func, type));
    }

    public void Load(string filename)
    {
        if (filename.Contains("alttpr - "))
            filename = "alttpr";

        filename = $"{Environment.CurrentDirectory}\\{CheatDirectory}\\{Path.GetFileNameWithoutExtension(filename)}.lua";
        if (!File.Exists(filename))
        {
            Lua.Close();
            return;
        }

        LuaFile = filename;

        Lua = new();
        if (MemCallbacks.Count > 0)
            RemoveCallbacks();

        Lua.NewTable("emu");

        Lua.RegisterFunction("emu.memcallback", this, typeof(LuaApi).GetMethod("AddMemCallback"));
        Lua.RegisterFunction("emu.eventcallback", this, typeof(LuaApi).GetMethod("AddEventCallback"));
        Lua.RegisterFunction("emu.log", this, typeof(LuaApi).GetMethod("Log"));
        Lua.RegisterFunction("emu.loglua", this, typeof(LuaApi).GetMethod("LogLua"));
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

        Lua.RegisterFunction("print", this, typeof(LuaApi).GetMethod("LuaPrint"));

        LuaRegistrationHelper.Enumeration<Type>(Lua);

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
            Lua.DoFile(@$"{filename}");
            Notifications.Init("Lua File Loaded Successfully");
        }
        catch (LuaScriptException e)
        {
            Lua.Close();
            Error = $"{e.Message}\n";
            Error += $"{e.Source}\n";
            LuaPrint(Error);
            return;
        }
        catch (Exception e)
        {
            Error += $"{e.Message}\n";
            Error += $"{e.Source}\n";
        }

        Error = "";
    }

    public void Save(string filename)
    {
        if (filename.Contains("alttpr - "))
            return;

        if (File.Exists(LuaFile))
        {
            filename = $"{Environment.CurrentDirectory}\\{CheatDirectory}\\{Path.GetFileNameWithoutExtension(filename)}.lua";
            var text = File.ReadAllText(LuaFile);
            File.WriteAllText(filename, text);
        }
    }

    public static void LuaPrint(object text)
    {
        ImGui.SetWindowPos(new(0, 0), ImGuiCond.Once);
        ImGui.SetNextWindowSize(new(200, 0), ImGuiCond.Once);
        if (ImGui.Begin("log"))
        {
            ImGui.Text($"{text}");
        }
        ImGui.End();
    }

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

    public record LuaMemCallback(Action<int> Action, Type Type, int StartAddr, int EndAddr = -1);

    public record LuaEventCallback(Action Action, Type Type);

    public enum Type
    {
        Read, Write, Exec, Frame
    }
}