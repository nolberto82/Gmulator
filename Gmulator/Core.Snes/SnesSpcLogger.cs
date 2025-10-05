using static Gmulator.Core.Snes.SnesSpc;

namespace Gmulator.Core.Snes;

public class SnesSpcLogger(SnesCpu cpu, SnesSpc spc, SnesApu apu)
{
    private SnesCpu Cpu;
    private SnesSpc Spc;
    private SnesApu Apu;
    public bool Logging { get; private set; }
    public StreamWriter Outfile { get; private set; }
    public Func<ushort> GetDp;

    public void SetSnes(Snes snes, SnesCpu cpu, SnesSpc spc, SnesApu apu)
    {
        Cpu = cpu;
        Spc = spc;
        Apu = apu;
    }

    private readonly Dictionary<int, string> Labels = new()
    {
        [0xf0] = "TEST", [0xf1] = "CONTROL", [0xf2] = "DSPADDR", [0xf3] = "DSPDATA",
        [0xf4] = "CPUIO0", [0xf5] = "CPUI1", [0xf6] = "CPUI2", [0xf7] = "CPUI3",
        [0xf8] = "RAMREG1", [0xf9] = "RAMREG2", [0xfa] = "T0TARGET", [0xfb] = "T1TARGET",
        [0xfc] = "T2TARGET", [0xfd] = "T0OUT", [0xfe] = "T1OUT", [0xff] = "T2OUT",
    };

    public (string, int, int) Disassemble(int pc, bool getregs, bool getbytes)
    {
        byte op = Apu.Read(pc, true);

        List<DisasmEntry> entry = [];
        Opcode dops = Spc.Disasm[op];

        string name = dops.Name;
        int size = dops.Size;

        string data = string.Empty;

        byte[] b = new byte[size];
        for (int i = 0; i < b.Length; i++)
            b[i] = Spc.Read(pc + i, true);

        pc++;
        string oper = dops.Oper.ToLower();
        string format = dops.Format;

        for (int i = 0; i < b.Length; i++)
        {
            if (i == size - 1)
            {
                if (size == 1)
                {
                    if (getbytes)
                        data += $"{b[0],-8:X2}  ";
                    data += $"{name} ";
                    data += oper;
                }
                else if (size == 2)
                {
                    if (getbytes)
                        data += $"{b[1],-5:X2}  ";
                    data += $"{name} ";
                    if (name.StartsWith("b"))
                        data += string.Format(oper, pc + (sbyte)b[1] + 1);
                    else if (name.StartsWith("db"))
                    {
                        if (op == 0xfe)
                            data += string.Format(oper, b[1], pc + (sbyte)b[1] + 1);
                        else
                            data += string.Format(oper, b[1], pc + (sbyte)b[1] + 2);
                    }

                    else
                        data += string.Format(oper, b[1]);
                }
                else if (size == 3)
                {
                    if (getbytes)
                        data += $"{b[2]:X2}  ";
                    data += $"{name} ";
                    if (name.StartsWith("db"))
                        data += string.Format(oper, b[1], pc + (sbyte)b[2] + 2);
                    else if (oper.Contains("${0:x4}."))
                    {
                        int a = b[2] << 8 | b[1];
                        data += string.Format(oper, a & 0x1fff, a >> 13);
                    }
                    else if (oper.Contains("${0:x4}"))
                        data += string.Format(oper, b[2] << 8 | b[1]);
                    else if (oper.Contains("${1:x4}"))
                        data += string.Format(oper, b[1], pc + b[2] + 2);
                    else
                        data += string.Format(oper, b[2], b[1]);

                    if (Labels.TryGetValue(b[1], out string l))
                    {
                        data = data.Replace($"${b[1]:X4}".ToLower(), l);
                    }
                }
            }
            else
            {
                if (getbytes)
                    data += $"{b[i]:X2} ";
            }

        }

        //if (dops.Name == "mov")
        //{
        //    if (size == 1) { }
        //    else if (size == 2)
        //        data += $" [${Snes.Spc.Read(b[1], true):X2}]";
        //    else if (size == 3) { }
        //}

        string regtext = string.Empty;

        if (getregs)
        {
            var r = Spc.GetRegs();
            regtext = $" A:{r["A"]} X:{r["X"]} Y:{r["Y"]} S:{r["S"]} P:";
            string s = "";
            foreach (var f in Spc.GetFlags())
            {
                if (f.Key == "E")
                    break;

                var k = f.Value ? f.Key : f.Key.ToLower();
                s += $"{k}";
            }
            regtext += new string([.. s.Reverse()]) + " ";
        }

        return (data, op, size);
    }

    internal void Log(int pc)
    {
        if (!Logging) return;
        if (Outfile != null && Outfile.BaseStream.CanWrite)
        {
            var (disasm, op, size) = Disassemble(pc, true, false);
            Outfile.WriteLine($"{pc:X4}  {disasm,-31}");
        }
    }

    public void Toggle()
    {
        Logging = !Logging;
        if (Logging)
            Outfile = new StreamWriter(Environment.CurrentDirectory + "\\trace-spc.log");
        else
            Outfile?.Close();
    }

    public void Close()
    {
        Logging = false;
        Outfile?.Close();
    }

    public void Reset() => Close();

    public ushort ReadWord(int a) => (ushort)(Spc.Read(a, true) | Spc.Read(a + 1, true) << 8);
}
