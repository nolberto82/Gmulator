using Gmulator.Ui;
using Gmulator.Shared;
using ImGuiNET;
using Raylib_cs;
using System.Numerics;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace Gmulator.Shared
{
    public class Emulator
    {
        public int System { get; set; }
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
        public bool Run { get; set; }
        public bool FastForward { get; set; }
        public bool Debug { get; set; }
        public bool IsScreenWindow { get; set; }
        public bool IsDeck { get; set; }
        public bool[] Buttons { get; set; }
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

        public void Init(int width, int height, int system, float menuheight, ImFontPtr[] imguifont, Font raylibfont)
        {
            Screen = Raylib.LoadRenderTexture(width, height);
            Dimensions = new(width, height);
            System = system;
            LuaApi = new(Screen.Texture, imguifont, raylibfont, menuheight, Debug);
        }

        public virtual void Reset(string name, bool reset)
        {
#if DEBUG
            Debug = true;
#endif

            if (!Debug)
                State = DebugState.Running;
            else
                State = DebugState.Break;
            LuaApi?.SetDebug(Debug);
            if (name != "")
                GameName = name;
        }

        public virtual void SetState(DebugState v)
        {
            State = v;
            if (v == DebugState.Running)
                Run = true;
        }

        public virtual void RunFrame(bool opened) { }

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
                DebugWindow?.Draw(Screen.Texture);
                IsScreenWindow = (bool)(DebugWindow?.IsScreenWindow);
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
            }
            Raylib.DrawFPS(width - 80, (int)(5 + MenuHeight));
        }

        public virtual unsafe void UpdateTexture(Texture2D texture, uint[] buffer)
        {
            fixed (uint* pixels = &buffer[0])
                Raylib.UpdateTexture(texture, pixels);
        }

        public void UpdateScreen(uint[] buffer)
        {
            UpdateTexture(Screen.Texture, buffer);
        }

        public T GetConsole<T>()
        {
            return (T)Convert.ChangeType(this, typeof(T));
        }

        public virtual void Update() { }

        public virtual void Input() { }

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
