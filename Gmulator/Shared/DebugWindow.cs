using Gmulator.Core.Snes;
using ImGuiNET;
using System;
using System.Net.Sockets;

namespace Gmulator.Shared
{
    public abstract class DebugWindow
    {
        private int itemindex;
        private string BpAddr = "";
        private string GotoAddr = "";
        private string BPCondition = "";
        public string GameName { get; set; } = "";
        public int StepOverAddr = -1;
        public bool FollowPc { get; private set; } = true;
        public bool IsSpc { get; private set; }
        public bool? ShowSa1 { get; set; } = false;
        public bool? ShowSpc { get; set; } = true;
        public bool[] BreakTypes { get; private set; } = [false, false, false];
        public Dictionary<string, Action<int>> ButtonNames { get; set; }
        public Dictionary<string, Action<bool>> ButtonNamesSa1 { get; set; }
        public List<MemRegion> MemRegions { get; set; } = [];
        public string[] RamNames { get; set; }
        public int AsmOffset { get; private set; }
        public int[] JumpAddr { get; set; }
        public int[] ScrollY { get; set; }
        public SortedDictionary<int, Breakpoint> Breakpoints { get; set; }
        public Dictionary<int, FreezeMem> FreezeValues { get; private set; } = [];
        public MemoryEditor MemoryEditor { get; set; }
        public Func<int, bool, bool, (string, int, int)>[] OnTrace { get; set; }
        public Action<string> SaveBreakpoints { get; set; }
        public const int MainCpu = 0;
        public const int Sa1Cpu = 1;
        public const int SpcCpu = 2;
        public const int GsuCpu = 3;



        public DebugWindow()
        {
            ButtonNames = new()
            {
                ["Run"] = Continue,
                ["Step"] = StepInto,
                ["Over"] = StepOver,
                ["Line"] = StepScanline,
                ["Reset"] = Reset,
                ["Trace"] = ToggleTrace,
            };

            MemoryEditor = new(AddBreakpoint);
        }

        public virtual void SetCpu(Snes snes) { }
        public virtual void Draw() { }
        public virtual void DrawCoProcessors() { }
        public virtual void DrawCpuInfo() { }
        public virtual void DrawStackInfo(Span<byte> data, int addr, int start, string name)
        {
            if (ImGui.BeginChild($"##winstack{name}"))
            {
                ImGui.BeginTable($"##stack{name}", 2);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                for (int i = data.Length - 1; i >= 0; i -= 2)
                {
                    TableRow($"{i:X4}", $"{data[i] | data[i - 1] << 8:X4}");
                }
                ImGui.EndTable();
                ImGui.EndChild();
            }
        }

        public virtual void DrawCartInfo(Dictionary<string, string> info)
        {
            if (ImGui.Begin("Cart"))
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
            ImGui.End();
        }

        public virtual void DrawMapperBanks(byte[] Prg, byte[] Chr) { }

        public virtual void DrawDmaInfo() { }

        public virtual void Continue(int type = 0) => FollowPc = true;
        public virtual void StepInto(int type = 0) => FollowPc = true;
        public virtual void StepOver(int type = 0) => FollowPc = true;
        public virtual void StepScanline(int type = 0) => FollowPc = true;
        public virtual void Reset(int type = 0) => FollowPc = true;
        public virtual void ToggleTrace(int type = 0) { }

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
            foreach (var n in MemRegions)
            {
                if (ImGui.BeginTabBar("memregions"))
                {
                    if (ImGui.BeginTabItem(n.Name))
                    {
                        if (n.Data != null)
                        {
                            MemoryEditor.OptAddrDigitsCount = n.AddrLength;
                            MemoryEditor.DrawContents(n.Data(), n.Data().Length, n.StartAddr);
                        }
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
            }
        }

        public virtual void DrawBreakpoints()
        {
            if (ImGui.BeginChild("bpchildwindow"))
            {
                const int columns = 7;
                Breakpoint cbp = null;
                if (ImGui.BeginTable("##bptable", columns, ImGuiTableFlags.RowBg))
                {
                    for (int i = 0; i < columns; i++)
                    {
                        if (i != 3)
                            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                        else
                            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 55);
                    }

                    ImGui.TableNextColumn();
                    foreach (var bp in Breakpoints.Values.ToList())
                    {
                        cbp = bp;
                        var types = (bp.Type & BPType.Exec) > 0 ? "X" : ".";
                        types += (bp.Type & BPType.Write) > 0 ? "W" : ".";
                        types += (bp.Type & BPType.Read) > 0 ? "R" : ".";
                        if (ImGui.Button($"{bp.Addr:X6}"))
                            SetJumpAddress(bp.Addr, 0);
                        ImGui.TableNextColumn();
                        ImGui.PushID(bp.Addr);
                        bool enabled = bp.Enabled;
                        if (ImGui.Checkbox("E", ref enabled))
                        {
                            bp.Enabled = enabled;
                            SaveBreakpoints(GameName);
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text(types);
                        ImGui.TableNextColumn();
                        ImGui.Text(RamNames[bp.RamType > 0 ? (int)bp.RamType % RamNames.Length : 0]);
                        ImGui.TableNextColumn();
                        ImGui.Text(bp.Condition > -1 ? $"{bp.Condition:X4}" : "    ");
                        ImGui.TableNextColumn();
                        if (ImGui.Button("Edit"))
                        {
                            BpAddr = $"{bp.Addr:X6}";
                            BPCondition = bp.Condition > -1 ? $"{bp.Condition}" : "";
                            BreakTypes[0] = (bp.Type & BPType.Exec) > 0;
                            BreakTypes[1] = (bp.Type & BPType.Write) > 0;
                            BreakTypes[2] = (bp.Type & BPType.Read) > 0;
                            ImGui.OpenPopup("bpmenu");
                        }

                        ImGui.SetNextWindowSize(new(0, 0));
                        if (ImGui.BeginPopupModal("bpmenu"))
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

                ImGui.SetNextWindowSize(new(0, 0));
                if (ImGui.BeginPopupContextWindow("bpmenu"))
                    DrawBpMenu(cbp);

                ImGui.EndChild();
            }
        }

        public virtual void DrawBpMenu(Breakpoint bp, bool edit = false)
        {
            ImGui.Combo("Ram Type", ref itemindex, RamNames, RamNames.Length);
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
                    AddBreakpoint(BpAddr.ToInt(), types, condition, BreakTypes[1], (RamType)itemindex);
                else
                    EditBreakpoint(BpAddr.ToInt(), bp.Addr, types, condition, BreakTypes[1], (RamType)itemindex);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new(99, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        public virtual void DrawRegisters(List<RegistersInfo> ioregisters)
        {
            ImGui.SetNextWindowSize(new(300, 600));
            if (ImGui.Begin("IO Registers"))
            {
                ImGui.BeginTable("##ioregs", 3, ImGuiTableFlags.RowBg);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 55);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60);
                foreach (var v in ioregisters)
                {
                    TableRowCol3(v.Address, v.Name, v.Value);
                }
                ImGui.EndTable();
            }
            ImGui.End();
        }

        public virtual void DrawTestAddr(int[] a, string[] testcpu)
        {
            ImGui.SetNextWindowPos(new(5, 420));
            ImGui.SetNextWindowSize(new(0, 0));
            if (ImGui.Begin("Test Error"))
            {
                for (int i = 0; i < a.Length; i++)
                    ImGui.Text($"{testcpu[i]} Test Address: {a[i]:X6}");
            }
            ImGui.End();
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
                            //UpdateScroll();
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool AccessCheck(int a, int v, RamType memtype, bool write)
        {
            Breakpoints.TryGetValue(a, out Breakpoint bp);
            if (bp != null && bp.Enabled && bp.RamType == memtype && bp.Type > 0)
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

        public virtual void AddFreezeValue(int a, byte v)
        {
            FreezeValues.Add(a, new FreezeMem(a, v));
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

    public class MemRegion(string name, Func<byte[]> data, int addr, int addrlength)
    {
        public string Name { get; } = name;
        public Func<byte[]> Data { get; } = data;
        public int StartAddr { get; } = addr;
        public int AddrLength { get; } = addrlength;
    }

    public class FreezeMem(int a, int v)
    {
        public int Addr { get; set; } = a;
        public int Value { get; set; } = v;
    }
}
