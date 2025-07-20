using ImGuiNET;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace Gmulator.Shared;
public unsafe class MemoryEditor
{
    private enum DataFormat
    {
        DataFormat_Bin = 0,
        DataFormat_Dec = 1,
        DataFormat_Hex = 2,
        DataFormat_COUNT
    };

    // Settings
    private bool Open;                                       // = true   // set to false when DrawWindow() was closed. ignore if not using DrawWindow().
    private readonly bool ReadOnly;                                   // = false  // disable any editing.
    private int Cols;                                       // = 16     // number of columns to display.
    private readonly bool OptShowOptions;                             // = true   // display options button/context menu. when disabled, options will be locked unless you provide your own UI for them.
    private bool OptShowDataPreview;                         // = false  // display a footer previewing the decimal/binary/hex/float representation of the currently selected bytes.
    private bool OptShowHexII;                               // = false  // display values in HexII representation instead of regular hexadecimal: hide null/zero bytes, ascii values as ".X".
    private bool OptShowAscii;                               // = true   // display ASCII representation on the right side.
    private bool OptGreyOutZeroes;                           // = true   // display null/zero bytes using the TextDisabled color.
    private bool OptUpperCaseHex;                            // = true   // display hexadecimal values as "FF" instead of "ff".
    private readonly int OptMidColsCount;                            // = 8      // set to 0 to disable extra spacing between every mid-cols.
    public int OptAddrDigitsCount;                         // = 0      // number of addr digits to display (default calculated based on maximum displayed addr).
    private readonly float OptFooterExtraHeight;                       // = 0      // space to reserve at the bottom of the widget to add custom widgets
    private readonly uint HighlightColor;                             //          // background color of highlighted bytes.
    public delegate byte ReadDel(int off);    // = 0      // optional handler to read bytes.
    public ReadDel ReadFn;
    public delegate void WriteDel(int off, byte d); // = 0      // optional handler to write bytes.
    public WriteDel WriteFn;
    public delegate bool HighlightDel(byte[] data, int off);//= 0      // optional handler to return Highlight property (to support non-contiguous highlighting).
    public HighlightDel HighlightFn;

    // [public State]
    private bool ContentsWidthChanged;
    private int DataPreviewAddr;
    private int DataEditingAddr;
    private bool DataEditingTakeFocus;
    private readonly byte[] DataInputBuf = new byte[32];
    private readonly byte[] AddrInputBuf = new byte[32];
    private int GotoAddr;
    private int HighlightMin, HighlightMax;
    private readonly ImGuiDataType PreviewDataType;

    public int SelectedRam { get; set; }
    private readonly int bpaddr;

    private record Sizes
    {
        public int AddrDigitsCount;
        public float LineHeight;
        public float GlyphWidth;
        public float HexCellWidth;
        public float SpacingBetweenMidCols;
        public float PosHexStart;
        public float PosHexEnd;
        public float PosAsciiStart;
        public float PosAsciiEnd;
        public float WindowWidth;
    };

    public MemoryEditor(Action<int, int, int, bool, int> addbp, Action<int, byte> addcheat)
    {
        // Settings
        Open = true;
        ReadOnly = false;
        Cols = 16;
        OptShowOptions = true;
        OptShowDataPreview = false;
        OptShowHexII = false;
        OptShowAscii = false;
        OptGreyOutZeroes = true;
        OptUpperCaseHex = true;
        OptMidColsCount = 8;
        OptAddrDigitsCount = 0;
        OptFooterExtraHeight = 0.0f;
        HighlightColor = ImGui.GetColorU32(new Vector4(1, 1, 1, 50 / 255));

        // State/publics
        ContentsWidthChanged = false;
        DataPreviewAddr = DataEditingAddr = -1;
        DataEditingTakeFocus = false;
        GotoAddr = -1;
        HighlightMin = HighlightMax = -1;
        PreviewDataType = ImGuiDataType.S32;
        AddBreakpoint = addbp;
        AddCheat = addcheat;
    }

    // Standalone Memory Editor window
    public void DrawWindow(string title, int ramlength, int base_display_addr = 0x0000)
    {
        CalcSizes(out Sizes s, ramlength, base_display_addr);
        ImGui.SetNextWindowSize(new(s.WindowWidth, s.WindowWidth * 0.60f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new(0.0f, 0.0f), new(s.WindowWidth, float.MaxValue));

        Open = true;
        if (ImGui.Begin(title, ref Open, ImGuiWindowFlags.NoScrollbar))
        {
            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                ImGui.OpenPopup("bpcontext");
            //DrawContents(ram.ToArray(), ram.Length, base_display_addr);
            if (ContentsWidthChanged)
                //CalcSizes(out s, ram.Length, base_display_addr);
                ImGui.SetWindowSize(new(s.WindowWidth, ImGui.GetWindowSize().Y));
        }
        ImGui.End();
    }

    public void DrawContents(byte[] mem_data_void, int mem_size, int base_display_addr = 0x0000)
    {
        if (Cols < 1)
            Cols = 1;

        if (ImGui.GetIO().MouseWheel != 0)
            DataEditingTakeFocus = false;

        byte[] mem_data = mem_data_void;
        CalcSizes(out Sizes s, mem_size, base_display_addr);
        ImGuiStylePtr style = ImGui.GetStyle();

        // We begin into our scrolling region with the 'ImGuiWindowFlags_NoMove' in order to prevent click from moving the window.
        // This is used as a facility since our main click detection code doesn't assign an ActiveId so the click would normally be caught as a window-move.
        float height_separator = style.ItemSpacing.Y;
        float footer_height = OptFooterExtraHeight;
        if (OptShowOptions)
            footer_height += height_separator + ImGui.GetFrameHeightWithSpacing() * 1;
        if (OptShowDataPreview)
            footer_height += height_separator + ImGui.GetFrameHeightWithSpacing() * 1 + ImGui.GetTextLineHeightWithSpacing() * 3;
        ImGui.BeginChild("##scrolling", new(0, -footer_height), ImGuiChildFlags.None);
        ImDrawListPtr draw_list = ImGui.GetWindowDrawList();

        //ScrollOffsets[SelectedRam] = ImGui.GetScrollY();

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));

        // We are not really using the clipper API correctly here, because we rely on visible_start_addr/visible_end_addr for our scrolling function.
        int line_total_count = (mem_size + Cols - 1) / Cols;

        ImGuiListClipper _clipper = new();
        ImGuiListClipperPtr clipper = new(&_clipper);
        clipper.Begin(line_total_count, s.LineHeight);

        int visible_start_addr = clipper.DisplayStart * Cols;
        int visible_end_addr = clipper.DisplayEnd * Cols;

        bool data_next = false;

        if (ReadOnly || DataEditingAddr >= mem_size)
            DataEditingAddr = -1;
        if (DataPreviewAddr >= mem_size)
            DataPreviewAddr = -1;

        int preview_data_type_size = OptShowDataPreview ? DataTypeGetSize(PreviewDataType) : 0;

        int data_editing_addr_backup = DataEditingAddr;
        int data_editing_addr_next = -1;
        if (DataEditingAddr != -1)
        {
            // Move cursor but only apply on next frame so scrolling with be synchronized (because currently we can't change the scrolling while the window is being rendered)
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow) && DataEditingAddr >= Cols)
data_editing_addr_next = DataEditingAddr - Cols;             else if (ImGui.IsKeyPressed(ImGuiKey.DownArrow) && DataEditingAddr < mem_size - Cols)
            { data_editing_addr_next = DataEditingAddr + Cols; }
            else if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow) && DataEditingAddr > 0)
            { data_editing_addr_next = DataEditingAddr - 1; }
            else if (ImGui.IsKeyPressed(ImGuiKey.RightArrow) && DataEditingAddr < mem_size - 1)
            { data_editing_addr_next = DataEditingAddr + 1; }
        }

        if (data_editing_addr_next != -1 && data_editing_addr_next / Cols != data_editing_addr_backup / Cols)
        {
            // Track cursor movements
            int scroll_offset = data_editing_addr_next / Cols - data_editing_addr_backup / Cols;
            bool scroll_desired = scroll_offset < 0 && data_editing_addr_next < visible_start_addr + Cols * 2 || scroll_offset > 0 && data_editing_addr_next > visible_end_addr - Cols * 2;
            if (scroll_desired)
                ImGui.SetScrollY(ImGui.GetScrollY() + scroll_offset * s.LineHeight);
        }

        // Draw vertical separator
        Vector2 window_pos = ImGui.GetWindowPos();
        if (OptShowAscii)
            draw_list.AddLine(new(window_pos.X + s.PosAsciiStart - s.GlyphWidth, window_pos.Y), new(window_pos.X + s.PosAsciiStart - s.GlyphWidth, window_pos.Y + 9999), ImGui.GetColorU32(ImGuiCol.Border));

        uint color_text = ImGui.GetColorU32(ImGuiCol.Text);
        uint color_disabled = OptGreyOutZeroes ? ImGui.GetColorU32(ImGuiCol.TextDisabled) : color_text;

        while (clipper.Step())
        {
            for (int line_i = clipper.DisplayStart; line_i < clipper.DisplayEnd; line_i++) // display only visible lines
            {
                int addr = line_i * Cols;
                ImGui.Text($"{base_display_addr + addr:X4}");

                // Draw Hexadecimal
                for (int n = 0; n < Cols && addr < mem_size; n++, addr++)
                {
                    float byte_pos_x = s.PosHexStart + s.HexCellWidth * n;
                    if (OptMidColsCount > 0)
                        byte_pos_x += n / OptMidColsCount * s.SpacingBetweenMidCols;
                    ImGui.SameLine(byte_pos_x);

                    // Draw highlight
                    bool is_highlight_from_user_range = addr >= HighlightMin && addr < HighlightMax;
                    bool is_highlight_from_user_func = HighlightFn != null && HighlightFn(mem_data, addr);
                    bool is_highlight_from_preview = addr >= DataPreviewAddr && addr < DataPreviewAddr + preview_data_type_size;
                    if (is_highlight_from_user_range || is_highlight_from_user_func || is_highlight_from_preview)
                    {
                        Vector2 pos = ImGui.GetCursorScreenPos();
                        float highlight_width = s.GlyphWidth * 2;
                        bool is_next_byte_highlighted = addr + 1 < mem_size && (HighlightMax != -1 && addr + 1 < HighlightMax || HighlightFn != null && HighlightFn(mem_data, addr + 1));
                        if (is_next_byte_highlighted || n + 1 == Cols)
                        {
                            highlight_width = s.HexCellWidth;
                            if (OptMidColsCount > 0 && n > 0 && n + 1 < Cols && (n + 1) % OptMidColsCount == 0)
                                highlight_width += s.SpacingBetweenMidCols;
                        }
                        draw_list.AddRectFilled(pos, new(pos.X + highlight_width, pos.Y + s.LineHeight), HighlightColor);
                    }

                    if (DataEditingAddr == addr)
                    {
                        // Display text input on current byte
                        bool data_write = false;
                        ImGui.PushID(addr);
                        if (DataEditingTakeFocus)
                        {
                            ImGui.SetKeyboardFocusHere(0);
                            ImGui.SetNextFrameWantCaptureMouse(true);
                            ReplaceChars(DataInputBuf, FixedHex(ReadFn != null ? ReadFn(addr) : mem_data[addr], 2));
                            ReplaceChars(AddrInputBuf, FixedHex(base_display_addr + addr, OptAddrDigitsCount));
                        }
                        ImGui.PushItemWidth(s.GlyphWidth * 2);

                        UserData user_data = new();
                        user_data.CursorPos = -1;

                        // TODO: check it (YTom)
                        var buf = FixedHex(ReadFn != null ? ReadFn(addr) : mem_data[addr], 2).ToCharArray();
                        user_data.CurrentBufOverwrite[0] = buf[0];
                        user_data.CurrentBufOverwrite[1] = buf[1];

                        ImGuiInputTextFlags flags = ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.NoHorizontalScroll | ImGuiInputTextFlags.AlwaysOverwrite | ImGuiInputTextFlags.CallbackAlways;
                        if (ImGui.InputText("##data", DataInputBuf, (uint)DataInputBuf.Length, flags, Callback, (nint)(&user_data)))
                            data_write = data_next = true;
                        else if (!DataEditingTakeFocus && !ImGui.IsItemActive())
                            DataEditingAddr = data_editing_addr_next = -1;
                        if (DataEditingTakeFocus && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            DataEditingAddr = -1;
                            DataEditingTakeFocus = false;
                        }
                        ImGui.PopItemWidth();
                        if (user_data.CursorPos >= 2)
                            data_write = data_next = true;
                        if (data_editing_addr_next != -1)
                            data_write = data_next = false;

                        if (data_write && TryHexParse(DataInputBuf, out int data_input_value))
                        {
                            if (WriteFn != null)
                                WriteFn(addr, (byte)data_input_value);
                            else
                                mem_data[addr] = (byte)data_input_value;
                        }
                        ImGui.PopID();
                    }
                    else
                    {
                        byte b = ReadFn != null ? ReadFn(addr) : mem_data[addr];

                        if (OptShowHexII)
                        {
                            if (b >= 32 && b < 128)
                                ImGui.Text($"{b}");
                            else if (b == 0xFF && OptGreyOutZeroes)
                                ImGui.TextDisabled("## ");
                            else if (b == 0x00)
                                ImGui.Text("   ");
                            else
                                ImGui.Text($"{b}");
                        }
                        else
                        {
                            if (b == 0 && OptGreyOutZeroes)
                                ImGui.TextDisabled("00 ");
                            else
                                ImGui.Text($"{b:X2}");
                        }
                        if (!ReadOnly && ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            DataEditingTakeFocus = true;
                            data_editing_addr_next = addr;
                        }

                        //if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                        //{
                        //    DataEditingAddr = -1;
                        //    DataEditingTakeFocus = false;
                        //    if (ImGui.IsItemHovered())
                        //    {
                        //        bpaddr = addr;
                        //        ImGui.OpenPopup("bpcontext");
                        //    }
                        //}

                        ImGui.SetNextWindowSize(new(0, 68));
                        if (ImGui.BeginPopupModal("bpcontext"))
                        {
                            var a = bpaddr;
                            if (bpaddr == addr)
                            {
                                ImGui.SetNextItemWidth(s.GlyphWidth * 7 + style.FramePadding.X * 2.0f);
                                if (ImGui.Button($"Breakpoint on Write - {a:X4}"))
                                    //InsertRemove(a, BPType.Write, true);
                                    ImGui.CloseCurrentPopup();

                                if (ImGui.Button($"Breakpoint on Read  - {a:X4}"))
                                {
                                    if (DataPreviewAddr > -1)
                                        //if (MemRegions[SelectedRam].Name == "Wram")
                                        //    addr += 0x8000;
                                        //InsertRemove(a, BPType.Read, true);
                                        ImGui.CloseCurrentPopup();
                                }

                                if (ImGui.Button("Close"))
                                    ImGui.CloseCurrentPopup();
                            }
                            ImGui.EndPopup();
                        }
                    }
                }

                if (OptShowAscii)
                {
                    // Draw ASCII values
                    ImGui.SameLine(s.PosAsciiStart);
                    Vector2 pos = ImGui.GetCursorScreenPos();
                    addr = line_i * Cols;
                    ImGui.PushID(line_i);
                    if (ImGui.InvisibleButton("ascii", new(s.PosAsciiEnd - s.PosAsciiStart, s.LineHeight)))
                    {
                        DataEditingAddr = DataPreviewAddr = addr + (int)((ImGui.GetIO().MousePos.X - pos.X) / s.GlyphWidth);
                        DataEditingTakeFocus = true;
                    }
                    ImGui.PopID();
                    for (int n = 0; n < Cols && addr < mem_size; n++, addr++)
                    {
                        if (addr == DataEditingAddr)
                        {
                            draw_list.AddRectFilled(pos, new(pos.X + s.GlyphWidth, pos.Y + s.LineHeight), ImGui.GetColorU32(ImGuiCol.FrameBg));
                            draw_list.AddRectFilled(pos, new(pos.X + s.GlyphWidth, pos.Y + s.LineHeight), ImGui.GetColorU32(ImGuiCol.TextSelectedBg));
                        }
                        byte c = ReadFn != null ? ReadFn(addr) : mem_data[addr];
                        char display_c = c < 32 || c >= 128 ? '.' : (char)c;
                        draw_list.AddText(pos, display_c == c ? color_text : color_disabled, $"{display_c}");
                        pos.X += s.GlyphWidth;
                    }
                }
            }
        }

        clipper.End();
        ImGui.PopStyleVar(2);
        ImGui.EndChild();

        // Notify the main window of our ideal child content size (FIXME: we are missing an API to get the contents size from the child)
        ImGui.SetCursorPosX(s.WindowWidth);
        ImGui.Dummy(new(0.0f, 0.0f));

        if (data_next && DataEditingAddr + 1 < mem_size)
        {
            DataEditingAddr = DataPreviewAddr = DataEditingAddr + 1;
            DataEditingTakeFocus = true;
        }
        else if (data_editing_addr_next != -1)
        {
            DataEditingAddr = DataPreviewAddr = data_editing_addr_next;
            DataEditingTakeFocus = true;
        }

        bool lock_show_data_preview = OptShowDataPreview;
        if (OptShowOptions)
        {
            ImGui.Separator();
            DrawOptionsLine(s, mem_size, base_display_addr);
        }

        if (lock_show_data_preview)
            ImGui.Separator();
    }

    private void DrawOptionsLine(Sizes s, int mem_size, int base_display_addr)
    {
        ImGuiStylePtr style = ImGui.GetStyle();
        //const char* format_range = OptUpperCaseHex ? "Range %0*" _PRISizeT "X..%0*" _PRISizeT "X" : "Range %0*" _PRISizeT "x..%0*" _PRISizeT "x";

        // Options menu
        if (ImGui.Button("Options"))
            ImGui.OpenPopup("context");
        if (ImGui.BeginPopup("context"))
        {
            ImGui.SetNextItemWidth(s.GlyphWidth * 7 + style.FramePadding.X * 2.0f);
            if (ImGui.DragInt("##cols", ref Cols, 0.2f, 4, 32, "%d cols")) { ContentsWidthChanged = true; if (Cols < 1) Cols = 1; }
            ImGui.Checkbox("Show Data Preview", ref OptShowDataPreview);
            ImGui.Checkbox("Show HexII", ref OptShowHexII);
            if (ImGui.Checkbox("Show Ascii", ref OptShowAscii)) ContentsWidthChanged = true;             ImGui.Checkbox("Grey out zeroes", ref OptGreyOutZeroes);
            ImGui.Checkbox("Uppercase Hex", ref OptUpperCaseHex);

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.Text($"{base_display_addr + mem_size - 1:X4}");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(s.AddrDigitsCount * s.GlyphWidth + style.FramePadding.X * 2.0f);
        if (ImGui.InputText("##addr", AddrInputBuf, (uint)s.AddrDigitsCount + 1, ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (TryHexParse(AddrInputBuf, out int goto_addr))
            {
                GotoAddr = goto_addr - base_display_addr;
                HighlightMin = HighlightMax = -1;
            }
        }

        if (GotoAddr != -1)
        {
            if (GotoAddr < mem_size)
            {
                ImGui.BeginChild("##scrolling");
                ImGui.SetScrollFromPosY(ImGui.GetCursorStartPos().Y + GotoAddr / Cols * ImGui.GetTextLineHeight());
                ImGui.EndChild();
                DataEditingAddr = DataPreviewAddr = GotoAddr;
                DataEditingTakeFocus = true;
            }
            GotoAddr = -1;
        }

        ImGui.SameLine();

        if (ImGui.Button("Write Bp"))
        {
            if (TryHexParse(AddrInputBuf, out var addr))
                AddBreakpoint(addr & 0xffff, BPType.Write, -1, true, 0);
        }

        ImGui.SameLine();

        if (ImGui.Button("Read Bp"))
        {
            if (TryHexParse(AddrInputBuf, out var addr))
                AddBreakpoint(addr & 0xffff, BPType.Read, -1, false, 0);
        }

        ImGui.SameLine();

        if (ImGui.Button("Freeze Value"))
        {
            if (TryHexParse(AddrInputBuf, out var addr))
                AddCheat(addr, 0);
        }
    }

    // FIXME: We should have a way to retrieve the text edit cursor position more easily in the API, this is rather tedious. This is such a ugly mess we may be better off not using InputText() at all here.
    public unsafe static int Callback(ImGuiInputTextCallbackData* userdata)
    {
        var data = new ImGuiInputTextCallbackDataPtr(userdata);
        UserData* user_data = (UserData*)data.UserData;
        if (!data.HasSelection())
            user_data->CursorPos = data.CursorPos;
        if (data.SelectionStart == 0 && data.SelectionEnd == data.BufTextLen)
        {
            /// When not editing a byte, always rewrite its content (this is a bit tricky, since InputText technically "owns" the master copy of the buffer we edit it in there)
            data.DeleteChars(0, data.BufTextLen);
            fixed (char* ptr = &user_data->CurrentBufOverwrite[0])
                data.InsertChars(0, new string(ptr));
            data.SelectionStart = 0;
            data.SelectionEnd = 2;
            data.CursorPos = 0;
            return 1;
        }
        return 0;
    }


    private Sizes CalcSizes(out Sizes s, int mem_size, int base_display_addr)
    {
        s = new();
        ImGuiStylePtr style = ImGui.GetStyle();
        s.AddrDigitsCount = OptAddrDigitsCount;
        if (s.AddrDigitsCount == 0)
            for (int n = base_display_addr + mem_size - 1; n > 0; n >>= 4)
                s.AddrDigitsCount++;
        s.LineHeight = ImGui.GetTextLineHeight();
        s.GlyphWidth = ImGui.CalcTextSize("F").X + 1;                  // We assume the font is mono-space
        s.HexCellWidth = (int)(s.GlyphWidth * 2.5f);             // "FF " we include trailing space in the width to easily catch clicks everywhere
        s.SpacingBetweenMidCols = (int)(s.HexCellWidth * 0.25f); // Every OptMidColsCount columns we add a bit of extra spacing
        s.PosHexStart = (s.AddrDigitsCount + 2) * s.GlyphWidth;
        s.PosHexEnd = s.PosHexStart + s.HexCellWidth * Cols;
        s.PosAsciiStart = s.PosAsciiEnd = s.PosHexEnd;
        if (OptShowAscii)
        {
            s.PosAsciiStart = s.PosHexEnd + s.GlyphWidth * 1;
            if (OptMidColsCount > 0)
                s.PosAsciiStart += (Cols + OptMidColsCount - 1) / OptMidColsCount * s.SpacingBetweenMidCols;
            s.PosAsciiEnd = s.PosAsciiStart + Cols * s.GlyphWidth;
        }
        s.WindowWidth = s.PosAsciiEnd + style.ScrollbarSize + style.WindowPadding.X * 2 + s.GlyphWidth;

        return s;
    }

    private static readonly int[] sizes = [1, 1, 2, 2, 4, 4, 8, 8, sizeof(float), sizeof(double)];
    private readonly Action<int, int, int, bool, int> AddBreakpoint;
    private readonly Action<int, byte> AddCheat;

    private static int DataTypeGetSize(ImGuiDataType data_type)
    {
        Debug.Assert(data_type >= 0 && data_type < ImGuiDataType.COUNT);
        return sizes[(int)data_type];
    }

    private string FixedHex(int v, int count) => OptUpperCaseHex ? v.ToString("X").PadLeft(count, '0') : v.ToString("x").PadLeft(count, '0');

    private static bool TryHexParse(byte[] bytes, out int result)
    {
        string input = System.Text.Encoding.UTF8.GetString(bytes).ToString();
        return int.TryParse(input, NumberStyles.AllowHexSpecifier, CultureInfo.CurrentCulture, out result);
    }

    private static void ReplaceChars(byte[] bytes, string input)
    {
        var address = System.Text.Encoding.ASCII.GetBytes(input);
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = i < address.Length ? address[i] : (byte)0;
        }
    }

    private class UserData
    {
        public char[] CurrentBufOverwrite = new char[3];  // Input
        public int CursorPos;               // Output
    }
}
