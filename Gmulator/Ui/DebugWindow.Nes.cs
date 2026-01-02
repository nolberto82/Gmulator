using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Nes.Mappers;
using Gmulator.Interfaces;
using ImGuiNET;
using Raylib_cs;
using System.Numerics;

namespace Gmulator.Ui
{
    internal class NesDebugWindow : DebugWindow
    {
        private readonly Nes Nes;
        private readonly NesCpu Cpu;
        private readonly NesPpu Ppu;
        private readonly NesLogger Logger;

        public NesDebugWindow(Nes nes, NesCpu cpu, NesPpu ppu, NesMmu mmu, NesLogger logger) : base(cpu, ppu)
        {
            Nes = nes;
            Breakpoints = nes.Breakpoints;
            Cpu = cpu;
            Ppu = ppu;
            Logger = logger;

            SetState = nes.SetState;
            SaveBreakpoints = nes.SaveBreakpoints;

            GameName = mmu.Mapper.Name ?? "";

            var mapper = mmu.Mapper;

            MemRegions =
            [
                new("Wram", mmu.ReadByte, mmu.WriteByte, 0x0000, mmu.Wram.Length, 4),
                new("Vram", ppu.Read, ppu.Write, 0x0000, ppu.Vram.Length, 4),
                new("Oram", ppu.ReadOam, null, 0x0000,ppu.Oram.Length, 2),
                new("Sram", mapper.ReadSram, mapper.WriteSram, 0x0000, mapper.Sram == null ? 0 : mapper.Sram.Length,  4),
                new("Prg", mapper.ReadPrg, mapper.WritePrg, 0x0000, mapper.PrgRom.Length,  6),
                new("Chr", mapper.ReadChr, mapper.Write, 0x0000, mapper.CharRom.Length,  6),
            ];

            RamNames = new()
            {
                new("Work", RamType.Wram), new("Save", RamType.Sram),
                new("Video", RamType.Vram), new("Sprites", RamType.Oram),
                new("Rom", RamType.Rom), new("Register", RamType.Register),
            };

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

        public override void Continue(DebugState type = 0)
        {
            Nes.SetState(DebugState.Running);
            base.Continue(0);
        }

        public override void Reset(DebugState type)
        {
            Nes.Reset("", true, Ppu.ScreenBuffer);
            base.Reset(type);
        }

        public override void StepInto(DebugState type) => base.StepInto(MainCpu);

        public override void StepOver(DebugState type)
        {
            var pc = Cpu.PC;
            var inst = Nes.Cpu.Disasm[Nes.Mmu.ReadByte(pc)];

            if (inst.Name == "jsr")
            {
                Cpu.StepOverAddr = pc + inst.Size;
                base.StepOver(MainCpu);
            }
            else
                StepInto(MainCpu);
        }

        public override void StepScanline(DebugState type) => base.StepScanline(MainCpu);

        public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

        public override void ToggleTrace(DebugState type) => Nes.Logger.Toggle();

        public override void JumpTo(int i) => base.JumpTo(i);
    }
}
