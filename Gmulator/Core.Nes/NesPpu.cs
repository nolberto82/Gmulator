using Gmulator;
using System;
using Raylib_cs;
using Gmulator.Core.Nes;
using Gmulator.Core.Snes;

namespace Gmulator.Core.Nes
{
    public class NesPpu(NesMmu mmu) : EmuState
    {
        public int[] MirrorHor = [0, 0, 1, 1];
        public int[] MirrorVer = [0, 1, 0, 1];
        public int[] MirrorNt0 = [0, 0, 0, 0];
        public int[] MirrorNt1 = [1, 1, 1, 1];
        public int[] MirrorFsc = [0, 1, 2, 3];

        private int Dummy2007;
        public int OamDma;
        private int NtAddr;
        private int AtAddr;
        private int BgAddr;
        private int NtNyte;
        private int AtByte;
        private int BgLo;
        private int BgHi;
        private int BgShiftLo;
        private int BgShiftHi;
        private int AtShiftLo;
        private int AtShiftHi;
        private int AtLo;
        private int AtHi;
        public int Scanline;
        public int Cycle { get; private set; }
        public int FrameCycles { get; private set; }
        public int Cycles { get; set; }
        public uint FrameCounter { get; private set; }
        public ulong Totalcycles { get; set; }

        private bool NoNmi;

        private List<SpriteData> SpriteScan;
        private int A12;
        private byte OamData;
        private int OamAddr;

        public int Nametable { get; private set; }
        public int Vaddr { get; private set; }
        public bool Spraddr { get; private set; }
        public int Bgaddr { get; private set; }
        public bool Spritesize { get; private set; }
        public int Master { get; private set; }
        public bool Nmi { get; private set; }

        public int Greyscale { get; private set; }
        public bool Backgroundleft { get; private set; }
        public bool Spriteleft { get; private set; }
        public bool Background { get; private set; }
        public bool Sprite { get; private set; }
        public int Red { get; private set; }
        public int Green { get; private set; }
        public int Blue { get; private set; }

        public int Lsb { get; private set; }
        public int SprOverflow { get; private set; }
        public bool Sprite0hit { get; private set; }
        public int Vblank { get; private set; } = 0;

        private const int CycleStart = 1;
        private const int CycleEnd = 257;
        private const int CyclePre1 = 321;
        private const int CyclePre2 = 336;
        private const int ScanlineEnd = 239;
        private const int ScanlineIdle = 240;
        private const int ScanVblank = 241;
        private const int ScanlinePre = -1;
        private int ScanlineFrameEnd;

        public bool IsRendering => Background || Sprite;
        public int FineY => (Lp.V & 0x7000) >> 12;

        public NesMmu Mmu { get; private set; } = mmu;
        public uint[] NametableBuffer { get; private set; } = new uint[NesWidth * NesWidth * 4];
        private uint[] ScreenBuffer;

        public Nes Nes { get; private set; }

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
            ScreenBuffer = new uint[NesWidth * NesHeight * 4];
        }

        public void Reset()
        {
            Scanline = 0;
            Dummy2007 = 0;
            Lp = default;

            Nmi = false;
            SprOverflow = 0;
            Sprite0hit = false;
            Vblank = 0;

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
            FrameCycles += c;
            Nes.Apu.Step(1);

            //if (Cart.Region == 1)
            //    c = 2;

            while (c-- > 0)
            {
                if ((Scanline < 240 || Scanline == ScanlinePre) && Cycle == 324 && IsRendering)
                    Mmu.Mapper.Scanline();

                if (Scanline >= 0 && Scanline <= ScanlineEnd && IsRendering)
                {
                    if ((Cycle >= CycleStart && Cycle < CycleEnd) || (Cycle >= CyclePre1 && Cycle <= CyclePre2))
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
                else if (Scanline == ScanlineIdle)
                {
                    if (Cycle == 1)
                    {
                        FrameCounter++;
                        Texture.Update(Nes.Screen.Texture, ScreenBuffer);
                    }
                }
                else if (Scanline == ScanVblank)
                {
                    if (Cycle == 0)
                    {
                        Vblank = 1;
                        if (Nmi && !NoNmi)
                            NesCpu.NmiTriggered = 1;
                    }
                }
                else if (Scanline == ScanlinePre)
                {
                    if (IsRendering)
                    {
                        if (Cycle >= CycleStart && Cycle <= CycleEnd || Cycle >= CyclePre1 && Cycle <= CyclePre2)
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
                        Cycle += IsRendering && OddFrame() && Scanline == -1 ? 1 : 0;
                }

                //cycle++;
                if (Cycle++ > 339)
                {
                    Cycle = 0;
                    Scanline++;
                    if (Scanline >= ScanlineFrameEnd)
                    {
                        Scanline = -1;
                        Vblank = 0;
                        FrameCycles = 0;
                        Sprite0hit = false;
                        SprOverflow = 0;
                        NoNmi = false;
                    }
                }
            }
            Totalcycles++;
            Cycles++;
        }

        public void ControlW(byte v) //2000
        {
            Lp.T = Lp.T & ~0xc00 | (v & 3) << 10;
            Nametable = v >> 1 & 3;
            Vaddr = v >> 2 & 1;
            Spraddr = (v >> 3 & 1) > 0;
            Bgaddr = v >> 4 & 1;
            Spritesize = (v >> 5 & 1) > 0;
            Nmi = v >> 7 > 0;

            if (Header.MapperId == 5)
                Mmu.Mapper.SpriteSize = Spritesize;

            Mmu.Ram[0x2000 & 0x2007] = v;
        }

        public void MaskW(byte v)
        {
            Backgroundleft = v.GetBit(1);
            Spriteleft = v.GetBit(2);
            Background = v.GetBit(3);
            Sprite = v.GetBit(4);
        }

        public byte StatusR()
        {
            byte v = (byte)((Vblank << 7 | (Sprite0hit ? 1 : 0) << 6 | SprOverflow << 5) & 0xe0
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
                    Vblank = 0;
                    //p2002 &= 0x7f;
                    return (byte)(v & 0x7f);
                }
            }
            Vblank = 0;

            Lp.W = false;
            return v;
        }

        public void OamAddressW(byte v) => OamAddr = v;

        public void OamDataW(byte v)
        {
            OamData = v;
            OamAddr++;
        }

        public void ScrollW(byte v) //2005
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

        public void AddressDataW(byte v) //2006
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

        public void DataW(byte v) //2007
        {
            Write(Lp.V, v);
            Lp.V += Vaddr > 0 ? 32 : 1;

            if (((Lp.V ^ A12) & 0x1000) > 0)
            {
                if ((Lp.V & 0x1000) > 0)
                    Mmu.Mapper.Scanline();
                A12 = Lp.V;
            }
        }

        public byte DataR()
        {
            byte v;
            if (Lp.V <= 0x3eff)
            {
                v = (byte)Dummy2007;
                Dummy2007 = Read(Lp.V);
            }
            else
                v = (byte)(Dummy2007 = Read(Lp.V));


            Lp.V += Vaddr > 0 ? 32 : 1;
            return v;
        }

        public void OamDamyCopy(byte v) //4014
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

            int x = Cycle - 1;
            int y = Scanline;
            int bg_pixel = 0;
            int bg_pal = 0;
            int spr_pixel = 0;
            int spr_pal = 0;
            int attrib = 0;
            if (Background)
            {
                if (x >= 8 || Backgroundleft)
                {
                    bg_pixel = ((BgShiftLo >> (15 - Lp.Fx)) & 1) | (BgShiftHi >> (15 - Lp.Fx) & 1) * 2;
                    bg_pal = (AtShiftLo >> (7 - Lp.Fx) & 1 | (AtShiftHi >> (7 - Lp.Fx) & 1) * 2) & 3;
                }
            }

            if (Sprite)
            {
                if (!(x < 8 && !Spriteleft))
                {
                    int bgaddr = Spraddr ? 0x1000 : 0x0000;
                    foreach (var spr in SpriteScan)
                    {
                        int tile = spr.Tile;
                        attrib = spr.Attrib;
                        byte spx = (byte)spr.X;
                        int fx = x - spr.X;
                        int fy = (y - (spr.Y + 1)) & (Spritesize ? 15 : 7);
                        if (spr.X == 255) continue;
                        if (spr.Y > 238) continue;
                        if (fx < 0 || fx > 7) continue;
                        if ((attrib & 0x40) == 0) fx = (byte)(7 - fx);
                        if ((attrib & 0x80) > 0) fy = (byte)((Spritesize ? 15 : 7) - fy);

                        int spraddr;
                        if (Spritesize)
                            spraddr = ((tile & 1) * 0x1000) + (tile & 0xfe) * 16 + fy + (fy & 8);
                        else
                            spraddr = bgaddr + tile * 16 + fy;

                        spr_pixel = (Read(spraddr) >> fx & 1) | (Read(spraddr + 8) >> fx & 1) * 2;
                        if (spr_pixel == 0) continue;

                        if (spr.Id == 0 && Sprite && Background)
                        {
                            if (bg_pixel > 0)
                            {
                                if (x != 255 && !Sprite0hit)
                                    Sprite0hit = true;
                            }
                        }
                        spr_pal = attrib & 3;
                        break;
                    }
                }
            }

            int offset = 0;
            if (bg_pixel > 0 && spr_pixel > 0)
            {
                if ((attrib & 0x20) == 0)
                    offset = spr_pixel + spr_pal * 4 + 0x10;
                else
                    offset = bg_pixel + bg_pal * 4;
            }
            else if (bg_pixel > 0)
                offset = bg_pixel + bg_pal * 4;
            else if (spr_pixel > 0)
                offset = spr_pixel + spr_pal * 4 + 0x10;


            //if (y > 7 && y < 232)
            ScreenBuffer[y * 256 + x] = pixPalettes[Read(0x3f00 + offset) % pixPalettes.Length];

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
                int size = Spritesize ? 16 : 8;
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
                    NtAddr = GetNtAddr();
                    LoadRegisters();
                    break;
                case 2: NtNyte = GetNtByte(NtAddr); break;
                case 3: AtAddr = GetAtAddr(); break;
                case 4:
                    AtByte = GetAtByte(AtAddr);
                    if ((Lp.V >> 5 & 2) > 0)
                        AtByte >>= 4;
                    if ((Lp.V & 2) > 0)
                        AtByte >>= 2;
                    break;
                case 5: BgAddr = GetBgAddr((byte)FineY); break;
                case 6: BgLo = GetBgLo(BgAddr); break;
                case 7: BgAddr += 8; break;
                case 0:
                    BgHi = GetBgHi(BgAddr);
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

        private bool OddFrame() => (FrameCounter & 1) == 1;
        private int GetNtAddr() => 0x2000 | Lp.V & 0xfff;
        private byte GetNtByte(int a) => Read(a);
        private int GetAtAddr() => 0x23c0 | Lp.V & 0xc00 | Lp.V >> 4 & 0x38 | Lp.V >> 2 & 0x07;
        private byte GetAtByte(int a) => Read(a);
        private int GetBgAddr(byte fy) => Bgaddr * 0x1000 + NtNyte * 16 + fy;
        private byte GetBgLo(int addr) => Read(addr);
        private byte GetBgHi(int addr) => Read(addr);

        private void LoadRegisters()
        {
            BgShiftLo = BgShiftLo & 0xff00 | BgLo;
            BgShiftHi = BgShiftHi & 0xff00 | BgHi;
            AtLo = AtByte & 1;
            AtHi = (AtByte & 2) > 0 ? 1 : 0;
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
                if (Mmu.Mapper.Header.ChrBanks > 0)
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

        public void Write(int addr, byte v)
        {
            addr &= 0x3fff;
            var vram = Mmu.Vram;

            //Mmu.CheckForBeakpoint(addr, v, BPType.VWrite);

            if (addr < 0x2000 || addr >= 0x3f00)
                vram[addr] = v;
            else
            {
                var a = addr % 0x400;
                if (Header.Mirror == SingleNt0)
                {
                    var b = MirrorNt0[(addr >> 10) & 3];
                    vram[0x2000 + a + b * 0x400] = v;
                }
                else if (Header.Mirror == SingleNt1)
                {
                    var b = MirrorNt1[((a >> 10) & 3)];
                    vram[0x2000 + a + b * 0x400] = v;
                }
                else if (Header.Mirror == Horizontal)
                {
                    switch ((addr >> 10) & 3)
                    {
                        case 0: vram[0x2000 + a] = v; break;
                        case 1: vram[0x2000 + a] = v; break;
                        case 2: vram[0x2400 + a] = v; break;
                        case 3: vram[0x2400 + a] = v; break;
                    }
                }
                else if (Header.Mirror == Vertical)
                {
                    switch ((addr >> 10) & 3)
                    {
                        case 0: vram[0x2000 + a] = v; break;
                        case 1: vram[0x2400 + a] = v; break;
                        case 2: vram[0x2000 + a] = v; break;
                        case 3: vram[0x2400 + a] = v; break;
                    }
                }
            }

            for (int i = 0; i < 7; i++)
                Array.Copy(vram, 0x3f00, vram, 0x3f20 + i * 32, 0x20);

            for (int i = 0; i < 4; i++)
                Array.Copy(vram, 0x3f10, vram, 0x3f04 + i * 0x4, 0x01);

            if (addr == 0x3f10)
                vram[0x3f00] = v;
            else if (addr == 0x3f00)
                vram[0x3f10] = v;
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
                    int bgaddr = Bgaddr + Read(ppuaddr) * 16 + fy;
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
            bw.Write(Vaddr);
            bw.Write(Spraddr);
            bw.Write(Bgaddr);
            bw.Write(Spritesize);
            bw.Write(Master);
            bw.Write(Nmi);

            bw.Write(Greyscale);
            bw.Write(Backgroundleft);
            bw.Write(Spriteleft);
            bw.Write(Background);
            bw.Write(Sprite);
            bw.Write(Red);
            bw.Write(Green);
            bw.Write(Blue);

            bw.Write(Lsb);
            bw.Write(SprOverflow);
            bw.Write(Sprite0hit);
            bw.Write(Vblank);
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
            Vaddr = br.ReadInt32();
            Spraddr = br.ReadBoolean();
            Bgaddr = br.ReadInt32();
            Spritesize = br.ReadBoolean();
            Master = br.ReadInt32();
            Nmi = br.ReadBoolean();

            Greyscale = br.ReadInt32();
            Backgroundleft = br.ReadBoolean();
            Spriteleft = br.ReadBoolean();
            Background = br.ReadBoolean();
            Sprite = br.ReadBoolean();
            Red = br.ReadInt32();
            Green = br.ReadInt32();
            Blue = br.ReadInt32();

            Lsb = br.ReadInt32();
            SprOverflow = br.ReadInt32();
            Sprite0hit = br.ReadBoolean();
            Vblank = br.ReadInt32();

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

        public Dictionary<string, dynamic> GetPpuInfo() => new()
        {
            ["Cycle"] = $"{Cycle}",
            ["Scanline"] = $"{Scanline}",
            ["Total Cycles"] = $"{Totalcycles}",
            ["V"] = $"{Lp.V:X4}",
            ["T"] = $"{Lp.T:X4}",
            ["X"] = $"{Lp.Fx:X2}",
            ["Background"] = $"{Background}",
            ["Sprite"] = $"{Sprite}",
            ["Sprite 0 Hit"] = $"{Sprite0hit}",
            ["VBlank"] = $"{Vblank}",
            ["Nmi"] = $"{Nmi}",
        };

        public struct PpuRegisters
        {
            public int V;
            public int T;
            public int Fx;
            public bool W;
        };
        public PpuRegisters Lp;

        private struct SpriteData(int y, int tile, int attrib, int x, int id)
        {
            public int Y = y;
            public int Tile = tile;
            public int Attrib = attrib;
            public int X = x;
            public int Id = id;
        }
    }
}
