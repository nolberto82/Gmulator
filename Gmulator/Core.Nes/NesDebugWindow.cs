using ImGuiNET;

namespace Gmulator.Core.Nes
{
    internal class NesDebugWindow : DebugWindow
    {
        private Nes Nes;
        private NesCpu Cpu;
        private NesLogger Logger;
        private Action<DebugState> SetState;

        public NesDebugWindow(Nes nes, NesCpu cpu, NesMmu mmu, NesLogger logger)
        {
            Nes = nes;
            Breakpoints = nes.Breakpoints;
            Cpu = cpu;
            Logger = logger;

            SetState = nes.SetState;
            SaveBreakpoints = nes.SaveBreakpoints;

            GameName = mmu.Mapper.Name ?? "";

            MemRegions =
            [
                new("Work", mmu.ReadWram, 0x0000, 4),
                new("Video", mmu.ReadVram, 0x0000, 4),
                new("Sprite", mmu.ReadOram, 0x0000, 2),
                new("Program", mmu.ReadPrg, 0x0000, 6),
                new("Character", mmu.ReadChr, 0x0000, 6),
            ];

            OnTrace =
            [
                logger.Disassemble,
            ];

            ScrollY = [0];
            JumpAddr = [-1];
        }

        public override void Draw()
        {
            if (ImGui.BeginChild("disasm", new(-1, -1), ImGuiChildFlags.FrameStyle))
            {
                var pc = Scroll(Cpu.PC, 0);

                if (ImGui.BeginPopupContextWindow("gotomenu"))
                    JumpTo(0);

                if (ImGui.IsKeyPressed(ImGuiKey.F5))
                    Nes.SetState(DebugState.Running);

                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Logger.Logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Key, ButtonSize))
                        v.e.Value(MainCpu);
                    ImGui.PopStyleColor();
                    if (v.i != 2 && v.i < ButtonNames.Count - 1)
                        ImGui.SameLine();
                }
                ImGui.Separator();

                for (int i = 0; i < DisasmMaxLines; i++)
                {
                    var (disasm, op, size) = Logger.Disassemble(pc, false, true);

                    ImGui.PushID(pc);
                    if (ImGui.Selectable($"{pc:X4} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            AddBreakpoint(pc, BPType.Exec, -1, false, RamType.Rom);
                    }

                    if (Breakpoints.TryGetValue(pc, out var bp))
                    {
                        if (bp.Enabled && (bp.Type & BPType.Exec) != 0)
                            DrawRect(0x4000ff00, 0xff00ff00);
                        else
                            DrawRect(0x000000ff, 0xff0000ff);
                    }
                    if (pc == Cpu.PC)
                    {
                        DrawRect(0x6000ffff, 0xff00ffff);
                        ImGui.SetScrollHereY(0.25f);
                    }

                    ImGui.PopID();
                    ImGui.SameLine();
                    ImGui.Text($"{disasm}");
                    pc += size;
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

        public override void AddBreakpoint(int a, int type, int condition, bool write, RamType index = 0) => base.AddBreakpoint(a, type, condition, write, index);

        public override void Continue(int type) => Nes.SetState(DebugState.Running);

        public override void Reset(int type = 0)
        {
            Nes.Reset("", true);
            base.Reset(type);
        }

        public override void StepInto(int type)
        {
            SetState(DebugState.StepMain);
        }

        public override void StepOver(int type) => base.StepOver(type);

        public override void StepScanline(int type) => base.StepScanline(type);

        public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

        public override void JumpTo(int i) => base.JumpTo(i);
    }
}
