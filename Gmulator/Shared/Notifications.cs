using ImGuiNET;

namespace Gmulator.Shared;

internal static class Notifications
{
    private static readonly List<TextEntry> Text = [];
    private static ImFontPtr ImGuiFont;
    private static Font GuiFont;
    private const float DurationMax = 100;
    private const float Speed = 25f;

    public class TextEntry
    {
        public string Text { get; set; }
        public float Duration { get; set; }

        public TextEntry(string text)
        {
            Text = text;
            Duration = DurationMax; ;
        }
    }

    public static void SetFont(ImFontPtr font, Font raylibfont)
    {
        ImGuiFont = font;
        GuiFont = raylibfont;
    }

    public static void Init(string text)
    {
        string[] res = text.Split([": ", "\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        if (Text.Count == 0)
        {
            Text.Add(new TextEntry($"{string.Join("\n", res)}"));
        }
        else
        {
            var element = Text.Any(e => e.Text == text);
            if (!element)
            {
                Text.Add(new TextEntry($"{string.Join("\n", res)}"));
            }
            else
            {
                var existing = Text.FirstOrDefault(e => e.Text == text);
                if (existing != null)
                    existing.Duration = DurationMax;
            }
        }
    }

    public static void Render(int x, int y, int width, bool debug)
    {
        if (!debug)
            RenderText(Text, x, y, width, Color.Yellow, debug);
    }

    public static void RenderDebug()
    {
        for (int i = 0; i < Text.Count; i++)
        {
            var list = ImGui.GetForegroundDrawList();
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            var fontsize = 14;

            Text[i].Duration -= Speed * Raylib.GetFrameTime();
            if (Text[i].Duration < 0)
                Text[i].Duration = 0;

            if (Text[i].Duration != 0)
            {
                string text = Text[i].Text;
                list.AddRectFilled(new(pos.X, size.Y), new(pos.X + size.X, size.Y + fontsize), 0xc0000000);
                list.AddText(ImGuiFont, fontsize, new(pos.X + 10, size.Y + fontsize - 3), 0xff00ffff, text);
                break;
            }
        }
    }

    public static void RenderText(List<TextEntry> Text, int x, int y, int width, Color c, bool debug)
    {
        var fontsize = debug ? 15 : GuiFont.BaseSize;
        var wheight = Raylib.GetScreenHeight();
        for (int i = 0; i < Text.Count; i++)
        {
            Text[i].Duration -= Speed * Raylib.GetFrameTime();
            if (Text[i].Duration < 0)
                Text[i].Duration = 0;

            if (Text[i].Duration != 0)
            {
                string text = Text[i].Text;
                Raylib.DrawRectangle(x, wheight - fontsize, width, fontsize, new(0, 0, 0, 192));
                Raylib.DrawTextEx(GuiFont, text, new(x + 5, wheight - fontsize), fontsize, 1f, c);
                break;
            }
        }
    }
}
