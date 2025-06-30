using System.Text.Json.Serialization;

namespace Gmulator.Core.Snes;
public partial class SnesCpu
{
	public struct Opcode(string name, string oper, int mode, int id, bool imm, int size, int cycles)
    {
        public string Name = name;
        public string Oper = oper;
        public int Mode = mode;
        public int Id = id;
        public bool Immediate = imm;
        public int Size = size;
        public int Cycles = cycles;
    };

	[JsonIgnore]
	public List<Opcode> Disasm = [];

	public void CreateOpcodes() 
	{
		Disasm.Add(new("brk", "", StackInterrupt, BRK, false, 2, 7)); //00
		Disasm.Add(new("ora", "(${0:x2},x)", DPIndexedIndirectX, ORA, false, 2, 6)); //01
		Disasm.Add(new("cop", "#${0:x2}", StackInterrupt, COP, false, 2, 7)); //02
		Disasm.Add(new("ora", "sr,s", StackRelative, ORA, false, 2, 4)); //03
		Disasm.Add(new("tsb", "${0:x2}", DirectPage, TSB, false, 2, 5)); //04
		Disasm.Add(new("ora", "${0:x2}", DirectPage, ORA, false, 2, 3)); //05
		Disasm.Add(new("asl", "${0:x2}", DirectPage, ASL, false, 2, 5)); //06
		Disasm.Add(new("ora", "[${0:x2}]", DPIndirectLong, ORA, false, 2, 6)); //07
		Disasm.Add(new("php", "", StackPush, PHP, false, 1, 3)); //08
		Disasm.Add(new("ora", "#${0:x2}", ImmediateMemory, ORA, true, 2, 2)); //09
		Disasm.Add(new("asl", "a", Accumulator, ASL, false, 1, 2)); //0A
		Disasm.Add(new("phd", "", StackPush, PHD, false, 1, 4)); //0B
		Disasm.Add(new("tsb", "${0:x4}", Absolute, TSB, false, 3, 6)); //0C
		Disasm.Add(new("ora", "${0:x4}", Absolute, ORA, false, 3, 4)); //0D
		Disasm.Add(new("asl", "${0:x4}", Absolute, ASL, false, 3, 6)); //0E
		Disasm.Add(new("ora", "${0:x6}", AbsoluteLong, ORA, false, 4, 5)); //0F
		Disasm.Add(new("bpl", "${0:x4}", ProgramCounterRelative, BPL, false, 2, 2)); //10
		Disasm.Add(new("ora", "(${0:x2}),y", DPIndirectIndexedY, ORA, false, 2, 5)); //11
		Disasm.Add(new("ora", "(${0:x2})", DPIndirect, ORA, false, 2, 5)); //12
		Disasm.Add(new("ora", "(sr,s),y", SRIndirectIndexedY, ORA, false, 2, 7)); //13
		Disasm.Add(new("trb", "${0:x2}", DirectPage, TRB, false, 2, 5)); //14
		Disasm.Add(new("ora", "${0:x2},x", DPIndexedX, ORA, false, 2, 4)); //15
		Disasm.Add(new("asl", "${0:x2},x", DPIndexedX, ASL, false, 2, 6)); //16
		Disasm.Add(new("ora", "[${0:x2}],y", DPIndirectLongIndexedY, ORA, false, 2, 6)); //17
		Disasm.Add(new("clc", "", Implied, CLC, false, 1, 2)); //18
		Disasm.Add(new("ora", "${0:x4},y", AbsoluteIndexedY, ORA, false, 3, 4)); //19
		Disasm.Add(new("inc", "a", Accumulator, INC, false, 1, 2)); //1A
		Disasm.Add(new("tcs", "", Implied, TCS, false, 1, 2)); //1B
		Disasm.Add(new("trb", "${0:x4}", Absolute, TRB, false, 3, 6)); //1C
		Disasm.Add(new("ora", "${0:x4},x", AbsoluteIndexedX, ORA, false, 3, 4)); //1D
		Disasm.Add(new("asl", "${0:x4},x", AbsoluteIndexedX, ASL, false, 3, 7)); //1E
		Disasm.Add(new("ora", " ${0:x6},x", AbsoluteLongIndexedX, ORA, false, 4, 5)); //1F
		Disasm.Add(new("jsr", "${0:x4}", Absolute, JSR, false, 3, 6)); //20
		Disasm.Add(new("and", "(${0:x2},x)", DPIndexedIndirectX, AND, false, 2, 6)); //21
		Disasm.Add(new("jsl", "${0:x6}", AbsoluteLong, JSL, false, 4, 8)); //22
		Disasm.Add(new("and", "sr,s", StackRelative, AND, false, 2, 4)); //23
		Disasm.Add(new("bit", "${0:x2}", DirectPage, BIT, false, 2, 3)); //24
		Disasm.Add(new("and", "${0:x2}", DirectPage, AND, false, 2, 3)); //25
		Disasm.Add(new("rol", "${0:x2}", DirectPage, ROL, false, 2, 5)); //26
		Disasm.Add(new("and", "[${0:x2}]", DPIndirectLong, AND, false, 2, 6)); //27
		Disasm.Add(new("plp", "", StackPull, PLP, false, 1, 4)); //28
		Disasm.Add(new("and", "#${0:x2}", ImmediateMemory, AND, true, 2, 2)); //29
		Disasm.Add(new("rol", "a", Accumulator, ROL, false, 1, 2)); //2A
		Disasm.Add(new("pld", "", StackPull, PLD, false, 1, 5)); //2B
		Disasm.Add(new("bit", "${0:x4}", Absolute, BIT, false, 3, 4)); //2C
		Disasm.Add(new("and", "${0:x4}", Absolute, AND, false, 3, 4)); //2D
		Disasm.Add(new("rol", "${0:x4}", Absolute, ROL, false, 3, 6)); //2E
		Disasm.Add(new("and", "${0:x6}", AbsoluteLong, AND, false, 4, 5)); //2F
		Disasm.Add(new("bmi", "${0:x4}", ProgramCounterRelative, BMI, false, 2, 2)); //30
		Disasm.Add(new("and", "(${0:x2}),y", DPIndirectIndexedY, AND, false, 2, 5)); //31
		Disasm.Add(new("and", "(${0:x2})", DPIndirect, AND, false, 2, 5)); //32
		Disasm.Add(new("and", "(sr,s),y", SRIndirectIndexedY, AND, false, 2, 7)); //33
		Disasm.Add(new("bit", "${0:x2},x", DPIndexedX, BIT, false, 2, 4)); //34
		Disasm.Add(new("and", "${0:x2},x", DPIndexedX, AND, false, 2, 4)); //35
		Disasm.Add(new("rol", "${0:x2},x", DPIndexedX, ROL, false, 2, 6)); //36
		Disasm.Add(new("and", "[${0:x2}],y", DPIndirectLongIndexedY, AND, false, 2, 6)); //37
		Disasm.Add(new("sec", "", Implied, SEC, false, 1, 2)); //38
		Disasm.Add(new("and", "${0:x4},y", AbsoluteIndexedY, AND, false, 3, 4)); //39
		Disasm.Add(new("dec", "a", Accumulator, DEC, false, 1, 2)); //3A
		Disasm.Add(new("tsc", "", Implied, TSC, false, 1, 2)); //3B
		Disasm.Add(new("bit", "${0:x4},x", AbsoluteIndexedX, BIT, false, 3, 4)); //3C
		Disasm.Add(new("and", "${0:x4},x", AbsoluteIndexedX, AND, false, 3, 4)); //3D
		Disasm.Add(new("rol", "${0:x4},x", AbsoluteIndexedX, ROL, false, 3, 7)); //3E
		Disasm.Add(new("and", " ${0:x6},x", AbsoluteLongIndexedX, AND, false, 4, 5)); //3F
		Disasm.Add(new("rti", "", StackRTI, RTI, false, 1, 6)); //40
		Disasm.Add(new("eor", "(${0:x2},x)", DPIndexedIndirectX, EOR, false, 2, 6)); //41
		Disasm.Add(new("wdm", "", NoMode, WDM, false, 2, 0)); //42
		Disasm.Add(new("eor", "sr,s", StackRelative, EOR, false, 2, 4)); //43
		Disasm.Add(new("mvp", "srcbk,destbk", BlockMove, MVP, false, 3, 1)); //44
		Disasm.Add(new("eor", "${0:x2}", DirectPage, EOR, false, 2, 3)); //45
		Disasm.Add(new("lsr", "${0:x2}", DirectPage, LSR, false, 2, 5)); //46
		Disasm.Add(new("eor", "[${0:x2}]", DPIndirectLong, EOR, false, 2, 6)); //47
		Disasm.Add(new("pha", "", StackPush, PHA, false, 1, 3)); //48
		Disasm.Add(new("eor", "#${0:x2}", ImmediateMemory, EOR, true, 2, 2)); //49
		Disasm.Add(new("lsr", "a", Accumulator, LSR, false, 1, 2)); //4A
		Disasm.Add(new("phk", "", StackPush, PHK, false, 1, 3)); //4B
		Disasm.Add(new("jmp", "${0:x4}", Absolute, JMP, false, 3, 3)); //4C
		Disasm.Add(new("eor", "${0:x4}", Absolute, EOR, false, 3, 4)); //4D
		Disasm.Add(new("lsr", "${0:x4}", Absolute, LSR, false, 3, 6)); //4E
		Disasm.Add(new("eor", "${0:x6}", AbsoluteLong, EOR, false, 4, 5)); //4F
		Disasm.Add(new("bvc", "${0:x4}", ProgramCounterRelative, BVC, false, 2, 2)); //50
		Disasm.Add(new("eor", "(${0:x2}),y", DPIndirectIndexedY, EOR, false, 2, 5)); //51
		Disasm.Add(new("eor", "(${0:x2})", DPIndirect, EOR, false, 2, 5)); //52
		Disasm.Add(new("eor", "(sr,s),y", SRIndirectIndexedY, EOR, false, 2, 7)); //53
		Disasm.Add(new("mvn", "srcbk,destbk", BlockMove, MVN, false, 3, 1)); //54
		Disasm.Add(new("eor", "${0:x2},x", DPIndexedX, EOR, false, 2, 4)); //55
		Disasm.Add(new("lsr", "${0:x2},x", DPIndexedX, LSR, false, 2, 6)); //56
		Disasm.Add(new("eor", "[${0:x2}],y", DPIndirectLongIndexedY, EOR, false, 2, 6)); //57
		Disasm.Add(new("cli", "", Implied, CLI, false, 1, 2)); //58
		Disasm.Add(new("eor", "${0:x4},y", AbsoluteIndexedY, EOR, false, 3, 4)); //59
		Disasm.Add(new("phy", "", StackPush, PHY, false, 1, 3)); //5A
		Disasm.Add(new("tcd", "", Implied, TCD, false, 1, 2)); //5B
		Disasm.Add(new("jml", "${0:x6}", AbsoluteLong, JML, false, 4, 4)); //5C
		Disasm.Add(new("eor", "${0:x4},x", AbsoluteIndexedX, EOR, false, 3, 4)); //5D
		Disasm.Add(new("lsr", "${0:x4},x", AbsoluteIndexedX, LSR, false, 3, 7)); //5E
		Disasm.Add(new("eor", " ${0:x6},x", AbsoluteLongIndexedX, EOR, false, 4, 5)); //5F
		Disasm.Add(new("rts", "", StackRTS, RTS, false, 1, 6)); //60
		Disasm.Add(new("adc", "(${0:x2},x)", DPIndexedIndirectX, ADC, false, 2, 6)); //61
		Disasm.Add(new("per", "label", StackPCRelativeLong, PER, false, 3, 6)); //62
		Disasm.Add(new("adc", "sr,s", StackRelative, ADC, false, 2, 4)); //63
		Disasm.Add(new("stz", "${0:x2}", DirectPage, STZ, false, 2, 3)); //64
		Disasm.Add(new("adc", "${0:x2}", DirectPage, ADC, false, 2, 3)); //65
		Disasm.Add(new("ror", "${0:x2}", DirectPage, ROR, false, 2, 5)); //66
		Disasm.Add(new("adc", "[${0:x2}]", DPIndirectLong, ADC, false, 2, 6)); //67
		Disasm.Add(new("pla", "", StackPull, PLA, false, 1, 4)); //68
		Disasm.Add(new("adc", "#${0:x2}", ImmediateMemory, ADC, true, 2, 2)); //69
		Disasm.Add(new("ror", "a", Accumulator, ROR, false, 1, 2)); //6A
		Disasm.Add(new("rtl", "", StackRTL, RTL, false, 1, 6)); //6B
		Disasm.Add(new("jmp", "(${0:x4})", AbsoluteIndirect, JMP, false, 3, 5)); //6C
		Disasm.Add(new("adc", "${0:x4}", Absolute, ADC, false, 3, 4)); //6D
		Disasm.Add(new("ror", "${0:x4}", Absolute, ROR, false, 3, 6)); //6E
		Disasm.Add(new("adc", "${0:x6}", AbsoluteLong, ADC, false, 4, 5)); //6F
		Disasm.Add(new("bvs", "${0:x4}", ProgramCounterRelative, BVS, false, 2, 2)); //70
		Disasm.Add(new("adc", "( ${0:x2}),y", DPIndirectIndexedY, ADC, false, 2, 5)); //71
		Disasm.Add(new("adc", "(${0:x2})", DPIndirect, ADC, false, 2, 5)); //72
		Disasm.Add(new("adc", "(sr,s),y", SRIndirectIndexedY, ADC, false, 2, 7)); //73
		Disasm.Add(new("stz", "${0:x2},x", DPIndexedX, STZ, false, 2, 4)); //74
		Disasm.Add(new("adc", "${0:x2},x", DPIndexedX, ADC, false, 2, 4)); //75
		Disasm.Add(new("ror", "${0:x2},x", DPIndexedX, ROR, false, 2, 6)); //76
		Disasm.Add(new("adc", "[${0:x2}],y", DPIndirectLongIndexedY, ADC, false, 2, 6)); //77
		Disasm.Add(new("sei", "", Implied, SEI, false, 1, 2)); //78
		Disasm.Add(new("adc", "${0:x4},y", AbsoluteIndexedY, ADC, false, 3, 4)); //79
		Disasm.Add(new("ply", "", StackPull, PLY, false, 1, 4)); //7A
		Disasm.Add(new("tdc", "", Implied, TDC, false, 1, 2)); //7B
		Disasm.Add(new("jmp", "(${0:x4},x)", AbsoluteIndexedIndirect, JMP, false, 3, 6)); //7C
		Disasm.Add(new("adc", "${0:x4},x", AbsoluteIndexedX, ADC, false, 3, 4)); //7D
		Disasm.Add(new("ror", "${0:x4},x", AbsoluteIndexedX, ROR, false, 3, 7)); //7E
		Disasm.Add(new("adc", " ${0:x6},x", AbsoluteLongIndexedX, ADC, false, 4, 5)); //7F
		Disasm.Add(new("bra", "${0:x4}", ProgramCounterRelative, BRA, false, 2, 3)); //80
		Disasm.Add(new("sta", "(${0:x2},x)", DPIndexedIndirectX, STA, false, 2, 6)); //81
		Disasm.Add(new("brl", "label", ProgramCounterRelativeLong, BRL, false, 3, 4)); //82
		Disasm.Add(new("sta", "sr,s", StackRelative, STA, false, 2, 4)); //83
		Disasm.Add(new("sty", "${0:x2}", DirectPage, STY, false, 2, 3)); //84
		Disasm.Add(new("sta", "${0:x2}", DirectPage, STA, false, 2, 3)); //85
		Disasm.Add(new("stx", "${0:x2}", DirectPage, STX, false, 2, 3)); //86
		Disasm.Add(new("sta", "[${0:x2}]", DPIndirectLong, STA, false, 2, 6)); //87
		Disasm.Add(new("dey", "", Implied, DEY, false, 1, 2)); //88
		Disasm.Add(new("bit", "#${0:x2}", ImmediateMemory, BIT, true, 2, 2)); //89
		Disasm.Add(new("txa", "", Implied, TXA, false, 1, 2)); //8A
		Disasm.Add(new("phb", "", StackPush, PHB, false, 1, 3)); //8B
		Disasm.Add(new("sty", "${0:x4}", Absolute, STY, false, 3, 4)); //8C
		Disasm.Add(new("sta", "${0:x4}", Absolute, STA, false, 3, 4)); //8D
		Disasm.Add(new("stx", "${0:x4}", Absolute, STX, false, 3, 4)); //8E
		Disasm.Add(new("sta", "${0:x6}", AbsoluteLong, STA, false, 4, 5)); //8F
		Disasm.Add(new("bcc", "${0:x4}", ProgramCounterRelative, BCC, false, 2, 2)); //90
		Disasm.Add(new("sta", "(${0:x2}),y", DPIndirectIndexedY, STA, false, 2, 6)); //91
		Disasm.Add(new("sta", "(${0:x2})", DPIndirect, STA, false, 2, 5)); //92
		Disasm.Add(new("sta", "(sr,s),y", SRIndirectIndexedY, STA, false, 2, 7)); //93
		Disasm.Add(new("sty", "${0:x2},x", DPIndexedX, STY, false, 2, 4)); //94
		Disasm.Add(new("sta", "_${0:x2}_x", DPIndexedX, STA, false, 2, 4)); //95
		Disasm.Add(new("stx", "${0:x2},y", DPIndexedY, STX, false, 2, 4)); //96
		Disasm.Add(new("sta", "[${0:x2}],y", DPIndirectLongIndexedY, STA, false, 2, 6)); //97
		Disasm.Add(new("tya", "", Implied, TYA, false, 1, 2)); //98
		Disasm.Add(new("sta", "${0:x4},y", AbsoluteIndexedY, STA, false, 3, 5)); //99
		Disasm.Add(new("txs", "", Implied, TXS, false, 1, 2)); //9A
		Disasm.Add(new("txy", "", Implied, TXY, false, 1, 2)); //9B
		Disasm.Add(new("stz", "${0:x4}", Absolute, STZ, false, 3, 4)); //9C
		Disasm.Add(new("sta", "${0:x4},x", AbsoluteIndexedX, STA, false, 3, 5)); //9D
		Disasm.Add(new("stz", "${0:x4},x", AbsoluteIndexedX, STZ, false, 3, 5)); //9E
		Disasm.Add(new("sta", " ${0:x6},x", AbsoluteLongIndexedX, STA, false, 4, 5)); //9F
		Disasm.Add(new("ldy", "#${0:x2}", ImmediateIndex, LDY, true, 2, 2)); //A0
		Disasm.Add(new("lda", "(${0:x2},x)", DPIndexedIndirectX, LDA, false, 2, 6)); //A1
		Disasm.Add(new("ldx", "#${0:x2}", ImmediateIndex, LDX, true, 2, 2)); //A2
		Disasm.Add(new("lda", "sr,s", StackRelative, LDA, false, 2, 4)); //A3
		Disasm.Add(new("ldy", "${0:x2}", DirectPage, LDY, false, 2, 3)); //A4
		Disasm.Add(new("lda", "${0:x2}", DirectPage, LDA, false, 2, 3)); //A5
		Disasm.Add(new("ldx", "${0:x2}", DirectPage, LDX, false, 2, 3)); //A6
		Disasm.Add(new("lda", "[${0:x2}]", DPIndirectLong, LDA, false, 2, 6)); //A7
		Disasm.Add(new("tay", "", Implied, TAY, false, 1, 2)); //A8
		Disasm.Add(new("lda", "#${0:x2}", ImmediateMemory, LDA, true, 2, 2)); //A9
		Disasm.Add(new("tax", "", Implied, TAX, false, 1, 2)); //AA
		Disasm.Add(new("plb", "", StackPull, PLB, false, 1, 4)); //AB
		Disasm.Add(new("ldy", "${0:x4}", Absolute, LDY, false, 3, 4)); //AC
		Disasm.Add(new("lda", "${0:x4}", Absolute, LDA, false, 3, 4)); //AD
		Disasm.Add(new("ldx", "${0:x4}", Absolute, LDX, false, 3, 4)); //AE
		Disasm.Add(new("lda", "${0:x6}", AbsoluteLong, LDA, false, 4, 5)); //AF
		Disasm.Add(new("bcs", "${0:x4}", ProgramCounterRelative, BCS, false, 2, 2)); //B0
		Disasm.Add(new("lda", "(${0:x2}),y", DPIndirectIndexedY, LDA, false, 2, 5)); //B1
		Disasm.Add(new("lda", "(${0:x2})", DPIndirect, LDA, false, 2, 5)); //B2
		Disasm.Add(new("lda", "(sr,s),y", SRIndirectIndexedY, LDA, false, 2, 7)); //B3
		Disasm.Add(new("ldy", "${0:x2},x", DPIndexedX, LDY, false, 2, 4)); //B4
		Disasm.Add(new("lda", "${0:x2},x", DPIndexedX, LDA, false, 2, 4)); //B5
		Disasm.Add(new("ldx", "${0:x2},y", DPIndexedY, LDX, false, 2, 4)); //B6
		Disasm.Add(new("lda", "[${0:x2}],y", DPIndirectLongIndexedY, LDA, false, 2, 6)); //B7
		Disasm.Add(new("clv", "", Implied, CLV, false, 1, 2)); //B8
		Disasm.Add(new("lda", "${0:x4},y", AbsoluteIndexedY, LDA, false, 3, 4)); //B9
		Disasm.Add(new("tsx", "", Implied, TSX, false, 1, 2)); //BA
		Disasm.Add(new("tyx", "", Implied, TYX, false, 1, 2)); //BB
		Disasm.Add(new("ldy", "${0:x4},x", AbsoluteIndexedX, LDY, false, 3, 4)); //BC
		Disasm.Add(new("lda", "${0:x4},x", AbsoluteIndexedX, LDA, false, 3, 4)); //BD
		Disasm.Add(new("ldx", "${0:x4},y", AbsoluteIndexedY, LDX, false, 3, 4)); //BE
		Disasm.Add(new("lda", " ${0:x6},x", AbsoluteLongIndexedX, LDA, false, 4, 5)); //BF
		Disasm.Add(new("cpy", "#${0:x2}", ImmediateIndex, CPY, true, 2, 2)); //C0
		Disasm.Add(new("cmp", "(${0:x2},x)", DPIndexedIndirectX, CMP, false, 2, 6)); //C1
		Disasm.Add(new("rep", "#${0:x2}", Immediate, REP, true, 2, 3)); //C2
		Disasm.Add(new("cmp", "sr,s", StackRelative, CMP, false, 2, 4)); //C3
		Disasm.Add(new("cpy", "${0:x2}", DirectPage, CPY, false, 2, 3)); //C4
		Disasm.Add(new("cmp", "${0:x2}", DirectPage, CMP, false, 2, 3)); //C5
		Disasm.Add(new("dec", "${0:x2}", DirectPage, DEC, false, 2, 5)); //C6
		Disasm.Add(new("cmp", "[${0:x2}]", DPIndirectLong, CMP, false, 2, 6)); //C7
		Disasm.Add(new("iny", "", Implied, INY, false, 1, 2)); //C8
		Disasm.Add(new("cmp", "#${0:x2}", ImmediateMemory, CMP, true, 2, 2)); //C9
		Disasm.Add(new("dex", "", Implied, DEX, false, 1, 2)); //CA
		Disasm.Add(new("wai", "", Implied, WAI, false, 1, 3)); //CB
		Disasm.Add(new("cpy", "${0:x4}", Absolute, CPY, false, 3, 4)); //CC
		Disasm.Add(new("cmp", "${0:x4}", Absolute, CMP, false, 3, 4)); //CD
		Disasm.Add(new("dec", "${0:x4}", Absolute, DEC, false, 3, 6)); //CE
		Disasm.Add(new("cmp", "${0:x6}", AbsoluteLong, CMP, false, 4, 5)); //CF
		Disasm.Add(new("bne", "${0:x4}", ProgramCounterRelative, BNE, false, 2, 2)); //D0
		Disasm.Add(new("cmp", "(${0:x2}),y", DPIndirectIndexedY, CMP, false, 2, 5)); //D1
		Disasm.Add(new("cmp", "(${0:x2})", DPIndirect, CMP, false, 2, 5)); //D2
		Disasm.Add(new("cmp", "(sr,s),y", SRIndirectIndexedY, CMP, false, 2, 7)); //D3
		Disasm.Add(new("pei", "(${0:x2})", StackDPIndirect, PEI, false, 2, 6)); //D4
		Disasm.Add(new("cmp", "${0:x2},x", DPIndexedX, CMP, false, 2, 4)); //D5
		Disasm.Add(new("dec", "${0:x2},x", DPIndexedX, DEC, false, 2, 6)); //D6
		Disasm.Add(new("cmp", "[${0:x2}],y", DPIndirectLongIndexedY, CMP, false, 2, 6)); //D7
		Disasm.Add(new("cld", "", Implied, CLD, false, 1, 2)); //D8
		Disasm.Add(new("cmp", "${0:x4},y", AbsoluteIndexedY, CMP, false, 3, 4)); //D9
		Disasm.Add(new("phx", "", StackPush, PHX, false, 1, 3)); //DA
		Disasm.Add(new("stp", "", Implied, STP, false, 1, 3)); //DB
		Disasm.Add(new("jml", "[${0:x4}]", AbsoluteIndirectLong, JML, false, 3, 6)); //DC
		Disasm.Add(new("cmp", "${0:x4},x", AbsoluteIndexedX, CMP, false, 3, 4)); //DD
		Disasm.Add(new("dec", "${0:x4},x", AbsoluteIndexedX, DEC, false, 3, 7)); //DE
		Disasm.Add(new("cmp", " ${0:x6},x", AbsoluteLongIndexedX, CMP, false, 4, 5)); //DF
		Disasm.Add(new("cpx", "#${0:x2}", ImmediateIndex, CPX, true, 2, 2)); //E0
		Disasm.Add(new("sbc", "(${0:x2},x)", DPIndexedIndirectX, SBC, false, 2, 6)); //E1
		Disasm.Add(new("sep", "#${0:x2}", Immediate, SEP, true, 2, 3)); //E2
		Disasm.Add(new("sbc", "sr,s", StackRelative, SBC, false, 2, 4)); //E3
		Disasm.Add(new("cpx", "${0:x2}", DirectPage, CPX, false, 2, 3)); //E4
		Disasm.Add(new("sbc", "${0:x2}", DirectPage, SBC, false, 2, 3)); //E5
		Disasm.Add(new("inc", "${0:x2}", DirectPage, INC, false, 2, 5)); //E6
		Disasm.Add(new("sbc", "[${0:x2}]", DPIndirectLong, SBC, false, 2, 6)); //E7
		Disasm.Add(new("inx", "", Implied, INX, false, 1, 2)); //E8
		Disasm.Add(new("sbc", "#${0:x2}", ImmediateMemory, SBC, true, 2, 2)); //E9
		Disasm.Add(new("nop", "", Implied, NOP, false, 1, 2)); //EA
		Disasm.Add(new("xba", "", Implied, XBA, false, 1, 3)); //EB
		Disasm.Add(new("cpx", "${0:x4}", Absolute, CPX, false, 3, 4)); //EC
		Disasm.Add(new("sbc", "${0:x4}", Absolute, SBC, false, 3, 4)); //ED
		Disasm.Add(new("inc", "${0:x4}", Absolute, INC, false, 3, 6)); //EE
		Disasm.Add(new("sbc", "${0:x6}", AbsoluteLong, SBC, false, 4, 5)); //EF
		Disasm.Add(new("beq", "${0:x4}", ProgramCounterRelative, BEQ, false, 2, 2)); //F0
		Disasm.Add(new("sbc", "(${0:x2}),y", DPIndirectIndexedY, SBC, false, 2, 5)); //F1
		Disasm.Add(new("sbc", "(${0:x2})", DPIndirect, SBC, false, 2, 5)); //F2
		Disasm.Add(new("sbc", "(sr,s),y", SRIndirectIndexedY, SBC, false, 2, 7)); //F3
		Disasm.Add(new("pea", "${0:x4}", StackAbsolute, PEA, false, 3, 5)); //F4
		Disasm.Add(new("sbc", "${0:x2},x", DPIndexedX, SBC, false, 2, 4)); //F5
		Disasm.Add(new("inc", "${0:x2},x", DPIndexedX, INC, false, 2, 6)); //F6
		Disasm.Add(new("sbc", "[${0:x2}],y", DPIndirectLongIndexedY, SBC, false, 2, 6)); //F7
		Disasm.Add(new("sed", "", Implied, SED, false, 1, 2)); //F8
		Disasm.Add(new("sbc", "${0:x4},y", AbsoluteIndexedY, SBC, false, 3, 4)); //F9
		Disasm.Add(new("plx", "", StackPull, PLX, false, 1, 4)); //FA
		Disasm.Add(new("xce", "", Implied, XCE, false, 1, 2)); //FB
		Disasm.Add(new("jsr", "(${0:x4},x))", AbsoluteIndexedIndirect, JSR, false, 3, 8)); //FC
		Disasm.Add(new("sbc", "${0:x4},x", AbsoluteIndexedX, SBC, false, 3, 4)); //FD
		Disasm.Add(new("inc", "${0:x4},x", AbsoluteIndexedX, INC, false, 3, 7)); //FE
		Disasm.Add(new("sbc", " ${0:x6},x", AbsoluteLongIndexedX, SBC, false, 4, 5)); //FF
	}

	//Opcodes
	public const int ADC = 0x00;
	public const int AND = 0x01;
	public const int ASL = 0x02;
	public const int BCC = 0x03;
	public const int BCS = 0x04;
	public const int BEQ = 0x05;
	public const int BIT = 0x06;
	public const int BMI = 0x07;
	public const int BNE = 0x08;
	public const int BPL = 0x09;
	public const int BRA = 0x0A;
	public const int BRK = 0x0B;
	public const int BRL = 0x0C;
	public const int BVC = 0x0D;
	public const int BVS = 0x0E;
	public const int CLC = 0x0F;
	public const int CLD = 0x10;
	public const int CLI = 0x11;
	public const int CLV = 0x12;
	public const int CMP = 0x13;
	public const int COP = 0x14;
	public const int CPX = 0x15;
	public const int CPY = 0x16;
	public const int DEC = 0x17;
	public const int DEX = 0x18;
	public const int DEY = 0x19;
	public const int EOR = 0x1A;
	public const int INC = 0x1B;
	public const int INX = 0x1C;
	public const int INY = 0x1D;
	public const int JMP = 0x1E;
	public const int JML = 0x1F;
	public const int JSR = 0x20;
	public const int JSL = 0x21;
	public const int LDA = 0x22;
	public const int LDX = 0x23;
	public const int LDY = 0x24;
	public const int LSR = 0x25;
	public const int MVN = 0x26;
	public const int MVP = 0x27;
	public const int NOP = 0x28;
	public const int ORA = 0x29;
	public const int PEA = 0x2A;
	public const int PEI = 0x2B;
	public const int PER = 0x2C;
	public const int PHA = 0x2D;
	public const int PHB = 0x2E;
	public const int PHD = 0x2F;
	public const int PHK = 0x30;
	public const int PHP = 0x31;
	public const int PHX = 0x32;
	public const int PHY = 0x33;
	public const int PLA = 0x34;
	public const int PLB = 0x35;
	public const int PLD = 0x36;
	public const int PLP = 0x37;
	public const int PLX = 0x38;
	public const int PLY = 0x39;
	public const int REP = 0x3A;
	public const int ROL = 0x3B;
	public const int ROR = 0x3C;
	public const int RTI = 0x3D;
	public const int RTL = 0x3E;
	public const int RTS = 0x3F;
	public const int SBC = 0x40;
	public const int SEC = 0x41;
	public const int SED = 0x42;
	public const int SEI = 0x43;
	public const int SEP = 0x44;
	public const int STA = 0x45;
	public const int STP = 0x46;
	public const int STX = 0x47;
	public const int STY = 0x48;
	public const int STZ = 0x49;
	public const int TAX = 0x4A;
	public const int TAY = 0x4B;
	public const int TCD = 0x4C;
	public const int TCS = 0x4D;
	public const int TDC = 0x4E;
	public const int TRB = 0x4F;
	public const int TSB = 0x50;
	public const int TSC = 0x51;
	public const int TSX = 0x52;
	public const int TXA = 0x53;
	public const int TXS = 0x54;
	public const int TXY = 0x55;
	public const int TYA = 0x56;
	public const int TYX = 0x57;
	public const int WAI = 0x58;
	public const int WDM = 0x59;
	public const int XBA = 0x5A;
	public const int XCE = 0x5B;

	//AddrMode
	public const int ImmediateMemory = 0x00;
	public const int ImmediateIndex = 0x01;
	public const int DPIndexedIndirectX = 0x02;
	public const int StackRelative = 0x03;
	public const int DirectPage = 0x04;
	public const int DPIndirectLong = 0x05;
	public const int Absolute = 0x06;
	public const int AbsoluteLong = 0x07;
	public const int DPIndirectIndexedY = 0x08;
	public const int DPIndirect = 0x09;
	public const int SRIndirectIndexedY = 0x0A;
	public const int DPIndexedX = 0x0B;
	public const int DPIndirectLongIndexedY = 0x0C;
	public const int AbsoluteIndexedY = 0x0D;
	public const int AbsoluteIndexedX = 0x0E;
	public const int AbsoluteLongIndexedX = 0x0F;
	public const int Accumulator = 0x10;
	public const int ProgramCounterRelative = 0x11;
	public const int StackInterrupt = 0x12;
	public const int ProgramCounterRelativeLong = 0x13;
	public const int Implied = 0x14;
	public const int AbsoluteIndirect = 0x15;
	public const int AbsoluteIndexedIndirect = 0x16;
	public const int AbsoluteIndirectLong = 0x17;
	public const int DPIndexedY = 0x18;
	public const int BlockMove = 0x19;
	public const int StackAbsolute = 0x1A;
	public const int StackDPIndirect = 0x1B;
	public const int StackPCRelativeLong = 0x1C;
	public const int StackPush = 0x1D;
	public const int StackPull = 0x1E;
	public const int Immediate = 0x1F;
	public const int StackRTI = 0x20;
	public const int StackRTL = 0x21;
	public const int StackRTS = 0x22;
	public const int NoMode = 0x23;
}