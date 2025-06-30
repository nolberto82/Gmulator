using static Gmulator.Core.Gbc.GbcCpu;

namespace Gmulator.Core.Gbc;
public class GbcLogger
{
    public StreamWriter Outfile { get; private set; }
    public bool Logging { get; private set; }

    public delegate byte Read(int a);
    public event Read ReadByte;
    public Func<Dictionary<string, bool>> GetFlags;

    public delegate Dictionary<string, byte> GetRegs();
    public event GetRegs OnGetRegs;

    public GbcLogger()
    {
    }

    public void LogToFile(ushort pc)
    {
        if (Outfile != null && Outfile.BaseStream.CanWrite)
        {
            bool gamedoctor = false;
            DisasmEntry e = Disassemble(0, pc, true);
            if (gamedoctor)
                Outfile.WriteLine($"{e.regtext}");
            else
                Outfile.WriteLine($"{e.pc:X4}  {e.disasm,-26} {e.regtext.ToUpper()}");
        }
    }

    public void Toggle()
    {
        Logging = !Logging;
        if (Logging == true)
            Outfile = new StreamWriter(Environment.CurrentDirectory + "\\trace.log");
        else
            Outfile?.Close();
    }

    public void Reset()
    {
        Logging = false;
        Outfile?.Close();
    }

    public DisasmEntry Disassemble(int bank, int pc, bool get_registers, bool gamedoctor = false)
    {
        string data = string.Empty;
        string bytes = string.Empty;
        string regtext = string.Empty;
        byte b1 = ReadByte(pc + 1);
        byte b2 = ReadByte(pc + 2);
        byte b3 = ReadByte(pc + 3);

        Opcode d;
        byte op = ReadByte(pc);
        if (op == 0xcb)
            d = OpInfoCB[ReadByte(pc + 1)];
        else
            d = OpInfo00[op];

        if (d.Size == 1)
        {
            if (d.Oper.Contains("n/a"))
                data = $"{d.Name}";
            else
            {
                if (d.Oper.Contains(','))
                    data = $"{d.Name} {d.Oper.Insert(d.Oper.IndexOf(',') + 1, " ")}";
                else
                    data = $"{d.Name} {d.Oper}";
            }
            bytes = $"{op:X2}";
        }
        else if (d.Size == 2)
        {
            if (d.Name.Contains('j'))
            {
                ushort offset = (ushort)(pc + (sbyte)b1 + 2);
                if (d.Oper != string.Empty)
                    data = $"{d.Name} {d.Oper} ${offset.ToString(d.Format)}";
                else
                    data = $"{d.Name} ${offset.ToString(d.Format)}";
            }
            else if (d.Name == "ldh")
            {
                if (op == 0xe0)
                    data = $"{d.Name} (${(0xff00 + b1):x4}), a";
                else
                    data = $"{d.Name} a, (${(0xff00 + b1):x4})";
            }
            else if (op == 0xe2)
                data = $"{d.Name} ($ff00+c), a";
            else
            {
                if (d.Oper != string.Empty)
                {
                    if (op == 0xf8)
                        data = $"{d.Name} {d.Oper.Insert(3, " ")}${b1:x2}";
                    else
                        data = $"{d.Name} {d.Oper} ${b1.ToString(d.Format)}";
                }
                else
                    data = $"{d.Name} ${b1.ToString(d.Format)}";
            }

            bytes = $"{op:X2} {b1:X2}";
        }

        else if (d.Size == 3)
        {
            if (d.Oper != string.Empty)
            {
                if (op == 0xea)
                    data = $"{d.Name} (${(b2 << 8 | b1).ToString(d.Format)}), {d.Oper}";
                else if (op == 0xfa)
                    data = $"{d.Name} a, (${(b2 << 8 | b1).ToString(d.Format)})";
                else
                    data = $"{d.Name} {d.Oper} ${(b2 << 8 | b1).ToString(d.Format)}";
            }
            else
                data = $"{d.Name} ${(b2 << 8 | b1).ToString(d.Format)}";
            bytes = $"{op:X2} {b1:X2} {b2:X2}";
        }
        else if (d.Size == 4)
        {
            if (d.Oper.Contains("u16"))
            {
                d.Oper = d.Oper.Replace("u16", $"${b3:X2}{b2:X2}");
                data = $"{d.Name,-4} {d.Oper}";
            }
            else if (d.Oper.Contains(",d8"))
            {
                d.Oper = d.Oper.Replace(",d8", $",${b3:X2}");
                d.Oper = d.Oper.Replace("u8", $"${b2:X2}");
                data = $"{d.Name,-4} {d.Oper}";
            }
            bytes = $"{op:X2} {b1:X2} {b2:X2} {b3:X2} ";
        }

        if (d.Name.Contains("pre"))
        {
            d.Size = 1;
            data = $"pre{"",-5}";
            bytes = $"{op:X2}";
        }

        if (get_registers)
        {
            if (gamedoctor)
            {
                //bytes = $"{op:X2},{b1:X2},{b2:X2},{b3:X2}";
                //regtext = $"A:{Cpu.A:X2} F:{Cpu.F:X2} B:{Cpu.B:X2} C:{Cpu.C:X2} " +
                //          $"D:{Cpu.D:X2} E:{Cpu.E:X2} H:{Cpu.H:X2} L:{Cpu.L:X2} " +
                //          $"SP:{Cpu.SP:X4} PC:{pc:X4} " +
                //          $"PCMEM:{bytes}";
            }
            else
            {
                //bytes = $"{op:X2},{b1:X2},{b2:X2},{b3:X2}";
                var Regs = OnGetRegs();
                var F = GetFlags();
                char[] flags =
                [
                    F["Z"] ? 'Z' : 'z',
                    F["N"] ? 'N' : 'n',
                    F["H"] ? 'H' : 'h',
                    F["C"] ? 'C' : 'c'
                ];

                foreach (var v in Regs.Values)
                {
                    regtext += v;
                }

                //regtext = $"{R["A"]:X2} B:{Cpu.B:X2} C:{Cpu.C:X2} D:{Cpu.D:X2} " +
                //           $"E:{Cpu.E:X2} F:{new(flags)} H:{Cpu.H:X2} L:{Cpu.L:X2} " +
                //          $"SP:{Cpu.SP:X4}";// Cy:{GbSys.ppu.Cycle + 2}";
                //regtext = emu.ppu.Cycle > 0 ? $"Cy:{Program.emu.ppu.Cycle + 2}" : "Cy:0";
            }
        }
        data = $"{bytes,-8} {data}";
        return new(pc, data, d.Name, d.Oper, regtext, d.Size, bytes);
    }
}
