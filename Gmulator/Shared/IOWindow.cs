using ImGuiNET;

namespace Gmulator;
public static class IOWindow
{
    public static void Render()
    {
        if (ImGui.BeginTabBar("##IORegs"))
        {
            if (ImGui.BeginTabItem("PPU"))
            {
                ImGui.BeginTable("IO3", 2, ImGuiTableFlags.Borders);
                //TableRow("Cycle", $"{Ppu.Cycle}");
                //TableRow("Scanline", $"{Ppu.Scanline}");
                //TableRow("Total Cycles", $"{Ppu.Totalcycles}");
                //TableRow("V", $"{Ppu.Lp.V:X4}");
                //TableRow("T", $"{Ppu.Lp.T:X4}");
                //TableRow("X", $"{Ppu.Lp.Fx:X2}");
                //TableRow("Background", $"{Ppu.Background}");
                //TableRow("Sprite", $"{Ppu.Sprite}");
                //TableRow("Sprite 0 Hit", $"{Ppu.Sprite0hit}");
                //TableRow("VBlank", $"{Ppu.Vblank}");
                //TableRow("Nmi", $"{Ppu.Nmi}");
                ImGui.EndTable();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("CH1"))
            {
                ImGui.BeginTable("IO0", 2, ImGuiTableFlags.Borders);
                ImGui.TableSetupColumn("Square 1");
                ImGui.TableSetupColumn("");
                ImGui.TableHeadersRow();
                //foreach (var e in Apu.GetChannel1())
                //    TableRow(e.Key, $"{e.Value}");
                ImGui.EndTable();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("CH2"))
            {
                ImGui.BeginTable("IO2", 2, ImGuiTableFlags.Borders);
                ImGui.TableSetupColumn("");
                ImGui.TableSetupColumn("LCDC");
                ImGui.TableHeadersRow();
                //foreach (var e in Apu.GetChannel2())
                //    TableRow(e.Key, $"{e.Value}");
                ImGui.EndTable();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("CH3"))
            {
                ImGui.BeginTable("IO3", 2, ImGuiTableFlags.Borders);
                ImGui.TableSetupColumn("");
                ImGui.TableSetupColumn("LCDC");
                ImGui.TableHeadersRow();
                //foreach (var e in Apu.GetChannel3())
                //    TableRow(e.Key, $"{e.Value}");
                ImGui.EndTable();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("CH4"))
            {
                ImGui.BeginTable("IO4", 2, ImGuiTableFlags.Borders);
                ImGui.TableSetupColumn("");
                ImGui.TableSetupColumn("LCDC");
                ImGui.TableHeadersRow();
                //foreach (var e in Apu.GetChannel4())
                //    TableRow(e.Key, $"{e.Value}");
                ImGui.EndTable();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("CH5"))
            {
                ImGui.BeginTable("IO5", 2, ImGuiTableFlags.Borders);
                ImGui.TableSetupColumn("");
                ImGui.TableSetupColumn("LCDC");
                ImGui.TableHeadersRow();
                //foreach (var e in Apu.GetChannel5())
                //    TableRow(e.Key, $"{e.Value}");
                ImGui.EndTable();

                ImGui.EndTabItem();
            }
        }
    }
}
