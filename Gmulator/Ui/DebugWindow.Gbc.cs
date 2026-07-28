using Gmulator.Core.Gbc;
using Gmulator.Interfaces;
using ImGuiNET;

namespace Gmulator.Ui
{
    internal class GbcDebugWindow : DebugWindow
    {
        private readonly Gbc Gbc;
        private new readonly GbcCpu Cpu;

        public GbcDebugWindow(Gbc gbc) : base(gbc)
        {
            Gbc = gbc;
            Cpu = gbc.Cpu;
            Breakpoints = gbc.Breakpoints;
            SaveBreakpoints = gbc.SaveBreakpoints;

            var mmu = gbc.Mmu;
            var mapper = mmu.Mapper;
            MemRegions =
            [
                new("Work", mmu.ReadWram, mmu.WriteWram, 0x0000, 0x8000, 4, BpType.WramWrite | BpType.WramRead, RamType.Wram),
                new("Save", mmu.ReadSram, null,0x0000, mapper.Sram.Length, 4, BpType.WramWrite | BpType.WramRead, RamType.Sram),
                new("Video", mmu.ReadVram, mmu.WriteVramBank, 0x0000, 0x4000, 4, BpType.VramWrite | BpType.VramRead, RamType.Vram),
                new("Sprite", mmu.ReadOam, null, 0x0000, 0x100, 2, BpType.WramWrite | BpType.WramRead, RamType.Wram),
                new("IO", mmu.ReadIo, mmu.WriteIo,0x0000, 0x80, 2, BpType.WramWrite | BpType.WramRead, RamType.Register),
                new("Hram", mmu.ReadHram, mmu.WriteHram,0x0000, 0x80, 2, BpType.WramWrite | BpType.WramRead, RamType.Wram),
                new("Rom", mapper.ReadRom, null, 0x0000, mapper.Rom.Length, 6, BpType.WramWrite | BpType.WramRead, RamType.Rom),
            ];

            Disassemble =
            [
                new(gbc.Logger.Disassemble,CpuType.Gbc),
            ];

            ScrollY = new int[Disassemble.Length];
            JumpAddr = new int[Disassemble.Length];

            GetCpuState = Cpu.GetRegisters;
            GetCpuFlags = Cpu.GetFlags;
            GetPpuState = Ppu.GetState;
            GetApuState = Gbc.Apu.GetState;
        }

        public override void Draw(Texture2D texture)
        {
            base.Draw(texture);
            base.DrawDebugger(Cpu.PC, Gbc.Logger.Logging, CpuType.Gbc);
            base.DrawRegisters();
            DrawCartInfo(Gbc.Mapper.GetInfo());
            DrawMemory();

            //ImGui.SetNextWindowPos(new(470, 272));
            //ImGui.SetNextWindowSize(new(405, 405));
            ImGui.Begin("Audio");
            {
                ImGui.Checkbox("Square 1", ref Gbc.Apu.Square1.Play);
                ImGui.Checkbox("Square 2", ref Gbc.Apu.Square2.Play);
                ImGui.Checkbox("Wave", ref Gbc.Apu.Wave.Play);
                ImGui.Checkbox("Noise", ref Gbc.Apu.Noise.Play);
                ImGui.End();
            }
        }

        public override void DrawBreakpoints(int index) => base.DrawBreakpoints(index);

        public override void DrawCpuInfo(ICpu cpu) =>
            base.DrawCpuInfo(cpu);

        public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

        public override void DrawMemory() => base.DrawMemory();

        public override void AddBreakpoint(int a, BpType type, RamType ramType, CpuType cpuType, int index, string access, int condition, bool write) => base.AddBreakpoint(a, type, ramType, cpuType, index, access, condition, write);

        public override void SetJumpAddress(object addr, int i) => base.SetJumpAddress(addr, i);

        public override void Reset() => base.Reset();

        public override void Continue() => base.Continue();

        public override void StepInto() => base.StepInto();

        public override void StepOver()
        {
            var Cpu = Gbc.Cpu;
            var pc = Cpu.PC;
            var inst = GbcCpu.OpInfo00[Gbc.Mmu.ReadByte(pc)];

            if (inst.Name == "call" || inst.Name == "rst")
            {
                Cpu.StepOverAddr = pc + inst.Size;
                SetState(DebugState.Running);
            }
            else
                StepInto();
        }

        public override void StepScanline() => base.StepScanline();

        public override void ToggleTrace() => Gbc.Logger.Toggle();
    }
}
