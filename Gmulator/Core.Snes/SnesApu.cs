using Gmulator.Core.Nes;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Snes;
public class SnesApu : EmuState
{
    public Timer[] Timers = new Timer[3];
    private ulong Cycles;
    private byte[] SpcIO;
    private byte[] CpuIO;
    private int[] TimerOut;
    public byte[] Ram { get => ram; private set => ram = value; }
    public bool WriteEnabled;
    private bool ReadEnabled;
    private bool IplEnabled;
    private int DspAddr;
    private byte[] ram;

    public const double apuCyclesPerMaster = (32040 * 32) / (1364 * 262 * 60.0);

    private Snes Snes;
    private SnesSpc Spc;
    private SnesDsp Dsp;
    private SnesPpu Ppu;
    private SnesSpcLogger Logger;

    private SortedDictionary<int, Breakpoint> Breakpoints;
    private Action<DebugState> SetState;
    private Func<int, bool> ExecuteCheck;
    private Func<int, int, RamType, bool, bool> AccessCheckSpc;

    public SnesApu()
    {
        Ram = new byte[0x10000];
        TimerOut = new int[3];
        SpcIO = new byte[6];
        CpuIO = new byte[4];
    }

    public void SetSnes(Snes snes, SnesCpu cpu, SnesPpu ppu, SnesSpc spc, SnesDsp dsp, SnesSpcLogger spclogger)
    {
        Snes = snes;
        Ppu = ppu;
        Spc = spc;
        Dsp = dsp;
        Logger = spclogger;
        Breakpoints = snes.Breakpoints;
        SetState = snes.SetState;
        if (snes.DebugWindow != null)
        {
            ExecuteCheck = snes.DebugWindow.ExecuteCheck;
            AccessCheckSpc = snes.DebugWindow.AccessCheckSpc;
        }
    }

    public void Step()
    {
        var syncto = (ulong)(Ppu.Cycles * apuCyclesPerMaster);
        while (Cycles < syncto)
        {
#if !DECKRELEASE
            if (Snes.Debug)
            {
                DebugState state = Snes.State;
                if (Logger.Logging)
                    Logger.Log(Spc.PC);

                if (Breakpoints.Count > 0 && state == DebugState.Running)
                {
                    if (ExecuteCheck(Spc.PC))
                    {
                        SetState(DebugState.Break);
                        return;
                    }
                }

                if (state == DebugState.Break || state == DebugState.StepSpc)
                {
                    SetState(DebugState.Break);
                    return;
                }
            }
#endif
            Spc.Step();
        }
    }

    public void Cycle()
    {
        if ((Cycles & 0x1f) == 0)
            Dsp.Cycle();

        // handle timers
        for (int i = 0; i < 3; i++)
        {
            if (Timers[i].Cycles == 0)
            {
                Timers[i].Cycles = (byte)(i == 2 ? 16 : 128);
                if (Timers[i].Enabled)
                {
                    Timers[i].Divider++;
                    if (Timers[i].Divider == Timers[i].Target)
                    {
                        Timers[i].Divider = 0;
                        Timers[i].Counter++;
                        Timers[i].Counter &= 0xf;
                    }
                }
            }
            Timers[i].Cycles--;
        }
        Cycles++;
    }

    public void Idle() => Cycle();

    public byte Read(int a, bool debug = false)
    {
        a &= 0xffff;
        Cycle();
#if DEBUG || RELEASE
        if (debug && AccessCheckSpc(a, -1, RamType.SpcRam, false))
            SetState(DebugState.Break);
#endif

        switch (a)
        {
            case 0xf0 or 0xf1 or 0xfa or 0xfb or 0xfc:
                return 0x00;
            case 0xf2: return (byte)DspAddr;
            case 0xf3: return Dsp.Read(DspAddr & 0x7f);
            case >= 0xf4 and <= 0xf7:
                return CpuIO[(byte)a - 0xf4];
            case >= 0xfd and <= 0xff:
                var v = Timers[a - 0xfd].Counter;
                Timers[a - 0xfd].Counter = 0;
                return v;
            case >= 0xffc0:
                if (IplEnabled)
                    return IplBootrom[a & 0x3f];
                break;
        }
        return ram[a];
    }

    public void Write(int addr, int value, bool debug = false)
    {
        int a = addr & 0xffff;
        byte v = (byte)value;

#if DEBUG || RELEASE
        if (debug && AccessCheckSpc(a, v, RamType.SpcRam, true))
            SetState(DebugState.Break);
#endif

        switch (a)
        {
            case 0xf0:
                return;
            case 0xf1:
                for (int i = 0; i < Timers.Length; i++)
                {
                    bool b = (v & (1 << i)) != 0;
                    if (!Timers[i].Enabled && b)
                    {
                        Timers[i].Divider = 0;
                        Timers[i].Counter = 0;
                    }
                    Timers[i].Enabled = b;
                }

                if ((v & 0x10) != 0)
                {
                    CpuIO[0] = 0;
                    CpuIO[1] = 0;
                }

                if ((v & 0x20) != 0)
                {
                    CpuIO[2] = 0;
                    CpuIO[3] = 0;
                }
                IplEnabled = (v & 0x80) != 0;
                return;
            case 0xf2:
                DspAddr = v;
                return;
            case 0xf3:
                if (DspAddr < 0x80)
                    Dsp.Write(DspAddr, v);
                return;
            case >= 0xf4 and <= 0xf7:
                SpcIO[a - 0xf4] = v;
                return;
            case 0xf8 or 0xf9:
                Timers[0].Target = v;
                return;
            case >= 0xfa and <= 0xfc:
                Timers[(a - 0xfa) & 3].Target = v;
                return;
            case >= 0xfd and <= 0xff:
                return;
        }

        ram[a] = v;
    }

    public byte ReadFromSpu(int a) => SpcIO[a & 3];

    public void WriteToSpu(int a, int v) => CpuIO[a] = (byte)v;

    public void Reset()
    {
        for (int i = 0; i < Timers.Length; i++)
            Timers[i] = new();

        WriteEnabled = true;
        IplEnabled = true;
        Cycles = 0;
        Array.Fill<byte>(Ram, 0);
        Array.Fill<byte>(SpcIO, 0);
        Array.Fill<byte>(CpuIO, 0);
    }

    public record Timer
    {
        public byte Cycles;
        public byte Divider;
        public byte Target;
        public byte Counter;
        public bool Enabled;
    }

    public byte[] IplBootrom { get; private set; } =
    [
        0xcd,0xef,0xbd,0xe8,0x00,0xc6,0x1d,0xd0,
        0xfc,0x8f,0xaa,0xf4,0x8f,0xbb,0xf5,0x78,
        0xcc,0xf4,0xd0,0xfb,0x2f,0x19,0xeb,0xf4,
        0xd0,0xfc,0x7e,0xf4,0xd0,0x0b,0xe4,0xf5,
        0xcb,0xf4,0xd7,0x00,0xfc,0xd0,0xf3,0xab,
        0x01,0x10,0xef,0x7e,0xf4,0x10,0xeb,0xba,
        0xf6,0xda,0x00,0xba,0xf4,0xc4,0xf4,0xdd,
        0x5d,0xd0,0xdb,0x1f,0x00,0x00,0xc0,0xff,
    ];

    public Dictionary<string, string> GetIO() => new()
    {
        ["Port 0(Cpu)"] = $"{SpcIO[0]:X2}",
        ["Port 0(Spc)"] = $"{CpuIO[0]:X2}",
        ["Port 1(Cpu)"] = $"{SpcIO[1]:X2}",
        ["Port 1(Spc)"] = $"{CpuIO[1]:X2}",
        ["Port 2(Cpu)"] = $"{SpcIO[2]:X2}",
        ["Port 2(Spc)"] = $"{CpuIO[2]:X2}",
        ["Port 3(Cpu)"] = $"{SpcIO[3]:X2}",
        ["Port 3(Spc)"] = $"{CpuIO[3]:X2}",
    };

    public override void Save(BinaryWriter bw)
    {
        bw.Write(Cycles); EmuState.WriteArray<byte>(bw, SpcIO);
        EmuState.WriteArray<byte>(bw, CpuIO); EmuState.WriteArray<int>(bw, TimerOut);
        EmuState.WriteArray<byte>(bw, Ram); bw.Write(WriteEnabled);
        bw.Write(ReadEnabled); bw.Write(IplEnabled);

        for (int i = 0; i < Timers.Length; i++)
        {
            var t = Timers[i];
            bw.Write(t.Cycles); bw.Write(t.Divider);
            bw.Write(t.Target); bw.Write(t.Counter);
            bw.Write(t.Enabled);
        }
    }

    public override void Load(BinaryReader br)
    {
        Cycles = br.ReadUInt64(); SpcIO = EmuState.ReadArray<byte>(br, SpcIO.Length);
        CpuIO = EmuState.ReadArray<byte>(br, CpuIO.Length); TimerOut = EmuState.ReadArray<int>(br, TimerOut.Length);
        Ram = EmuState.ReadArray<byte>(br, Ram.Length); WriteEnabled = br.ReadBoolean();
        ReadEnabled = br.ReadBoolean(); IplEnabled = br.ReadBoolean();

        for (int i = 0; i < Timers.Length; i++)
        {
            var t = Timers[i];
            t.Cycles = br.ReadByte(); t.Divider = br.ReadByte();
            t.Target = br.ReadByte(); t.Counter = br.ReadByte();
            t.Enabled = br.ReadBoolean();
        }
    }
}