using Raylib_cs;
using System.Data;

namespace Gmulator.Core.Gbc;
public class GbcJoypad
{
    private static byte status;
    public static byte Status
    {
        get => status;
        set
        {
            status = 0xef;
            if ((value & 0x10) == 0)
            {
                if (buttons[4])
                    status ^= 0x01;
                if (buttons[5])
                    status ^= 0x02;
                if (buttons[6])
                    status ^= 0x04;
                if (buttons[7])
                    status ^= 0x08;
            }
            else if ((value & 0x20) == 0)
            {
                if (buttons[0])
                    status ^= 0x01;
                if (buttons[1])
                    status ^= 0x02;
                if (buttons[2])
                    status ^= 0x04;
                if (buttons[3])
                    status ^= 0x08;
            }
        }
    }

    private static readonly bool[] buttons;
    public bool this[int i]
    {
        get => buttons[i];
        set => buttons[i] = value;
    }

    static GbcJoypad() => buttons = new bool[8];
    public static byte Read()
    {
        byte v = 0;
        if (Status == 0x10)
        {
            v = (byte)((Status >> 4) | 0xf0);
        }
        else if (Status == 0x20)
        {
            v = (byte)((Status & 0x0f) | 0xf0);
        }
        return (byte)(v | 0xdf);
    }
    public static bool[] GetButtons() => buttons;
}
