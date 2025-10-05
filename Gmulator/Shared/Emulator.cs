
using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using System;
using System.Dynamic;
using System.Numerics;
using System.Text.Json;
using System.Timers;
using System.Xml.Linq;
using Timer = System.Timers.Timer;

namespace Gmulator.Shared
{
    public class Emulator
    {
        public int Console { get; set; }
        public string GameName { get; set; }
        public string LastName { get; set; }
        public Config Config { get; set; }
        public RenderTexture2D Screen { get; set; }
        public Dictionary<int, Cheat> Cheats { get; set; } = [];
        public Cheat Cheat { get; set; } = new();
        public LuaApi LuaApi { get; set; }
        public DebugWindow DebugWindow { get; set; }
        public CheatConverter CheatConverter { get; set; }

        public DebugState State { get; set; }
        public bool FastForward { get; set; }
        public bool Debug { get; set; }
        public bool IsScreenWindow { get; set; }
        public bool IsDeck { get; set; }
        public Vector2 Dimensions { get; set; } = new(GbWidth, GbHeight);
        public object StateLock { get; set; } = new object();
        public SortedDictionary<int, Breakpoint> Breakpoints { get; set; } = [];

        public enum StateResult
        {
            Success, Failed, Mismatch
        }

        public Emulator()
        {
            GameName = "";
        }

        public void Init(int width, int height, int console, float menuheight, ImFontPtr[] imguifont, Font raylibfont)
        {
            Screen = Raylib.LoadRenderTexture(width, height);
            Dimensions = new(width, height);
            Console = console;
            LuaApi = new(Screen.Texture, imguifont, raylibfont, menuheight, Debug);
        }

        public virtual void Reset(string name, bool reset)
        {
            if (!Debug)
                State = DebugState.Running;
            else
                State = DebugState.Break;
            LuaApi?.SetDebug(Debug);
            if (name != "")
                GameName = name;
        }

        public virtual void SetState(DebugState v) => State = v;

        public virtual void Execute(bool opened) { }

        public virtual void Render(float MenuHeight)
        {
            var width = Raylib.GetScreenWidth();
            var height = Raylib.GetScreenHeight();
            var texwidth = Dimensions.X;
            var texheight = Dimensions.Y;
            var scale = Math.Min(width / texwidth, height / texheight);
            var posx = (int)((width - texwidth * scale) / 2);
            var posy = (int)(((height - texheight * scale) / 2) + MenuHeight);

            IsScreenWindow = false;
            if (Debug)
            {
                ImGui.SetNextWindowPos(new(5, MenuHeight + 5));
                ImGui.SetNextWindowSize(new(320, 330));
                if (ImGui.Begin("Screen"))
                {
                    if (ImGui.IsWindowFocused())
                        IsScreenWindow = true;
                    ImGui.Image((nint)Screen.Texture.Id, ImGui.GetContentRegionAvail());

                    Notifications.RenderDebug();
                }
                ImGui.End();

                ImGui.SetNextWindowPos(new(320 + 10, MenuHeight + 5), ImGuiCond.Once);
                ImGui.SetNextWindowSize(new(220, 475));
                if (ImGui.Begin("Debugger", NoScrollFlags))
                {
                    DebugWindow?.Draw();
                    DebugWindow?.DrawCoProcessors();
                }
                ImGui.End();

                ImGui.SetNextWindowPos(new(5, height - 300), ImGuiCond.Once);
                ImGui.SetNextWindowSize(new(430, 300));
                if (ImGui.Begin("Memory", NoScrollFlags))
                    DebugWindow?.DrawMemory();
                ImGui.End();

                ImGui.SetNextWindowPos(new(5 + 435, height - 300));
                ImGui.SetNextWindowSize(new(310, 300));
                if (ImGui.Begin("Breakpoints", NoScrollFlags))
                    DebugWindow?.DrawBreakpoints();
                ImGui.End();

                if (Console == SnesConsole)
                {
                    ImGui.SetNextWindowPos(new(5 + 750, height - 300));
                    ImGui.SetNextWindowSize(new(0, 300));
                    if (ImGui.Begin("DMA", NoScrollFlags))
                        DebugWindow?.DrawDmaInfo();
                    ImGui.End();
                }
            }
            else
            {
                if (Raylib.IsWindowFocused())
                    IsScreenWindow = true;

                Raylib.DrawTexturePro(
                    Screen.Texture,
                    new Rectangle(0, 0, texwidth, texheight),
                    new Rectangle(posx, posy,
                    texwidth * scale,
                    texheight * scale + MenuHeight),
                    Vector2.Zero, 0, Color.White);

                Notifications.Render(posx, (int)MenuHeight, (int)(texwidth * scale), Debug);
                Raylib.DrawFPS(width - 100, (int)(5 + MenuHeight));
            }
        }

        public T GetConsole<T>()
        {
            return (T)Convert.ChangeType(this, typeof(T));
        }

        public virtual void Update() { }
        public virtual void Close() { }
        public virtual void SaveState(int slot, StateResult res)
        {
            switch (res)
            {
                case StateResult.Success:
                    Notifications.Init($"State {slot} Saved Successfully");
                    break;
                case StateResult.Failed:
                    Notifications.Init($"Error Saving Save State {slot}");
                    break;
            }
        }

        public virtual void LoadState(int slot, StateResult res)
        {
            switch (res)
            {
                case StateResult.Success:
                    Notifications.Init($"State {slot} Loaded Successfully");
                    break;
                case StateResult.Failed:
                    Notifications.Init($"Save State {slot} Doesn't Exist");
                    break;
                case StateResult.Mismatch:
                    Notifications.Init($"Save State {slot} Version Mismatch");
                    break;
            }
        }

        public virtual void LoadBreakpoints(string name)
        {
            Breakpoints.Clear();
            var file = @$"{DebugDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
            if (File.Exists(file))
            {
                var res = JsonSerializer.Deserialize<List<Breakpoint>>(File.ReadAllText(file), GEmuJsonContext.Default.Options);
                foreach (var bp in res)
                {
                    Breakpoints.Add(bp.Addr, bp);
                }
            }
        }

        public virtual void SaveBreakpoints(string name)
        {
            if (!Directory.Exists(DebugDirectory)) return;
            if (name == null || name == "") return;
            var bps = Breakpoints.Values.DistinctBy(c => c.Addr).ToList();
            var file = @$"{DebugDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
            var json = JsonSerializer.Serialize(bps, GEmuJsonContext.Default.Options);
            File.WriteAllText(file, json);
        }
    }
}
