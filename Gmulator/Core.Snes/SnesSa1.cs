using System.Text;

namespace Gmulator.Core.Snes;

public class SnesSa1 : SnesCpu
{
    public Snes Snes { get; }

    private readonly int Message;

    public bool _sa1IrqRequest { get; private set; }
    public bool _sa1Ready { get; private set; }
    public bool _sa1Reset { get; private set; }
    public bool _sa1NmiRequest { get; private set; }
    private int _resetVector;
    public int NmiVector { get; private set; }
    public int IrqVector { get; private set; }

    private int _sfr;
    private int _cfr;
    private int _sie;
    private bool _sa1IrqEnabled;
    private bool _sa1CharIrq;
    private int _sic;

    private int[] _mmcBanks = new int[4];
    private int _bmaps;
    private int _bmap;
    private int _bpwa;
    private int _sbwe;
    private int _siwp;
    private bool _irqSa1Enabled;
    private bool _nmiSa1Enabled;
    private bool _snesIrq;
    private bool _nmiVectorSelect;
    private bool _irqVectorSelect;

    private Func<int, int> _readOpcode;

    public SnesSa1(Snes snes)
    {
        Snes = snes;
        _readOpcode = (int a) => Snes.ReadOp(a);
    }

    public byte[] Iram { get; private set; } = new byte[0x800];

    public void Init()
    {

    }

    public override void Step()
    {
        if (IrqEnabled)
        {
            Nmi(IrqVector, true);
            IrqEnabled = false;
            return;
        }

        if (!_sa1Ready && !_sa1Reset)
        {
            if (!Snes.Run && Snes.Breakpoints.Count > 0 && Snes.State == DebugState.Running)
            {
                if (!Snes.Run && Snes.DebugWindow.ExecuteCheck(PC))
                {
                    Snes.SetState(DebugState.Break);
                    return;
                }
            }

            int op = _readOpcode(PC++);
            ExecOp(op);

            if (_sa1NmiRequest && _nmiSa1Enabled)
            {
                //Nmi(NmiVector);
                //Sa1Interrupt = true;
            }

            if ((PS & FI) == 0 && _sa1IrqRequest && _irqSa1Enabled)
            {
                IrqEnabled = true;
                //Sa1Interrupt = true;
            }
        }
    }

    public override void Nmi(int type, bool sa1)
    {
        base.Nmi(type, true);
    }

    public override int Read(int a)
    {
        return Snes.ReadMemory(a);
    }

    public override void Write(int a, int v)
    {
        Snes.WriteMemory(a, v);
    }

    public int ReadRam(int a)
    {
        return Iram[a & 0x7ff];
    }

    public void WriteRam(int a, int v)
    {
        Iram[a & 0x7ff] = (byte)v;
    }

    public override void Reset(bool isSa1)
    {
        base.Reset(true);
        PC = _resetVector;

    }

    public void Reset2()
    {
        WriteCpuReg(0x2200, 0x20);
        WriteCpuReg(0x2209, 0x00);
        WriteCpuReg(0x220a, 0x00);
        Array.Fill<byte>(Iram, 0x00);
    }

    public int ReadIram(int a)
    {
        return Iram[a & 0x7ff];
    }

    public void WriteIram(int a, int v)
    {
        Iram[a & 0x7ff] = (byte)v;
    }

    public int ReadReg(int a)
    {
        a &= 0xffff;
        switch (a)
        {
            case 0x2300:
            {
                return _sfr;
            }
            case 0x2301:
            {
                int b = 0;
                b = _sa1IrqRequest ? 0x80 : 00;
                return b;
            }

        }
        return 0;
    }

    public void WriteCpuReg(int a, int v)
    {
        switch (a)
        {
            case 0x2200:
                _sa1IrqRequest = (v & 0x80) != 0;
                _sa1Ready = (v & 0x40) != 0;
                _sa1Reset = (v & 0x20) != 0;
                _sa1NmiRequest = (v & 0x10) != 0;
                Sa1Interrupt = true;
                Reset(true);
                break;
            case 0x2201:
                _sa1IrqEnabled = (v & 0x80) != 0;
                _sa1CharIrq = (v & 0x20) != 0;
                Sa1Interrupt = true;
                break;
            case 0x2202:
                _sic = v;
                break;

            case 0x2203 or 0x2204:
                if (a % 2 == 1)
                    _resetVector = (_resetVector & 0xff00) | v;
                else
                    _resetVector = (_resetVector & 0xff) | v << 8;
                break;
            case 0x2205 or 0x2206:
                if (a % 2 == 1)
                    NmiVector = (NmiVector & 0xff00) | v;
                else
                    NmiVector = (NmiVector & 0xff) | v << 8;
                break;
            case 0x2207 or 0x2208:
                if (a % 2 == 1)
                    IrqVector = (IrqVector & 0xff00) | v;
                else
                    IrqVector = (IrqVector & 0xff) | v << 8;
                break;
            case >= 0x2209 and <= 0x221f: WriteSa1Reg(a, v); break;
            case >= 0x2220 and <= 0x2223: _mmcBanks[a & 3] = v; break;
            case 0x2224: _bmaps = v; break;
            case 0x2225: _bmap = v; break;
            case 0x2226: _sbwe = v; break;
            case 0x2228: _bpwa = v; break;
            case 0x2229: _siwp = v; break;
        }
    }

    public void WriteSa1Reg(int a, int v)
    {
        switch (a)
        {
            case 0x2209:
                _nmiVectorSelect = (v & 0x20) != 0;
                _irqVectorSelect = (v & 0x40) != 0;
                _snesIrq = (v & 0x80) != 0;
                break;
            case 0x220a:
                _irqSa1Enabled = (v & 0x80) != 0;
                _nmiSa1Enabled = (v & 0x10) != 0;
                break;
            case 0x220b:
                if ((v & 0x80) != 0)
                    _irqSa1Enabled = false;
                if ((v & 0x10) != 0)
                    _nmiSa1Enabled = false;
                break;
        }
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

    public List<RegisterInfo> GetRegs() =>
    [
        new("2200.0-3","Message",$"{Message}"),
        new("2200.4","Message",$"{Message}"),
        new("2200.5","Message",$"{Message}"),
        new("2200.6","Message",$"{Message}"),
        new("2200.7","Message",$"{Message}"),
        new("2203/4","Reset Vector",$"{_resetVector}"),
        new("2205/6","Nmi Vector",$"{NmiVector}"),
        new("2207/8","Irq Vector",$"{IrqVector}"),
    ];
}
