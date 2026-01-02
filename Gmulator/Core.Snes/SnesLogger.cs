using static Gmulator.Core.Snes.SnesCpu;

namespace Gmulator.Core.Snes;

public class SnesLogger(Snes snes)
{
    public bool Logging { get; private set; }

    private StreamWriter Outfile;
    private readonly Snes Snes = snes;
    private readonly SnesCpu Cpu = snes.Cpu;

    private bool MMode;
    private bool XMode;

    public (string, int, int) Disassemble(int pc, bool getregs, bool getbytes, bool isSa1=false)
    {
        var snes = Snes;
        var Cpu = isSa1 ? Snes.Sa1 : Snes.Cpu;
        int op = snes.ReadOp(pc);
        int a, c, x = 0;
        string w = string.Empty;

        Opcode dops = Cpu.Disasm[op];

        string name = dops.Name;
        int size = dops.Size;
        int mode = dops.Mode;
        string oper = dops.Oper;
        string bytedata = "";

        string data = $"{dops.Name} ";
        string regtext = string.Empty;

        int[] b = new int[size];

        for (int i = 0; i < b.Length; i++)
            b[i] = Snes.ReadOp(pc + i);

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
                a = snes.ReadWord(pc + 1);
                if (op == 0x20 || op == 0x4c || op == 0x6c)
                    data += $"${a:x4}";
                else
                    data += $"${a:x4}";
                break;
            case AbsoluteIndexedIndirect:
                c = snes.ReadWord(pc + 1);
                a = snes.ReadWord((ushort)(c + Cpu.X));
                data += $"(${c:x4},x)";// [${Cpu.PB:X2}{a:X4}] ";
                break;
            case AbsoluteIndexedX:
                c = snes.ReadWord(pc + 1);
                a = c + (Cpu.XMem ? (byte)Cpu.X : Cpu.X);
                data += $"${c:x4},x";// [${Cpu.DB << 16 | a:X6}] ";
                break;
            case AbsoluteIndexedY:
                c = snes.ReadWord(pc + 1);
                a = Cpu.DB << 16 | c + (Cpu.XMem ? (byte)Cpu.Y : Cpu.Y);
                data += $"${c:x4},y";// [${Cpu.DB << 16 | a:X6}] ";
                break;
            case AbsoluteIndirect:
                a = snes.ReadWord(pc + 1);
                data += $"$({a:x4})";// [${Cpu.PB << 16 | snes.ReadWord(a):X6}]";
                break;
            case AbsoluteIndirectLong:
                a = snes.ReadWord(pc + 1);
                data += $"[${a:x4}]";// [${snes.ReadLong(a):X6}]";
                break;
            case AbsoluteLong:
                a = snes.ReadLong(pc + 1);
                data += $"${a:x6}";
                break;
            case AbsoluteLongIndexedX:
                c = snes.ReadLong(pc + 1);
                data += $"${c:x6},x";// [${c + Cpu.X:X6}]";
                break;
            case BlockMove:
                data += $"${snes.ReadOp(pc + 2):x2},${snes.ReadOp(pc + 1):x2}";
                break;
            case DPIndexedIndirectX:
                c = snes.ReadOp(pc + 1);
                x = (c + Cpu.D + Cpu.X) & 0xffff;
                x = Cpu.E ? 0x100 : x;
                a = Cpu.DB << 16 | snes.ReadOp(x);
                a |= Cpu.DB << 16 | snes.ReadOp((x & 0xff) == 0xff ? x & 0xff00 : x + 1) << 8;
                data += $"(${c:x2},x)";// [{a:X6}]";
                break;
            case DPIndexedX:
                c = snes.ReadOp(pc + 1);
                a = (c + Cpu.D + Cpu.X) & 0xffff;
                a = Cpu.E && a > 0xff ? a & 0xff | 0x100 : a;
                data += $"${c:x2},x";// [${a:X6}]";
                break;
            case DPIndexedY:
                c = snes.ReadOp(pc + 1);
                a = (ushort)(c + Cpu.D + Cpu.Y);
                data += $"${c:x2},y";// [${a:X6}]";
                break;
            case DPIndirect:
                a = snes.ReadOp(pc + 1);
                data += $"(${a:x2})";
                break;
            case DPIndirectIndexedY:
                a = snes.ReadOp(pc + 1);
                data += $"(${a:x2}),y";// [${Cpu.DB << 16 | snes.ReadWord(a) + Cpu.Y:X6}]";
                break;
            case DPIndirectLong:
                c = snes.ReadOp(pc + 1);
                a = snes.ReadLong((c + Cpu.D) & 0xffff);
                data += $"[${c:x2}]";// [${a:X6}]";
                break;
            case DPIndirectLongIndexedY:
                c = snes.ReadOp(pc + 1);
                a = snes.ReadLong(Cpu.D + c) + Cpu.Y;
                data += $"[${c:x2}],y";// [${a:X6}]";
                break;
            case DirectPage:
                c = snes.ReadOp(pc + 1);
                a = (snes.ReadOp(pc + 1) + Cpu.D) & 0xffff;
                data += $"${c:x2}";// [${a:X6}]";
                break;
            case Immediate:
                a = snes.ReadOp(pc + 1);
                data += $"#${a:x2}";

                //if (op == 0xe2 && b[1].GetBit(4))
                //    XMode = false;
                // if (op == 0xc2 && b[1].GetBit(5))
                //     MMode = true;
                break;
            case ImmediateIndex:
                if (!Cpu.XMem)
                {
                    c = snes.ReadOp(pc + 2);
                    a = snes.ReadWord(pc + 1);
                    data += $"#${a:x4}";
                    size++;
                }
                else
                {
                    a = snes.ReadOp(pc + 1);
                    data += $"#${a:x2}";
                }
                break;
            case ImmediateMemory:
                if (!Cpu.MMem)
                {
                    c = snes.ReadOp(pc + 2);
                    a = snes.ReadWord(pc + 1);
                    data += $"#${a:x4}";
                    size++;
                }
                else
                {
                    a = snes.ReadOp(pc + 1);
                    data += $"#${a:x2}";
                }
                break;
            case ProgramCounterRelative:
                a = pc + (sbyte)snes.ReadOp(pc + 1) + 2;
                data += $"${a:x4}";
                break;
            case ProgramCounterRelativeLong:
                a = pc + (short)(snes.ReadWord(pc + 1) + 3);
                data += $"${a:x6}";// [${a:X6}]";
                break;
            case SRIndirectIndexedY:
                c = snes.ReadOp(pc + 1);
                a = snes.ReadWord(c + Cpu.SP + Cpu.Y);
                data += $"(${c:x2},s),y";// [${a:X6}]";
                break;
            case StackAbsolute:
                c = snes.ReadWord(pc + 1);
                data += $"#${c:x4}";
                break;
            case StackDPIndirect:
                c = snes.ReadOp(pc + 1); pc++;
                data += $"${c:x2}";// [${(ushort)(Cpu.SP + Cpu.D):X6}]";
                break;
            case StackInterrupt:
                data += $"$00";
                break;
            case StackPCRelativeLong:
                data += $"{pc + snes.ReadWord(pc + 1) + 3:X6}";
                break;
            case StackRelative:
                c = snes.ReadOp(pc + 1);
                data += $"${c:x2},s";// [${c + Cpu.SP:X6}]";
                break;
            default:
                break;
        }

        if (getregs)
        {
            foreach (var r in Cpu.GetRegisters())
            {
                if (r.Value == "P" || r.Value == "PB") continue;
                var v = r.Value == "DB" ? $"{r.Value:x2}" : $"{r.Value:x4}";
                regtext += $"{r.Value}:{v.ToLower()} ";
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
        }
        return (data, op, size);
    }

    public void Log(int hpos)
    {
        if (!Logging) return;
        if (Outfile != null && Outfile.BaseStream.CanWrite)
        {
            var bank = Cpu.PB << 16;
            var pc = bank | Cpu.PC;
            var (disasm, _, _) = Disassemble(pc, true, true);
            Outfile.WriteLine($"{bank | pc:x6} {disasm,-13} {hpos}");
        }
    }

    public void Toggle()
    {
        Logging = !Logging;
        if (Logging)
            Outfile = new StreamWriter(Environment.CurrentDirectory + "\\trace.log");
        else
            Outfile?.Close();
    }

    private void Close()
    {
        Logging = false;
        Outfile?.Close();
    }

    public void Reset()
    {
        MMode = true;
        Close();
    }

    public override string ToString() => base.ToString();
}
