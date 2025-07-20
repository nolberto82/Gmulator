using ImGuiNET;

namespace Gmulator.Core.Nes
{
    internal class NesDebugWindow : DebugWindow
    {
        public Nes Nes { get; }

        public NesDebugWindow(Nes nes)
        {
            Nes = nes;
            Breakpoints = nes.Breakpoints;

            MemRegions =
            [
                new("Wram",nes.Mmu.ReadWram, 0x0000,4),
                new("Vram",nes.Mmu.ReadVram,0x0000, 4),
                new("Oram",nes.Mmu.ReadOram, 0x0000, 2),
                new("Prgm",nes.Mmu.ReadPrg,0x000000, 6),
                new("Chrm",nes.Mmu.ReadChr, 0x000000,6),
            ];

            OnTrace =
            [
                nes.Logger.Disassemble,
            ];

            ScrollY = [0];
            JumpAddr = [-1];
        }

        public override void Draw()
        {
            if (ImGui.BeginChild("disasm", new(-1, -1), ImGuiChildFlags.FrameStyle))
            {
                var pc = Scroll(Nes.Cpu.PC, 0);

                if (ImGui.BeginPopupContextWindow("gotomenu"))
                    JumpTo(0);

                if (ImGui.IsKeyPressed(ImGuiKey.F5))
                    Nes.SetState(Running);

                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Nes.Logger.Logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Key))
                        v.e.Value(MainCpu);
                    ImGui.PopStyleColor();
                    if (v.i != 2 && v.i < ButtonNames.Count - 1)
                        ImGui.SameLine();
                }
                ImGui.Separator();

                for (int i = 0; i < DisasmMaxLines; i++)
                {
                    var e = Nes.Logger.Disassemble(0, pc, false, true);

                    ImGui.PushID(pc);
                    if (ImGui.Selectable($"{e.pc:X4} "))
                    {

                    }
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

        public override void AddBreakpoint(int a, int type, int condition, bool write, int index = 0) => base.AddBreakpoint(a, type, condition, write);

        public override void Continue(int type) => Nes.SetState(Running);

        public override void StepInto(int type) => Nes.Cpu.Step();

        public override void StepOver(int type) => base.StepOver(type);

        public override void StepScanline(int type) => base.StepScanline(type);

        public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

        public override void JumpTo(int i) => base.JumpTo(i);
    }
}
