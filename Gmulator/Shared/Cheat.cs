using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using System;
using System.Text.Json;

namespace Gmulator.Shared;

public class Cheat
{
    public string Description;
    public string Codes;
    public int Address;
    public byte Compare { get; set; }
    public byte Value { get; set; }
    public int Type { get; set; }
    public int Bank { get; set; }
    public bool Enabled { get; set; }
    public string Filename { get; private set; }

    public Cheat() { }
    public Cheat(string description, int address, byte value, byte compare, int type, bool enabled, string codes)
    {
        Description = description == "" ? "Cheat" : description;
        Address = address;
        Value = value;
        Compare = compare;
        Enabled = enabled;
        Codes = codes;
        Type = type;
        if (type == ProAction)
            Bank = compare;
    }

    public class RawCode(int address, byte compare, byte value, int type, bool enabled)
    {
        public int Address { get; set; } = address;
        public byte Compare { get; set; } = compare;
        public byte Value { get; set; } = value;
        public int Type { get; set; } = type;
        public bool Enabled { get; set; } = enabled;
    }

    public (int, byte, byte, int, int) DecryptCode(string c, Emulator Emu)
    {
        switch (Emu)
        {
            case Gbc:
            {
                if (c.Length == 9)
                {
                    if (c.Replace("\r", "").ToUpper().All("01234567890ABCDEF".Contains))
                    {
                        var addr = Convert.ToUInt16($"{c[5]}{c[2]}{c[3]}{c[4]}", 16) ^ 0xf000;
                        byte cmp = (byte)(Convert.ToByte($"{c[6]}{c[8]}", 16).Ror(2) ^ 0xba);
                        var val = Convert.ToByte(c[..2], 16);
                        return (addr, cmp, val, GameGenie, GbcConsole);
                    }
                }
                else if (c.Length == 8)
                {
                    var addr = Convert.ToUInt16($"{c.Substring(6, 2)}{c.Substring(4, 2)}", 16);
                    var cmp = Convert.ToByte($"{c[..2]}", 16);
                    var val = Convert.ToByte($"{c.Substring(2, 2)}", 16);
                    return (addr, cmp, val, ProAction, GbcConsole);
                }
                return (-1, 0, 0, -1, GbcConsole);
            }
            case Nes:
            {
                if (c.Length == 8)
                {
                    if (c.Replace("\r", "").ToUpper().All("APZLGITYEOXUKSVN".Contains))
                    {
                        List<int> r = [];
                        foreach (var l in c)
                            r.Add("APZLGITYEOXUKSVN".IndexOf(l));

                        var addr = (r[3] & 7) << 12 | (r[5] & 7) << 8 |
                        (r[2] & 7) << 4 | r[4] & 7 | (r[4] & 8) << 8 |
                        (r[1] & 8) << 4 | r[3] & 8 | 0x8000;
                        var val = (byte)((r[1] & 7) << 4 | (r[0] & 8) << 4 |
                        r[0] & 7);
                        byte cmp = 0;
                        if (c.Length == 8)
                        {
                            cmp = (byte)((r[7] & 7) << 4 | (r[6] & 8) << 4 |
                            r[6] & 7 | r[5] & 8);
                            val += (byte)(r[7] & 8);
                        }
                        else
                            val += (byte)(r[5] & 8);

                        return (addr, cmp, val, GameGenie, NesConsole);
                    }
                }
                else if (c.Length == 7)
                {
                    var addr = Convert.ToInt16(c[..4], 16);
                    var val = Convert.ToByte(c.Substring(5, 2), 16);
                    return (addr, 0, val, ProAction, NesConsole);
                }
                break;
            }
            case Snes:
            {
                if (c.Length == 9 && c.ToLowerInvariant().StartsWith("7e") || c.ToLowerInvariant().StartsWith("7f"))
                {
                    var addr = Convert.ToInt32(c[..6], 16);
                    var val = Convert.ToByte(c.Substring(7, 2), 16);
                    return (addr, 0, val, ProAction, SnesConsole);
                }
                else if (c.Length == 9 && c.Contains(':'))
                {
                    var addr = Convert.ToInt32(c[..6], 16);
                    var val = Convert.ToByte(c.Substring(7, 2), 16);
                    return (addr, 0, val, GameGenie, SnesConsole);
                }
                else if (c.Replace("\r", "").ToUpper().All("0123456789ABCDEF".Contains))
                {
                    List<byte> r = [];
                    int d = 0;
                    for (int i = 0; i < c.Length; i++)
                    {
                        d <<= 4;
                        d |= ((byte)"DF4709156BC8A23E".IndexOf(c[i]));
                    }

                    byte val = (byte)(d >> 24);
                    var addr = (d >> 10) & 0xc | (d >> 10) & 3;
                    var temp = (d >> 2) & 0xc | (d >> 2) & 0x3;
                    addr <<= 4; addr |= temp;
                    temp = (d >> 20) & 0xf;
                    addr <<= 4; addr |= temp;
                    temp = (d << 2) & 0xc | (d >> 14) & 3;
                    addr <<= 4; addr |= temp;
                    temp = (d >> 16) & 0xf;
                    addr <<= 4; addr |= temp;
                    temp = (d >> 6) & 0xc | (d >> 6) & 3;
                    addr <<= 4; addr |= temp;

                    return (addr, 0, val, GameGenie, SnesConsole);
                }
                break;
            }
        }
        return (-1, 0, 0, -1, NoConsole);
    }
}
