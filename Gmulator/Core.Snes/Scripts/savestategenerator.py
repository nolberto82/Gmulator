
s = '''    public int VPos { get; private set; }
    public int HPos { get; private set; }
    public ulong Cycles { get; private set; }
    public uint Frame { get; private set; }
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
    public bool[] MathColor { get; private set; } = new bool[8];
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
    public uint[] ScreenBuffer { get; private set; } = new uint[SnesWidth * SnesHeight];
'''

types = {
    "byte": "Byte",
    "sbyte": "SByte",
    "ushort": "UInt16",
    "short": "Int16",
    "int": "Int32",
    "uint": "UInt32",
    "long": "Int64",
    "ulong": "UInt64",
    "bool": "Boolean",
}

writes = []
reads = []
s = [x.strip() for x in s.split("\n")]

for x in s:

    if x.startswith("//") or x == '':
        continue
    if x.startswith("public") or x.startswith("private"):
        isarray = False  
        if "[]" in x:
            isarray = True
        n = x.find(" //")
        if n > -1:
            x = x[0:n]
        j = x.find(" ") + 1
        k = x.find("{")
        if k == -1:
            k = x.find(" = ")
            if k == -1:
                k = len(x)
        n = x[j:k].find(" ")

        v = x[j+n:k].replace(";","")

        if isarray:
            t = f'EmuState.WriteArray<{x[j:j+n-2]}>(bw,{v.strip()});'
        else:
            t = f'bw.Write({v.strip()});'  

        writes.append(t)

for x in s:
    if x.startswith("//") or x == '':
        continue
    if x.startswith("public") or x.startswith("private"):
        isarray = False  
        if "[]" in x:
            isarray = True
        n = x.find(" //")
        if n > -1:
            x = x[0:n]
        j = x.find(" ") + 1
        k = x.find("{")
        if k == -1:
            k = x.find(" = ")
            if k == -1:
                k = len(x)
        n = x[j:k].find(" ")

        v = x[j+n:k].replace(";","")

        if isarray:
            t = f'{v} = EmuState.ReadArray<{x[j:j+n-2]}>(br,{v.strip()}.Length);'
        else:
            t = f'{v.strip()} = br.Read{types[x[j:j+n]]}();'

        reads.append(t)

i = 0
for t in writes:
    i += 1
    if "EmuState" not in t and i % 4 > 0:
        print(t, end=" ")
    else:
        print(t)

print("\n")

i = 0
for t in reads:
    i += 1
    if "EmuState" not in t and i % 4 > 0:
        print(t, end=" ")
    else:
        print(t)
