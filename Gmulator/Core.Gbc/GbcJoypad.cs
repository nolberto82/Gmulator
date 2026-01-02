using Gmulator.Shared;
using Raylib_cs;
using System.Data;

namespace Gmulator.Core.Gbc;

public class GbcJoypad
{
    private readonly Dictionary<int, KeyboardKey> Keys = new()
    {
        [0x00] = KbA, [0x01] = KbB, [0x02] = KbSelect, [0x03] = KbStart,
        [0x04] = KbRight, [0x05] = KbLeft, [0x06] = KbUp, [0x07] = KbDown,
    };

    private readonly Dictionary<int, GamepadButton> Pad = new()
    {
        [0x00] = BtnA, [0x01] = BtnB, [0x02] = BtnSelect, [0x03] = BtnStart,
        [0x04] = BtnRight, [0x05] = BtnLeft, [0x06] = BtnUp, [0x07] = BtnDown,
    };

    private readonly bool[] _buttons;

    public GbcJoypad(Gbc gbc)
    {
        _buttons = new bool[8];
        gbc.SetMemory(0x00, 0x00, 0xff00, 0xff00, 0xffff, Read00, Write00, RamType.Register, 1);
    }

    private int Read00(int a) => Status;
    private void Write00(int a, int v)
    {
        Status = (byte)v;
        //IF |= IntJoypad;
    }

    private byte _status;
    public byte Status
    {
        get => _status;
        set
        {
            _status = 0xef;
            if ((value & 0x10) == 0)
            {
                if (_buttons[4])
                    _status ^= 0x01;
                if (_buttons[5])
                    _status ^= 0x02;
                if (_buttons[6])
                    _status ^= 0x04;
                if (_buttons[7])
                    _status ^= 0x08;
            }
            else if ((value & 0x20) == 0)
            {
                if (_buttons[0])
                    _status ^= 0x01;
                if (_buttons[1])
                    _status ^= 0x02;
                if (_buttons[2])
                    _status ^= 0x04;
                if (_buttons[3])
                    _status ^= 0x08;
            }
        }
    }

    public byte Read()
    {
        byte v = 0;
        if (_status == 0x10)
            v = (byte)((_status >> 4) | 0xf0);
        else if (_status == 0x20)
            v = (byte)((_status & 0x0f) | 0xf0);

        return (byte)(v | 0xdf);
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
                _buttons[0] = true;
            if (Raylib.IsGamepadButtonDown(0, BtnY))
                _buttons[1] = true;
        }
    }
}
