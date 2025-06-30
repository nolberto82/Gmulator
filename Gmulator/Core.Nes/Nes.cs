using Gmulator.Shared;
using GNes.Core;
using GNes.Core.Mappers;
using ImGuiNET;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Nes;
public class Nes : Emulator
{
    public NesCpu Cpu { get; private set; }
    public NesPpu Ppu { get; private set; }
    public NesApu Apu { get; private set; }
    public NesMmu Mmu { get; private set; }
    public BaseMapper Mapper { get; private set; }
    public Header Cart { get; private set; }
    public Timer Timer { get; private set; }
    public NesLogger Logger { get; private set; }
    public Audio Audio { get; }
    public NesJoypad Joypad1 { get; private set; } = new();
    public NesJoypad Joypad2 { get; private set; } = new();

    public Nes()
    {
        CheatConverter = new(this);
        Mmu = new(Cheats);
        Cpu = new();
        Ppu = new(Mmu);
        Apu = new();
        Logger = new(Cpu);
        Ppu.Init(this);
        Mmu.Init(Joypad1, Joypad2);
        Input.Init(NesJoypad.GetButtons());

        Logger.OnGetFlags += Cpu.GetFlags;
        Logger.OnGetRegs += Cpu.GetRegs;
        Logger.ReadByte += Mmu.Read;

        Mmu.StatusR = Ppu.StatusR;
        Mmu.DataR = Ppu.DataR;
        Mmu.ControlW = Ppu.ControlW;
        Mmu.MaskW = Ppu.MaskW;
        Mmu.OamAddressW = Ppu.OamAddressW;
        Mmu.OamDataW = Ppu.OamDataW;
        Mmu.ScrollW = Ppu.ScrollW;
        Mmu.AddressDataW = Ppu.AddressDataW;
        Mmu.DataW = Ppu.DataW;
        Mmu.OamDamyCopy = Ppu.OamDamyCopy;
        Mmu.ApuRead = Apu.Read;
        Mmu.ApuWrite = Apu.Write;
        Cpu.PpuStep = Ppu.Step;
        Cpu.ReadByte = Mmu.Read;
        Cpu.WriteByte = Mmu.Write;
        Cpu.ReadWord = Mmu.ReadWord;
        Apu.Dmc.Read = Cpu.ReadDmc;
    }

    public override void Execute(bool opened, int times)
    {
        if (Mapper != null && State == Running && !opened)
        {
            var cyclesframe = Header.Region == 0 ? NesNtscCycles : NesPalCycles;
            while (times-- > 0)
            {
                while (Ppu.Cycles < cyclesframe)
                {
                    ushort pc = (ushort)Cpu.PC;
                    Cpu.Step();

                    if (Debug)
                    {
                        if (Cpu.StepOverAddr == Cpu.PC)
                        {
                            State = Break;
                            Cpu.StepOverAddr = -1;
                            return;
                        }

                        if (Breakpoints.Count > 0)
                        {
                            if (DebugWindow.ExecuteCheck(pc))
                            {
                                State = Break;
                                return;
                            }
                        }

                        if (Logger.Logging)
                            Logger.Log(pc);
                    }

                    if (State == Break)
                        return;
                }
                Ppu.Cycles -= cyclesframe;
            }
        }
    }

    public override void Update() => Input.Update(this, NesConsole, Ppu.FrameCounter);

    public override void Render(float MenuHeight) => base.Render(MenuHeight);

    public override void Reset(string name, string lastname, bool reset)
    {
        if (name != "")
            Mapper = Mmu.LoadRom(name);

        if (Mapper != null)
        {
            GameName = name;
            DebugWindow ??= new NesDebugWindow(this);
            Mmu.Ram.AsSpan(0x6000, 0x2000).ToArray();
            Cpu.Reset();
            Ppu.Reset();
            Apu.Reset(Header.Region, Header.Region == 0 ? NesNtscCpuClock : NesPalCpuClock);
            Mmu.Reset();
            Logger.Reset();
            Cheat.Load(this, false);
            base.Reset(name, lastname, true);
        }
    }

    public override void SaveState(int slot, StateResult res)
    {
        if (Mapper == null) return;

        lock (StateLock)
        {
            var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Header.Name)}.gs";
            if (name != "")
            {
                using BinaryWriter bw = new(new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write));

                bw.Write(Encoding.ASCII.GetBytes(SaveStateVersion));
                Mmu.Save(bw);
                Cpu.Save(bw);
                Ppu.Save(bw);
                Apu.Save(bw);
                Mapper.Save(bw);
                base.SaveState(slot, StateResult.Success);
            }
            else
                base.SaveState(slot, StateResult.Failed);
        }
    }

    public override void LoadState(int slot, StateResult res)
    {
        if (Mapper == null) return;

        lock (StateLock)
        {
            var name = $"{Environment.CurrentDirectory}\\{StateDirectory}\\{Path.GetFileNameWithoutExtension(Mapper.Header.Name)}.gs";
            if (File.Exists(name))
            {
                using BinaryReader br = new(new FileStream(name, FileMode.Open, FileAccess.Read));

                if (Encoding.ASCII.GetString(br.ReadBytes(4)) == SaveStateVersion)
                {
                    Mmu.Load(br);
                    Cpu.Load(br);
                    Ppu.Load(br);
                    Apu.Load(br);
                    Mapper.Load(br);
                    base.LoadState(slot, StateResult.Success);
                }
                else
                    base.LoadState(slot, StateResult.Mismatch);
            }
            else
                base.LoadState(slot, StateResult.Failed);
        }
    }

    public override void SaveBreakpoints(string name) => base.SaveBreakpoints(name);

    public override void LoadBreakpoints(string name) => base.LoadBreakpoints(name);

    public override void Close()
    {
        Mmu?.SaveRam();
        Cheat.Save(Mapper.Name, Cheats);
        SaveBreakpoints(Mapper.Name);
    }

    public override void SetState(int v) => base.SetState(v);
}
