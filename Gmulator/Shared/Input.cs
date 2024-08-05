using Raylib_cs;

namespace Gmulator;
internal class Input
{
    private static bool[] Buttons;
    public static bool RotateAB { get; set; }

    public static void Init(bool[] buttons) => Buttons = buttons;
    public static void Update(int system, uint framecount)
    {
        Buttons[0] = Raylib.IsKeyDown(KbA);
        Buttons[1] = Raylib.IsKeyDown(KbB);
        Buttons[2] = Raylib.IsKeyDown(KbSelect);
        Buttons[3] = Raylib.IsKeyDown(KbStart);
        if (system == NesSystem)
        {
            Buttons[4] = Raylib.IsKeyDown(KbUp);
            Buttons[5] = Raylib.IsKeyDown(KbDown);
            Buttons[6] = Raylib.IsKeyDown(KbLeft);
            Buttons[7] = Raylib.IsKeyDown(KbRight);
        }
        else
        {
            Buttons[4] = Raylib.IsKeyDown(KbRight);
            Buttons[5] = Raylib.IsKeyDown(KbLeft);
            Buttons[6] = Raylib.IsKeyDown(KbUp);
            Buttons[7] = Raylib.IsKeyDown(KbDown);
        }

        Buttons[0] |= Raylib.IsKeyPressedRepeat(KbX);
        Buttons[1] |= Raylib.IsKeyPressedRepeat(KbY);

        if (Raylib.IsGamepadAvailable(0))
        {
            if (RotateAB)
            {
                Buttons[0] |= Raylib.IsGamepadButtonDown(0, BtnB);
                Buttons[1] |= Raylib.IsGamepadButtonDown(0, BtnY);
                //turbo
                if (Raylib.IsGamepadButtonDown(0, BtnA) && framecount % 2 == 0)
                    Buttons[0] = true;
                if (Raylib.IsGamepadButtonDown(0, BtnX) && framecount % 2 == 0)
                    Buttons[1] = true;
            }
            else
            {
                Buttons[0] |= Raylib.IsGamepadButtonDown(0, BtnA);
                Buttons[1] |= Raylib.IsGamepadButtonDown(0, BtnB);

                //turbo
                if (Raylib.IsGamepadButtonDown(0, BtnX) && framecount % 2 == 0)
                    Buttons[0] = true;
                if (Raylib.IsGamepadButtonDown(0, BtnY) && framecount % 2 == 0)
                    Buttons[1] = true;
            }

            Buttons[2] |= Raylib.IsGamepadButtonDown(0, BtnSelect);
            Buttons[3] |= Raylib.IsGamepadButtonDown(0, BtnStart);
            if (system == NesSystem)
            {
                Buttons[4] |= Raylib.IsGamepadButtonDown(0, BtnUp);
                Buttons[5] |= Raylib.IsGamepadButtonDown(0, BtnDown);
                Buttons[6] |= Raylib.IsGamepadButtonDown(0, BtnLeft);
                Buttons[7] |= Raylib.IsGamepadButtonDown(0, BtnRight);
            }
            else
            {
                Buttons[4] |= Raylib.IsGamepadButtonDown(0, BtnRight);
                Buttons[5] |= Raylib.IsGamepadButtonDown(0, BtnLeft);
                Buttons[6] |= Raylib.IsGamepadButtonDown(0, BtnUp);
                Buttons[7] |= Raylib.IsGamepadButtonDown(0, BtnDown);
            }

        }
    }
}
