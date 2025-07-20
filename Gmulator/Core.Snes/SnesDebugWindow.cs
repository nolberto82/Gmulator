using ImGuiNET;
using static Gmulator.Core.Snes.SnesLogger;

namespace Gmulator.Core.Snes;

internal class SnesDebugWindow : DebugWindow
{
    public Snes Snes { get; }

    public SnesDebugWindow(Snes snes)
    {
        Snes = snes;
        Breakpoints = snes.Breakpoints;

        MemRegions =
        [
            new("Wram",snes.GetWram, 0x7e0000,6),
            new("Sram",snes.GetSram,0x0000,4),
            new("Vram",snes.GetVram,0x0000,4),
            new("Cram",snes.GetCram,0x0000,3),
            new("Oram",snes.GetOram, 0x0000,3),
            new("Spc",snes.GetSpc,0x0000,4),
            new("Rom",snes.GetRom, 0x0000,6),
            new("Iram",snes.GetIram, 0x0000,3),
        ];

        OnTrace =
        [
            snes.Logger.Disassemble,
            snes.SpcLogger.Disassemble,
        ];

        ScrollY = [0, 0];
        JumpAddr = [-1, -1];
    }

    public override void Draw()
    {
        DrawCpuInfo();
        if (ImGui.BeginChild("##disasmcpu", new(0, 310), ImGuiChildFlags.FrameStyle))
        {
            var Cpu = Snes.Cpu;
            var bank = Cpu.PB << 16;
            var pc = Scroll(bank | Cpu.PC, 0);

            ImGui.BeginChild("##buttons", new(0, 45));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Snes.Logger.Logging ? GREEN : WHITE);
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
                Snes.SetState(Running);

            ImGui.Separator();

            for (int i = 0; i < DisasmMaxLines; i++)
            {
                var e = Snes.Logger.Disassemble(bank, pc, false, false);

                ImGui.PushID(pc);

                if (ImGui.Selectable($"{pc:X6} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        AddBreakpoint(pc, BPType.Exec, -1, false, RamType.Rom);
                }

                DrawHighlight(bank | Cpu.PC, pc);

                ImGui.PopID();
                ImGui.SameLine();
                ImGui.Text($"{e.disasm}");
                pc += (byte)e.size;
            }
            ImGui.EndChild();
        }

        if (ShowSa1 == true)
            RenderSa1();

        if (ShowSpc == true)
            DrawSpc();

        DrawCartInfo(Snes.Mapper.GetCartInfo());

        //DrawStackInfo(Snes.Ram.AsSpan(0, 0x2000), Snes.Cpu.SP, 0x1fff, "cpu");

        DrawRegisters(null);
    }

    public override void RenderSa1()
    {
        ImGui.SetNextWindowPos(new(550, 30));
        ImGui.SetNextWindowSize(new(220, 615));
        if (ImGui.Begin("Sa1"))
        {
            var Cpu = Snes.Sa1;
            var bank = Cpu.PB << 16;
            var Pc = Cpu.PC;
            var pc = Scroll(bank | Pc, 0);

            ImGui.BeginChild("##buttons", new(0, 45));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Snes.Logger.Logging ? GREEN : WHITE);
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
                var e = Snes.Logger.Disassemble(bank, pc, false, false);

                ImGui.PushID(pc);

                if (ImGui.Selectable($"{pc:X6} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        AddBreakpoint(e.pc, BPType.Exec, -1, false, RamType.Rom);
                }

                if (Breakpoints.TryGetValue(e.pc, out var bp))
                {
                    if (bp.Enabled && (bp.Type & BPType.Exec) != 0)
                        DrawRect(0x4000ff00, 0xff00ff00);
                    else
                        DrawRect(0x000000ff, 0xff0000ff);
                }
                if (e.pc == (bank | Pc))
                {
                    DrawRect(0x6000ffff, 0xff00ffff);
                    ImGui.SetScrollHereY(0.25f);
                }

                ImGui.PopID();
                ImGui.SameLine();
                ImGui.Text($"{e.disasm}");
                pc += (byte)e.size;
            }
        }
        ImGui.End();
    }

    private void DrawSpc()
    {
        ImGui.SetNextWindowPos(new(550 + (ShowSa1 == true ? 220 : 0), 30));
        ImGui.SetNextWindowSize(new(220, 615));
        if (ImGui.Begin("Spc"))
        {
            RenderSpcInfo();

            var Spc = Snes.Spc;
            var Pc = Spc.PC;
            var pc = (ushort)Scroll(Pc, 1);

            if (ImGui.BeginPopupContextWindow("gotomenu"))
                JumpTo(1);

            ImGui.BeginChild("##buttons", new(0, 50));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && Snes.SpcLogger.Logging ? GREEN : WHITE);
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
                var e = Snes.SpcLogger.Disassemble(0, pc, false, false);

                ImGui.PushID(pc);
                if (ImGui.Selectable($"{e.pc:X4} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        AddBreakpoint(e.pc, BPType.SpcExec, -1, false, RamType.SpcRom);
                }

                if (Breakpoints.TryGetValue(e.pc, out var bp))
                {
                    if (bp.Enabled && (bp.Type & BPType.SpcExec) != 0)
                        DrawRect(0x4000ff00, 0xff00ff00);
                    else
                        DrawRect(0x000000ff, 0xff0000ff);
                }
                else if (e.pc == Pc)
                    DrawRect(0x6000ffff, 0xff00ffff);

                ImGui.PopID();
                ImGui.SameLine();
                ImGui.Text($"{e.disasm}");
                pc += (byte)e.size;
            }

            ImGui.BeginChild("##spcportsports", new(0, 150));
            if (ImGui.BeginTable("##spcports", 2, ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                foreach (var c in Snes.Apu.GetIO())
                    TableRow(c.Key, $"{c.Value}");
                ImGui.EndTable();
            }
            ImGui.EndChild();
        }
        ImGui.End();
    }

    public override void DrawBreakpoints() => base.DrawBreakpoints();

    public override void DrawCpuInfo()
    {
        ImGui.BeginChild("##cpuwindow", new(0, 65));
        {
            var v = Snes.Cpu.GetRegs();
            for (int i = 0; i < v.Count; i++)
            {
                if (i == 0)
                    ImGui.Text($"{v.ElementAt(i).Key}:  {v.ElementAt(i).Value}");
                else
                    ImGui.Text($"{v.ElementAt(i).Key}: {v.ElementAt(i).Value}");
                if ((i + 1) % 3 != 0 && (i < v.Count - 1))
                    ImGui.SameLine();
            }
            ImGui.Text($"Cycles: {Snes.Ppu.HPos:D3}"); ImGui.SameLine();
            ImGui.Text($"Scanline: {Snes.Ppu.VPos}");
        }
        ImGui.EndChild();

        ImGui.BeginChild("##cpuflags", new(0, 55));
        {
            var v = Snes.Cpu.GetFlags();
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
            var v = Snes.Spc.GetRegs();
            for (int i = 0; i < v.Count; i++)
            {
                ImGui.Text($"{v.ElementAt(i).Key} {v.ElementAt(i).Value}");
                ImGui.SameLine();
            }
        }
        ImGui.EndChild();

        ImGui.BeginChild("##spcflags", new(0, 55));
        {
            var v = Snes.Spc.GetFlags();
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
        for (int c = 0; c < Snes.Dma.Count; c++)
        {
            if (ImGui.BeginTabBar("##dmatab"))
            {
                if (ImGui.BeginTabItem($"{c:X2}"))
                {
                    if (ImGui.BeginTable("##dmainfo", 2, ImGuiTableFlags.RowBg))
                    {
                        var v = Snes.Dma[c].GetIoRegs();
                        for (int i = 0; i < v.Count; i++)
                        {
                            TableRow(v.ElementAt(i).Key, $"{v.ElementAt(i).Value}");
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
        base.DrawStackInfo(data, addr, !Snes.Cpu.E ? start : 0x1ff, name);
    }

    public override void DrawRegisters(List<RegistersInfo> ioregisters)
    {
        base.DrawRegisters(Snes.Ppu.GetRegs());
    }

    public override void AddBreakpoint(int a, int type, int condition, bool write, int index = 0)
    {
        if (index == RamType.Sram)
            a += 0x6000;
        base.AddBreakpoint(a, type, condition, write, index);
        Snes.SaveBreakpoints(Snes.Mapper.Name);
    }

    public override void AddFreezeValue(int a, byte v)
    {
        base.AddFreezeValue(a, v);
    }

    public override void Continue(int type)
    {
        Snes.Run = true;
        if (type == SpcCpu)
        {
            Snes.SpcLogger.Log(Snes.Spc.PC);
            Snes.Spc.Step();
        }
        Snes.SetState(Running);
        base.Continue(type);
    }

    public override void StepInto(int type)
    {
        switch (type)
        {
            case MainCpu:
                Snes.SetState(StepMain);
                break;
            case Sa1Cpu:
                Snes.Sa1.Step();
                break;
            case SpcCpu:
                var spc = Snes.Spc.PC;
                Snes.Spc.Step();
                Snes.SpcLogger.Log(spc);
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
                var pc = Snes.Cpu.PB << 16 | Snes.Cpu.PC;
                var inst = Snes.Cpu.Disasm[Snes.ReadOp(pc)];
                if (inst.Name == "jsr" || inst.Name == "jsl")
                {
                    Snes.Cpu.StepOverAddr = pc + inst.Size;
                    Snes.Cpu.Step();
                    Snes.State = Running;
                }
                else
                    StepInto(MainCpu);
                Snes.Logger.Log(Snes.Cpu.PB << 16, pc);
                break;
            }
            case Sa1Cpu:
            {
                var pc = Snes.Sa1.PB << 16 | Snes.Sa1.PC;
                var inst = Snes.Sa1.Disasm[Snes.ReadOp(pc)];
                if (inst.Name == "jsr" || inst.Name == "jsl")
                {
                    Snes.Sa1.StepOverAddr = pc + inst.Size;
                    Snes.Sa1.Step();
                    Snes.State = Running;
                }
                else
                    StepInto(MainCpu);
                Snes.Logger.Log(Snes.Sa1.PB << 16, pc);
                break;
            }
            case SpcCpu:
                StepInto(MainCpu);
                Snes.SpcLogger.Log(Snes.Spc.PC);
                break;
        }
        base.StepOver(MainCpu);
    }

    public override void Reset(int type)
    {
        Snes.Reset("", "", true);
        base.Reset(type);
    }

    public override void StepScanline(int type)
    {
        switch (type)
        {
            case MainCpu:
            {
                var oldline = Snes.Ppu.VPos;
                while (oldline == Snes.Ppu.VPos)
                    Snes.Cpu.Step();
                Snes.State = Break;
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
            case Sa1Cpu: Snes.Logger.Toggle(); break;
            case SpcCpu: Snes.SpcLogger.Toggle(); break;
        }
    }

    public override void JumpTo(int i) => base.JumpTo(i);
}
