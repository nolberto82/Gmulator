using Gmulator.Ui;
using ImGuiNET;
using Raylib_cs;
using System.Runtime.CompilerServices;

namespace Gmulator.Shared;

internal class Input
{
    private static bool[] Buttons;
    public static Action<bool> SetButtons;
    private static float OldRightThumbY;

    public static void Init(bool[] buttons) => Buttons = buttons;

    public static void UpdateGuiInput(Emulator emu, Gui menu)
    {
        if (emu == null) return;
        if (!Raylib.IsWindowFocused()) return;

        var newrightstickY = Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightY);
        if (newrightstickY > -0.1f && newrightstickY < 0.1f) newrightstickY = 0.0f; //Deadzone

        if (menu?.Opened == false)
        {
            if (emu.State == DebugState.Paused)
                emu.State = DebugState.Running;

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
        }

        if (newrightstickY < 0 && OldRightThumbY == 0)
            emu.SaveState(0, 0);
        else if (newrightstickY > 0 && OldRightThumbY == 0)
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

        OldRightThumbY = newrightstickY;
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
