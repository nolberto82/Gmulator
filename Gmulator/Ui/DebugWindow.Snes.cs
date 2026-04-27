using Gmulator.Core.Snes;
using Gmulator.Core.Snes.Sa1;
using Gmulator.Core.Snes.Spc;
using Gmulator.Interfaces;
using ImGuiNET;

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
    private readonly SnesMapper Mapper;
    private readonly SnesDma Dma;

    public SnesDebugWindow(Snes snes) : base(snes)
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
        ReadOp = snes.ReadOp;
        SaveBreakpoints = snes.SaveBreakpoints;
        CoProcessor = Mapper.Coprocessor;

        GameName = Mapper.Name ?? "";

        OnDisassemble =
        [
            Logger.Disassemble,
            Logger.Disassemble,
            SpcLogger.Disassemble,
            null
        ];

        GetCpuState = Cpu.GetRegisters;
        GetCpuFlags = Cpu.GetFlags;
        GetPpuState = Ppu.GetState;
        GetApuState = Dsp.GetState;
        GetSpcState = Spc.GetRegisters;
        GetSpcFlags = Spc.GetFlags;
        GetPortState = snes.Apu.GetState;
        GetSpcPC = () => Spc.PC;

        if (Sa1 != null)
        {
            GetSa1State = Sa1!.GetRegisters;
            GetSa1Flags = Sa1!.GetFlags;
            GetSa1IORegs = Sa1!.GetIORegisters;
        }

        ScrollY = new int[CpuNumbers];
        JumpAddr = new int[CpuNumbers];
    }

    public override void Reset(Snes snes)
    {
        var mmu = snes.Mmu;

        MemRegions =
        [
            new("Work", mmu.ReadWram, mmu.WriteWram, 0x7e0000, 0x20000, 6, BpType.WramRead | BpType.WramWrite),
            new("Save", Mapper.ReadSram, Mapper.WriteSram, 0x0000, Mapper.Sram.Length,$"{Mapper.Sram.Length}".Length, BpType.SramWrite | BpType.SramRead),
            new("Video", Ppu.ReadByte, Ppu.WriteByte, 0x0000, 0x10000, 4, BpType.VramWrite | BpType.VramRead),
            new("Oam", Ppu.ReadOram,Ppu.WriteOram, 0x0000, 0x220, 3, BpType.OramWrite | BpType.OramRead),
            new("Color", Ppu.ReadCram ,Ppu.WriteCram, 0x0000, 0x200, 3, BpType.CramWrite | BpType.CramRead),
            new("Spc",Spc.ReadDebug, Spc.Write,0x0000, 0x10000, 4, BpType.SpcWrite | BpType.SpcRead),
        ];

        if (Mapper.Coprocessor == SnesMapper.Sa1)
            MemRegions.Add(new("Sa1", Sa1.Mmu.ReadIram, Sa1.WriteIram, 0x0000, 0x800, 3, BpType.Sa1Write | BpType.Sa1Read));
        //if (Mapper.Coprocessor == SnesMapper.Gsu)
        //    MemRegions.Add(new("Gsu", Sa1.Mmu.ReadIram, Sa1.WriteIram, 0x0000, 0x800, 3, BPType.WramWrite | BPType.WramRead));

        MemRegions.Add(new("Prg", mmu.ReadByte, Mapper.Write, 0x0000, Mapper.Rom.Length, 6, BpType.CodeExec));
        MemRegions.Add(new("Register", null, null, -1, -1, -1, 0));

    }

    public override void Draw(Texture2D texture)
    {
        base.Draw(texture);
        DrawDebugger(Cpu.PBPC, Logger.LogMain, SelectedCpu);
        DrawCoProcessors(Logger.LogSa1);
        //DrawStackInfo(Snes.Ram.AsSpan(0, 0x2000), Snes.Cpu.SP, 0x1fff, "cpu");
        DrawCartInfo(Mapper.GetCartInfo());
        DrawRegisters();
        DrawDmaInfo();
        DrawMemory();

#if DEBUG || DECKDEBUG
        DrawTestAddr([Cpu.TestAddr, Spc.TestAddr], testcpu);
#endif
    }

    public override void DrawButtons(bool logging, int processor) => base.DrawButtons(logging, processor);

    public override void DrawBreakpoints() => base.DrawBreakpoints();

    public override void DrawCpuInfo(ICpu cpu) =>
        base.DrawCpuInfo(cpu);

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

    public override void DrawStackInfo(Span<byte> data, int addr, int start, string name) => base.DrawStackInfo(data, addr, !Cpu.EmulationMode ? start : 0x1ff, name);

    public override void AddBreakpoint(int addr, BpType type, int condition, bool write)
    {
        if (addr == -1) return;
        var bp = Breakpoints.Find(b => b.Addr == addr && b.Type == type);
        if (bp == null)
        {
            Breakpoints.Add(new(addr, -1, type, write, true));
            SaveBreakpoints(GameName);
        }
        //base.AddBreakpoint(addr, type, condition, write);
    }

    public override void Continue(DebugState type)
    {
        if (SelectedCpu == SpcCpu)
        {
            SpcLogger.Log();
            Spc.Step();
        }
        else if (SelectedCpu == GsuCpu)
        {
            //Snes.SpcLogger.Log(Snes.Spc.PC);
            //SetState(DebugState.StepGsu);
            //Snes.Gsu.Exec(Snes.State,Snes.Debug);
        }
        Snes.Run = true;
        base.Continue(MainCpu);
    }

    public override void Reset(DebugState type)
    {
        Snes.Reset(GameName, true);
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
                SpcLogger.Log();
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
                var pc = Cpu.PBPC;
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
                var pc = Sa1.PBPC;
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
                SpcLogger.Log();
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
                Snes.EmuState = DebugState.Break;
                break;
            }
        }
    }

    public override void ToggleTrace(DebugState type)
    {
        switch (SelectedCpu)
        {
            case MainCpu: Logger.Toggle(false); break;
            case Sa1Cpu: Logger.Toggle(true); break;
            case SpcCpu: SpcLogger.Toggle(); break;
        }
    }

    public override void JumpTo(int i) => base.JumpTo(i);
}
