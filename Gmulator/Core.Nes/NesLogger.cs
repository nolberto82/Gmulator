using System.IO;
using System.Reflection.Metadata.Ecma335;
using static GNes.Core.NesCpu;

namespace GNes.Core;
public class NesLogger
{
    private static StreamWriter outFile;
    public bool Logging;
    private readonly NesCpu Cpu;
    public NesPpu Ppu;

    public Func<int, byte> ReadByte;

    public NesLogger(NesCpu cpu)
    {
        Cpu = cpu;
    }

    public void Reset() => Logging = false;
    public void Toggle(bool log = true)
    {
        if (!log)
            Logging = false;
        else
            Logging = !Logging;

        if (Logging)
            Outfile = new StreamWriter(Environment.CurrentDirectory + "\\trace.log");
        else
        {
            if (Outfile != null)
                Outfile.Close();
        }
    }

    public void OpenCloseLog()
    {
        Logging = !Logging;
        if (Logging)
            outFile = new StreamWriter(Environment.CurrentDirectory + "\\trace.log");
        else
            outFile?.Close();
    }

    public DisasmEntry Disassemble(int bank, int pc, bool get_registers, bool getbytes)
    {
        if (pc + 2 >= 0x10000)
            return new DisasmEntry(pc, "", "", "", "", 0);

        byte op = ReadByte(pc);
        byte b0 = ReadByte(pc);
        byte b1 = ReadByte(pc + 1);
        byte b2 = ReadByte(pc + 2);

        string data = string.Empty;
        string regtext = string.Empty;

        Opcode dops = Cpu.Disasm[op];

        string name = dops.Name;
        int size = dops.Size;
        int mode = dops.Mode;

        switch (mode)
        {
            case IMPL:
            case ACCU:
            {
                data = $"{b0,-8:X2} {name}";
                break;
            }
            case IMME:
            {
                data = $"{b0:X2} {b1,-5:X2} {name} #${b1:X2}";
                break;
            }
            case ZERP:
            {
                data = $"{b0:X2} {b1,-5:X2} {name} ${b1:X2}";// = ${Mem.ReadDebug(b1):X2}";
                break;
            }
            case ZERX:
            {
                int a = ReadByte(pc + 1);
                data = $"{b0:X2} {b1,-5:X2} {name} ${b1:X2},X [${(byte)(b1 + Cpu.X):X4}]";// = ${Mem.ReadDebug((byte)(b1 + Cpu.X)):X2}";
                break;
            }
            case ZERY:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                data = $"{b0:X2} {b1,-5:X2} {name} ${b1:X2},Y [{(byte)(b1 + Cpu.Y):X2}]";// = {Mem.ReadDebug((byte)(b1 + Cpu.Y)):X2}";
                break;
            }
            case ABSO:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                if (op == 0x4c || op == 0x6c || op == 0x20)
                    data = $"{b0:X2} {b1:X2} {b2:X2} {name} ${a:X4}";
                else
                {
                    if (IORegNames.ContainsKey(a))
                        data = $"{b0:X2} {b1:X2} {b2:X2} {name} {IORegNames[a]}";
                    else
                        data = $"{b0:X2} {b1:X2} {b2:X2} {name} ${a:X4}";
                }
                break;
            }
            case ABSX:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                if (IORegNames.ContainsKey(a))
                    data = $"{b0:X2} {b1:X2} {b2:X2} {name} {IORegNames[a]}";
                else
                    data = $"{b0:X2} {b1:X2} {b2:X2} {name} ${a:X4},X " +
                    $"[${(ushort)(a + Cpu.X):X4}]";// = ${Mem.ReadDebug(a + Cpu.X):X2}";
                break;
            }
            case ABSY:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                if (IORegNames.ContainsKey(a))
                    data = $"{b0:X2} {b1:X2} {b2:X2} {name} {IORegNames[a]}";
                else
                    data = $"{b0:X2} {b1:X2} {b2:X2} {name} ${a:X4},Y " +
                    $"[${(ushort)(a + Cpu.Y):X4}]";// = {Mem.ReadDebug(a + Cpu.Y):X2}";
                break;
            }
            case INDX:
            {
                byte lo = ReadByte(b1 + Cpu.X & 0xff);
                byte hi = ReadByte(b1 + 1 + Cpu.X & 0xff);
                ushort a = (ushort)(lo | hi << 8);
                data = $"{b0:X2} {b1,-5:X2} {name} (${b1:X2},X) @ {b1 + Cpu.X & 0xff:X2} =" +
                    $" {a:X4}";// = {Mem.ReadDebug(a):X2}";
                break;
            }
            case INDY:
            {
                byte lo = ReadByte(b1 & 0xff);
                byte hi = ReadByte(b1 + 1 & 0xff);
                ushort a = (ushort)(lo | hi << 8);
                data = $"{b0:X2} {b1,-5:X2} {name} (${b1:X2}),Y [${(ushort)(a + Cpu.Y):X4}]";// [{a:X4}]";// [{(ushort)(a + Cpu.Y):X4}]";// =" +
                                                                                             //$" {Mem.ReadDebug((ushort)(a + Cpu.Y)):X2}";
                break;
            }
            case INDI:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                int b = a;
                if ((a & 0xff) == 0xff) ++a;
                else a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                data = $"{b0:X2} {b1:X2} {b2:X2} {name} (${b:X4}) = {a:X4}";
                break;
            }
            case RELA:
            {
                int a = pc + (sbyte)b1 + 2;
                data = $"{b0:X2} {b1,-5:X2} {name} ${a:X4}";
                break;
            }
            default:
                data = $"UNDEFINED";
                break;
        }

        if (get_registers)
        {
            string btext = "";
            if (data.Length > 9)
            {
                btext = $" {data[..9]}";
                data = data[9..];
            }

            regtext = $"A:{Cpu.A:X2} X:{Cpu.X:X2} Y:{Cpu.Y:X2}" +
                $" S:{Cpu.SP:X2} P:{Cpu.Ps:X2} Cyc:{Ppu.Totalcycles} Scan:{Ppu.Scanline,-3}";

            return new(pc, data, name, data, regtext, size, btext);
        }
        else
            return new(pc, data, name, data, regtext, size);
    }

    internal void Log(ushort pc)
    {
        if (Outfile != null && Outfile.BaseStream.CanWrite)
        {
            bool gamedoctor = false;
            DisasmEntry e = Disassemble(0, pc, true, false);
            if (gamedoctor)
                Outfile.WriteLine($"{e.regtext}");
            else
                Outfile.WriteLine($"{e.pc:X4}  {e.disasm,-26} {e.regtext.ToUpper()}");
        }
    }

    private readonly Dictionary<int, string> IORegNames = new()
    {
        [0x4000] = "SQ1_VOL",
        [0x4001] = "SQ1_SWEEP",
        [0x4002] = "SQ1_LO",
        [0x4003] = "SQ1_HI",
        [0x4004] = "SQ2_VOL",
        [0x4005] = "SQ2_SWEEP",
        [0x4006] = "SQ2_LO",
        [0x4007] = "SQ2_HI",
        [0x4008] = "TRI_LINEAR",
        [0x4009] = "",
        [0x400A] = "TRI_LO",
        [0x400B] = "TRI_HI",
        [0x400C] = "NOISE_VOL",
        [0x400D] = "",
        [0x400E] = "NOISE_LO",
        [0x400F] = "NOISE_HI",
        [0x4010] = "DMC_FREQ",
        [0x4011] = "DMC_RAW",
        [0x4012] = "DMC_START",
        [0x4013] = "DMC_LEN",
        [0x4014] = "OAMDMA",
        [0x4015] = "SND_CHN",
        [0x4016] = "JOY1",
        [0x4017] = "JOY2",
    };
    internal Func<Dictionary<string, bool>> OnGetFlags;
    internal Func<Dictionary<string, byte>> OnGetRegs;
    private StreamWriter Outfile;
}
