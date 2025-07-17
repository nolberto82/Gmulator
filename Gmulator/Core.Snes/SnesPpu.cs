using Raylib_cs;
using System;
using System.ComponentModel.Design;

namespace Gmulator.Core.Snes;
public class SnesPpu : EmuState
{
    private Snes Snes;

    public int VPos { get; private set; }
    public int HPos { get; private set; }
    public ulong Cycles { get; private set; }
    public uint FrameCounter { get; private set; }
    public bool CgRamToggle { get; private set; }
    private int PrevScrollX;
    private int CurrScrollX;
    public bool FrameReady { get; set; }
    public bool Vblank { get; private set; }
    public bool Hblank { get; private set; }
    public int AutoJoyCounter { get; private set; }
    public int MosaicSize { get; private set; }
    private int ScrollXMode7;
    private int ScrollYMode7;
    private int W1Left;
    private int W1Right;
    private int W2Left;
    private int W2Right;
    private bool AddSub;
    private bool DirColor;
    private int Prevent;
    private int Clip;
    private int SpritesScanline;
    private readonly int[] ObjSizeWidth = [8, 8, 8, 16, 16, 32, 16, 16, 16, 32, 64, 32, 64, 64, 32, 32];
    private readonly int[] ObjSizeHeight = [8, 8, 8, 16, 16, 32, 32, 32, 16, 32, 64, 32, 64, 64, 64, 32];
    public int ObjTable1 { get; private set; }
    public int ObjTable2 { get; private set; }
    public int ObjSize { get; private set; }
    public bool ObjPrioRotation { get; private set; }
    public int ObjPrioIndex { get; private set; }
    public int OamAddr { get; private set; }
    public int InterOamAddr { get; private set; }
    public int Brightness { get; private set; }
    public bool ForcedBlank { get; private set; }
    public int BgMode { get; private set; }
    public bool Mode1Bg3Prio { get; private set; }
    public int RamAddrLow { get; private set; }
    public int RamAddrMedium { get; private set; }
    public int RamAddrHigh { get; private set; }
    public int MultiplyA { get; set; }
    public int MultiplyB { get; set; }
    public int Dividend { get; set; }
    public int Divisor { get; set; }
    public int VramAddrInc { get; private set; }
    public int VramAddrRemap { get; private set; }
    public bool VramAddrMode { get; private set; }
    public int VramAddr { get; private set; }
    public int VramLatch { get; private set; }
    public bool OverscanMode { get; private set; }
    public bool HiResMode { get; private set; }
    public bool ExtBgMode { get; private set; }

    private int M7A; //211B
    private int M7B; //211C
    private int M7C; //211D
    private int M7D; //211E
    private int M7X; //211F
    private int M7Y; //2120
    private int CGADD; //2121
    private int CGDATA; //2122
    private int COLDATA; //2132
    private int MPYL; //2134
    private int MPYM; //2135
    private int MPYH; //2136
    private int SLHV; //2137
    private int OAMDATAREAD; //2138
    private int VMDATALREAD; //2139
    private int VMDATAHREAD; //213A
    private int CGDATAREAD; //213B
    private int OPHCT; //213C
    private int OPVCT; //213D
    private int STAT77; //213E
    private int STAT78; //213F

    private int NMITIMEN; ///4200
    private int WRIO; //4201
    private int HTIMEL; //4207
    private int HTIMEH; //4208
    private int VTIMEL; //4209
    private int VTIMEH; //420A
    private int MDMAEN; //420B
    private int HDMAEN; //420C
    private int RDNMI; //4210
    private int TIMEUP; //4211
    private int HVBJOY; //4212
    private int RDIO; //4213
    private int JOY1L; //4218
    private int JOY1H; //4219
    private int JOY2L; //421A
    private int JOY2H; //421B
    private int JOY3L; //421C
    private int JOY3H; //421D
    private int JOY4L; //421E
    private int JOY4H; //421F
    private bool CounterLatch;
    private bool OphctLatch;
    private bool OpvctLatch;
    private int MultiplyRes;

    private int[] BgMapbase = [0, 0, 0, 0];
    private int[] BgTilebase = [0, 0, 0, 0];
    private int[] BgScrollX = [0, 0, 0, 0];
    private int[] BgScrollY = [0, 0, 0, 0];
    private int[] BgSizeX = [255, 255, 255, 255];
    private int[] BgSizeY = [255, 255, 255, 255];
    public bool[] ColorMath { get; private set; } = new bool[8];
    public bool[] Win1Enabled { get; private set; } = new bool[6];
    public bool[] Win1Inverted { get; private set; } = new bool[6];
    public bool[] Win2Enabled { get; private set; } = new bool[6];
    public bool[] Win2Inverted { get; private set; } = new bool[6];
    public int[] WinLogic { get; private set; } = [0, 0, 0, 0, 0, 0];
    public bool[] MainBgs { get; private set; } = new bool[5];
    public bool[] SubBgs { get; private set; } = new bool[5];
    public bool[] WinMainBgs { get; private set; } = new bool[5];
    public bool[] WinSubBgs { get; private set; } = new bool[5];
    public bool[] MosaicEnabled { get; private set; } = new bool[4];
    public bool[] Mode7Settings { get; private set; } = new bool[4];
    public bool[] BgCharSize { get; private set; } = new bool[4];

    public ushort[] Vram { get; private set; }
    public ushort[] Cram { get; private set; }
    public byte[] Oam { get; private set; }
    private uint[] ScreenBuffer;

    private GfxColor Main = new();
    private GfxColor Sub = new();

    public int MulDivResult { get => (ushort)field; private set => field = (ushort)value; }
    public int MulDivRemainder { get => (ushort)field; set => field = (ushort)value; }
    private int Overscan { get => OverscanMode ? 240 : 225; }
    private bool GetHIrq { get => NMITIMEN.GetBit(4); }
    private bool GetVIrq { get => NMITIMEN.GetBit(5); }
    private ushort GetHTime { get => (ushort)(HTIMEL | HTIMEH << 8); }
    private ushort GetVTime { get => (ushort)(VTIMEL | VTIMEH << 8); }

    public bool DramRefresh { get; private set; }

    public void Division(int v)
    {
        if (v > 0)
        {
            MulDivResult = (ushort)(Dividend / v);
            MulDivRemainder = (ushort)(Dividend % v);
        }
        else
        {
            MulDivResult = 0xffff;
            MulDivRemainder = (ushort)Dividend;
        }
    }

    private byte OamLatch;
    private byte Mode7Latch;
    private readonly SpriteData[] SpriteScan;
    private GfxColor[] MBgs = [new(), new(), new(), new(), new()];
    private readonly GfxColor[] SBgs = [new(), new(), new(), new(), new()];
    private GfxColor Fixed;

    public Action SetNmi;
    public Action SetIRQ;
    public Func<int> ApuStep;
    public Action AutoJoyRead;
    public Action<int, byte> SetDma;

    public SnesPpu()
    {
        ScreenBuffer = new uint[SnesWidth * SnesHeight];
        SpriteScan = new SpriteData[32];
        for (int i = 0; i < SpriteScan.Length; i++)
            SpriteScan[i] = new();
    }

    public void SetSnes(Snes snes) => Snes = snes;
    public void SetJoy1L(int v) => JOY1L = (byte)v;
    public void SetJoy1H(int v) => JOY1H = (byte)v;

    public void Step(int c)
    {
        for (int i = 0; i < c / 2; i++)
        {
            Cycles += 2;

            if (AutoJoyCounter >= 0)
                AutoJoyCounter--;
            else
                HVBJOY &= 0xfe;

            if (VPos == 0 && HPos == 0)
                InitHdma();

            if (HPos < 4 || HPos >= 1096)
            {
                Hblank = true;
                HVBJOY |= 0x40;
            }
            else
                HVBJOY &= 0x3f;

            if (HPos == 1104)
            {
                if (!Vblank)
                    ProcessHdma();
            }

            if (GetHIrq && !GetVIrq)
            {
                if (HPos == GetHTime * 6)
                {
                    SetIRQ();
                    TIMEUP = 0x80;
                }
            }
            if (GetVIrq && VPos == GetVTime)
            {
                if (HPos == 0 || HPos == GetHTime * 6)
                {
                    SetIRQ();
                    TIMEUP = 0x80;
                }
            }

            if (VPos < Overscan)
            {
                if (HPos == 0)
                {
                    HVBJOY &= 0x3f;
                    Hblank = false;
                    OamAddr = InterOamAddr;
                }

                if (HPos == 1084)
                {
                    if (!ForcedBlank)
                    {
                        if (!Snes.FastForward || Snes.FastForward && FrameCounter % Snes.Config.FrameSkip == 0)
                        {
                            EvaluateSprites();
                            if (VPos > 0 && VPos < Overscan)
                                Render(VPos);
                        }
                    }
                }
            }
            else
            {
                if (VPos == Overscan && HPos == 0)
                {
                    Snes.Apu.Step();
                    Hblank = true;
                    Vblank = true;
                    HVBJOY |= 0xc0;
                    RDNMI |= 0x80;
                    if (NMITIMEN.GetBit(7))
                        SetNmi();

                    if (NMITIMEN.GetBit(0))
                    {
                        AutoJoyRead();
                        HVBJOY |= 0x41;
                        AutoJoyCounter = 1056;
                    }
                    Texture.Update(Snes.Screen.Texture, ScreenBuffer);
                }
            }

            HPos += 2;
            if (HPos >= 536 && !DramRefresh)
            {
                Cycles += 40;
                HPos += 40;
                DramRefresh = true;
            }

            if (HPos >= 1364)
            {
                Hblank = false;
                DramRefresh = false;
                HVBJOY = ~0x40;
                HPos = 0;
                VPos++;
                if (VPos >= 262)
                {
                    VPos = 0;
                    Vblank = false;
                    RDNMI &= 0x7f;
                    FrameReady = true;
                    FrameCounter++;
                }
            }
        }
    }

    public void ProcessHdma()
    {
        var Dma = Snes.Dma;
        int count = 0;
        for (int i = 0; i < Dma.Count; i++)
        {
            if (Dma[i].HdmaEnabled && !Dma[i].Completed)
            {
                if (Dma[i].TransferEnabled)
                {
                    if (Dma[i].Indirect)
                    {
                        while (count < Dma[i].Max[Dma[i].Mode & 7])
                        {
                            Dma[i].Transfer(Dma[i].HBank << 16 | Dma[i].Size, Dma[i].Mode, count++);
                            Dma[i].Size++;
                        }
                        count = 0;
                    }
                    else
                    {
                        while (count < Dma[i].Max[Dma[i].Mode & 7])
                        {
                            Dma[i].Transfer(Dma[i].ABank << 16 | Dma[i].HAddress, Dma[i].Mode, count++);
                            Dma[i].HAddress++;
                        }
                        count = 0;
                    }
                }

                Dma[i].HCounter--;
                Dma[i].TransferEnabled = Dma[i].HCounter.GetBit(7);
                if ((Dma[i].HCounter & 0x7f) == 0)
                {
                    var v = Dma[i].Read(Dma[i].ABank << 16 | Dma[i].HAddress++);
                    Dma[i].HCounter = v;
                    Dma[i].Repeat = v.GetBit(7);
                    var bank = Dma[i].ABank << 16;
                    if (Dma[i].Indirect)
                        Dma[i].Size = (ushort)(Dma[i].Read(bank | Dma[i].HAddress++) | Dma[i].Read(bank | Dma[i].HAddress++) << 8);

                    Dma[i].TransferEnabled = true;
                    if (Dma[i].HCounter == 0)
                        Dma[i].Completed = true;
                }
            }
        }
    }

    private void InitHdma()
    {
        var Dma = Snes.Dma;
        for (int i = 0; i < Dma.Count; i++)
        {
            if (Dma[i].HdmaEnabled)
            {
                Dma[i].Completed = false;
                Dma[i].HAddress = Dma[i].AAddress;
                var v = Dma[i].Read(Dma[i].ABank << 16 | Dma[i].HAddress++);
                Dma[i].HCounter = v;
                Dma[i].Repeat = v.GetBit(7);
                if (Dma[i].Indirect)
                {
                    Dma[i].Size = (ushort)(Dma[i].Read(Dma[i].ABank << 16 | Dma[i].HAddress)
                        | Dma[i].Read(Dma[i].ABank << 16 | Dma[i].HAddress + 1) << 8);
                    Dma[i].HAddress += 2;
                }
                Dma[i].TransferEnabled = true;
            }
            else
                Dma[i].TransferEnabled = false;
        }
    }

    private void Render(int y)
    {
        Sub = new(0, 0, 5);
        Span<int> bpp = new(Layers[BgMode][BgMode == 1 || BgMode == 7 ? 4 : 2]);
        int mapaddr, sx, sy;
        bool main, sub;
        //Array.Fill<uint>(ScreenBuffer, 0xff000000, VPos * 256, 256);
        for (int x = 0; x < 256; x++)
        {
            int mx = 0, my = 0;
            for (int i = 0; i < bpp.Length; i++)
            {
                main = MainBgs[i];
                sub = SubBgs[i];

                MBgs[i].Color = 0; MBgs[i].Priority = 0;
                SBgs[i].Color = 0; SBgs[i].Priority = 0;

                if (!main & !sub)
                    continue;

                int paloff = BgMode == 0 ? i * 32 : 0;

                if (BgMode < 7)
                {
                    if (MosaicEnabled[i])
                    {
                        mx = MosaicSize > 0 ? (x % MosaicSize) : 0;
                        my = MosaicSize > 0 ? (y % MosaicSize) : 0;
                    }

                    sx = (x - mx + BgScrollX[i]);
                    sy = (y - my + BgScrollY[i]);

                    if (BgMode == 2)
                    {
                        if (x > 7)
                        {
                            int h = GetMode2Tile(BgScrollX[2] + (x - 8) & 0xf8, BgScrollY[2], BgMapbase[2]);
                            int v = GetMode2Tile(BgScrollX[2] + (x - 8) & 0xf8, BgScrollY[2] + 8, BgMapbase[2]);
                            var bit = i == 0 ? 13 : 14;
                            if (h.GetBit(bit))
                                sx = (sx & 7) + (x & ~7) + (h + BgScrollX[i]) & 0x1fff;
                            if (v.GetBit(bit))
                                sy += (v & 0x1fff);
                        }

                        mapaddr = BgMapbase[i] + (byte)sy / 8 * 32 + (byte)sx / 8;
                        mapaddr += BgSizeX[i] > 0xff ? (sx & 0x100) * 4 : 0;
                        mapaddr += (BgSizeY[i] > 0xff ? ((sy & 0x100) * 8) : 0) & 0x7fff;
                    }
                    else
                    {
                        if (BgCharSize[i])
                            mapaddr = BgMapbase[i] + ((sy / 2 / 8) * 32 + (sx / 2 / 8));
                        else
                        {
                            sx &= BgSizeX[i];
                            sy &= BgSizeY[i];
                            mapaddr = (BgMapbase[i] + ((byte)sy / 8) * 32) + (byte)sx / 8;
                            mapaddr += (sx & 0x100) * 4;
                        }
                        mapaddr += (((BgSizeY[i] == 0x1ff) && (sy & 0x100) > 0 ? BgSizeX[i] == 0x1ff ? 0x800 : 0x400 : 0));
                        mapaddr &= 0x7fff;
                    }

                    (var color, var pixel, var pal) = GetColor(sx, sy, mapaddr, BgTilebase[i], BgCharSize[i], bpp[i], paloff);

                    if (main && pixel > 0)
                    {
                        MBgs[i].Color = color;
                        MBgs[i].Palette = pal;
                        MBgs[i].Priority = (Vram[mapaddr] >> 13) & 1;
                        MBgs[i].Layer = i;

                        if (WinMainBgs[i] && GetWindow(i, x))
                            MBgs[i].Color = 0;
                    }

                    if (sub && pixel > 0)
                    {
                        SBgs[i].Color = color;
                        SBgs[i].Palette = pal;
                        SBgs[i].Priority = (Vram[mapaddr] >> 13) & 1;
                        SBgs[i].Layer = i;

                        if (SubBgs[i] && GetWindow(i, x))
                            SBgs[i].Color = 0;
                    }
                }
                else
                {
                    //int rx = Mode7Settings[1] ? 255 - x : x;
                    int ry = Mode7Settings[1] ? 255 - y : y;
                    var cx = ScrollXMode7 - M7X;
                    var cy = ScrollYMode7 - M7Y;
                    int ch = cx.GetBit(13) ? cx | ~0x3ff : cx & 0x3ff;
                    int cv = cy.GetBit(13) ? cy | ~0x3ff : cy & 0x3ff;
                    sx = ((short)M7A * ch & ~63) + (((short)M7B * cv) & ~63) + ((short)M7B * ry & ~63) + (M7X << 8);
                    sy = (((short)M7C * ch) & ~63) + (((short)M7D * cv) & ~63) + ((short)M7D * ry & ~63) + (M7Y << 8);
                    var ox = (sx + (short)M7A * x);
                    var oy = (sy + (short)M7C * x);
                    ox >>= 8;// & 0x3ff;
                    oy >>= 8;// & 0x3ff;

                    if (Mode7Settings[3] && (ox < 0 || oy < 0 || ox >= 1024 || oy >= 1024))
                    {
                        ox &= 7;
                        oy &= 7;
                    }

                    if (!Mode7Settings[3])
                        mapaddr = ((oy >> 3) * 128 + (ox >> 3)) & 0x7fff;
                    else
                        mapaddr = ((oy >> 3) * 128 + (ox >> 3)) & 0x7fff;

                    (var color, _, _) = GetColor(ox, oy, mapaddr, BgTilebase[i], BgCharSize[i], bpp[i], 0);

                    if (main)
                    {
                        MBgs[i].Color = color;
                    }
                }
            }

            main = MainBgs[4] && !GetWindow(4, x);
            sub = SubBgs[4] && !GetWindow(4, x);

            MBgs[4].Color = 0; SBgs[4].Color = 0;
            MBgs[4].Priority = 0; SBgs[4].Priority = 0;

            if (main || sub)
            {
                for (int i = 0; i < SpritesScanline; i++)
                {
                    var s = SpriteScan[i];
                    if (s.Y == 224) continue;
                    if (s.X == -256 || s.X > 256) continue;
                    int fx = (x - s.X);
                    int fy = (y - s.Y);

                    if (fx < 0 || fx >= s.Width) continue;

                    if (s.Attrib.GetBit(6))
                        fx = s.Width - fx - 1;

                    if (s.Attrib.GetBit(7))
                        fy = s.Height - fy - 1;

                    var baseaddr = ObjTable1 + ((s.Attrib & 1) > 0 ? ObjTable2 : 0);
                    var spraddr = baseaddr + (s.Tile + (fx / 8)) * 16 + (fy & 7) + ((byte)fy / 8) * (s.Width * s.Height);
                    var colorid = GetPixel(spraddr, 7 - fx & 7, 4);
                    var palid = (s.Attrib & 0x0e) >> 1;
                    var pal = (0x80 + palid * 16 + colorid) & 0xff;
                    var color = (ushort)(Cram[pal]);
                    if (colorid > 0)
                    {
                        if (WinMainBgs[4] && GetWindow(4, x))
                            continue;

                        if (colorid > 0)
                        {
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

            var clip = Clip switch
            {
                1 => !GetWindow(5, x),
                2 or 3 => GetWindow(5, x),
                _ => false
            };

            Main = GetPriority(BgMode, MBgs);

            if (clip)
                Main.Color = 0;

            var add = !ColorMath[7];
            var half = ColorMath[6] ? 1 : 0;
            var math = GetMathEnabled(Main.Layer, x);

            if (math && AddSub)
            {
                Sub = GetPriority(BgMode, SBgs);
                if (Sub.Layer == 5 && BgMode != 7)
                {
                    Sub.Color = Fixed.Color;
                    half = 0;
                }
            }

            ScreenBuffer[(VPos - 1) * 256 + x] = GetRGB555((ushort)Main.Color, (ushort)Sub.Color, Brightness, math, add, half);
        }
    }

    public static uint GetRGB555(ushort p, ushort s, int br, bool math, bool add, int half)
    {
        var brightness = br / 15f;
        var mr = (p & 0x1f);
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
        if (!Win1Enabled[i] && !Win2Enabled[i]) return false;
        var w1 = x >= W1Left && x <= W1Right;
        var w2 = x >= W2Left && x <= W2Right;
        if (Win1Enabled[i] && !Win2Enabled[i])
            return Win1Inverted[i] ? !w1 : w1;
        else if (!Win1Enabled[i] && Win2Enabled[i])
            return Win2Inverted[i] ? !w2 : w2;
        w1 = Win1Inverted[i] ? !w1 : w1;
        w2 = Win2Inverted[i] ? !w2 : w2;
        var log = (WinLogic[i]) switch
        {
            0 => w1 || w2,
            1 => w1 && w2,
            2 => w1 != w2,
            3 => w1 == w2,
            _ => false
        };
        return log;
    }

    private bool GetMathEnabled(int i, int x)
    {
        var prev = Prevent switch
        {
            1 => !GetWindow(5, x),
            2 or 3 => GetWindow(5, x),
            _ => false
        };
        if (prev)
            return false;
        return ColorMath[i] && (Main.Layer != 4 || Main.Palette >= 0xc0);
    }

    private GfxColor GetPriority(int mode, GfxColor[] Colors)
    {
        Span<int[]> layer = new(Layers[mode]);
        switch (mode)
        {
            case 0 or 2 or 3 or 4 or 5 or 6:
            {
                for (int i = 0; i < layer[0].Length; i++)
                {
                    int l = layer[0][i];
                    int p = layer[1][i];
                    if (Colors[l].Priority == p && (Colors[l].Color & 1) > 0)
                        return Colors[l];
                }
                break;
            }
            case 1:
            {
                var n = Mode1Bg3Prio ? 1 : 0;
                for (int i = 0; i < layer[0].Length; i++)
                {
                    int l = layer[n + 0][i];
                    int p = layer[n + 2][i];
                    if (l == 2 && n == 1 && Colors[l].Priority == p && (Colors[l].Color & 1) > 0)
                        return Colors[2];
                    else if (Colors[l].Priority == p && (Colors[l].Color & 1) > 0)
                        return Colors[l];
                    else if (l == 2 && n == 0 && Colors[l].Priority == p && (Colors[l].Color & 1) > 0)
                        return Colors[2];
                }
                break;
            }
            case 7:
            {
                var n = ExtBgMode ? 1 : 0;
                for (int i = 0; i < layer[0].Length; i++)
                {
                    int l = layer[n + 0][i];
                    int p = layer[n + 2][i];
                    if (Colors[l].Priority == p && (Colors[l].Color & 1) > 0)
                        return Colors[l];
                }
                break;
            }
        }
        ;
        return new(Cram[0], 0, 5);
    }

    private (int, int, int) GetColor(int sx, int sy, int mapaddr, int tilebase, bool bigchar, int bpp, int paloff)
    {
        int pixel = 0;
        int p;
        if (BgMode < 7)
        {
            var flipx = ((Vram[mapaddr] >> 14) & 1) > 0;
            var flipy = ((Vram[mapaddr] >> 15) & 1) > 0;
            var fx = flipx ? 7 - (sx ^ 7) & 7 : 7 - sx & 7;
            var fy = flipy ? (sy ^ 7) & 7 : sy & 7;
            var tileid = Vram[mapaddr] & 0x3ff;
            if (bigchar)
            {
                tileid += (sx & 8) > 7 ? (flipx ? 0 : 1) : (flipx ? 1 : 0);
                tileid += (sy & 8) > 7 ? (flipy ? 0 : 16) : (flipy ? 16 : 0);
            }
            var palid = Vram[mapaddr] >> 10 & 7;
            ushort ta = (ushort)(tilebase + tileid * bpp * 4 + fy);
            pixel = GetPixel(ta, fx, bpp);
            p = (ushort)(ushort)(paloff + palid * (bpp == 4 ? 16 : bpp == 8 ? 256 : 4) + pixel);
            return (((ushort)(Cram[p & 0xff] | (pixel > 0 ? 1 : 0))), pixel, p);
        }
        else
        {
            var tileid = Vram[mapaddr] & 0xff;
            ushort ta = (ushort)((tileid * 64 + (sy & 7) * 8 + (sx & 7)) & 0x3fff);
            p = Vram[ta] >> 8;
            return (((ushort)(Cram[p & 0xff] | 1)), pixel, p);
        }
    }

    private int GetMode2Tile(int x, int y, int mapaddr) => Vram[mapaddr + (y / 8) * 32 + x / 8];

    private void RenderExtBG(int i, int x, int y)
    {
        if (!ExtBgMode)
        {
            MBgs[i].Color = 0;
            return;
        }

        var main = MainBgs[i] && !GetWindow(i, x);
        var sub = SubBgs[i] && !GetWindow(i, x);
        if (main || sub)
        {
            int ry = Mode7Settings[1] ? 255 - y : y;
            var cx = ScrollXMode7 - M7X;
            var cy = ScrollYMode7 - M7Y;
            int ch = cx.GetBit(13) ? cx | ~0x3ff : cx & 0x3ff;
            int cv = cy.GetBit(13) ? cy | ~0x3ff : cy & 0x3ff;
            int sx = ((short)M7A * ch & ~63) + (((short)M7B * cv) & ~63) + ((short)M7B * ry & ~63) + (M7X << 8);
            int sy = (((short)M7C * ch) & ~63) + (((short)M7D * cv) & ~63) + ((short)M7D * ry & ~63) + (M7Y << 8);
            var ox = (sx + (short)M7A * x);
            var oy = (sy + (short)M7C * x);
            ox = (ox >> 8) & 0x3ff;
            oy = (oy >> 8) & 0x3ff;

            var mapaddr = ((oy >> 3) * 128 + (ox >> 3)) & 0x7fff;
            var tileid = Vram[mapaddr] & 0xff;
            ushort ta = (ushort)((tileid * 64 + (oy & 7) * 8 + (ox & 7)) & 0x3fff);
            var colorid = Vram[ta] >> 8;
            var opaque = (colorid > 0 ? 1 : 0);
            if (main)
            {
                MBgs[i].Color = opaque == 1 ? (ushort)(Cram[colorid & 0xff] | 1) : (ushort)Cram[0];
                MBgs[i].Priority = (Vram[mapaddr] >> 13);
            }
        }
    }

    private void RenderObj(int x, int y)
    {
        var main = MainBgs[4] && !GetWindow(4, x);
        var sub = SubBgs[4] && !GetWindow(4, x);

        MBgs[4].Color = 0; SBgs[4].Color = 0;
        MBgs[4].Priority = 0; SBgs[4].Priority = 0;

        if (!main && !sub)
            return;

        for (int i = 0; i < SpritesScanline; i++)
        {
            SpriteData s = SpriteScan[i];
            if (s.Y == 224) continue;
            if (s.X == -256 || s.X > 256) continue;
            int fx = (x - s.X);
            int fy = (y - s.Y);

            if (fx < 0 || fx >= s.Width) continue;

            for (int xx = 0; xx < 8; xx++)
            {
                if (s.X + xx < 0 || s.X + xx > SnesWidth - 1)
                    continue;
            }

            if (s.Attrib.GetBit(6))
                fx = s.Width - fx - 1;

            if (s.Attrib.GetBit(7))
                fy = s.Height - fy - 1;

            var baseaddr = ObjTable1 + ((s.Attrib & 1) > 0 ? ObjTable2 : 0);
            var spraddr = baseaddr + (s.Tile + (fx / 8)) * 16 + (fy & 7) + ((byte)fy / 8) * (s.Width * s.Height);
            var colorid = GetPixel(spraddr, 7 - fx & 7, 4);
            var palid = (s.Attrib & 0x0e) >> 1;
            var pal = (0x80 + palid * 16 + colorid) & 0xff;
            var color = (ushort)(Cram[pal]);
            if (colorid > 0)
            {
                if (WinMainBgs[4] && GetWindow(4, x))
                    return;

                if (colorid > 0)
                {
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


    private void EvaluateSprites()
    {
        int c = SpritesScanline = 0;
        int n = ObjPrioRotation ? (InterOamAddr & 0x1fc) / 4 : 0;
        for (int i = 0; i < 128; i++)
        {
            if (c > 31)
                break;

            var v = Oam[0x200 + n / 4];
            var t = v >> ((n & 3) << 1) & 3;
            var highbit = t & 1;
            int sy = Oam[n * 4 + 1];
            int yp = VPos - sy - 1;
            int width = ObjSizeWidth[((ObjSize | t) / 2) << 3 & 0xf];
            int height = ObjSizeHeight[((ObjSize | t) / 2) << 3 & 0xf];

            if (yp >= 0 && yp < height || sy + height > 255 && VPos < (byte)(sy + height))
            {
                SpriteScan[c].X = highbit * -256 + Oam[n * 4 + 0];
                SpriteScan[c].Y = Oam[n * 4 + 1] + 1;
                SpriteScan[c].Tile = Oam[n * 4 + 2];
                SpriteScan[c].Attrib = Oam[n * 4 + 3];
                SpriteScan[c].Priority = (Oam[n * 4 + 3] >> 4) & 3;
                SpriteScan[c].Width = width;
                SpriteScan[c].Height = height;
                SpriteScan[c].Id = n;
                c++;
            }
            n = (n + 1) & 0x7f;
        }
        SpritesScanline = c;
    }

    private int GetPixel(int ta, int fx, int bpp)
    {
        switch (bpp)
        {
            case 2:
            {
                var b0 = Vram[ta & 0x7fff];
                return ((byte)b0 >> fx & 1) | (b0 >> 8 >> fx & 1) * 2;
            }
            case 4:
            {
                var b0 = Vram[ta & 0x7fff];
                var b1 = Vram[ta + 8 & 0x7fff];
                return ((byte)b0 >> fx & 1) | (b0 >> 8 >> fx & 1) * 2 |
                       ((byte)b1 >> fx & 1) * 4 | (b1 >> 8 >> fx & 1) * 8;
            }
            case 8:
            {
                var b0 = Vram[ta & 0x7fff];
                var b1 = Vram[ta + 0x08 & 0x7fff];
                var b2 = Vram[ta + 0x10 & 0x7fff];
                var b3 = Vram[ta + 0x18 & 0x7fff];
                return ((byte)b0 >> fx & 1) | (b0 >> 8 >> fx & 1) * 2 |
                       ((byte)b1 >> fx & 1) * 4 | (b1 >> 8 >> fx & 1) * 8 |
                       ((byte)b2 >> fx & 1) * 16 | (b2 >> 8 >> fx & 1) * 32 |
                       ((byte)b3 >> fx & 1) * 64 | (b3 >> 8 >> fx & 1) * 128;
            }
        }
        return 0;
    }

    public ushort ReadVram(int a) => (ushort)(Vram[a] | Vram[a + 1] << 8);

    public byte Read(int a)
    {
        switch (a)
        {
            case 0x34: return (byte)MultiplyRes;
            case 0x35: return (byte)(MultiplyRes >> 8);
            case 0x36: return (byte)(MultiplyRes >> 16);
            case 0x37:
                if (WRIO.GetBit(7))
                {
                    OPHCT = HPos >> 2;
                    OPVCT = VPos;
                    CounterLatch = true;
                }
                return Snes.Cpu.OpenBus;
            case 0x38:
            {
                var v = 0;
                if (OamAddr < 0x200 && (OamAddr & 1) == 1)
                {
                    v = Oam[OamAddr];
                    //break;
                }
                else if (OamAddr > 0x1ff)
                    v = Oam[OamAddr % Oam.Length];
                OamAddr++;
                return (byte)v;
            }
            case 0x39:
            {
                var v = (byte)VramLatch;
                if (!VramAddrMode)
                {
                    VramLatch = Vram[GetVramRemap()];
                    VramAddr += ppuaddrinc[VramAddrInc];
                }
                return (byte)v;
            }
            case 0x3a:
            {
                var v = (byte)(VramLatch >> 8);
                if (VramAddrMode)
                {
                    VramLatch = Vram[GetVramRemap()];
                    VramAddr += ppuaddrinc[VramAddrInc];
                }
                return (byte)v;
            }
            case 0x3c:
            {
                int v;
                if (!OphctLatch)
                    v = OPHCT & 0xff;
                else
                    v = OPHCT >> 8;
                OphctLatch = !OphctLatch;
                return (byte)v;
            }
            case 0x3d:
            {
                int v;
                if (!OpvctLatch)
                    v = OPVCT & 0xff;
                else
                    v = OPVCT >> 8;
                OpvctLatch = !OpvctLatch;
                return (byte)v;
            }
            case 0x3f:
                CounterLatch = false;
                OphctLatch = false;
                OpvctLatch = false;
                return (byte)STAT78;
        }
        return 0x00;
    }

    readonly int[] ppuaddrinc = [1, 32, 128, 128];
    public void Write(int a, byte v)
    {
        switch (a & 0xff)
        {
            case 0x00:
                ForcedBlank = v.GetBit(7);
                Brightness = v & 0x0f;
                break;
            case 0x01:
                ObjTable1 = (v & 0x03) << 13;
                ObjTable2 = ((v & 0x18) >> 3) + 1 << 12;
                ObjSize = ((v & 0xe0) >> 5) & 7;
                break;
            case 0x02:
                OamAddr = v;
                InterOamAddr = v << 1;
                ObjPrioIndex = v & 0xe0;
                break;
            case 0x03:
                OamAddr |= v << 8;
                OamAddr = (OamAddr & 0x1ff) << 1;
                ObjPrioRotation = v.GetBit(7);
                break;
            case 0x04:
                if ((OamAddr & 1) == 0)
                    OamLatch = v;
                if (OamAddr < 0x200 && (OamAddr & 1) == 1)
                {
                    Oam[OamAddr - 1] = OamLatch;
                    Oam[OamAddr++] = v;
                    break;
                }
                else if (OamAddr > 0x1ff)
                    Oam[OamAddr % Oam.Length] = v;
                OamAddr++;
                break;
            case 0x05:
                BgMode = v & 7;
                Mode1Bg3Prio = v.GetBit(3);
                BgCharSize = [v.GetBit(4), v.GetBit(5), v.GetBit(6), v.GetBit(7)];
                break;
            case 0x06:
                MosaicEnabled = [v.GetBit(0), v.GetBit(1), v.GetBit(2), v.GetBit(3),];
                MosaicSize = (v & 0xc0) >> 4;
                break;
            case 0x07 or 0x08 or 0x09 or 0x0a:
                var b = v & 3;
                BgMapbase[(a & 0xff) - 7] = (v >> 2 << 10) & 0x7fff;
                var i = (a & 0xff) - 7;
                switch (b)
                {
                    case 0:
                        BgSizeX[i] = 255; BgSizeY[i] = 255;
                        break;
                    case 1:
                        BgSizeX[i] = 511; BgSizeY[i] = 255;
                        break;
                    case 2:
                        BgSizeX[i] = 255; BgSizeY[i] = 511;
                        break;
                    case 3:
                        BgSizeX[i] = 511; BgSizeY[i] = 511;
                        break;
                }
                break;
            case 0x0b:
                BgTilebase[(byte)a - 0xb] = (v & 0xf) << 12;
                BgTilebase[(byte)a - 0xb + 1] = (v >> 4) << 12;
                break;
            case 0x0c:
                BgTilebase[(byte)a - 0xb + 1] = (v & 0xf) << 12;
                BgTilebase[(byte)a - 0xb + 2] = (v >> 4) << 12;
                break;
            case 0x0d:
                ScrollXMode7 = ((v << 8) | Mode7Latch) & 0xffff;
                Mode7Latch = v;
                goto case 0x0f;
            case 0x0f:
            case 0x11:
            case 0x13:
                BgScrollX[((a & 0xff) - 0xd) / 2] = (ushort)(((v << 8) | (PrevScrollX & ~7) | (CurrScrollX & 7)));
                PrevScrollX = v;
                CurrScrollX = v;
                break;
            case 0x0e:
                ScrollYMode7 = ((v << 8) | Mode7Latch) & 0xffff;
                Mode7Latch = v;
                goto case 0x10;
            case 0x10:
            case 0x12:
            case 0x14:
                BgScrollY[((a & 0xff) - 0xe) / 2] = (ushort)(((v << 8) | PrevScrollX & 0xff));
                PrevScrollX = v;
                break;
            case 0x15:
                VramAddrInc = v & 3;
                VramAddrRemap = (v >> 2) & 3;
                VramAddrMode = v.GetBit(7);
                break;
            case 0x16:
                VramAddr = VramAddr & 0xff00 | v;
                VramLatch = Vram[GetVramRemap()];
                break;
            case 0x17:
                VramAddr = VramAddr & 0xff | (v << 8);
                VramLatch = Vram[GetVramRemap()];
                break;
            case 0x18:
            {
                var va = GetVramRemap();
                Vram[va] = (ushort)(Vram[va] & 0xff00 | v);
                if (!VramAddrMode)
                    VramAddr += ppuaddrinc[VramAddrInc];
                if (Snes.Debug && Snes.DebugWindow.AccessCheck((ushort)(VramAddr * 2), v, RamType.Vram, true))
                    Snes.State = Break;
                break;
            }
            case 0x19:
            {
                var va = GetVramRemap();
                Vram[va] = (ushort)(Vram[va] & 0xff | v << 8);
                if (VramAddrMode)
                    VramAddr += ppuaddrinc[VramAddrInc];
                if (Snes.Debug && Snes.DebugWindow.AccessCheck((ushort)(VramAddr * 2), v, RamType.Vram, true))
                    Snes.State = Break;
                break;
            }

            case 0x1a:
                Mode7Settings = [v.GetBit(0), v.GetBit(1), v.GetBit(6), v.GetBit(7)];
                break;
            case 0x1b:
                M7A = (v << 8) | Mode7Latch;
                Mode7Latch = v;
                break;
            case 0x1c:
                M7B = (v << 8) | Mode7Latch;
                Mode7Latch = v;
                MultiplyRes = (short)M7A * (sbyte)(M7B >> 8);
                break;
            case 0x1d:
                M7C = (v << 8) | Mode7Latch;
                Mode7Latch = v;
                break;
            case 0x1e:
                M7D = (v << 8) | Mode7Latch;
                Mode7Latch = v;
                break;
            case 0x1f:
                M7X = (v << 8) | Mode7Latch;
                Mode7Latch = v;
                break;
            case 0x20:
                M7Y = (v << 8) | Mode7Latch;
                Mode7Latch = v;
                break;
            case 0x21: CGADD = v; CgRamToggle = false; break;
            case 0x22:
                if (!CgRamToggle)
                    Cram[CGADD & 0xff] = v;
                else
                {
                    Cram[CGADD & 0xff] = (ushort)(Cram[CGADD & 0xff] & 0xff | v << 8);
                    CGADD++;
                }
                CgRamToggle = !CgRamToggle;
                break;
            case 0x23:
                Win1Inverted[0] = v.GetBit(0); Win1Enabled[0] = v.GetBit(1);
                Win2Inverted[0] = v.GetBit(2); Win2Enabled[0] = v.GetBit(3);
                Win1Inverted[1] = v.GetBit(4); Win1Enabled[1] = v.GetBit(5);
                Win2Inverted[1] = v.GetBit(6); Win2Enabled[1] = v.GetBit(7);
                break;
            case 0x24:
                Win1Inverted[2] = v.GetBit(0); Win1Enabled[2] = v.GetBit(1);
                Win2Inverted[2] = v.GetBit(2); Win2Enabled[2] = v.GetBit(3);
                Win1Inverted[3] = v.GetBit(4); Win1Enabled[3] = v.GetBit(5);
                Win2Inverted[3] = v.GetBit(6); Win2Enabled[3] = v.GetBit(7);
                break;
            case 0x25:
                Win1Inverted[4] = v.GetBit(0); Win1Enabled[4] = v.GetBit(1);
                Win2Inverted[4] = v.GetBit(2); Win2Enabled[4] = v.GetBit(3);
                Win1Inverted[5] = v.GetBit(4); Win1Enabled[5] = v.GetBit(5);
                Win2Inverted[5] = v.GetBit(6); Win2Enabled[5] = v.GetBit(7);
                break;
            case 0x26: W1Left = v; break;
            case 0x27: W1Right = v; break;
            case 0x28: W2Left = v; break;
            case 0x29: W2Right = v; break;
            case 0x2a:
                WinLogic[0] = v & 3; WinLogic[1] = (v >> 2) & 3;
                WinLogic[2] = (v >> 4) & 3; WinLogic[3] = (v >> 6) & 3;
                break;
            case 0x2b:
                WinLogic[4] = v & 3; WinLogic[5] = (v >> 2) & 3;
                break;
            case 0x2c: MainBgs = [v.GetBit(0), v.GetBit(1), v.GetBit(2), v.GetBit(3), v.GetBit(4)]; break;
            case 0x2d: SubBgs = [v.GetBit(0), v.GetBit(1), v.GetBit(2), v.GetBit(3), v.GetBit(4)]; break;
            case 0x2e: WinMainBgs = [v.GetBit(0), v.GetBit(1), v.GetBit(2), v.GetBit(3), v.GetBit(4)]; break; ;
            case 0x2f: WinSubBgs = [v.GetBit(0), v.GetBit(1), v.GetBit(2), v.GetBit(3), v.GetBit(4)]; break;
            case 0x30:
                DirColor = v.GetBit(0);
                AddSub = v.GetBit(1);
                Prevent = (v >> 4) & 3;
                Clip = (v >> 6) & 3;
                break;
            case 0x31:
                ColorMath = [v.GetBit(0), v.GetBit(1), v.GetBit(2), v.GetBit(3),
                    v.GetBit(4), v.GetBit(5), v.GetBit(6), v.GetBit(7)];
                break;
            case 0x32:
                var c = v & 0x1f;
                if (v.GetBit(5))
                    Fixed.Color = (ushort)(Fixed.Color & 0x7fe0 | c);
                if (v.GetBit(6))
                    Fixed.Color = (ushort)(Fixed.Color & 0x7c1f | c << 5);
                if (v.GetBit(7))
                    Fixed.Color = (ushort)(Fixed.Color & 0x3ff | c << 10);
                break;
            case 0x33:
                OverscanMode = v.GetBit(2);
                HiResMode = v.GetBit(3);
                ExtBgMode = v.GetBit(4);
                break;
            case 0x81: RamAddrLow = v; break;
            case 0x82: RamAddrMedium = v; break;
            case 0x83: RamAddrHigh = v; break;
        }
    }

    public void WriteByte(int a, int v)
    {
        bool islow = (a & 1) == 0;
        if (islow)
            Vram[a / 2] = (ushort)((Vram[a / 2] & 0xff00) | v);
        else
            Vram[a / 2] = (ushort)((Vram[a / 2] & 0x00ff) | v << 8);
    }

    public byte ReadIO(int a)
    {
        switch (a)
        {
            case 0x10:
            {
                var v = RDNMI;
                v |= Snes.Cpu.OpenBus & 0x70;
                RDNMI &= 0x7f;
                return (byte)v;
            }
            case 0x11:
            {
                var v = TIMEUP;
                v |= Snes.Cpu.OpenBus & 0x7f;
                HVBJOY &= 0x3f;
                TIMEUP &= 0x7f;
                return (byte)v;
            }
            case 0x12:
            {
                var v = HVBJOY & 0x01;
                v |= (HPos < 4 || HPos >= 1096) ? 0x40 : 0x00;
                v |= HVBJOY & 0x80;
                v |= Snes.Cpu.OpenBus & 0x3e;
                return (byte)v;
            }
            case 0x14: return (byte)MulDivResult;
            case 0x15: return (byte)(MulDivResult >> 8);
            case 0x16: return (byte)MulDivRemainder;
            case 0x17: return (byte)(MulDivRemainder >> 8);
            case 0x18: return (byte)JOY1L;
            case 0x19: return (byte)JOY1H;
        }
        return 0x00;
    }

    public void WriteIO(int a, int v)
    {
        switch (a & 0x1f)
        {
            case 0x00: NMITIMEN = v; break;
            case 0x01: WRIO = v; break;
            case 0x02: MultiplyA = v; break;
            case 0x03: MulDivRemainder = MultiplyA * v; break;
            case 0x04: Dividend = (Dividend & 0xff00) | v; break;
            case 0x05: Dividend = (Dividend & 0x00ff) | v << 8; break;
            case 0x06: Division(v); break;
            case 0x07: HTIMEL = v; break;
            case 0x08: HTIMEH = v; break;
            case 0x09: VTIMEL = v; break;
            case 0x0a: VTIMEH = v; break;
        }
    }

    private int GetVramRemap()
    {
        var a = VramAddr & 0x7fff;
        return VramAddrRemap switch
        {
            1 => (a & 0xff00) | (a & 0xe0) >> 5 | (a & 0x1f) << 3,
            2 => (a & 0xfe00) | (a & 0x1c0) >> 6 | (a & 0x3f) << 3,
            3 => (a & 0xfc00) | (a & 0x380) >> 7 | (a & 0x7f) << 3,
            _ => a,
        };
    }

    public void Reset()
    {
        VPos = 0;
        HPos = 0;
        Cycles = 0;
        Vram = new ushort[0x8000];
        Cram = new ushort[0x100];
        Oam = new byte[0x220];
        VramAddr = 0;
        VramLatch = 0;
        CGDATA = 0;
        HTIMEH = 0x01; HTIMEL = 0xff;
        VTIMEH = 0x01; VTIMEL = 0xff;
        Dividend = 0xffff;
        MultiplyA = 0xff; MultiplyB = 0xff;
        WRIO = 0xff;
        HVBJOY = 0x02;
        DramRefresh = false;
        Array.Fill<uint>(ScreenBuffer, 0xff000000);
        MBgs = [new(), new(), new(), new(), new(), new(),];
    }

    private readonly Dictionary<int, int[][]> Layers = new()
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

        public SpriteData(int X, int Y, int Tile, int Attrib, int Width, int Height, int Id)
        {
            this.X = X;
            this.Y = Y;
            this.Tile = Tile;
            this.Attrib = Attrib;
            Priority = (Attrib >> 4) & 3;
            this.Width = Width;
            this.Height = Height;
            this.Id = Id;
        }
    }

    private struct GfxColor(int color, int priority, int layer)
    {
        public int Color = color;
        public int Palette = 0;
        public int Priority = priority;
        public int Layer = layer;
    }

    public List<RegistersInfo> GetRegs() =>
    [
        new("","HClock",$"{HPos}"),
        new("","Scanline", $"{VPos}"),
        new("2105","BGMode", $"{BgMode:X2}"),
        new("2100","Brightness", $"{Brightness:X2}"),
        new("2132","Fixed Color", $"{Fixed.Color:X4}"),
        new("4216/7","Remainder", $"{MulDivRemainder:X4}"),
        new("","ObjAddr", $"{OamAddr:X4}"),
        new("2102/3","ObjPrioIndex", $"{ObjPrioIndex:X4}"),
        new("2103.7","ObjPrioRotation", $"{ObjPrioRotation}"),
        new("2116/7","Vram Addr", $"{VramAddr:X4}"),
        new("2126/7","Wram Address", $"{VramAddr * 2:X4}"),
        new("2126","W1 Left", $"{W1Left:X2}"),
        new("2127","W1 Right", $"{W1Right:X2}"),
        new("2128","W2 Left", $"{W2Left:X2}"),
        new("2129","W2 Right", $"{W2Right:X2}"),
        new("211B","M7A", $"{M7A:X4}"),
        new("211C","M7B", $"{M7B:X4}"),
        new("211D","M7C", $"{M7C:X4}"),
        new("211E","M7D", $"{M7D:X4}"),
        new("211F","M7X", $"{M7X:X4}"),
        new("2120","M7Y", $"{M7Y:X4}"),
        new("2107.0-1","BG1 Size", $"{BgCharSize[0]:X2}"),
        new("2107.2-6","BG1 Addr", $"{BgMapbase[0]:X4}"),
        new("2108.0-1","BG2 Size", $"{BgCharSize[1]:X2}"),
        new("2108.2-6","BG2 Addr", $"{BgMapbase[1]:X4}"),
        new("2109.0-1","BG3 Size", $"{BgCharSize[2]:X2}"),
        new("2109.2-6","BG3 Addr", $"{BgMapbase[2]:X4}"),
        new("210A.0-1","BG4 Size", $"{BgCharSize[3]:X2}"),
        new("210A.2-6","BG4 Addr", $"{BgMapbase[3]:X4}"),
        new("210B.0-2","BG1 Tile Addr", $"{BgTilebase[0]:X4}"),
        new("210B.4-6","BG2 Tile Addr", $"{BgTilebase[1]:X4}"),
        new("210C.0-2","BG3 Tile Addr", $"{BgTilebase[2]:X4}"),
        new("210C.4-6","BG4 Tile Addr", $"{BgTilebase[3]:X4}"),
        new("210D","BG1 X", $"{BgScrollX[0]:X4}"),
        new("210E","BG1 Y", $"{BgScrollY[0]:X4}"),
        new("210F","BG2 X", $"{BgScrollX[1]:X4}"),
        new("2110","BG2 Y", $"{BgScrollY[1]:X4}"),
        new("2111","BG3 X", $"{BgScrollX[2]:X4}"),
        new("2112","BG3 Y", $"{BgScrollY[2]:X4}"),
        new("2113","BG4 X", $"{BgScrollX[3]:X4}"),
        new("2114","BG4 Y", $"{BgScrollY[3]:X4}"),
        new("212C.0","MBG1 Enabled", $"{MainBgs[0]}"),
        new("212C.1","MBG2 Enabled", $"{MainBgs[1]}"),
        new("212C.2","MBG3 Enabled", $"{MainBgs[2]}"),
        new("212C.3","MBG4 Enabled", $"{MainBgs[3]}"),
        new("212C.4","MOAM Enabled", $"{MainBgs[4]}"),
        new("212D.0","SBG1 Enabled", $"{SubBgs[0]}"),
        new("212D.1","SBG2 Enabled", $"{SubBgs[1]}"),
        new("212D.2","SBG3 Enabled", $"{SubBgs[2]}"),
        new("212D.3","SBG4 Enabled", $"{SubBgs[3]}"),
        new("212D.4","SOAM Enabled", $"{SubBgs[3]}"),
        new("4200.4","HIrq", $"{GetHIrq}"),
        new("4200.5","VIrq", $"{GetVIrq}"),
        new("4207/8","HTIME", $"{GetHTime:X4}"),
        new("4209/A","VTIME", $"{GetVTime:X4}"),
        new("4212","HVBJOY", $"{HVBJOY:X2}"),
    ];

    public override void Save(BinaryWriter bw)
    {
        bw.Write(VPos); bw.Write(HPos); bw.Write(Cycles); bw.Write(FrameCounter);
        bw.Write(CgRamToggle); bw.Write(PrevScrollX); bw.Write(CurrScrollX); bw.Write(FrameReady);
        bw.Write(Vblank); bw.Write(Hblank); bw.Write(AutoJoyCounter); bw.Write(MosaicSize);
        bw.Write(ScrollXMode7); bw.Write(ScrollYMode7); bw.Write(W1Left); bw.Write(W1Right);
        bw.Write(W2Left); bw.Write(W2Right); bw.Write(AddSub); bw.Write(DirColor);
        bw.Write(Prevent); bw.Write(Clip); bw.Write(ObjTable1); bw.Write(ObjTable2);
        bw.Write(ObjSize); bw.Write(ObjPrioRotation); bw.Write(ObjPrioIndex); bw.Write(OamAddr);
        bw.Write(InterOamAddr); bw.Write(Brightness); bw.Write(ForcedBlank); bw.Write(BgMode);
        bw.Write(Mode1Bg3Prio); bw.Write(RamAddrLow); bw.Write(RamAddrMedium); bw.Write(RamAddrHigh);
        bw.Write(MultiplyA); bw.Write(MultiplyB); bw.Write(Dividend); bw.Write(Divisor);
        bw.Write(VramAddrInc); bw.Write(VramAddrRemap); bw.Write(VramAddrMode); bw.Write(VramAddr);
        bw.Write(VramLatch); bw.Write(OverscanMode); bw.Write(HiResMode); bw.Write(ExtBgMode);
        bw.Write(M7A); bw.Write(M7B); bw.Write(M7C); bw.Write(M7D);
        bw.Write(M7X); bw.Write(M7Y); bw.Write(CGADD); bw.Write(CGDATA);
        bw.Write(COLDATA); bw.Write(MPYL); bw.Write(MPYM); bw.Write(MPYH);
        bw.Write(SLHV); bw.Write(OAMDATAREAD); bw.Write(VMDATALREAD); bw.Write(VMDATAHREAD);
        bw.Write(CGDATAREAD); bw.Write(OPHCT); bw.Write(OPVCT); bw.Write(STAT77);
        bw.Write(STAT78); bw.Write(NMITIMEN); bw.Write(WRIO); bw.Write(HTIMEL);
        bw.Write(HTIMEH); bw.Write(VTIMEL); bw.Write(VTIMEH); bw.Write(MDMAEN);
        bw.Write(HDMAEN); bw.Write(RDNMI); bw.Write(TIMEUP); bw.Write(HVBJOY);
        bw.Write(RDIO); bw.Write(JOY1L); bw.Write(JOY1H); bw.Write(JOY2L);
        bw.Write(JOY2H); bw.Write(JOY3L); bw.Write(JOY3H); bw.Write(JOY4L);
        bw.Write(JOY4H); bw.Write(CounterLatch); bw.Write(OphctLatch); bw.Write(OpvctLatch);
        bw.Write(MultiplyRes); EmuState.WriteArray<int>(bw, BgMapbase);
        EmuState.WriteArray<int>(bw, BgTilebase);
        EmuState.WriteArray<int>(bw, BgScrollX);
        EmuState.WriteArray<int>(bw, BgScrollY);
        EmuState.WriteArray<int>(bw, BgSizeX);
        EmuState.WriteArray<int>(bw, BgSizeY);
        EmuState.WriteArray<bool>(bw, ColorMath);
        EmuState.WriteArray<bool>(bw, Win1Enabled);
        EmuState.WriteArray<bool>(bw, Win1Inverted);
        EmuState.WriteArray<bool>(bw, Win2Enabled);
        EmuState.WriteArray<bool>(bw, Win2Inverted);
        EmuState.WriteArray<int>(bw, WinLogic);
        EmuState.WriteArray<bool>(bw, MainBgs);
        EmuState.WriteArray<bool>(bw, SubBgs);
        EmuState.WriteArray<bool>(bw, WinMainBgs);
        EmuState.WriteArray<bool>(bw, WinSubBgs);
        EmuState.WriteArray<bool>(bw, MosaicEnabled);
        EmuState.WriteArray<bool>(bw, Mode7Settings);
        EmuState.WriteArray<bool>(bw, BgCharSize);
        EmuState.WriteArray<ushort>(bw, Vram);
        EmuState.WriteArray<ushort>(bw, Cram);
        EmuState.WriteArray<byte>(bw, Oam);
        EmuState.WriteArray<uint>(bw, ScreenBuffer);
    }

    public override void Load(BinaryReader br)
    {
        VPos = br.ReadInt32(); HPos = br.ReadInt32(); Cycles = br.ReadUInt64(); FrameCounter = br.ReadUInt32();
        CgRamToggle = br.ReadBoolean(); PrevScrollX = br.ReadInt32(); CurrScrollX = br.ReadInt32(); FrameReady = br.ReadBoolean();
        Vblank = br.ReadBoolean(); Hblank = br.ReadBoolean(); AutoJoyCounter = br.ReadInt32(); MosaicSize = br.ReadInt32();
        ScrollXMode7 = br.ReadInt32(); ScrollYMode7 = br.ReadInt32(); W1Left = br.ReadInt32(); W1Right = br.ReadInt32();
        W2Left = br.ReadInt32(); W2Right = br.ReadInt32(); AddSub = br.ReadBoolean(); DirColor = br.ReadBoolean();
        Prevent = br.ReadInt32(); Clip = br.ReadInt32(); ObjTable1 = br.ReadInt32(); ObjTable2 = br.ReadInt32();
        ObjSize = br.ReadInt32(); ObjPrioRotation = br.ReadBoolean(); ObjPrioIndex = br.ReadInt32(); OamAddr = br.ReadInt32();
        InterOamAddr = br.ReadInt32(); Brightness = br.ReadInt32(); ForcedBlank = br.ReadBoolean(); BgMode = br.ReadInt32();
        Mode1Bg3Prio = br.ReadBoolean(); RamAddrLow = br.ReadInt32(); RamAddrMedium = br.ReadInt32(); RamAddrHigh = br.ReadInt32();
        MultiplyA = br.ReadInt32(); MultiplyB = br.ReadInt32(); Dividend = br.ReadInt32(); Divisor = br.ReadInt32();
        VramAddrInc = br.ReadInt32(); VramAddrRemap = br.ReadInt32(); VramAddrMode = br.ReadBoolean(); VramAddr = br.ReadInt32();
        VramLatch = br.ReadInt32(); OverscanMode = br.ReadBoolean(); HiResMode = br.ReadBoolean(); ExtBgMode = br.ReadBoolean();
        M7A = br.ReadInt32(); M7B = br.ReadInt32(); M7C = br.ReadInt32(); M7D = br.ReadInt32();
        M7X = br.ReadInt32(); M7Y = br.ReadInt32(); CGADD = br.ReadInt32(); CGDATA = br.ReadInt32();
        COLDATA = br.ReadInt32(); MPYL = br.ReadInt32(); MPYM = br.ReadInt32(); MPYH = br.ReadInt32();
        SLHV = br.ReadInt32(); OAMDATAREAD = br.ReadInt32(); VMDATALREAD = br.ReadInt32(); VMDATAHREAD = br.ReadInt32();
        CGDATAREAD = br.ReadInt32(); OPHCT = br.ReadInt32(); OPVCT = br.ReadInt32(); STAT77 = br.ReadInt32();
        STAT78 = br.ReadInt32(); NMITIMEN = br.ReadInt32(); WRIO = br.ReadInt32(); HTIMEL = br.ReadInt32();
        HTIMEH = br.ReadInt32(); VTIMEL = br.ReadInt32(); VTIMEH = br.ReadInt32(); MDMAEN = br.ReadInt32();
        HDMAEN = br.ReadInt32(); RDNMI = br.ReadInt32(); TIMEUP = br.ReadInt32(); HVBJOY = br.ReadInt32();
        RDIO = br.ReadInt32(); JOY1L = br.ReadInt32(); JOY1H = br.ReadInt32(); JOY2L = br.ReadInt32();
        JOY2H = br.ReadInt32(); JOY3L = br.ReadInt32(); JOY3H = br.ReadInt32(); JOY4L = br.ReadInt32();
        JOY4H = br.ReadInt32(); CounterLatch = br.ReadBoolean(); OphctLatch = br.ReadBoolean(); OpvctLatch = br.ReadBoolean();
        MultiplyRes = br.ReadInt32(); BgMapbase = EmuState.ReadArray<int>(br, BgMapbase.Length);
        BgTilebase = EmuState.ReadArray<int>(br, BgTilebase.Length);
        BgScrollX = EmuState.ReadArray<int>(br, BgScrollX.Length);
        BgScrollY = EmuState.ReadArray<int>(br, BgScrollY.Length);
        BgSizeX = EmuState.ReadArray<int>(br, BgSizeX.Length);
        BgSizeY = EmuState.ReadArray<int>(br, BgSizeY.Length);
        ColorMath = EmuState.ReadArray<bool>(br, ColorMath.Length);
        Win1Enabled = EmuState.ReadArray<bool>(br, Win1Enabled.Length);
        Win1Inverted = EmuState.ReadArray<bool>(br, Win1Inverted.Length);
        Win2Enabled = EmuState.ReadArray<bool>(br, Win2Enabled.Length);
        Win2Inverted = EmuState.ReadArray<bool>(br, Win2Inverted.Length);
        WinLogic = EmuState.ReadArray<int>(br, WinLogic.Length);
        MainBgs = EmuState.ReadArray<bool>(br, MainBgs.Length);
        SubBgs = EmuState.ReadArray<bool>(br, SubBgs.Length);
        WinMainBgs = EmuState.ReadArray<bool>(br, WinMainBgs.Length);
        WinSubBgs = EmuState.ReadArray<bool>(br, WinSubBgs.Length);
        MosaicEnabled = EmuState.ReadArray<bool>(br, MosaicEnabled.Length);
        Mode7Settings = EmuState.ReadArray<bool>(br, Mode7Settings.Length);
        BgCharSize = EmuState.ReadArray<bool>(br, BgCharSize.Length);
        Vram = EmuState.ReadArray<ushort>(br, Vram.Length);
        Cram = EmuState.ReadArray<ushort>(br, Cram.Length);
        Oam = EmuState.ReadArray<byte>(br, Oam.Length);
        ScreenBuffer = EmuState.ReadArray<uint>(br, ScreenBuffer.Length);
    }
}


