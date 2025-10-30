namespace Gmulator.Core.Nes
{
    public class NesPpu() : EmuState
    {
        private readonly int[] MirrorHor = [0, 0, 1, 1];
        private readonly int[] MirrorVer = [0, 1, 0, 1];
        private readonly int[] MirrorNt0 = [0, 0, 0, 0];
        private readonly int[] MirrorNt1 = [1, 1, 1, 1];
        private readonly int[] MirrorFsc = [0, 1, 2, 3];

        private int Dummy2007;
        private int OamDma;
        private int NtAddr, AtAddr, BgAddr;
        private int NtNyte, AtByte;
        private int BgLo, BgHi, AtLo, AtHi;
        private int BgShiftLo, BgShiftHi, AtShiftLo, AtShiftHi;
        public int Scanline { get => scanline; private set => scanline = value; }
        public int Cycle { get => cycle; private set => cycle = value; }
        public int Cycles { get => cycles; set => cycles = value; }
        public uint FrameCounter { get => frameCounter; private set => frameCounter = value; }
        public ulong Totalcycles { get => totalcycles; private set => totalcycles = value; }
        public bool NoNmi { get; private set; }

        private List<SpriteData> SpriteScan;
        private int A12;
        private byte OamData;
        private int OamAddr;

        private int Nametable;
        private int VaddrIncrease;
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

        private int Lsb;
        private bool _sprOverflow;
        private bool _sprite0hit;
        private bool _vBlank;

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
        public int FineY => ((Lp.V & 0x7000) >> 12) & 0xff;

        private NesMmu Mmu;
        private NesApu Apu;
        public uint[] NametableBuffer { get; private set; } = new uint[NesWidth * NesWidth * 4];
        public uint[] ScreenBuffer { get; private set; }

        private Nes Nes;
        private Action<int, int> WriteByte;

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
            WriteByte = nes.Mmu.WriteWram;
            ScreenBuffer = new uint[NesWidth * NesHeight * 4];
        }

        public void Reset()
        {
            Scanline = 0;
            Dummy2007 = 0;
            Lp = default;

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
                        NtNyte = Read(0x2000 | Lp.V & 0xfff);

                    //increment y scroll
                    if (Cycle == 257)
                    {
                        EvalSprites();
                        LoadRegisters();
                        YIncrease();
                    }

                    //copy horizontal bits
                    if (Cycle == 256)
                        Lp.V = Lp.V & ~0x41f | Lp.T & 0x41f;
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
                            NesCpu.NmiTriggered = 1;
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
                            Lp.V = Lp.V & ~0x41f | Lp.T & 0x41f;
                        }

                        if (Cycle > 280 && Cycle <= 304)
                            Lp.V = Lp.V & ~0x7be0 | Lp.T & 0x7be0;
                    }

                    if (Cycle == 339)
                        Cycle += IsRendering && (FrameCounter & 1) == 1 && Scanline == -1 ? 1 : 0;
                }

                if (cycle++ > 339)
                {
                    cycle = 0;
                    scanline++;
                    if (scanline >= ScanlineFrameEnd)
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

        public void ControlW(int v) //2000
        {
            Lp.T = Lp.T & ~0xc00 | (v & 3) << 10;
            Nametable = v >> 1 & 3;
            VaddrIncrease = v >> 2 & 1;
            _sprTable = (v >> 3 & 1) != 0;
            _bgTable = v >> 4 & 1;
            _spriteSize = (v >> 5 & 1) != 0;
            _nmi = v >> 7 != 0;

            if (Header.MapperId == 5)
                Mmu.Mapper.SpriteSize = _spriteSize;

            WriteByte(0x2000 & 0x2007, v);
        }

        public void MaskW(int v)
        {
            _backgroundLeft = (v & 0x02) != 0;
            _spriteLeft = (v & 0x04) != 0;
            _background = (v & 0x08) != 0;
            _sprite = (v & 0x10) != 0;
        }

        public int StatusR()
        {
            int v = (((_vBlank ? 1 : 0) << 7 | (_sprite0hit ? 1 : 0) << 6 | (_sprOverflow ? 1 : 0) << 5) & 0xe0
                | Dummy2007 & 0x1f);

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
                    //p2002 &= 0x7f;
                    return (v & 0x7f);
                }
            }
            _vBlank = false;

            Lp.W = false;
            return v;
        }

        public void OamAddressW(int v) => OamAddr = v;

        public void OamDataW(int v)
        {
            OamData = (byte)v;
            OamAddr++;
        }

        public void ScrollW(int v) //2005
        {
            if (!Lp.W)
            {
                Lp.T &= 0x7fe0;
                Lp.T |= (v & 0xf8) >> 3;
                Lp.Fx = (byte)(v & 0x07);
            }
            else
                Lp.T = Lp.T & 0xc1f | (v & 0xf8) << 2 | (v & 7) << 12;
            Lp.W ^= true;
        }

        public void AddressDataW(int v) //2006
        {
            if (!Lp.W)
            {
                Lp.T = (ushort)(Lp.T & 0x80ff | (v & 0x3f) << 8);
                //cycle -= 9;
            }
            else
            {
                Lp.T = Lp.T & 0xff00 | v;
                Lp.V = Lp.T;
            }
            Lp.W ^= true;
        }

        public void DataW(int v) //2007
        {
            Write(Lp.V, v);
            Lp.V += VaddrIncrease != 0 ? 32 : 1;

            if (((Lp.V ^ A12) & 0x1000) != 0)
            {
                if ((Lp.V & 0x1000) != 0)
                    Mmu.Mapper.Scanline();
                A12 = Lp.V;
            }
        }

        public int DataR()
        {
            int v;
            if (Lp.V <= 0x3eff)
            {
                v = Dummy2007;
                Dummy2007 = Read(Lp.V);
            }
            else
                v = (Dummy2007 = Read(Lp.V));


            Lp.V += VaddrIncrease != 0 ? 32 : 1;
            return v & 0xff;
        }

        public void OamDamyCopy(int v) //4014
        {
            for (int i = 0; i < 256; i++)
            {
                int oamaddr = v << 8; OamDma = v;
                Mmu.Oram[i] = Mmu.Ram[oamaddr + i];
                Step(3); Step(3);
                Totalcycles++;
            }

            if ((Totalcycles & 1) == 1)
            {
                Step(3); Step(3);
                Totalcycles++;
            }
            else
            {
                Step(3);
                Totalcycles++;
            }
        }

        private void RenderPixels()
        {
            if (Nes.FastForward && FrameCounter % Nes.Config.FrameSkip == 0) return;

            int x = cycle - 1;
            int y = scanline;
            int bg_pixel = 0;
            int bg_pal = 0;
            int spr_pixel = 0;
            int spr_pal = 0;
            int attrib = 0;

            // Cache Lp.Fx for repeated use
            int fx_shift = 15 - Lp.Fx;
            int at_shift = 7 - Lp.Fx;

            if (_background && (x >= 8 || _backgroundLeft))
            {
                bg_pixel = ((BgShiftLo >> fx_shift) & 1) | (((BgShiftHi >> fx_shift) & 1) << 1);
                bg_pal = ((AtShiftLo >> at_shift) & 1) | (((AtShiftHi >> at_shift) & 1) << 1);
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
                    byte spr_lo = Read(spraddr);
                    byte spr_hi = Read(spraddr + 8);
                    spr_pixel = (spr_lo >> fx & 1) | ((spr_hi >> fx & 1) << 1);

                    if (spr_pixel == 0) continue;

                    if (spr.Id == 0 && _sprite && _background && bg_pixel != 0 && x != 255 && !_sprite0hit)
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
            switch (cycle & 7)
            {
                case 1:
                    NtAddr = 0x2000 | Lp.V & 0xfff;
                    LoadRegisters();
                    break;
                case 2: NtNyte = Read(NtAddr); break;
                case 3: AtAddr = 0x23c0 | Lp.V & 0xc00 | Lp.V >> 4 & 0x38 | Lp.V >> 2 & 0x07; break;
                case 4:
                    AtByte = Read(AtAddr);
                    if ((Lp.V >> 5 & 2) != 0)
                        AtByte >>= 4;
                    if ((Lp.V & 2) != 0)
                        AtByte >>= 2;
                    break;
                case 5: BgAddr = _bgTable * 0x1000 + NtNyte * 16 + FineY; break;
                case 6: BgLo = Read(BgAddr); break;
                case 7: BgAddr += 8; break;
                case 0:
                    BgHi = Read(BgAddr);
                    XIncrease();
                    break;
            }
        }

        private void XIncrease()
        {
            if ((Lp.V & 0x1f) == 0x1f)
                Lp.V = Lp.V & ~0x1f ^ 0x400;
            else
                Lp.V++;
        }

        private void YIncrease()
        {
            if ((Lp.V & 0x7000) != 0x7000)
                Lp.V += 0x1000;
            else
            {
                Lp.V &= ~0x7000;
                int y = (Lp.V & 0x3e0) >> 5;

                if (y == 29)
                {
                    y = 0;
                    Lp.V ^= 0x800;
                }
                else if (y == 31)
                    y = 0;
                else
                    y++;

                Lp.V = Lp.V & ~0x3e0 | y << 5;
            }
        }

        private void LoadRegisters()
        {
            BgShiftLo = BgShiftLo & 0xff00 | BgLo;
            BgShiftHi = BgShiftHi & 0xff00 | BgHi;
            AtLo = AtByte & 1;
            AtHi = (AtByte & 2) != 0 ? 1 : 0;
        }

        private void UpdateRegisters()
        {
            BgShiftLo <<= 1;
            BgShiftHi <<= 1;
            AtShiftLo = (AtShiftLo << 1) | AtLo;
            AtShiftHi = (AtShiftHi << 1) | AtHi;
        }

        public byte Read(int addr)
        {
            addr &= 0x3fff;
            byte v = 0;
            var Vram = Mmu.Vram;

            if (addr < 0x2000)
            {
                if (Header.MapperId == 15)
                    Mmu.Mapper.SetLatch(addr, v);
                if (Mmu.Mapper.Header.ChrBanks != 0)
                    return Mmu.Mapper.ReadChr(addr);
                else
                    return Vram[addr];
            }
            else if (addr >= 0x3f00)
                return Vram[addr];

            var a = addr % 0x400;
            if (Header.Mirror == SingleNt0)
                v = Vram[0x2000 + (a % 0x400) + MirrorNt0[(a >> 10) & 3] * 0x400];
            else if (Header.Mirror == SingleNt1)
                v = Vram[0x2000 + (a % 0x400) + MirrorNt1[(a >> 10) & 3] * 0x400];
            else if (Header.Mirror == Horizontal)
            {
                switch ((addr >> 10) & 3)
                {
                    case 0: v = Vram[0x2000 + a]; break;
                    case 1: v = Vram[0x2000 + a]; break;
                    case 2: v = Vram[0x2400 + a]; break;
                    case 3: v = Vram[0x2400 + a]; break;
                }
            }
            else if (Header.Mirror == Vertical)
            {
                switch ((addr >> 10) & 3)
                {
                    case 0: v = Vram[0x2000 + a]; break;
                    case 1: v = Vram[0x2400 + a]; break;
                    case 2: v = Vram[0x2000 + a]; break;
                    case 3: v = Vram[0x2400 + a]; break;
                }
            }

            //Mmu.CheckForBeakpoint(addr, v, BPType.VRead);

            return v;
        }

        public void Write(int addr, int v)
        {
            addr &= 0x3fff;
            var vram = Mmu.Vram;

            //Mmu.CheckForBeakpoint(addr, v, BPType.VWrite);

            if (addr < 0x2000 || addr >= 0x3f00)
                vram[addr] = (byte)v;
            else
            {
                var a = addr % 0x400;
                if (Header.Mirror == SingleNt0)
                {
                    var b = MirrorNt0[(addr >> 10) & 3];
                    vram[0x2000 + a + b * 0x400] = (byte)v;
                }
                else if (Header.Mirror == SingleNt1)
                {
                    var b = MirrorNt1[((a >> 10) & 3)];
                    vram[0x2000 + a + b * 0x400] = (byte)v;
                }
                else if (Header.Mirror == Horizontal)
                {
                    switch ((addr >> 10) & 3)
                    {
                        case 0: vram[0x2000 + a] = (byte)v; break;
                        case 1: vram[0x2000 + a] = (byte)v; break;
                        case 2: vram[0x2400 + a] = (byte)v; break;
                        case 3: vram[0x2400 + a] = (byte)v; break;
                    }
                }
                else if (Header.Mirror == Vertical)
                {
                    switch ((addr >> 10) & 3)
                    {
                        case 0: vram[0x2000 + a] = (byte)v; break;
                        case 1: vram[0x2400 + a] = (byte)v; break;
                        case 2: vram[0x2000 + a] = (byte)v; break;
                        case 3: vram[0x2400 + a] = (byte)v; break;
                    }
                }
            }

            for (int i = 0; i < 7; i++)
                Array.Copy(vram, 0x3f00, vram, 0x3f20 + i * 32, 0x20);

            for (int i = 0; i < 4; i++)
                Array.Copy(vram, 0x3f10, vram, 0x3f04 + i * 0x4, 0x01);

            if (addr == 0x3f10)
                vram[0x3f00] = (byte)v;
            else if (addr == 0x3f00)
                vram[0x3f10] = (byte)v;
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

        public override void Save(BinaryWriter bw)
        {
            bw.Write(Nametable);
            bw.Write(VaddrIncrease);
            bw.Write(_sprTable);
            bw.Write(_bgTable);
            bw.Write(_spriteSize);
            bw.Write(_masterSelect);
            bw.Write(_nmi);

            bw.Write(_greyscale);
            bw.Write(_backgroundLeft);
            bw.Write(_spriteLeft);
            bw.Write(_background);
            bw.Write(_sprite);
            bw.Write(_red);
            bw.Write(_green);
            bw.Write(_blue);

            bw.Write(Lsb);
            bw.Write(_sprOverflow);
            bw.Write(_sprite0hit);
            bw.Write(_vBlank);
            bw.Write(Dummy2007);
            bw.Write(OamDma);
            bw.Write(NtAddr);
            bw.Write(AtAddr);
            bw.Write(BgAddr);
            bw.Write(NtNyte);
            bw.Write(AtByte);
            bw.Write(BgLo);
            bw.Write(BgHi);
            bw.Write(BgShiftLo);
            bw.Write(BgShiftHi);
            bw.Write(AtShiftLo);
            bw.Write(AtShiftHi);
            bw.Write(AtLo);
            bw.Write(AtHi);
            bw.Write(Scanline);
            bw.Write(Cycle);
            bw.Write(FrameCounter);
            bw.Write(Totalcycles);
        }

        public override void Load(BinaryReader br)
        {
            Nametable = br.ReadInt32();
            VaddrIncrease = br.ReadInt32();
            _sprTable = br.ReadBoolean();
            _bgTable = br.ReadInt32();
            _spriteSize = br.ReadBoolean();
            _masterSelect = br.ReadInt32();
            _nmi = br.ReadBoolean();

            _greyscale = br.ReadInt32();
            _backgroundLeft = br.ReadBoolean();
            _spriteLeft = br.ReadBoolean();
            _background = br.ReadBoolean();
            _sprite = br.ReadBoolean();
            _red = br.ReadInt32();
            _green = br.ReadInt32();
            _blue = br.ReadInt32();

            Lsb = br.ReadInt32();
            _sprOverflow = br.ReadBoolean();
            _sprite0hit = br.ReadBoolean();
            _vBlank = br.ReadBoolean();

            Dummy2007 = br.ReadInt32();
            OamDma = br.ReadInt32();
            NtAddr = br.ReadInt32();
            AtAddr = br.ReadInt32();
            BgAddr = br.ReadInt32();
            NtNyte = br.ReadInt32();
            AtByte = br.ReadInt32();
            BgLo = br.ReadInt32();
            BgHi = br.ReadInt32();
            BgShiftLo = br.ReadInt32();
            BgShiftHi = br.ReadInt32();
            AtShiftLo = br.ReadInt32();
            AtShiftHi = br.ReadInt32();
            AtLo = br.ReadInt32();
            AtHi = br.ReadInt32();
            Scanline = br.ReadInt32();
            Cycle = br.ReadInt32();
            FrameCounter = br.ReadUInt32();
            Totalcycles = br.ReadUInt64();
        }

        public List<RegisterInfo> GetState() =>
        [
            new("","Cycle",$"{Cycle}"),
            new("","Scanline",$"{Scanline}"),
            new("","V",$"{Lp.V:X4}"),
            new("","T",$"{Lp.T:X4}"),
            new("","X",$"{Lp.Fx:X2}"),
            new("2000","Control",""),
            new("2","Vram Increase",$"{VaddrIncrease}"),
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

        public struct PpuRegisters
        {
            public int V;
            public int T;
            public int Fx;
            public bool W;
        };
        public PpuRegisters Lp;
        private int cycle;
        private int scanline;
        private int cycles;
        private uint frameCounter;
        private ulong totalcycles;

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
