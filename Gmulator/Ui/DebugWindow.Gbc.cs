using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Interfaces;
using ImGuiNET;
using Raylib_cs;

namespace Gmulator.Ui
{
    internal class GbcDebugWindow : DebugWindow
    {
        private readonly Gbc Gbc;
        private readonly GbcCpu Cpu;

        public GbcDebugWindow(Gbc gbc) : base(gbc.Cpu, gbc.Ppu)
        {
            Gbc = gbc;
            Cpu = gbc.Cpu;
            Breakpoints = gbc.Breakpoints;
            SetState = gbc.SetState;
            SaveBreakpoints = gbc.SaveBreakpoints;

            var mmu = gbc.Mmu;
            var mapper = mmu.Mapper;
            MemRegions =
            [
                new("Work", mmu.ReadWram, mmu.WriteWram, 0x0000, 0x8000, 4),
                new("Video", mmu.ReadVram, mmu.WriteVramBank, 0x0000, 0x4000, 4),
                new("Save", mmu.ReadSram, null,0x0000, mapper.Sram.Length, 4),
                new("Sprite", mmu.ReadOam, null, 0x0000, 0x100, 2),
                new("IO", mmu.ReadIo, mmu.WriteIo,0x0000, 0x80, 2),
                new("Hram", mmu.ReadHram, mmu.WriteHram,0x0000, 0x80, 2),
                new("Rom", mapper.ReadRom, null, 0x0000, mapper.Rom.Length, 6),
            ];

            RamNames =
            [
                new("Work", RamType.Wram), new("Save", RamType.Sram),
                new("Video", RamType.Vram), new("Sprites", RamType.Oram),
                new("Rom", RamType.Rom), new("Register", RamType.Register),
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

            ImGui.SetNextWindowPos(new(558, 681));
            ImGui.Begin("Audio");
            {
                ImGui.Checkbox("Square 1", ref Gbc.Apu.Square1.Play);
                ImGui.Checkbox("Square 2", ref Gbc.Apu.Square2.Play);
                ImGui.Checkbox("Wave", ref Gbc.Apu.Wave.Play);
                ImGui.Checkbox("Noise", ref Gbc.Apu.Noise.Play);
                ImGui.End();
            }
        }

        public override void DrawDebugger(int pc, bool logging, int n) => base.DrawDebugger(pc, logging, n);

        public override void DrawBreakpoints() => base.DrawBreakpoints();

        public override void DrawCpuInfo(Func<List<RegisterInfo>> cpu, Func<List<RegisterInfo>> cpuflags) =>
            base.DrawCpuInfo(cpu, cpuflags);

        public override void DrawCartInfo(Dictionary<string, string> info) => base.DrawCartInfo(info);

        public override void DrawMemory() => base.DrawMemory();

        public override void AddBreakpoint(int a, int type, int condition, bool write, RamType index = 0) => base.AddBreakpoint(a, type, condition, write, index);

        public override void SetJumpAddress(object addr, int i) => base.SetJumpAddress(addr, i);

        public override void Continue(DebugState type = 0)
        {
            Cpu.Step();
            Gbc.SetState(DebugState.Running);
        }

        public override void StepInto(DebugState type)
        {
            base.StepInto(type);
        }

        public override void StepOver(DebugState type)
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
                StepInto(MainCpu);
        }

        public override void Reset(DebugState type = 0)
        {
            Gbc.Reset("", true, Ppu.ScreenBuffer);
            base.Reset(type);
        }

        public override void StepScanline(DebugState type) => base.StepScanline(type);

        public override bool ExecuteCheck(int a) => base.ExecuteCheck(a);

        public override void ToggleTrace(DebugState type)
        {
            Gbc.Logger.Toggle();
        }
    }
}
