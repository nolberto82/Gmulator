using GBoy.Core;
using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using ImGuiExtra;
using ImGuiNET;
using System.Collections;
using System.Dynamic;
using System.Numerics;

namespace Gmulator;
public class DebugWindow
{
    public const int DisasmMaxLines = 14;
    public const int BreakpointsMaxLines = 10;

    public bool FollowPc { get; private set; }
    public int AsmOffset { get; private set; }
    public int JumpAddr { get; private set; } = -1;
    private int ScrollY;

    public Action<int> SetState;
    public Func<int, byte> Read;
    public Action<int, int> Write;
    public Action CpuStep;
    public delegate void Reset(string name, string lastname);
    public event Reset OnReset;
    public delegate void LogToggle(bool log = true);
    public event LogToggle OnToggle;
    public delegate DisasmEntry Disassemble(int pc, bool get_registers, bool gamedoctor = false);
    public event Disassemble OnDisassemble;
    private int System;

    public Emulator Emu { get; }
    public Dictionary<int, Breakpoint> Breakpoints { get; private set; }

    public int RamType;
    public const int VramB = 1;
    public const int WramB = 2;

    private MemoryEditor MemoryEditor;

    private string JumpAddress;
    private string BPAddress;
    private string BPCondition;
    public int ScrollOffset;
    public int MinOffset;
    public int MaxOffset;

    public DebugWindow(Emulator emu, Dictionary<int, Breakpoint> bps, int system)
    {
        Emu = emu;
        Breakpoints = bps;
        System = system;
        JumpAddress = "";
        BPAddress = "";
        BPCondition = "";

        MemoryEditor = new()
        {
            ReadFn = ReadMem,
            WriteFn = WriteMem,
            OptAddrDigitsCount = 4,
        };
    }

    public void Render(Dictionary<int, Breakpoint> Breakpoints, bool logging)
    {
        var PC = Emu.GetPc<int>();
        bool romloaded = Emu.GetConsole().Mapper != null;
        if (ImGui.BeginChild("##goto", new(-1, 80), ImGuiChildFlags.FrameStyle))
        {
            if (ImGui.Button("Run", ButtonSize))
            {
                StepInto();
                SetState(Running);
                JumpAddr = -1;
                AsmOffset = 0;
                FollowPc = true;
            }

            ImGui.SameLine();

            if (ImGui.Button("Over", ButtonSize) || ImGui.IsKeyPressed(ImGuiKey.F8))
                StepOver();

            ImGui.SameLine();

            if (ImGui.Button("Into", ButtonSize) || ImGui.IsKeyPressed(ImGuiKey.F7))
                StepInto();

            if (ImGui.Button("1 Line", ButtonSize) || ImGui.IsKeyPressed(ImGuiKey.F6))
                StepScanline();

            ImGui.SameLine();

            if (ImGui.Button("Reset", ButtonSize))
            {
                OnReset("", "");
                FollowPc = true;
            }

            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Button, logging ? 0xff00ff00 : 0xff0000ff);
            if (ImGui.Button("Trace", ButtonSize))
                OnToggle();
            ImGui.PopStyleColor();

            if (ImGui.Button("Goto", new Vector2(ButtonSize.X, 0)))
                SetJumpAddress(JumpAddress);

            ImGui.SameLine();
            ImGui.PushItemWidth(-1);
            ImGui.InputText($"##bpinput", ref JumpAddress, 4, HexInputFlags);
            ImGui.PopItemWidth();
            ImGui.EndChild();
        }

        if (ImGui.GetWindowPos().Y < 20)
            ImGui.SetWindowPos(new(ImGui.GetWindowPos().X, 20));

        if (FollowPc)
        {
            ScrollY = 0;
            JumpAddr = -1;
        }

        var jump = JumpAddr > -1 ? (ushort)JumpAddr : PC + ScrollY;
        ushort pc = (ushort)(!FollowPc ? (ushort)(jump + AsmOffset) : jump);

        float mousewheel = ImGui.GetIO().MouseWheel;
        if (mousewheel != 0)
        {
            if (ImGui.IsWindowHovered())
            {
                FollowPc = false;
                if (mousewheel > 0)
                    ScrollY -= 3;
                else if (mousewheel < 0)
                    ScrollY += 3;
            }
        }

        for (int i = 0; i < DisasmMaxLines; i++)
        {
            ImGui.PushID(pc);
            DisasmEntry e = OnDisassemble(pc, false);

            if (e.pc == PC)
                if (FollowPc)
                    ImGui.SetScrollHereY(0.25f);

            if (ImGui.Selectable($"{pc:X4} {e.disasm}", pc == PC, ImGuiSelectableFlags.AllowDoubleClick))
            {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    if (!Breakpoints.ContainsKey(pc))
                        Breakpoints.Add(e.pc, new(e.pc, -1, BPType.Exec, true));
                    else
                        Breakpoints.Remove(pc);
                }
            }

            if (Breakpoints.ContainsKey(pc))
            {
                var bp = Breakpoints[pc];
                if (bp.Enabled && (bp.Type & BPType.Exec) != 0)
                    DrawRect(0x4000ff00, 0xff00ff00);
                else
                    DrawRect(0x000000ff, 0xff0000ff);
            }

            if (pc == PC)
                DrawRect(0x6000ffff, 0xff00ffff);

            pc += (ushort)e.size;
        }
    }

    public void RenderCpuInfo()
    {
        var Cpu = Emu.GetConsole().Cpu;
        ImGui.Text($"FPS {ImGui.GetIO().Framerate}");
        ImGui.Separator();
        ImGui.BeginGroup();
        {
            foreach (var e in Cpu.GetRegs())
                ImGui.Text($"{e.Key} {e.Value:X2}");
        }
        ImGui.EndGroup();

        ImGui.BeginGroup();
        {
            foreach (var e in Cpu.GetFlags())
            {
                bool v = e.Value;
                ImGui.Checkbox(e.Key, ref v);
                if (e.Key != "D")
                    ImGui.SameLine();
            }
        }
        ImGui.EndGroup();
    }

    public void RenderBreakpoints()
    {
        var open = true;
        ImGui.SetNextWindowSize(new(100, 155));
        if (ImGui.BeginPopupModal("bpmenu", ref open, NoScrollFlags))
        {
            ImGui.PushItemWidth(-1);
            ImGui.InputText($"##bpinput2", ref BPAddress, 4, HexInputFlags);
            ImGui.PopItemWidth();
            //if (ImGui.Button("Add Exec", new(-1, 0)))
            //{
            //    InsertRemove(BPAddress, BPType.Exec, true);
            //    ImGui.CloseCurrentPopup();
            //}
            //if (ImGui.Button("Add Write", new(-1, 0)))
            //{
            //    InsertRemove(BPAddress, BPType.Write, true);
            //    ImGui.CloseCurrentPopup();
            //}
            //if (ImGui.Button("Add Read", new(-1, 0)))
            //{
            //    InsertRemove(BPAddress, BPType.Read, true);
            //    ImGui.CloseCurrentPopup();
            //}
            ImGui.Separator();
            if (ImGui.Button("Close", new(-1, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGui.BeginGroup();
        {
            ImGui.BeginChild("bplist", new(-1, -20));
            foreach (var item in Breakpoints)
            {
                var bp = item.Value;
                if (ImGui.Button($"{bp.Addr:X4}"))
                    SetJumpAddress(bp.Addr);

                ImGui.SameLine();

                ImGui.PushID(item.Key);
                bool enabled = bp.Enabled;
                ImGui.Checkbox("E", ref enabled);
                bp.Enabled = enabled;
                ImGui.SameLine();
                Checkbox("X", (bp.Type & BPType.Exec) > 0, ref bp, BPType.Exec);
                ImGui.SameLine();
                Checkbox("W", (bp.Type & BPType.Write) > 0, ref bp, BPType.Write);
                ImGui.SameLine();
                Checkbox("R", (bp.Type & BPType.Read) > 0, ref bp, BPType.Read);
                ImGui.SameLine();

                if (ImGui.Button("x"))
                {
                    if (Breakpoints.ContainsKey(bp.Addr))
                        Breakpoints.Remove(bp.Addr);
                }
            }
            ImGui.EndChild();
        }

        ImGui.BeginGroup();
        {
            if (ImGui.Button("Add Breakpoint", new(-1, 20)))
                ImGui.OpenPopup("bpmenu");
        }
        ImGui.EndGroup();
    }

    public void RenderMapperBanks(byte[] Prg, byte[] Chr)
    {
        ImGui.BeginGroup();
        ImGui.TextColored(GREEN, "Prg");
        for (int i = 0; i < Prg.Length; i++)
        {
            ImGui.Text($"{Prg[i]:X2}");
            ImGui.SameLine();
        }
        ImGui.EndGroup();
        ImGui.Separator();
        ImGui.BeginGroup();
        ImGui.TextColored(GREEN, "Chr");
        for (int i = 0; i < Chr.Length; i++)
        {
            ImGui.Text($"{Chr[i]:X2}");
            ImGui.SameLine();
        }
        ImGui.EndGroup();
    }

    public unsafe void RenderMemory()
    {
        var Ram = Emu.GetRam<byte[]>();
        MemoryEditor.DrawContents(Ram, Ram.Length, 0x0000);
    }

    public void StepInto()
    {
        CpuStep();
        AsmOffset = 0;
        JumpAddr = -1;
        Program.State = Stepping;
        FollowPc = true;
    }

    public void StepOver()
    {
        //byte op = ReadByte(Cpu.PC);
        //if (opInfo00[op].Name == "jsr")
        //{
        //    Cpu.StepOverAddr = (ushort)(Cpu.PC + Cpu.opInfo00[op].Size);
        //    StepInto();
        //    OnSetState(Running);
        //}
        //else
        //{
        //    if (op == 0x76)
        //    {
        //        while (op == 0x76)
        //        {
        //            StepInto();
        //            op = ReadByte(Cpu.PC);
        //        }
        //    }
        //    else
        //        StepInto();
        //}
    }

    public void StepScanline()
    {
        //if (!Mmu.Emu.RomLoaded) return;
        //int oldscanline = Ppu.Scanline;
        //while (oldscanline == Ppu.Scanline)
        //    StepInto();
    }

    public byte ReadMem(int a)
    {
        return Read(a);
    }

    public void WriteMem(int a, byte v)
    {
        Write(a, v);
    }

    public void SetJumpAddress(dynamic addr)
    {
        if (addr.GetType() == typeof(string) && addr == "") return;
        if (addr.GetType() == typeof(string))
            JumpAddr = Convert.ToUInt16(addr, 16);
        else
            JumpAddr = addr;
        AsmOffset = 0;
    }

    public Action OnBreakpointHit;
    public void Check(int a)
    {
        if (Breakpoints.TryGetValue(a, out Breakpoint bp))
        {
            if (bp.Enabled && a == bp.Addr)
                OnBreakpointHit();
        }
    }

    public void Check(int a, byte v)
    {
        if (Breakpoints.TryGetValue(a, out Breakpoint bp))
        {
            if (bp.Enabled && a == bp.Addr && (bp.Condition == -1 || bp.Condition == v))
                OnBreakpointHit();
        }
    }
}