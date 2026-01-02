using Gmulator.Core.Gbc;
using Gmulator.Interfaces;
using ImGuiNET;
using static Gmulator.Interfaces.IMmu;

namespace Gmulator.Core.Nes
{
    public class NesPpu() : ISaveState, IPpu
    {
        private readonly int[] MirrorNt0 = [0, 0, 0, 0];
        private readonly int[] MirrorNt1 = [1, 1, 1, 1];
        private List<MemoryHandler> MemoryHandlers;

        public byte[] Vram { get; private set; } = new byte[0x4000];
        public byte[] Oram { get; private set; } = new byte[0x100];
        public byte[] Pram { get; private set; } = new byte[0x2000];
        private int _dummy2007;
        private int _oamDma;
        private int _ntAddr;
        private int _atAddr;
        private int _bgAddr;
        private int _ntByte;
        private int _atByte;
        private int _bgLo;
        private int _bgHi;
        private int _atLo;
        private int _atHi;
        private int _bgShiftLo;
        private int _bgShiftHi;
        private int _atShiftLo;
        private int _atShiftHi;
        public int Scanline { get; private set; }
        public int Cycle { get; private set; }
        public int Cycles { get; set; }
        public uint FrameCounter { get; private set; }
        public ulong Totalcycles { get; private set; }
        public bool NoNmi { get; private set; }

        private int _vramAddr;
        private int _tempAddr;
        private int _fineX;
        private bool _writeToggle;

        private int _a12;
        private int _oamData;
        private int _oamAddr;

        private int _nametable;
        private int _vaddrIncrease;
        private bool _sprTable;
        private int _bgTable;
        private bool _spriteSize;
        private int _masterSelect;
        private bool _nmi;

        private int _greyscale;
        private bool _backgroundLeft;
        private bool _spriteLeft;
        private bool _background;
        private bool _sprite;
        private int _red;
        private int _green;
        private int _blue;

        private int _lsb;
        private bool _sprOverflow;
        private bool _sprite0hit;
        private bool _vBlank;

        private List<SpriteData> SpriteScan;

        private enum CycleState
        {
            Start = 1, End = 257, Pre1 = 321, Pre2 = 336,
        }

        private enum ScanlineState
        {
            End = 239, Idle = 240, Vblank = 241, Pre = -1
        }

        private int ScanlineFrameEnd;

        public bool IsRendering => _background || _sprite;
        public int FineY => ((_vramAddr & 0x7000) >> 12) & 0xff;
        public int GetScanline() => Scanline;
        public int ReadOam(int a) => Oram[a & 0xff];
        public void WriteOam(int a, int v) => Oram[a & 0xff] = (byte)v;
        public int ReadVram(int a) => Vram[a & 0x3fff];

        private NesMmu Mmu;
        private NesApu Apu;
        public uint[] NametableBuffer { get; private set; } = new uint[NesWidth * NesWidth * 4];
        public uint[] ScreenBuffer { get; set; }

        private Nes Nes;

        private readonly uint[] pixPalettes = new uint[192 / 3];
        private readonly byte[] palBuffer =
        [
            0x61,0x61,0x61,0x00,0x00,0x88,0x1F,0x0D,0x99,0x37,0x13,0x79,0x56,0x12,0x60,0x5D,
            0x00,0x10,0x52,0x0E,0x00,0x3A,0x23,0x08,0x21,0x35,0x0C,0x0D,0x41,0x0E,0x17,0x44,
            0x17,0x00,0x3A,0x1F,0x00,0x2F,0x57,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
            0xAA,0xAA,0xAA,0x0D,0x4D,0xC4,0x4B,0x24,0xDE,0x69,0x12,0xCF,0x90,0x14,0xAD,0x9D,
            0x1C,0x48,0x92,0x34,0x04,0x73,0x50,0x05,0x5D,0x69,0x13,0x16,0x7A,0x11,0x13,0x80,
            0x08,0x12,0x76,0x49,0x1C,0x66,0x91,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
            0xFC,0xFC,0xFC,0x63,0x9A,0xFC,0x8A,0x7E,0xFC,0xB0,0x6A,0xFC,0xDD,0x6D,0xF2,0xE7,
            0x71,0xAB,0xE3,0x86,0x58,0xCC,0x9E,0x22,0xA8,0xB1,0x00,0x72,0xC1,0x00,0x5A,0xCD,
            0x4E,0x34,0xC2,0x8E,0x4F,0xBE,0xCE,0x42,0x42,0x42,0x00,0x00,0x00,0x00,0x00,0x00,
            0xFC,0xFC,0xFC,0xBE,0xD4,0xFC,0xCA,0xCA,0xFC,0xD9,0xC4,0xFC,0xEC,0xC1,0xFC,0xFA,
            0xC3,0xE7,0xF7,0xCE,0xC3,0xE2,0xCD,0xA7,0xDA,0xDB,0x9C,0xC8,0xE3,0x9E,0xBF,0xE5,
            0xB8,0xB2,0xEB,0xC8,0xB7,0xE5,0xEB,0xAC,0xAC,0xAC,0x00,0x00,0x00,0x00,0x00,0x00
        ];

        public void Init(Nes nes)
        {
            Nes = nes;
            Mmu = nes.Mmu;
            Apu = nes.Apu;
            ScreenBuffer = new uint[NesWidth * NesHeight * 4];

            MemoryHandlers = [];
            for (int i = 0; i < 0x4000; i++)
            {
                MemoryHandlers.Add(new(0, 0, 0, 0, 0, (int a) => 0, (int a, int v) => { }, RamType.None));
            }

            nes.SetMemory(0x00, 0x01, 0x0000, 0x3fff, 0x3fff, Read, Write, RamType.Vram, 1);
        }

        public void Reset()
        {
            Scanline = 0;
            _dummy2007 = 0;
            _vramAddr = _tempAddr = _fineX = 0;
            _writeToggle = false;
            _nmi = false;
            _sprOverflow = false;
            _sprite0hit = false;
            _vBlank = false;

            Cycle = 25;
            Totalcycles = 7;
            FrameCounter = 1;
            //if (Cart.Region == 0)
            ScanlineFrameEnd = 261;
            //else
            //    ScanlineFrameEnd = 311;


            for (int i = 0; i < palBuffer.Length; i += 3)
                pixPalettes[i / 3] = (uint)(palBuffer[i] | palBuffer[i + 1] << 8 | palBuffer[i + 2] << 16 | 0xff000000);

            Array.Fill(ScreenBuffer, 0xff000000);
            SpriteScan = [];
        }

        public void Step(int c)
        {
            Apu.Step(1);

            //if (Cart.Region == 1)
            //    c = 2;

            while (c-- > 0)
            {
                if ((Scanline < 240 || Scanline == (int)ScanlineState.Pre) && Cycle == 324 && IsRendering)
                    Mmu.Mapper.Scanline();

                if (Scanline >= 0 && Scanline <= (int)ScanlineState.End && IsRendering)
                {
                    if ((Cycle >= (int)CycleState.Start && Cycle < (int)CycleState.End) || (Cycle >= (int)CycleState.Pre1 && Cycle <= (int)CycleState.Pre2))
                    {
                        GetTiles();
                        RenderPixels();
                    }

                    if (Cycle == 338 || Cycle == 339)
                        _ntByte = Read(0x2000 | _vramAddr & 0xfff);

                    //increment y scroll
                    if (Cycle == 257)
                    {
                        EvalSprites();
                        LoadRegisters();
                        YIncrease();
                    }

                    //copy horizontal bits
                    if (Cycle == 256)
                        _vramAddr = _vramAddr & ~0x41f | _tempAddr & 0x41f;
                }
                else if (Scanline == (int)ScanlineState.Idle)
                {
                    if (Cycle == 1)
                    {
                        FrameCounter++;
                    }
                }
                else if (Scanline == (int)ScanlineState.Vblank)
                {
                    if (Cycle == 0)
                    {
                        _vBlank = true;
                        if (_nmi && !NoNmi)
                            Nes.Cpu.NmiTriggered = 1;
                    }
                }
                else if (Scanline == (int)ScanlineState.Pre)
                {
                    if (IsRendering)
                    {
                        if (Cycle >= (int)CycleState.Start && Cycle <= (int)CycleState.End || Cycle >= (int)CycleState.Pre1 && Cycle <= (int)CycleState.Pre2)
                            GetTiles();

                        //copy horizontal bits
                        if (Cycle == 257)
                        {
                            LoadRegisters();
                            EvalSprites();
                            _vramAddr = _vramAddr & ~0x41f | _tempAddr & 0x41f;
                        }

                        if (Cycle > 280 && Cycle <= 304)
                            _vramAddr = _vramAddr & ~0x7be0 | _tempAddr & 0x7be0;
                    }

                    if (Cycle == 339)
                        Cycle += IsRendering && (FrameCounter & 1) == 1 && Scanline == -1 ? 1 : 0;
                }

                if (Cycle++ > 339)
                {
                    Cycle = 0;
                    Scanline++;
                    if (Scanline >= ScanlineFrameEnd)
                    {
                        Scanline = -1;
                        _vBlank = false;
                        _sprite0hit = false;
                        _sprOverflow = false;
                        NoNmi = false;
                    }
                }
            }
            Totalcycles++;
            Cycles++;
        }

        public int ReadRegister(int a)
        {
            switch (a)
            {
                case 0x2002:
                {
                    int v = (((_vBlank ? 1 : 0) << 7 | (_sprite0hit ? 1 : 0) << 6 | (_sprOverflow ? 1 : 0) << 5) & 0xe0
                        | _dummy2007 & 0x1f);

                    if (Scanline == 241)
                    {
                        if (Cycle == 0)
                        {
                            NoNmi = true;
                        }
                        else if (Cycle == 1 || Cycle == 2)
                        {
                            return v &= 0x7f;
                        }
                        else if (Cycle == 3)
                        {
                            NoNmi = false;
                            _vBlank = false;
                            return (v & 0x7f);
                        }
                    }
                    _vBlank = false;
                    _writeToggle = false;
                    return v;
                }
                case 0x2007:
                {
                    int v;
                    if (_vramAddr <= 0x3eff)
                    {
                        v = _dummy2007;
                        _dummy2007 = Read(_vramAddr);
                    }
                    else
                        v = (_dummy2007 = Read(_vramAddr));

                    _vramAddr += _vaddrIncrease != 0 ? 32 : 1;
                    return v & 0xff;
                }
                default: return 0;
            }
        }

        public void WriteRegister(int a, int v)
        {
            switch (a)
            {
                case 0x2000:
                {
                    _tempAddr = _tempAddr & ~0xc00 | (v & 3) << 10;
                    _nametable = v >> 1 & 3;
                    _vaddrIncrease = v >> 2 & 1;
                    _sprTable = (v >> 3 & 1) != 0;
                    _bgTable = v >> 4 & 1;
                    _spriteSize = (v >> 5 & 1) != 0;
                    _nmi = v >> 7 != 0;

                    if (Header.MapperId == 5)
                        Mmu.Mapper.SpriteSize = _spriteSize;
                    break;
                }
                case 0x2001:
                {
                    _backgroundLeft = (v & 0x02) != 0;
                    _spriteLeft = (v & 0x04) != 0;
                    _background = (v & 0x08) != 0;
                    _sprite = (v & 0x10) != 0;
                    break;
                }
                case 0x2003: _oamAddr = v; break;
                case 0x2004:
                {
                    _oamData = v & 0xff;
                    _oamAddr++;
                    break;
                }
                case 0x2005:
                {
                    if (!_writeToggle)
                    {
                        _tempAddr &= 0x7fe0;
                        _tempAddr |= (v & 0xf8) >> 3;
                        _fineX = (byte)(v & 0x07);
                    }
                    else
                        _tempAddr = _tempAddr & 0xc1f | (v & 0xf8) << 2 | (v & 7) << 12;
                    _writeToggle ^= true;
                    break;
                }
                case 0x2006:
                {
                    if (!_writeToggle)
                    {
                        _tempAddr = (ushort)(_tempAddr & 0x80ff | (v & 0x3f) << 8);
                    }
                    else
                    {
                        _tempAddr = _tempAddr & 0xff00 | v;
                        _vramAddr = _tempAddr;
                    }
                    _writeToggle ^= true;
                    break;
                }
                case 0x2007:
                {
                    Write(_vramAddr, v);
                    _vramAddr += _vaddrIncrease != 0 ? 32 : 1;

                    if (((_vramAddr ^ _a12) & 0x1000) != 0)
                    {
                        if ((_vramAddr & 0x1000) != 0)
                            Mmu.Mapper.Scanline();
                        _a12 = _vramAddr;
                    }
                    break;
                }
            }
        }

        public void Write4014(int a, int v) //4014
        {
            for (int i = 0; i < 256; i++)
            {
                int oamaddr = v << 8; _oamDma = v;
                Mmu.Oram[i] = Mmu.Wram[oamaddr + i];
                Step(3); Step(3);
                Totalcycles++;
            }

            if ((Totalcycles & 1) == 1)
            {
                Step(3); Step(3);
            }
            else
                Step(3);
            Totalcycles++;
        }

        private void RenderPixels()
        {
            //if (Nes.FastForward && FrameCounter % Nes.Config.FrameSkip == 0) return;

            int x = Cycle - 1;
            int y = Scanline;
            int bg_pixel = 0;
            int bg_pal = 0;
            int spr_pixel = 0;
            int spr_pal = 0;
            int attrib = 0;

            // Cache Lp.Fx for repeated use
            int fx_shift = 15 - _fineX;
            int at_shift = 7 - _fineX;

            if (_background && (x >= 8 || _backgroundLeft))
            {
                bg_pixel = ((_bgShiftLo >> fx_shift) & 1) | (((_bgShiftHi >> fx_shift) & 1) << 1);
                bg_pal = ((_atShiftLo >> at_shift) & 1) | (((_atShiftHi >> at_shift) & 1) << 1);
                bg_pal &= 3;
            }

            if (_sprite && !(x < 8 && !_spriteLeft))
            {
                int bgaddr = _sprTable ? 0x1000 : 0x0000;
                // Use for loop for better performance than != 0
                int spriteCount = SpriteScan.Count;
                for (int i = 0; i < spriteCount; i++)
                {
                    var spr = SpriteScan[i];
                    int tile = spr.Tile;
                    attrib = spr.Attrib;
                    int fx = x - spr.X;
                    int fy = (y - (spr.Y + 1)) & (_spriteSize ? 15 : 7);

                    // Fast path: skip invisible sprites
                    if (spr.X == 255 || spr.Y > 238 || fx < 0 || fx > 7)
                        continue;

                    if ((attrib & 0x40) == 0) fx = 7 - fx;
                    if ((attrib & 0x80) != 0) fy = (_spriteSize ? 15 : 7) - fy;

                    int spraddr;
                    if (_spriteSize)
                        spraddr = ((tile & 1) * 0x1000) + ((tile & 0xfe) * 16) + fy + (fy & 8);
                    else
                        spraddr = bgaddr + tile * 16 + fy;

                    // Cache Read(spraddr) and Read(spraddr+8)
                    int spr_lo = Read(spraddr);
                    int spr_hi = Read(spraddr + 8);
                    spr_pixel = (spr_lo >> fx & 1) | ((spr_hi >> fx & 1) << 1);

                    if (spr_pixel == 0) continue;

                    if (!_sprite0hit && spr.Id == 0 && _background && bg_pixel != 0 && x != 255)
                        _sprite0hit = true;

                    spr_pal = attrib & 3;
                    break;
                }
            }

            int offset = 0;
            if (bg_pixel != 0 && spr_pixel != 0)
            {
                offset = ((attrib & 0x20) == 0)
                    ? spr_pixel + spr_pal * 4 + 0x10
                    : bg_pixel + bg_pal * 4;
            }
            else if (bg_pixel != 0)
            {
                offset = bg_pixel + bg_pal * 4;
            }
            else if (spr_pixel != 0)
            {
                offset = spr_pixel + spr_pal * 4 + 0x10;
            }

            // Avoid modulo if offset is always in range (0..63)
            int paletteIndex = Read(0x3f00 + offset);
            if ((uint)paletteIndex >= (uint)pixPalettes.Length)
                paletteIndex %= pixPalettes.Length;

            ScreenBuffer[y * 256 + x] = pixPalettes[paletteIndex];

            UpdateRegisters();
        }

        private void EvalSprites()
        {
            SpriteScan.Clear();
            int c = 0;

            for (int i = 0; i < 64; i++)
            {
                if (c >= 8)
                    break;

                int yp = Scanline - Mmu.Oram[i * 4 + 0];
                int size = _spriteSize ? 16 : 8;
                if (yp > -1 && yp < size)
                {
                    SpriteScan.Add(new
                    (
                        Mmu.Oram[i * 4 + 0],
                        Mmu.Oram[i * 4 + 1],
                        Mmu.Oram[i * 4 + 2],
                        Mmu.Oram[i * 4 + 3],
                        i)
                    );
                    c++;
                }
            }
        }

        private void GetTiles()
        {
            switch (Cycle & 7)
            {
                case 1:
                    _ntAddr = 0x2000 | _vramAddr & 0xfff;
                    LoadRegisters();
                    break;
                case 2: _ntByte = Read(_ntAddr); break;
                case 3: _atAddr = 0x23c0 | _vramAddr & 0xc00 | _vramAddr >> 4 & 0x38 | _vramAddr >> 2 & 0x07; break;
                case 4:
                    _atByte = Read(_atAddr);
                    if ((_vramAddr >> 5 & 2) != 0)
                        _atByte >>= 4;
                    if ((_vramAddr & 2) != 0)
                        _atByte >>= 2;
                    break;
                case 5: _bgAddr = _bgTable * 0x1000 + _ntByte * 16 + FineY; break;
                case 6: _bgLo = Read(_bgAddr); break;
                case 7: _bgAddr += 8; break;
                case 0:
                    _bgHi = Read(_bgAddr);
                    XIncrease();
                    break;
            }
        }

        private void XIncrease()
        {
            if ((_vramAddr & 0x1f) == 0x1f)
                _vramAddr = _vramAddr & ~0x1f ^ 0x400;
            else
                _vramAddr++;
        }

        private void YIncrease()
        {
            if ((_vramAddr & 0x7000) != 0x7000)
                _vramAddr += 0x1000;
            else
            {
                _vramAddr &= ~0x7000;
                int y = (_vramAddr & 0x3e0) >> 5;

                if (y == 29)
                {
                    y = 0;
                    _vramAddr ^= 0x800;
                }
                else if (y == 31)
                    y = 0;
                else
                    y++;

                _vramAddr = _vramAddr & ~0x3e0 | y << 5;
            }
        }

        private void LoadRegisters()
        {
            _bgShiftLo = _bgShiftLo & 0xff00 | _bgLo;
            _bgShiftHi = _bgShiftHi & 0xff00 | _bgHi;
            _atLo = _atByte & 1;
            _atHi = (_atByte & 2) != 0 ? 1 : 0;
        }

        private void UpdateRegisters()
        {
            _bgShiftLo <<= 1;
            _bgShiftHi <<= 1;
            _atShiftLo = (_atShiftLo << 1) | _atLo;
            _atShiftHi = (_atShiftHi << 1) | _atHi;
        }

        public int Read(int a)
        {
            return a switch
            {
                < 0x2000 => ReadPattern(a),
                < 0x3f00 => ReadNametable(a),
                _ => ReadPalette(a)
            };
        }

        public void Write(int a, int v)
        {
            switch (a)
            {
                case < 0x2000: WritePattern(a, v); break;
                case < 0x3f00: WriteNametable(a, v); break;
                default: WritePalette(a, v); break;
            }
        }

        private int ReadNametable(int addr)
        {
            int a = addr % 0x400;
            if (Header.Mirror == SingleNt0)
                return Vram[0x2000 + a + MirrorNt0[(a >> 10) & 3] * 0x400];
            else if (Header.Mirror == SingleNt1)
                return Vram[0x2000 + a + MirrorNt1[(a >> 10) & 3] * 0x400];
            else if (Header.Mirror == Horizontal)
            {
                switch ((addr >> 10) & 3)
                {
                    case 0: return Vram[0x2000 + a];
                    case 1: return Vram[0x2000 + a];
                    case 2: return Vram[0x2400 + a];
                    case 3: return Vram[0x2400 + a];
                }
            }
            else if (Header.Mirror == Vertical)
            {
                switch ((addr >> 10) & 3)
                {
                    case 0: return Vram[0x2000 + a];
                    case 1: return Vram[0x2400 + a];
                    case 2: return Vram[0x2000 + a];
                    case 3: return Vram[0x2400 + a];
                }
            }
            return 0;
        }

        public int ReadPattern(int a)
        {
            if (Header.MapperId == 9)
                Mmu.Mapper.SetLatch(a, 0);
            if (Mmu.Mapper.Header.ChrBanks != 0)
                return Mmu.Mapper.ReadChr(a);
            else
                return Vram[a];
        }

        public int ReadPalette(int a)
        {
            return Vram[a & 0x3fff];
        }

        public void WriteNametable(int addr, int v)
        {
            var a = addr % 0x400;
            if (Header.Mirror == SingleNt0)
            {
                var b = MirrorNt0[(addr >> 10) & 3];
                Vram[0x2000 + a + b * 0x400] = (byte)v;
            }
            else if (Header.Mirror == SingleNt1)
            {
                var b = MirrorNt1[((a >> 10) & 3)];
                Vram[0x2000 + a + b * 0x400] = (byte)v;
            }
            else if (Header.Mirror == Horizontal)
            {
                switch ((addr >> 10) & 3)
                {
                    case 0: Vram[0x2000 + a] = (byte)v; break;
                    case 1: Vram[0x2000 + a] = (byte)v; break;
                    case 2: Vram[0x2400 + a] = (byte)v; break;
                    case 3: Vram[0x2400 + a] = (byte)v; break;
                }
            }
            else if (Header.Mirror == Vertical)
            {
                switch ((addr >> 10) & 3)
                {
                    case 0: Vram[0x2000 + a] = (byte)v; break;
                    case 1: Vram[0x2400 + a] = (byte)v; break;
                    case 2: Vram[0x2000 + a] = (byte)v; break;
                    case 3: Vram[0x2400 + a] = (byte)v; break;
                }
            }
        }

        private void WritePattern(int a, int v)
        {
            Vram[a & 0x3fff] = (byte)v;
        }

        public void WritePalette(int a, int v)
        {
            Vram[a & 0x3fff] = (byte)v;

            for (int i = 0; i < 7; i++)
                Array.Copy(Vram, 0x3f00, Vram, 0x3f20 + i * 32, 0x20);

            for (int i = 0; i < 4; i++)
                Array.Copy(Vram, 0x3f10, Vram, 0x3f04 + i * 0x4, 0x01);

            if (a == 0x3f10)
                Vram[0x3f00] = (byte)v;
            else if (a == 0x3f00)
                Vram[0x3f10] = (byte)v;
        }

        public void RenderNametable(ref uint[] buffer)
        {
            for (int y = 0; y < 480; y++)
            {
                int a = 0;
                for (int x = 0; x < 512; x++)
                {
                    if (Header.Mirror == Vertical && x >= 256)
                        a = 0x400;

                    else if (Header.Mirror == Horizontal && y >= 240)
                        a = 0x800;

                    int ppuaddr = 0x2000 + a + ((x % 256) / 8) + ((y % 240) / 8) * 32;
                    int attaddr = 0x23c0 | (ppuaddr & 0xc00) | ((ppuaddr >> 4) & 0x38) | ((ppuaddr >> 2) & 0x07);

                    int fy = y & 7;
                    int fx = x & 7;
                    int bgaddr = _bgTable + Read(ppuaddr) * 16 + fy;
                    byte attr_shift = (byte)((ppuaddr >> 4) & 4 | (ppuaddr & 2));
                    byte bit2 = (byte)((Read(attaddr) >> attr_shift) & 3);

                    byte color = (byte)(Read(bgaddr) >> (7 - fx) & 1 |
                        (Read(bgaddr + 8) >> (7 - fx) & 1) * 2);
                    buffer[y * 512 + x] = pixPalettes[Read(0x3f00 | bit2 * 4 + color)];
                }
            }
        }

        public void Save(BinaryWriter bw)
        {
            WriteArray(bw, Vram); WriteArray(bw, Oram); WriteArray(bw, Pram); bw.Write(_dummy2007);
            bw.Write(_oamDma); bw.Write(_ntAddr); bw.Write(_atAddr); bw.Write(_bgAddr);
            bw.Write(_ntByte); bw.Write(_atByte); bw.Write(_bgLo); bw.Write(_bgHi);
            bw.Write(_atLo); bw.Write(_atHi); bw.Write(_bgShiftLo); bw.Write(_bgShiftHi);
            bw.Write(_atShiftLo); bw.Write(_atShiftHi); bw.Write(Scanline); bw.Write(Cycle);
            bw.Write(Cycles); bw.Write(FrameCounter); bw.Write(Totalcycles); bw.Write(NoNmi);
            bw.Write(_vramAddr); bw.Write(_tempAddr); bw.Write(_fineX); bw.Write(_writeToggle);
            bw.Write(_a12); bw.Write(_oamData); bw.Write(_oamAddr); bw.Write(_nametable);
            bw.Write(_vaddrIncrease); bw.Write(_sprTable); bw.Write(_bgTable); bw.Write(_spriteSize);
            bw.Write(_masterSelect); bw.Write(_nmi); bw.Write(_greyscale); bw.Write(_backgroundLeft);
            bw.Write(_spriteLeft); bw.Write(_background); bw.Write(_sprite); bw.Write(_red);
            bw.Write(_green); bw.Write(_blue); bw.Write(_lsb); bw.Write(_sprOverflow);
            bw.Write(_sprite0hit); bw.Write(_vBlank);
        }

        public void Load(BinaryReader br)
        {
            Vram = ReadArray<byte>(br, Vram.Length); Oram = ReadArray<byte>(br, Oram.Length); Pram = ReadArray<byte>(br, Pram.Length); _dummy2007 = br.ReadInt32();
            _oamDma = br.ReadInt32(); _ntAddr = br.ReadInt32(); _atAddr = br.ReadInt32(); _bgAddr = br.ReadInt32();
            _ntByte = br.ReadInt32(); _atByte = br.ReadInt32(); _bgLo = br.ReadInt32(); _bgHi = br.ReadInt32();
            _atLo = br.ReadInt32(); _atHi = br.ReadInt32(); _bgShiftLo = br.ReadInt32(); _bgShiftHi = br.ReadInt32();
            _atShiftLo = br.ReadInt32(); _atShiftHi = br.ReadInt32(); Scanline = br.ReadInt32(); Cycle = br.ReadInt32();
            Cycles = br.ReadInt32(); FrameCounter = br.ReadUInt32(); Totalcycles = br.ReadUInt64(); NoNmi = br.ReadBoolean();
            _vramAddr = br.ReadInt32(); _tempAddr = br.ReadInt32(); _fineX = br.ReadInt32(); _writeToggle = br.ReadBoolean();
            _a12 = br.ReadInt32(); _oamData = br.ReadInt32(); _oamAddr = br.ReadInt32(); _nametable = br.ReadInt32();
            _vaddrIncrease = br.ReadInt32(); _sprTable = br.ReadBoolean(); _bgTable = br.ReadInt32(); _spriteSize = br.ReadBoolean();
            _masterSelect = br.ReadInt32(); _nmi = br.ReadBoolean(); _greyscale = br.ReadInt32(); _backgroundLeft = br.ReadBoolean();
            _spriteLeft = br.ReadBoolean(); _background = br.ReadBoolean(); _sprite = br.ReadBoolean(); _red = br.ReadInt32();
            _green = br.ReadInt32(); _blue = br.ReadInt32(); _lsb = br.ReadInt32(); _sprOverflow = br.ReadBoolean();
            _sprite0hit = br.ReadBoolean(); _vBlank = br.ReadBoolean();
        }

        public List<RegisterInfo> GetState() =>
        [
            new("","Cycle",$"{Cycle}"),
            new("","Scanline",$"{Scanline}"),
            new("","V",$"{_vramAddr:X4}"),
            new("","T",$"{_tempAddr:X4}"),
            new("","X",$"{_fineX:X2}"),
            new("2000","Control",""),
            new("2","Vram Increase",$"{_vaddrIncrease}"),
            new("3","Sprite Address",$"{_sprTable}"),
            new("4","BG Address",$"{_bgTable}"),
            new("5","Sprite Size",$"{_spriteSize}"),
            new("6","Master Ppu",$"{_masterSelect}"),
            new("7","Nmi",$"{_nmi}"),

            new("2001","Mask",""),
            new("0","Greyscale",$"{_greyscale}"),
            new("1","Background Left 8px",$"{_backgroundLeft}"),
            new("2","Sprites Left 8px",$"{_spriteLeft}"),
            new("3","Show Background",$"{_background}"),
            new("4","Show Sprites",$"{_sprite}"),
            new("5","Red",$"{_red}"),
            new("6","Green",$"{_green}"),
            new("7","Blue",$"{_blue}"),

            new("2002","Status",""),
            new("5","Sprite Overflow",$"{_sprOverflow}"),
            new("6","Sprite 0 Hit",$"{_sprite0hit}"),
            new("7","VBlank",$"{_vBlank}"),
        ];

        public struct SpriteData(int y, int tile, int attrib, int x, int id)
        {
            public int Y = y;
            public int Tile = tile;
            public int Attrib = attrib;
            public int X = x;
            public int Id = id;
        }
    }
}
