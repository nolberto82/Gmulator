using ImGuiNET;
using Raylib_cs;
using System.Runtime.InteropServices;

namespace Gmulator.Shared;
internal static class Notifications
{
    private static string[] Text;
    private static int Frames;
    private static ImFontPtr ImGuiFont;
    private static Font GuiFont;

    public static void SetFont(ImFontPtr font, Font raylibfont)
    {
        ImGuiFont = font;
        GuiFont = raylibfont;
    }

    public static void Init(string text)
    {
        Text = text.Split([": ", "\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Frames = 125;
    }

    public static void Render(int x, int y, int width, bool debug)
    {
        if (Frames-- > 0)
        {
            if (!debug)
                RenderText(Text, x, y, width, Color.Yellow, debug);
        }
    }

    public static void RenderDebug()
    {
        if (Frames-- > 0)
        {
            var list = ImGui.GetForegroundDrawList();
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            ImGui.SetCursorPos(pos);
            list.AddRectFilled(pos, new(pos.X + size.X, 5 + 15 * Text.Length), 0xc0000000);
            foreach (var text in Text)
                list.AddText(ImGuiFont, 16, new(pos.X + 5, size.Y + 5), 0xff00ffff, text);
        }
    }

    public static void RenderText(string[] text, int x, int y, int width, Color c, bool debug)
    {
        var fontsize = debug ? 15 : 30;
        var wheight = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(x, (int)(wheight - y - fontsize), width, fontsize * text.Length, new(0, 0, 0, 120));
        foreach (var item in text)
        {
            Raylib.DrawTextEx(GuiFont, item, new(x + 5, (int)(wheight - y - fontsize)), fontsize, 1f, c);
            y += fontsize;
        }
    }
}
