using Gmulator.Core.Gbc;
using static Gmulator.Core.Snes.SnesCpu;

namespace Gmulator.Core.Snes;

public class SnesLogger(Snes snes, SnesCpu cpu)
{
    public bool Logging { get; internal set; }

    internal Func<Dictionary<string, bool>> GetFlags;
    internal Func<Dictionary<string, string>> GetRegs;
    private StreamWriter Outfile;
    private Snes Snes = snes;
    private SnesCpu Cpu = cpu;

    private bool MMode;
    private bool XMode;

    public (string, int, int) Disassemble(int pc, bool getregs, bool getbytes)
    {
        var snes = Snes;
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

        if (cpu.MMem)
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
                a = snes.ReadWord((ushort)(c + cpu.X));
                data += $"(${c:x4},x)";// [${Cpu.PB:X2}{a:X4}] ";
                break;
            case AbsoluteIndexedX:
                c = snes.ReadWord(pc + 1);
                a = c + (cpu.XMem ? (byte)cpu.X : cpu.X);
                data += $"${c:x4},x";// [${Cpu.DB << 16 | a:X6}] ";
                break;
            case AbsoluteIndexedY:
                c = snes.ReadWord(pc + 1);
                a = cpu.DB << 16 | c + (cpu.XMem ? (byte)cpu.Y : cpu.Y);
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
                x = (c + cpu.D + cpu.X) & 0xffff;
                x = cpu.E ? 0x100 : x;
                a = cpu.DB << 16 | snes.ReadOp(x);
                a |= cpu.DB << 16 | snes.ReadOp((x & 0xff) == 0xff ? x & 0xff00 : x + 1) << 8;
                data += $"(${c:x2},x)";// [{a:X6}]";
                break;
            case DPIndexedX:
                c = snes.ReadOp(pc + 1);
                a = (c + cpu.D + cpu.X) & 0xffff;
                a = cpu.E && a > 0xff ? a & 0xff | 0x100 : a;
                data += $"${c:x2},x";// [${a:X6}]";
                break;
            case DPIndexedY:
                c = snes.ReadOp(pc + 1);
                a = (ushort)(c + cpu.D + cpu.Y);
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
                a = snes.ReadLong((c + cpu.D) & 0xffff);
                data += $"[${c:x2}]";// [${a:X6}]";
                break;
            case DPIndirectLongIndexedY:
                c = snes.ReadOp(pc + 1);
                a = snes.ReadLong(cpu.D + c) + cpu.Y;
                data += $"[${c:x2}],y";// [${a:X6}]";
                break;
            case DirectPage:
                c = snes.ReadOp(pc + 1);
                a = (snes.ReadOp(pc + 1) + cpu.D) & 0xffff;
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
                if (!cpu.XMem)
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
                if (!cpu.MMem)
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
                a = snes.ReadWord(c + cpu.SP + cpu.Y);
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
            foreach (var r in cpu.GetRegisters())
            {
                if (r.Key == "P" || r.Key == "PB") continue;
                var v = r.Key == "DB" ? $"{r.Value:x2}" : $"{r.Value:x4}";
                regtext += $"{r.Key}:{v.ToLower()} ";
            }

            string s = "";
            foreach (var f in GetFlags())
            {
                if (f.Key == "E")
                    s += " ";

                var k = f.Value ? f.Key : f.Key.ToLower();
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

    public void Close()
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
