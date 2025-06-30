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

    public int SpeedMode { get => KEY1.GetBit(7) ? 2 : 1; }
    public bool DMAactive { get => HDMA5.GetBit(7); }
    public bool DMAHBlank { get; set; }

    public GbcIO(GbcTimer timer)
    {
        Timer = timer;
        //Tick = m.Tick;
    }
    public GbcIO() { }

    public void Init(GbcMmu mmu, GbcPpu ppu, GbcApu apu)
    {
        Mmu = mmu;
        Ppu = ppu;
        Apu = apu;
    }

    public byte Read(int a, bool editor = false)
    {
        var io = (byte)a;
        if (io == 0x00)
            return GbcJoypad.Status;
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
                BGPI += (byte)(BGPI.GetBit(7) ? 1 : 0);
            return BGPD;
        }
        else if (io == 0x6a)
            return OBPI;
        else if (io == 0x6b)
        {
            OBPD = Ppu.CGBObjPal[OBPI & 0x3f];
            if (!editor)
                OBPI += (byte)(OBPI.GetBit(7) ? 1 : 0);
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
            GbcJoypad.Status = v;
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
            if ((v.GetBit(3) || v.GetBit(4) || v.GetBit(5)) && !v.GetBit(6))
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
                DMAHBlank = v.GetBit(7);
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
            BGPI += (byte)(BGPI.GetBit(7) ? 1 : 0);
        }
        else if (io == 0x6a)
            OBPI = v;
        else if (io == 0x6b)
        {
            OBPD = v;
            Ppu.SetObjPalette(OBPI, v);
            OBPI += (byte)(OBPI.GetBit(7) ? 1 : 0);
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

    public Dictionary<string, dynamic> GetLCDC() => new()
    {
        ["Background"] = LCDC.GetBit(0),
        ["Sprites"] = LCDC.GetBit(1),
        ["Sprite Size"] = LCDC.GetBit(2) ? "8x16" : "8x8",
        ["BG Map"] = LCDC.GetBit(3) ? "9C00:9FFF" : "9800:9BFF",
        ["BG Tile"] = LCDC.GetBit(4) ? "8000:8FFF" : "8800:97FF",
        ["Window"] = LCDC.GetBit(5),
        ["Window Map"] = LCDC.GetBit(6) ? "9C00:9FFF" : "9800:9BFF",
        ["LCD"] = LCDC.GetBit(7),
    };

    public Dictionary<string, dynamic> GetSTAT() => new()
    {
        ["PPU mode"] = STAT & 3,
        ["LYC == LY"] = STAT.GetBit(2),
        ["Mode 0 select"] = STAT.GetBit(3),
        ["Mode 1 select"] = STAT.GetBit(4),
        ["Mode 2 select"] = STAT.GetBit(5),
        ["LYC select"] = STAT.GetBit(6),
    };

    public Dictionary<string, dynamic> GetIF() => new()
    {
        ["Vblank"] = IF.GetBit(0),
        ["LCD"] = IF.GetBit(1),
        ["Timer"] = IF.GetBit(2),
        ["Serial"] = IF.GetBit(3),
        ["Joypad"] = IF.GetBit(4),
    };

    public Dictionary<string, dynamic> GetIE() => new()
    {
        ["Vblank"] = IF.GetBit(0),
        ["LCD"] = IF.GetBit(1),
        ["Timer"] = IF.GetBit(2),
        ["Serial"] = IF.GetBit(3),
        ["Joypad"] = IF.GetBit(4),
    };

    public Dictionary<string, dynamic> GetChannel1() => new()
    {
        ["Sweep Shift"] = Apu.Square1.SweepShift,
        ["Sweep Negate"] = Apu.Square1.SweepNegate,
        ["Sweep Period"] = Apu.Square1.SweepPeriod,
        ["Length"] = Apu.Square1.LengthCounter,
        ["Duty"] = Apu.Square1.Duty,
        ["Env Period"] = Apu.Square1.EnvPeriod,
        ["Env Increase"] = Apu.Square1.EnvDirection,
        ["Env Volume"] = Apu.Square1.EnvVolume,
        ["Frequency"] = Apu.Square1.Frequency,
        ["Length Enabled"] = Apu.Square1.LengthEnabled,
        ["Enabled"] = Apu.Square1.Enabled,
        ["Timer"] = Apu.Square1.Timer,
        ["Duty Position"] = Apu.Square1.Position,
        ["Sweep Enabled"] = Apu.Square1.SweepPeriod > 0,
        ["Sweep Frequency"] = Apu.Square1.ShadowFrequency,
        ["Sweep Timer"] = Apu.Square1.SweepTimer,
        ["Env Timer"] = Apu.Square1.Duty,
    };

    public Dictionary<string, dynamic> GetChannel2() => new()
    {
        ["Length"] = Apu.Square2.LengthCounter,
        ["Duty"] = Apu.Square2.Duty,
        ["Env Period"] = Apu.Square2.EnvPeriod,
        ["Env Increase"] = Apu.Square2.EnvDirection,
        ["Env Volume"] = Apu.Square2.EnvVolume,
        ["Frequency"] = Apu.Square2.Frequency,
        ["Length Enabled"] = Apu.Square2.LengthEnabled,
        ["Enabled"] = Apu.Square2.Enabled,
        ["Timer"] = Apu.Square2.Timer,
        ["Duty Position"] = Apu.Square2.Position,
        ["Env Timer"] = Apu.Square2.Duty,
    };

    public Dictionary<string, dynamic> GetChannel3() => new()
    {
        ["Sound Enabled"] = Apu.Wave.Dac,
        ["Length"] = Apu.Wave.LengthCounter,
        ["Volume"] = Apu.Wave.VolumeShift,
        ["Frequency"] = Apu.Wave.Frequency,
        ["Length Enabled"] = Apu.Wave.LengthEnabled,
        ["Enabled"] = Apu.Wave.Enabled,
        ["Timer"] = Apu.Wave.Timer,
        ["Position"] = Apu.Wave.Position,
    };

    public Dictionary<string, dynamic> GetChannel4() => new()
    {
        ["Length"] = Apu.Noise.LengthCounter,
        ["Env Period"] = Apu.Noise.EnvPeriod,
        ["Env Increase"] = Apu.Noise.EnvDirection,
        ["Env Volume"] = Apu.Noise.EnvVolume,
        ["Volume"] = Apu.Noise.Shift,
        ["Frequency"] = Apu.Noise.Frequency,
        ["Length Enabled"] = Apu.Noise.LengthEnabled,
        ["Enabled"] = Apu.Noise.Enabled,
        ["Timer"] = Apu.Noise.Timer,
        ["Position"] = Apu.Noise.Position,
    };

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
