using Raylib_cs;

namespace Gmulator.Core.Nes;
public class NesJoypad
{
    private bool Strobe;
    private int ButtonId;

    public NesJoypad()
    {
        buttons = new bool[8];
    }

    public void Write(byte v)
    {
        if ((v & 1) > 0)
            Strobe = true;
    }

    public byte Read()
    {
        byte v = 0x40;
        if (ButtonId >= 0 && Strobe)
        {
            if (buttons[ButtonId % 8])
                v |= 0x01;

            if (++ButtonId > 7)
            {
                Strobe = false;
                ButtonId = 0;
            }
        }
        return v;
    }

    private static bool[] buttons;
    public bool this[int i]
    {
        get => buttons[i];
        set => buttons[i] = value;
    }
    public static bool[] GetButtons() => buttons;
}