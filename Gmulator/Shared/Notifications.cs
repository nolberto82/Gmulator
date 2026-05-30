using ImGuiNET;

namespace Gmulator.Shared;

internal static class Notifications
{
    private static List<string> Text = [];
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
        if (Text.Count >= 2)
            Text.Clear();

        string[] res = text.Split([": ", "\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Text.Add($"{string.Join("\n", res)}\n");
        Frames = 125;
    }

    public static void Render(int x, int y, int width, bool debug)
    {
        if (Frames-- > 0)
        {
            if (!debug)
                RenderText(Text, x, y, width, Color.Yellow, debug);
        }
        else if (Text.Count > 0)
            Text = [];
    }

    public static void RenderDebug()
    {
        if (Frames-- > 0)
        {
            var list = ImGui.GetForegroundDrawList();
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            var fontsize = 14;
            for (int i = 0; i < Text.Count; i++)
            {
                string text = Text[i];
                list.AddRectFilled(new(pos.X, size.Y), new(pos.X + size.X, size.Y + fontsize), 0xc0000000);
                list.AddText(ImGuiFont, fontsize, new(pos.X + 10, size.Y + fontsize - 3), 0xff00ffff, text);
            }
        }
    }

    public static void RenderText(List<string> Text, int x, int y, int width, Color c, bool debug)
    {
        var fontsize = debug ? 15 : 30;
        var wheight = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(x, wheight - y - fontsize * 1, width, fontsize * 2, new(0, 0, 0, 192));
        for (int i = 0; i < Text.Count; i++)
        {
            string text = Text[i];
            Raylib.DrawTextEx(GuiFont, text, new(x + 5, wheight - y - i * fontsize), fontsize, 1f, c);
        }
    }
}
