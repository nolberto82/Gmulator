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
        public Dictionary<string, Action<bool>> ButtonNamesSa1 { get; set; }
        public List<MemRegion> MemRegions { get; set; } = [];
        public int AsmOffset { get; private set; }
        public int[] JumpAddr { get; set; }
        public int[] ScrollY { get; set; }
        public int SelectedCpu { get; set; }
        public List<Breakpoint> Breakpoints { get; set; }
        public MemoryEditor MemoryEditor { get; set; }
        public Func<int, bool, (string, string, int, int)>[] OnDisassemble { get; set; }
        public Action CpuStep { get; set; }
        public Action<DebugState> SetState { get; set; }
        public Action<string> SaveBreakpoints { get; set; }
        public Func<List<RegisterInfo>> GetCpuState { get; set; }
        public Func<List<RegisterInfo>> GetCpuFlags { get; set; }
        public Func<List<RegisterInfo>> GetSa1State { get; set; }
        public Func<List<RegisterInfo>> GetSa1Flags { get; set; }
        public Func<List<RegisterInfo>> GetSa1IORegs { get; set; }
        public Func<List<RegisterInfo>> GetPpuState { get; set; }
        public Func<List<RegisterInfo>> GetApuState { get; set; }
        public Func<List<RegisterInfo>> GetSpcState { get; set; }
        public Func<List<RegisterInfo>> GetSpcFlags { get; set; }
        public Func<List<RegisterInfo>> GetPortState { get; set; }
        public Func<int> GetSpcPC { get; set; }
        public Func<int[]> GetPrg { get; set; }
        public Func<int[]> GetChr { get; set; }
        public IConsole Console { get; set; }
        public ICpu Cpu { get; set; }
        public IPpu Ppu { get; set; }

        public const int MainCpu = 0;
        public const int Sa1Cpu = 1;
        public const int SpcCpu = 2;
        public const int GsuCpu = 3;

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

            MemoryEditor = new(AddBreakpoint);
        }

        public virtual void Reset(Snes snes)
        { }

        public virtual void Draw(Texture2D texture)
        {
            ImGui.SetNextWindowPos(new(5, 30));
            ImGui.SetNextWindowSize(new(256, 240));
            ImGui.Begin("Screen");
            {
                if (ImGui.IsWindowFocused())
                    IsScreenWindow = true;
                var size = ImGui.GetContentRegionAvail();

                //rlImGui.ImageRect(texture, (int)size.X, (int)size.Y, new(0, 0, (int)size.X, -(int)size.Y));
                ImGui.Image((nint)texture.Id, ImGui.GetContentRegionAvail());
                Notifications.RenderDebug();
                ImGui.End();
            }
        }

        public virtual void DrawDebugger(int pc, bool logging, int n)
        {
            ImGui.SetNextWindowPos(new(5, 272));
            ImGui.SetNextWindowSize(new(460, 405));
            ImGui.Begin("Main Processor");
            {
                ImGui.Columns(2);
                DrawButtons(logging, 0);
                ImGui.SetColumnWidth(0, 215);
                DrawDisassembly(pc, MainCpu);
                ImGui.NextColumn();
                DrawCpuInfo(Cpu);
                DrawBreakpoints();
                ImGui.Columns(1);
                ImGui.End();
            }
        }

        private void DrawDisassembly(int Pc, int n)
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
                    var (disasm, access, op, size) = OnDisassemble[n](pc, false);

                    ImGui.PushID(pc);

                    if (ImGui.Selectable($"{pc:X6} ", false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            var bp = Breakpoints.Find(b => b.Addr == pc);
                            if (bp == null)
                                AddBreakpoint(pc, BpType.CodeExec, -1, false);
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

        public virtual void DrawCoProcessors(bool logging)
        {
            ImGui.SetNextWindowPos(new(470, 272));
            ImGui.SetNextWindowSize(new(395, 405));
            ImGui.Begin("Co Processors", NoScrollFlags);
            {
                ICpu cpu = null;
                ImGui.Columns(2);
                DrawButtons(logging, SelectedCpu);
                ImGui.BeginTabBar("##cputab");
                {
                    if (GetSa1State != null)
                    {
                        if (ImGui.BeginTabItem("Sa1"))
                        {
                            SnesSa1 sa1 = (Console as Snes).Sa1;
                            ImGui.SetColumnWidth(0, 210);
                            SelectedCpu = Sa1Cpu;
                            cpu = sa1;
                            (Console as Snes).Logger.IsSa1 = true;
                            DrawDisassembly((Console as Snes).Sa1.PBPC, Sa1Cpu);
                            (Console as Snes).Logger.IsSa1 = false;
                            ImGui.EndTabItem();
                        }
                    }

                    if (GetSpcPC != null && ImGui.BeginTabItem("Spc"))
                    {
                        SnesSpc spc = (Console as Snes).Spc;
                        SelectedCpu = SpcCpu;
                        cpu = spc;
                        DrawDisassembly(spc.PC, SpcCpu);
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
                    DrawCpuInfo(cpu);
                    ImGui.Columns(1);

                    ImGui.EndTabBar();
                }
                ImGui.End();
            }
        }

        public virtual void DrawButtons(bool logging, int processor)
        {
            ImGui.BeginChild($"##Buttons{processor}", new(0, 45));
            {
                foreach (var v in ButtonNames.Select((e, i) => new { e, i }))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, v.i == ButtonNames.Count - 1 && logging ? GREEN : WHITE);
                    if (ImGui.Button(v.e.Name, ButtonSize))
                    {
                        SelectedCpu = processor;
                        v.e.Action(0);
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
            ImGui.BeginChild("Cpu Registers", new(70, 140));
            {
                var registers = cpu.GetRegisters();
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
                var flags = cpu.GetFlags();
                for (int i = 0; i < flags.Count; i++)
                {
                    var v = flags[i];
                    Checkbox(v.Name, Convert.ToBoolean(v.Value));
                    if ((i + 1) % 2 > 0 && i != flags.Count - 1)
                        ImGui.SameLine();
                }

                ImGui.EndChild();
            }
            ImGui.SeparatorText("");
            ImGui.Text($"Cycles: {cpu.Cycles}");
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
            ImGui.SetNextWindowSize(new(409, 240));
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

        public virtual void Continue(DebugState type)
        {
            FollowPc = true;
            Console.EmuState = DebugState.Running;
        }

        public virtual void StepInto(DebugState type)
        {
            //SetState(type);
            FollowPc = true;
            Console.EmuState = type;
        }

        public virtual void StepOver(DebugState type)
        {
            FollowPc = true;
            Console.EmuState = DebugState.Running;
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
        { }

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
                    }
                    ImGui.EndTabBar();
                }
            }
            //if (ImGui.BeginCombo("##memoryregions", MemRegions[MemoryEditor.SelectedMemTab].Name))
            //{
            //    for (int i = 0; i < MemRegions.Count; i++)
            //    {
            //        MemRegion m = MemRegions[i];
            //        if (m.StartAddr == -1) continue;
            //        if (ImGui.Selectable(m.Name, MemoryEditor.SelectedMemTab == i))
            //        {
            //            MemoryEditor.ReadFn = m.Read;
            //            MemoryEditor.WriteFn = m.Write;
            //            MemoryEditor.OptAddrDigitsCount = m.AddrLength;
            //            MemoryEditor.SelectedMemTab = i;
            //        }
            //    }
            //    ImGui.EndCombo();
            //}

            //if (MemoryEditor.ReadFn == null || MemoryEditor.WriteFn == null)
            //{
            //    MemoryEditor.ReadFn = MemRegions[0].Read;
            //    MemoryEditor.WriteFn = MemRegions[0].Write;
            //    MemoryEditor.OptAddrDigitsCount = MemRegions[0].AddrLength;
            //}

            //int index = MemoryEditor.SelectedMemTab;
            //MemoryEditor.DrawContents(null, MemRegions[index].Size, MemRegions[index].StartAddr);
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
                    for (int i = 0; i < Breakpoints.Count; i++)
                    {
                        Breakpoint bp = Breakpoints[i];
                        cbp = bp;
                        var types = (bp.Type & Access.Exec) > 0 ? "X" : ".";
                        types += (bp.Type & Access.Write) > 0 ? "W" : ".";
                        types += (bp.Type & Access.Read) > 0 ? "R" : ".";
                        ImGui.PushID(i);
                        if (ImGui.Button($"{bp.Addr:X6}"))
                            SetJumpAddress(bp.Addr, 0);
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
                        var text = $"{types} {name} {condition}";
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
                                //itemindex = (int)MemRegions[bp.Type].Type;
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
            var n = MemRegions.FindIndex(x => (int)x.Type == itemindex);
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
                IsSpc = itemindex == 6;
                if (!edit)
                    AddBreakpoint(BpAddr.ToInt(), types, condition, BreakTypes[1]);
                else
                    EditBreakpoint(BpAddr.ToInt(), bp.Addr, types, condition, BreakTypes[1], itemindex);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new(99, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        public virtual void DrawRegisters()
        {
            ImGui.SetNextWindowPos(new(870, 30));
            ImGui.SetNextWindowSize(new(325, 648));
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
                if (GetSa1IORegs != null && ImGui.BeginTabItem("Sa1"))
                {
                    DrawIORegisters(GetSa1IORegs());
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

        public virtual void AddBreakpoint(int addr, BpType type, int condition, bool write)
        {
            if (addr == -1) return;
            var bp = Breakpoints.Find(b => b.Addr == addr);
            if (bp == null)
            {
                Breakpoints.Add(new(addr, -1, type, write, true));
                SaveBreakpoints(GameName);
            }
        }

        public virtual void RemoveBreakpoint(Breakpoint bp)
        {
            Breakpoints.Remove(bp);
            SaveBreakpoints(GameName);
        }

        public virtual void EditBreakpoint(int a, int o, BpType type, int condition, bool write, int index = 0)
        {
            if (a == -1 || o == -1) return;
            var bp = Breakpoints.FirstOrDefault(b => b.Addr == o);
            if (bp != null)
            {
                Breakpoints.Remove(bp);
                Breakpoints.Add(new(a, condition, type, write, bp.Enabled));
                SaveBreakpoints(GameName);
            }
        }
    }

    public class MemRegion(string name, ReadDel read, WriteDel write, int addr, int size, int addrlength, BpType type)
    {
        public string Name { get; } = name;
        public ReadDel Read { get; } = read;
        public WriteDel Write { get; } = write;
        public int StartAddr { get; } = addr;
        public int Size { get; } = size;
        public int AddrLength { get; } = addrlength;
        public BpType Type { get; set; } = type;
    }

    public class ButtonName(string name, Action<DebugState> action)
    {
        public string Name { get; set; } = name;
        public Action<DebugState> Action { get; set; } = action;
    }
}
