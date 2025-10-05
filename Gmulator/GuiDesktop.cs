using Gmulator.Core.Gbc;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace Gmulator;

internal class GuiDesktop : Gui
{
    public override void Run(bool isdeck)
    {
        base.Run(isdeck);
    }

    public override void Init(bool isdeck)
    {
        base.Init(isdeck);
    }

    public override void Reset(string name)
    {
        base.Reset(name);

#if DEBUG
        Emulator.Debug = true;
#endif

        Emulator?.Reset(name, false);
        Emulator?.Config?.Load();

        if (Emulator.Debug)
            Emulator.State = DebugState.Break;
        else
            Emulator.State = DebugState.Running;
    }

    private void DisableInputs()
    {
        var io = ImGui.GetIO();
        if ((io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad) > 0)
        {
            //GetPressedKey(io);
            unsafe
            {
                io.NativePtr->KeysData_121.Down = 0; //disable Circle/B button in Deck mode
            }
        }
    }

    private static void GetPressedKey(ImGuiIOPtr io)
    {
        for (int i = 0; i < io.KeysData.Count; i++)
        {
            if (io.KeysData[i].Down > 0)
                ImGui.Text($"{i}");
        }
    }
}
