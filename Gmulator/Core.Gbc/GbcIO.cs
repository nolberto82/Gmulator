using Gmulator.Shared;

namespace Gmulator.Core.Gbc;

public partial class GbcIO : EmuState
{
    public bool UpdateTIMA { get; set; }

    public byte SB { get; set; }
    public byte SC { get; set; }

    public byte DIV { get; set; }
    public byte TIMA { get; set; }
    public byte TMA { get; set; }
    public byte TAC { get; set; }

    public byte IE
    {
        get => Mmu.ReadDirect(0xffff);
        set => Mmu.WriteDirect(0xffff, value);
    }
    public byte IF
    {
        get => Mmu.ReadDirect(0xff0f);
        set => Mmu.WriteDirect(0xff0f, value);
    }

    public byte LY { get; set; }
    public byte LYC { get; set; }
    public byte LCDC { get; set; }

    public byte STAT { get; set; }
    public byte BGP { get; set; }
    public byte OBP0 { get; set; }
    public byte OBP1 { get; set; }
    public byte BGPI { get; set; }
    public byte BGPD { get; set; }
    public byte OBPI { get; set; }
    public byte OBPD { get; set; }
    public byte SCY { get; set; }
    public byte SCX { get; set; }
    public byte WY { get; set; }
    public byte WX { get; set; }
    public byte OAMDMA { get; set; }
    public byte KEY1 { get; set; }
    public byte VBK { get; set; }
    public byte SVBK { get; set; }
    public byte HDMA1 { get; private set; }
    public byte HDMA2 { get; private set; }
    public byte HDMA3 { get; private set; }
    public byte HDMA4 { get; private set; }
    public byte HDMA5 { get; set; }

    public GbcTimer Timer { get; private set; }
    public GbcApu Apu { get; private set; }
    public GbcMmu Mmu { get; private set; }
    public GbcPpu Ppu { get; private set; }
    private GbcJoypad Joypad;

    public int SpeedMode { get => (KEY1 & 0x80) != 0 ? 2 : 1; }
    public bool DMAactive { get => (HDMA5 & 0x80) != 0; }
    public bool DMAHBlank { get; set; }

    public GbcIO(Gbc gbc)
    {
        Timer = gbc.Timer;
    }
    public GbcIO() { }

    public void Init(Gbc gbc)
    {
        Mmu = gbc.Mmu;
        Ppu = gbc.Ppu;
        Apu = gbc.Apu;
        Joypad = gbc.Joypad;
    }

    public byte Read(int a, bool editor = false)
    {
        var io = (byte)a;
        if (io == 0x00)
            return Joypad.Status;
        else if (io == 0x01)
            return SB;
        else if (io == 0x02)
            return SC;
        else if (io == 0x03)
            return 0xff;
        else if (io == 0x04)
            return (byte)(DIV | 0xad);
        else if (io == 0x05)
            return TIMA;
        else if (io == 0x06)
            return TMA;
        else if (io == 0x07)
            return (byte)(TAC | 0xf8);
        else if (io >= 0x08 && io <= 0x0e)
            return 0xff;
        if (io == 0x0f)
            return IF;
        else if (io >= 0x10 && io <= 0x26)
            return Apu.Read(io);
        else if (io >= 0x27 && io <= 0x2f)
            return 0xff;
        else if (io <= 0x3f)
            return Apu.Wave.ReadWaveRam(a);
        else if (io == 0x40)
            return LCDC;
        else if (io == 0x41)
            return STAT;
        else if (io == 0x42)
            return SCY;
        else if (io == 0x43)
            return SCX;
        else if (io == 0x44)
            return LY;
        else if (io == 0x45)
            return LYC;
        else if (io == 0x46)
            return OAMDMA;
        else if (io == 0x47)
            return BGP;
        else if (io == 0x48)
            return OBP0;
        else if (io == 0x49)
            return OBP1;
        else if (io == 0x4a)
            return WY;
        else if (io == 0x4b)
            return WX;
        else if (io == 0x4d)
            return KEY1;
        else if (io == 0x4f)
            return VBK;
        else if (io == 0x55)
        {
            var v = HDMA5 == 0 ? 0xff : HDMA5;
            if (v == 0xff)
                DMAHBlank = false;
            return (byte)v;
        }
        else if (io == 0x68)
            return BGPI;
        else if (io == 0x69)
        {
            BGPD = Ppu.CGBBkgPal[BGPI & 0x3f];
            if (!editor)
                BGPI += (byte)((BGPI & 0x80) != 0 ? 1 : 0);
            return BGPD;
        }
        else if (io == 0x6a)
            return OBPI;
        else if (io == 0x6b)
        {
            OBPD = Ppu.CGBObjPal[OBPI & 0x3f];
            if (!editor)
                OBPI += (byte)((OBPI & 0x80) != 0 ? 1 : 0);
            return OBPD;
        }
        else if (io == 0x70)
            return SVBK;
        else if (io == 0xff)
            return IE;
        return 0;
    }

    public void Write(int a, byte v)
    {
        var io = (byte)a;
        if (io == 0x00)
        {
            Joypad.Status = v;
            IF |= IntJoypad;
        }
        else if (io == 0x01)
            SB = v;
        else if (io == 0x02)
            SC = v;
        else if (io == 0x04)
            DIV = v;
        else if (io == 0x05)
        {
            if (!Timer.Overflow)
                TIMA = v;
        }
        else if (io == 0x06)
            TMA = v;
        else if (io == 0x07)
            TAC = v;
        else if (io >= 0x10 && io <= 0x3f)
            Apu.Write(io, v);
        else if (io == 0x0f)
            IF = v;
        else if (io == 0x40)
            LCDC = v;
        else if (io == 0x41)
        {
            STAT = (byte)(v & 0x78 | STAT & 7 | 0x80);
            if (((v & 0x08) != 0 || (v & 0x10) != 0 || (v & 0x20) != 0) && (v & 0x40) == 0)
                IF |= 2;
        }
        else if (io == 0x42)
            SCY = v;
        else if (io == 0x43)
            SCX = v;
        else if (io == 0x45)
            LYC = v;
        else if (io == 0x46)
        {
            Ppu.WriteDMA(v);
            OAMDMA = v;
        }
        else if (io == 0x47)
            BGP = v;
        else if (io == 0x48)
            OBP0 = v;
        else if (io == 0x49)
            OBP1 = v;
        else if (io == 0x4a)
            WY = v;
        else if (io == 0x4b)
            WX = v;
        else if (io == 0x4d)
            KEY1 = v;
        else if (io == 0x4f)
        {
            if (Mmu.Mapper.CGB)
                VBK = (byte)(v & 1);
        }
        else if (io == 0x51)
            HDMA1 = v;
        else if (io == 0x52)
            HDMA2 = v;
        else if (io == 0x53)
            HDMA3 = v;
        else if (io == 0x54)
            HDMA4 = v;
        else if (io == 0x55)
        {
            HDMA5 = (byte)(v & 0x7f);
            if (!DMAactive)
            {
                DMAHBlank = (v & 0x80) != 0;
                if (!DMAHBlank)
                {
                    var src = (HDMA1 << 8 | HDMA2) & 0xfff0;
                    var dst = ((HDMA3 << 8 | HDMA4) & 0x1ff0) | 0x8000;
                    Mmu.WriteBlock(src, dst, (HDMA5 + 1) * 16);
                }
            }
        }
        else if (io == 0x68)
            BGPI = v;
        else if (io == 0x69)
        {
            BGPD = v;
            Ppu.SetBkgPalette(BGPI, v);
            BGPI += (byte)((BGPI & 0x80) != 0 ? 1 : 0);
        }
        else if (io == 0x6a)
            OBPI = v;
        else if (io == 0x6b)
        {
            OBPD = v;
            Ppu.SetObjPalette(OBPI, v);
            OBPI += (byte)((OBPI & 0x80) != 0 ? 1 : 0);
        }
        else if (io == 0x70)
        {
            if (Mmu.Mapper.CGB)
                SVBK = (byte)(v == 0 ? 1 : (v & 7));
        }
        else if (io == 0xff)
            IE = v;
    }

    public void Reset()
    {
        IE = 0; IF = 0;
        SVBK = 0; VBK = 0;
        KEY1 = 0;
        DIV = 0; TIMA = 0;
        TMA = 0; TAC = 0;
        BGPD = 0xff;
    }

    public List<RegisterInfo> GetState() =>
    [
        new("FF40","LCDC",""),
        new("0","Background", (LCDC & 0x01) != 0 ? "Enabled" : "Disabled"),
        new("1","Sprites", (LCDC & 0x02) != 0 ? "Enabled" : "Disabled"),
        new("2","Sprite Size", (LCDC & 0x04) != 0 ? "8x16" : "8x8"),
        new("3","BG Map", (LCDC & 0x08) != 0 ? "9C00:9FFF" : "9800:9BFF"),
        new("4","BG Tile", (LCDC & 0x10) != 0 ? "8000:8FFF" : "8800:97FF"),
        new("5","Window", (LCDC & 0x20) != 0 ? "Enabled" : "Disabled"),
        new("6","Window Map", (LCDC & 0x40) != 0 ? "9C00:9FFF" : "9800:9BFF"),
        new("7","LCD", (LCDC & 0x80) != 0 ? "Enabled" : "Disabled"),
        new("FF41","STAT",""),
        new("0-1","PPU mode", $"{(STAT & 3)}"),
        new("2","LYC == LY", (STAT & 0x04) != 0 ? "Enabled" : "Disabled"),
        new("3","Mode 0 select", (STAT & 0x08) != 0 ? "Enabled" : "Disabled"),
        new("4","Mode 1 select", (STAT & 0x10) != 0 ? "Enabled" : "Disabled"),
        new("5","Mode 2 select", (STAT & 0x20) != 0 ? "Enabled" : "Disabled"),
        new("6","LYC select", (STAT & 0x40) != 0 ? "Enabled" : "Disabled"),
        new("FF0F","IF",""),
        new("0","Vblank", (IF & 0x01) != 0 ? "Enabled" : "Disabled"),
        new("1","LCD", (IF & 0x02) != 0 ? "Enabled" : "Disabled"),
        new("2","Timer", (IF & 0x04) != 0 ? "Enabled" : "Disabled"),
        new("3","Serial", (IF & 0x08) != 0 ? "Enabled" : "Disabled"),
        new("4","Joypad", (IF & 0x10) != 0 ? "Enabled" : "Disabled"),
        new("FFFF","IE",""),
        new("0","Vblank", (IE & 0x01) != 0 ? "Enabled" : "Disabled"),
        new("1","LCD", (IE & 0x02) != 0 ? "Enabled" : "Disabled"),
        new("2","Timer", (IE & 0x04) != 0 ? "Enabled" : "Disabled"),
        new("3","Serial", (IE & 0x08) != 0 ? "Enabled" : "Disabled"),
        new("4","Joypad", (IE & 0x10) != 0 ? "Enabled" : "Disabled"),
    ];

    public override void Save(BinaryWriter bw)
    {
        bw.Write(UpdateTIMA);
        bw.Write(SB);
        bw.Write(SC);
        bw.Write(DIV);
        bw.Write(TIMA);
        bw.Write(TMA);
        bw.Write(TAC);
        bw.Write(IE);
        bw.Write(IF);
        bw.Write(LY);
        bw.Write(LYC);
        bw.Write(LCDC);
        bw.Write(STAT);
        bw.Write(BGP);
        bw.Write(OBP0);
        bw.Write(OBP1);
        bw.Write(BGPI);
        bw.Write(BGPD);
        bw.Write(OBPI);
        bw.Write(OBPD);
        bw.Write(SCY);
        bw.Write(SCX);
        bw.Write(WY);
        bw.Write(WX);
        bw.Write(OAMDMA);
        bw.Write(KEY1);
        bw.Write(VBK);
        bw.Write(SVBK);
        bw.Write(HDMA1);
        bw.Write(HDMA2);
        bw.Write(HDMA3);
        bw.Write(HDMA4);
        bw.Write(HDMA5);
    }

    public override void Load(BinaryReader br)
    {
        UpdateTIMA = br.ReadBoolean();
        SB = br.ReadByte();
        SC = br.ReadByte();
        DIV = br.ReadByte();
        TIMA = br.ReadByte();
        TMA = br.ReadByte();
        TAC = br.ReadByte();
        IE = br.ReadByte();
        IF = br.ReadByte();
        LY = br.ReadByte();
        LYC = br.ReadByte();
        LCDC = br.ReadByte();
        STAT = br.ReadByte();
        BGP = br.ReadByte();
        OBP0 = br.ReadByte();
        OBP1 = br.ReadByte();
        BGPI = br.ReadByte();
        BGPD = br.ReadByte();
        OBPI = br.ReadByte();
        OBPD = br.ReadByte();
        SCY = br.ReadByte();
        SCX = br.ReadByte();
        WY = br.ReadByte();
        WX = br.ReadByte();
        OAMDMA = br.ReadByte();
        KEY1 = br.ReadByte();
        VBK = br.ReadByte();
        SVBK = br.ReadByte();
        HDMA1 = br.ReadByte();
        HDMA2 = br.ReadByte();
        HDMA3 = br.ReadByte();
        HDMA4 = br.ReadByte();
        HDMA5 = br.ReadByte();
    }
}
