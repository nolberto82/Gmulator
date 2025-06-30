using ImGuiNET;
using Raylib_cs;
using System.Runtime.CompilerServices;

namespace Gmulator;
internal class Input
{
    private static bool[] Buttons;
    public static Action<bool> SetButtons;
    private static float OldRightThumbY;

    public static void Init(bool[] buttons) => Buttons = buttons;
    public static void Update(Emulator emu, int system, uint framecount)
    {
        if (system == SnesConsole)
        {
            SetButtons(emu.IsScreenWindow);
            return;
        }

        if (emu.IsScreenWindow)
        {
            Buttons[0] = Raylib.IsKeyDown(KbA);
            Buttons[1] = Raylib.IsKeyDown(KbB);
            Buttons[2] = Raylib.IsKeyDown(KbSelect);
            Buttons[3] = Raylib.IsKeyDown(KbStart);
            if (system == NesConsole)
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
        }

        if (Raylib.IsGamepadAvailable(0))
        {
            if (emu.Config.RotateAB > 0)
            {
                Buttons[0] |= Raylib.IsGamepadButtonDown(0, BtnB);
                Buttons[1] |= Raylib.IsGamepadButtonDown(0, BtnY);
                //turbo
                if (Raylib.IsGamepadButtonDown(0, BtnA) && framecount % emu?.Config.FrameSkip == 0)
                    Buttons[0] = true;
                if (Raylib.IsGamepadButtonDown(0, BtnX) && framecount % emu?.Config.FrameSkip == 0)
                    Buttons[1] = true;
            }
            else
            {
                Buttons[0] |= Raylib.IsGamepadButtonDown(0, BtnA);
                Buttons[1] |= Raylib.IsGamepadButtonDown(0, BtnB);

                //turbo
                if (Raylib.IsGamepadButtonDown(0, BtnX) && framecount % emu?.Config.FrameSkip == 0)
                    Buttons[0] = true;
                if (Raylib.IsGamepadButtonDown(0, BtnY) && framecount % emu?.Config.FrameSkip == 0)
                    Buttons[1] = true;
            }

            Buttons[2] |= Raylib.IsGamepadButtonDown(0, BtnSelect);
            Buttons[3] |= Raylib.IsGamepadButtonDown(0, BtnStart);
            if (system == NesConsole)
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

    public static void UpdateGuiInput(Emulator emu, Menu menu, bool isdeck)
    {
        DisableInputs();
        if (emu == null) return;
        if (!Raylib.IsWindowFocused()) return;

        var NewRightStickY = Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightY);
        if (NewRightStickY > -0.1f && NewRightStickY < 0.1f) NewRightStickY = 0.0f; //Deadzone

        if (Raylib.IsGamepadButtonPressed(0, BtnL2))
            menu?.Open(emu);

        if (menu?.Opened == false)
        {
            if (emu.State == Paused)
                emu.State = Running;
        }

        if (Raylib.IsGamepadButtonDown(0, GamepadButton.RightTrigger2) && !emu.FastForward)
        {
            emu.FastForward = true;
            Raylib.SetTargetFPS(0);
            Raylib.ClearWindowState(ConfigFlags.VSyncHint);
        }
        else if (!Raylib.IsGamepadButtonDown(0, GamepadButton.RightTrigger2) && emu.FastForward)
        {
            emu.FastForward = false;
            Raylib.SetTargetFPS(60);
            Raylib.SetWindowState(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
        }

        if (NewRightStickY < 0 && OldRightThumbY == 0)
            emu.SaveState(0, 0);
        else if (NewRightStickY > 0 && OldRightThumbY == 0)
            emu.LoadState(0, 0);

        var shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        if (shift)
        {
            var slot = GetSaveStateSlot();
            if (slot > -1)
                emu?.SaveState(slot, 0);
        }
        else
        {
            foreach (var k in SaveStateKeys)
            {
                if (Raylib.IsKeyPressed(k.Key))
                    emu.LoadState(k.Value, 0);
            }
        }

        OldRightThumbY = NewRightStickY;
    }

    private static void DisableInputs()
    {
        var io = ImGui.GetIO();
        if ((io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad) > 0)
        {
            //GetKeysPressed(io);
            unsafe
            {
                io.NativePtr->KeysData_121.Down = 0; //disable Circle/B button in Deck mode
            }
        }
    }

    private static int GetSaveStateSlot()
    {
        var slot = SaveStateKeys.Keys.FirstOrDefault(m => m.Equals(SaveStateKeys[m]));
        foreach (var key in SaveStateKeys)
        {
            if (Raylib.IsKeyPressed(key.Key))
                return key.Value;
        }
        return -1;
    }

    private static unsafe void GetKeysPressed(ImGuiIOPtr io)
    {
        RangeAccessor<ImGuiKeyData> KeysData = new(&io.NativePtr->KeysData_0, 154);
        for (int i = 0; i < KeysData.Count; i++)
        {
            if (KeysData[i].Down > 0)
                ImGui.Text($"{i}");
        }
    }
}
