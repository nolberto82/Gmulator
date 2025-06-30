
namespace Gmulator.Core.Gbc;
public class GbcTimer
{
    public int DivideRegister { get; private set; }
    public int TimerCounter { get; private set; }
    public int TotalCounter { get; private set; }
    public bool Overflow { get; private set; }

    public GbcTimer() { }

    public void Step(GbcIO IO, GbcCpu cpu, int cycles)
    {
        var m = 1;
        TotalCounter += cycles;
        DivideRegister += cycles;
        if (DivideRegister >= 256 / m)
        {
            DivideRegister -= 256 / m;
            IO.DIV++;
        }

        if ((IO.TAC & 0x04) > 0)
        {
            int div = 0;

            _ = (IO.TAC & 3) switch
            {
                0 => div = 1024,
                1 => div = 16,
                2 => div = 64,
                3 => div = 256,
                _ => 0,
            };

            if (Overflow)
            {
                IO.TIMA = IO.TMA;
                IO.IF |= 4;
                cpu.Halt = false;
                Overflow = false;
            }

            if (IO.UpdateTIMA)
            {
                ++IO.TIMA;
                IO.UpdateTIMA = false;
            }

            TimerCounter += cycles;
            while (TimerCounter >= div / m)
            {

                TimerCounter -= div / m;
                IO.UpdateTIMA = true;
                if (IO.TIMA == 0xff)
                    Overflow = true;
            }
        }
    }

    public void Reset()
    {
        TimerCounter = 0;
        DivideRegister = 0;
        TotalCounter = 0;
    }
}
