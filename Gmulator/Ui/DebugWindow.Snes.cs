using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using Gmulator.Core.Snes.Mappers;
using Gmulator.Interfaces;
using ImGuiNET;
using Raylib_cs;
using static Gmulator.Core.Snes.SnesLogger;

namespace Gmulator.Ui;

internal class SnesDebugWindow : DebugWindow
{
    private readonly string[] testcpu = ["Cpu", "Spc", "Gsu"];
    private const int CpuNumbers = 3;
    private readonly Snes Snes;
    private readonly SnesCpu Cpu;
    private readonly SnesPpu Ppu;
    private readonly SnesSpc Spc;
    private readonly SnesDsp Dsp;
    private readonly SnesSa1 Sa1;
    private readonly SnesLogger Logger;
    private readonly SnesSpcLogger SpcLogger;

    private readonly Func<int, int> ReadOp;

    private readonly int CoProcessor;
    private readonly BaseMapper Mapper;
    private readonly SnesDma Dma;

    public SnesDebugWindow(Snes snes) : base(snes.Cpu, snes.Ppu)
    {
        Snes = snes;
        Cpu = snes.Cpu;
        Ppu = snes.Ppu;
        Spc = snes.Spc;
        Dsp = snes.Dsp;
        Sa1 = snes.Sa1;
        Mapper = snes.Mapper;
        Dma = snes.Dma;
        Logger = snes.Logger;
        SpcLogger = snes.SpcLogger;
        Breakpoints = snes.Breakpoints;
        SetState = snes.SetState;
        ReadOp = snes.ReadOp;
        SaveBreakpoints = snes.SaveBreakpoints;
        CoProcessor = Mapper.CoProcessor;

        GameName = Mapper.Name ?? "";
        var mmu = snes.Mmu;

        MemRegions =
        [
            new("Work", mmu.ReadByte,mmu.WriteByte, 0x7e0000, 0x20000, 6),
            new("Save", Mapper.ReadSram, Mapper.Write, 0x0000, Mapper.Sram.Length,4),
            new("Video", Ppu.ReadVram, Ppu.WriteByte, 0x0000, 0x10000, 4),
            new("Sprites", Ppu.ReadOram,Ppu.WriteOram, 0x0000, 0x220, 3),
            new("Color", Ppu.ReadCram,Ppu.WriteCram, 0x0000, 0x200, 3),
            new("Spc", Spc.Read, Spc.Write,0x0000, 0x10000, 4),
            //new("Sa1", Sa1.ReadIram, Sa1.WriteIram, 0x0000, 0x800, 3),
            new("Rom",Mapper.Read, Mapper.Write, 0x0000, Mapper.Rom.Length, 6)
        ];

        if (Mapper.CoProcessor == BaseMapper.Sa1)
        {
            MemRegions.Add(new("Sa1", Sa1.ReadIram, Sa1.WriteIram, 0x0000, 0x800, 3));
        }

        RamNames =
        [
            new("Work", RamType.Wram), new("Save", RamType.Sram),
            new("Video", RamType.Vram), new("Sprites", RamType.Oram),
            new("Rom", RamType.Rom), new("Color", RamType.Cram),
            new("Spc Ram", RamType.SpcRam), new("Spc Rom", RamType.SpcRom),
            new("Gsu Rom", RamType.GsuRom), new("Register", RamType.Register),
        ];

        OnDisassemble =
        [
            Logger.Disassemble,
            Logger.Disassemble,
            SpcLogger.Disassemble,
            null
        ];

        GetCpuState = Cpu.GetRegisters;
        GetCpuFlags = Cpu.GetFlags;
        if (Sa1 != null)
        {
            GetSa1State = Sa1.GetRegisters;
            GetSa1Flags = Sa1.GetFlags;
        }
        GetPpuState = Ppu.GetState;
        GetApuState = Dsp.GetState;
        GetSpcState = Spc.GetRegisters;
        GetSpcFlags = Spc.GetFlags;
        GetPortState = snes.Apu.GetState;
        GetSpcPC = () => Spc.PC;

        ScrollY = new int[CpuNumbers];
        JumpAddr = new int[CpuNumbers];
    }

    public override void SetCpu(Snes snes)
    {
        OnDisassemble[1] = snes.Logger.Disassemble;
        OnDisassemble[2] = null;
    }

    public override void Draw(Texture2D texture)
    {
        base.Draw(texture);
        DrawDebugger(0, Logger.Logging, SelectedCpu);
        //DrawStackInfo(Snes.Ram.AsSpan(0, 0x2000), Snes.Cpu.SP, 0x1fff, "cpu");
        DrawCartInfo(Mapper.GetCartInfo());
        DrawRegisters();
        DrawDmaInfo();
        DrawMemory();

#if DEBUG || DECKDEBUG
        //DrawTestAddr([Cpu.TestAddr, Spc.TestAddr], testcpu);
#endif
    }

    public override void DrawDebugger(int PC, bool logging, int n)
    {
        if (SelectedCpu == MainCpu || SelectedCpu == SpcCpu)
            base.DrawDebugger(Cpu.PB << 16 | Cpu.PC, logging, n);
        else if (SelectedCpu == Sa1Cpu)
            base.DrawDebugger(Sa1.PB << 16 | Sa1.PC, logging, n);

    }

    public override void DrawButtons(bool logging, int processor) => base.DrawButtons(logging, processor);

    public override void DrawBreakpoints() => base.DrawBreakpoints();

    public override void DrawCpuInfo(Func<List<RegisterInfo>> cpu, Func<List<RegisterInfo>> cpuflags) =>
        base.DrawCpuInfo(cpu, cpuflags);

    public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

    public override void DrawMemory() => base.DrawMemory();

    public override void DrawDmaInfo()
    {
        ImGui.SetNextWindowPos(new(559, 680));
        ImGui.SetNextWindowSize(new(299, 291));
        ImGui.Begin("Dma", NoScrollFlags);
        for (int c = 0; c < 8; c++)
        {
            if (ImGui.BeginTabBar("##dmatab"))
            {
                if (ImGui.BeginTabItem($"{c:X2}"))
                {
                    if (ImGui.BeginTable("##dmainfo", 3, ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 140);
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
        ImGui.End();
    }

    public override void DrawStackInfo(Span<byte> data, int addr, int start, string name)
    {
        base.DrawStackInfo(data, addr, !Cpu.E ? start : 0x1ff, name);
    }

    public override void AddBreakpoint(int a, int type, int condition, bool write, RamType index = 0)
    {
        if (index == RamType.Sram)
            a += 0x6000;
        base.AddBreakpoint(a, type, condition, write, index);
    }

    public override void Continue(DebugState type)
    {
        if (SelectedCpu == SpcCpu)
        {
            SpcLogger.Log(Spc.PC);
            Spc.Step();
        }
        else if (SelectedCpu == GsuCpu)
        {
            //Snes.SpcLogger.Log(Snes.Spc.PC);
            //SetState(DebugState.StepGsu);
            //Snes.Gsu.Exec(Snes.State,Snes.Debug);
        }
        SetState(DebugState.Running);
        base.Continue(MainCpu);
    }

    public override void Reset(DebugState type)
    {
        Snes.Reset("", true, Ppu.ScreenBuffer);
        base.Reset(MainCpu);
    }

    public override void StepInto(DebugState type)
    {
        DebugState step = DebugState.StepMain;
        switch (SelectedCpu)
        {
            case Sa1Cpu:
                step = DebugState.StepSa1;
                break;
            case SpcCpu:
                //Spc.Step();
                SpcLogger.Log(Spc.PC);
                step = DebugState.StepSpc;
                break;
            case GsuCpu:
                //SetState(DebugState.StepGsu);
                //Snes.Gsu.Exec(Snes.State, Snes.Debug);
                break;
        }
        base.StepInto(step);
    }

    public override void StepOver(DebugState type)
    {
        switch (SelectedCpu)
        {
            case MainCpu:
            {
                var pc = Cpu.PB << 16 | Cpu.PC;
                var inst = Cpu.Disasm[ReadOp(pc)];
                if (inst.Name == "jsr" || inst.Name == "jsl")
                {
                    Cpu.StepOverAddr = pc + inst.Size;
                    Cpu.Step();
                    base.StepOver(MainCpu);
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
                    base.StepOver(MainCpu);
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
    }

    public override void StepScanline(DebugState type)
    {
        switch (SelectedCpu)
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
    }

    public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

    public override void ToggleTrace(DebugState type)
    {
        switch (SelectedCpu)
        {
            case MainCpu:
            case Sa1Cpu: Logger.Toggle(); break;
            case SpcCpu: SpcLogger.Toggle(); break;
        }
    }

    public override void JumpTo(int i) => base.JumpTo(i);
}
