namespace Gmulator.Core.Snes;
public class SnesSa1(Snes snes) : SnesCpu
{
    public Snes Snes { get; } = snes;

    private readonly int Message;

    public bool Irq { get; private set; }
    public bool Ready { get; private set; }
    public bool Reseta1 { get; private set; }
    public bool Nmia1 { get; private set; }
    public int ResetVector { get; private set; }
    public int NmiVector { get; private set; }
    public int IrqVector { get; private set; }

    public byte[] Iram { get; private set; } = new byte[0x800];

    public override void Step()
    {
        if (Reseta1)
        {
           
        }
        if (!Reseta1)
        {
            //byte op = Snes.ReadMemory(PC);
            //ExecOp(op);
        }
    }

    public new int Read(int a)
    {
        return base.Read(a);
    }

    public void Write(int a, int v)
    {
        base.Write(a, v);
    }

    public static void Reset()
    {
        
    }

    public byte ReadIram(int a)
    {
        return Iram[a & 0x7ff];
    }

    public void WriteIram(int a, byte v)
    {
        Iram[a & 0x7ff] = v;
    }

    public static byte ReadReg(int a)
    {
        switch (a & 0xff)
        {
            case 0x00:
            {
                byte b = 0;

                return b;
            }
            case 0x01:
            {
                byte b = 0x80;

                return b;
            }
        }
        return 0;
    }

    public void WriteReg(int a, byte b)
    {
        var v = b;
        switch (a & 0xff)
        {
            case 0x00:
                Irq = (v & 0x80) != 0;
                Ready = (v & 0x40) != 0;
                Reseta1 = (v & 0x20) != 0;
                Nmia1 = (v & 0x10) != 0;
                Reset();
                break;
            case 0x01:

                break;
            case 0x02:

                break;

            case 0x03 or 0x04:
                if (a % 2 == 1)
                    ResetVector = (ResetVector & 0xff00) | v;
                else
                    ResetVector = (ResetVector & 0xff) | v << 8;
                break;
            case 0x05 or 0x06:
                if (a % 2 == 1)
                    NmiVector = (NmiVector & 0xff00) | v;
                else
                    NmiVector = (NmiVector & 0xff) | v << 8;
                break;
            case 0x07 or 0x08:
                if (a % 2 == 1)
                    IrqVector = (IrqVector & 0xff00) | v;
                else
                    IrqVector = (IrqVector & 0xff) | v << 8;
                break;
        }
    }

    public new List<RegisterInfo> GetRegs() =>
    [
        new("2200.0-3","Message",$"{Message}"),
        new("2200.4","Message",$"{Message}"),
        new("2200.5","Message",$"{Message}"),
        new("2200.6","Message",$"{Message}"),
        new("2200.7","Message",$"{Message}"),
        new("2203/4","Reset Vector",$"{ResetVector}"),
        new("2205/6","Nmi Vector",$"{NmiVector}"),
        new("2207/8","Irq Vector",$"{IrqVector}"),
    ];
}
