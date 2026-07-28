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
                new("Wram", mmu.ReadByte, mmu.WriteByte, 0x0000, mmu.Wram.Length, 4, BpType.WramWrite | BpType.WramRead, RamType.Wram),
                new("Vram", ppu.Read, ppu.Write, 0x0000, ppu.Vram.Length, 4, BpType.WramWrite | BpType.WramRead, RamType.Vram),
                new("Oram", ppu.ReadOam, null, 0x0000,ppu.Oram.Length, 2, BpType.WramWrite | BpType.WramRead, RamType.Oram),
                new("Sram", mapper.ReadSram, mapper.WriteSram, 0x0000, mapper.Sram == null ? 0 : mapper.Sram.Length,  4, BpType.WramWrite | BpType.WramRead, RamType.Sram),
                new("Prg", mapper.ReadPrg, mapper.WritePrg, 0x0000, mapper.PrgRom.Length,  6, BpType.WramWrite | BpType.WramRead, RamType.Rom),
                new("Chr", mapper.ReadChr, mapper.Write, 0x0000, mapper.CharRom.Length,  6, BpType.WramWrite | BpType.WramRead, RamType.Rom),
            ];

            Disassemble =
            [
                new(Logger.Disassemble,CpuType.Nes)
            ];

            ScrollY = new int[Disassemble.Length];
            JumpAddr = new int[Disassemble.Length];

            GetCpuState = Cpu.GetRegisters;
            GetCpuFlags = Cpu.GetFlags;
            GetPpuState = Ppu.GetState;
            GetApuState = Nes.Apu.GetState;
            GetPrg = () => Nes.Mapper.Prg;
            GetChr = () => Nes.Mapper.Chr;
        }

        public override void Draw(Texture2D texture)
        {
            base.Draw(texture);
            base.DrawDebugger(Nes.Cpu.PC, Logger.Logging, CpuType.Nes);
            DrawCartInfo(Nes.Mapper.GetInfo());
            base.DrawRegisters();
            DrawMemory();
        }

        public override void DrawBreakpoints(int index) => base.DrawBreakpoints(index);

        public override void DrawCpuInfo(ICpu cpu) =>
            base.DrawCpuInfo(cpu);

        public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

        public override void DrawMemory() => base.DrawMemory();

        public override void AddBreakpoint(int addr, BpType type,RamType ramType, CpuType cpuType, int index, string access, int condition, bool write) => 
            base.AddBreakpoint(addr, type, ramType, cpuType, index, access, condition, write);

        public override void Reset() => base.Reset();
        public override void Continue() => base.Continue();

        public override void StepInto() => base.StepInto();

        public override void StepOver()
        {
            var pc = Nes.Cpu.PC;
            var inst = Nes.Cpu.Disasm[Nes.Mmu.ReadByte(pc)];

            if (inst.Name == "jsr")
            {
                Cpu.StepOverAddr = pc + inst.Size;
                base.StepOver();
            }
            else
                StepInto();
        }

        public override void StepScanline() => base.StepScanline();

        public override void ToggleTrace() => Nes.Logger.Toggle();

        public override void JumpTo(int i) => base.JumpTo(i);
    }
}
