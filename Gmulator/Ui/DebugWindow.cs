using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using Gmulator.Core.Snes.Mappers;
using Gmulator.Interfaces;
using ImGuiNET;
using Raylib_cs;
using System;
using System.Net.Sockets;
using static Gmulator.Shared.MemoryEditor;

namespace Gmulator.Ui
{
    public abstract class DebugWindow
    {
        private int itemindex;
        private string BpAddr = "";
        private string GotoAddr = "";
        private string BPCondition = "";
        public string GameName { get; set; } = "";
        public int StepOverAddr = -1;
        public bool IsScreenWindow { get; private set; } = true;
        public bool FollowPc { get; private set; } = true;
        public bool IsSpc { get; private set; }
        public bool? ShowSa1 { get; set; } = false;
        public bool? ShowSpc { get; set; } = true;
        public bool[] BreakTypes { get; private set; } = [false, false, false];
        public List<ButtonName> ButtonNames { get; set; }
        public Dictionary<string, Action<bool>> ButtonNamesSa1 { get; set; }
        public List<MemRegion> MemRegions { get; set; } = [];
        public List<RamName> RamNames { get; set; }
        public int AsmOffset { get; private set; }
        public int[] JumpAddr { get; set; }
        public int[] ScrollY { get; set; }
        public int SelectedCpu { get; set; }
        public SortedDictionary<int, Breakpoint> Breakpoints { get; set; }
        public MemoryEditor MemoryEditor { get; set; }
        public Func<int, bool, bool, bool, (string, int, int)>[] OnDisassemble { get; set; }
        public Action CpuStep { get; set; }
        public Action<DebugState> SetState { get; set; }
        public Action<string> SaveBreakpoints { get; set; }
        public Func<List<RegisterInfo>> GetCpuState { get; set; }
        public Func<List<RegisterInfo>> GetCpuFlags { get; set; }
        public Func<List<RegisterInfo>> GetSa1State { get; set; }
        public Func<List<RegisterInfo>> GetSa1Flags { get; set; }
        public Func<List<RegisterInfo>> GetPpuState { get; set; }
        public Func<List<RegisterInfo>> GetApuState { get; set; }
        public Func<List<RegisterInfo>> GetSpcState { get; set; }
        public Func<List<RegisterInfo>> GetSpcFlags { get; set; }
        public Func<List<RegisterInfo>> GetPortState { get; set; }
        public Func<int> GetSpcPC { get; set; }
        public Func<int[]> GetPrg { get; set; }
        public Func<int[]> GetChr { get; set; }
        public IPpu Ppu { get; set; }

        public const int MainCpu = 0;
        public const int Sa1Cpu = 1;
        public const int SpcCpu = 2;
        public const int GsuCpu = 3;

        public DebugWindow(ICpu cpu, IPpu ppu)
        {
            CpuStep = cpu.Step;
            Ppu = ppu;
            ButtonNames =
            [
                new("Run", Continue),
                new("Reset",Reset),
                new("Step", StepInto),
                new("Over", StepOver),
                new("Line", StepScanline),
                new("Trace", ToggleTrace),
            ];

            MemoryEditor = new(AddBreakpoint);
        }

        public virtual void SetCpu(Snes snes) { }

        public virtual void Draw(Texture2D texture)
        {
            ImGui.SetNextWindowPos(new(5, 30));
            ImGui.SetNextWindowSize(new(256, 240));
            ImGui.Begin("Screen");
            {
                if (ImGui.IsWindowFocused())
                    IsScreenWindow = true;
                ImGui.Image((nint)texture.Id, ImGui.GetContentRegionAvail());
                Notifications.RenderDebug();
                ImGui.End();
            }
        }

        public virtual void DrawDebugger(int PC, bool logging, int n)
        {
            ImGui.SetNextWindowPos(new(5, 272));
            ImGui.SetNextWindowSize(new(550, 405));
            ImGui.Begin("Debugger");
            {
                Func<List<RegisterInfo>> cpu = null;
                Func<List<RegisterInfo>> cpuflags = null;
                DrawButtons(logging, n);
                ImGui.BeginTabBar("##cputab");
                {
                    ImGui.Columns(2);
                    ImGui.SetColumnWidth(0, 200);
                    if (ImGui.BeginTabItem("Cpu"))
                    {
                        SelectedCpu = MainCpu;
                        cpu = GetCpuState;
                        cpuflags = GetCpuFlags;
                        DrawDisassembly(PC, logging, false, MainCpu);
                        ImGui.EndTabItem();
                    }

                    if (GetSa1State != null)
                    {
                        if (ImGui.BeginTabItem("Sa1"))
                        {
                            SelectedCpu = Sa1Cpu;
                            cpu = GetSa1State;
                            cpuflags = GetSa1Flags;
                            DrawDisassembly(PC, logging, true, Sa1Cpu);
                            ImGui.EndTabItem();
                        }
                    }

                    if (GetSpcPC != null && ImGui.BeginTabItem("Spc"))
                    {
                        SelectedCpu = SpcCpu;
                        cpu = () => GetSpcState();
                        cpuflags = () => GetSpcFlags();
                        DrawDisassembly(GetSpcPC(), logging, false, SpcCpu);
                        ImGui.EndTabItem();
                    }
                    //if (CoProcessor == BaseMapper.CoprocessorGsu)
                    //{
                    //    if (ImGui.BeginTabItem("Gsu"))
                    //    {
                    //        //base.DrawDisassembly(Snes.Gsu.PC, GsuCpu);
                    //        ImGui.EndTabItem();
                    //    }
                    //}

                    ImGui.NextColumn();
                    DrawCpuInfo(cpu, cpuflags);
                    DrawBreakpoints();
                    ImGui.Columns(1);

                    ImGui.EndTabBar();
                }
                ImGui.End();
            }
        }

        private void DrawDisassembly(int PC, bool logging, bool isSa1, int n)
        {
            ImGui.BeginChild("Disassembly");
            {
                var pc = Scroll(PC, 0);

                if (ImGui.BeginPopupContextWindow("gotomenu"))
                    JumpTo(0);

                if (ImGui.IsKeyPressed(ImGuiKey.F5))
                    SetState(DebugState.Running);

                for (int i = 0; i < DisasmMaxLines; i++)
                {
                    var (disasm, op, size) = OnDisassemble[n](pc, false, false, isSa1);

                    ImGui.PushID(pc);
                    if (ImGui.Selectable($"{pc:X6} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            AddBreakpoint(pc, BPType.Exec, -1, false, RamType.Rom);
                    }

                    DrawHighlight(PC, pc);

                    ImGui.PopID();
                    ImGui.SameLine();
                    ImGui.Text($"{disasm}");
                    pc += size;
                }
                ImGui.EndChild();
            }
        }

        public virtual void DrawButtons(bool logging, int processor)
        {
            ImGui.BeginChild("##Buttons", new(0, 25));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Name, ButtonSize))
                        v.e.Action(0);
                    ImGui.SameLine();
                    ImGui.PopStyleColor();
                }
                ImGui.EndChild();
            }
        }

        public virtual void DrawCoProcessors() { }
        public virtual void DrawCpuInfo(Func<List<RegisterInfo>> cpu, Func<List<RegisterInfo>> cpuflags)
        {
            ImGui.BeginChild("Cpu Registers", new(70, 140));
            {
                var registers = cpu();
                for (int i = 0; i < registers.Count; i++)
                {
                    var v = registers[i];
                    ImGui.Text($"{v.Name}"); ImGui.SameLine();
                    ImGui.TextColored(GREEN, $"{v.Value}");
                }
                ImGui.EndChild();
            }
            ImGui.SameLine();
            ImGui.BeginChild("##cpuflags", new(0, 140));
            {
                var flags = cpuflags();
                for (int i = 0; i < flags.Count; i++)
                {
                    var v = flags[i];
                    Checkbox(v.Name, Convert.ToBoolean(v.Value));
                    if ((i + 1) % 2 > 0)
                        ImGui.SameLine();
                }
                ImGui.EndChild();
            }
        }

        public virtual void DrawStackInfo(Span<byte> data, int addr, int start, string name)
        {
            if (ImGui.Begin($"##winstack{name}"))
            {
                ImGui.BeginTable($"##stack{name}", 2);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                for (int i = data.Length - 1; i >= 0; i -= 2)
                {
                    TableRow($"{i:X4}", $"{data[i] | data[i - 1] << 8:X4}");
                }
                ImGui.EndTable();
                ImGui.End();
            }
        }

        public virtual void DrawCartInfo(Dictionary<string, string> info)
        {
            ImGui.SetNextWindowPos(new(266, 30));
            ImGui.SetNextWindowSize(new(289, 240));
            ImGui.Begin("Cartridge");
            {
                var v = info;
                if (ImGui.BeginTable("##cartinfotable", 2, ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                    for (int i = 0; i < v.Count; i++)
                    {
                        TableRow(v.ElementAt(i).Key, v.ElementAt(i).Value);
                    }
                    ImGui.EndTable();
                }
            }

            DrawMapperBanks();

            ImGui.End();
        }

        public virtual void DrawMapperBanks()
        {
            if (GetPrg == null || GetChr == null) return;
            ImGui.BeginChild("Banks");
            ImGui.Columns(2);
            ImGui.SetColumnWidth(0, 108);
            ImGui.SeparatorText("Prg");
            var Prg = GetPrg();
            for (int i = 0; i < Prg?.Length; i++)
            {
                ImGui.Text($"{i:X2}"); ImGui.SameLine();
                ImGui.TextColored(GREEN, $"{Prg[i]:X2}");
                if ((i + 1) % 2 != 0)
                    ImGui.SameLine();
            }
            ImGui.NextColumn();

            ImGui.SeparatorText("Chr");
            var Chr = GetChr();
            for (int i = 0; i < Chr?.Length; i++)
            {
                ImGui.Text($"{i:X2}"); ImGui.SameLine();
                ImGui.TextColored(GREEN, $"{Chr[i]:X2}");
                if ((i + 1) % 2 != 0)
                    ImGui.SameLine();
            }
            ImGui.Columns(1);
            ImGui.EndChild();
        }

        public virtual void DrawDmaInfo() { }

        public virtual void Continue(DebugState type) => FollowPc = true;
        public virtual void StepInto(DebugState type)
        {
            SetState(type);
            FollowPc = true;
        }

        public virtual void StepOver(DebugState type)
        {
            SetState(DebugState.Running);
            FollowPc = true;
        }

        public virtual void StepScanline(DebugState type)
        {
            var oldline = Ppu.GetScanline();
            while (oldline == Ppu.GetScanline())
                CpuStep();
            SetState(DebugState.Break);
            FollowPc = true;
        }

        public virtual void Reset(DebugState type) => FollowPc = true;
        public virtual void ToggleTrace(DebugState type)
        {

        }

        public virtual void SetJumpAddress(object addr, int i)
        {
            if (addr.GetType() == typeof(string) && addr.ToString() == "") return;
            if (addr.GetType() == typeof(string))
            {
                if (int.TryParse(addr.ToString(), System.Globalization.NumberStyles.HexNumber, null, out var res))
                    JumpAddr[i] = res;
            }
            else
                JumpAddr[i] = (int)addr;
            AsmOffset = 0;
            ScrollY[i] = 0;
            FollowPc = false;
        }

        public virtual void DrawMemory()
        {
            ImGui.SetNextWindowPos(new(5, 680));
            ImGui.SetNextWindowSize(new(550, 295));
            ImGui.Begin("Memory", NoScrollFlags);
            foreach (var n in MemRegions)
            {
                if (ImGui.BeginTabBar("memregions"))
                {
                    if (ImGui.BeginTabItem(n.Name))
                    {
                        MemoryEditor.ReadFn = n.Read;
                        MemoryEditor.WriteFn = n.Write;
                        MemoryEditor.OptAddrDigitsCount = n.AddrLength;
                        MemoryEditor.DrawContents(null, n.Size, n.StartAddr);
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }

        public virtual void DrawBreakpoints()
        {
            ImGui.SeparatorText("Breakpoints");
            if (ImGui.BeginChild("Breakpoints"))
            {
                const int columns = 4;
                Breakpoint cbp = null;
                if (ImGui.BeginTable("##bptable", columns, ImGuiTableFlags.RowBg))
                {
                    for (int i = 0; i < columns; i++)
                    {
                        if (i < 2)
                            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                        else
                            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 90);
                    }

                    ImGui.TableNextColumn();
                    List<Breakpoint> list = [.. Breakpoints.Values];
                    for (int i = 0; i < list.Count; i++)
                    {
                        Breakpoint bp = list[i];
                        cbp = bp;
                        var types = (bp.Type & BPType.Exec) > 0 ? "X" : ".";
                        types += (bp.Type & BPType.Write) > 0 ? "W" : ".";
                        types += (bp.Type & BPType.Read) > 0 ? "R" : ".";
                        if (ImGui.Button($"{bp.Addr:X6}"))
                            SetJumpAddress(bp.Addr, 0);
                        ImGui.TableNextColumn();
                        ImGui.PushID(bp.Addr);
                        bool enabled = bp.Enabled;
                        if (ImGui.Checkbox("", ref enabled))
                        {
                            bp.Enabled = enabled;
                            SaveBreakpoints(GameName);
                        }

                        var condition = bp.Condition > -1 ? $"{bp.Condition:X4}" : "    ";
                        var name = RamNames.FirstOrDefault(x => (int)x.Type == (int)bp.RamType).Name;
                        var text = $"{types} {name} {condition}";
                        ImGui.TableNextColumn();
                        if (ImGui.Selectable(text, false, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                BpAddr = $"{bp.Addr:X6}";
                                BPCondition = bp.Condition > -1 ? $"{bp.Condition}" : "";
                                BreakTypes[0] = (bp.Type & BPType.Exec) > 0;
                                BreakTypes[1] = (bp.Type & BPType.Write) > 0;
                                BreakTypes[2] = (bp.Type & BPType.Read) > 0;
                                itemindex = (int)bp.RamType;
                                ImGui.OpenPopup("Edit Breakpoint");
                            }
                        }

                        ImGui.SetNextWindowSize(new(0, 0));
                        if (ImGui.BeginPopupModal("Edit Breakpoint"))
                            DrawBpMenu(bp, true);
                        ImGui.TableNextColumn();

                        if (ImGui.Button("x"))
                        {
                            AddBreakpoint(bp.Addr, bp.Type, bp.Condition, bp.Write);
                            ImGui.PopID();
                            break;
                        }

                        ImGui.TableNextColumn();
                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.IsWindowHovered())
                    ImGui.OpenPopup("Add Breakpoint");

                ImGui.SetNextWindowSize(new(0, 0));
                if (ImGui.BeginPopupModal("Add Breakpoint"))
                    DrawBpMenu(cbp);
            }
            ImGui.EndChild();
        }

        public virtual void DrawBpMenu(Breakpoint bp, bool edit = false)
        {
            ImGui.Combo("Type", ref itemindex, [.. RamNames.Select(x => x.Name)], RamNames.Count);
            ImGui.PushItemWidth(-1);
            ImGui.Text("Address:"); ImGui.SameLine(86);
            ImGui.InputText($"##bpinput2", ref BpAddr, 6, HexInputFlags);
            OpenCopyContext("Address", ref BpAddr);
            ImGui.Text("Condition:"); ImGui.SameLine();
            ImGui.InputText($"##bpinput4", ref BPCondition, 6, HexInputFlags);
            OpenCopyContext("Condition", ref BPCondition);
            ImGui.PopItemWidth();

            var condition = BPCondition != "" && BPCondition != "-1" ? Convert.ToInt32(BPCondition, 16) : -1;

            ImGui.Checkbox("Exec", ref BreakTypes[0]); ImGui.SameLine();
            ImGui.Checkbox("Write", ref BreakTypes[1]); ImGui.SameLine();
            ImGui.Checkbox("Read", ref BreakTypes[2]);

            ImGui.Separator();
            if (ImGui.Button("Ok", new(99, 0)))
            {
                var types = BreakTypes[0] ? BPType.Exec : 0;
                types += BreakTypes[1] ? BPType.Write : 0;
                types += BreakTypes[2] ? BPType.Read : 0;
                IsSpc = itemindex == 6;
                if (!edit)
                    AddBreakpoint(BpAddr.ToInt(), types, condition, BreakTypes[1], RamNames[itemindex].Type);
                else
                    EditBreakpoint(BpAddr.ToInt(), bp.Addr, types, condition, BreakTypes[1], (RamType)itemindex);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new(99, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        public virtual void DrawRegisters()
        {
            ImGui.SetNextWindowPos(new(558, 30));
            ImGui.SetNextWindowSize(new(299, 648));
            ImGui.Begin("IO Registers", NoScrollFlags);
            {
                ImGui.BeginTabBar("##ioregtab");
                List<RegisterInfo> ioregisters = [];
                if (ImGui.BeginTabItem("Ppu"))
                {
                    DrawIORegisters(GetPpuState());
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Apu"))
                {
                    DrawIORegisters(GetApuState());
                    ImGui.EndTabItem();
                }
                if (GetPortState != null && ImGui.BeginTabItem("Ports"))
                {
                    DrawIORegisters(GetPortState());
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
                ImGui.End();
            }
        }

        private static void DrawIORegisters(List<RegisterInfo> ioregisters)
        {
            ImGui.BeginChild("##regswindow");
            ImGui.BeginTable("##ioregs", 3, ImGuiTableFlags.RowBg);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60);
            foreach (var v in ioregisters)
            {
                TableRowCol3(v.Address, v.Name, v.Value);
            }
            ImGui.EndTable();
            ImGui.EndChild();
        }

        public virtual void DrawTestAddr(int[] a, string[] testcpu)
        {
            ImGui.Begin("Test Error");
            {
                for (int i = 0; i < a.Length; i++)
                    ImGui.Text($"{testcpu[i]} Test Address: {a[i]:X6}");
                ImGui.End();
            }
        }

        public void DrawHighlight(int pc, int line)
        {
            if (Breakpoints.TryGetValue(line, out var bp))
            {
                if (bp.Enabled && (bp.Type & BPType.Exec) != 0)
                    DrawRect(0x4000ff00, 0xff00ff00);
                else
                    DrawRect(0x000000ff, 0xff0000ff);
            }
            if (line == pc)
            {
                DrawRect(0x6000ffff, 0xff00ffff);
                ImGui.SetScrollHereY(0.25f);
            }
        }

        public virtual void JumpTo(int i)
        {
            ImGui.PushItemWidth(-1);
            ImGui.InputText($"##bpinput2", ref GotoAddr, 6, HexInputFlags);
            ImGui.PopItemWidth();

            OpenCopyContext("gotocopypaste", ref GotoAddr);

            ImGui.Separator();
            if (ImGui.Button("Ok", new(99, 0)))
            {
                SetJumpAddress(GotoAddr, i);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new(99, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        public virtual int Scroll(int pc, int i)
        {
            var jump = JumpAddr[i] > -1 ? JumpAddr[i] : pc + ScrollY[i];
            pc = !FollowPc ? (jump + ScrollY[i]) : jump;

            if (FollowPc)
            {
                ScrollY[i] = 0;
                JumpAddr[i] = -1;
            }

            float mousewheel = ImGui.GetIO().MouseWheel;
            if (mousewheel != 0)
            {
                if (ImGui.IsWindowHovered())
                {
                    FollowPc = false;
                    if (mousewheel > 0)
                        ScrollY[i] -= 4;
                    else if (mousewheel < 0)
                        ScrollY[i] += 4;
                }
            }
            return pc;
        }


        public virtual bool ExecuteCheck(int a)
        {
            Breakpoints.TryGetValue(a, out Breakpoint bp);
            if (bp != null)
            {
                if (bp.Enabled)
                {
                    if (a == bp.Addr)
                    {
                        if (bp.Type == BPType.Exec || bp.Type == BPType.SpcExec || bp.Type == BPType.GsuExec)
                        {
                            FollowPc = true;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool AccessCheck(int a, int v, RamType memtype, int mask, bool write)
        {
            Breakpoints.TryGetValue(a, out Breakpoint bp);
            if (bp == null)
                Breakpoints.TryGetValue(a & mask, out bp);
            if (bp != null && bp.Enabled && bp.RamType == memtype && bp.Type > 0)
            {
                if ((bp.Type == BPType.Write && (bp.Write && write)) ||
                    (bp.Type == BPType.Read && (!bp.Write && !write)))
                {
                    if ((bp.Condition == -1) || (bp.Condition == v))
                        return true;
                }
            }
            return false;
        }

        public bool AccessCheckSpc(int a, int v, RamType memtype, bool write)
        {
            Breakpoints.TryGetValue(a, out Breakpoint bp);
            if (bp != null && bp.Enabled && bp.RamType == memtype && (bp.Type & (BPType.Read | BPType.Write)) > 0)
            {
                if ((bp.Type == BPType.Write && (bp.Write && write)) ||
                    (bp.Type == BPType.Read && (!bp.Write && !write)))
                {
                    if ((a == bp.Addr && bp.Condition == -1) || (a == bp.Addr && bp.Condition == v))
                        return true;
                }
            }
            return false;
        }

        public virtual void AddBreakpoint(int a, int type, int condition, bool write, RamType index = 0)
        {
            if (a == -1) return;
            Breakpoints.TryGetValue(a, out Breakpoint bp);
            if (bp == null)
                Breakpoints.Add(a, new(a, -1, type, write, true, index));
            else
                Breakpoints.Remove(a);
            SaveBreakpoints(GameName);
        }

        public virtual void EditBreakpoint(int a, int o, int type, int condition, bool write, RamType index = 0)
        {
            if (a == -1 || o == -1) return;
            Breakpoints.TryGetValue(o, out Breakpoint bp);
            if (bp != null)
            {
                Breakpoints.Remove(o);
                Breakpoints[a] = new(a, condition, type, write, bp.Enabled, index);
                SaveBreakpoints(GameName);
            }
        }
    }

    public class MemRegion(string name, ReadDel read, WriteDel write, int addr, int size, int addrlength)
    {
        public string Name { get; } = name;
        public ReadDel Read { get; } = read;
        public WriteDel Write { get; } = write;
        public int StartAddr { get; } = addr;
        public int Size { get; } = size;
        public int AddrLength { get; } = addrlength;
    }

    public class RamName(string name, RamType type)
    {
        public string Name { get; } = name;
        public RamType Type { get; } = type;
    }

    public class ButtonName
    {
        public string Name { get; set; }
        public Action<DebugState> Action { get; set; }
        public ButtonName(string name, Action<DebugState> action)
        {
            Name = name;
            Action = action;
        }
    }
}
