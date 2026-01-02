using Gmulator.Core.Gbc;
using Gmulator.Interfaces;
using Gmulator.Shared;
using Raylib_cs;
using System;
using System.Security.Cryptography;

namespace Gmulator.Core.Gbc;

public partial class GbcPpu : IPpu, ISaveState
{
    private const byte HBLANK = 0;
    private const byte VBLANK = 1;
    private const byte OAM = 2;
    private const byte LCD = 3;
    private const int VblankDots = 456;
    private const int OamDots = 80;
    private const int LcdDots = 172;

    public GbcMmu Mmu { get; private set; }
    private readonly Action<uint[]> UpdateScreen;
    private readonly Gbc Gbc;
    private readonly List<Sprite> _sprites;
    public byte[] LineBGColors { get; private set; }
    public uint FrameCounter { get; private set; }
    private int PrevMode { get; set; }

    private int _wly;
    private int _dots;
    private bool _cgb;
    private int _ly;
    private int _lyc;
    private int _lcdc;
    private int _scy;
    private int _scx;
    private int _wy;
    private int _wx;
    private int _stat;
    private int _bgp;
    private int _obp0;
    private int _obp1;
    private int _oamDma;
    private int _key1;
    private int _bgpi;
    private int _bgpd;
    private int _obpi;
    private int _obpd;
    private int _hdma1;
    private int _hdma2;
    private int _hdma3;
    private int _hdma4;
    private int _hdma5;
    public uint[] ScreenBuffer { get; set; }
    public byte[] CGBBkgPal { get; private set; }
    public byte[] CGBObjPal { get; private set; }

    public int SpeedMode { get => (_key1 & 0x80) != 0 ? 2 : 1; }
    public bool DMAactive { get => (_hdma5 & 0x80) != 0; }
    public bool DMAHBlank { get; set; }

    public uint[] ClearBuffer(uint[] buffer) => [.. Enumerable.Repeat(GbColors[1][3], buffer.Length)];
    public int GetScanline() => _ly;
    private bool BackgroundOn => (_lcdc & 0x01) != 0;
    private bool SpriteOn => (_lcdc & 0x02) != 0;
    private bool WindowOn => (_lcdc & 0x20) != 0;
    public int WindowAddr { get => (_lcdc & 0x40) != 0 ? 0x9c00 : 0x9800; }
    public int TileAddr { get => (_lcdc & 0x10) != 0 ? 0x8000 : 0x8800; }
    public int MapAddr { get => (_lcdc & 0x08) != 0 ? 0x9c00 : 0x9800; }

    public readonly uint[][] GbColors =
    [
        [0xffe7ffd6, 0xff88c070, 0xff346856, 0xff082432],
        [0xffffffff, 0xffaaaaaa, 0xff555555, 0xff000000]
    ];

    public GbcPpu(Gbc gbc)
    {
        Gbc = gbc;
        Mmu = gbc.Mmu;
        UpdateScreen = gbc.UpdateScreen;

        ScreenBuffer = new uint[GbWidth * GbHeight * 4];
        LineBGColors = new byte[GbWidth * 4];
        _sprites = [];
        CGBBkgPal = new byte[64];
        CGBObjPal = new byte[64];

        gbc.SetMemory(0x00, 0x01, 0xff40, 0xff70, 0xffff, Read, Write, RamType.Register, 1);
    }

    public void Step(int cyc)
    {
        if ((_lcdc & 0x80) == 0)
        {
            _ly = 0;
            _wly = 0;
            _dots = 0;
            SetMode(0, 0);
            return;
        }

        _dots += cyc;
        switch (_stat & 3)
        {
            case HBLANK:
                if (DMAHBlank && _ly < 144)
                {
                    var src = (_hdma1 << 8 | _hdma2) & 0xfff0;
                    var dst = ((_hdma3 << 8 | _hdma4) & 0x1ff0) | 0x8000;
                    Mmu.WriteBlock(src, dst, (byte)((_hdma5 + 1) & 0x7f) * 16);
                    _hdma5--;
                    if (_hdma5 == 0xff)
                        DMAHBlank = false;
                }

                if (_dots >= VblankDots)
                {
                    if (++_ly > 143)
                    {
                        SetMode(VBLANK, 4);
                        Gbc.Cpu.RequestIF(IntVblank);
                        _wly = 0;
                    }
                    else
                    {
                        SetMode(OAM, 5);
                    }
                }
                break;
            case VBLANK:
                if (_dots >= VblankDots)
                {
                    if (_ly == 153)
                        CompareLYC();

                    if (++_ly > 153)
                    {
                        SetMode(OAM, 5);
                        _ly = 0;
                        _wly = 0;
                        FrameCounter++;
                        UpdateScreen(ScreenBuffer);
                    }
                }
                break;
            case OAM:
                if (_dots >= OamDots)
                    SetMode(LCD, 0);
                break;
            case LCD:
                if (_dots >= LcdDots)
                {
                    if (_ly < 144)
                        DrawScanline();
                    SetMode(HBLANK, 3);
                }
                break;
        }

        if (_dots >= VblankDots)
        {
            _dots -= VblankDots;
            CompareLYC();
        }
    }

    private void CompareLYC()
    {
        if (_ly == _lyc)
        {
            _stat |= 4;
            if ((_stat & 0x40) != 0)
                Gbc.Cpu.RequestIF(IntLcd);
        }
        else
            _stat &= unchecked((byte)~4);
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
            byte sy = (byte)(_ly + _scy);
            byte sx = (byte)(x + _scx);

            uint rgb = 0;
            byte color = 0;
            int attribute = 0;

            if (BackgroundOn)
            {
                if (!_cgb)
                {
                    int bgaddr = GetBgAddr(TileAddr, MapAddr, sx, sy);
                    color = GetColor(bgaddr, sx, false);
                    rgb = GbColors[1][(byte)(_bgp >> (color << 1) & 3)];
                    // rgb = GbColors[1][0];
                }
                else
                {
                    var attaddr = ((MapAddr + sy / 8 * 32) + sx / 8);
                    attribute = Mmu.ReadAttribute(attaddr);
                    var bank = ((attribute >> 3) & 1) * 0x2000;
                    int bgaddr = GetBgAddr(TileAddr, MapAddr, sx, sy, (attribute & 0x40) != 0) + bank;
                    color = GetColor(bgaddr, sx, (attribute & 0x20) != 0);
                    var n = (((attribute & 7) << 2) + color) << 1;
                    var pal = (ushort)(CGBBkgPal[n] | CGBBkgPal[n + 1] << 8);
                    rgb = GetRGB555(pal);
                }
            }

            ScreenBuffer[_ly * GbWidth + x] = rgb;
            LineBGColors[x] = (byte)(color | (attribute & 0x80));
        }
    }

    private void DrawWindow()
    {
        int wx = _wx - 7;
        var row = _wly;
        int bgPixel;

        if (row >= GbHeight || wx >= GbWidth)
            return;

        if (_wy > _ly || _wy > GbHeight)
            return;

        for (int x = 0; x < 256; x++)
        {
            if (wx + x < 0 || wx + x > GbWidth)
                continue;

            uint rgb;
            byte color;
            if (!_cgb)
            {
                int bgaddr = GetBgAddr(TileAddr, WindowAddr, x, row);
                color = GetColor(bgaddr, x, false);
                bgPixel = (byte)(_bgp >> (color << 1) & 3);
                rgb = GbColors[1][bgPixel];
            }
            else
            {
                var att = Mmu.ReadVram((ushort)(WindowAddr - 0x8000 + row / 8 * 32) + x / 8 + 0x2000);
                var bank = ((att >> 3) & 1) * 0x2000;
                int bgaddr = GetBgAddr(TileAddr, WindowAddr, x, row, (att & 0x40) != 0) + bank;
                color = GetColor(bgaddr, x, (att & 0x20) != 0);
                var n = (((att & 7) << 2) + color) << 1;
                var pal = (ushort)(CGBBkgPal[n] | CGBBkgPal[n + 1] << 8);
                rgb = GetRGB555(pal);
            }
            ScreenBuffer[_ly * GbWidth + wx + x] = rgb;
        }
        _wly++;
    }

    public void DrawSprites()
    {
        int numsprites = 0;
        int size = (_lcdc & 0x04) != 0 ? 16 : 8;
        bool[] sprites = new bool[40];

        for (int i = 0; i < 40; i++)
        {
            int sy = Mmu.ReadByte(0xfe00 + i * 4) - 16;
            if (_ly >= sy && _ly < sy + size)
            {
                sprites[i] = true;
                numsprites++;
                if (numsprites > 9)
                    break;
            }
        }

        for (int i = 39; i >= 0; i--)
        {
            int sy = Mmu.ReadByte(0xfe00 + i * 4) - 16;
            int sx = Mmu.ReadByte(0xfe01 + i * 4) - 8;
            int ti = Mmu.ReadByte(0xfe02 + i * 4);
            int at = Mmu.ReadByte(0xfe03 + i * 4);
            bool flipX = (at & 0x20) != 0;
            bool flipY = (at & 0x40) != 0;

            if (!sprites[i])
                continue;

            int fy = flipY ? (_ly - sy ^ (size - 1)) : _ly - sy;

            int tile = size == 16 ? ti & 0xfe : ti;
            int bgaddr = 0x8000 + tile * 16 + fy * 2;
            for (int xx = 0; xx < 8; xx++)
            {
                if (sx + xx < 0 || sx + xx > GbWidth || sy >= GbHeight)
                    continue;

                int pos = _ly * GbWidth + sx + xx;
                bool priority = (at & 0x80) == 0;
                int color;
                var fx = flipX ? xx : xx ^ 7;
                var bank = _cgb ? ((at >> 3) & 1) * 0x2000 : 0;

                uint rgb;
                if (!_cgb)
                {
                    color = (Mmu.ReadVram((ushort)bgaddr - 0x8000) >> (fx & 7) & 1) |
                        (Mmu.ReadVram((ushort)bgaddr - 0x8000 + 1) >> (fx & 7) & 1) * 2;
                    var spPixel = (at & 0x10) != 0 ? _obp1 >> (color << 1) & 3 : _obp0 >> (color << 1) & 3;
                    rgb = GbColors[1][spPixel];
                }
                else
                {
                    color = (Mmu.ReadVram((ushort)bgaddr + bank) >> (fx & 7) & 1) |
                        (Mmu.ReadVram((ushort)bgaddr + 1 + bank) >> (fx & 7) & 1) * 2;
                    var n = ((at & 7) << 2) + color << 1;
                    var pal = CGBObjPal[n] | CGBObjPal[n + 1] << 8;
                    rgb = GetRGB555((ushort)pal);
                }

                var bgcolor = LineBGColors[sx + xx] & 3;
                if (color != 0)
                {
                    if (!_cgb)
                    {
                        if (priority || bgcolor == 0)
                            ScreenBuffer[pos] = rgb;
                    }
                    else
                    {
                        var bgpriority = (LineBGColors[sx + xx] & 0x80) != 0;
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
        int tile = Mmu.ReadVram((ushort)(mapaddr + sy / 8 * 32) + sx / 8);
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
            res = (Mmu.ReadVram((ushort)bgaddr) >> (sx & 7) & 1) |
             (Mmu.ReadVram((ushort)bgaddr + 1) >> (sx & 7) & 1) * 2;
        }
        else
        {
            res = Mmu.ReadVram((ushort)bgaddr) >> (7 - (sx & 7)) & 1 |
                (Mmu.ReadVram((ushort)bgaddr + 1) >> (7 - (sx & 7)) & 1) * 2;
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
        _stat = (byte)(_stat & 0xfc | n);
        if (bit != 0)
        {
            //if (PrevMode != n && (_stat & bit) != 0)
            //    IO.IF |= IntLcd;
            PrevMode = n;
        }
    }

    public void SetBkgPalette(int o, int v) => CGBBkgPal[o & 0x3f] = (byte)v;
    public void SetObjPalette(int o, int v) => CGBObjPal[o & 0x3f] = (byte)v;

    public void Reset(bool cgb)
    {
        _dots = 173;
        _lcdc = 0x91; _stat = 0x81;
        _ly = 146; _lyc = 0;
        _cgb = cgb;
        _key1 = 0;
        _bgpd = 0xff;

        Array.Fill<byte>(CGBBkgPal, 0x00);
        Array.Fill<byte>(CGBObjPal, 0x00);

        ScreenBuffer = ClearBuffer(ScreenBuffer);
    }

    public void Save(BinaryWriter bw)
    {
        bw.Write(_wly); bw.Write(_dots); bw.Write(_cgb); bw.Write(_ly);
        bw.Write(_lyc); bw.Write(_lcdc); bw.Write(_scy); bw.Write(_scx);
        bw.Write(_wy); bw.Write(_wx); bw.Write(_stat); bw.Write(_bgp);
        bw.Write(_obp0); bw.Write(_obp1); bw.Write(_oamDma); bw.Write(_key1);
        bw.Write(_bgpi); bw.Write(_bgpd); bw.Write(_obpi); bw.Write(_obpd);
        bw.Write(_hdma1); bw.Write(_hdma2); bw.Write(_hdma3); bw.Write(_hdma4);
        bw.Write(_hdma5); WriteArray(bw, ScreenBuffer); WriteArray(bw, CGBBkgPal); WriteArray(bw, CGBObjPal);
    }

    public void Load(BinaryReader br)
    {
        _wly = br.ReadInt32(); _dots = br.ReadInt32(); _cgb = br.ReadBoolean(); _ly = br.ReadInt32();
        _lyc = br.ReadInt32(); _lcdc = br.ReadInt32(); _scy = br.ReadInt32(); _scx = br.ReadInt32();
        _wy = br.ReadInt32(); _wx = br.ReadInt32(); _stat = br.ReadInt32(); _bgp = br.ReadInt32();
        _obp0 = br.ReadInt32(); _obp1 = br.ReadInt32(); _oamDma = br.ReadInt32(); _key1 = br.ReadInt32();
        _bgpi = br.ReadInt32(); _bgpd = br.ReadInt32(); _obpi = br.ReadInt32(); _obpd = br.ReadInt32();
        _hdma1 = br.ReadInt32(); _hdma2 = br.ReadInt32(); _hdma3 = br.ReadInt32(); _hdma4 = br.ReadInt32();
        _hdma5 = br.ReadInt32(); ScreenBuffer = ReadArray<uint>(br, ScreenBuffer.Length); CGBBkgPal = ReadArray<byte>(br, CGBBkgPal.Length); CGBObjPal = ReadArray<byte>(br, CGBObjPal.Length);
    }

    public List<RegisterInfo> GetState() =>
    [
        new("", "Scanline", $"{_ly}"),
        new("FF40", "LCDC", ""),
        new("0", "Background", (_lcdc & 0x01) != 0 ? "Enabled" : "Disabled"),
        new("1", "Sprites", (_lcdc & 0x02) != 0 ? "Enabled" : "Disabled"),
        new("2", "Sprite Size", (_lcdc & 0x04) != 0 ? "8x16" : "8x8"),
        new("3", "BG Map", (_lcdc & 0x08) != 0 ? "9C00:9FFF" : "9800:9BFF"),
        new("4", "BG Tile", (_lcdc & 0x10) != 0 ? "8000:8FFF" : "8800:97FF"),
        new("5", "Window", (_lcdc & 0x20) != 0 ? "Enabled" : "Disabled"),
        new("6", "Window Map", (_lcdc & 0x40) != 0 ? "9C00:9FFF" : "9800:9BFF"),
        new("7", "LCD", (_lcdc & 0x80) != 0 ? "Enabled" : "Disabled"),
        new("FF41", "STAT", ""),
        new("0-1", "PPU mode", $"{(_stat & 3)}"),
        new("2", "LYC == LY", (_stat & 0x04) != 0 ? "Enabled" : "Disabled"),
        new("3", "Mode 0 select", (_stat & 0x08) != 0 ? "Enabled" : "Disabled"),
        new("4", "Mode 1 select", (_stat & 0x10) != 0 ? "Enabled" : "Disabled"),
        new("5", "Mode 2 select", (_stat & 0x20) != 0 ? "Enabled" : "Disabled"),
        new("6", "LYC select", (_stat & 0x40) != 0 ? "Enabled" : "Disabled"),
        //return [.. IO.GetState()];
    ];
}

public class Sprite(int i, byte x, byte y, byte tile, byte attribute)
{
    public int ID = i;
    public byte X = x;
    public byte Y = y;
    public byte Tile = tile;
    public byte Attribute = attribute;
}
