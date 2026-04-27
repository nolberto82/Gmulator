
using ImGuiNET;
using NLua;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Gmulator.Shared.LuaScript;

internal partial class GuiLua
{
    private RenderTexture2D _screen;
    private ImFontPtr[] _consolas;
    private Font _guiFont;
    private float _menuHeight;
    private bool _debug;

    public GuiLua(NLua.Lua state, LuaManager luaManager)
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

        var fontsize = 10;// args.Length > 3 && args[3] != null ? Convert.ToInt32(args[3]) : 32;
        bool iscaled = false;
        if (args.Length == 5)
        {
            if (args[4] == null)
                iscaled = Convert.ToBoolean(args[3]);
            else
                iscaled = Convert.ToBoolean(args[4]);
        }

        uint textcolor = 0xffffffff;
        if (args.Length > 4 && args[3] != null)
            textcolor = Convert.ToUInt32(args[3]);

        var text = $"{args[2]}";
        //(var x, var y, _) = GetScaledPosition(args[0], args[1], _screen, true);
        var x = Convert.ToInt32(args[0]);
        var y = Convert.ToInt32(args[1]);
        var scale = GetScale();
        var textureHeight = _screen.Texture.Height - 1;
        var textureSize = Raylib.MeasureTextEx(_guiFont, text, fontsize, 1);
        var textRect = new Rectangle(x, y, textureSize.X, textureSize.Y);
        //Raylib.BeginTextureMode(_screen);
        //if (args.Length == 5)
        //    Raylib.DrawRectangleRec(textRect, GetColor(args[4] == null ? 0x00000000 : Convert.ToUInt32(args[4])));

        Raylib.DrawTextEx(_guiFont, text, new(x + textRect.Width / 2 - textureSize.X / 2, y), textureSize.Y, 0f, GetColor(textcolor));

    }
    public void DrawRectangle(params object[] args)
    {
        if (args.Length < 5)
            return;

        //if (Convert.ToInt32(args[0]) < 0 || Convert.ToInt32(args[0]) > Screen.Width)
        //    return;

        var scale = GetScale();
        var x = Convert.ToInt32(args[0]);
        var y = Convert.ToInt32(args[1]);
        var w = Convert.ToInt32(args[2]);
        var h = Convert.ToInt32(args[3]);
        var bgcolor = args[4] == null ? 0xffffffff : Convert.ToUInt32(args[4]);
        var textureHeight = _screen.Texture.Height;

        Raylib.DrawRectangle(x, y, w, h, GetColor(bgcolor));
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
        var textureHeight = _screen.Texture.Height;
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
        var x = (int)((Convert.ToInt64(vx) * (isscaled ? scale : 1) + left));
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
}
