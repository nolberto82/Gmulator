using Gmulator.Interfaces;
using Gmulator.Shared.Lua;
using ImGuiNET;
using NLua;
using NLua.Exceptions;
using System.Data;
using System.Numerics;
using Color = Raylib_cs.Color;
using Lua = NLua.Lua;

namespace Gmulator.Shared.LuaScript;

public class LuaManager(RenderTexture2D screen, ImFontPtr[] consolas, Font font, float menuheight, bool debug)
{
    private NLua.Lua _state;
    public RenderTexture2D Screen => screen;
    private readonly List<Texture2D> Textures = [];
    public ImFontPtr[] Consolas => consolas;
    public Font GuiFont => font;
    public float MenuHeight { get; } = menuheight;
    public bool Debug { get; private set; } = debug;
    private readonly FileSystemWatcher Watcher = new(CheatDirectory, "*.lua");

    private string LuaCwd;
    private string Error = "";
    private string LuaFile = "";
    private bool _fileChanged;



    public void Init()
    {
        Watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
        Watcher.Changed += OnChanged;
        Watcher.EnableRaisingEvents = true;
    }

    public void Load(string filename, IConsole console)
    {
        if (filename.Contains("alttpr - "))
            filename = "alttpr";

        filename = $"{Environment.CurrentDirectory}\\{CheatDirectory}\\{Path.GetFileNameWithoutExtension(filename)}.lua";
        if (!File.Exists(filename))
            return;

        LuaFile = filename;

        _state = new();

        var emu = new EmuLua(_state);
        var mem = new MemLua(_state, console);
        var gui = new GuiLua(_state, this);

        //Lua.Globals["mem.onexec"] = () => OnExec;

        _state.RegisterFunction("print", this, typeof(LuaManager).GetMethod("LuaPrint"));

        if (string.IsNullOrEmpty(LuaCwd))
        {
            Directory.SetCurrentDirectory(@$"{Environment.CurrentDirectory}");
            LuaCwd = Environment.CurrentDirectory;
            LuaCwd = LuaCwd.Replace("\\", "/");
            LuaCwd = $"{LuaCwd}/Cheats";
        }

        try
        {
            _state.DoString(@"package.path = package.path ..';" + LuaCwd + "/?.lua'");
            _state.DoFile(@$"{filename}");
            Notifications.Init("Lua File Loaded Successfully");
        }
        catch (LuaScriptException e)
        {
            Error = $"{e.Message}\n{e.Source}";
            _state.Close();
            return;
        }
        catch (SyntaxErrorException e)
        {
            Error = $"{e.Message}\n{e.Source}";
            _state.Close();
            return;
        }

        Error = "";
    }

    public void SetDebug(bool value) => Debug = value;


    public static string GetVersion() => EmulatorName;

    public void OnExec(int addr = -1)
    {
        if (_state == null) return;
        try
        {
            foreach (var call in EmuLua.MemCallbacks)
            {
                if (call.Action != null)
                {
                    if (call.EndAddr > -1 && call.StartAddr <= addr && addr <= call.EndAddr || call.StartAddr == addr)
                        call?.Action(addr);
                }
            }
        }
        catch (LuaScriptException e)
        {
            Error += $"{e.Message}\n";
            Error += $"{e.Source}\n";
            _state?.Close();
        }
        catch (SyntaxErrorException e)
        {
            Error += $"{e.Message}\n";
            Error += $"{e.Source}\n";
            _state?.Close();
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

    public void InitMemCallbacks(IConsole console)
    {

    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
            return;

        _fileChanged = true;
        //Load(LuaFile, console);
    }

    public void Update(bool opened)
    {
        if (_state == null) return;
        if (LuaFile == "" || opened) return;
        {
            try
            {
                for (int i = 0; i < EmuLua.EventCallbacks.Count; i++)
                {
                    var call = EmuLua.EventCallbacks[i];
                    if (call.Action != null)
                        call?.Action();
                }
            }
            catch (LuaScriptException e)
            {
                Error += $"{e.Message}\n";
                Error += $"{e.Source}\n";
                _state?.Close();
            }
            catch (SyntaxErrorException e)
            {
                Error += $"{e.Message}\n";
                Error += $"{e.Source}\n";
                _state?.Close();
            }
        }

        if (Error != "")
        {
            if (Debug)
            {
                ImGui.Begin("LuaError");
                ImGui.TextWrapped($"{Error}");
                ImGui.End();
            }
            else
                Raylib.DrawTextEx(GuiFont, $"{Error}", new(0, 50), 24, 1, Color.Red);
        }
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

    public void LuaPrint(object text) => Raylib.DrawTextEx(GuiFont, $"{text}", new(0, 0), 18, 1, Color.Red);

    public void Reset()
    {
        LuaFile = "";
        _state?.Close();
        //RemoveCallbacks();
    }
    public void Unload()
    {
        _state?.Close();
        Raylib.UnloadRenderTexture(Screen);
        foreach (var t in Textures)
            Raylib.UnloadTexture(t);
    }

    public static class Type
    {
        public const int Read = 0;
        public const int Write = 1;
        public const int Exec = 2;
        public const int Frame = 3;
    }
}