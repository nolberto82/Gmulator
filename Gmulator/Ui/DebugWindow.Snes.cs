using Gmulator.Core.Snes;
using Gmulator.Core.Snes.Gsu;
using Gmulator.Core.Snes.Sa1;
using Gmulator.Core.Snes.Spc;
using Gmulator.Interfaces;
using ImGuiNET;

namespace Gmulator.Ui;

internal class SnesDebugWindow : DebugWindow
{
    private readonly string[] testcpu = ["Cpu", "Spc", "Gsu"];
    private const int CpuNumbers = 4;
    private readonly Snes Snes;
    private new readonly SnesCpu Cpu;
    private new readonly SnesPpu Ppu;
    private readonly SnesSpc Spc;
    private readonly SnesDsp Dsp;
    private readonly SnesSa1 Sa1;
    private readonly SnesGsu Gsu;
    private readonly SnesLogger Logger;
    private readonly SnesSpcLogger SpcLogger;
    private readonly SnesGsuLogger GsuLogger;
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
        Gsu = snes.Gsu;
        Mapper = snes.Mapper;
        Dma = snes.Dma;
        Logger = snes.Logger;
        SpcLogger = snes.SpcLogger;
        GsuLogger = snes.GsuLogger;
        Breakpoints = snes.Breakpoints;
        ReadOp = snes.ReadOp;
        SaveBreakpoints = snes.SaveBreakpoints;
        CoProcessor = Mapper.Coprocessor;

        GameName = Mapper.Name ?? "";

        Disassemble =
        [
           new(Logger.Disassemble,CpuType.Snes),
           new(SpcLogger.Disassemble,CpuType.Spc),
           new(Logger.Disassemble,CpuType.Sa1),
           new(GsuLogger.Disassemble, CpuType.Gsu),
        ];

        ScrollY = new int[Disassemble.Length];
        JumpAddr = new int[Disassemble.Length];

        GetCpuState = Cpu.GetRegisters;
        GetCpuFlags = Cpu.GetFlags;
        GetPpuState = Ppu.GetState;
        GetApuState = SnesDsp.GetState;
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

        if (Gsu != null)
        {
            GetGsuState = Gsu!.GetRegisters;
            //GetGsuFlags = Gsu!.GetFlags;
            GetGsuIORegs = Gsu!.GetRegisterInfo;
        }

        SetMemoryDomains();
    }

    public void SetMemoryDomains()
    {
        var mmu = Snes.Mmu;

        MemRegions =
        [
            new("Work", mmu.ReadWram, mmu.WriteWram, 0x7e0000, 0x20000, 6, BpType.WramRead | BpType.WramWrite, RamType.Wram),
            new("Save", Mapper.ReadSram, Mapper.WriteSram, 0x0000, Mapper.Sram.Length,$"{Mapper.Sram.Length}".Length, BpType.SramWrite | BpType.SramRead, RamType.Sram),
            new("Video", Ppu.ReadByte, Ppu.WriteByte, 0x0000, 0x10000, 4, BpType.VramWrite | BpType.VramRead, RamType.Vram),
            new("Oam", Ppu.ReadOram,Ppu.WriteOram, 0x0000, 0x220, 3, BpType.OramWrite | BpType.OramRead, RamType.Oram),
            new("Color", Ppu.ReadCram ,Ppu.WriteCram, 0x0000, 0x200, 3, BpType.CramWrite | BpType.CramRead, RamType.Cram),
            new("Spc",Spc.ReadDebug, Spc.Write,0x0000, 0x10000, 4, BpType.SpcWrite | BpType.SpcRead, RamType.SpcRam),
        ];

        if (Mapper.Coprocessor == SnesMapper.Sa1)
            MemRegions.Add(new("Sa1", Sa1.Mmu.ReadIram, Sa1.WriteIram, 0x0000, 0x800, 3, BpType.Sa1Write | BpType.Sa1Read, RamType.Iram));
        if (Mapper.Coprocessor == SnesMapper.Gsu)
        {
            MemRegions[1] = new("Gsu", Gsu.Mmu.Read, Gsu.Mmu.Write, 0x0000, Mapper.RamSize, 4, BpType.GsuWrite | BpType.GsuRead, RamType.GsuRam);
            MemRegions.Add(new("Prg", Gsu.ReadPrg2, Gsu.WritePrg2, 0x0000, Mapper.Rom.Length, 6, BpType.CodeExec, RamType.Rom));
        }
        else
            MemRegions.Add(new("Prg", Mapper.Read, Mapper.Write, 0x0000, Mapper.Rom.Length, 6, BpType.CodeExec, RamType.Rom));
        MemRegions.Add(new("Register", null, null, -1, -1, -1, BpType.RegWrite | BpType.RegRead, RamType.Register));

    }

    public override void Draw(Texture2D texture)
    {
        base.Draw(texture);
        DrawDebugger(Cpu.PBPC, Logger.LogMain, CpuType.Snes);
        //DrawGsuInfo();
        //DrawStackInfo(Snes.Ram.AsSpan(0, 0x2000), Snes.Cpu.SP, 0x1fff, "cpu");
        DrawCartInfo(Mapper.GetCartInfo());
        DrawRegisters();
        DrawDmaInfo();
        DrawMemory();

#if DEBUG || DECKDEBUG
        //DrawTestAddr([Cpu.TestAddr, Spc.TestAddr], testcpu);
#endif
    }

    public override void DrawButtons(bool logging, CpuType processor) => base.DrawButtons(logging, processor);

    public override void DrawCpuInfo(ICpu cpu) =>
        base.DrawCpuInfo(cpu);

    public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

    public override void DrawMemory() => base.DrawMemory();

    public override void DrawDmaInfo()
    {
        //ImGui.SetNextWindowPos(new(559, 680));
        //ImGui.SetNextWindowSize(new(299, 291));
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

    private void DrawGsuInfo()
    {
        if (CoProcessor != SnesMapper.Gsu) return;
        ImGui.BeginChild("##cpuflags", new(0, 140));
        {

            ImGui.EndChild();
        }
    }

    public override void DrawStackInfo(Span<byte> data, int addr, int start, string name) => base.DrawStackInfo(data, addr, !Cpu.EmulationMode ? start : 0x1ff, name);

    public override void AddBreakpoint(int addr, BpType type, RamType ramType, CpuType cpuType, int index, string access, int condition, bool write) =>
        base.AddBreakpoint(addr, type, ramType, cpuType, index, access, condition, write);

    public override void Reset() => base.Reset();

    public override void Continue()
    {
        if (SelectedCpu == CpuType.Spc)
        {
            SpcLogger.Log();
            Spc.Step();
        }
        else if (SelectedCpu == CpuType.Gsu)
        {
            //Snes.SpcLogger.Log(Snes.Spc.PC);
            //SetState(DebugState.StepGsu);
            //Snes.Gsu.Exec(Snes.State,Snes.Debug);
        }

        //Snes?.Sa1?.DbgState = DebugState.Running;
        //Snes?.Gsu?.DbgState = DebugState.Running;
        Snes.DbgState = DebugState.Running;
        Snes.Run = true;
        base.Continue();
    }

    public override void StepInto()
    {
        Snes.Run = true;
        switch (SelectedCpu)
        {
            case CpuType.Sa1:
                break;
            case CpuType.Spc:
                SpcLogger.Log();
                break;
            case CpuType.Gsu:
                Snes.DbgState = DebugState.StepGsu;
                Snes.Gsu.Step(Gsu.Cycles + 1, true);
                Snes.DbgState = DebugState.Break;
                return;
        }
        base.StepInto();
    }

    public override void StepOver()
    {
        switch (SelectedCpu)
        {
            case CpuType.Snes:
            {
                var pc = Cpu.PBPC;
                var inst = Cpu.Disasm[ReadOp(pc)];
                if (inst.Name == "jsr" || inst.Name == "jsl")
                {
                    Cpu.StepOverAddr = pc + inst.Size;
                    Cpu.Step();
                    base.StepOver();
                }
                else
                    StepInto();
                Logger.Log(Ppu.HPos);
                break;
            }
            case CpuType.Sa1:
            {
                var pc = Sa1.PBPC;
                var inst = Sa1.Disasm[ReadOp(pc)];
                if (inst.Name == "jsr" || inst.Name == "jsl")
                {
                    Sa1.StepOverAddr = pc + inst.Size;
                    Sa1.Step();
                    base.StepOver();
                }
                else
                    StepInto();
                Logger.Log(Ppu.HPos);
                break;
            }
            case CpuType.Spc:
                StepInto();
                SpcLogger.Log();
                break;
        }
    }

    public override void StepScanline()
    {
        var oldline = Ppu.VPos;
        while (oldline == Ppu.VPos)
        {
            Cpu.Step();
        }
        Snes.DbgState = DebugState.Break;
    }

    public override void ToggleTrace()
    {
        switch (SelectedCpu)
        {
            case CpuType.Snes: Logger.Toggle(false); break;
            case CpuType.Spc: SpcLogger.Toggle(); break;
            case CpuType.Sa1: Logger.Toggle(true); break;
            case CpuType.Gsu: GsuLogger.Toggle(); break;
        }
    }

    public override void JumpTo(int i) => base.JumpTo(i);
}
