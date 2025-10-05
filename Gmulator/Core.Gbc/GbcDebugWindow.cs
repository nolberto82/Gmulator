using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using static Gmulator.Core.Gbc.GbcCpu.Opcode;

namespace Gmulator.Core.Gbc
{
    internal class GbcDebugWindow : DebugWindow
    {
        private readonly Gbc Gbc;
        private readonly GbcIO IO;
        private readonly Action<DebugState> SetState;

        public GbcDebugWindow(Gbc gbc)
        {
            Gbc = gbc;
            IO = gbc.IO;
            Breakpoints = gbc.Breakpoints;
            SetState = gbc.SetState;
            SaveBreakpoints = gbc.SaveBreakpoints;

            MemRegions =
            [
                new("Wram",gbc.Mmu.ReadWram, 0x0000, 4),
                new("Vram",gbc.Mmu.ReadVram,0x0000, 4),
                new("Sram",gbc.Mmu.ReadSram,0x0000, 4),
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
                DrawCpuInfo();
                var pc = Scroll(Gbc.Cpu.PC, 0);

                if (ImGui.BeginPopupContextWindow("gotomenu"))
                    JumpTo(0);

                if (ImGui.IsKeyPressed(ImGuiKey.F5))
                    Gbc.SetState(DebugState.Running);

                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Gbc.Logger.Logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Key, ButtonSize))
                        v.e.Value(MainCpu);
                    ImGui.PopStyleColor();
                    if (v.i != 2 && v.i < ButtonNames.Count - 1)
                        ImGui.SameLine();
                }
                ImGui.Separator();

                for (int i = 0; i < DisasmMaxLines; i++)
                {
                    var (disasm, op, size) = Gbc.Logger.Disassemble(pc, false);

                    ImGui.PushID(pc);
                    if (ImGui.Selectable($"{pc:X4} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            AddBreakpoint(pc, BPType.Exec, -1, false, RamType.Rom);
                    }

                    DrawHighlight(Gbc.Cpu.PC, pc);

                    ImGui.PopID();
                    ImGui.SameLine();
                    ImGui.Text($"{disasm}");
                    pc += size;
                }
                ImGui.EndChild();
            }

            DrawRegisters(null);
            DrawCartInfo(Gbc.Mapper.GetInfo());
        }

        public override void DrawBreakpoints() => base.DrawBreakpoints();

        public override void DrawRegisters(List<RegistersInfo> ioregisters)
        {
            base.DrawRegisters(IO.GetLCDC());
            base.DrawRegisters(IO.GetIF());
            base.DrawRegisters(IO.GetIE());
        }

        public override void DrawCpuInfo()
        {
            ImGui.BeginChild("##cpuwindow", new(0, 65));

            var v = Gbc.Cpu.GetRegisters();
            for (int i = 0; i < v.Count; i++)
            {
                if (i == 0)
                    ImGui.Text($"{v.ElementAt(i).Key}: {v.ElementAt(i).Value}");
                else
                    ImGui.Text($"{v.ElementAt(i).Key}: {v.ElementAt(i).Value}");
                if ((i + 1) % 3 != 0 && (i < v.Count - 1))
                    ImGui.SameLine();
            }

            ImGui.EndChild();
        }

        public override void DrawMapperBanks(byte[] Prg, byte[] Chr)
        {

        }

        public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

        public override void DrawMemory() => base.DrawMemory();

        public override void AddBreakpoint(int a, int type, int condition, bool write, RamType index = 0) => base.AddBreakpoint(a, type, condition, write, index);

        public override void SetJumpAddress(object addr, int i) => base.SetJumpAddress(addr, i);

        public override void Continue(int type = 0)
        {
            Gbc.Cpu.Step();
            Gbc.SetState(DebugState.Running);
        }

        public override void StepInto(int type = 0)
        {
            Gbc.Cpu.Step();
            SetState(DebugState.Break);
            base.StepInto();
        }

        public override void StepOver(int type = 0)
        {
            var Cpu = Gbc.Cpu;
            var pc = Cpu.PC;
            var inst = GbcCpu.OpInfo00[Gbc.Mmu.Read(pc)];

            if (inst.Name == "call" || inst.Name == "rst")
            {
                Gbc.Cpu.StepOverAddr = pc + inst.Size;
                Cpu.Step();
                SetState(DebugState.Running);
            }
            else
                StepInto();
        }

        public override void Reset(int type = 0)
        {
            Gbc.Reset("", true);
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
