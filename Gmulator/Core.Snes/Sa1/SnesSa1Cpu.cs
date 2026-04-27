using Gmulator.Interfaces;

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

    public void Step()
    {
        ulong syncto = Snes.Ppu.Cycles / 2;
        while (Cycles < syncto)
        {
            if (Sa1._sa1Wait || Sa1._sa1Reset)
            {
                Cycles++;
                HasStepped = false;
            }
            else
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
        }
    }

    public bool DebugStep()
    {
        if (!Sa1._sa1Wait && !Sa1._sa1Reset)
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
        PC = Sa1.GetSa1IrqVector();
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
        PC = Sa1.GetSa1NmiVector();
        PB = 0;
    }

    public int Read(int a)
    {
        Cycles++;
        if (a == NMIn)
            return (byte)Sa1.GetSnesNmiVector();
        if (a == NMIn + 1)
            return (byte)(Sa1.GetSnesNmiVector() >> 8);
        if (a == IRQn)
            return (byte)Sa1.GetSnesIrqVector();
        if (a == IRQn + 1) 
            return (byte)(Sa1.GetSnesIrqVector() >> 8);
        return Sa1.ReadByte(a);
    }

    public void Write(int a, int v)
    {
        Cycles++;
        Sa1.WriteByte(a, v);
    }

    public override void Reset(bool isSa1)
    {
        PC = Sa1.GetResetVector();
        AddCycles((int)(Snes.Ppu.Cycles / 2));
    }

    public void ResetCpu()
    {
        base.Reset(true);
        Cycles = 0;
        PC = Sa1.GetResetVector();
    }
}

