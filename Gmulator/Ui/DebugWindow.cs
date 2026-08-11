using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using Gmulator.Core.Snes.Sa1;
using Gmulator.Interfaces;
using ImGuiNET;
using rlImGui_cs;
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
        public List<MemRegion> MemRegions { get; set; } = [];
        public int AsmOffset { get; private set; }
        public int[] JumpAddr { get; set; }
        public int[] ScrollY { get; set; }
        public CpuType SelectedCpu { get; set; }
        public List<Breakpoint> Breakpoints { get; set; }
        public MemoryEditor MemoryEditor { get; set; }
        public Disassemble[] Disassemble { get; set; }
        public Action CpuStep { get; set; }
        public Action<DebugState> SetState { get; set; }
        public Action<string> SaveBreakpoints { get; set; }
        public Func<List<RegisterInfo>> GetCpuState { get; set; }
        public Func<List<RegisterInfo>> GetCpuFlags { get; set; }
        public Func<List<RegisterInfo>> GetSa1State { get; set; }
        public Func<List<RegisterInfo>> GetSa1Flags { get; set; }
        public Func<List<RegisterInfo>> GetSa1IORegs { get; set; }
        public Func<List<RegisterInfo>> GetGsuState { get; set; }
        public Func<List<RegisterInfo>> GetGsuFlags { get; set; }
        public Func<List<RegisterInfo>> GetGsuIORegs { get; set; }
        public Func<List<RegisterInfo>> GetPpuState { get; set; }
        public Func<List<RegisterInfo>> GetApuState { get; set; }
        public Func<List<RegisterInfo>> GetSpcState { get; set; }
        public Func<List<RegisterInfo>> GetSpcFlags { get; set; }
        public Func<List<RegisterInfo>> GetPortState { get; set; }
        public Func<int, List<RegisterInfo>> GetDmaState { get; set; }
        public Func<int> GetSpcPC { get; set; }
        public Func<int[]> GetPrg { get; set; }
        public Func<int[]> GetChr { get; set; }
        public IConsole Console { get; set; }
        public ICpu Cpu { get; set; }
        public IPpu Ppu { get; set; }

        public int GetCpuIndex()
        {
            int index= Math.Abs(Array.FindIndex(Disassemble, k => k.CpuType == SelectedCpu));
            if (index == -1)
                return 0;
            else 
                return index;
        }

        public DebugWindow(IConsole console)
        {
            CpuStep = console.Cpu.Step;
            Console = console;
            Cpu = console.Cpu;
            Ppu = console.Ppu;
            ButtonNames =
            [
                new("Run", Continue),
                new("Step", StepInto),
                new("Over", StepOver),
                new("Reset",Reset),
                new("Line", StepScanline),
                new("Trace", ToggleTrace),
            ];

            MemoryEditor = new();
        }

        public virtual void Draw(Texture2D texture)
        {
            ImGui.Begin("Screen");
            {
                if (ImGui.IsWindowFocused())
                    IsScreenWindow = true;

                ImGui.Image((nint)texture.Id, ImGui.GetContentRegionAvail());
                Notifications.RenderDebug();
                ImGui.End();
            }
        }

        public virtual void DrawDebugger(int pc, bool logging, CpuType n)
        {
            int index = Array.FindIndex(Disassemble, k => k.CpuType == SelectedCpu);
            if (index == -1)
                index = 0;

            ImGui.Begin("Processors");
            {
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 210);
                SelectedCpu = CpuType.Snes;
                DrawButtons(logging, CpuType.Snes);
                DrawDisassembly(pc, 2);
                ImGui.NextColumn();
                DrawCpuInfo(Cpu);
                DrawBreakpoints((int)n);
                ImGui.Columns(1);
            }
            ImGui.End();
        }

        public void DrawDisassembly(int Pc, int n)
        {
            ImGui.PushID(n);
            ImGui.BeginChild($"Disassembly{n}");
            {
                var pc = Scroll(Pc, n);
                //pc = pc & 0xfff000;
                if (ImGui.BeginPopupContextWindow($"gotomenu{n}"))
                    JumpTo(n);

                if (ImGui.IsKeyPressed(ImGuiKey.F5))
                    SetState(DebugState.Running);

                for (int i = 0; i < DisasmMaxLines - 1; i++)
                {
                    var (disasm, access, op, size) = Disassemble[n].OnDisassemble(pc, false);

                    ImGui.PushID(pc);

                    if (ImGui.Selectable($"{pc:X6} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            var bp = Breakpoints.Find(b => b.Addr == pc);
                            if (bp == null)
                                AddBreakpoint(pc, BpType.CodeExec, RamType.Rom, SelectedCpu, 0, "X..", -1, false);
                            else
                                RemoveBreakpoint(bp);
                        }

                    }

                    DrawHighlight(Pc, pc);

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetNextWindowSize(new(150, 0));
                        ImGui.BeginTooltip();
                        int offset = Console.Mmu.GetOffset(pc);
                        ImGui.Text($"PC:  ${pc:x6}");
                        ImGui.Text($"Prg: ${offset:x6}");
                        ImGui.Text($"Op:  ${op:x2}");
                        ImGui.Text($"Mem: {access}");
                        ImGui.EndTooltip();
                    }

                    ImGui.PopID();
                    ImGui.SameLine();
                    ImGui.Text($"{disasm}");
                    pc += size;
                }
                ImGui.EndChild();
            }
            ImGui.PopID();
        }

        public virtual void DrawButtons(bool logging, CpuType processor)
        {
            ImGui.BeginChild($"##Buttons{processor}", new(0, 45));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Name, ButtonSize))
                    {
                        SelectedCpu = processor;
                        v.e.Action();
                    }
                    if (v.i != 2)
                        ImGui.SameLine();
                    ImGui.PopStyleColor();
                }
                ImGui.EndChild();
            }
        }

        public virtual void DrawCpuInfo(ICpu cpu)
        {
            if (cpu == null) return;
            ImGui.BeginChild("Cpu Registers", new(0, cpu is not SnesGsu ? 110 : 90));
            {
                var registers = cpu.GetRegisters();
                for (int i = 0; i < registers.Count; i++)
                {
                    var v = registers[i];
                    if (cpu is SnesGsu)
                        ImGui.Text($"{v.Name,-3}");
                    else
                        ImGui.Text($"{v.Name}");
                    ImGui.SameLine();
                    ImGui.TextColored(GREEN, $"{v.Value}");
                    if (v.Address == "" && i < registers.Count - 1)
                        ImGui.SameLine();
                }
                ImGui.Separator();
                if (cpu is SnesCpu)
                {
                    ImGui.Text($"H Clock: {Ppu.GetState()[0].Value}");
                    ImGui.Text($"Scanline: {Ppu.GetState()[1].Value}");
                }

                ImGui.Text($"Cycles: {cpu.Cycles}");
                ImGui.EndChild();
            }

            ImGui.SeparatorText("Flags");
            ImGui.BeginChild("##cpuflags", new(0, cpu is not SnesGsu ? 80 : 50));
            {
                var flags = cpu.GetFlags();
                for (int i = 0; i < flags.Count; i++)
                {
                    var v = flags[i];
                    Checkbox(v.Name, Convert.ToBoolean(v.Value));
                    if (v.Address == "")
                        ImGui.SameLine();
                }
                ImGui.EndChild();
            }

            if (cpu is NesCpu)
                DrawMapperBanks();

            if (cpu is SnesGsu)
            {
                ImGui.SeparatorText("Misc");
                ImGui.BeginChild("##miscregisters", new(0, 40));
                {
                    var misc = (Console as Snes)?.Gsu.GetMisc();
                    for (int i = 0; i < misc.Count; i++)
                    {
                        var v = misc[i];
                        ImGui.Text($"{v.Name}");
                        ImGui.SameLine();
                        ImGui.TextColored(GREEN, $"{v.Value}");
                        if (v.Address == "")
                            ImGui.SameLine();
                    }
                    ImGui.EndChild();
                }
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
                    //TableRow("Cpu Test Addr",$"{Cpu.TestAddr}");
                    ImGui.EndTable();
                }
            }
            ImGui.End();
        }

        public virtual void DrawMapperBanks()
        {
            if (GetPrg == null || GetChr == null) return;
            ImGui.BeginChild("Banks", new(0, 130));
            ImGui.SeparatorText("Prg");
            var Prg = GetPrg();
            for (int i = 0; i < Prg?.Length; i++)
            {
                ImGui.Text($"{i:X2}"); ImGui.SameLine();
                ImGui.TextColored(GREEN, $"{Prg[i]:X2}");
                if ((i + 1) % 4 != 0)
                    ImGui.SameLine();
            }

            ImGui.SeparatorText("Chr");
            var Chr = GetChr();
            for (int i = 0; i < Chr?.Length; i++)
            {
                ImGui.Text($"{i:X2}"); ImGui.SameLine();
                ImGui.TextColored(GREEN, $"{Chr[i]:X2}");
                if ((i + 1) % 4 != 0)
                    ImGui.SameLine();
            }
            ImGui.EndChild();
        }

        public virtual void DrawDmaInfo(Func<int, List<RegisterInfo>> regs)
        {
            ImGui.BeginChild("Dma");
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
                            var v = regs(c);
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
            ImGui.EndChild();
        }

        public virtual void Continue()
        {
            FollowPc = true;
            Cpu.Step();
        }

        public virtual void StepInto()
        {
            FollowPc = true;
            Console.DbgState = DebugState.StepMain;
        }

        public virtual void StepOver()
        {
            FollowPc = true;
            Console.DbgState = DebugState.Running;
        }

        public virtual void StepScanline()
        {
            var oldline = Ppu.GetScanline();
            while (oldline == Ppu.GetScanline())
                CpuStep();
            SetState(DebugState.Break);
            FollowPc = true;
        }

        public virtual void Reset()
        {
            Console.Reset(GameName, true);
            Console.DbgState = DebugState.Break;
            FollowPc = true;
        }

        public virtual void ToggleTrace()
        { }

        public virtual void SetJumpAddress(object addr, int i)
        {
            if (i > JumpAddr.Length) return;
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
            //ImGui.SetNextWindowPos(new(5, 680));
            //ImGui.SetNextWindowSize(new(550, 295));
            ImGui.Begin("Memory", NoScrollFlags);

            for (int i = 0; i < MemRegions.Count; i++)
            {
                MemRegion n = MemRegions[i];
                if (ImGui.BeginTabBar("memregions"))
                {
                    if (ImGui.BeginTabItem(n.Name))
                    {
                        MemoryEditor.ReadFn = n.Read;
                        MemoryEditor.WriteFn = n.Write;
                        MemoryEditor.OptAddrDigitsCount = n.AddrLength;
                        MemoryEditor.SelectedMemTab = i;
                        MemoryEditor.DrawContents(null, n.Size, n.StartAddr);
                        ImGui.EndTabItem();

                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.IsWindowHovered())
                            ImGui.OpenPopup("memorycontext");

                        if (ImGui.BeginPopup("memorycontext"))
                        {
                            if (ImGui.Button("Dump"))
                            {
                                byte[] memory = new byte[n.Size];
                                for (int b = 0; b < n.Size; b++)
                                    memory[b] = (byte)n.Read(b);
                                File.WriteAllBytes($"{n.Name}.bin", memory);
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.EndPopup();
                        }
                    }
                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }

        public virtual void DrawBreakpoints(int index)
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
                    for (int i = 0; i < Breakpoints.Count; i++)
                    {
                        Breakpoint bp = Breakpoints[i];
                        if (bp.CpuType != (CpuType)index)
                            continue;
                        cbp = bp;
                        ImGui.PushID(i);
                        if (ImGui.Button($"{bp.Addr:X6}"))
                            SetJumpAddress(bp.Addr, index);
                        ImGui.TableNextColumn();

                        bool enabled = bp.Enabled;
                        if (ImGui.Checkbox("", ref enabled))
                        {
                            bp.Enabled = enabled;
                            SaveBreakpoints(GameName);
                        }

                        var condition = bp.Condition > -1 ? $"{bp.Condition:X4}" : "    ";
                        string name = "";
                        var memRegion = MemRegions.FirstOrDefault(x => (x.Type & bp.Type) == bp.Type);
                        if (memRegion != null)
                            name = memRegion.Name;
                        var text = $"{bp.Access} {name} {condition}";
                        ImGui.TableNextColumn();
                        if (ImGui.Selectable(text, false, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                BpAddr = $"{bp.Addr:X6}";
                                BPCondition = bp.Condition > -1 ? $"{bp.Condition:X2}" : "";
                                BreakTypes[0] = (bp.Type & Access.Exec) > 0;
                                BreakTypes[1] = (bp.Type & Access.Write) > 0;
                                BreakTypes[2] = (bp.Type & Access.Read) > 0;
                                itemindex = bp.Index;
                                ImGui.OpenPopup("Edit Breakpoint");
                            }
                        }

                        ImGui.SetNextWindowSize(new(0, 0));
                        if (ImGui.BeginPopupModal("Edit Breakpoint"))
                            DrawBpMenu(bp, true);
                        ImGui.TableNextColumn();

                        if (ImGui.Button("x"))
                        {
                            RemoveBreakpoint(bp);
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
            ImGui.Combo("Type", ref itemindex, [.. MemRegions.Select(x => x.Name)], MemRegions.Count);
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
                BpType types = BreakTypes[0] ? MemRegions[itemindex].Type & Access.Exec : 0;
                types += BreakTypes[1] ? (int)(MemRegions[itemindex].Type & Access.Write) : 0;
                types += BreakTypes[2] ? (int)(MemRegions[itemindex].Type & Access.Read) : 0;
                string access = BreakTypes[0] ? "X" : ".";
                access += BreakTypes[1] ? "W" : ".";
                access += BreakTypes[2] ? "R" : ".";
                IsSpc = itemindex == 5;
                if (!edit)
                    AddBreakpoint(BpAddr.ToInt(), MemRegions[itemindex].Type, MemRegions[itemindex].RamType, SelectedCpu, itemindex, access, condition, BreakTypes[1]);
                else
                    EditBreakpoint(BpAddr.ToInt(), bp.Addr, types, MemRegions[itemindex].RamType, SelectedCpu, itemindex, access, condition, BreakTypes[1]);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new(99, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        public virtual void DrawRegisters()
        {
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

                if (GetDmaState != null && ImGui.BeginTabItem("Dma"))
                {
                    DrawDmaInfo(GetDmaState);
                    ImGui.EndTabItem();
                }

                if (GetSa1IORegs != null && ImGui.BeginTabItem("Sa1"))
                {
                    DrawIORegisters(GetSa1IORegs());
                    ImGui.EndTabItem();
                }

                if (GetGsuIORegs != null && ImGui.BeginTabItem("Gsu"))
                {
                    DrawIORegisters(GetGsuIORegs());
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
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 170);
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
            ImGui.SetNextWindowPos(new(680, 30));
            ImGui.SetNextWindowSize(new(0, 240));
            ImGui.Begin("Test Error");
            {
                for (int i = 0; i < a.Length; i++)
                    ImGui.Text($"{testcpu[i]} Test Address: {a[i]:X6}");
                ImGui.End();
            }
        }

        public void DrawHighlight(int pc, int line)
        {
            var bp = Breakpoints.Find(b => b.Addr == line);
            if (bp != null)
            {
                if (bp.Enabled && (bp.Type & BpType.CodeExec) != 0)
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

        public virtual void AddBreakpoint(int addr, BpType type, RamType ramType, CpuType cpuType, int index, string access, int condition, bool write)
        {
            if (addr == -1) return;
            var bp = Breakpoints.Find(b => b.Addr == addr);
            if (bp == null)
            {
                Breakpoints.Add(new(addr, -1, type, ramType, cpuType, index, access, write, true));
                SaveBreakpoints(GameName);
            }
        }

        public virtual void RemoveBreakpoint(Breakpoint bp)
        {
            Breakpoints.Remove(bp);
            SaveBreakpoints(GameName);
        }

        public virtual void EditBreakpoint(int newAddr, int oldAddr, BpType type, RamType ramType, CpuType cpuType, int index, string access, int condition, bool write)
        {
            if (newAddr == -1 || oldAddr == -1) return;
            var bp = Breakpoints.FirstOrDefault(b => b.Addr == oldAddr);
            if (bp != null)
            {
                Breakpoints.Remove(bp);
                Breakpoints.Add(new(newAddr, condition, type, ramType, cpuType, index, access, write, bp.Enabled));
                SaveBreakpoints(GameName);
            }
        }
    }

    public class Disassemble(Func<int, bool, (string, string, int, int)> onDisassemble, CpuType cpuType)
    {
        public Func<int, bool, (string, string, int, int)> OnDisassemble { get; } = onDisassemble;
        public CpuType CpuType { get; } = cpuType;
    }

    public class MemRegion(string name, ReadDel read, WriteDel write, int addr, int size, int addrlength, BpType type, RamType ramType)
    {
        public string Name { get; } = name;
        public ReadDel Read { get; } = read;
        public WriteDel Write { get; } = write;
        public int StartAddr { get; } = addr;
        public int Size { get; } = size;
        public int AddrLength { get; } = addrlength;
        public BpType Type { get; set; } = type;
        public RamType RamType { get; set; } = ramType;
    }

    public class ButtonName(string name, Action action)
    {
        public string Name { get; set; } = name;
        public Action Action { get; set; } = action;
    }
}
