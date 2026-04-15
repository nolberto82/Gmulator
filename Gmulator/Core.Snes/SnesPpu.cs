using Gmulator.Interfaces;
using System.Collections.Frozen;

namespace Gmulator.Core.Snes;

public partial class SnesPpu : ISaveState, IPpu
{
    #region State
    public int VPos { get; private set; }
    public int HPos { get; private set; }
    public ulong Cycles { get; private set; }
    public uint FrameCounter { get; private set; }
    public bool FrameReady { get; set; }
    private bool _cgRamToggle;
    private int _prevScrollX;
    private int _currScrollX;
    private bool _vblank;
    private bool _hblank;
    private int _autoJoyCounter;
    private int _mosaicSize;
    private int _scrollXMode7;
    private int _scrollYMode7;
    private int _w1Left;
    private int _w1Right;
    private int _w2Left;
    private int _w2Right;
    private bool _addSub;
    private bool _dirColor;
    private int _prevent;
    private int _clip;
    private int _spritesScanline;
    private readonly int[] _objSizeWidth = [8, 8, 8, 16, 16, 32, 16, 16, 16, 32, 64, 32, 64, 64, 32, 32];
    private readonly int[] _objSizeHeight = [8, 8, 8, 16, 16, 32, 32, 32, 16, 32, 64, 32, 64, 64, 64, 32];
    private int _objTable1;
    private int _objTable2;
    private int _objSize;
    private bool _objPrioRotation;
    private int _objPrioIndex;
    private int _oamAddr;
    private int _interOamAddr;
    private int _brightness;
    private bool _forcedBlank;
    private int _bgMode;
    private bool _mode1Bg3Priority;
    private int _ramAddrLow;
    private int _ramAddrMedium;
    private int _ramAddrHigh;
    private int _multiplyA;
    private int _multiplyB;
    private int _dividend;
    private int _divisor;
    private int _vramAddrIncrease;
    private int _vramAddrRemap;
    private bool _vramAddrMode;
    private int _vramAddr;
    private int _vramLatch;
    private bool _overscanMode;
    private bool _hiResMode;
    private bool _extBgMode;
    private int _m7A; //211B
    private int _m7B; //211C
    private int _m7C; //211D
    private int _m7D; //211E
    private int _m7X; //211F
    private int _m7Y; //2120
    private int _cgAdd; //2121
    private int _cgData; //2122
    private int _colData; //2132
    private int _mpyL; //2134
    private int _mpyM; //2135
    private int _mpyH; //2136
    private int _slhv; //2137
    private int _oamDataRead; //2138
    private int _vmDataLowRead; //2139
    private int _vmDataHighRead; //213A
    private int _cgDataRead; //213B
    private int _ophct; //213C
    private int _opvct; //213D
    private int _stat77; //213E
    private int _stat78; //213F

    private int[] _bgMapbase = [0, 0, 0, 0];
    private int[] _bgTilebase = [0, 0, 0, 0];
    private int[] _bgScrollX = [0, 0, 0, 0];
    private int[] _bgScrollY = [0, 0, 0, 0];
    private int[] _bgSizeX = [255, 255, 255, 255];
    private int[] _bgSizeY = [255, 255, 255, 255];
    private bool[] _colorMath = new bool[8];
    private bool[] _win1Enabled = new bool[6];
    private bool[] _win1Inverted = new bool[6];
    private bool[] _win2Enabled = new bool[6];
    private bool[] _win2Inverted = new bool[6];
    private int[] _winLogic = [0, 0, 0, 0, 0, 0];
    private bool[] _mainBgs = new bool[5];
    private bool[] _subBgs = new bool[5];
    private bool[] _winMainBgs = new bool[5];
    private bool[] _winSubBgs = new bool[5];
    private bool[] _mosaicEnabled = new bool[4];
    private bool[] _mode7Settings = new bool[4];
    private bool[] _bgCharSize = new bool[4];

    private ushort[] _vram;
    private ushort[] _cram;
    private byte[] _oam;
    public uint[] ScreenBuffer { get; set; }
    #endregion

    private int _cgBuffer;

    private GfxColor Main = new();
    private GfxColor Sub = new();
    private GfxColor Backdrop = new();

    public int MulDivResult { get => field & 0xffff; private set => field = value & 0xffff; }
    public int MulDivRemainder { get => field & 0xffff; private set => field = value & 0xffff; }
    private int Overscan { get => _overscanMode ? 240 : 225; }
    private bool GetHIrq { get => (_nmiTimEn & 0x10) != 0; }
    private bool GetVIrq { get => (_nmiTimEn & 0x20) != 0; }
    private int GetHTime { get => (_hTimeLow | _hTimeHigh << 8) & 0xffff; }
    private int GetVTime { get => (_vTimeLow | _vTimeHigh << 8) & 0xffff; }

    private bool _dramRefresh;

    private GfxColor Tranparent;

    private Snes Snes;

    public int GetScanline() => VPos;
    public void ProcessHdma() => Dma.HandleHdma();
    private void InitHdma() => Dma.InitHdma();

    public void Division(int v)
    {
        if (v != 0)
        {
            MulDivResult = (ushort)(_dividend / v);
            MulDivRemainder = (ushort)(_dividend % v);
        }
        else
        {
            MulDivResult = 0xffff;
            MulDivRemainder = (ushort)_dividend;
        }
    }

    private byte _oamLatch;
    private byte _mode7Latch;
    private readonly SpriteData[] _spriteScan;
    private GfxColor[] MBgs = [new(), new(), new(), new(), new()];
    private GfxColor[] SBgs = [new(), new(), new(), new(), new()];
    private bool[] _windowState = new bool[6];
    private GfxColor Fixed;

    public Action SetNmi;
    public Action SetIRQ;
    public Func<int> ApuStep;
    public Action AutoJoyRead;
    public Action<int, byte> SetDma;
    private SnesCpu Cpu;
    private SnesApu Apu;
    private SnesDma Dma;
    private readonly FrozenDictionary<int, int[][]> _layers;
    public SnesPpu()
    {
        ScreenBuffer = new uint[SnesWidth * SnesHeight];
        _spriteScan = new SpriteData[32];
        for (int i = 0; i < _spriteScan.Length; i++)
            _spriteScan[i] = new();

        _vram = new ushort[0x8000];
        _cram = new ushort[0x100];
        _oam = new byte[0x220];
        _layers = DictLayers.ToFrozenDictionary();
    }

    public void SetSnes(Snes snes)
    {
        Snes = snes;
        Cpu = snes.Cpu;
        Apu = snes.Apu;
        Dma = snes.Dma;
    }

    public void SetJoy1L(int v) => _joy1L = v & 0xff;
    public void SetJoy1H(int v) => _joy1H = v & 0xff;

    public void Step(int c)
    {
        for (int i = 0; i < c / 2; i++)
        {
            Snes?.Sa1?.Step();

            Cycles += 2;

            if (_autoJoyCounter >= 0)
                _autoJoyCounter--;
            else
                _hvbJoy &= 0xfe;

            if (VPos == 0 && HPos == 0)
                InitHdma();

            if (HPos < 4 || HPos >= 1096)
            {
                _hblank = true;
                _hvbJoy |= 0x40;
            }
            //else
            //    _hvbJoy &= 0x3f;

            if (HPos == 1104)
            {
                if (!_vblank)
                    ProcessHdma();
            }

            if (GetHIrq && !GetVIrq)
            {
                if (HPos == GetHTime * 6)
                {
                    SetIRQ();
                    _timeUp = 0x80;
                }
            }
            if (GetVIrq && VPos == GetVTime)
            {
                if (HPos == 0 || HPos == GetHTime * 6)
                {
                    SetIRQ();
                    _timeUp = 0x80;
                }
            }

            if (VPos < Overscan)
            {
                if (HPos == 0)
                {
                    _hvbJoy &= 0x3f;
                    _hblank = false;
                    _oamAddr = _interOamAddr;
                }

                if (VPos != 0 && VPos < Overscan && HPos == 512)
                {
                    if (!_forcedBlank)
                        EvaluateSprites(VPos);

                    if (!Snes.FastForward || Snes.FastForward && FrameCounter % Snes.Config.FrameSkip == 0)
                    {
                            Render(VPos);
                    }
                }
            }
            else
            {
                if (VPos == Overscan && HPos == 0)
                {
                    Apu.Step();
                    _hblank = true;
                    _vblank = true;
                    _hvbJoy |= 0xc0;
                    _rdNmi |= 0x80;
                    if ((_nmiTimEn & 0x80) != 0)
                        SetNmi();

                    if ((_nmiTimEn & 0x01) != 0)
                    {
                        AutoJoyRead();
                        _hvbJoy |= 0x41;
                        _autoJoyCounter = 1056;
                    }
                }
            }

            HPos += 2;
            if (!_dramRefresh && HPos == 536)
            {
                Cycles += 40;
                HPos += 40;
                _dramRefresh = true;
            }

            if (HPos >= 1364)
            {
                _hblank = false;
                _dramRefresh = false;
                _hvbJoy &= ~0x40;
                HPos = 0;
                VPos++;
                if (VPos >= 262)
                {
                    VPos = 0;
                    _vblank = false;
                    _rdNmi &= 0x7f;
                    FrameReady = true;
                    FrameCounter++;
                    if (!_forcedBlank)
                        EvaluateSprites(0);
                }
            }
        }
    }

    public void Render(int y)
    {
        Sub = new(0, 0, 5);
        Span<int> bpp = new(DictLayers[_bgMode][_bgMode == 1 || _bgMode == 7 ? 4 : 2]);
        int mapaddr = 0, sx = 0, sy = 0, half = 0;
        uint rgb = 0;
        bool main, sub, math = false;

        for (int x = 0; x < 256; x++)
        {
            if (!_forcedBlank)
            {
                _windowState[0] = GetWindow(0, x);
                _windowState[1] = GetWindow(1, x);
                _windowState[2] = GetWindow(2, x);
                _windowState[3] = GetWindow(3, x);
                _windowState[4] = GetWindow(4, x);
                _windowState[5] = GetWindow(5, x);

                if (_bgMode < 7)
                    RenderMode(mapaddr, x, y, bpp);
                else
                    RenderMode7(mapaddr, x, y, sx, sy, bpp);


                main = _mainBgs[4] && !GetWindow(4, x);
                sub = _subBgs[4] && !GetWindow(4, x);

                MBgs[4].Color = 0; SBgs[4].Color = 0;
                MBgs[4].Priority = 0; SBgs[4].Priority = 0;

                RenderSprites(x, y, main, sub);

                bool clip = _clip switch
                {
                    1 => !_windowState[5],
                    2 or 3 => _windowState[5],
                    _ => false
                };

                Main = GetPriority(_bgMode, MBgs);

                if (clip)
                    Main.Color = 0;

                half = _colorMath[6] ? 1 : 0;
                math = _bgMode != 7 && GetMathEnabled(Main.Layer, x);

                if (!_colorMath[7] && _addSub)
                {
                    Sub = GetPriority(_bgMode, SBgs);
                    if (Sub.Layer == 5 && _bgMode != 7)
                    {
                        Sub.Color = Fixed.Color;
                        half = 0;
                    }
                }
                else
                    Sub.Color = Fixed.Color;

                float brightness = (float)(_brightness / 15f);
                int mr = Main.Color & 0x1f;
                int mg = (Main.Color >> 5) & 0x1f;
                int mb = (Main.Color >> 10) & 0x1f;

                int red = mr, green = mg, blue = mb;

                if (math)
                {
                    int sr = Sub.Color & 0x1f;
                    int sg = (Sub.Color >> 5) & 0x1f;
                    int sb = (Sub.Color >> 10) & 0x1f;
                    if (!_colorMath[7])
                    {
                        red += sr; green += sg; blue += sb;
                        red = (red > 0x1f ? 0x1f : red) >> half;
                        green = (green > 0x1f ? 0x1f : green) >> half;
                        blue = (blue > 0x1f ? 0x1f : blue) >> half;
                    }
                    else
                    {
                        red -= sr; green -= sg; blue -= sb;
                        red = (red < 0 ? 0 : red) >> half;
                        green = (green < 0 ? 0 : green) >> half;
                        blue = (blue < 0 ? 0 : blue) >> half;
                    }
                }

                red = (int)(red * brightness); green = (int)(green * brightness); blue = (int)(blue * brightness);
                rgb = (uint)((red << 3 | red >> 2) & 0xff | ((green << 3 | green >> 2) & 0xff) << 8 | ((blue << 3 | blue >> 2) & 0xff) << 16);
            }
            else
                rgb = 0;
            ScreenBuffer[VPos * 256 + x] = 0xff000000 | rgb;
        }
    }

    private void RenderMode(int mapaddr, int x, int y, Span<int> bpp)
    {
        int mx = 0, my = 0;
        for (int i = 0; i < bpp.Length; i++)
        {
            int paloff = _bgMode == 0 ? i * 32 : 0;

            if (_bgMode < 7)
            {
                if (_mosaicEnabled[i])
                {
                    mx = _mosaicSize != 0 ? (x % _mosaicSize) : 0;
                    my = _mosaicSize != 0 ? (y % _mosaicSize) : 0;
                }

                int sx = x - mx + _bgScrollX[i];
                int sy = y - my + _bgScrollY[i];
                bool main = _mainBgs[i];
                bool sub = _subBgs[i];

                MBgs[i].Color = 0; MBgs[i].Priority = 0;
                SBgs[i].Color = 0; SBgs[i].Priority = 0;

                if (!main & !sub)
                    continue;

                if (_bgMode == 2)
                {
                    if (x > 7)
                    {
                        int h = GetMode2Tile(_bgScrollX[2] + (x - 8) & 0xf8, _bgScrollY[2], _bgMapbase[2]);
                        int v = GetMode2Tile(_bgScrollX[2] + (x - 8) & 0xf8, _bgScrollY[2] + 8, _bgMapbase[2]);
                        var bit = i == 0 ? 13 : 14;
                        if ((h & (1 << bit)) != 0)
                            sx = (sx & 7) + (x & ~7) + h + _bgScrollX[i] & 0x1fff;
                        if ((v & (1 << bit)) != 0)
                            sy += v & 0x1fff;
                    }

                    mapaddr = _bgMapbase[i] + (sy & 0xff) / 8 * 32 + (sx & 0xff) / 8;
                    mapaddr += _bgSizeX[i] > 0xff ? (sx & 0x100) * 4 : 0;
                    mapaddr += (_bgSizeY[i] > 0xff ? ((sy & 0x100) * 8) : 0) & 0x7fff;
                }
                else
                {
                    if (_bgCharSize[i])
                        mapaddr = _bgMapbase[i] + sy / 2 / 8 * 32 + (sx / 2 / 8);
                    else
                    {
                        sx &= _bgSizeX[i];
                        sy &= _bgSizeY[i];
                        mapaddr = _bgMapbase[i] + (sy & 0xff) / 8 * 32 + (sx & 0xff) / 8;
                        mapaddr += (sx & 0x100) * 4;
                    }
                    mapaddr += (_bgSizeY[i] == 0x1ff) && (sy & 0x100) != 0 ? _bgSizeX[i] == 0x1ff ? 0x800 : 0x400 : 0;
                    mapaddr &= 0x7fff;
                }

                (var color, var pixel, var pal) = GetColor(sx, sy, mapaddr, _bgTilebase[i], _bgCharSize[i], bpp[i], paloff);
                if (main && pixel != 0)
                {
                    MBgs[i].Color = color;
                    MBgs[i].Palette = pal;
                    MBgs[i].Priority = (_vram[mapaddr] >> 13) & 1;
                    MBgs[i].Layer = i;

                    if (_winMainBgs[i] && GetWindow(i, x))
                        MBgs[i].Color = 0;
                }

                if (sub && pixel != 0)
                {
                    SBgs[i].Color = color;
                    SBgs[i].Palette = pal;
                    SBgs[i].Priority = (_vram[mapaddr] >> 13) & 1;
                    SBgs[i].Layer = i;

                    if (_subBgs[i] && GetWindow(i, x))
                        SBgs[i].Color = 0;
                }
            }
        }
    }

    private void RenderMode7(int mapaddr, int x, int y, int sx, int sy, Span<int> bpp)
    {
        for (int i = 0; i < bpp.Length; i++)
        {


            //int rx = Mode7Settings[1] ? 255 - x : x;
            int ry = _mode7Settings[1] ? 255 - y : y;
            var cx = _scrollXMode7 - _m7X;
            var cy = _scrollYMode7 - _m7Y;
            int ch = (cx & 0x2000) != 0 ? cx | ~0x3ff : cx & 0x3ff;
            int cv = (cy & 0x2000) != 0 ? cy | ~0x3ff : cy & 0x3ff;
            sx = ((short)_m7A * ch & ~63) + (((short)_m7B * cv) & ~63) + ((short)_m7B * ry & ~63) + (_m7X << 8);
            sy = (((short)_m7C * ch) & ~63) + (((short)_m7D * cv) & ~63) + ((short)_m7D * ry & ~63) + (_m7Y << 8);
            var ox = sx + (short)_m7A * x;
            var oy = sy + (short)_m7C * x;
            ox >>= 8;// & 0x3ff;
            oy >>= 8;// & 0x3ff;

            if (_mode7Settings[3] && (ox < 0 || oy < 0 || ox >= 1024 || oy >= 1024))
            {
                ox &= 7;
                oy &= 7;
            }

            if (!_mode7Settings[3])
                mapaddr = ((oy >> 3) * 128 + (ox >> 3)) & 0x7fff;
            else
                mapaddr = ((oy >> 3) * 128 + (ox >> 3)) & 0x7fff;

            (var color, _, _) = GetColor(ox, oy, mapaddr, _bgTilebase[i], _bgCharSize[i], bpp[i], 0);

            MBgs[i].Color = color;
        }
    }

    private void RenderSprites(int x, int y, bool main, bool sub)
    {
        if (main || sub)
        {
            for (int i = 0; i < _spritesScanline; i++)
            {
                var s = _spriteScan[i];
                if (s.Y == 224) continue;
                if (s.X == -256 || s.X > 256) continue;
                int fx = x - s.X;
                int fy = y - s.Y;

                if (fx < 0 || fx >= s.Width) continue;

                if ((s.Attrib & 0x40) != 0)
                    fx = s.Width - fx - 1;

                if ((s.Attrib & 0x80) != 0)
                    fy = s.Height - fy - 1;

                int baseaddr = _objTable1 + ((s.Attrib & 1) != 0 ? _objTable2 : 0);
                int spraddr = baseaddr + (s.Tile + (fx / 8)) * 16 + (fy & 7) + (fy & 0xff) / 8 * s.Width * s.Height;
                int colorid = GetPixel(spraddr, 7 - fx & 7, 4);
                int palid = (s.Attrib & 0x0e) >> 1;
                int pal = (0x80 + palid * 16 + colorid) & 0xff;
                int color = _cram[pal];
                if (colorid != 0)
                {
                    if (_winMainBgs[4] && GetWindow(4, x))
                        continue;

                    if (main)
                    {
                        MBgs[4].Color = color | 1;
                        MBgs[4].Palette = pal;
                        MBgs[4].Layer = 4;
                        MBgs[4].Priority = s.Priority;
                    }

                    if (sub)
                    {
                        SBgs[4].Color = color | 1;
                        SBgs[4].Palette = pal;
                        SBgs[4].Layer = 4;
                        SBgs[4].Priority = s.Priority;
                    }
                    break;
                }
            }
        }
    }

    public static uint GetRGB555(ushort p, ushort s, int br, bool math, bool add, int half)
    {
        var brightness = br / 15f;
        var mr = p & 0x1f;
        var mg = (p >> 5) & 0x1f;
        var mb = (p >> 10) & 0x1f;

        int r = mr, g = mg, b = mb;

        if (math)
        {
            var sr = s & 0x1f;
            var sg = (s >> 5) & 0x1f;
            var sb = (s >> 10) & 0x1f;
            if (add)
            {
                r += sr; g += sg; b += sb;
                r = (r > 0x1f ? 0x1f : r) >> half;
                g = (g > 0x1f ? 0x1f : g) >> half;
                b = (b > 0x1f ? 0x1f : b) >> half;
            }
            else
            {
                r -= sr; g -= sg; b -= sb;
                r = (r < 0 ? 0 : r) >> half;
                g = (g < 0 ? 0 : g) >> half;
                b = (b < 0 ? 0 : b) >> half;
            }
        }

        r = (int)(r * brightness); g = (int)(g * brightness); b = (int)(b * brightness);
        var rgb = (uint)((byte)(r << 3 | r >> 2) | (byte)(g << 3 | g >> 2) << 8 | (byte)(b << 3 | b >> 2) << 16);
        return rgb | 0xff000000;
    }

    private bool GetWindow(int i, int x)
    {
        if (!_win1Enabled[i] && !_win2Enabled[i]) return false;

        bool w1 = x >= _w1Left && x <= _w1Right;
        bool w2 = x >= _w2Left && x <= _w2Right;

        if (_win1Enabled[i] && !_win2Enabled[i])
            return _win1Inverted[i] ? !w1 : w1;
        if (!_win1Enabled[i] && _win2Enabled[i])
            return _win2Inverted[i] ? !w2 : w2;

        if (_win1Inverted[i]) w1 = !w1;
        if (_win2Inverted[i]) w2 = !w2;

        return _winLogic[i] switch
        {
            0 => w1 || w2,
            1 => w1 && w2,
            2 => w1 != w2,
            3 => w1 == w2,
            _ => false
        };
    }

    private bool GetMathEnabled(int i, int x)
    {
        bool prev = false;
        if (_prevent == 1)
            prev = !_windowState[5];
        else if (_prevent == 2 || _prevent == 3)
            prev = _windowState[5];

        if (prev)
            return false;

        bool colorMathEnabled = _colorMath[i];
        int mainLayer = Main.Layer;
        int mainPalette = Main.Palette;

        if (!colorMathEnabled)
            return false;

        return mainLayer != 4 || mainPalette >= 0xc0;
    }

    private GfxColor GetPriority(int mode, GfxColor[] Colors)
    {
        int[][] layerArr = _layers[mode];
        int[] layer0 = layerArr[0];
        int[] layer1 = layerArr[1];

        switch (mode)
        {
            case 0 or 2 or 3 or 4 or 5 or 6:
                for (int i = 0, len = layer0.Length; i < len; i++)
                {
                    int l = layer0[i];
                    int p = layer1[i];
                    if (Colors[l].Priority == p && (Colors[l].Color & 1) != 0)
                        return Colors[l];
                }
                break;

            case 1:
            {
                int n = _mode1Bg3Priority ? 1 : 0;
                int[] layerN0 = layerArr[n + 0];
                int[] layerN2 = layerArr[n + 2];
                for (int i = 0, len = layerN0.Length; i < len; i++)
                {
                    int l = layerN0[i];
                    int p = layerN2[i];
                    if (l == 2 && n == 1 && Colors[l].Priority == p && (Colors[l].Color & 1) != 0)
                        return Colors[2];
                    if (Colors[l].Priority == p && (Colors[l].Color & 1) != 0)
                        return Colors[l];
                    if (l == 2 && n == 0 && Colors[l].Priority == p && (Colors[l].Color & 1) != 0)
                        return Colors[2];
                }
            }
            break;

            case 7:
            {
                int n = _extBgMode ? 1 : 0;
                int[] layerN0 = layerArr[n + 0];
                int[] layerN2 = layerArr[n + 2];
                for (int i = 0, len = layerN0.Length; i < len; i++)
                {
                    int l = layerN0[i];
                    int p = layerN2[i];
                    if (Colors[l].Priority == p && (Colors[l].Color & 1) != 0)
                        return Colors[l];
                }
            }
            break;
        }
        Backdrop.Color = _cram[0];
        Backdrop.Priority = 0;
        Backdrop.Layer = 5;
        return Backdrop;
    }

    private (int, int, int) GetColor(int sx, int sy, int mapaddr, int tilebase, bool bigchar, int bpp, int paloff)
    {
        int pixel = 0;
        int p;
        ushort vramVal = _vram[mapaddr];

        if (_bgMode < 7)
        {
            bool flipx = ((vramVal >> 14) & 1) != 0;
            bool flipy = ((vramVal >> 15) & 1) != 0;
            int fx = flipx ? (7 - ((sx ^ 7) & 7)) : (7 - (sx & 7));
            int fy = flipy ? ((sy ^ 7) & 7) : (sy & 7);
            int tileid = vramVal & 0x3ff;

            if (bigchar)
            {
                int sx8 = sx & 8;
                int sy8 = sy & 8;
                tileid += (sx8 != 0) ? (flipx ? 0 : 1) : (flipx ? 1 : 0);
                tileid += (sy8 != 0) ? (flipy ? 0 : 16) : (flipy ? 16 : 0);
            }

            int palid = (vramVal >> 10) & 7;
            int ta = tilebase + tileid * bpp * 4 + fy;
            pixel = GetPixel(ta, fx, bpp);

            int paletteSize = bpp switch { 4 => 16, 8 => 256, _ => 4 };
            p = paloff + palid * paletteSize + pixel;
            ushort cramVal = _cram[p & 0xff];
            return (cramVal | (pixel != 0 ? 1 : 0), pixel, p);
        }
        else
        {
            int tileid = vramVal & 0xff;
            int ta = (tileid * 64 + ((sy & 7) * 8) + (sx & 7)) & 0x3fff;
            p = _vram[ta] >> 8;
            ushort cramVal = _cram[p & 0xff];
            return (cramVal | 1, pixel, p);
        }
    }

    private int GetMode2Tile(int x, int y, int mapaddr) => _vram[(mapaddr + y / 8 * 32 + x / 8) & 0x7fff];

    private void EvaluateSprites(int y)
    {
        int c = _spritesScanline = 0;
        int n = _objPrioRotation ? (_interOamAddr & 0x1fc) / 4 : 0;
        for (int i = 0; i < 128; i++)
        {
            if (c > 31)
                break;

            var v = _oam[0x200 + n / 4];
            var t = v >> ((n & 3) << 1) & 3;
            var highbit = t & 1;
            int sy = _oam[n * 4 + 1];
            int yp = y - sy - 1;
            int width = _objSizeWidth[((_objSize | t) / 2) << 3 & 0xf];
            int height = _objSizeHeight[((_objSize | t) / 2) << 3 & 0xf];

            if (yp >= 0 && yp < height || sy + height > 255 && y < ((sy + height) & 0xff))
            {
                _spriteScan[c].X = highbit * -256 + _oam[n * 4 + 0];
                _spriteScan[c].Y = _oam[n * 4 + 1] + 1;
                _spriteScan[c].Tile = _oam[n * 4 + 2];
                _spriteScan[c].Attrib = _oam[n * 4 + 3];
                _spriteScan[c].Priority = (_oam[n * 4 + 3] >> 4) & 3;
                _spriteScan[c].Width = width;
                _spriteScan[c].Height = height;
                _spriteScan[c].Id = n;
                c++;
            }
            n = (n + 1) & 0x7f;
        }
        _spritesScanline = c;
    }

    private int GetPixel(int ta, int fx, int bpp)
    {
        int idx0 = ta & 0x7fff;
        switch (bpp)
        {
            case 2:
            {
                ushort b0 = _vram[idx0];
                int bit0 = (b0 >> fx) & 1;
                int bit1 = (b0 >> (8 + fx)) & 1;
                return bit0 | (bit1 << 1);
            }
            case 4:
            {
                ushort b0 = _vram[idx0];
                ushort b1 = _vram[(ta + 8) & 0x7fff];
                int bit0 = (b0 >> fx) & 1;
                int bit1 = (b0 >> (8 + fx)) & 1;
                int bit2 = (b1 >> fx) & 1;
                int bit3 = (b1 >> (8 + fx)) & 1;
                return bit0 | (bit1 << 1) | (bit2 << 2) | (bit3 << 3);
            }
            case 8:
            {
                ushort b0 = _vram[idx0];
                ushort b1 = _vram[(ta + 0x08) & 0x7fff];
                ushort b2 = _vram[(ta + 0x10) & 0x7fff];
                ushort b3 = _vram[(ta + 0x18) & 0x7fff];
                int bit0 = (b0 >> fx) & 1;
                int bit1 = (b0 >> (8 + fx)) & 1;
                int bit2 = (b1 >> fx) & 1;
                int bit3 = (b1 >> (8 + fx)) & 1;
                int bit4 = (b2 >> fx) & 1;
                int bit5 = (b2 >> (8 + fx)) & 1;
                int bit6 = (b3 >> fx) & 1;
                int bit7 = (b3 >> (8 + fx)) & 1;
                return bit0 | (bit1 << 1) | (bit2 << 2) | (bit3 << 3)
                     | (bit4 << 4) | (bit5 << 5) | (bit6 << 6) | (bit7 << 7);
            }
        }
        return 0;
    }

    public int ReadByte(int a)
    {
        int addr = a >> 1;
        if ((a & 1) == 0)
            return _vram[addr & 0x7fff] & 0xff;
        else
            return _vram[addr & 0x7fff] >> 8 & 0xff;
    }

    public void WriteByte(int a, int v)
    {
        int addr = a >> 1;
        if ((a & 1) == 0)
            _vram[addr] = (ushort)((_vram[addr] & 0xff00) | v);
        else
            _vram[addr] = (ushort)((_vram[addr] & 0x00ff) | v << 8);
    }

    private int GetVramRemap()
    {
        var a = _vramAddr & 0x7fff;
        return _vramAddrRemap switch
        {
            1 => (a & 0xff00) | (a & 0xe0) >> 5 | (a & 0x1f) << 3,
            2 => (a & 0xfe00) | (a & 0x1c0) >> 6 | (a & 0x3f) << 3,
            3 => (a & 0xfc00) | (a & 0x380) >> 7 | (a & 0x7f) << 3,
            _ => a,
        };
    }

    public int ReadVram(int a)
    {
        if ((a & 1) == 0)
            return _vram[a >> 1] & 0x00ff;
        else
            return _vram[a >> 1] >> 8 & 0xff;
    }

    public void WriteVram(int a, int v)
    {
        if ((a & 1) == 0)
        {
            var s = _vram[a >> 1];
            _vram[a >> 1] = (ushort)(s & 0xff00 | v);
        }
        else
        {
            var s = _vram[a >> 1];
            _vram[a >> 1] = (ushort)(s & 0x00ff | v << 8);
        }
    }

    public int ReadOram(int a) => _oam[a];
    public void WriteOram(int a, int v) => _oam[a] = (byte)v;

    public int ReadCram(int a)
    {
        if ((a & 1) == 0)
        {
            return _cram[a >> 1] & 0x00ff;
        }
        else
        {
            return _cram[a >> 1] >> 8 & 0xff;
        }
    }

    public void WriteCram(int a, int v)
    {
        if ((a & 1) == 0)
        {
            _cram[a & 0xff] = (ushort)v;
        }
        else
        {
            var s = _cram[a >> 1];
            _cram[a >> 1] = (ushort)(s & 0x00ff | v << 8);
        }
    }

    public void Reset()
    {
        VPos = 0;
        HPos = 0;
        Cycles = 0;
        _vram = new ushort[0x8000];
        _cram = new ushort[0x100];
        _oam = new byte[0x220];
        _vramAddr = 0;
        _vramLatch = 0;
        _cgData = 0;
        _hTimeHigh = 0x01; _hTimeLow = 0xff;
        _vTimeHigh = 0x01; _vTimeLow = 0xff;
        _dividend = 0xffff;
        _multiplyA = 0xff; _multiplyB = 0xff;
        _wrIo = 0xff;
        _hvbJoy = 0x02;
        _dramRefresh = false;
        Array.Fill(ScreenBuffer, 0xff000000);
        MBgs = [new(), new(), new(), new(), new(), new()];
        SBgs = [new(), new(), new(), new(), new(), new()];
        Tranparent = new(0, 0, 5);
    }

    private readonly Dictionary<int, int[][]> DictLayers = new()
    {
        [0] = [[4, 0, 1, 4, 0, 1, 4, 2, 4, 4, 2, 4],
               [3, 1, 1, 2, 0, 0, 1, 1, 1, 0, 0, 0],
               [2, 2, 2, 2]],
        [1] = [[4, 0, 1, 4, 0, 1, 4, 2, 4, 2], [2, 4, 0, 1, 4, 0, 1, 4, 4, 2],
               [3, 1, 1, 2, 0, 0, 1, 1, 0, 0], [1, 3, 1, 1, 2, 0, 0, 1, 0, 0],
               [4, 4, 2]],
        [2] = [[4, 0, 4, 1, 4, 0, 4, 1],
               [3, 1, 2, 1, 1, 0, 0, 0],
               [4, 4]],
        [3] = [[4, 0, 4, 1, 4, 0, 4, 1],
               [3, 1, 2, 1, 1, 0, 0, 0],
               [8, 4]],
        [4] = [[4, 0, 4, 1, 4, 0, 4, 1],
               [3, 1, 2, 1, 1, 0, 0, 0],
               [8, 2]],
        [5] = [[4, 0, 4, 1, 4, 0, 4, 1],
               [3, 1, 2, 1, 1, 0, 0, 0],
               [4, 2]],
        [6] = [[4, 0, 4, 4, 0, 4],
               [3, 1, 2, 1, 0, 0],
               [4]],
        [7] = [[4, 4, 4, 0, 4], [4, 4, 1, 4, 0, 4, 1],
               [3, 2, 1, 0, 0], [3, 2, 1, 1, 0, 0, 0],
               [8]],
    };
    private readonly bool _himeh;

    public struct SpriteData
    {
        public int X;
        public int Y;
        public int Tile;
        public int Attrib;
        public int Priority;
        public int Width;
        public int Height;
        public int Id;

        public SpriteData() { }
    }

    private struct GfxColor(int color, int priority, int layer)
    {
        public int Color = color;
        public int Palette = 0;
        public int Priority = priority;
        public int Layer = layer;
    }

    public List<RegisterInfo> GetState() =>
    [
        new("","HClock",$"{HPos}"),
        new("","Scanline", $"{VPos}"),
        new("4200.4","HIrq", $"{GetHIrq}"),
        new("4200.5","VIrq", $"{GetVIrq}"),
        new("4207/8","HTIME", $"{GetHTime:X4}"),
        new("4209/A","VTIME", $"{GetVTime:X4}"),
        new("4212","HVBJOY", $"{_hvbJoy:X2}"),
        new("2105","BGMode", $"{_bgMode:X2}"),
        new("2100","Brightness", $"{_brightness:X2}"),
        new("2132","Fixed Color", $"{Fixed.Color:X4}"),
        new("4216/7","Remainder", $"{MulDivRemainder:X4}"),
        new("2101|0-2","Oam Table",$"{_objTable1:X4}"),
        new("2101|3-5","Oam Table 2",$"{_objTable2:X4}"),
        new("","ObjAddr", $"{_oamAddr:X4}"),
        new("2102/3","ObjPrioIndex", $"{_objPrioIndex:X4}"),
        new("2103.7","ObjPrioRotation", $"{_objPrioRotation}"),
        new("2115-7","Vram Addr", $"{(_vramAddr * 2)&0xffff:X4}"),
        new("2126","W1 Left", $"{_w1Left:X2}"),
        new("2127","W1 Right", $"{_w1Right:X2}"),
        new("2128","W2 Left", $"{_w2Left:X2}"),
        new("2129","W2 Right", $"{_w2Right:X2}"),
        new("211B-20","Mode 7",""),
        new("211B","M7A", $"{_m7A:X4}"),
        new("211C","M7B", $"{_m7B:X4}"),
        new("211D","M7C", $"{_m7C:X4}"),
        new("211E","M7D", $"{_m7D:X4}"),
        new("211F","M7X", $"{_m7X:X4}"),
        new("2120","M7Y", $"{_m7Y:X4}"),
        new("2107-0A","Tilemaps",""),
        new("07|0-1","BG1 Size", $"{_bgCharSize[0]:X2}"),
        new("07|2-6","BG1 Addr", $"{_bgMapbase[0]:X4}"),
        new("08|0-1","BG2 Size", $"{_bgCharSize[1]:X2}"),
        new("08|2-6","BG2 Addr", $"{_bgMapbase[1]:X4}"),
        new("09|0-1","BG3 Size", $"{_bgCharSize[2]:X2}"),
        new("09|2-6","BG3 Addr", $"{_bgMapbase[2]:X4}"),
        new("0A|0-1","BG4 Size", $"{_bgCharSize[3]:X2}"),
        new("0A|2-6","BG4 Addr", $"{_bgMapbase[3]:X4}"),
        new("210B-0C","Tiles",""),
        new("0B|0-2","BG1 Tile Addr", $"{_bgTilebase[0]:X4}"),
        new("0B|4-6","BG2 Tile Addr", $"{_bgTilebase[1]:X4}"),
        new("0C|0-2","BG3 Tile Addr", $"{_bgTilebase[2]:X4}"),
        new("0C|4-6","BG3 Tile Addr", $"{_bgTilebase[3]:X4}"),
        new("210D-2114","Scroll", ""),
        new("0D","BG1 X", $"{_bgScrollX[0]:X4}"),
        new("0E","BG1 Y", $"{_bgScrollY[0]:X4}"),
        new("0F","BG2 X", $"{_bgScrollX[1]:X4}"),
        new("10","BG2 Y", $"{_bgScrollY[1]:X4}"),
        new("11","BG3 X", $"{_bgScrollX[2]:X4}"),
        new("12","BG3 Y", $"{_bgScrollY[2]:X4}"),
        new("13","BG4 X", $"{_bgScrollX[3]:X4}"),
        new("14","BG4 Y", $"{_bgScrollY[3]:X4}"),
        new("212C","Main Layers", ""),
        new("|0","BG1 Enabled", $"{_mainBgs[0]}"),
        new("|1","BG2 Enabled", $"{_mainBgs[1]}"),
        new("|2","BG3 Enabled", $"{_mainBgs[2]}"),
        new("|3","BG4 Enabled", $"{_mainBgs[3]}"),
        new("|4","OAM Enabled", $"{_mainBgs[4]}"),
        new("212D","Sub Layers", ""),
        new("|0","BG1 Enabled", $"{_subBgs[0]}"),
        new("|1","BG2 Enabled", $"{_subBgs[1]}"),
        new("|2","BG3 Enabled", $"{_subBgs[2]}"),
        new("|3","BG4 Enabled", $"{_subBgs[3]}"),
        new("|4","OAM Enabled", $"{_subBgs[4]}"),
    ];

    public void Save(BinaryWriter bw)
    {
        bw.Write(VPos); bw.Write(HPos); bw.Write(Cycles); bw.Write(FrameCounter);
        bw.Write(_cgRamToggle); bw.Write(_prevScrollX); bw.Write(_currScrollX); bw.Write(FrameReady);
        bw.Write(_vblank); bw.Write(_hblank); bw.Write(_autoJoyCounter); bw.Write(_mosaicSize);
        bw.Write(_scrollXMode7); bw.Write(_scrollYMode7); bw.Write(_w1Left); bw.Write(_w1Right);
        bw.Write(_w2Left); bw.Write(_w2Right); bw.Write(_addSub); bw.Write(_dirColor);
        bw.Write(_prevent); bw.Write(_clip); bw.Write(_objTable1); bw.Write(_objTable2);
        bw.Write(_objSize); bw.Write(_objPrioRotation); bw.Write(_objPrioIndex); bw.Write(_oamAddr);
        bw.Write(_interOamAddr); bw.Write(_brightness); bw.Write(_forcedBlank); bw.Write(_bgMode);
        bw.Write(_mode1Bg3Priority); bw.Write(_ramAddrLow); bw.Write(_ramAddrMedium); bw.Write(_ramAddrHigh);
        bw.Write(_multiplyA); bw.Write(_multiplyB); bw.Write(_dividend); bw.Write(_divisor);
        bw.Write(_vramAddrIncrease); bw.Write(_vramAddrRemap); bw.Write(_vramAddrMode); bw.Write(_vramAddr);
        bw.Write(_vramLatch); bw.Write(_overscanMode); bw.Write(_hiResMode); bw.Write(_extBgMode);
        bw.Write(_m7A); bw.Write(_m7B); bw.Write(_m7C); bw.Write(_m7D);
        bw.Write(_m7X); bw.Write(_m7Y); bw.Write(_cgAdd); bw.Write(_cgData);
        bw.Write(_colData); bw.Write(_mpyL); bw.Write(_mpyM); bw.Write(_mpyH);
        bw.Write(_slhv); bw.Write(_oamDataRead); bw.Write(_vmDataLowRead); bw.Write(_vmDataHighRead);
        bw.Write(_cgDataRead); bw.Write(_ophct); bw.Write(_opvct); bw.Write(_stat77);
        bw.Write(_stat78); bw.Write(_nmiTimEn); bw.Write(_wrIo); bw.Write(_hTimeLow);
        bw.Write(_hTimeHigh); bw.Write(_vTimeLow); bw.Write(_vTimeHigh); bw.Write(_mdmaEn);
        bw.Write(_hdmaEn); bw.Write(_rdNmi); bw.Write(_timeUp); bw.Write(_hvbJoy);
        bw.Write(_rdIo); bw.Write(_joy1L); bw.Write(_joy1H); bw.Write(_joy2L);
        bw.Write(_joy2H); bw.Write(_joy3L); bw.Write(_joy3H); bw.Write(_joy4L);
        bw.Write(_joy4H); bw.Write(_counterLatch); bw.Write(_ophctLatch); bw.Write(_opvctLatch);
        bw.Write(_multiplyRes); WriteArray(bw, _bgMapbase);
        WriteArray(bw, _bgTilebase);
        WriteArray(bw, _bgScrollX);
        WriteArray(bw, _bgScrollY);
        WriteArray(bw, _bgSizeX);
        WriteArray(bw, _bgSizeY);
        WriteArray(bw, _colorMath);
        WriteArray(bw, _win1Enabled);
        WriteArray(bw, _win1Inverted);
        WriteArray(bw, _win2Enabled);
        WriteArray(bw, _win2Inverted);
        WriteArray(bw, _winLogic);
        WriteArray(bw, _mainBgs);
        WriteArray(bw, _subBgs);
        WriteArray(bw, _winMainBgs);
        WriteArray(bw, _winSubBgs);
        WriteArray(bw, _mosaicEnabled);
        WriteArray(bw, _mode7Settings);
        WriteArray(bw, _bgCharSize);
        WriteArray(bw, _vram);
        WriteArray(bw, _cram);
        WriteArray(bw, _oam);
        WriteArray(bw, ScreenBuffer);
    }

    public void Load(BinaryReader br)
    {
        VPos = br.ReadInt32(); HPos = br.ReadInt32(); Cycles = br.ReadUInt64(); FrameCounter = br.ReadUInt32();
        _cgRamToggle = br.ReadBoolean(); _prevScrollX = br.ReadInt32(); _currScrollX = br.ReadInt32(); FrameReady = br.ReadBoolean();
        _vblank = br.ReadBoolean(); _hblank = br.ReadBoolean(); _autoJoyCounter = br.ReadInt32(); _mosaicSize = br.ReadInt32();
        _scrollXMode7 = br.ReadInt32(); _scrollYMode7 = br.ReadInt32(); _w1Left = br.ReadInt32(); _w1Right = br.ReadInt32();
        _w2Left = br.ReadInt32(); _w2Right = br.ReadInt32(); _addSub = br.ReadBoolean(); _dirColor = br.ReadBoolean();
        _prevent = br.ReadInt32(); _clip = br.ReadInt32(); _objTable1 = br.ReadInt32(); _objTable2 = br.ReadInt32();
        _objSize = br.ReadInt32(); _objPrioRotation = br.ReadBoolean(); _objPrioIndex = br.ReadInt32(); _oamAddr = br.ReadInt32();
        _interOamAddr = br.ReadInt32(); _brightness = br.ReadInt32(); _forcedBlank = br.ReadBoolean(); _bgMode = br.ReadInt32();
        _mode1Bg3Priority = br.ReadBoolean(); _ramAddrLow = br.ReadInt32(); _ramAddrMedium = br.ReadInt32(); _ramAddrHigh = br.ReadInt32();
        _multiplyA = br.ReadInt32(); _multiplyB = br.ReadInt32(); _dividend = br.ReadInt32(); _divisor = br.ReadInt32();
        _vramAddrIncrease = br.ReadInt32(); _vramAddrRemap = br.ReadInt32(); _vramAddrMode = br.ReadBoolean(); _vramAddr = br.ReadInt32();
        _vramLatch = br.ReadInt32(); _overscanMode = br.ReadBoolean(); _hiResMode = br.ReadBoolean(); _extBgMode = br.ReadBoolean();
        _m7A = br.ReadInt32(); _m7B = br.ReadInt32(); _m7C = br.ReadInt32(); _m7D = br.ReadInt32();
        _m7X = br.ReadInt32(); _m7Y = br.ReadInt32(); _cgAdd = br.ReadInt32(); _cgData = br.ReadInt32();
        _colData = br.ReadInt32(); _mpyL = br.ReadInt32(); _mpyM = br.ReadInt32(); _mpyH = br.ReadInt32();
        _slhv = br.ReadInt32(); _oamDataRead = br.ReadInt32(); _vmDataLowRead = br.ReadInt32(); _vmDataHighRead = br.ReadInt32();
        _cgDataRead = br.ReadInt32(); _ophct = br.ReadInt32(); _opvct = br.ReadInt32(); _stat77 = br.ReadInt32();
        _stat78 = br.ReadInt32(); _nmiTimEn = br.ReadInt32(); _wrIo = br.ReadInt32(); _hTimeLow = br.ReadInt32();
        _hTimeHigh = br.ReadInt32(); _vTimeLow = br.ReadInt32(); _vTimeHigh = br.ReadInt32(); _mdmaEn = br.ReadInt32();
        _hdmaEn = br.ReadInt32(); _rdNmi = br.ReadInt32(); _timeUp = br.ReadInt32(); _hvbJoy = br.ReadInt32();
        _rdIo = br.ReadInt32(); _joy1L = br.ReadInt32(); _joy1H = br.ReadInt32(); _joy2L = br.ReadInt32();
        _joy2H = br.ReadInt32(); _joy3L = br.ReadInt32(); _joy3H = br.ReadInt32(); _joy4L = br.ReadInt32();
        _joy4H = br.ReadInt32(); _counterLatch = br.ReadBoolean(); _ophctLatch = br.ReadBoolean(); _opvctLatch = br.ReadBoolean();
        _multiplyRes = br.ReadInt32(); _bgMapbase = ReadArray<int>(br, _bgMapbase.Length);
        _bgTilebase = ReadArray<int>(br, _bgTilebase.Length);
        _bgScrollX = ReadArray<int>(br, _bgScrollX.Length);
        _bgScrollY = ReadArray<int>(br, _bgScrollY.Length);
        _bgSizeX = ReadArray<int>(br, _bgSizeX.Length);
        _bgSizeY = ReadArray<int>(br, _bgSizeY.Length);
        _colorMath = ReadArray<bool>(br, _colorMath.Length);
        _win1Enabled = ReadArray<bool>(br, _win1Enabled.Length);
        _win1Inverted = ReadArray<bool>(br, _win1Inverted.Length);
        _win2Enabled = ReadArray<bool>(br, _win2Enabled.Length);
        _win2Inverted = ReadArray<bool>(br, _win2Inverted.Length);
        _winLogic = ReadArray<int>(br, _winLogic.Length);
        _mainBgs = ReadArray<bool>(br, _mainBgs.Length);
        _subBgs = ReadArray<bool>(br, _subBgs.Length);
        _winMainBgs = ReadArray<bool>(br, _winMainBgs.Length);
        _winSubBgs = ReadArray<bool>(br, _winSubBgs.Length);
        _mosaicEnabled = ReadArray<bool>(br, _mosaicEnabled.Length);
        _mode7Settings = ReadArray<bool>(br, _mode7Settings.Length);
        _bgCharSize = ReadArray<bool>(br, _bgCharSize.Length);
        _vram = ReadArray<ushort>(br, _vram.Length);
        _cram = ReadArray<ushort>(br, _cram.Length);
        _oam = ReadArray<byte>(br, _oam.Length);
        ScreenBuffer = ReadArray<uint>(br, ScreenBuffer.Length);
    }
}


