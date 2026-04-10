using static Gmulator.Core.Snes.SnesCpu;

namespace Gmulator.Core.Snes;

public class SnesLogger(Snes snes)
{
    public bool LogMain { get; private set; }
    public bool LogSa1 { get; private set; }

    private StreamWriter Outfile;
    private readonly Snes Snes = snes;
    private readonly SnesCpu Cpu = snes.Cpu;
    private readonly List<Opcode> opcodes = snes.Cpu.Disasm;
    private readonly Func<int, int> Read = snes.ReadOp;
    private readonly Func<int, int> ReadWord = snes.ReadWord;
    private readonly Func<int, int> ReadLong = snes.ReadLong;
    private bool MMode;
    private bool XMode;

    public (string, string, int, int) Disassemble(int pc, bool getRegisters)
    {
        int x = Cpu.X; int y = Cpu.Y;
        int op = Read(pc);
        int a;

        Opcode opcode = opcodes[op];

        int size = opcode.Size;
        int mode = opcode.Mode;
        string bytedata = "";

        string data = $"{opcode.Name} ";
        string regtext = string.Empty;
        string access = string.Empty;

        int[] b = new int[size];

        for (int i = 0; i < b.Length; i++)
            b[i] = Read(pc + i);

        foreach (var n in b)
            bytedata += $"{n:x2} ";

        if (op == 0xe2)
        {
            XMode = (b[1] & FX) != 0;
            MMode = (b[1] & FM) != 0;
        }

        if (op == 0xc2)
        {
            XMode = (b[1] & FX) == 0;
            MMode = (b[1] & FM) == 0;
        }

        if (Cpu.MMem)
            MMode = true;

        switch (mode)
        {
            case Absolute:
                a = ReadWord(pc + 1);
                if (op == 0x20 || op == 0x4c || op == 0x6c)
                    data += $"${a:x4}";
                else
                {
                    data += $"${a:x4}";
                    access += $"${Cpu.DB:x2}{a:x4} = #${Read(a):x2}";
                }
                break;
            case AbsoluteIndexedIndirect:
                a = ReadWord(pc + 1);
                data += $"(${a:x4},x)";
                break;
            case AbsoluteIndexedX:
                a = ReadWord(pc + 1);
                data += $"${a:x4},x";
                access += $"${a + Cpu.X:x4} = #${Read(a + Cpu.X):x2}";
                break;
            case AbsoluteIndexedY:
                a = ReadWord(pc + 1);
                data += $"${a:x4},y";
                access += $"${a + Cpu.Y:x4} = #${Read(a + Cpu.Y):x2}";
                break;
            case AbsoluteIndirect:
                a = ReadWord(pc + 1);
                data += $"$({a:x4})";
                break;
            case AbsoluteIndirectLong:
                a = ReadWord(pc + 1);
                data += $"[${a:x4}]";
                break;
            case AbsoluteLong:
                a = ReadLong(pc + 1);
                data += $"${a:x6}";
                access += $"${a:x6} = #${Read(a):x2}";
                break;
            case AbsoluteLongIndexedX:
                a = ReadLong(pc + 1);
                data += $"${a:x6},x";
                access += $"${a + Cpu.X:x6} = #${Read(a + Cpu.X):x2}";
                break;
            case BlockMove:
                data += $"${Read(pc + 2):x2},${Read(pc + 1):x2}";
                break;
            case DPIndexedIndirectX:
                a = Read(pc + 1);
                data += $"(${a:x2},x)";
                break;
            case DPIndexedX:
                a = Read(pc + 1);
                data += $"${a:x2},x";
                break;
            case DPIndexedY:
                a = Read(pc + 1);
                data += $"${a:x2},y";
                break;
            case DPIndirect:
                a = Read(pc + 1);
                data += $"(${a:x2})";
                break;
            case DPIndirectIndexedY:
                a = Read(pc + 1);
                data += $"(${a:x2}),y";
                break;
            case DPIndirectLong:
                a = Read(pc + 1);
                data += $"[${a:x2}]";
                a = ReadLong(a);
                access += $"${a:x6} = #${Read(a):x2}";
                break;
            case DPIndirectLongIndexedY:
                a = Read(pc + 1);
                data += $"[${a:x2}],y";
                a = ReadLong(a + Cpu.D);
                access += $"${a + Cpu.Y:x6} = #${Read(a + Cpu.Y):x2}";
                break;
            case DirectPage:
                a = Read(pc + 1);
                data += $"${a:x2}";
                access += $"${a:x4} = #${Read(a):x2}";
                break;
            case Immediate:
                //if (!MMode && op != 0xc2 && op != 0xe2)
                //{
                //    a = Read(pc + 1) | Read(pc + 2) << 8;
                //    data += $"#${a:x4}";
                //}
                //else
                //{
                a = Read(pc + 1);
                data += $"#${a:x2}";
                //}
                break;
            case ImmediateIndex:
                if (!Cpu.XMem)
                {
                    a = Read(pc + 1) | Read(pc + 2) << 8;
                    data += $"#${a:x4}";
                    size++;
                }
                else
                {
                    a = Read(pc + 1);
                    data += $"#${a:x2}";
                }
                break;
            case ImmediateMemory:
                if (!Cpu.MMem)
                {
                    a = Read(pc + 1) | Read(pc + 2) << 8;
                    data += $"#${a:x4}";
                    size++;
                }
                else
                {
                    a = Read(pc + 1);
                    data += $"#${a:x2}";
                }
                break;
            case ProgramCounterRelative:
                a = pc + (sbyte)Read(pc + 1) + 2;
                data += $"${a:x4}";
                break;
            case ProgramCounterRelativeLong:
                a = pc + (short)(ReadWord(pc + 1) + 3);
                data += $"${a:x6}";// [${a:X6}]";
                break;
            case SRIndirectIndexedY:
                a = Read(pc + 1);
                data += $"(${a:x2},s),y";
                break;
            case StackAbsolute:
                a = ReadWord(pc + 1);
                data += $"#${a:x4}";
                break;
            case StackDPIndirect:
                a = Read(pc + 1);
                data += $"${a:x2}";
                break;
            case StackInterrupt:
                data += $"$00";
                break;
            case StackPCRelativeLong:
                data += $"{pc + ReadWord(pc + 1) + 3:X6}";
                break;
            case StackRelative:
                a = Read(pc + 1);
                data += $"${a:x2},s";
                break;
            default:
                break;
        }

        if (getRegisters)
        {
            foreach (var r in Cpu.GetRegisters())
            {
                if (r.Value == "P" || r.Value == "PB") continue;
                var v = r.Value == "DB" ? $"{r.Value:x2}" : $"{r.Value:x4}";
                regtext += $"{r.Name.Replace(" ", "")}:{v.ToLower()} ";
            }

            string s = "";
            foreach (var f in Cpu.GetFlags())
            {
                if (f.Value == "E")
                    s += " ";

                var k = f.Value != "" ? f.Value : f.Value.ToLower();
                s += $"{k}";
            }
            //regtext += new string([.. s.Reverse()]) + " ";
            return ($"{data,-20} {regtext}", access, op, size);
        }
        return (data, access, op, size);
    }

    public void Log(int hpos)
    {
        if (!LogMain) return;
        if (Outfile != null && Outfile.BaseStream.CanWrite)
        {
            var (disasm, _, _, _) = Disassemble(Cpu.PBPC, true);
            Outfile.WriteLine($"{Cpu.PBPC:x6} {disasm,-13} {hpos}");
        }
    }

    public void LogSaOne(int hpos)
    {
        if (!LogSa1) return;
        if (Outfile != null && Outfile.BaseStream.CanWrite)
        {
            var (disasm, _, _, _) = Disassemble(Snes.Sa1.Cpu.PBPC, true);
            Outfile.WriteLine($"{Snes.Sa1.Cpu.PBPC:x6} {disasm,-13} {hpos}");
        }
    }

    public void Toggle(bool sa1)
    {
        if (sa1)
            LogSa1 = !LogSa1;
        else
            LogMain = !LogMain;

        if (LogMain || LogSa1)
            Outfile = new StreamWriter($"{Environment.CurrentDirectory}/trace{(LogSa1 ? ".sa1" : "")}.log");
        else
            Outfile?.Close();
    }

    private void Close()
    {
        LogMain = LogSa1 = false;
        Outfile?.Close();
    }

    public void Reset()
    {
        MMode = true;
        Close();
    }

    public override string ToString() => base.ToString();
}
