using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using ImGuiNET;
using Raylib_cs;

namespace Gmulator.Ui
{
    internal class GbcDebugWindow : DebugWindow
    {
        private readonly Gbc Gbc;
        private readonly GbcCpu Cpu;
        private readonly GbcPpu Ppu;
        private readonly GbcIO IO;

        public GbcDebugWindow(Gbc gbc)
        {
            Gbc = gbc;
            Cpu = gbc.Cpu;
            Ppu = gbc.Ppu;
            IO = gbc.IO;
            Breakpoints = gbc.Breakpoints;
            SetState = gbc.SetState;
            SaveBreakpoints = gbc.SaveBreakpoints;

            MemRegions =
            [
                new("Wram",gbc.Mmu.ReadWram, 0x0000, 4),
                new("Vram",gbc.Mmu.ReadVram,0x0000, 4),
                new("Sram",gbc.Mmu.ReadSram,0x0000, 4),
                new("Oram",gbc.Mmu.ReadOram, 0xfe00,2),
                new("Rom",gbc.Mmu.ReadRom, 0x000000,6),
            ];

            RamNames =
            [
                "WRAM", "SRAM", "VRAM", "OAM",
                "ROM", "REG"
            ];

            OnDisassemble =
            [
                gbc.Logger.Disassemble,
            ];

            GetCpuState = Cpu.GetRegisters;
            GetCpuFlags = Cpu.GetFlags;
            GetPpuState = Ppu.GetState;
            GetApuState = Gbc.Apu.GetState;

            ScrollY = [0];
            JumpAddr = [-1];
        }

        public override void Draw(Texture2D texture)
        {
            base.Draw(texture);
            DrawDebugger(Cpu.PC, Gbc.Logger.Logging, MainCpu);
            base.DrawRegisters();
            DrawCartInfo(Gbc.Mapper.GetInfo());
            DrawMemory();
        }

        public override void DrawDebugger(int pc, bool logging, int n) => base.DrawDebugger(pc, logging, n);

        public override void DrawBreakpoints() => base.DrawBreakpoints();

        public override void DrawCpuInfo(Func<List<RegisterInfo>> cpu, Func<List<RegisterInfo>> cpuflags) => 
            base.DrawCpuInfo(cpu, cpuflags);

        public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

        public override void DrawMemory() => base.DrawMemory();

        public override void AddBreakpoint(int a, int type, int condition, bool write, RamType index = 0) => base.AddBreakpoint(a, type, condition, write, index);

        public override void SetJumpAddress(object addr, int i) => base.SetJumpAddress(addr, i);

        public override void Continue(Action Step, int scanline, int type = 0)
        {
            Gbc.Cpu.Step();
            Gbc.SetState(DebugState.Running);
        }

        public override void StepInto(Action step = null, int scanline = 0, int type = 0)
        {
            SetState(DebugState.Break);
            base.StepInto(step, 0);
        }

        public override void StepOver(Action Step, int scanline, int type = 0)
        {
            var Cpu = Gbc.Cpu;
            var pc = Cpu.PC;
            var inst = GbcCpu.OpInfo00[Gbc.Mmu.Read(pc)];

            if (inst.Name == "call" || inst.Name == "rst")
            {
                Gbc.Cpu.StepOverAddr = pc + inst.Size;
                Cpu.Step();
                SetState(DebugState.Running);
            }
            else
                StepInto();
        }

        public override void Reset(Action reset, int scanline, int type = 0)
        {
            Gbc.Reset("", true);
            base.Reset(reset, type);
        }

        public override void StepScanline(Action action, int scanline, int type = 0)
        {
            base.StepScanline(action, Gbc.IO.LY, type);
        }

        public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

        public override void ToggleTrace(Action action = null, int scanline = 0, int type = 0)
        {
            Gbc.Logger.Toggle();
        }
    }
}
