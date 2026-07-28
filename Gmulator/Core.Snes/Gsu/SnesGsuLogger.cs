namespace Gmulator.Core.Snes.Gsu;

public class SnesGsuLogger(Snes snes)
{
    private StreamWriter _outFile;
    private bool _skipNop;

    private SnesGsu Gsu => snes.Gsu;
    public bool Logging { get; private set; }

    public (string, string, int, int) Disassemble(int pc, bool getregs)
    {
        int op = Gsu.ReadDebug(pc);
        switch (op)
        {
            case 0x00: return ("stop", "", op, 1);
            case 0x01: return ("nop", "", op, 1);
            case 0x02: return ("cache", "", op, 1);
            case 0x03: return ("lsr", "", op, 1);
            case 0x04: return ("rol", "", op, 1);
            case 0x05: return ($"bra ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x06: return ($"bge ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x07: return ($"blt ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x08: return ($"bne ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x09: return ($"beq ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x0a: return ($"bpl ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x0b: return ($"bmi ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x0c: return ($"bcc ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x0d: return ($"bcs ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x0e: return ($"bvc ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case 0x0f: return ($"bvs ${pc + (sbyte)Gsu.ReadDebug(pc + 1) + 2:x6}", "", op, 2);
            case >= 0x10 and <= 0x1f:
                if (Gsu.Prefix)
                    return ($"move r{op & 0x0f}", "", op, 1);
                else
                    return ($"to r{op & 0x0f}", "", op, 1);
            case >= 0x20 and <= 0x2f:
                return ($"with r{op & 0x0f}", "", op, 1);
            case >= 0x30 and <= 0x3b:
                if (Gsu.Alt1)
                    return ($"stb (r{op & 0x0f})", "", op, 1);
                else
                    return ($"stw (r{op & 0x0f})", "", op, 1);
            case 0x3c: return ($"loop", "", op, 1);
            case 0x3d or 0x3e or 0x3f: return ($"alt{op & 3}", "", op, 1);
            case >= 0x40 and <= 0x4b:
                if (Gsu.Alt1)
                    return ($"ldb (r{op & 0x0f})", "", op, 1);
                else
                    return ($"ldw (r{op & 0x0f})", "", op, 1);
            case 0x4c:
                if (Gsu.Alt1)
                    return ($"rpix", "", op, 1);
                else
                    return ($"plot", "", op, 1);
            case 0x4d: return ($"swap", "", op, 1);
            case 0x4e:
                if (Gsu.Alt1)
                    return ($"cmode", "", op, 1);
                else
                    return ($"color", "", op, 1);
            case 0x4f: return ($"not", "", op, 1);
            case >= 0x50 and <= 0x5f:
                if (Gsu.Alt3)
                    return ($"adc #{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt1)
                    return ($"adc r{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt2)
                    return ($"add #{op & 0x0f}", "", op, 1);
                else
                    return ($"add r{op & 0x0f}", "", op, 1);

            case >= 0x60 and <= 0x6f:
            {
                if (Gsu.Alt3)
                    return ($"cmp r{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt1)
                    return ($"sbc r{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt2)
                    return ($"sub #{op & 0x0f}", "", op, 1);
                else
                    return ($"sub r{op & 0x0f}", "", op, 1);
            }

            case 0x70: return ("merge", "", op, 1);
            case >= 0x71 and <= 0x7f:
            {
                if (Gsu.Alt3)
                    return ($"bic #{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt2)
                    return ($"and #{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt1)
                    return ($"bic r{op & 0x0f}", "", op, 1);
                else
                    return ($"and r{op & 0x0f}", "", op, 1);
            }
            case >= 0x80 and <= 0x8f:
            {
                if (Gsu.Alt3)
                    return ($"umult #{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt1)
                    return ($"umult r{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt2)
                    return ($"umult #{op & 0x0f}", "", op, 1);
                else
                    return ($"mult r{op & 0x0f}", "", op, 1);
            }
            case 0x90: return ("sbk", "", op, 1);
            case >= 0x91 and <= 0x94: return ($"link #{op & 7}", "", op, 1);
            case 0x95: return ("sex", "", op, 1);
            case 0x96:
            {
                if (Gsu.Alt1)
                    return ("div2", "", op, 2);
                else
                    return ("asr", "", op, 1);
            }
            case 0x97: return ("ror", "", op, 1);
            case >= 0x98 and <= 0x9d: return ($"jmp r{op & 0x0f}", "", op, 1);
            case 0x9e: return ("lob", "", op, 1);
            case 0x9f:
                if (Gsu.Alt1)
                    return ("lmult", "", op, 1);
                else
                    return ("fmult", "", op, 1);
            case >= 0xa0 and <= 0xaf:
            {
                int lo = Gsu.ReadDebug(pc + 1);
                if (Gsu.Alt3)
                    return ($"ibt r{op & 0x0f},#${lo:x2}", "", op, 2);
                else if (Gsu.Alt2)
                    return ($"sms r{op & 0x0f},(${lo * 2:x2})", "", op, 2);
                else if (Gsu.Alt1)
                    return ($"lms r{op & 0x0f},(${lo * 2:x2})", "", op, 2);
                else
                    return ($"ibt r{op & 0x0f},#${lo:x2}", "", op, 2);
            }
            case >= 0xb0 and <= 0xbf:
                if (Gsu.Prefix)
                    return ($"moves r{op & 0x0f}", "", op, 1);
                else
                    return ($"from r{op & 0x0f}", "", op, 1);
            case 0xc0: return ("hib", "", op, 1);

            case >= 0xc1 and <= 0xcf:
            {
                if (Gsu.Alt3)
                    return ($"xor #{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt1)
                    return ($"xor r{op & 0x0f}", "", op, 1);
                else if (Gsu.Alt2)
                    return ($"xor #{op & 0x0f}", "", op, 1);
                else
                    return ($"or r{op & 0x0f}", "", op, 1);
            }
            case >= 0xd0 and <= 0xde: return ($"inc r{op & 0x0f}", "", op, 1);

            case 0xdf:
                if (!Gsu.Alt2)
                    return ("getc", "", op, 1);
                if (!Gsu.Alt1)
                    return ("ramb", "", op, 1);
                else
                    return ("romb", "", op, 1);

            case >= 0xe0 and <= 0xee: return ($"dec r{op & 0x0f}", "", op, 1);
            case 0xef:
            {
                if (Gsu.Alt3)
                    return ("getbs", "", op, 1);
                else if (Gsu.Alt2)
                    return ("getbl", "", op, 1);
                else if (Gsu.Alt1)
                    return ("getbh", "", op, 1);
                else
                    return ("getb", "", op, 1);
            }
            case >= 0xf0 and <= 0xff:
            {
                int lo = Gsu.ReadDebug(pc + 1);
                int hi = Gsu.ReadDebug(pc + 2);
                if (Gsu.Alt3)
                    return ($"iwt r{op & 0x0f},#${hi << 8 | lo:x4}", "", op, 3);
                else if (Gsu.Alt2)
                    return ($"sm r{op & 0x0f},(${hi << 8 | lo:x4})", "", op, 3);
                else if (Gsu.Alt1)
                    return ($"lm r{op & 0x0f},(${hi << 8 | lo:x4})", "", op, 3);
                else
                    return ($"iwt r{op & 0x0f},#${hi << 8 | lo:x4}", "", op, 3);
            }
        }

        return ("", "", op, 1);
    }

    public void Log(int hpos)
    {
        if (_outFile != null && _outFile.BaseStream.CanWrite)
        {
            if (_skipNop && Gsu.ReadDebug(Gsu.PC) == 0x01)
            {
                _skipNop = false;
                return;
            }

            var (disasm, _, _, _) = Disassemble(Gsu.PC, true);
            if (disasm == "stop")
                _skipNop = true;
            
            string regstr = string.Empty;
            string flagstr = string.Empty;
            var registers = Gsu.GetRegisters();
            var misc = Gsu.GetMisc();
            var flags = Gsu.GetFlagsArray();
            for (int i = 0; i < registers.Count; i++)
                regstr += $"{registers[i].Name}:{registers[i].Value} ";

            for (int i = 0; i < flags.Count; i++)
                flagstr += flags[i].Value ? $"{flags[i].Name}" : $"{flags[i].Address}";

            string srcstr = $"{misc[0].Name.ToUpper()}:{misc[0].Value:X2}";
            string dststr = $"{misc[1].Name.ToUpper()}:{misc[1].Value:X2}";

            _outFile.WriteLine($"{Gsu.CurrentPC:X6}  {disasm,-30} B:{misc[6].Value:X2} C:{misc[5].Value:X2} {srcstr} {dststr} {regstr}SFR:{flagstr}".TrimEnd());
        }
    }

    public void Toggle()
    {
        Logging = !Logging;
        _skipNop = false;
        if (Logging)
            _outFile = new StreamWriter($"{Environment.CurrentDirectory}/trace.gsu.log");
        else
            _outFile?.Close();
    }

    private void Close()
    {
        _outFile?.Close();
    }

    public void Reset()
    {
        Logging = false;
        _skipNop = false;
        Close();
    }
}
