using ImGuiNET;
using Raylib_cs;
using System.Numerics;

namespace Gmulator;
public static class GuiUtils
{
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

    public static void HexInput(ref string v)
    {
        ImGui.PushItemWidth(-1);
        ImGui.InputText($"##bpinput", ref v, 4, HexInputFlags);
        ImGui.PopItemWidth();
        ImGui.EndChild();
    }

    public static bool OpenCopyContext(string name, ref string text)
    {
        var v = false;
        if (ImGui.BeginPopupContextItem($"##{name}", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverExistingPopup))
        {
            if (ImGui.MenuItem("Copy", true))
                Raylib.SetClipboardText(text.ToString());
            ImGui.EndPopup();
        }
        if (ImGui.BeginPopupContextItem($"##{name}", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverExistingPopup))
        {
            if (ImGui.MenuItem("Paste", true))
            {
                text = Raylib.GetClipboardText_();
                v = true;
            }
            ImGui.EndPopup();
        }
        return v;
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
    }

    public static bool TableRowSelect(string name, string v, bool selected)
    {
        var b = false;
        ImGui.TableNextColumn();
        ImGui.Selectable(name, selected, ImGuiSelectableFlags.SpanAllColumns);
        if (ImGui.IsItemHovered())
            b = true;
        ImGui.TableNextColumn();
        if (v != null)
            ImGui.Text(v);
        else
            ImGui.Text("");
        return b;
    }

    public static void TableRowCol3(string addr, string name, string v)
    {
        ImGui.TableNextColumn(); ImGui.Text(addr);
        ImGui.TableNextColumn(); ImGui.Text(name);
        ImGui.TableNextColumn(); ImGui.Text(v != null ? v : "");
    }

    public static void TableRowCol3(string addr, string name, bool v)
    {
        ImGui.TableNextColumn(); ImGui.Text(addr);
        ImGui.TableNextColumn(); ImGui.Text(name);
        ImGui.TableNextColumn(); ImGui.Checkbox("", ref v);
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
}
