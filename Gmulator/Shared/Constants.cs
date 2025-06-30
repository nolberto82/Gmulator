using ImGuiNET;
using Raylib_cs;
using System.Numerics;

namespace Gmulator;
public static class Constants
{
    public const int FC = 0x10;
    public const int FH = 0x20;
    public const int FN = 0x40;
    public const int FZ = 0x80;

    public const int IntVblank = 0x01;
    public const int IntLcd = 0x02;
    public const int IntTimer = 0x04;
    public const int IntSerial = 0x08;
    public const int IntJoypad = 0x10;

    public const int Break = 0;
    public const int StepMain = 1;
    public const int StepSpc = 2;
    public const int StepOverMain = 3;
    public const int StepOverSpc = 4;
    public const int Running = 5;
    public const int Paused = 6;
    public const int Stopped = 7;

    public const int Horizontal = 3;
    public const int Vertical = 2;
    public const int SingleNt0 = 0;
    public const int SingleNt1 = 1;

    public const int ScreenWidth = 1280;
    public const int ScreenHeight = 900;
    public const int DeckWidth = 1280;
    public const int DeckHeight = 800;

    public const int NesCpuClock = 1789773;
    public const int NesCyclesPerFrame = 29780;
    public const int GbcCpuClock = 4194304;
    public const int GbcCycles = 70224;

    public const int NesNtscCpuClock = 1789773;
    public const int NesPalCpuClock = 1662607;
    public const int NesNtscCycles = 29780;
    public const int NesPalCycles = 33247;

    public const int Fps = 60;

    public const int GbWidth = 160;
    public const int GbHeight = 144;
    public const int NesWidth = 256;
    public const int NesHeight = 240;
    public const int SnesWidth = 256;
    public const int SnesHeight = 240;

    public const int GbcAudioFreq = 44100;
    public const int NesAudioFreq = 44100;
    public const int SnesAudioFreq = 44100;

    public const int SnesMaxSamples = 4096;

    public const double apuCyclesPerMaster = (32040 * 32) / (1364 * 262 * 60.0);

    public const int GameGenie = 0;
    public const int GameShark = 1;
    public const int Raw = 2;

    public const int NoConsole = -1;
    public const int GbcConsole = 0;
    public const int NesConsole = 1;
    public const int SnesConsole = 2;

    public const int DisasmMaxLines = 15;

    public static readonly Vector2 ButtonSize = new(60, 0);
    public static readonly Vector4 RED = new(1, 0, 0, 1);
    public static readonly Vector4 GREEN = new(0, 1, 0, 1);
    public static readonly Vector4 BLUE = new(0, 0, 1, 1);
    public static readonly Vector4 WHITE = new(1, 1, 1, 1);
    public static readonly Vector4 YELLOW = new(1, 1, 0, 1);
    public static readonly Vector4 GRAY = new(128 / 255f, 128 / 255f, 128 / 255f, 1);
    public static readonly Vector4 DEFCOLOR = new(0.260f, 0.590f, 0.980f, 0.400f);
    public const ImGuiInputTextFlags HexInputFlags = ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase;
    public const ImGuiWindowFlags NoScrollFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    public const ImGuiWindowFlags DockFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoCollapse |
        ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize;

    public const KeyboardKey KbA = KeyboardKey.Z;
    public const KeyboardKey KbB = KeyboardKey.X;
    public const KeyboardKey KbSelect = KeyboardKey.Space;
    public const KeyboardKey KbStart = KeyboardKey.Enter;
    public const KeyboardKey KbRight = KeyboardKey.Right;
    public const KeyboardKey KbLeft = KeyboardKey.Left;
    public const KeyboardKey KbUp = KeyboardKey.Up;
    public const KeyboardKey KbDown = KeyboardKey.Down;
    public const KeyboardKey KbX = KeyboardKey.C;
    public const KeyboardKey KbY = KeyboardKey.B;

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

    public readonly static Dictionary<KeyboardKey, int> SaveStateKeys = new()
    {
        [KeyboardKey.F1] = 0, [KeyboardKey.F2] = 1, [KeyboardKey.F3] = 2,
        [KeyboardKey.F4] = 3, [KeyboardKey.F5] = 4, [KeyboardKey.F6] = 5,
        [KeyboardKey.F7] = 6, [KeyboardKey.F8] = 7, [KeyboardKey.F9] = 8,
    };

    public const string SaveStateVersion = "1.07";
    public static readonly string RomDirectory = "Roms";
    public static readonly string SaveDirectory = "Saves";
    public static readonly string StateDirectory = "States";
    public static readonly string CheatDirectory = "Cheats";
    public static readonly string LuaDirectory = "Lua";
    public static readonly string ConfigDirectory = "Config";
    public static readonly string DebugDirectory = "Debugger";

    public class RegistersInfo(string address, string name, string value)
    {
        public string Address { get; private set; } = address;
        public string Name { get; private set; } = name;
        public string Value { get; private set; } = value;
    }

    public class DisasmEntry(int pc, string disasm, string name, string oper, string regtext, int size, string bytetext = "")
    {
        public int pc = pc;
        public string disasm = disasm;
        public string name = name;
        public string oper = oper;
        public string regtext = regtext;
        public string bytetext = bytetext;
        public int size = size;
        public int addr;
    }

    public static readonly Dictionary<int, string> MapperTypes = new()
    {
        [0x00] = "ROM ONLY",
        [0x01] = "MBC1",
        [0x02] = "MBC1RAM",
        [0x03] = "MBC1RAMBATTERY",
        [0x05] = "MBC2",
        [0x06] = "MBC2BATTERY",
        [0x08] = "ROMRAM 1",
        [0x09] = "ROMRAMBATTERY 1",
        [0x0B] = "MMM01",
        [0x0C] = "MMM01RAM",
        [0x0D] = "MMM01RAMBATTERY",
        [0x0F] = "MBC3TIMERBATTERY",
        [0x10] = "MBC3TIMERRAMBATTERY 2",
        [0x11] = "MBC3",
        [0x12] = "MBC3RAM 2",
        [0x13] = "MBC3RAMBATTERY 2",
        [0x19] = "MBC5",
        [0x1A] = "MBC5RAM",
        [0x1B] = "MBC5RAMBATTERY",
        [0x1C] = "MBC5RUMBLE",
        [0x1D] = "MBC5RUMBLERAM",
        [0x1E] = "MBC5RUMBLERAMBATTERY",
        [0x20] = "MBC6",
        [0x22] = "MBC7SENSORRUMBLERAMBATTERY",
        [0xFC] = "POCKET CAMERA",
        [0xFD] = "BANDAI TAMA5",
        [0xFE] = "HuC3",
        [0xFF] = "HuC1RAMBATTERY",
    };
}
