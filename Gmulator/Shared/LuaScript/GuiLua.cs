
using ImGuiNET;
using NLua;
using System.Numerics;

namespace Gmulator.Shared.LuaScript;

internal partial class GuiLua
{
    private RenderTexture2D _screen;
    private readonly ImFontPtr[] _consolas;
    private Font _guiFont;
    private readonly float _menuHeight;

    public GuiLua(Lua state, LuaManager luaManager)
    {
        _screen = luaManager.Screen;
        _consolas = luaManager.Consolas;
        _guiFont = luaManager.GuiFont;
        _menuHeight = luaManager.MenuHeight;

        state.NewTable("gui");
        state.RegisterFunction("gui.drawtext", this, typeof(GuiLua).GetMethod("DrawText"));
        state.RegisterFunction("gui.drawtextcircled", this, typeof(GuiLua).GetMethod("DrawTextCircled"));
        state.RegisterFunction("gui.drawrect", this, typeof(GuiLua).GetMethod("DrawRectangle"));
        state.RegisterFunction("gui.loadimage", this, typeof(GuiLua).GetMethod("LoadImage"));
        state.RegisterFunction("gui.drawimage", this, typeof(GuiLua).GetMethod("DrawImage"));
    }

    public void DrawText(params object[] args)
    {
        if (args.Length < 4)
            return;

        int fontsize = args.Length == 6 && args[5] != null ? Convert.ToInt32(args[5]) : 8;

        uint textcolor = 0xffffffff;
        if (args.Length > 4 && args[3] != null)
            textcolor = Convert.ToUInt32(args[3]);

        var text = $"{args[2]}";
        var x = Convert.ToInt32(args[0]);
        var y = Convert.ToInt32(args[1]);
        var textureSize = Raylib.MeasureTextEx(_guiFont, text, fontsize, 1);
        var textRect = new Rectangle(x, y, 8 * (text.Length - 1), 8);
        //if (args.Length >= 5)
        //    Raylib.DrawRectangleRec(textRect, GetColor(args[4] == null ? 0x00000000 : Convert.ToUInt32(args[4])));

        //Raylib.DrawTextEx(_guiFont, text, new(x + textRect.Width / 2 - textureSize.X / 2, y), textureSize.Y, 0f, GetColor(textcolor));
        DrawPixeledText(x, y, text, textcolor);
    }

    public static void DrawRectangle(params object[] args)
    {
        if (args.Length < 5)
            return;

        var x = Convert.ToInt32(args[0]);
        var y = Convert.ToInt32(args[1]);
        var w = Convert.ToInt32(args[2]);
        var h = Convert.ToInt32(args[3]);
        var bgcolor = args[4] == null ? 0xffffffff : Convert.ToUInt32(args[4]);

        Raylib.DrawRectangle(x, y, w, h, GetColor(bgcolor));
    }

    public void DrawPixeledText(int x, int y, string text, uint color)
    {
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i] - 32;
            var width = _font[c * 8];
            for (int j = 0; j < 7; j++)
            {
                var b = _font[c * 8 + j + 1];
                for (int k = 0; k < width; k++)
                {
                    if ((b >> (7 - k) & 1) != 0)
                        Raylib.DrawPixel(x + k, y + j, GetColor(color));
                    else
                        Raylib.DrawPixel(x + k, y + j, GetColor(0));
                }
            }
            x += width;
        }
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
        (var x, var y, _) = GetScaledPosition(args[0], args[1], _screen);
        var tz = Raylib.MeasureTextEx(_guiFont, text, fontsize, 1);
        Raylib.DrawCircle((int)(x + tz.X / 2), (int)(y + tz.Y / 2), fontsize / 2, GetColor(bgcolor));
        Raylib.DrawTextEx(_guiFont, text, new(x, y), fontsize, 0f, GetColor(textcolor));
    }

    public void DrawImage(params object[] args)
    {
        if (args.Length < 3)
            return;

        if (args[0] is not Texture2D)
            return;

        var t = (Texture2D)args[0];
        (var x, var y, var scale) = GetScaledPosition(args[1], args[2], _screen);

        //Raylib.DrawTexture(t, x, y, Color.White);
        Raylib.DrawTexturePro(t, new Rectangle(0, 0, t.Width, t.Height),
        new Rectangle(x, y, t.Width * scale,
        t.Height * scale - _menuHeight),
        Vector2.Zero, 0, Color.White);
    }

    public static Texture2D LoadImage(params object[] args)
    {
        if (args.Length != 1 && !File.Exists($"{args[0]}"))
            return new();

        var path = $"{AppDomain.CurrentDomain.BaseDirectory}{CheatDirectory}";
        return Raylib.LoadTexture($"{path}\\{args[0]}");
    }

    public static Color GetColor(uint color)
    {
        var c = color != 0xffffffff ? color ^ 0xff000000 : color;
        var r = (byte)(c >> 16);
        var g = (byte)(c >> 8);
        var b = (byte)c;
        var a = (byte)(c >> 24);
        return new(r, g, b, a);
    }

    public (int, int, float) GetScaledPosition(object vx, object vy, RenderTexture2D target, bool isscaled = true)
    {
        var width = Raylib.GetRenderWidth();
        var height = Raylib.GetRenderHeight();
        var textureWidth = target.Texture.Width;
        var textureHeight = target.Texture.Height;
        var scale = Math.Min((float)width / textureWidth, (float)height / textureHeight);
        var left = isscaled ? (width - textureWidth * scale) / 2 : 0;
        var top = isscaled ? ((height - textureHeight * scale) / 2) + _menuHeight : _menuHeight;
        var x = (int)(Convert.ToInt64(vx) * (isscaled ? scale : 1) + left);
        var y = (int)(Convert.ToInt64(vy) * (isscaled ? scale : 1) + top);
        return (x, y, scale);
    }

    public float GetScale()
    {
        var width = Raylib.GetRenderWidth();
        var height = Raylib.GetRenderHeight();
        var textureWidth = _screen.Texture.Width;
        var textureHeight = _screen.Texture.Height;
        var scale = Math.Min((float)width / textureWidth, (float)height / textureHeight);
        return scale;
    }

    private readonly byte[] _font = //from MesenCE
    [
        6,  0,  0,  0,  0,  0,  0,  0,	// 0x20 - Spacebar
		2,128,128,128,128,128,  0,128,
        5, 80, 80, 80,  0,  0,  0,  0,
        6, 80, 80,248, 80,248, 80, 80,
        6, 32,120,160,112, 40,240, 32,
        6, 64,168, 80, 32, 80,168, 16,
        6, 96,144,160, 64,168,144,104,
        3, 64, 64,  0,  0,  0,  0,  0,
        4, 32, 64, 64, 64, 64, 64, 32,
        4,128, 64, 64, 64, 64, 64,128,
        6,  0, 80, 32,248, 32, 80,  0,
        6,  0, 32, 32,248, 32, 32,  0,
        3,  0,  0,  0,  0,  0, 64,128,
        5,  0,  0,  0,240,  0,  0,  0,
        3,  0,  0,  0,  0,  0,  0, 64,
        5, 16, 16, 32, 32, 32, 64, 64,
        6,112,136,136,136,136,136,112,	// 0x30 - 0
		6, 32, 96, 32, 32, 32, 32, 32,
        6,112,136,  8, 48, 64,128,248,
        6,112,136,  8, 48,  8,136,112,
        6, 16, 48, 80,144,248, 16, 16,
        6,248,128,128,240,  8,  8,240,
        6, 48, 64,128,240,136,136,112,
        6,248,  8, 16, 16, 32, 32, 32,
        6,112,136,136,112,136,136,112,
        6,112,136,136,120,  8, 16, 96,
        3,  0,  0, 64,  0,  0, 64,  0,
        3,  0,  0, 64,  0,  0, 64,128,
        4,  0, 32, 64,128, 64, 32,  0,
        5,  0,  0,240,  0,240,  0,  0,
        4,  0,128, 64, 32, 64,128,  0,
        6,112,136,  8, 16, 32,  0, 32,	// 0x3F - ?
		6,112,136,136,184,176,128,112,	// 0x40 - @
		6,112,136,136,248,136,136,136,	// 0x41 - A
		6,240,136,136,240,136,136,240,
        6,112,136,128,128,128,136,112,
        6,224,144,136,136,136,144,224,
        6,248,128,128,240,128,128,248,
        6,248,128,128,240,128,128,128,
        6,112,136,128,184,136,136,120,
        6,136,136,136,248,136,136,136,
        4,224, 64, 64, 64, 64, 64,224,
        6,  8,  8,  8,  8,  8,136,112,
        6,136,144,160,192,160,144,136,
        6,128,128,128,128,128,128,248,
        6,136,216,168,168,136,136,136,
        6,136,136,200,168,152,136,136,
        7, 48, 72,132,132,132, 72, 48,
        6,240,136,136,240,128,128,128,
        6,112,136,136,136,168,144,104,
        6,240,136,136,240,144,136,136,
        6,112,136,128,112,  8,136,112,
        6,248, 32, 32, 32, 32, 32, 32,
        6,136,136,136,136,136,136,112,
        6,136,136,136, 80, 80, 32, 32,
        6,136,136,136,136,168,168, 80,
        6,136,136, 80, 32, 80,136,136,
        6,136,136, 80, 32, 32, 32, 32,
        6,248,  8, 16, 32, 64,128,248,
        3,192,128,128,128,128,128,192,
        5, 64, 64, 32, 32, 32, 16, 16,
        3,192, 64, 64, 64, 64, 64,192,
        4, 64,160,  0,  0,  0,  0,  0,
        6,  0,  0,  0,  0,  0,  0,248,
        3,128, 64,  0,  0,  0,  0,  0,
        5,  0,  0, 96, 16,112,144,112,	// 0x61 - a
		5,128,128,224,144,144,144,224,
        5,  0,  0,112,128,128,128,112,
        5, 16, 16,112,144,144,144,112,
        5,  0,  0, 96,144,240,128,112,
        5, 48, 64,224, 64, 64, 64, 64,
        5,  0,112,144,144,112, 16,224,
        5,128,128,224,144,144,144,144,
        2,128,  0,128,128,128,128,128,
        4, 32,  0, 32, 32, 32, 32,192,
        5,128,128,144,160,192,160,144,
        2,128,128,128,128,128,128,128,
        6,  0,  0,208,168,168,168,168,
        5,  0,  0,224,144,144,144,144,
        5,  0,  0, 96,144,144,144, 96,
        5,  0,224,144,144,224,128,128,
        5,  0,112,144,144,112, 16, 16,
        5,  0,  0,176,192,128,128,128,
        5,  0,  0,112,128, 96, 16,224,
        4, 64, 64,224, 64, 64, 64, 32,
        5,  0,  0,144,144,144,144,112,
        5,  0,  0,144,144,144,160,192,
        6,  0,  0,136,136,168,168, 80,
        5,  0,  0,144,144, 96,144,144,
        5,  0,144,144,144,112, 16, 96,
        5,  0,  0,240, 32, 64,128,240,
        4, 32, 64, 64,128, 64, 64, 32,
        3, 64, 64, 64, 64, 64, 64, 64,
        4,128, 64, 64, 32, 64, 64,128,
        6,  0,104,176,  0,  0,  0,  0,
        5,  0,  0,113, 80,113,  0,  0,
    ];
}
