namespace GNes.Core;
public partial class NesCpu
{
	//Opcodes
	public const int RTS = 0x00;
	public const int PHP = 0x01;
	public const int SED = 0x02;
	public const int BVS = 0x03;
	public const int TXA = 0x04;
	public const int TAY = 0x05;
	public const int RRA = 0x06;
	public const int LDY = 0x07;
	public const int BPL = 0x08;
	public const int SEC = 0x09;
	public const int DEC = 0x0A;
	public const int BVC = 0x0B;
	public const int PHA = 0x0C;
	public const int LAX = 0x0D;
	public const int AAX = 0x0E;
	public const int AXA = 0x0F;
	public const int SXA = 0x10;
	public const int INC = 0x11;
	public const int BMI = 0x12;
	public const int BNE = 0x13;
	public const int TSX = 0x14;
	public const int BEQ = 0x15;
	public const int JMP = 0x16;
	public const int BRK = 0x17;
	public const int STY = 0x18;
	public const int LSR = 0x19;
	public const int ASL = 0x1A;
	public const int TXS = 0x1B;
	public const int LDX = 0x1C;
	public const int ASR = 0x1D;
	public const int SBC = 0x1E;
	public const int XAS = 0x1F;
	public const int CLI = 0x20;
	public const int CPX = 0x21;
	public const int INX = 0x22;
	public const int ORA = 0x23;
	public const int PLP = 0x24;
	public const int ATX = 0x25;
	public const int DEX = 0x26;
	public const int SYA = 0x27;
	public const int SEI = 0x28;
	public const int BCS = 0x29;
	public const int TYA = 0x2A;
	public const int ROR = 0x2B;
	public const int CLV = 0x2C;
	public const int CPY = 0x2D;
	public const int TOP = 0x2E;
	public const int RTI = 0x2F;
	public const int DCP = 0x30;
	public const int DEY = 0x31;
	public const int ARR = 0x32;
	public const int SRE = 0x33;
	public const int CMP = 0x34;
	public const int RLA = 0x35;
	public const int ADC = 0x36;
	public const int CLC = 0x37;
	public const int EOR = 0x38;
	public const int LAR = 0x39;
	public const int NOP = 0x3A;
	public const int XAA = 0x3B;
	public const int INY = 0x3C;
	public const int ANC = 0x3D;
	public const int KIL = 0x3E;
	public const int PLA = 0x3F;
	public const int AND = 0x40;
	public const int BIT = 0x41;
	public const int CLD = 0x42;
	public const int JSR = 0x43;
	public const int LDA = 0x44;
	public const int ISB = 0x45;
	public const int ROL = 0x46;
	public const int AXS = 0x47;
	public const int STX = 0x48;
	public const int TAX = 0x49;
	public const int SLO = 0x4A;
	public const int STA = 0x4B;
	public const int BCC = 0x4C;

	//AddrMode
	public const int IMPL = 0x00;
	public const int ACCU = 0x01;
	public const int IMME = 0x02;
	public const int ZERP = 0x03;
	public const int ZERX = 0x04;
	public const int ZERY = 0x05;
	public const int ABSO = 0x06;
	public const int ABSX = 0x07;
	public const int ABSY = 0x08;
	public const int INDX = 0x09;
	public const int INDY = 0x0A;
	public const int INDI = 0x0B;
	public const int RELA = 0x0C;
	public const int ERRO = 0x0D;

	public struct Opcode
    {
        public string Name;
        public int Id;
        public int Mode;
        public int Size;
        public int Cycles;
        public int ExtraCycle;

        public Opcode(string name, int id, int mode, int size, int cycles, int extracycle)
        {
            Name = name;
            Id = id;
            Mode = mode;
            Size = size;
            Cycles = cycles;
            ExtraCycle = extracycle;
        }
    };

	public List<Opcode> Disasm = [];

	public void CreateOpcodes() 
	{
		Disasm.Add(new("brk", BRK, IMPL, 1, 7, 0)); //00
		Disasm.Add(new("ora", ORA, INDX, 2, 6, 0)); //01
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //02
		Disasm.Add(new("slo", SLO, INDX, 2, 8, 0)); //03
		Disasm.Add(new("nop", NOP, ZERP, 2, 3, 0)); //04
		Disasm.Add(new("ora", ORA, ZERP, 2, 3, 0)); //05
		Disasm.Add(new("asl", ASL, ZERP, 2, 5, 0)); //06
		Disasm.Add(new("slo", SLO, ZERP, 2, 5, 0)); //07
		Disasm.Add(new("php", PHP, IMPL, 1, 3, 0)); //08
		Disasm.Add(new("ora", ORA, IMME, 2, 2, 0)); //09
		Disasm.Add(new("asl A", ASL, ACCU, 1, 2, 0)); //0A
		Disasm.Add(new("anc", ANC, IMME, 2, 2, 0)); //0B
		Disasm.Add(new("top", TOP, ABSO, 3, 4, 0)); //0C
		Disasm.Add(new("ora", ORA, ABSO, 3, 4, 0)); //0D
		Disasm.Add(new("asl", ASL, ABSO, 3, 6, 0)); //0E
		Disasm.Add(new("slo", SLO, ABSO, 3, 6, 0)); //0F
		Disasm.Add(new("bpl", BPL, RELA, 2, 2, 1)); //10
		Disasm.Add(new("ora", ORA, INDY, 2, 5, 1)); //11
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //12
		Disasm.Add(new("slo", SLO, INDY, 2, 8, 0)); //13
		Disasm.Add(new("nop", NOP, ZERX, 2, 4, 0)); //14
		Disasm.Add(new("ora", ORA, ZERX, 2, 4, 0)); //15
		Disasm.Add(new("asl", ASL, ZERX, 2, 6, 0)); //16
		Disasm.Add(new("slo", SLO, ZERX, 2, 6, 0)); //17
		Disasm.Add(new("clc", CLC, IMPL, 1, 2, 0)); //18
		Disasm.Add(new("ora", ORA, ABSY, 3, 4, 1)); //19
		Disasm.Add(new("nop", NOP, IMPL, 1, 2, 0)); //1A
		Disasm.Add(new("slo", SLO, ABSY, 3, 7, 0)); //1B
		Disasm.Add(new("top", TOP, ABSX, 3, 4, 1)); //1C
		Disasm.Add(new("ora", ORA, ABSX, 3, 4, 1)); //1D
		Disasm.Add(new("asl", ASL, ABSX, 3, 7, 0)); //1E
		Disasm.Add(new("slo", SLO, ABSX, 3, 7, 0)); //1F
		Disasm.Add(new("jsr", JSR, ABSO, 3, 6, 0)); //20
		Disasm.Add(new("and", AND, INDX, 2, 6, 0)); //21
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //22
		Disasm.Add(new("rla", RLA, INDX, 2, 8, 0)); //23
		Disasm.Add(new("bit", BIT, ZERP, 2, 3, 0)); //24
		Disasm.Add(new("and", AND, ZERP, 2, 3, 0)); //25
		Disasm.Add(new("rol", ROL, ZERP, 2, 5, 0)); //26
		Disasm.Add(new("rla", RLA, ZERP, 2, 5, 0)); //27
		Disasm.Add(new("plp", PLP, IMPL, 1, 4, 0)); //28
		Disasm.Add(new("and", AND, IMME, 2, 2, 0)); //29
		Disasm.Add(new("rol A", ROL, ACCU, 1, 2, 0)); //2A
		Disasm.Add(new("anc", ANC, IMME, 2, 2, 0)); //2B
		Disasm.Add(new("bit", BIT, ABSO, 3, 4, 0)); //2C
		Disasm.Add(new("and", AND, ABSO, 3, 4, 0)); //2D
		Disasm.Add(new("rol", ROL, ABSO, 3, 6, 0)); //2E
		Disasm.Add(new("rla", RLA, ABSO, 3, 6, 0)); //2F
		Disasm.Add(new("bmi", BMI, RELA, 2, 2, 1)); //30
		Disasm.Add(new("and", AND, INDY, 2, 5, 1)); //31
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //32
		Disasm.Add(new("rla", RLA, INDY, 2, 8, 0)); //33
		Disasm.Add(new("nop", NOP, ZERX, 2, 4, 0)); //34
		Disasm.Add(new("and", AND, ZERX, 2, 4, 0)); //35
		Disasm.Add(new("rol", ROL, ZERX, 2, 6, 0)); //36
		Disasm.Add(new("rla", RLA, ZERX, 2, 6, 0)); //37
		Disasm.Add(new("sec", SEC, IMPL, 1, 2, 0)); //38
		Disasm.Add(new("and", AND, ABSY, 3, 4, 1)); //39
		Disasm.Add(new("nop", NOP, IMPL, 1, 2, 0)); //3A
		Disasm.Add(new("rla", RLA, ABSY, 3, 7, 0)); //3B
		Disasm.Add(new("top", TOP, ABSX, 3, 4, 1)); //3C
		Disasm.Add(new("and", AND, ABSX, 3, 4, 1)); //3D
		Disasm.Add(new("rol", ROL, ABSX, 3, 7, 0)); //3E
		Disasm.Add(new("rla", RLA, ABSX, 3, 7, 0)); //3F
		Disasm.Add(new("rti", RTI, IMPL, 1, 6, 0)); //40
		Disasm.Add(new("eor", EOR, INDX, 2, 6, 0)); //41
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //42
		Disasm.Add(new("sre", SRE, INDX, 2, 8, 0)); //43
		Disasm.Add(new("nop", NOP, ZERP, 2, 3, 0)); //44
		Disasm.Add(new("eor", EOR, ZERP, 2, 3, 0)); //45
		Disasm.Add(new("lsr", LSR, ZERP, 2, 5, 0)); //46
		Disasm.Add(new("sre", SRE, ZERP, 2, 5, 0)); //47
		Disasm.Add(new("pha", PHA, IMPL, 1, 3, 0)); //48
		Disasm.Add(new("eor", EOR, IMME, 2, 2, 0)); //49
		Disasm.Add(new("lsr A", LSR, ACCU, 1, 2, 0)); //4A
		Disasm.Add(new("asr", ASR, IMME, 2, 2, 0)); //4B
		Disasm.Add(new("jmp", JMP, ABSO, 3, 3, 0)); //4C
		Disasm.Add(new("eor", EOR, ABSO, 3, 4, 0)); //4D
		Disasm.Add(new("lsr", LSR, ABSO, 3, 6, 0)); //4E
		Disasm.Add(new("sre", SRE, ABSO, 3, 6, 0)); //4F
		Disasm.Add(new("bvc", BVC, RELA, 2, 2, 1)); //50
		Disasm.Add(new("eor", EOR, INDY, 2, 5, 1)); //51
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //52
		Disasm.Add(new("sre", SRE, INDY, 2, 8, 0)); //53
		Disasm.Add(new("nop", NOP, ZERX, 2, 4, 0)); //54
		Disasm.Add(new("eor", EOR, ZERX, 2, 4, 0)); //55
		Disasm.Add(new("lsr", LSR, ZERX, 2, 6, 0)); //56
		Disasm.Add(new("sre", SRE, ZERX, 2, 6, 0)); //57
		Disasm.Add(new("cli", CLI, IMPL, 1, 2, 0)); //58
		Disasm.Add(new("eor", EOR, ABSY, 3, 4, 1)); //59
		Disasm.Add(new("nop", NOP, IMPL, 1, 2, 0)); //5A
		Disasm.Add(new("sre", SRE, ABSY, 3, 7, 0)); //5B
		Disasm.Add(new("top", TOP, ABSX, 3, 4, 1)); //5C
		Disasm.Add(new("eor", EOR, ABSX, 3, 4, 1)); //5D
		Disasm.Add(new("lsr", LSR, ABSX, 3, 7, 0)); //5E
		Disasm.Add(new("sre", SRE, ABSX, 3, 7, 0)); //5F
		Disasm.Add(new("rts", RTS, IMPL, 1, 6, 0)); //60
		Disasm.Add(new("adc", ADC, INDX, 2, 6, 0)); //61
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //62
		Disasm.Add(new("rra", RRA, INDX, 2, 8, 0)); //63
		Disasm.Add(new("nop", NOP, ZERP, 2, 3, 0)); //64
		Disasm.Add(new("adc", ADC, ZERP, 2, 3, 0)); //65
		Disasm.Add(new("ror", ROR, ZERP, 2, 5, 0)); //66
		Disasm.Add(new("rra", RRA, ZERP, 2, 5, 0)); //67
		Disasm.Add(new("pla", PLA, IMPL, 1, 4, 0)); //68
		Disasm.Add(new("adc", ADC, IMME, 2, 2, 0)); //69
		Disasm.Add(new("ror A", ROR, ACCU, 1, 2, 0)); //6A
		Disasm.Add(new("arr", ARR, IMME, 2, 2, 0)); //6B
		Disasm.Add(new("jmp", JMP, INDI, 3, 5, 0)); //6C
		Disasm.Add(new("adc", ADC, ABSO, 3, 4, 0)); //6D
		Disasm.Add(new("ror", ROR, ABSO, 3, 6, 0)); //6E
		Disasm.Add(new("rra", RRA, ABSO, 3, 6, 0)); //6F
		Disasm.Add(new("bvs", BVS, RELA, 2, 2, 1)); //70
		Disasm.Add(new("adc", ADC, INDY, 2, 5, 1)); //71
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //72
		Disasm.Add(new("rra", RRA, INDY, 2, 8, 0)); //73
		Disasm.Add(new("nop", NOP, ZERX, 2, 4, 0)); //74
		Disasm.Add(new("adc", ADC, ZERX, 2, 4, 0)); //75
		Disasm.Add(new("ror", ROR, ZERX, 2, 6, 0)); //76
		Disasm.Add(new("rra", RRA, ZERX, 2, 6, 0)); //77
		Disasm.Add(new("sei", SEI, IMPL, 1, 2, 0)); //78
		Disasm.Add(new("adc", ADC, ABSY, 3, 4, 1)); //79
		Disasm.Add(new("nop", NOP, IMPL, 1, 2, 0)); //7A
		Disasm.Add(new("rra", RRA, ABSY, 3, 7, 0)); //7B
		Disasm.Add(new("top", TOP, ABSX, 3, 4, 1)); //7C
		Disasm.Add(new("adc", ADC, ABSX, 3, 4, 1)); //7D
		Disasm.Add(new("ror", ROR, ABSX, 3, 7, 0)); //7E
		Disasm.Add(new("rra", RRA, ABSX, 3, 7, 0)); //7F
		Disasm.Add(new("nop", NOP, IMME, 2, 2, 0)); //80
		Disasm.Add(new("sta", STA, INDX, 2, 6, 0)); //81
		Disasm.Add(new("nop", NOP, IMME, 2, 2, 0)); //82
		Disasm.Add(new("aax", AAX, INDX, 2, 6, 0)); //83
		Disasm.Add(new("sty", STY, ZERP, 2, 3, 0)); //84
		Disasm.Add(new("sta", STA, ZERP, 2, 3, 0)); //85
		Disasm.Add(new("stx", STX, ZERP, 2, 3, 0)); //86
		Disasm.Add(new("aax", AAX, ZERP, 2, 3, 0)); //87
		Disasm.Add(new("dey", DEY, IMPL, 1, 2, 0)); //88
		Disasm.Add(new("nop", NOP, IMME, 2, 2, 0)); //89
		Disasm.Add(new("txa", TXA, IMPL, 1, 2, 0)); //8A
		Disasm.Add(new("xaa", XAA, IMME, 2, 2, 0)); //8B
		Disasm.Add(new("sty", STY, ABSO, 3, 4, 0)); //8C
		Disasm.Add(new("sta", STA, ABSO, 3, 4, 0)); //8D
		Disasm.Add(new("stx", STX, ABSO, 3, 4, 0)); //8E
		Disasm.Add(new("aax", AAX, ABSO, 3, 4, 0)); //8F
		Disasm.Add(new("bcc", BCC, RELA, 2, 2, 0)); //90
		Disasm.Add(new("sta", STA, INDY, 2, 6, 1)); //91
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //92
		Disasm.Add(new("axa", AXA, ABSY, 2, 6, 0)); //93
		Disasm.Add(new("sty", STY, ZERX, 2, 4, 0)); //94
		Disasm.Add(new("sta", STA, ZERX, 2, 4, 0)); //95
		Disasm.Add(new("stx", STX, ZERY, 2, 4, 0)); //96
		Disasm.Add(new("aax", AAX, ZERY, 2, 4, 0)); //97
		Disasm.Add(new("tya", TYA, IMPL, 1, 2, 0)); //98
		Disasm.Add(new("sta", STA, ABSY, 3, 5, 0)); //99
		Disasm.Add(new("txs", TXS, IMPL, 1, 2, 0)); //9A
		Disasm.Add(new("xas", XAS, ABSY, 3, 5, 0)); //9B
		Disasm.Add(new("sya", SYA, ABSX, 3, 5, 0)); //9C
		Disasm.Add(new("sta", STA, ABSX, 3, 5, 0)); //9D
		Disasm.Add(new("sxa", SXA, ABSY, 3, 5, 0)); //9E
		Disasm.Add(new("axa", AXA, ABSY, 3, 5, 0)); //9F
		Disasm.Add(new("ldy", LDY, IMME, 2, 2, 0)); //A0
		Disasm.Add(new("lda", LDA, INDX, 2, 6, 0)); //A1
		Disasm.Add(new("ldx", LDX, IMME, 2, 2, 0)); //A2
		Disasm.Add(new("lax", LAX, INDX, 2, 6, 0)); //A3
		Disasm.Add(new("ldy", LDY, ZERP, 2, 3, 0)); //A4
		Disasm.Add(new("lda", LDA, ZERP, 2, 3, 0)); //A5
		Disasm.Add(new("ldx", LDX, ZERP, 2, 3, 0)); //A6
		Disasm.Add(new("lax", LAX, ZERP, 2, 3, 0)); //A7
		Disasm.Add(new("tay", TAY, IMPL, 1, 2, 0)); //A8
		Disasm.Add(new("lda", LDA, IMME, 2, 2, 0)); //A9
		Disasm.Add(new("tax", TAX, IMPL, 1, 2, 0)); //AA
		Disasm.Add(new("atx", ATX, IMME, 2, 2, 0)); //AB
		Disasm.Add(new("ldy", LDY, ABSO, 3, 4, 0)); //AC
		Disasm.Add(new("lda", LDA, ABSO, 3, 4, 0)); //AD
		Disasm.Add(new("ldx", LDX, ABSO, 3, 4, 0)); //AE
		Disasm.Add(new("lax", LAX, ABSO, 3, 4, 0)); //AF
		Disasm.Add(new("bcs", BCS, RELA, 2, 2, 1)); //B0
		Disasm.Add(new("lda", LDA, INDY, 2, 5, 1)); //B1
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //B2
		Disasm.Add(new("lax", LAX, INDY, 2, 5, 0)); //B3
		Disasm.Add(new("ldy", LDY, ZERX, 2, 4, 0)); //B4
		Disasm.Add(new("lda", LDA, ZERX, 2, 4, 0)); //B5
		Disasm.Add(new("ldx", LDX, ZERY, 2, 4, 0)); //B6
		Disasm.Add(new("lax", LAX, ZERY, 2, 4, 0)); //B7
		Disasm.Add(new("clv", CLV, IMPL, 1, 2, 0)); //B8
		Disasm.Add(new("lda", LDA, ABSY, 3, 4, 1)); //B9
		Disasm.Add(new("tsx", TSX, IMPL, 1, 2, 0)); //BA
		Disasm.Add(new("lar", LAR, ABSY, 3, 4, 1)); //BB
		Disasm.Add(new("ldy", LDY, ABSX, 3, 4, 1)); //BC
		Disasm.Add(new("lda", LDA, ABSX, 3, 4, 1)); //BD
		Disasm.Add(new("ldx", LDX, ABSY, 3, 4, 1)); //BE
		Disasm.Add(new("lax", LAX, ABSY, 3, 4, 1)); //BF
		Disasm.Add(new("cpy", CPY, IMME, 2, 2, 0)); //C0
		Disasm.Add(new("cmp", CMP, INDX, 2, 6, 0)); //C1
		Disasm.Add(new("nop", NOP, IMME, 2, 2, 0)); //C2
		Disasm.Add(new("dcp", DCP, INDX, 2, 8, 0)); //C3
		Disasm.Add(new("cpy", CPY, ZERP, 2, 3, 0)); //C4
		Disasm.Add(new("cmp", CMP, ZERP, 2, 3, 0)); //C5
		Disasm.Add(new("dec", DEC, ZERP, 2, 5, 0)); //C6
		Disasm.Add(new("dcp", DCP, ZERP, 2, 5, 0)); //C7
		Disasm.Add(new("iny", INY, IMPL, 1, 2, 0)); //C8
		Disasm.Add(new("cmp", CMP, IMME, 2, 2, 0)); //C9
		Disasm.Add(new("dex", DEX, IMPL, 1, 2, 0)); //CA
		Disasm.Add(new("axs", AXS, IMME, 2, 2, 0)); //CB
		Disasm.Add(new("cpy", CPY, ABSO, 3, 4, 0)); //CC
		Disasm.Add(new("cmp", CMP, ABSO, 3, 4, 0)); //CD
		Disasm.Add(new("dec", DEC, ABSO, 3, 6, 0)); //CE
		Disasm.Add(new("dcp", DCP, ABSO, 3, 6, 0)); //CF
		Disasm.Add(new("bne", BNE, RELA, 2, 2, 1)); //D0
		Disasm.Add(new("cmp", CMP, INDY, 2, 5, 1)); //D1
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //D2
		Disasm.Add(new("dcp", DCP, INDY, 2, 8, 0)); //D3
		Disasm.Add(new("nop", NOP, ZERX, 2, 4, 0)); //D4
		Disasm.Add(new("cmp", CMP, ZERX, 2, 4, 0)); //D5
		Disasm.Add(new("dec", DEC, ZERX, 2, 6, 0)); //D6
		Disasm.Add(new("dcp", DCP, ZERX, 2, 6, 0)); //D7
		Disasm.Add(new("cld", CLD, IMPL, 1, 2, 0)); //D8
		Disasm.Add(new("cmp", CMP, ABSY, 3, 4, 1)); //D9
		Disasm.Add(new("nop", NOP, IMPL, 1, 2, 0)); //DA
		Disasm.Add(new("dcp", DCP, ABSY, 3, 7, 0)); //DB
		Disasm.Add(new("top", TOP, ABSX, 3, 4, 1)); //DC
		Disasm.Add(new("cmp", CMP, ABSX, 3, 4, 1)); //DD
		Disasm.Add(new("dec", DEC, ABSX, 3, 7, 0)); //DE
		Disasm.Add(new("dcp", DCP, ABSX, 3, 7, 0)); //DF
		Disasm.Add(new("cpx", CPX, IMME, 2, 2, 0)); //E0
		Disasm.Add(new("sbc", SBC, INDX, 2, 6, 0)); //E1
		Disasm.Add(new("nop", NOP, IMME, 2, 2, 0)); //E2
		Disasm.Add(new("isb", ISB, INDX, 2, 8, 0)); //E3
		Disasm.Add(new("cpx", CPX, ZERP, 2, 3, 0)); //E4
		Disasm.Add(new("sbc", SBC, ZERP, 2, 3, 0)); //E5
		Disasm.Add(new("inc", INC, ZERP, 2, 5, 0)); //E6
		Disasm.Add(new("isb", ISB, ZERP, 2, 5, 0)); //E7
		Disasm.Add(new("inx", INX, IMPL, 1, 2, 0)); //E8
		Disasm.Add(new("sbc", SBC, IMME, 2, 2, 0)); //E9
		Disasm.Add(new("nop", NOP, IMPL, 1, 2, 0)); //EA
		Disasm.Add(new("sbc", SBC, IMME, 2, 2, 0)); //EB
		Disasm.Add(new("cpx", CPX, ABSO, 3, 4, 0)); //EC
		Disasm.Add(new("sbc", SBC, ABSO, 3, 4, 0)); //ED
		Disasm.Add(new("inc", INC, ABSO, 3, 6, 0)); //EE
		Disasm.Add(new("isb", ISB, ABSO, 3, 6, 0)); //EF
		Disasm.Add(new("beq", BEQ, RELA, 2, 2, 1)); //F0
		Disasm.Add(new("sbc", SBC, INDY, 2, 5, 1)); //F1
		Disasm.Add(new("kil", KIL, IMPL, 1, 0, 0)); //F2
		Disasm.Add(new("isb", ISB, INDY, 2, 8, 0)); //F3
		Disasm.Add(new("nop", NOP, ZERX, 2, 4, 0)); //F4
		Disasm.Add(new("sbc", SBC, ZERX, 2, 4, 0)); //F5
		Disasm.Add(new("inc", INC, ZERX, 2, 6, 0)); //F6
		Disasm.Add(new("isb", ISB, ZERX, 2, 6, 0)); //F7
		Disasm.Add(new("sed", SED, IMPL, 1, 2, 0)); //F8
		Disasm.Add(new("sbc", SBC, ABSY, 3, 4, 1)); //F9
		Disasm.Add(new("nop", NOP, IMPL, 1, 2, 0)); //FA
		Disasm.Add(new("isb", ISB, ABSY, 3, 7, 0)); //FB
		Disasm.Add(new("top", TOP, ABSX, 3, 4, 1)); //FC
		Disasm.Add(new("sbc", SBC, ABSX, 3, 4, 1)); //FD
		Disasm.Add(new("inc", INC, ABSX, 3, 7, 0)); //FE
		Disasm.Add(new("isb", ISB, ABSX, 3, 7, 0)); //FF
	}
}