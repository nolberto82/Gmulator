using Gmulator.Core.Nes;
using Gmulator.Interfaces;

namespace Gmulator.Ui
{
    internal class NesDebugWindow : DebugWindow
    {
        private readonly Nes Nes;
        private readonly NesLogger Logger;

        public NesDebugWindow(Nes nes) : base(nes)
        {
            Nes = nes;
            Breakpoints = nes.Breakpoints;
            Logger = nes.Logger;

            SaveBreakpoints = nes.SaveBreakpoints;

            GameName = nes.Mmu.Mapper.Name ?? "";
            var mapper = nes.Mmu.Mapper;
            var mmu = nes.Mmu;
            var ppu = nes.Ppu;

            MemRegions =
            [
                new("Wram", mmu.ReadByte, mmu.WriteByte, 0x0000, mmu.Wram.Length, 4, 0),
                new("Vram", ppu.Read, ppu.Write, 0x0000, ppu.Vram.Length, 4, 1),
                new("Oram", ppu.ReadOam, null, 0x0000,ppu.Oram.Length, 2, 2),
                new("Sram", mapper.ReadSram, mapper.WriteSram, 0x0000, mapper.Sram == null ? 0 : mapper.Sram.Length,  4, 3),
                new("Prg", mapper.ReadPrg, mapper.WritePrg, 0x0000, mapper.PrgRom.Length,  6, 4),
                new("Chr", mapper.ReadChr, mapper.Write, 0x0000, mapper.CharRom.Length,  6, 5),
            ];

            OnDisassemble =
            [
                Logger.Disassemble,
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
            base.DrawDebugger(Nes.Cpu.PC, Logger.Logging, MainCpu);
            DrawCartInfo(Nes.Mapper.GetInfo());
            base.DrawRegisters();
            DrawMemory();
        }

        public override void DrawBreakpoints() => base.DrawBreakpoints();

        public override void DrawCpuInfo(ICpu cpu) =>
            base.DrawCpuInfo(cpu);

        public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

        public override void DrawMemory() => base.DrawMemory();

        public override void AddBreakpoint(int a, int type, int condition, bool write, int index = 0) => base.AddBreakpoint(a, type, condition, write, index);

        public override void Continue(DebugState type = 0) => base.Continue(0);

        public override void Reset(DebugState type)
        {
            Nes.Reset("", true, Ppu.ScreenBuffer);
            base.Reset(type);
        }

        public override void StepInto(DebugState type) => base.StepInto(MainCpu);

        public override void StepOver(DebugState type)
        {
            var pc = Nes.Cpu.PC;
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

        public override void ToggleTrace(DebugState type) => Nes.Logger.Toggle();

        public override void JumpTo(int i) => base.JumpTo(i);
    }
}
