using Gmulator.Core.Snes.Gsu;
using Gmulator.Interfaces;
using System.Collections;
using System.Diagnostics;
using System.Security.Principal;

namespace Gmulator.Core.Snes;

public partial class SnesGsu(Snes snes) : IConsole, ICpu, IGsu
{
    private const int FZ = 1 << 1;
    private const int FC = 1 << 2;
    private const int FS = 1 << 3;
    private const int FV = 1 << 4;
    public const int FG = 1 << 5;
    public const int FR = 1 << 6;
    private const int FAlt1 = 1 << 8;
    private const int FAlt2 = 1 << 9;
    private const int FL = 1 << 10;
    private const int FH = 1 << 11;
    private const int FB = 1 << 12;
    private const int FI = 1 << 15;

    #region State
    private ushort[] _registers;
    private ulong _cycles;
    private byte _pbr;
    private int _statusFlag;
    private int _backupRam;
    private byte[] _cacheRam;
    private bool[] _cacheValid;

    private bool _irqDisabled;
    private bool _highSpeed;
    private int _romBank;
    private int _ramBank;
    private int _ramAddr;
    private int _ramDelay;
    private int _ramWriteAddr;
    private int _ramWriteValue;

    private int _screenBase;
    private int _screenMode;
    private int _screenHeight;
    private int _versionMode;
    private bool _ramAccess;
    private bool _romAccess;

    private bool _plotTransparent;
    private bool _plotDither;
    private bool _colorHighNibble;
    private bool _colorFreezeHigh;
    private bool _objMode;

    private int _cacheBase;

    private int _plotReg;
    private byte _colorReg;
    private byte _romBuffer;

    private byte _colorBpp;

    private int _srcReg;
    private int _dstReg;

    private bool _r15Changed;
    private bool _clockSelect;

    private bool _stopped;
    #endregion

    private PixelCache PrimaryCache;
    private PixelCache SecondaryCache;
    private struct PixelCache(int x, int y, int[] pixels, int validBits)
    {
        public int X = x;
        public int Y = y;
        public int[] Pixels = pixels;
        public int ValidBits = validBits;
        public readonly PixelCache DeepCopy => new(X, Y, (int[])Pixels.Clone(), ValidBits);
    }

    private byte[] _rom;
    private readonly SnesMapper Mapper = snes.Mapper;


    public bool Zero => (_statusFlag & FZ) != 0;
    public bool Carry => (_statusFlag & FC) != 0;
    public bool Sign => (_statusFlag & FS) != 0;
    public bool Overflow => (_statusFlag & FV) != 0;
    public bool Go => (_statusFlag & FG) != 0;
    public bool RomRead => (_statusFlag & FR) != 0;
    public bool Alt1 => (_statusFlag & FAlt1) != 0;
    public bool Alt2 => (_statusFlag & FAlt2) != 0;
    public bool ImmLow => (_statusFlag & FL) != 0;
    public bool ImmHigh => (_statusFlag & FH) != 0;
    public bool Prefix => (_statusFlag & FB) != 0;
    public bool Irq => (_statusFlag & FI) != 0;

    public bool Alt3 => Alt1 && Alt2;
    public int StatusFlag => _statusFlag;

    public int SourceReg => _srcReg;
    public int DestinationReg => _dstReg;

    public int Opcode { get => _opcode; private set => _opcode = value; }
    public ulong Cycles => _cycles;
    private int _opcode;

    private readonly Snes Snes = snes;
    public SnesGsuMmu Mmu = new();

    private readonly List<Breakpoint> Breakpoints = snes.Breakpoints;
    private readonly Action<DebugState> SetState;
    private readonly Func<int, bool> ExecuteCheck;
    private readonly Func<int, int, int, bool, bool> AccessCheckSpc;
    private int PrevPC;

    public MemoryMap GsuMap = new(0x1000);
    private int _romDelay;
    private RamType RamType;

    public int TestAddr { get; private set; }

    public int PC
    {
        get => PrevPC;
    }

    public int CurrentPC => _pbr << 16 | _registers[15];

    public int GetCurrentOpcode => _opcode;

    public ICpu Cpu => this as ICpu;

    public IPpu Ppu => Snes.Ppu;

    IMmu IConsole.Mmu => (IMmu)Mmu;

    public DebugState DbgState { get; set; }
    List<Breakpoint> IConsole.Breakpoints { get; set; }
    public bool Run { get; set; }

    public string GameName => Snes.GameName;

    ulong ICpu.Cycles => _cycles;

    public Action Tick { get; set; }
    public int StepOverAddr { get; set; }

    private void SetMemoryMap(int ramsize)
    {
        var Mapper = Snes.Mapper;
        var Ppu = Snes.Ppu;
        var CpuMap = Snes.CpuMap;

        CpuMap.Sram(0x00, 0x3f, 0x6000, 0x7fff, ReadGsuRam, WriteGsuRam);
        CpuMap.Sram(0x80, 0xbf, 0x6000, 0x7fff, ReadGsuRam, WriteGsuRam);
        CpuMap.Sram(0x70, 0x71, 0x0000, ramsize - 1, ReadGsuRam, WriteGsuRam);
        CpuMap.Sram(0x7c, 0x7d, 0x0000, ramsize - 1, ReadGsuRam, WriteGsuRam);

        GsuMap.LoRom(0x00, 0x3f, 0x8000, 0xffff, ReadPrg, Write);
        GsuMap.LoRom(0x00, 0x3f, 0x0000, 0x7fff, ReadPrg, Write);
        GsuMap.LoRom(0x40, 0x5f, 0x0000, 0xffff, ReadPrg, Write);
        CpuMap.LoRom(0x00, 0x3f, 0x8000, 0xffff, Mapper.Read, Write);
        CpuMap.LoRom(0x80, 0xbf, 0x8000, 0xffff, Mapper.Read, Write);
        CpuMap.LoRom(0x40, 0x5f, 0x0000, 0xffff, Mapper.Read, Write);
        CpuMap.LoRom(0xc0, 0xdf, 0x0000, 0xffff, Mapper.Read, Write);

        GsuMap.Sram(0x70, 0x71, 0x0000, ramsize - 1, ReadGsuRam, WriteGsuRam);
        GsuMap.Sram(0x7c, 0x7d, 0x0000, ramsize - 1, ReadGsuRam, WriteGsuRam);
        // GsuMap.LoRom(0xc0, 0xdf, 0x8000, 0xffff, Mapper.Read, Mapper.Write);

        //CpuMap.Sram(0x40, 0x4f, 0x0000, 0xffff, Mapper.ReadSram, Mapper.WriteSram);

        CpuMap.Register(0x00, 0x3f, 0x2000, 0x2fff, Ppu.Read, Ppu.Write);
        CpuMap.Register(0x80, 0xbf, 0x2000, 0x2fff, Ppu.Read, Ppu.Write);

        CpuMap.Register(0x00, 0x3f, 0x3000, 0x3fff, ReadIO, WriteIO);
        CpuMap.Register(0x80, 0xbf, 0x3000, 0x3fff, ReadIO, WriteIO);
    }

    public void Reset(int ramsize)
    {
        _statusFlag = 0;
        _registers = new ushort[16];
        _cacheRam = new byte[512];
        _cacheValid = new bool[32];
        _rom = Snes.Mapper?.Rom;
        _opcode = 0x01;
        _cacheBase = 0;
        _r15Changed = false;
        _cycles = 0;
        _ramDelay = 0;
        _romDelay = 0;
        PrevPC = 0;
        PrimaryCache = new PixelCache(0, 0, new int[8], 0);
        SecondaryCache = new PixelCache(0, 0, new int[8], 0);
        Mmu.Reset(ramsize, Mapper.Name);
        SetMemoryMap(ramsize);
    }

    public void Step(ulong cycles, bool step = false)
    {
        while (Go && _cycles < cycles)
        {
            Exec(step);
            //_cycles++;
            if (Snes.DbgState == DebugState.Break)
                return;
        }

        if (cycles > _cycles)
            StepCycle((int)(cycles - _cycles));
    }

    public void Exec(bool step)
    {
        if (Go || step)
        {
            if (Snes.Debug && Snes.Breakpoints.Count > 0)
            {
                if (Snes.DbgState == DebugState.Break)
                    return;

                if (!Snes.Run && Snes.Debugger.Execute(PC, CpuType.Gsu))
                {
                    Snes.DbgState = DebugState.Break;
                    return;
                }
            }

            if (_opcode == 0x00)
                TestAddr = PC;

            int op = ReadOpcode();
            switch (op)
            {
                case 0x00: Stop(); break;
                case 0x01: Nop(); break;
                case 0x02: Cache(); break;
                case 0x03: Lsr(); break;
                case 0x04: Rol(); break;
                case 0x05: Branch(true); break;
                case 0x06: Branch(((_statusFlag & FS) ^ (_statusFlag & FV)) == 0); break;
                case 0x07: Branch(((_statusFlag & FS) ^ (_statusFlag & FV)) != 0); break;
                case 0x08: Branch(!Zero); break;
                case 0x09: Branch(Zero); break;
                case 0x0a: Branch(!Sign); break;
                case 0x0b: Branch(Sign); break;
                case 0x0c: Branch(!Carry); break;
                case 0x0d: Branch(Carry); break;
                case 0x0e: Branch(!Overflow); break;
                case 0x0f: Branch(Overflow); break;
                case >= 0x10 and <= 0x1f: To(op); break;
                case >= 0x20 and <= 0x2f: With(op); break;
                case >= 0x30 and <= 0x3b: Stw(op); break;
                case 0x3c: Loop(); break;
                case 0x3d: AltOne(); break;
                case 0x3e: AltTwo(); break;
                case 0x3f: AltThree(); break;
                case >= 0x40 and <= 0x4b: Ldw(op); break;
                case 0x4c: Plot(); break;
                case 0x4d: Swap(); break;
                case 0x4e: Color(); break;
                case 0x4f: Not(); break;
                case >= 0x50 and <= 0x5f: Add(op); break;
                case >= 0x60 and <= 0x6f: Sub(op & 0x0f); break;
                case 0x70: Merge(); break;
                case >= 0x71 and <= 0x7f: And(op); break;
                case >= 0x80 and <= 0x8f: Mult(op); break;
                case 0x90: Sbk(); break;
                case >= 0x91 and <= 0x94: Link(op); break;
                case 0x95: Sex(); break;
                case 0x96: Asr(); break;
                case 0x97: Ror(); break;
                case >= 0x98 and <= 0x9d: Jmp(op); break;
                case 0x9e: Lob(); break;
                case 0x9f: Fmult(); break;
                case >= 0xa0 and <= 0xaf: Ibt(op); break;
                case >= 0xb0 and <= 0xbf: From(op & 0x0f); break;
                case 0xc0: Hib(); break;
                case >= 0xc1 and <= 0xcf: Or(op & 0x0f); break;
                case >= 0xd0 and <= 0xde: Inc(op); break;
                case 0xdf: GetC(); break;
                case >= 0xe0 and <= 0xee: Dec(op); break;
                case 0xef: GetB(); break;
                case >= 0xf0 and <= 0xff: Iwt(op & 0x0f); break;
                default: break;
            }

            if (Go && Snes.GsuLogger.Logging)
                Snes.GsuLogger.Log(Snes.Ppu.HPos);

            if (_r15Changed)
                _r15Changed = false;
            else
                _registers[15]++;
        }
    }

    private void StepCycle(int cycles)
    {
        _cycles += (ulong)cycles;
        if (_ramDelay != 0)
        {
            _ramDelay -= Math.Min(cycles, _ramDelay);
            if (_ramDelay == 0)
            {
                WriteByte(0x700000 | _ramBank << 16 | _ramWriteAddr, (byte)_ramWriteValue);
            }
        }

        if (_romDelay != 0)
        {
            _romDelay -= Math.Min(cycles, _romDelay);
            if (_romDelay == 0)
            {
                _romBuffer = (byte)ReadByte(_romBank << 16 | _registers[14]);
                _statusFlag &= ~FR;
            }
        }
    }

    public int ReadDebug(int addr)
    {
        addr = (addr & 0xff0000) | addr & 0xffff;
        int cacheAddr = _registers[15] - _cacheBase;
        //if (addr < 512)
        //{
        //return _cacheRam[cacheAddr];
        //}
        //else
        {
            if (_pbr <= 0x5f)
            {

            }
            else
            {

            }
            return ReadPrg(addr);
        }
    }

    public int ReadByte(int addr)
    {
        addr &= 0xffffff;
        int b = addr >> 12;
        RamType = GsuMap.Handlers[b].Type;
        int value = GsuMap.Handlers[b].Read(addr);
        if (Snes.Debug)
            Snes.Debugger.Watchpoint(addr, value, CpuType.Gsu, false);
        return (byte)value;
    }

    public void WriteByte(int addr, int value)
    {
        addr &= 0xffffff;
        int b = addr >> 12;
        RamType = GsuMap.Handlers[b].Type;
        GsuMap.Handlers[b].Write(addr, value);
        if (Snes.Debug)
            Snes.Debugger.Watchpoint(addr, value, CpuType.Gsu, true);
    }

    public int ReadPrg(int addr)
    {
        if (_rom == null) return 0;
        int a = GsuMap.Handlers[addr >> 12].Offset + (addr & 0xfff);
        return _rom[a % _rom.Length];
    }

    public int ReadPrg2(int addr)
    {
        if (_rom == null) return 0;
        return _rom[addr % _rom.Length];
    }

    public void WritePrg2(int addr, int value)
    {
        if (_rom == null) return;
        _rom[addr % _rom.Length] = (byte)value;
    }

    public void Write(int addr, int value)
    {
    }

    private int ReadGsuRam(int addr)
    {
        return Mmu.Read(addr & Snes.CpuMap.Handlers[addr >> 12].Mask);
    }

    private void WriteGsuRam(int addr, int value)
    {
        Mmu.Write(addr & Snes.CpuMap.Handlers[addr >> 12].Mask, value);
    }

    private int ReadOpcode()
    {
        int v = _opcode;
        _opcode = ReadByte();
        return v;
    }

    private int ReadValue()
    {
        int v = _opcode;
        _registers[15]++;
        _opcode = ReadByte();
        return v;
    }

    private int ReadByte()
    {
        int addr = PrevPC = _pbr << 16 | _registers[15];
        ushort cacheAddr = (ushort)(_registers[15] - _cacheBase);
        if (cacheAddr < 512)
        {
            if (!_cacheValid[cacheAddr >> 4])
                InitRamCache(cacheAddr & 0xfff0);

            StepCycle(_clockSelect ? 1 : 2);

            return _cacheRam[cacheAddr];
        }
        else
        {
            if (_pbr <= 0x5f)
            {
                WaitRamOperation();
            }
            else
            {
                WaitRamOperation();
            }

            StepCycle(_clockSelect ? 5 : 6);
            return ReadByte(addr);
        }
    }

    private void InitRamCache(int addr)
    {
        if (_pbr <= 0x5f)
        {
            WaitRamOperation();
        }
        else
        {
            WaitRamOperation();
        }

        ushort dst = (ushort)(addr & 0x1f0);
        int baseAddr = (_pbr << 16) + _cacheBase + dst;
        for (int i = 0; i < 16; i++)
        {
            _cacheRam[dst + i] = (byte)ReadByte(baseAddr + i);
        }
        StepCycle(_clockSelect ? 5 * 16 : 6 * 16);
        _cacheValid[addr >> 4] = true;
    }

    private byte ReadRomBuffer()
    {
        WaitRomOperation();
        return _romBuffer;
    }

    private int ReadRamBuffer(int addr)
    {
        WaitRamOperation();
        return ReadByte(0x700000 | _ramBank << 16 | addr);
    }

    private void UpdateRam(int addr, int value)
    {
        WaitRamOperation();
        _ramDelay = _clockSelect ? 5 : 6;
        _ramWriteAddr = addr & 0xffff;
        _ramWriteValue = value & 0xff;
    }

    private void WaitRomOperation()
    {
        if (_romDelay != 0)
            StepCycle(_romDelay);
    }

    private void WaitRamOperation()
    {
        if (_ramDelay != 0)
            StepCycle(_ramDelay);
    }

    private void WriteRegister(int register, int value)
    {
        _registers[register] = (ushort)value;

        if (register == 14)
        {
            _statusFlag |= FR;
            _romDelay = _clockSelect ? 5 : 6;
        }
        else if (register == 15)
        {
            _r15Changed = true;
        }
    }

    public int ReadIO(int addr)
    {
        switch (addr & 0xffff)
        {
            case >= 0x3000 and <= 0x301f:
                if ((addr & 1) == 0)
                    return (byte)(_registers[(addr & 0x1f) / 2] & 0xff);
                else
                    return (byte)(_registers[(addr & 0x1f) / 2] >> 8);
            case 0x3030: return (byte)_statusFlag;
            case 0x3031: return (byte)(_statusFlag >> 8);
            case 0x3034: return _pbr;
            case 0x3036: return _romBank;
            case 0x303b: return _versionMode;
            case 0x303c: return _ramBank;
            case 0x303e: return _cacheBase;
        }
        return 0;
    }

    public void WriteIO(int addr, int val)
    {
        byte value = (byte)val;
        int a = addr & 0xffff;
        switch (a)
        {
            case >= 0x3000 and <= 0x301f:
                if ((a & 1) == 0)
                    _registers[(a & 0x1f) / 2] = value;
                else
                {
                    _registers[(a & 0x1f) / 2] |= (ushort)(value << 8);

                    int reg = (a >> 1) & 0x0f;
                    if (reg == 14)
                    {
                        _statusFlag |= FR;
                        _romDelay = _clockSelect ? 5 : 6;
                    }
                    else if (a == 0x301f)
                    {
                        _statusFlag |= 0x20;
                    }
                }
                break;
            case 0x3030:
                bool running = (StatusFlag & FG) == FG;
                _statusFlag = (_statusFlag & 0xff00) | value & 0xff;
                if (running&& (StatusFlag & FG) == 0)
                {
                    _cacheBase = 0;
                    InvalidateCache();
                }
                break;
            case 0x3031:
                _statusFlag = (_statusFlag & 0xff) | value << 8;
                break;
            case 0x3033:
                _backupRam = value;
                break;
            case 0x3034:
                _pbr = (byte)(value & 0x7f);
                InvalidateCache();
                break;
            case 0x3036:
                _romBank = value & 0x7f;
                break;
            case 0x3037:
                _irqDisabled = (value & 0x80) != 0;
                _highSpeed = (value & 0x10) != 0;
                break;
            case 0x3038: _screenBase = value; break;
            case 0x3039: _clockSelect = (value & 0x01) != 0; break;
            case 0x303a:
                _screenMode = value;
                _screenHeight = (_screenMode & 0x20) >> 4 | (_screenMode & 0x04) >> 2;
                _colorBpp = (value & 3) switch
                {
                    0 => 2,
                    1 or 2 => 4,
                    3 => 8,
                    _ => 0
                };

                _romAccess = (value & 0x10) != 0;
                _ramAccess = (value & 0x08) != 0;
                break;
            case >= 0x3100 and <= 0x32ff:
                int cacheAddr = _cacheBase + (a - 0x3100) & 0x1ff;
                _cacheRam[cacheAddr] = value;
                if ((cacheAddr & 0x0f) == 0x0f)
                    _cacheValid[cacheAddr >> 4] = true;
                break;
        }
    }

    private void ResetFlags()
    {
        _statusFlag &= ~0x1300;
        _srcReg = 0;
        _dstReg = 0;
    }

    public ushort[] GetRegs() => _registers;

    public int GetSfr() => _statusFlag;
    public int GetSrcReg() => _srcReg;
    public int GetDstReg() => _dstReg;

    public Dictionary<string, bool> GetFlags() => new()
    {
        ["Z"] = (_statusFlag & FZ) != 0,
        ["S"] = (_statusFlag & FS) != 0,
        ["G"] = (_statusFlag & FG) != 0,
        ["Alt1"] = (_statusFlag & FAlt1) != 0,
        ["B"] = (_statusFlag & FB) != 0,
        ["C"] = (_statusFlag & FC) != 0,
        ["V"] = (_statusFlag & FV) != 0,
        ["R"] = (_statusFlag & FB) != 0,
        ["Alt2"] = (_statusFlag & FAlt2) != 0,
        ["I"] = (_statusFlag & FI) != 0,
    };

    public List<RegisterInfo> GetRegisterInfo() =>
    [
        new("3037|5","Speed Mode",""),
        new("3037|7","Irq Disabled",""),
        new("3038","Screen Base",$"{_screenBase}"),
        new("3039|0","Clock Select",$"{_clockSelect}"),
        new("303A|0-1","Color Gradient",""),
        new("303A|2.5","Screen Height",$"{_screenMode >> 2 | _screenMode >> 5}"),
        new("303A|3","Gsu Ram Access",""),
        new("303A|4","Gsu Rom Access",""),
        new("","CMODE",""),
        new("","Transparent",""),
    ];

    public List<RegisterInfo> GetRegisters()
    {
        List<RegisterInfo> list = [];
        for (int i = 0; i < _registers.Length; i++)
        {
            list.Add(new RegisterInfo((i + 1) % 4 == 0 ? $"-" : "", $"R{i}", $"{_registers[i]:X4}"));
        }
        return list;
    }

    List<RegisterInfo> ICpu.GetFlags() =>
    [
        new("","Z",$"{Zero:X2}"),
        new("","C",$"{Carry:X2}"),
        new("","S",$"{Sign:X2}"),
        new("","A1",$"{Alt1:X2}"),
        new("-","P",$"{Prefix:X2}"),
        new("","V",$"{Overflow:X2}"),
        new("","G",$"{Go:X2}"),
        new("","R",$"{RomRead:X2}"),
        new("","A2",$"{Alt2:X2}"),
        new("","I",$"{Irq:X2}"),
    ];

    public List<RegisterInfo> GetFlagsArray() =>
    [
        new("i","I",Irq),
        new("-","-",false),
        new("-","-",false),
        new("p","P",Prefix),
        new("j","J",ImmHigh),
        new("i","I",ImmLow),
        new("-","2",Alt2),
        new("-","1",Alt1),
        new("-","-",false),
        new("-","-",RomRead),
        new("r","R",Go),
        new("V","V",Overflow),
        new("s","S",Sign),
        new("c","C",Carry),
        new("z","Z",Zero),
        new("-","-",false),
    ];

    public List<RegisterInfo> GetMisc() =>
    [
        new("", "Src", $"{_srcReg:X2}"),
        new("", "Dst", $"{_dstReg:X2}"),
        new("", "Pbr", $"{_pbr:X2}"),
        new("", "Rom", $"{_romBank:X2}"),
        new("", "Ram", $"{_ramBank:X2}"),
        new("-", "Color", $"{_colorReg:X2}"),
        new("", "RomB", $"{_romBuffer:X2}"),
        new("", "Sfr", $"{_statusFlag:X4}"),
        new("", "Addr", $"{_ramAddr:X4}"),
    ];

    public int GetReg(string reg)
    {
        return 0;
    }

    public void SetReg(string reg, int value)
    {

    }

    public void Step()
    {

    }

    public void Save(BinaryWriter bw)
    {
        WriteArray(bw, _registers);
        bw.Write(_cycles); bw.Write(_pbr);
        bw.Write(_statusFlag); bw.Write(_backupRam);
        WriteArray(bw, _cacheRam); WriteArray(bw, _cacheValid);
        bw.Write(_irqDisabled); bw.Write(_highSpeed);
        bw.Write(_romBank); bw.Write(_ramBank);
        bw.Write(_ramAddr); bw.Write(_ramDelay);
        bw.Write(_ramWriteAddr); bw.Write(_ramWriteValue);
        bw.Write(_screenBase); bw.Write(_screenMode);
        bw.Write(_screenHeight); bw.Write(_versionMode);
        bw.Write(_ramAccess); bw.Write(_romAccess);
        bw.Write(_plotTransparent); bw.Write(_plotDither);
        bw.Write(_colorHighNibble); bw.Write(_colorFreezeHigh);
        bw.Write(_objMode); bw.Write(_cacheBase);
        bw.Write(_plotReg); bw.Write(_colorReg);
        bw.Write(_romBuffer); bw.Write(_colorBpp);
        bw.Write(_srcReg); bw.Write(_dstReg);
        bw.Write(_r15Changed); bw.Write(_clockSelect);
        bw.Write(_stopped);

        bw.Write(PrimaryCache.X); bw.Write(PrimaryCache.Y);
        bw.Write(PrimaryCache.ValidBits); WriteArray(bw, PrimaryCache.Pixels);
        bw.Write(SecondaryCache.X); bw.Write(SecondaryCache.Y);
        bw.Write(SecondaryCache.ValidBits); WriteArray(bw, SecondaryCache.Pixels);

        Mmu?.Save(bw);
    }

    public void Load(BinaryReader br)
    {
        _registers = ReadArray<ushort>(br, _registers.Length);
        _cycles = br.ReadUInt64(); _pbr = br.ReadByte();
        _statusFlag = br.ReadInt32(); _backupRam = br.ReadInt32();
        _cacheRam = ReadArray<byte>(br, _cacheRam.Length); _cacheValid = ReadArray<bool>(br, _cacheValid.Length);
        _irqDisabled = br.ReadBoolean(); _highSpeed = br.ReadBoolean();
        _romBank = br.ReadInt32(); _ramBank = br.ReadInt32();
        _ramAddr = br.ReadInt32(); _ramDelay = br.ReadInt32();
        _ramWriteAddr = br.ReadInt32(); _ramWriteValue = br.ReadInt32();
        _screenBase = br.ReadInt32(); _screenMode = br.ReadInt32();
        _screenHeight = br.ReadInt32(); _versionMode = br.ReadInt32();
        _ramAccess = br.ReadBoolean(); _romAccess = br.ReadBoolean();
        _plotTransparent = br.ReadBoolean(); _plotDither = br.ReadBoolean();
        _colorHighNibble = br.ReadBoolean(); _colorFreezeHigh = br.ReadBoolean();
        _objMode = br.ReadBoolean(); _cacheBase = br.ReadInt32();
        _plotReg = br.ReadInt32(); _colorReg = br.ReadByte();
        _romBuffer = br.ReadByte(); _colorBpp = br.ReadByte();
        _srcReg = br.ReadInt32(); _dstReg = br.ReadInt32();
        _r15Changed = br.ReadBoolean(); _clockSelect = br.ReadBoolean();
        _stopped = br.ReadBoolean();

        PrimaryCache.X = br.ReadInt32(); PrimaryCache.Y = br.ReadInt32();
        PrimaryCache.ValidBits = br.ReadInt32();
        PrimaryCache.Pixels = ReadArray<int>(br, PrimaryCache.Pixels.Length);
        SecondaryCache.X = br.ReadInt32(); SecondaryCache.Y = br.ReadInt32();
        SecondaryCache.ValidBits = br.ReadInt32();
        SecondaryCache.Pixels = ReadArray<int>(br, SecondaryCache.Pixels.Length);

        Mmu?.Load(br);
    }

    public void Reset(string name, bool reset)
    {

    }
}
