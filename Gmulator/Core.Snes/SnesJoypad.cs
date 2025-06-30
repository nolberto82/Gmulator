using Gmulator.Shared;
using Raylib_cs;

namespace Gmulator.Core.Snes
{
    public class SnesJoypad : IJoypad
    {
        private bool Strobe;
        private int ButtonId;

        public Action<int> SetJoy1L;
        public Action<int> SetJoy1H;

        private readonly Dictionary<int, KeyboardKey> Keys = new()
        {
            [0x00] = 0, [0x01] = 0, [0x02] = 0, [0x03] = 0,
            [0x04] = KeyboardKey.A, [0x05] = KeyboardKey.S,
            [0x06] = KeyboardKey.Z, [0x07] = KeyboardKey.B,
            [0x08] = KeyboardKey.Right, [0x09] = KeyboardKey.Left,
            [0x0a] = KeyboardKey.Down, [0x0b] = KeyboardKey.Up,
            [0x0c] = KeyboardKey.Enter, [0x0d] = KeyboardKey.Space,
            [0x0e] = KeyboardKey.C, [0x0f] = KeyboardKey.X,
        };

        private readonly Dictionary<int, GamepadButton> Pad = new()
        {
            [0x00] = 0, [0x01] = 0, [0x02] = 0, [0x03] = 0,
            [0x04] = GamepadButton.RightTrigger1, [0x05] = GamepadButton.LeftTrigger1,
            [0x06] = GamepadButton.RightFaceUp, [0x07] = GamepadButton.RightFaceRight,
            [0x08] = GamepadButton.LeftFaceRight, [0x09] = GamepadButton.LeftFaceLeft,
            [0x0a] = GamepadButton.LeftFaceDown, [0x0b] = GamepadButton.LeftFaceUp,
            [0x0c] = GamepadButton.MiddleRight, [0x0d] = GamepadButton.MiddleLeft,
            [0x0e] = GamepadButton.RightFaceLeft, [0x0f] = GamepadButton.RightFaceDown,
        };

        public const GamepadButton BtnA = GamepadButton.RightFaceRight;
        public const GamepadButton BtnB = GamepadButton.RightFaceDown;
        public const GamepadButton BtnSelect = GamepadButton.MiddleLeft;
        public const GamepadButton BtnStart = GamepadButton.MiddleRight;
        public const GamepadButton BtnRight = GamepadButton.LeftFaceRight;
        public const GamepadButton BtnLeft = GamepadButton.LeftFaceLeft;
        public const GamepadButton BtnUp = GamepadButton.LeftFaceUp;
        public const GamepadButton BtnDown = GamepadButton.LeftFaceDown;
        public const GamepadButton BtnL2 = GamepadButton.LeftTrigger2;
        public const GamepadButton BtnR2 = GamepadButton.RightTrigger2;
        public const GamepadButton BtnX = GamepadButton.RightFaceUp;
        public const GamepadButton BtnY = GamepadButton.RightFaceLeft;

        public bool[] Buttons { get; set; }

        public SnesJoypad()
        {
            Buttons = new bool[16];
        }

        public int Read(int min, int max)
        {
            int v = 0;
            for (int i = min; i < max; i++)
            {
                if (Buttons[i])
                    v |= (Buttons[i] ? 1 : 0) << i;
            }
            return (ushort)v;
        }

        public void AutoRead()
        {
            SetJoy1L((byte)Read(0, 8));
            SetJoy1H((byte)(Read(8, 16) >> 8));
        }

        public void Write(byte v)
        {
            if ((v & 1) > 0)
                Strobe = true;
        }

        public byte Read4016()
        {
            byte v = 0x40;
            if (ButtonId >= 0 && Strobe)
            {
                if (Buttons[ButtonId % 8])
                    v |= 0x01;

                if (++ButtonId > 7)
                {
                    Strobe = false;
                    ButtonId = 0;
                }
            }
            return (byte)(v | 1);
        }

        public void SetButtons(bool screenfocus)
        {
            for (int i = 0; i < Buttons.Length; i++)
            {
                if (screenfocus && Raylib.IsKeyDown(Keys[i]) || Raylib.IsGamepadButtonDown(0, Pad[i]))
                    Buttons[i] = true;
                else
                    Buttons[i] = false;
            }
        }
    }
}
