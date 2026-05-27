using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Snes
{
    public class SnesGui
    {
        public SnesGui()
        {
        }

        public void Render(Snes Snes)
        {
            var Cpu = Snes.Cpu;
            var Ppu = Snes.Ppu;
            var Spc = Snes.Spc;
            var Apu = Snes.Apu;
            var Dsp = Snes.Dsp;
            var Logger = Snes.Logger;
            var SpcLogger = Snes.SpcLogger;
            var Breakpoints = Snes.Breakpoints;
            var DebugWindow = Snes.DebugWindow;
            var Ram = Snes.Ram;

            if (ImGui.Begin("Apu Channels"))
            {
                for (int i = 0; i < Dsp.Channels.Length; i++)
                {
                    Channel ch = Dsp.Channels[i];
                    ImGui.Checkbox($"{i}", ref ch.Enabled);
                }
                ImGui.End();
            }

            //if (ImGui.Begin("Cpu", NoScrollFlags))
            //{
            //    ImGui.Columns(2);
            //    ImGui.SetColumnWidth(0, 350);
            //    ImGui.SetColumnWidth(1, 160);
            //    DebugWindow.Toggle = Logger.Toggle;
            //    DebugWindow.Render(Cpu.PB << 16 | Cpu.PC, Logger.Logging, Snes.IsSpc = false, Cpu.TestAddr);

            //    Span<byte> stack = new();
            //    if (0x1ff - Cpu.SP + 1 > 0)
            //        stack = new Span<byte>(Snes.Ram, Cpu.SP + 1, 0x1ff - Cpu.SP + 1);
            //    var cpuinfo = Cpu.GetRegs();
            //    var ppuinfo = Ppu.GetRegs();
            //    var flags = Cpu.GetFlags();

            //    ImGui.NextColumn();
            //    if (ImGui.BeginChild("Cpu Info", new(0, 150)))
            //    {
            //        ImGui.BeginTable("##cpuinfo", 2, ImGuiTableFlags.RowBg);
            //        {
            //            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
            //            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);

            //            foreach (var e in cpuinfo)
            //            {
            //                if (e.Key != "S")
            //                    TableRow($"{e.Key}", $"{e.Value}");
            //            }
            //            TableRow("Vram", $"{ppuinfo["Vram Addr"]}");
            //            ImGui.EndTable();
            //        }
            //        ImGui.EndChild();
            //    }

            //    ImGui.BeginGroup();
            //    {
            //        int i = 0;
            //        foreach (var e in flags)
            //        {
            //            bool v = e.Value;
            //            ImGui.Checkbox(e.Key, ref v);
            //            i++;
            //            if (i != 4)
            //                ImGui.SameLine();
            //        }
            //    }
            //    ImGui.EndGroup();

            //    ImGui.Text($"Cycles: {Ppu.Cycles}");

            //    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
            //    ImGui.BeginGroup();
            //    string input = "";
            //    for (int i = 0; i < stack.Length; i += 2)
            //    {
            //        if (i >= stack.Length - 1) break;
            //        input += $"${stack[i]:x2} ${stack[i + 1]:x2}\n";
            //    }
            //    ImGui.Text($"S:{cpuinfo["S"]}");
            //    ImGui.InputTextMultiline("##123", ref input, 6000, new(0, 0));
            //    ImGui.EndGroup();
            //    ImGui.End();
            //}

            if (ImGui.Begin("Spc"))
            {
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 300);
                //ImGui.SetColumnWidth(1, 120);
                DebugWindow.Toggle = SpcLogger.Toggle;
                DebugWindow.Render(Spc.PC, SpcLogger.Logging, Snes.IsSpc = true, Spc.TestAddr);

                ImGui.NextColumn();
                Span<byte> stack = new();
                if (0x1ff - Cpu.SP + 1 > 0)
                    stack = new Span<byte>(Ram, Cpu.SP + 1, 0x1ff - Cpu.SP + 1);

                if (ImGui.BeginChild("spcports"))
                {
                    var spcinfo = Spc.GetRegs();
                    if (ImGui.BeginTable("##spcportstable", 2, ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Registers");
                        ImGui.TableSetupColumn("");
                        ImGui.TableHeadersRow();
                        foreach (var e in spcinfo)
                            TableRow(e.Key, $"{e.Value:X2}");
                        TableRow("Cycles", $"{Snes.Apu.Cycles}");
                        ImGui.EndTable();
                    }

                    ImGui.BeginGroup();
                    {
                        int i = 0;
                        foreach (var e in Spc.GetFlags())
                        {
                            bool v = e.Value;
                            ImGui.Checkbox(e.Key, ref v);
                            i++;
                            if (i != 4)
                                ImGui.SameLine();
                        }
                    }
                    ImGui.EndGroup();

                    var spcio = Apu.GetApuIo();
                    if (ImGui.BeginTable("##aputable", 2, ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Ports", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("");
                        ImGui.TableHeadersRow();
                        foreach (var e in spcio)
                            TableRow(e.Key, $"{e.Value:X2}");

                        ImGui.EndTable();
                    }
                    ImGui.EndChild();
                }
                ImGui.Columns(1);
                ImGui.End();

            }

            if (ImGui.Begin("Memory", NoScrollFlags))
            {
                if (ImGui.BeginTabBar("##memtab"))
                {
                    if (ImGui.BeginTabItem("Wram"))
                    {
                        DebugWindow.RenderMemory(Ram, 0);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Sram"))
                    {
                        DebugWindow.RenderMemory(Snes.Mapper.Sram, 1);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Vram"))
                    {
                        var data = new byte[Ppu.Vram.Length * 2];
                        Buffer.BlockCopy(Ppu.Vram, 0, data, 0, data.Length);
                        DebugWindow.RenderMemory(data, 2);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Cram"))
                    {
                        var data = new byte[Ppu.Cram.Length * 2];
                        Buffer.BlockCopy(Ppu.Cram, 0, data, 0, data.Length);
                        DebugWindow.RenderMemory(data, 3);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Oram"))
                    {
                        DebugWindow.RenderMemory(Ppu.Oam, 4);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Cpu Rom"))
                    {
                        var rom = Snes.Mapper.Rom;
                        DebugWindow.RenderMemory(rom, 5);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Spc Ram"))
                    {
                        var ram = Apu.Ram;
                        DebugWindow.RenderMemory(ram, 6);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Spc Rom"))
                    {
                        var rom = Apu.IplBootrom;
                        DebugWindow.RenderMemory(rom, 7);
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.End();
            }

            //if (ImGui.Begin("Breakpoints", NoScrollFlags))
            //{
            //    DebugWindow.RenderBreakpoints();
            //    ImGui.End();
            //}

            //if (ImGui.Begin("Registers", ImGuiWindowFlags.NoScrollbar))
            //{
            //    if (ImGui.BeginTabBar("##IORegs"))
            //    {
            //        if (ImGui.BeginTabItem("PPU"))
            //        {
            //            ImGui.BeginChild("##ppuregs");
            //            ImGui.BeginTable("PPU5", 2, ImGuiTableFlags.RowBg);

            //            ImGui.TableSetupColumn("");
            //            ImGui.TableSetupColumn("LCDC");
            //            ImGui.TableHeadersRow();
            //            foreach (var e in Ppu.GetRegs())
            //                TableRow(e.Key, $"{e.Value}");
            //            ImGui.EndTable();
            //            ImGui.EndChild();
            //            ImGui.EndTabItem();
            //        }

            //        if (ImGui.BeginTabItem("DMA"))
            //        {
            //            if (ImGui.BeginChild("##dmachannels"))
            //            {
            //                if (ImGui.BeginTable("DMA5", 2, ImGuiTableFlags.RowBg))
            //                {
            //                    ImGui.TableSetupColumn("");
            //                    ImGui.TableSetupColumn("");
            //                    ImGui.TableNextRow();
            //                    for (int i = 0; i < 8; i++)
            //                    {
            //                        TableRowBgColor($"Channel {i:X2}", "", 0xff606060);
            //                        foreach (var e in Snes.Dma[i].GetIoRegs())
            //                            TableRow(e.Key, $"{e.Value:X6}");

            //                    }
            //                    ImGui.EndTable();
            //                }

            //                ImGui.EndChild();
            //            }
            //            ImGui.EndTabItem();
            //        }

            //        if (ImGui.BeginTabItem("Apu"))
            //        {
            //            if (ImGui.BeginChild("##apuchannels"))
            //            {
            //                if (ImGui.BeginTable("APU5", 3, ImGuiTableFlags.RowBg))
            //                {
            //                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
            //                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
            //                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
            //                    ImGui.TableNextRow();
            //                    //TableRowBgColor($"Channel {i:X2}", "", 0xff606060);
            //                    foreach (var e in Snes.Dsp.GetRegs())
            //                        TableRow(e.Key, $"{e.Value[0]}", $"{e.Value[1]}");

            //                    for (var i = 0; i < 8; i++)
            //                    {
            //                        TableRowBgColor($"Voice {i}", "", 0xff606060);
            //                        foreach (var e in Snes.Dsp.GetVoices(i))
            //                        {
            //                            TableRow(e.Key, $"{e.Value[0]}", $"{e.Value[1]}");
            //                        }
            //                    }
            //                    ImGui.EndTable();
            //                }
            //                ImGui.EndChild();
            //            }
            //            ImGui.EndTabItem();
            //        }

            //        ImGui.EndTabBar();
            //    }
            //    ImGui.End();
            //}

            //if (ImGui.Begin("Cartridge", ImGuiWindowFlags.NoScrollbar))
            //{
            //    if (Snes.Mapper != null)
            //    {
            //        var cart = Snes.Mapper.GetCartInfo();
            //        foreach (var c in cart)
            //        {
            //            ImGui.TextWrapped($"{c.Key}: {c.Value}");
            //        }
            //    }
            //    ImGui.End();
            //}
        }
    }
}
