using Gmulator.Shared;

namespace Gmulator.Core.Gbc;
public class GbcPpu : EmuState
{
    private const byte HBLANK = 0;
    private const byte VBLANK = 1;
    private const byte OAM = 2;
    private const byte LCD = 3;
    private const int VblankDots = 456;
    private const int OamDots = 80;
    private const int LcdDots = 172;

    public int Dots { get; set; }
    public GbcMmu Mmu { get; private set; }
    public GbcIO IO { get; private set; }

    public byte[] LineBGColors { get; private set; }

    public bool LCDOn { get; private set; }
    public uint FrameCounter { get; private set; }

    private int PrevMode { get; set; }
    public int WLY { get; private set; }
    public byte[] CGBBkgPal { get; private set; }
    public byte[] CGBObjPal { get; private set; }
    private bool CGB;

    private bool BackgroundOn => IO.LCDC.GetBit(0);
    private bool SpriteOn => IO.LCDC.GetBit(1);
    private bool WindowOn => IO.LCDC.GetBit(5);
    public int WindowAddr { get => IO.LCDC.GetBit(6) ? 0x9c00 : 0x9800; }
    public int TileAddr { get => IO.LCDC.GetBit(4) ? 0x8000 : 0x8800; }
    public int MapAddr { get => IO.LCDC.GetBit(3) ? 0x9c00 : 0x9800; }

    private uint[] ScreenBuffer;

    public readonly uint[][] GbColors =
    [
        [0xffe7ffd6, 0xff88c070, 0xff346856, 0xff082432],
        [0xffffffff, 0xffaaaaaa, 0xff555555, 0xff000000]
    ];
    private readonly Gbc Gbc;
    public List<Sprite> sprites;

    public uint[] ClearBuffer(uint[] buffer) => [.. Enumerable.Repeat(GbColors[1][3], buffer.Length)];

    public GbcPpu(Gbc gbc)
    {
        Gbc = gbc;
        Mmu = gbc.Mmu;
        IO = gbc.IO;

        ScreenBuffer = new uint[GbWidth * GbHeight * 4];
        LineBGColors = new byte[GbWidth * 4]; ;
        sprites = [];
        CGBBkgPal = new byte[64];
        CGBObjPal = new byte[64];
    }

    public void Step(int cyc)
    {
        if (!IO.LCDC.GetBit(7))
        {
            IO.LY = 0;
            WLY = 0;
            Dots = 0;
            SetMode(0, 0);
            return;
        }

        Dots += cyc;
        switch (IO.STAT & 3)
        {
            case HBLANK:
                if (IO.DMAHBlank && IO.LY < 144)
                {
                    var src = (IO.HDMA1 << 8 | IO.HDMA2) & 0xfff0;
                    var dst = ((IO.HDMA3 << 8 | IO.HDMA4) & 0x1ff0) | 0x8000;
                    Mmu.WriteBlock(src, dst, (byte)((IO.HDMA5 + 1) & 0x7f) * 16);
                    IO.HDMA5--;
                    if (IO.HDMA5 == 0xff)
                        IO.DMAHBlank = false;
                }

                if (Dots >= VblankDots)
                {
                    if (++IO.LY > 143)
                    {
                        SetMode(VBLANK, 4);
                        IO.IF |= IntVblank;
                        WLY = 0;
                    }
                    else
                    {
                        SetMode(OAM, 5);
                    }
                }
                break;
            case VBLANK:
                if (Dots >= VblankDots)
                {
                    if (IO.LY == 153)
                        CompareLYC();

                    if (++IO.LY > 153)
                    {
                        SetMode(OAM, 5);
                        IO.LY = 0;
                        WLY = 0;
                        FrameCounter++;
                        Texture.Update(Gbc.Screen.Texture, ScreenBuffer);
                    }
                }
                break;
            case OAM:
                if (Dots >= OamDots)
                    SetMode(LCD, 0);
                break;
            case LCD:
                if (Dots >= LcdDots)
                {
                    if (IO.LY < 144)
                        DrawScanline();
                    SetMode(HBLANK, 3);
                }
                break;
        }

        if (Dots >= VblankDots)
        {
            Dots -= VblankDots;
            CompareLYC();
        }
    }

    private void CompareLYC()
    {
        if (IO.LY == IO.LYC)
        {
            IO.STAT |= 4;
            if (IO.STAT.GetBit(6))
                IO.IF |= IntLcd;
        }
        else
            IO.STAT &= unchecked((byte)~4);
    }

    private void DrawScanline()
    {
        if (!Gbc.FastForward || Gbc.FastForward && FrameCounter % Gbc.Config.FrameSkip == 0)
        {
            Array.Fill<byte>(LineBGColors, 0);
            DrawBackground();
            if (WindowOn)
                DrawWindow();
            if (SpriteOn)
                DrawSprites();
        }
    }

    private void DrawBackground()
    {
        for (int x = 0; x < GbWidth; x++)
        {
            byte sy = (byte)(IO.LY + IO.SCY);
            byte sx = (byte)(x + IO.SCX);

            uint rgb = 0;
            byte color = 0;
            byte att = 0;

            if (!CGB)
            {
                if (BackgroundOn)
                {
                    int bgaddr = GetBgAddr(TileAddr, MapAddr, sx, sy);
                    color = GetColor(bgaddr, sx, false);
                    rgb = GbColors[1][(byte)(IO.BGP >> (color << 1) & 3)];
                }
                else
                    rgb = GbColors[1][0];
            }
            else
            {
                if (BackgroundOn)
                {
                    var attaddr = (ushort)(MapAddr - 0x8000 + sy / 8 * 32) + sx / 8 + 0x2000;
                    att = Mmu.Vram[attaddr];
                    var bank = ((att >> 3) & 1) * 0x2000;
                    int bgaddr = GetBgAddr(TileAddr, MapAddr, sx, sy, att.GetBit(6)) + bank;
                    color = GetColor(bgaddr, sx, att.GetBit(5));
                    var n = (((att & 7) << 2) + color) << 1;
                    var pal = (ushort)(CGBBkgPal[n] | CGBBkgPal[n + 1] << 8);
                    rgb = GetRGB555(pal);
                }
            }
            ScreenBuffer[IO.LY * GbWidth + x] = rgb;
            LineBGColors[x] = (byte)(color | att & 0x80);
        }
    }

    private void DrawWindow()
    {
        int wy = IO.WY;
        int wx = IO.WX - 7;
        var row = WLY;
        int bgPixel;

        if (row >= GbHeight || wx >= GbWidth)
            return;

        if (wy > IO.LY || wy > GbHeight)
            return;

        for (int x = 0; x < 256; x++)
        {
            if (wx + x < 0 || wx + x > GbWidth)
                continue;

            uint rgb;
            byte color;
            if (!CGB)
            {
                int bgaddr = GetBgAddr(TileAddr, WindowAddr, x, row);
                color = GetColor(bgaddr, x, false);
                bgPixel = (byte)(IO.BGP >> (color << 1) & 3);
                rgb = GbColors[1][bgPixel];
            }
            else
            {
                var att = Mmu.Vram[(ushort)(WindowAddr - 0x8000 + row / 8 * 32) + x / 8 + 0x2000];
                var bank = ((att >> 3) & 1) * 0x2000;
                int bgaddr = GetBgAddr(TileAddr, WindowAddr, x, row, att.GetBit(6)) + bank;
                color = GetColor(bgaddr, x, att.GetBit(5));
                var n = (((att & 7) << 2) + color) << 1;
                var pal = (ushort)(CGBBkgPal[n] | CGBBkgPal[n + 1] << 8);
                rgb = GetRGB555(pal);
            }
            ScreenBuffer[IO.LY * GbWidth + wx + x] = rgb;
            //LineBGColors[x] |= color;
        }
        WLY++;
    }

    public void DrawSprites()
    {
        int numsprites = 0;
        int size = IO.LCDC.GetBit(2) ? 16 : 8;
        bool[] sprites = new bool[40];

        for (int i = 0; i < 40; i++)
        {
            int sy = Mmu.Ram[0xfe00 + i * 4] - 16;
            if (IO.LY >= sy && IO.LY < sy + size)
            {
                sprites[i] = true;
                numsprites++;
                if (numsprites > 9)
                    break;
            }
        }

        for (int i = 39; i >= 0; i--)
        {
            int sy = Mmu.Ram[0xfe00 + i * 4] - 16;
            int sx = Mmu.Ram[0xfe01 + i * 4] - 8;
            byte ti = Mmu.Ram[0xfe02 + i * 4];
            byte at = Mmu.Ram[0xfe03 + i * 4];
            bool flipX = at.GetBit(5);
            bool flipY = at.GetBit(6);

            if (!sprites[i])
                continue;

            int fy = flipY ? (IO.LY - sy ^ (size - 1)) : IO.LY - sy;

            int tile = size == 16 ? ti & 0xfe : ti;
            int bgaddr = 0x8000 + tile * 16 + fy * 2;
            for (int xx = 0; xx < 8; xx++)
            {
                if (sx + xx < 0 || sx + xx > GbWidth || sy >= GbHeight)
                    continue;

                int pos = IO.LY * GbWidth + sx + xx;
                bool priority = !at.GetBit(7);
                int color;
                var fx = flipX ? xx : xx ^ 7;
                var bank = CGB ? ((at >> 3) & 1) * 0x2000 : 0;

                uint rgb;
                if (!CGB)
                {
                    color = (Mmu.Ram[(ushort)bgaddr] >> (fx & 7) & 1) |
                        (Mmu.Ram[(ushort)bgaddr + 1] >> (fx & 7) & 1) * 2;
                    var spPixel = at.GetBit(4) ? IO.OBP1 >> (color << 1) & 3 : IO.OBP0 >> (color << 1) & 3;
                    rgb = GbColors[1][spPixel];
                }
                else
                {
                    color = (Mmu.Vram[(ushort)bgaddr - 0x8000 + bank] >> (fx & 7) & 1) |
                        (Mmu.Vram[(ushort)bgaddr + 1 - 0x8000 + bank] >> (fx & 7) & 1) * 2;
                    var n = ((at & 7) << 2) + color << 1;
                    var pal = CGBObjPal[n] | CGBObjPal[n + 1] << 8;
                    rgb = GetRGB555((ushort)pal);
                }

                var bgcolor = LineBGColors[sx + xx] & 3;
                if (color > 0)
                {
                    if (!CGB)
                    {
                        if (priority || bgcolor == 0)
                            ScreenBuffer[pos] = rgb;
                    }
                    else
                    {
                        var bgpriority = LineBGColors[sx + xx].GetBit(7);
                        if (bgcolor == 0)
                            ScreenBuffer[pos] = rgb;
                        else if (!BackgroundOn)
                            ScreenBuffer[pos] = rgb;
                        else if (!bgpriority && priority)
                            ScreenBuffer[pos] = rgb;
                    }
                }
            }
        }
    }

    public int GetBgAddr(int tileaddr, int mapaddr, int sx, int sy, bool flipy = false)
    {
        int tile = Mmu.Vram[(ushort)(mapaddr - 0x8000 + sy / 8 * 32) + sx / 8];
        var off = flipy ? 14 - (sy & 7) * 2 : (sy & 7) * 2;
        if (tileaddr - 0x8000 != 0)
            return (ushort)(tileaddr - 0x8000 + 0x800 + (sbyte)tile * 16) + off;
        else
            return (ushort)(tileaddr - 0x8000 + tile * 16) + off;
    }

    public byte GetColor(int bgaddr, int sx, bool flipx)
    {
        int res;
        if (flipx)
        {
            res = (Mmu.Vram[(ushort)bgaddr] >> (sx & 7) & 1) |
             (Mmu.Vram[(ushort)bgaddr + 1] >> (sx & 7) & 1) * 2;
        }
        else
        {
            res = Mmu.Vram[(ushort)bgaddr] >> (7 - (sx & 7)) & 1 |
                (Mmu.Vram[(ushort)bgaddr + 1] >> (7 - (sx & 7)) & 1) * 2;
        }
        return (byte)res;
    }

    public static uint GetRGB555(ushort p)
    {
        var r = p & 0x1f;
        var g = p >> 5 & 0x1f;
        var b = p >> 10 & 0x1f;
        return (uint)((byte)(r << 3 | r >> 2) |
        (byte)(g << 3 | g >> 2) << 8 |
        (byte)(b << 3 | b >> 2) << 16 | 0xff000000);
    }

    public void SetMode(int n, byte bit)
    {
        IO.STAT = (byte)(IO.STAT & 0xfc | n);
        if (bit > 0)
        {
            if (PrevMode != n && IO.STAT.GetBit(bit))
                IO.IF |= IntLcd;
            PrevMode = n;
        }
    }

    public void WriteDMA(byte v)
    {
        var a = v << 8;
        Span<byte> srcbytes = (a >> 12) switch
        {
            <= 0x07 => Mmu.Mapper.ReadRomBlock(a, 0xa0),
            <= 0x09 => CGB ? new Span<byte>(Mmu.Vram, a, 0xa0) : new(Mmu.Ram, a, 0xa0),
            <= 0x0b => new Span<byte>(Mmu.Sram, a - 0xa000 + (Mmu.Mapper.Rambank * 0x2000), 0xa0),
            <= 0x0c => CGB ? new Span<byte>(Mmu.Ram, a, 0xa0) : new(Mmu.Ram, a, 0xa0),
            <= 0x0d => CGB ? new Span<byte>(Mmu.Wram, (a - 0xd000) + IO.SVBK * 0x1000, 0xa0) : new(Mmu.Ram, a, 0xa0),
            _ => null,
        };

        if (!srcbytes.IsEmpty)
        {
            Span<byte> dstbytes = new(Mmu.Ram, 0xfe00, 0xa0);
            srcbytes.CopyTo(dstbytes);
        }
    }

    public void SetBkgPalette(int o, byte v) => CGBBkgPal[o & 0x3f] = v;
    public void SetObjPalette(int o, byte v) => CGBObjPal[o & 0x3f] = v;

    public void Reset(bool cgb)
    {
        Dots = 173;
        IO.LCDC = 0x91; IO.STAT = 0x81;
        IO.LY = 146; IO.LYC = 0;
        CGB = cgb;

        Array.Fill<byte>(CGBBkgPal, 0x00);
        Array.Fill<byte>(CGBObjPal, 0x00);

        ScreenBuffer = ClearBuffer(ScreenBuffer);
    }

    public override void Save(BinaryWriter bw)
    {
        byte[] p = new byte[ScreenBuffer.Length];
        Buffer.BlockCopy(ScreenBuffer, 0, p, 0, p.Length);
        bw.Write(Dots);
        bw.Write(CGBBkgPal);
        bw.Write(CGBObjPal);
        bw.Write(p);
    }

    public override void Load(BinaryReader br)
    {
        Dots = br.ReadInt32();
        CGBBkgPal = br.ReadBytes(CGBBkgPal.Length);
        CGBObjPal = br.ReadBytes(CGBObjPal.Length);
        var p = br.ReadBytes(ScreenBuffer.Length);
        Buffer.BlockCopy(p, 0, ScreenBuffer, 0, p.Length);
    }
}

public class Sprite(int i, byte x, byte y, byte tile, byte attribute)
{
    public int ID = i;
    public byte X = x;
    public byte Y = y;
    public byte Tile = tile;
    public byte Attribute = attribute;
}
