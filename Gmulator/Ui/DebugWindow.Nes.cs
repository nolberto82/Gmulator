using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Nes.Mappers;
using ImGuiNET;
using Raylib_cs;
using System.Numerics;

namespace Gmulator.Ui
{
    internal class NesDebugWindow : DebugWindow
    {
        private Nes Nes;
        private NesCpu Cpu;
        private NesPpu Ppu;
        private NesLogger Logger;

        public NesDebugWindow(Nes nes, NesCpu cpu, NesPpu ppu, NesMmu mmu, NesLogger logger)
        {
            Nes = nes;
            Breakpoints = nes.Breakpoints;
            Cpu = cpu;
            Ppu = ppu;
            Logger = logger;

            SetState = nes.SetState;
            SaveBreakpoints = nes.SaveBreakpoints;

            GameName = mmu.Mapper.Name ?? "";

            MemRegions =
            [
                new("Wram", mmu.ReadWram, 0x0000, 4),
                new("Sram", mmu.ReadSram, 0x0000, 4),
                new("Vram", mmu.ReadVram, 0x0000, 4),
                new("Oam", mmu.ReadOram, 0x0000, 2),
                new("Prg", mmu.ReadPrg, 0x0000, 6),
                new("Chr", mmu.ReadChr, 0x0000, 6),
            ];

            RamNames =
            [
                "WRAM", "SRAM", "VRAM", "OAM",
                "ROM", "REG"
            ];

            OnDisassemble =
            [
                logger.Disassemble,
            ];

            GetCpuState = Cpu.GetRegisters;
            GetCpuFlags = Cpu.GetFlags;
            GetPpuState = Ppu.GetState;
            GetApuState = Nes.Apu.GetState;
            GetPrg = () => Nes.Mapper.Prg;
            GetChr = () => Nes.Mapper.Chr;

            ScrollY = [0];
            JumpAddr = [-1];
        }

        public override void Draw(Texture2D texture)
        {
            base.Draw(texture);
            DrawDebugger(Cpu.PC, Logger.Logging, MainCpu);
            DrawCartInfo(Nes.Mapper.GetInfo());
            base.DrawRegisters();
            DrawMemory();
        }

        public override void DrawDebugger(int pc, bool logging, int n) => base.DrawDebugger(pc, logging, n);

        public override void DrawBreakpoints() => base.DrawBreakpoints();

        public override void DrawCpuInfo(Func<List<RegisterInfo>> cpu, Func<List<RegisterInfo>> cpuflags) =>
            base.DrawCpuInfo(cpu, cpuflags);

        public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

        public override void DrawMemory() => base.DrawMemory();

        public override void AddBreakpoint(int a, int type, int condition, bool write, RamType index = 0) => base.AddBreakpoint(a, type, condition, write, index);

        public override void Continue(Action action = null, int scanline = 0, int type = 0)
        {
            Nes.SetState(DebugState.Running);
        }

        public override void Reset(Action action = null, int scanline = 0, int type = 0)
        {
            Nes.Reset("", true);
            base.Reset();
        }

        public override void StepInto(Action action = null, int scanline = 0, int type = 0)
        {
            SetState(DebugState.StepMain);
            base.StepInto();
        }

        public override void StepOver(Action action = null, int scanline = 0, int type = 0) => base.StepOver();

        public override void StepScanline(Action action, int scanline, int type)
        {
            var oldline = Ppu.Scanline;
            while (oldline == Ppu.Scanline)
                action();
            SetState(DebugState.Break);
        }

        public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

        public override void ToggleTrace(Action action = null, int scanline = 0, int type = 0) => Nes.Logger.Toggle();

        public override void JumpTo(int i) => base.JumpTo(i);
    }
}
