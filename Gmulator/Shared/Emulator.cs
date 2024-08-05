using GBoy.Core;
using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using ImGuiNET;
using Raylib_cs;
using System.Dynamic;
using System.Numerics;
using System.Text.Json;

namespace Gmulator.Shared
{
    public class Emulator
    {
        public DebugWindow DebugWindow { get; set; }
        public int Console { get; set; }
        public Gbc Gbc { get; set; }
        public Nes Nes { get; set; }

        public virtual void Execute(int State, bool debug)
        { }

        public virtual void Render(float MenuHeight, bool debug)
        {
            var width = Raylib.GetScreenWidth();
            var height = Raylib.GetScreenHeight();
            var texwidth = Program.Screen.Texture.Width;
            var texheight = Program.Screen.Texture.Height;
            var scale = Math.Min((float)width / texwidth, (float)height / texheight);
            var posx = (int)((width - texwidth * scale) / 2);
            var posy = (int)(((height - texheight * scale) / 2) + MenuHeight);

            if (debug)
            {
                scale = Console != GbcSystem ? 1.5f : 2.5f;
                posy = (int)MenuHeight;
                posx = posx * 2;

                if (ImGui.Begin("Debugger"))
                {
                    DebugWindow.Render(DebugWindow.Breakpoints, false);
                    ImGui.End();
                }

                ImGui.SetNextWindowSize(new(400, 270));
                if (ImGui.Begin("Memory", NoScrollFlags))
                {
                    DebugWindow.RenderMemory();
                    ImGui.End();
                }

                if (ImGui.Begin("Breakpoints", NoScrollFlags))
                {
                    DebugWindow.RenderBreakpoints();
                    ImGui.End();
                }

                if (ImGui.Begin("Cpu Info", NoScrollFlags))
                {
                    DebugWindow.RenderCpuInfo();
                    ImGui.End();
                }
            }

            Raylib.DrawTexturePro(
                Program.Screen.Texture,
                new Rectangle(0, 0, texwidth, texheight),
                new Rectangle(posx, posy,
                texwidth * scale,
                texheight * scale - MenuHeight),
                Vector2.Zero, 0, Color.White);

            Raylib.DrawFPS(width - 100, (int)(5 + MenuHeight));
            Notifications.Render((int)((width - texwidth * scale) / 2), (int)MenuHeight, width, height);
        }

        public virtual void Reset(string name,bool reset, bool debug) { }
        public virtual void Update() { }
        public virtual void Close(Dictionary<int, Breakpoint> Breakpoints) { }
        public virtual void SaveState() { }
        public virtual void LoadState() { }
        public virtual dynamic GetConsole() { return default; }
        public virtual T GetRam<T>() { return default; }
        public virtual T GetPc<T>() { return default; }
        public virtual T GetCpuInfo<T>(int i) { return default; }
        public virtual T GetPpuInfo<T>() { return default; }

        public virtual void SaveRam() { }

        public virtual void LoadBreakpoints(Dictionary<int, Breakpoint> Breakpoints, string name)
        {
            Breakpoints.Clear();
            var file = @$"{DebugDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
            if (File.Exists(file))
            {
                var res = JsonSerializer.Deserialize<List<Breakpoint>>(File.ReadAllText(file), GEmuJsonContext.Default.Options);
                foreach (var bp in res)
                {
                    //InsertRemove(bp.Addr, bp.Type, bp.Enabled);
                    Breakpoints.Add(bp.Addr, bp);
                }
            }
        }

        public virtual void SaveBreakpoints(Dictionary<int, Breakpoint> Breakpoints, string name)
        {

            if (name == "" && Breakpoints.Count == 0) return;
            var bps = Breakpoints.Values.DistinctBy(c => c.Addr).ToList();
            var file = @$"{DebugDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
            var json = JsonSerializer.Serialize(bps, GEmuJsonContext.Default.Options);
            File.WriteAllText(file, json);
        }
    }
}
