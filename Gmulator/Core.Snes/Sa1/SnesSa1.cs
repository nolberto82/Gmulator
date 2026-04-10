using Gmulator.Core.Nes;
using Gmulator.Core.Snes.Mappers;
using static Gmulator.Core.Snes.SnesCpu;
using Gmulator.Interfaces;

namespace Gmulator.Core.Snes.Sa1;

public partial class SnesSa1 : Emulator, IConsole
{
    public Snes Snes { get; }
    public SnesSa1Cpu Cpu;
    public SnesSa1Mmu Mmu;

    #region State
    private int _resetVector;
    private int _nmiVector;
    private int _irqVector;
    private int _snesMessage;
    private int _sa1Message;
    private bool _snesCharConvIrqEnabled;
    private bool _snesCharConvIrqFlag;
    public int[] _mmcBanks = new int[4];
    private bool _sa1IrqEnabled;
    private bool _sa1NmiEnabled;
    private bool _nmiVectorSelect;
    private bool _irqVectorSelect;

    private bool _irqEnabled;
    private bool _irqRequest;
    public bool _sa1IrqRequest;
    public bool _sa1Ready;
    public bool _sa1Reset;
    public bool _sa1NmiRequest;
    private bool _charDmaActive;

    private int _bwCpuBank;
    private int _bwSa1Bank;
    private int _bwRamRegionProtect;
    private int _cpuIramProtect;
    private int _mathControl;
    private int _multiplicand;
    private int _multiplier;
    private int _mathResult;

    private bool _dmaControl;
    private int _dmaPriority;
    private bool _dmaMode;
    private int _dmaConvType;
    private int _dmaDstDevice;
    private int _dmaSrcDevice;
    private bool _dmaCharConv;

    private int _dmaSrcStartAddr;
    private int _dmaDstStartAddr;
    private int _dmaTerminalCounter;
    #endregion

    public int GetResetVector() => _resetVector;
    public int GetIrqVector() => !_irqVectorSelect ? ReadWord(IRQn) : _irqVector;
    public int GetNmiVector() => !_nmiVectorSelect ? ReadWord(NMIn) : _nmiVector;

    ICpu IConsole.Cpu => throw new NotImplementedException();

    public IPpu Ppu => throw new NotImplementedException();

    IMmu IConsole.Mmu => throw new NotImplementedException();

    public DebugState EmuState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public Debugger Debugger { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public MemoryMap Sa1Map;

    public int BwSa1Bank { get => _bwSa1Bank; }

    public SnesSa1(Snes snes)
    {
        Snes = snes;
        Cpu = new(snes, this);
        Mmu = new();
        Sa1Map = new(0x1000);
    }

    private void SetMemoryMap()
    {
        var Mapper = Snes.Mapper;
        var Ppu = Snes.Ppu;
        var CpuMap = Snes.CpuMap;

        Sa1Map.Iram(0x00, 0x3f, 0x0000, 0x07ff, Mmu.ReadIram, Mmu.WriteIram);
        Sa1Map.Iram(0x80, 0xbf, 0x0000, 0x07ff, Mmu.ReadIram, Mmu.WriteIram);
        Sa1Map.Iram(0x00, 0x3f, 0x3000, 0x37ff, Mmu.ReadIram, Mmu.WriteIram);
        Sa1Map.Iram(0x80, 0xbf, 0x3000, 0x37ff, Mmu.ReadIram, Mmu.WriteIram);

        Sa1Map.Register(0x00, 0x3f, 0x2000, 0x2fff, ReadReg, WriteSa1Reg);
        Sa1Map.Register(0x80, 0xbf, 0x2000, 0x2fff, ReadReg, WriteSa1Reg);

        Sa1Map.Sram(0x40, 0x4f, 0x0000, 0xffff, Mapper.ReadSram, Mapper.WriteSram);
        CpuMap.Sram(0x40, 0x4f, 0x0000, 0xffff, Mapper.ReadSram, Mapper.WriteSram);

        CpuMap.Register(0x00, 0x3f, 0x2000, 0x2fff, Ppu.Read, Ppu.Write);
        CpuMap.Register(0x80, 0xbf, 0x2000, 0x2fff, Ppu.Read, Ppu.Write);
        CpuMap.Register(0x80, 0xbf, 0x3000, 0x37ff, Mmu.ReadIram, Mmu.WriteIram);
        CpuMap.Register(0x00, 0x3f, 0x3000, 0x37ff, Mmu.ReadIram, Mmu.WriteIram);

        Sa1Map.Sram(0x00, 0x3f, 0x6000, 0x7fff, ReadBwRam, WriteBwRam);
        Sa1Map.Sram(0x80, 0xbf, 0x6000, 0x7fff, ReadBwRam, WriteBwRam);

        UpdateMmcBanks();
        UpdateRamBanks();
    }

    public int ReadIram(int a) => Mmu.ReadIram(a);

    public void WriteIram(int a, int v) => Mmu.WriteIram(a, v);

    public void Step()
    {
        Cpu.Step();
    }

    private void CheckInterrupts()
    {
        if (_sa1NmiRequest && _sa1NmiEnabled)
        {
            //Nmi(NmiVector);
            //Sa1Interrupt = true;
        }

        if (_sa1IrqEnabled && _sa1IrqRequest)
            Cpu.Irq();

        if (_irqEnabled && _irqRequest || _snesCharConvIrqFlag && _snesCharConvIrqEnabled)
            Snes.Cpu.SetIrq();
    }

    public int ReadByte(int a)
    {
        a &= 0xffffff;
        int b = a >> 12;
        Snes.Debugger.Access(a, -1, Sa1Map.Handlers[b], false);
        return Sa1Map.Handlers[b].Read(a) & 0xff;
    }

    public void WriteByte(int a, int v)
    {
        a &= 0xffffff;
        int b = a >> 12;
        Sa1Map.Handlers[b].Write(a, v);
        Snes.Debugger.Access(a, v, Sa1Map.Handlers[b], true);
    }

    public int ReadByteDebug(int a)
    {
        a &= 0xffffff;
        int b = a >> 12;
        return Sa1Map.Handlers[b].Read(a) & 0xff;
    }

    public int ReadWord(int a) => (ushort)(ReadByteDebug(a) | ReadByteDebug(a + 1) << 8);
    public int ReadLong(int a) => ReadByteDebug(a) | ReadByteDebug(a + 1) << 8 | ReadByteDebug(a + 2) << 16;


    public int ReadBwRam(int a)
    {
        int addr = GetBwAddr(a);
        return Snes.Mapper.ReadSram(addr);
    }

    public void WriteBwRam(int a, int v)
    {
        int addr = GetBwAddr(a);
        Snes.Mapper.WriteBwRam(addr, v);
    }

    private int GetBwAddr(int a) => (BwSa1Bank * 0x2000) | (a & 0x1fff);

    public void Reset2()
    {
        WriteCpuReg(0x2200, 0x20);
        WriteCpuReg(0x2209, 0x00);
        WriteCpuReg(0x220a, 0x00);
        Cpu.ResetCpu();
        Mmu.Reset();
        SetMemoryMap();

        _irqVectorSelect = false;
        _nmiVectorSelect = false;
        _multiplicand = 0;
        _multiplier = 0;
        _mathResult = 0;
        _snesCharConvIrqEnabled = false;
        _dmaControl = _dmaMode = _dmaCharConv = false;

        for (int i = 0; i < _mmcBanks.Length; i++)
            _mmcBanks[i] = i;
    }

    public void UpdateMmcBanks()
    {
        var Mapper = Snes.Mapper;
        var cpuMap = Snes.CpuMap;
        for (int i = 0; i < 2; i++)
        {
            var map = i == 0 ? cpuMap : Sa1Map;
            map.Set(0x00, 0x1f, 0x8000, 0xffff, Mapper.Read, (a, v) => { }, RamType.Rom, 0x1000, (_mmcBanks[0] & 0x80) != 0 ? (_mmcBanks[0] & 7) << 20 : 0x000000);
            map.Set(0x20, 0x3f, 0x8000, 0xffff, Mapper.Read, (a, v) => { }, RamType.Rom, 0x1000, (_mmcBanks[1] & 0x80) != 0 ? (_mmcBanks[1] & 7) << 20 : 0x100000);
            map.Set(0x80, 0x9f, 0x8000, 0xffff, Mapper.Read, (a, v) => { }, RamType.Rom, 0x1000, (_mmcBanks[2] & 0x80) != 0 ? (_mmcBanks[2] & 7) << 20 : 0x200000);
            map.Set(0xa0, 0xbf, 0x8000, 0xffff, Mapper.Read, (a, v) => { }, RamType.Rom, 0x1000, (_mmcBanks[3] & 0x80) != 0 ? (_mmcBanks[3] & 7) << 20 : 0x300000);
            map.Set(0xc0, 0xcf, 0x0000, 0xffff, Mapper.Read, (a, v) => { }, RamType.Rom, 0x1000, (_mmcBanks[0] & 7) << 20);
            map.Set(0xd0, 0xdf, 0x0000, 0xffff, Mapper.Read, (a, v) => { }, RamType.Rom, 0x1000, (_mmcBanks[1] & 7) << 20);
            map.Set(0xe0, 0xef, 0x0000, 0xffff, Mapper.Read, (a, v) => { }, RamType.Rom, 0x1000, (_mmcBanks[2] & 7) << 20);
            map.Set(0xf0, 0xff, 0x0000, 0xffff, Mapper.Read, (a, v) => { }, RamType.Rom, 0x1000, (_mmcBanks[3] & 7) << 20);
        }
    }

    public void UpdateRamBanks()
    {
        var Mapper = Snes.Mapper;
        var cpuMap = Snes.CpuMap;
        cpuMap.Sram(0x00, 0x3f, 0x6000, 0x7fff, Mapper.ReadSram, Mapper.WriteSram, _bwCpuBank);
        cpuMap.Sram(0x80, 0xbf, 0x6000, 0x7fff, Mapper.ReadSram, Mapper.WriteSram, _bwCpuBank);
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(_resetVector); bw.Write(_nmiVector); bw.Write(_irqVector); bw.Write(_snesMessage);
        bw.Write(_sa1Message); bw.Write(_snesCharConvIrqEnabled); bw.Write(_snesCharConvIrqFlag); WriteArray(bw, _mmcBanks);
        bw.Write(_sa1IrqEnabled); bw.Write(_sa1NmiEnabled); bw.Write(_nmiVectorSelect); bw.Write(_irqVectorSelect);
        bw.Write(_irqEnabled); bw.Write(_irqRequest); bw.Write(_sa1IrqRequest); bw.Write(_sa1Ready);
        bw.Write(_sa1Reset); bw.Write(_sa1NmiRequest); bw.Write(_charDmaActive); bw.Write(_bwCpuBank);
        bw.Write(_bwSa1Bank); bw.Write(_bwRamRegionProtect); bw.Write(_cpuIramProtect); bw.Write(_mathControl);
        bw.Write(_mathResult); bw.Write(_dmaControl); bw.Write(_dmaPriority); bw.Write(_dmaMode);
        bw.Write(_dmaConvType); bw.Write(_dmaDstDevice); bw.Write(_dmaSrcDevice); bw.Write(_dmaCharConv);
        bw.Write(_dmaSrcStartAddr); bw.Write(_dmaDstStartAddr); bw.Write(_dmaTerminalCounter);
    }

    public void Load(BinaryReader br)
    {
        _resetVector = br.ReadInt32(); _nmiVector = br.ReadInt32(); _irqVector = br.ReadInt32(); _snesMessage = br.ReadInt32();
        _sa1Message = br.ReadInt32(); _snesCharConvIrqEnabled = br.ReadBoolean(); _snesCharConvIrqFlag = br.ReadBoolean(); _mmcBanks = ReadArray<int>(br, _mmcBanks.Length);
        _sa1IrqEnabled = br.ReadBoolean(); _sa1NmiEnabled = br.ReadBoolean(); _nmiVectorSelect = br.ReadBoolean(); _irqVectorSelect = br.ReadBoolean();
        _irqEnabled = br.ReadBoolean(); _irqRequest = br.ReadBoolean(); _sa1IrqRequest = br.ReadBoolean(); _sa1Ready = br.ReadBoolean();
        _sa1Reset = br.ReadBoolean(); _sa1NmiRequest = br.ReadBoolean(); _charDmaActive = br.ReadBoolean(); _bwCpuBank = br.ReadInt32();
        _bwSa1Bank = br.ReadInt32(); _bwRamRegionProtect = br.ReadInt32(); _cpuIramProtect = br.ReadInt32(); _mathControl = br.ReadInt32();
        _mathResult = br.ReadInt32(); _dmaControl = br.ReadBoolean(); _dmaPriority = br.ReadInt32(); _dmaMode = br.ReadBoolean();
        _dmaConvType = br.ReadInt32(); _dmaDstDevice = br.ReadInt32(); _dmaSrcDevice = br.ReadInt32(); _dmaCharConv = br.ReadBoolean();
        _dmaSrcStartAddr = br.ReadInt32(); _dmaDstStartAddr = br.ReadInt32(); _dmaTerminalCounter = br.ReadInt32();
    }

    public List<RegisterInfo> GetIORegisters() =>
[
    new("2200","",""),
        new("2200|0-3","Message",$"{_snesMessage:X2}"),
        new("2200|4","Nmi Request",$"{_sa1NmiRequest}"),
        new("2200|5","Reset",$"{_sa1Reset}"),
        new("2200|6","Wait",$"{_sa1Ready}"),
        new("2200|7","Sa1 Irq Request",$"{_sa1IrqRequest}"),
        new("2201","",$""),
        new("2201|5","Char Conv Irq Enabled",$"{_snesCharConvIrqEnabled}"),
        new("2201|7","Irq Enabled",$"{_irqEnabled}"),
        new("2202","",$""),
        new("2202|5","Char Irq Flag",$"{_snesCharConvIrqFlag}"),
        new("2203/4","Reset Vector",$"{_resetVector:X4}"),
        new("2205/6","Nmi Vector",$"{_nmiVector:X4}"),
        new("2207/8","Irq Vector",$"{_irqVector:X4}"),
        new("2209|4","Nmi Vector Select",$"{_nmiVectorSelect}"),
        new("2209|5","Irq Vector Select",$"{_irqVectorSelect}"),
        new("2209|7","Irq Request",$"{_irqRequest}"),
        new("220A|4","Sa1 Nmi Enabled",$"{_sa1NmiEnabled}"),
        new("220A|7","Sa1 Irq Enabled",$"{_sa1IrqEnabled}"),
        new("","Mmc Banks",$""),
        new("2220","Mmc Bank C",$"{_mmcBanks[0]:X2}"),
        new("2221","Mmc Bank D",$"{_mmcBanks[1]:X2}"),
        new("2222","Mmc Bank E",$"{_mmcBanks[2]:X2}"),
        new("2223","Mmc Bank F",$"{_mmcBanks[3]:X2}"),
        new("","Ram Banks",$""),
        new("2224|0-4","Cpu Bw Ram Bank",$"{_bwCpuBank:X2}"),
        new("2225|0-6","Sa1 Bw Ram Bank",$"{_bwSa1Bank:X2}"),
        new("","Dma",$""),
        new ("2230|0-1","Dma Src Device",$"{_dmaSrcDevice}"),
        new ("2230|2-3","Dma Src Dst",$"{_dmaDstDevice}"),
        new ("2230|4","Dma Mode",$"{_dmaMode}"),
        new ("2230|5","Dma Conv Type",$"{_dmaConvType}"),
        new ("2230|6","Dma Priority",$"{_dmaPriority}"),
        new ("2230|7","Dma Enabled",$"{_dmaControl}"),
        new("2232","Dma Src Addr",$"{_dmaSrcStartAddr:X6}"),
        new("2235","Dma Dst Addr",$"{_dmaDstStartAddr:X6}"),
        new("","Math",$""),
        new("2250|0-1","Math Control",$"{_mathControl}"),
        new("2251/2","Multiplicand",$"{_multiplicand:X2}"),
        new("2253/4","Multiplier",$"{_multiplier:X4}"),
        new("","Cpu Status",$""),
        new("2300|5","Char Conv Flag",$"{_snesCharConvIrqFlag}"),
        new("","",$""),
        new("2306-A","Math Result",$"{_mathResult:X4}"),
    ];
}
