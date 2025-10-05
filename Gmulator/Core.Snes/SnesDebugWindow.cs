using Gmulator.Core.Nes;
using Gmulator.Core.Snes.Mappers;
using ImGuiNET;
using static Gmulator.Core.Snes.SnesLogger;

namespace Gmulator.Core.Snes;

internal class SnesDebugWindow : DebugWindow
{
    private readonly string[] testcpu = ["Cpu", "Spc", "Gsu"];
    private const int CpuNumbers = 3;
    private Snes Snes;
    private SnesCpu Cpu;
    private SnesPpu Ppu;
    private SnesSpc Spc;
    private SnesApu Apu;
    private SnesSa1 Sa1;
    private SnesLogger Logger;
    private SnesSpcLogger SpcLogger;
    private Action<DebugState> SetState;
    private Action<bool> SetRun;

    private Func<int, int> ReadOp;

    private int CoProcessor;
    private BaseMapper Mapper;
    private SnesDma Dma;

    public SnesDebugWindow(Snes snes, SnesCpu cpu, SnesPpu ppu, SnesSpc spc, SnesApu apu, SnesSa1 sa1, BaseMapper mapper, SnesDma dma, SnesLogger logger, SnesSpcLogger spclogger)
    {
        Snes = snes;
        Cpu = cpu;
        Ppu = ppu;
        Spc = spc;
        Apu = apu;
        Sa1 = sa1;
        Mapper = mapper;
        Dma = dma;
        Logger = logger;
        SpcLogger = spclogger;
        Breakpoints = snes.Breakpoints;
        SetState = snes.SetState;
        SetRun = snes.SetRun;
        ReadOp = snes.ReadOp;
        SaveBreakpoints = snes.SaveBreakpoints;
        CoProcessor = Mapper.CoProcessor;

        GameName = Mapper.Name ?? "";

        MemRegions =
        [
            new("Work",snes.GetWram, 0x7e0000,6),
            new("Save",snes.GetSram,0x0000,4),
            new("Video",snes.GetVram,0x0000,4),
            new("Color",snes.GetCram,0x0000,3),
            new("Sprites",snes.GetOram, 0x0000,3),
            new("Spc",snes.GetSpc,0x0000,4),
            new("Sa1",snes.GetIram, 0x0000,3),
            new("Rom",snes.GetRom, 0x0000,6)
        ];

        RamNames =
        [
            "Work", "Save", "Video", "Sprites",
            "Main Rom", "Color", "Spc Ram", "Spc Rom",
            "Gsu Rom", "Register"
        ];

        OnTrace =
        [
            logger.Disassemble,
            spclogger.Disassemble,
            null
        ];

        ScrollY = new int[CpuNumbers];
        JumpAddr = new int[CpuNumbers];
    }

    public override void SetCpu(Snes snes)
    {
        OnTrace[2] = null;
    }

    public override void Draw()
    {
        DrawCpuInfo();
        if (ImGui.BeginChild("##disasmcpu", new(0, 310), ImGuiChildFlags.FrameStyle))
        {
            var bank = Cpu.PB << 16;
            var pc = Scroll(bank | Cpu.PC, 0);

            ImGui.BeginChild("##buttons", new(0, 45));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Logger.Logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Key, ButtonSize))
                        v.e.Value(MainCpu);
                    ImGui.PopStyleColor();
                    if (v.i != 2 && v.i < ButtonNames.Count - 1)
                        ImGui.SameLine();
                }
            }
            ImGui.EndChild();

            if (ImGui.BeginPopupContextWindow("gotomenu"))
                JumpTo(0);

            if (ImGui.IsKeyPressed(ImGuiKey.F5))
                SetState(DebugState.Running);

            ImGui.Separator();

            for (int i = 0; i < DisasmMaxLines; i++)
            {
                var (disasm, op, size) = Logger.Disassemble(pc, false, false);

                ImGui.PushID(pc);

                if (ImGui.Selectable($"{pc:X6} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        AddBreakpoint(pc, BPType.Exec, -1, false, RamType.Rom);
                }

                DrawHighlight(bank | Cpu.PC, pc);

                ImGui.PopID();
                ImGui.SameLine();
                ImGui.Text($"{op:x2}  {disasm}");
                pc += size;
            }
            ImGui.EndChild();
        }

        DrawCartInfo(Mapper.GetCartInfo());

        //DrawStackInfo(Snes.Ram.AsSpan(0, 0x2000), Snes.Cpu.SP, 0x1fff, "cpu");

        DrawRegisters(null);

#if DEBUG || DECKDEBUG
        DrawTestAddr([Cpu.TestAddr, Spc.TestAddr], testcpu);
#endif
    }

    public override void DrawCoProcessors()
    {
        ImGui.SetNextWindowPos(new(555, 30));
        ImGui.SetNextWindowSize(new(280, 615));
        if (ImGui.Begin("CoProcessors"))
        {
            if (ImGui.BeginTabBar("##coprocessors"))
            {
                if (CoProcessor == BaseMapper.CoprocessorSa1)
                {
                    if (ImGui.BeginTabItem("Sa1"))
                    {
                        DrawSa1();
                        ImGui.EndTabItem();
                    }
                }

                if (ImGui.BeginTabItem("Spc"))
                {
                    DrawSpc();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }

    public void DrawSa1()
    {
        //ImGui.SetNextWindowPos(new(550, 30));
        //ImGui.SetNextWindowSize(new(220, 615));
        if (ImGui.BeginChild("Sa1"))
        {
            var Cpu = Sa1;
            var bank = Cpu.PB << 16;
            var Pc = Cpu.PC;
            var pc = Scroll(bank | Pc, 0);

            ImGui.BeginChild("##buttons", new(0, 45));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Logger.Logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Key, ButtonSize))
                        v.e.Value(Sa1Cpu);
                    ImGui.PopStyleColor();
                    if (v.i != 2 && v.i < ButtonNames.Count - 1)
                        ImGui.SameLine();
                }
            }
            ImGui.EndChild();

            if (ImGui.BeginPopupContextWindow("gotomenu"))
                JumpTo(0);

            for (int i = 0; i < DisasmMaxLines; i++)
            {
                var (disasm, op, size) = Logger.Disassemble(pc, false, false);

                ImGui.PushID(pc);

                if (ImGui.Selectable($"{pc:X6} ", false, ImGuiSelectableFlags.AllowDoubleClick))
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
                if (pc == (bank | Pc))
                {
                    DrawRect(0x6000ffff, 0xff00ffff);
                    ImGui.SetScrollHereY(0.25f);
                }

                ImGui.PopID();
                ImGui.SameLine();
                ImGui.Text($"{op}  {disasm}");
                pc += size;
            }
            ImGui.EndChild();
        }
    }

    private void DrawSpc()
    {
        //ImGui.SetNextWindowPos(new(550 + (ShowSa1 == true ? 220 : 0), 30));
        //ImGui.SetNextWindowSize(new(220, 615));
        if (ImGui.BeginChild("Spc"))
        {
            RenderSpcInfo();

            var Pc = Spc.PC;
            int pc = Scroll(Pc, 1) & 0xffff;

            if (ImGui.BeginPopupContextWindow("gotomenu"))
                JumpTo(1);

            ImGui.BeginChild("##buttons", new(0, 50));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && SpcLogger.Logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Key, ButtonSize))
                        v.e.Value(SpcCpu);
                    ImGui.PopStyleColor();
                    if (v.i != 2 && v.i < ButtonNames.Count - 1)
                        ImGui.SameLine();
                }
            }
            ImGui.EndChild();

            for (int i = 0; i < DisasmMaxLines; i++)
            {
                var (disasm, op, size) = SpcLogger.Disassemble(pc, false, false);

                ImGui.PushID(pc);
                if (ImGui.Selectable($"{pc:X4} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        AddBreakpoint(pc, BPType.SpcExec, -1, false, RamType.SpcRom);
                }

                if (Breakpoints.TryGetValue(pc, out var bp))
                {
                    if (bp.Enabled && (bp.Type & BPType.SpcExec) != 0)
                        DrawRect(0x4000ff00, 0xff00ff00);
                    else
                        DrawRect(0x000000ff, 0xff0000ff);
                }
                else if (pc == Pc)
                    DrawRect(0x6000ffff, 0xff00ffff);

                ImGui.PopID();
                ImGui.SameLine();
                ImGui.Text($"{disasm}");
                pc += size;
            }

            ImGui.BeginChild("##spcportsports", new(0, 150));
            if (ImGui.BeginTable("##spcports", 2, ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                foreach (var c in Apu.GetIO())
                    TableRow(c.Key, $"{c.Value}");
                ImGui.EndTable();
            }
            ImGui.EndChild();
            ImGui.EndChild();
        }
    }

    public override void DrawBreakpoints() => base.DrawBreakpoints();

    public override void DrawCpuInfo()
    {
        ImGui.BeginChild("##cpuwindow", new(0, 65));
        {
            var v = Cpu.GetRegisters();
            for (int i = 0; i < v.Count; i++)
            {
                if (i == 0)
                    ImGui.Text($"{v.ElementAt(i).Key}:  {v.ElementAt(i).Value}");
                else
                    ImGui.Text($"{v.ElementAt(i).Key}: {v.ElementAt(i).Value}");
                if ((i + 1) % 3 != 0 && (i < v.Count - 1))
                    ImGui.SameLine();
            }
            ImGui.Text($"Cycles: {Ppu.HPos:D3}"); ImGui.SameLine();
            ImGui.Text($"Scanline: {Ppu.VPos}");
        }
        ImGui.EndChild();

        ImGui.BeginChild("##cpuflags", new(0, 55));
        {
            var v = Cpu.GetFlags();
            for (int i = 0; i < v.Count; i++)
            {
                Checkbox(v.ElementAt(i).Key, v.ElementAt(i).Value);
                if ((i + 1) % 4 > 0 || i > 4)
                    ImGui.SameLine();
            }
        }
        ImGui.EndChild();
    }

    private void RenderSpcInfo()
    {
        ImGui.BeginChild("##spcinfowin", new(0, 55));
        {
            var v = Spc.GetRegs();
            for (int i = 0; i < v.Count; i++)
            {
                ImGui.Text($"{v.ElementAt(i).Key} {v.ElementAt(i).Value}");
                ImGui.SameLine();
            }
        }
        ImGui.EndChild();

        ImGui.BeginChild("##spcflags", new(0, 55));
        {
            var v = Spc.GetFlags();
            for (int i = 0; i < v.Count; i++)
            {
                Checkbox(v.ElementAt(i).Key, v.ElementAt(i).Value);
                if ((i + 1) % 4 > 0)
                    ImGui.SameLine();
            }
        }
        ImGui.EndChild();
    }

    public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

    public override void DrawMemory() => base.DrawMemory();

    public override void DrawDmaInfo()
    {
        for (int c = 0; c < 8; c++)
        {
            if (ImGui.BeginTabBar("##dmatab"))
            {
                if (ImGui.BeginTabItem($"{c:X2}"))
                {
                    if (ImGui.BeginTable("##dmainfo", 3, ImGuiTableFlags.RowBg))
                    {
                        var v = Dma.GetIoRegs(c);
                        for (int i = 0; i < v.Count; i++)
                        {
                            TableRowCol3(v[i].Address, v[i].Name, v[i].Value);
                        }
                        ImGui.EndTable();
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
    }

    public override void DrawStackInfo(Span<byte> data, int addr, int start, string name)
    {
        base.DrawStackInfo(data, addr, !Cpu.E ? start : 0x1ff, name);
    }

    public override void DrawRegisters(List<RegistersInfo> ioregisters)
    {
        base.DrawRegisters(Ppu.GetRegs());
    }

    public override void AddBreakpoint(int a, int type, int condition, bool write, RamType index = 0)
    {
        if (index == RamType.Sram)
            a += 0x6000;
        base.AddBreakpoint(a, type, condition, write, index);
    }

    public override void Continue(int type)
    {
        SetRun(true);
        if (type == SpcCpu)
        {
            SpcLogger.Log(Spc.PC);
            Spc.Step();
        }
        else if (type == GsuCpu)
        {
            //Snes.SpcLogger.Log(Snes.Spc.PC);
            //SetState(DebugState.StepGsu);
            //Snes.Gsu.Exec(Snes.State,Snes.Debug);
        }
        SetState(DebugState.Running);
        base.Continue(type);
    }

    public override void Reset(int type)
    {
        Snes.Reset("", true);
        base.Reset(type);
    }

    public override void StepInto(int type)
    {
        switch (type)
        {
            case MainCpu:
                SetState(DebugState.StepMain);
                break;
            case Sa1Cpu:
                Sa1.Step();
                break;
            case SpcCpu:
                Spc.Step();
                SpcLogger.Log(Spc.PC);
                SetState(DebugState.StepSpc);
                break;
            case GsuCpu:
                //SetState(DebugState.StepGsu);
                //Snes.Gsu.Exec(Snes.State, Snes.Debug);
                break;
        }
        base.StepInto(type);
    }

    public override void StepOver(int type)
    {
        switch (type)
        {
            case MainCpu:
            {
                var pc = Cpu.PB << 16 | Cpu.PC;
                var inst = Cpu.Disasm[ReadOp(pc)];
                if (inst.Name == "jsr" || inst.Name == "jsl")
                {
                    Cpu.StepOverAddr = pc + inst.Size;
                    Cpu.Step();
                    SetState(DebugState.Running);
                }
                else
                    StepInto(MainCpu);
                Logger.Log(Ppu.HPos);
                break;
            }
            case Sa1Cpu:
            {
                var pc = Sa1.PB << 16 | Sa1.PC;
                var inst = Sa1.Disasm[ReadOp(pc)];
                if (inst.Name == "jsr" || inst.Name == "jsl")
                {
                    Sa1.StepOverAddr = pc + inst.Size;
                    Sa1.Step();
                    SetState(DebugState.Running);
                }
                else
                    StepInto(MainCpu);
                Logger.Log(Ppu.HPos);
                break;
            }
            case SpcCpu:
                StepInto(MainCpu);
                SpcLogger.Log(Spc.PC);
                break;
        }
        base.StepOver(MainCpu);
    }

    public override void StepScanline(int type)
    {
        switch (type)
        {
            case MainCpu:
            {
                var oldline = Ppu.VPos;
                while (oldline == Ppu.VPos)
                    Cpu.Step();
                SetState(DebugState.Break);
                break;
            }
        }
        base.StepScanline(type);
    }

    public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

    public override void ToggleTrace(int type)
    {
        switch (type)
        {
            case MainCpu:
            case Sa1Cpu: Logger.Toggle(); break;
            case SpcCpu: SpcLogger.Toggle(); break;
        }
    }

    public override void JumpTo(int i) => base.JumpTo(i);
}
