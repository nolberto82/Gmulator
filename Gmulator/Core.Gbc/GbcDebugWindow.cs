using Gmulator.Core.Nes;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Gbc
{
    internal class GbcDebugWindow : DebugWindow
    {
        public Gbc Gbc { get; }

        public GbcDebugWindow(Gbc gbc)
        {
            Gbc = gbc;
            Breakpoints = gbc.Breakpoints;

            MemRegions =
            [
                new("Wram",gbc.Mmu.ReadWram, 0x0000, 4),
                new("Vram",gbc.Mmu.ReadVram,0x0000, 4),
                new("Oram",gbc.Mmu.ReadOram, 0xfe00,2),
                new("Rom",gbc.Mmu.ReadRom, 0x000000,6),
            ];

            OnTrace =
            [
                gbc.Logger.Disassemble,
            ];

            ScrollY = [0];
            JumpAddr = [-1];
        }

        public override void Draw()
        {
            if (ImGui.BeginChild("##disasm"))
            {
                var pc = Scroll(Gbc.Cpu.PC, 0);

                if (ImGui.BeginPopupContextWindow("gotomenu"))
                    JumpTo(0);

                if (ImGui.IsKeyPressed(ImGuiKey.F5))
                    Gbc.SetState(Running);

                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Gbc.Logger.Logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Key))
                        v.e.Value(MainCpu);
                    ImGui.PopStyleColor();
                    if (v.i != 2 && v.i < ButtonNames.Count - 1)
                        ImGui.SameLine();
                }
                ImGui.Separator();

                for (int i = 0; i < DisasmMaxLines; i++)
                {
                    var e = Gbc.Logger.Disassemble(0, pc, false);

                    ImGui.PushID(pc);
                    if (ImGui.Selectable($"{e.pc:X4} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            AddBreakpoint(e.pc, BPType.Exec, -1, false, RamType.Rom);
                    }

                    DrawHighlight(Gbc.Cpu.PC, e.pc);

                    ImGui.PopID();
                    ImGui.SameLine();
                    ImGui.Text($"{e.disasm}");
                    pc += (byte)e.size;
                }
                ImGui.EndChild();
            }
        }

        public override void DrawBreakpoints() => base.DrawBreakpoints();

        public override void DrawCpuInfo()
        {

        }

        public override void DrawMapperBanks(byte[] Prg, byte[] Chr)
        {

        }

        public override void DrawMemory() => base.DrawMemory();

        public override void AddBreakpoint(int a, int type, int condition, bool write, int index = 0) => base.AddBreakpoint(a, type, condition, write, index);

        public override void SetJumpAddress(object addr, int i) => base.SetJumpAddress(addr, i);

        public override void Continue(int type = 0)
        {
            Gbc.SetState(Running);
            StepInto();
        }

        public override void StepInto(int type = 0) => Gbc.Cpu.Step();

        public override void StepOver(int type = 0) => base.StepOver(type);

        public override void Reset(int type = 0)
        {
            Gbc.Reset("", "", true);
            base.Reset(type);
        }

        public override void StepScanline(int type = 0) => base.StepScanline(type);

        public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

        public override void ToggleTrace(int type = 0)
        {
            Gbc.Logger.Toggle();
        }
    }
}
