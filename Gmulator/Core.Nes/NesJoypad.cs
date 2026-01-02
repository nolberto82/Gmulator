using Raylib_cs;

namespace Gmulator.Core.Nes;

public class NesJoypad()
{
    private bool Strobe;
    private int ButtonId;
    private readonly bool[] _buttons = new bool[8];

    private readonly Dictionary<int, KeyboardKey> Keys = new()
    {
        [0x00] = KbA, [0x01] = KbB, [0x02] = KbSelect, [0x03] = KbStart,
        [0x04] = KbUp, [0x05] = KbDown, [0x06] = KbLeft, [0x07] = KbRight,
    };

    private readonly Dictionary<int, GamepadButton> Pad = new()
    {
        [0x00] = BtnA, [0x01] = BtnB, [0x02] = BtnSelect, [0x03] = BtnStart,
        [0x04] = BtnUp, [0x05] = BtnDown, [0x06] = BtnLeft, [0x07] = BtnRight,
    };

    public void Write(int a = 0, int v = 0)
    {
        if ((v & 1) > 0)
            Strobe = true;
    }

    public int Read(int a = 0)
    {
        byte v = 0x40;
        if (ButtonId >= 0 && Strobe)
        {
            if (_buttons[ButtonId & 7])
                v |= 0x01;

            if (++ButtonId > 7)
            {
                Strobe = false;
                ButtonId = 0;
            }
        }
        return v;
    }

    public void Update(bool screenfocus, uint frame)
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (screenfocus && Raylib.IsKeyDown(Keys[i]) || Raylib.IsGamepadButtonDown(0, Pad[i]))
                _buttons[i] = true;
            else
                _buttons[i] = false;
        }

        if (frame % 2 == 0)
        {
            if (Raylib.IsGamepadButtonDown(0, BtnX))
                _buttons[1] = true;
        }

    }
}