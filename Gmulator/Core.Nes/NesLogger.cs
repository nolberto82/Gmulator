
using static Gmulator.Core.Nes.NesCpu;

namespace Gmulator.Core.Nes;

public class NesLogger(Nes nes)
{
    private StreamWriter _outFile;
    public bool Logging;
    private readonly NesCpu Cpu = nes.Cpu;
    private readonly NesPpu Ppu = nes.Ppu;
    public Func<int, byte> ReadByte;

    public void Toggle(bool log = true)
    {
        if (!log)
            Logging = false;
        else
            Logging = !Logging;

        if (Logging)
            _outFile = new StreamWriter(Environment.CurrentDirectory + "\\trace.log");
        else
            _outFile?.Close();
    }

    public void OpenCloseLog()
    {
        Logging = !Logging;
        if (Logging)
            _outFile = new StreamWriter(Environment.CurrentDirectory + "\\trace.log");
        else
            _outFile?.Close();
    }

    public (string, string, int, int) Disassemble(int pc, bool getRegisters)
    {
        if (pc + 2 >= 0x10000)
            return ("", "", 0, 1);

        int x = Cpu.X;
        int y = Cpu.Y;

        int op = ReadByte(pc);
        int b0 = ReadByte(pc);
        int b1 = ReadByte(pc + 1);
        int b2 = ReadByte(pc + 2);

        string data = string.Empty;
        string regtext = string.Empty;
        string access = string.Empty;
        Opcode dops = Cpu.Disasm[op];

        string name = dops.Name;
        int size = dops.Size;
        int mode = dops.Mode;

        switch (mode)
        {
            case IMPL:
            case ACCU:
            {
                data = $"{name}";
                break;
            }
            case IMME:
            {
                data = $"{name} #${b1:X2}";
                break;
            }
            case ZERP:
            {
                data = $"{name} ${b1:X2}";// = ${Mem.ReadDebug(b1):X2}";
                break;
            }
            case ZERX:
            {
                data = $"{name} ${b1:X2},X [${(byte)(b1 + x):X4}]";// = ${Mem.ReadDebug((byte)(b1 + Cpu.X)):X2}";
                break;
            }
            case ZERY:
            {
                data = $"{name} ${b1:X2},Y [{(byte)(b1 + y):X2}]";// = {Mem.ReadDebug((byte)(b1 + Cpu.Y)):X2}";
                break;
            }
            case ABSO:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                if (op == 0x4c || op == 0x6c || op == 0x20)
                    data = $"{name} ${a:X4}";
                else
                {
                    if (IORegNames.ContainsKey(a))
                        data = $"{name} {IORegNames[a]}";
                    else
                        data = $"{name} ${a:X4}";
                }
                break;
            }
            case ABSX:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                if (IORegNames.ContainsKey(a))
                    data = $"{name} {IORegNames[a]}";
                else
                    data = $"{name} ${a:X4},X " +
                    $"[${(ushort)(a + x):X4}]";// = ${Mem.ReadDebug(a + Cpu.X):X2}";
                break;
            }
            case ABSY:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                if (IORegNames.ContainsKey(a))
                    data = $"{name} {IORegNames[a]}";
                else
                    data = $"{name} ${a:X4},Y " +
                    $"[${(ushort)(a + y):X4}]";// = {Mem.ReadDebug(a + Cpu.Y):X2}";
                break;
            }
            case INDX:
            {
                int lo = ReadByte(b1 + x & 0xff);
                int hi = ReadByte(b1 + 1 + x & 0xff);
                ushort a = (ushort)(lo | hi << 8);
                data = $"{name} (${b1:X2},X) @ {b1 + x & 0xff:X2} =" +
                    $" {a:X4}";// = {Mem.ReadDebug(a):X2}";
                break;
            }
            case INDY:
            {
                int lo = ReadByte(b1 & 0xff);
                int hi = ReadByte(b1 + 1 & 0xff);
                ushort a = (ushort)(lo | hi << 8);
                data = $"{name} (${b1:X2}),Y [${(ushort)(a + y):X4}]";// [{a:X4}]";// [{(ushort)(a + Cpu.Y):X4}]";// =" +
                                                                      //$" {Mem.ReadDebug((ushort)(a + Cpu.Y)):X2}";
                break;
            }
            case INDI:
            {
                ushort a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                int b = a;
                if ((a & 0xff) == 0xff) ++a;
                else a = (ushort)(ReadByte(pc + 1) | ReadByte(pc + 2) << 8);
                data = $"{name} (${b:X4}) = {a:X4}";
                break;
            }
            case RELA:
            {
                int a = pc + (sbyte)b1 + 2;
                data = $"{name} ${a:X4}";
                break;
            }
            default:
                data = $"UNDEFINED";
                break;
        }

        if (getRegisters)
        {
            if (data.Length > 9)
                data = data[9..];

            regtext = $"A:{Cpu.A:X2} X:{Cpu.X:X2} Y:{Cpu.Y:X2}" +
                $" S:{Cpu.SP:X2} P:{Cpu.PS:X2} Cyc:{Ppu.Totalcycles} Scan:{Ppu.Scanline,-3}";

            return ($"{data,-38} {regtext}", access, op, size);
        }
        else
            return (data, access, op, size);
    }

    internal void Log()
    {
        if (_outFile != null && _outFile.BaseStream.CanWrite)
        {
            var (disasm, _, _, _) = Disassemble(Cpu.PC, true);
            _outFile.WriteLine($"{Cpu.PC:X4}  {disasm,-26}");
        }
    }

    private void Close()
    {
        Logging = false;
        _outFile?.Close();
    }

    public void Reset() => Close();

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
}
