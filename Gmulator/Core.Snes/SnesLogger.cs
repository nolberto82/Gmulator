using static Gmulator.Core.Snes.SnesCpu;

namespace Gmulator.Core.Snes
{
    public class SnesLogger(Snes snes)
    {
        public bool Logging { get; internal set; }

        internal Func<Dictionary<string, bool>> GetFlags;
        internal Func<Dictionary<string, string>> GetRegs;
        private StreamWriter Outfile;
        private readonly Snes Snes = snes;

        public bool MMode { get; internal set; }
        public bool XMode { get; internal set; }

        public DisasmEntry Disassemble(int bank, int pc, bool getregs, bool getbytes)
        {
            var Cpu = Snes.Cpu;
            //pc = bank | pc;
            byte op = Snes.ReadOp(pc);
            int a = 0;
            int c, x;
            string w = string.Empty;

            string data = string.Empty;
            string regtext = string.Empty;

            Opcode dops = Cpu.Disasm[op];

            string name = dops.Name;
            int size = dops.Size;
            int mode = dops.Mode;
            string oper = dops.Oper;
            string bytedata = "";

            byte[] b = new byte[size];

            for (int i = 0; i < b.Length; i++)
                b[i] = Snes.ReadOp(bank | pc + i);

            foreach (var n in b)
                bytedata += $"{n:x2} ";

            if (op == 0xe2)
            {
                XMode = b[1].GetBit(4);
                MMode = b[1].GetBit(5);
            }

            if (op == 0xc2)
            {
                XMode = !b[1].GetBit(4);
                MMode = !b[1].GetBit(5);
            }

            if (Cpu.MMem)
                MMode = true;

            switch (mode)
            {
                case Absolute:
                    a = Snes.ReadWord(pc + 1);
                    if (op == 0x20 || op == 0x4c || op == 0x6c)
                        data += $"${a:x4}";// [${Cpu.PB << 16 | a:X6}]";
                    else
                        data += $"${a:x4}";// [${Cpu.DB << 16 | a:X6}]";
                    break;
                case AbsoluteIndexedIndirect:
                    c = Snes.ReadWord(pc + 1);
                    a = Snes.ReadWord((ushort)(c + Cpu.X));
                    data = $"(${c:x4},x)";// [${Cpu.PB:X2}{a:X4}] ";
                    break;
                case AbsoluteIndexedX:
                    c = Snes.ReadWord(pc + 1);
                    a = c + (Cpu.XMem ? (byte)Cpu.X : Cpu.X);
                    data = $"${c:x4},x";// [${Cpu.DB << 16 | a:X6}] ";
                    break;
                case AbsoluteIndexedY:
                    c = Snes.ReadWord(pc + 1);
                    a = Cpu.DB << 16 | c + (Cpu.XMem ? (byte)Cpu.Y : Cpu.Y);
                    data += $"${c:x4},y";// [${Cpu.DB << 16 | a:X6}] ";
                    break;
                case AbsoluteIndirect:
                    a = Snes.ReadWord(pc + 1);
                    data += $"$({a:x4})";// [${Cpu.PB << 16 | Snes.ReadWord(a):X6}]";
                    break;
                case AbsoluteIndirectLong:
                    a = Snes.ReadWord(pc + 1);
                    data += $"[${a:x4}]";// [${Snes.ReadLong(a):X6}]";
                    break;
                case AbsoluteLong:
                    a = Snes.ReadLong(pc + 1);
                    data += $"${a:x6}";
                    break;
                case AbsoluteLongIndexedX:
                    c = Snes.ReadLong(pc + 1);
                    data += $"${c:x6},x";// [${c + Cpu.X:X6}]";
                    break;
                case BlockMove:
                    data += $"${Snes.ReadOp(pc + 2):x2},${Snes.ReadOp(pc + 1):x2}";
                    break;
                case DPIndexedIndirectX:
                    c = Snes.ReadOp(pc + 1);
                    x = (ushort)(c + Cpu.D + Cpu.X);
                    x = Cpu.E ? 0x100 : x;
                    a = Cpu.DB << 16 | Snes.ReadOp(x);
                    a |= Cpu.DB << 16 | Snes.ReadOp((x & 0xff) == 0xff ? x & 0xff00 : x + 1) << 8;
                    data += $"(${c:x2},x)";// [{a:X6}]";
                    break;
                case DPIndexedX:
                    c = Snes.ReadOp(pc + 1);
                    a = (ushort)(c + Cpu.D + Cpu.X);
                    a = Cpu.E && a > 0xff ? a & 0xff | 0x100 : a;
                    data += $"${c:x2},x";// [${a:X6}]";
                    break;
                case DPIndexedY:
                    c = Snes.ReadOp(pc + 1);
                    a = (ushort)(c + Cpu.D + Cpu.Y);
                    data += $"${c:x2},y";// [${a:X6}]";
                    break;
                case DPIndirect:
                    a = Snes.ReadOp(pc + 1);
                    data += $"(${a:x2})";
                    break;
                case DPIndirectIndexedY:
                    a = Snes.ReadOp(pc + 1);
                    data += $"(${a:x2}),y";// [${Cpu.DB << 16 | Snes.ReadWord(a) + Cpu.Y:X6}]";
                    break;
                case DPIndirectLong:
                    c = Snes.ReadOp(pc + 1);
                    a = Snes.ReadLong((ushort)(c + Cpu.D));
                    data += $"[${c:x2}]";// [${a:X6}]";
                    break;
                case DPIndirectLongIndexedY:
                    c = Snes.ReadOp(pc + 1);
                    a = Snes.ReadLong(Cpu.D + c) + Cpu.Y;
                    data = $"[${c:x2}],y";// [${a:X6}]";
                    break;
                case DirectPage:
                    c = Snes.ReadOp(pc + 1);
                    a = (ushort)(Snes.ReadOp(pc + 1) + Cpu.D);
                    data += $"${c:x2}";// [${a:X6}]";
                    break;
                case Immediate:
                    a = Snes.ReadOp(pc + 1);
                    data += $"#${a:x2}";

                    //if (op == 0xe2 && b[1].GetBit(4))
                    //    XMode = false;
                    // if (op == 0xc2 && b[1].GetBit(5))
                    //     MMode = true;
                    break;
                case ImmediateIndex:
                    if (!Cpu.XMem)
                    {
                        c = Snes.ReadOp(pc + 2);
                        a = Snes.ReadWord(pc + 1);
                        data = $"#${a:x4}";
                        size++;
                    }
                    else
                    {
                        a = Snes.ReadOp(pc + 1);
                        data = $"#${a:x2}";
                    }
                    break;
                case ImmediateMemory:
                    if (!Cpu.MMem)
                    {
                        c = Snes.ReadOp(pc + 2);
                        a = Snes.ReadWord(pc + 1);
                        data += $"#${a:x4}";
                        size++;
                    }
                    else
                    {
                        a = Snes.ReadOp(pc + 1);
                        data += $"#${a:x2}";
                    }
                    break;
                case ProgramCounterRelative:
                    a = pc + (sbyte)Snes.ReadOp(pc + 1) + 2;
                    data += $"${a:x4}";
                    break;
                case ProgramCounterRelativeLong:
                    a = pc + (short)(Snes.ReadWord(pc + 1) + 3);
                    data += $"${a:x6}";// [${a:X6}]";
                    break;
                case SRIndirectIndexedY:
                    c = Snes.ReadOp(pc + 1);
                    a = Snes.ReadWord(c + Cpu.SP + Cpu.Y);
                    data += $"(${c:x2},s),y";// [${a:X6}]";
                    break;
                case StackAbsolute:
                    c = Snes.ReadWord(pc + 1);
                    data += $"#${c:x4}";
                    break;
                case StackDPIndirect:
                    c = Snes.ReadOp(pc + 1); pc++;
                    data += $"${c:x2}";// [${(ushort)(Cpu.SP + Cpu.D):X6}]";
                    break;
                case StackInterrupt:
                    data += $"$00";
                    break;
                case StackPCRelativeLong:
                    data += $"{pc + Snes.ReadWord(pc + 1) + 3:X6}";
                    break;
                case StackRelative:
                    c = Snes.ReadOp(pc + 1);
                    data += $"${c:x2},s";// [${c + Cpu.SP:X6}]";
                    break;
                default:
                    break;
            }

            if (getregs)
            {
                foreach (var r in Cpu.GetRegs())
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
                regtext += new string([.. s.Reverse()]) + " ";
            }
            return new(bank | pc, $"{name} {data}", name, "", regtext, size, getbytes ? bytedata : "");
        }

        public void Log(int bank, int pc)
        {
            if (!Logging) return;
            if (Outfile != null && Outfile.BaseStream.CanWrite)
            {
                var e = Disassemble(bank, pc, true, true);
                Outfile.WriteLine($"{bank | e.pc:x6} {e.disasm,-13} {e.regtext}{Snes.Ppu.HPos}");
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
}
