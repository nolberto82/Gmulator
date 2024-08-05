using ImGuiNET;
using Raylib_cs;
using System.Numerics;

namespace Gmulator;
public static class GuiUtils
{
    public static void DrawScreen(RenderTexture2D Screen, float menuheight, bool isdeck, bool debug)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var texwidth = Screen.Texture.Width;
        var texheight = Screen.Texture.Height;
        var scale = Math.Min((float)width / texwidth, (float)height / texheight);
        var posx = (int)((width - texwidth * scale) / 2);
        var posy = (int)(((height - texheight * scale) / 2) + menuheight + 3);

        Raylib.SetTextureFilter(Screen.Texture, TextureFilter.Point);
        Raylib.DrawTexturePro(
            Screen.Texture,
            new Rectangle(0, 0, texwidth, texheight),
            new Rectangle((width - texwidth * scale) / 2,
           ((height - (texheight) * scale) / 2) + menuheight,
            texwidth * scale,
            texheight * scale - menuheight),
            Vector2.Zero, 0, Color.White);

        var fontsize = debug ? 15 : 30;
        Raylib.DrawFPS(width - 100, (int)(5 + menuheight));
        Notifications.Render((int)((width - texwidth * scale) / 2), (int)menuheight, width, height);
    }

    public static Vector2 DrawGrid(Texture2D texture, Vector2 pos, Vector2 region, Vector2 mp, Vector2 gridsize, ImDrawListPtr list)
    {
        Vector2 size = new(region.X, region.Y);
        float scaleX = size.X / texture.Width;
        float scaleY = size.Y / texture.Height;
        float sepX = scaleX * 8;
        float sepY = scaleY * 8;

        DrawLines(list, pos, gridsize, new(sepX, sepY), gridsize, 0xffc0c0c0);

        var tx = (int)(mp.X / sepX);
        var ty = (int)(mp.Y / sepY);
        if (mp.X > 0 && mp.Y > 0 && tx < gridsize.X && ty < gridsize.Y && ImGui.IsWindowHovered())
        {
            float left = pos.X + (tx * sepX);
            float top = pos.Y + (ty * sepY);
            float right = pos.X + (tx + 1) * sepX;
            float bottom = pos.Y + (ty + 1) * sepY;
            list.AddRect(new(left, top), new(right, bottom), 0xffff0000, 2, 0, 2);
            return new(tx, ty);
        }
        return Vector2.Zero;
    }

    public static void DrawLines(ImDrawListPtr list, Vector2 pos, Vector2 size, Vector2 sep, Vector2 max, uint color)
    {
        float x = pos.X;
        float y = pos.Y;
        for (int i = 0; i < max.X; i++)
        {
            list.AddLine(new(x, pos.Y), new(x, y + size.Y * sep.Y), color);
            x += sep.X;
        }
        for (int i = 0; i < max.Y; i++)
        {
            list.AddLine(new(pos.X, y), new(pos.X + size.X * sep.X, y), color);
            y += sep.Y;
        }
    }

    public static void OpenCopyContext(string name, ref string text)
    {
        if (ImGui.BeginPopupContextItem($"##{name}", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverExistingPopup))
        {
            if (ImGui.MenuItem("Copy", true))
                Raylib.SetClipboardText(text.ToString());
            ImGui.EndPopup();
        }
        if (ImGui.BeginPopupContextItem($"##{name}", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverExistingPopup))
        {
            if (ImGui.MenuItem("Paste", true))
                text = Raylib.GetClipboardText_();
            ImGui.EndPopup();
        }
    }

    public static void Checkbox(string name, bool chk) => ImGui.Checkbox(name, ref chk);
    public static void Checkbox(string name, bool chk, ref Breakpoint bp, int type)
    {
        if (ImGui.Checkbox(name, ref chk))
            bp.Type ^= type;
    }

    public static void TableRow(ref bool chk, string chkname, string name)
    {
        ImGui.TableNextColumn(); ImGui.Checkbox(chkname, ref chk);
        ImGui.TableNextColumn(); ImGui.Text(name);
    }

    public static void TableRow(string name, string v)
    {
        ImGui.TableNextColumn(); ImGui.Text(name);
        ImGui.TableNextColumn(); 
        if (v != null) 
            ImGui.Text(v); 
        else 
            ImGui.Text("");
        ImGui.TableNextRow();
    }

    public static void TableRow(string name, string chkname, ref bool v)
    {
        ImGui.TableNextColumn(); ImGui.Text(name);
        ImGui.TableNextColumn(); ImGui.Checkbox(chkname, ref v);
        ImGui.TableNextColumn();
        ImGui.TableNextRow();
    }

    public static void DrawRect(uint filled, uint unfilled)
    {
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddRectFilled(min, max, filled);
        ImGui.GetWindowDrawList().AddRect(min, max, unfilled);
    }

    public static bool Button(string name, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        return ImGui.Button(name);
    }

    public static string IniSettings { get; } =
@"
[Window][Debug##Default]
Pos=60,60
Size=400,400
Collapsed=0

[Window][Display]
Pos=0,21
Size=571,779
Collapsed=0
DockId=0x00000001,0

[Window][Debugger]
Pos=573,21
Size=244,261
Collapsed=0
DockId=0x00000007,0

[Window][Cpu Info]
Pos=819,21
Size=209,487
Collapsed=0
DockId=0x00000006,0

[Window][Breakpoints]
Pos=573,284
Size=244,224
Collapsed=0
DockId=0x00000008,0

[Window][Memory Viewer]
Pos=573,510
Size=455,290
Collapsed=0
DockId=0x00000003,0

[Window][Registers]
Pos=1030,21
Size=250,779
Collapsed=0
DockId=0x0000000A,0

[Window][Menu]
Pos=0,21
Size=1280,800
Collapsed=0

[Window][Menu/##buttons_D61958BF]
IsChild=1
Size=1263,41

[Window][bpcontext]
Pos=294,419
Size=198,68
Collapsed=0

[Window][Ppu Debug]
Pos=474,220
Size=656,517
Collapsed=0

[Window][DockSpaceViewport_11111111]
Pos=0,21
Size=1280,779
Collapsed=0

[Window][Cheat Codes]
Pos=435,74
Size=400,400
Collapsed=0

[Docking][Data]
DockSpace           ID=0x8B93E3BD Window=0xA787BDB4 Pos=0,21 Size=1280,779 Split=X Selected=0x96643A2F
  DockNode          ID=0x00000009 Parent=0x8B93E3BD SizeRef=1028,779 Split=X
    DockNode        ID=0x00000001 Parent=0x00000009 SizeRef=571,487 CentralNode=1 HiddenTabBar=1 Selected=0x96643A2F
    DockNode        ID=0x00000005 Parent=0x00000009 SizeRef=455,487 Split=Y Selected=0xE1830C86
      DockNode      ID=0x00000002 Parent=0x00000005 SizeRef=244,487 Split=X Selected=0xE1830C86
        DockNode    ID=0x00000004 Parent=0x00000002 SizeRef=244,487 Split=Y Selected=0xE1830C86
          DockNode  ID=0x00000007 Parent=0x00000004 SizeRef=244,261 Selected=0xE1830C86
          DockNode  ID=0x00000008 Parent=0x00000004 SizeRef=244,224 HiddenTabBar=1 Selected=0x8A8CACFC
        DockNode    ID=0x00000006 Parent=0x00000002 SizeRef=209,487 Selected=0xD08D0702
      DockNode      ID=0x00000003 Parent=0x00000005 SizeRef=244,290 Selected=0xC206E20F
  DockNode          ID=0x0000000A Parent=0x8B93E3BD SizeRef=250,779 Selected=0xEAEE9E08
";
}
