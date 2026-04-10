using Gmulator.Interfaces;
using ImGuiNET;
using System.Text;

namespace Gmulator.Core.Snes.Sa1;
public class SnesSa1Cpu : SnesCpu, ISaveState
{
    public bool HasStepped { get; private set; }



    private readonly Snes Snes;
    private readonly SnesSa1 Sa1;


    public SnesSa1Cpu(Snes snes, SnesSa1 sa1)
    {
        Snes = snes;
        Sa1 = sa1;
    }

    private int ReadOpcode()
    {
        Cycles++;
        int v = Sa1.ReadByte(PBPC);
        PC++;
        return v;
    }

    public override void Step()
    {
        var syncto = Snes.Ppu.Cycles / 8;
        while (Cycles < syncto)
        {
            if (!Sa1._sa1Ready && !Sa1._sa1Reset)
            {
#if !DECKRELEASE
                if (Snes.Breakpoints.Count > 0)
                {
                    if (!Snes.Run && Snes.Debugger.Execute(PBPC))
                    {
                        Snes.EmuState = DebugState.Break;
                        break;
                    }
                }
                if (Snes.EmuState == DebugState.Break)
                    return;

                Snes.Logger.LogSaOne(Snes.Ppu.HPos);
#endif
                int op = ReadOpcode();
                int mode = Disasm[op].Mode;
                int addr = GetMode(mode);
                ExecOp(op, mode, addr);
                HasStepped = true;
            }
            else
            {
                Cycles++;
                HasStepped = false;
            }
        }
    }

    public bool DebugStep()
    {
        if (!Sa1._sa1Ready && !Sa1._sa1Reset)
        {
            int op = ReadOpcode();
            int mode = Disasm[op].Mode;
            int addr = GetMode(mode);
            ExecOp(op, mode, addr);
            return true;
        }
        return false;
    }

    public override void Irq()
    {
        if (!E)
        {
            Push((byte)PB);
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            PS |= FI;
            Idle();
        }
        else
        {
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
        }
        PC = Sa1.GetIrqVector();
        PB = 0;
    }

    public override void Nmi()
    {
        if (!E)
        {
            Push((byte)PB);
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
            PS |= FI;
            Idle();
        }
        else
        {
            Push((byte)(PC >> 8));
            Push((byte)(PC & 0xff));
            Push((byte)PS);
        }
        PC = Sa1.GetNmiVector();
        PB = 0;
    }

    public override int Read(int a)
    {
        Cycles++;
        return Sa1.ReadByte(a);
    }

    public override void Write(int a, int v)
    {
        Cycles++;
        Sa1.WriteByte(a, v);
    }

    public override void Reset(bool isSa1) => PC = Sa1.GetResetVector();

    public void ResetCpu()
    {
        base.Reset(true);
        Cycles = 0;
        PC = Sa1.GetResetVector();
    }

    public new List<RegisterInfo> GetFlags()
    {
        int ps = PS;
        return
        [
            new("","C",$"{(ps & FC) != 0}"),
            new("","Z",$"{(ps & FZ) != 0}"),
            new("","I",$"{(ps & FI) != 0}"),
            new("","D",$"{(ps & FD) != 0}"),
            new("","X",$"{(ps & FX) != 0}"),
            new("","M",$"{(ps & FM) != 0}"),
            new("","V",$"{(ps & FV) != 0}"),
            new("","N",$"{(ps & FN) != 0}"),
            new("","E",$"{E}"),
        ];
    }

    public new List<RegisterInfo> GetRegisters() =>
    [
        new("","A ",$"{A:X4}"),
        new("","X ",$"{X:X4}"),
        new("","Y ",$"{Y:X4}"),
        new("","SP",$"{SP:X4}"),
        new("","D ",$"{D:X4}"),
        new("","P ",$"{PS:X4}"),
        new("","DB",$"{DB:X2}"),
        new("","PB",$"{PB:X2}"),
    ];


}

